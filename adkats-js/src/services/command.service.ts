import type { RowDataPacket } from 'mysql2/promise';
import type { Logger } from '../core/logger.js';
import type { Database } from '../database/connection.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { PlayerService } from './player.service.js';
import type { ACommand, CommandDbRow } from '../models/command.js';
import type { ARecord } from '../models/record.js';
import type { APlayer } from '../models/player.js';
import type { ARole } from '../models/role.js';
import type { ParsedChat } from '../handlers/player-chat.handler.js';
import type { AdKatsConfig } from '../core/config.js';

import { commandFromDbRow, isCommandEnabled } from '../models/command.js';
import { createRecord, RecordSource } from '../models/record.js';
import { levenshteinDistance } from '../utils/levenshtein.js';

/**
 * Command execution context.
 */
export interface CommandContext {
  record: ARecord;
  player: APlayer;
  targetPlayer: APlayer | null;
  args: string | null;
  respond: (message: string) => Promise<void>;
  respondError: (message: string) => Promise<void>;
}

/**
 * Command handler function type.
 */
export type CommandHandler = (ctx: CommandContext) => Promise<void>;

/**
 * Command registration.
 */
interface RegisteredCommand {
  command: ACommand;
  handler: CommandHandler;
}

/**
 * Service for managing and executing commands.
 */
export class CommandService {
  private logger: Logger;
  private db: Database;
  private eventBus: AdKatsEventBus;
  private playerService: PlayerService;
  private config: AdKatsConfig;
  private serverId: number;

  // Command registries
  private commandsByKey: Map<string, ACommand> = new Map();
  private commandsByText: Map<string, ACommand> = new Map();
  private commandsById: Map<number, ACommand> = new Map();

  // Command handlers
  private handlers: Map<string, CommandHandler> = new Map();

  // User roles cache
  private userRoles: Map<number, ARole> = new Map();
  private playerUserIds: Map<number, number> = new Map();

  // Pending confirmations
  private pendingConfirmations: Map<string, ARecord> = new Map();

  constructor(
    logger: Logger,
    db: Database,
    eventBus: AdKatsEventBus,
    playerService: PlayerService,
    config: AdKatsConfig,
    serverId: number
  ) {
    this.logger = logger;
    this.db = db;
    this.eventBus = eventBus;
    this.playerService = playerService;
    this.config = config;
    this.serverId = serverId;
  }

  /**
   * Initialize the command service by loading commands from database.
   */
  async initialize(): Promise<void> {
    await this.loadCommands();
    await this.loadRoles();
    this.logger.info({ commandCount: this.commandsByKey.size }, 'Command service initialized');
  }

  /**
   * Load commands from database.
   */
  private async loadCommands(): Promise<void> {
    const rows = await this.db.query<(CommandDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_commands WHERE command_active != 'Disabled'`
    );

    this.commandsByKey.clear();
    this.commandsByText.clear();
    this.commandsById.clear();

    for (const row of rows) {
      const command = commandFromDbRow(row);
      this.commandsByKey.set(command.commandKey, command);
      this.commandsByText.set(command.commandText.toLowerCase(), command);
      this.commandsById.set(command.commandId, command);
    }

    this.logger.debug({ count: rows.length }, 'Loaded commands from database');
  }

  /**
   * Load roles and their command permissions from database.
   */
  private async loadRoles(): Promise<void> {
    // Load roles
    const roleRows = await this.db.query<(RowDataPacket & {
      role_id: number;
      role_key: string;
      role_name: string;
    })[]>(
      `SELECT * FROM adkats_roles`
    );

    // Load role-command mappings
    const roleCommandRows = await this.db.query<(RowDataPacket & {
      role_id: number;
      command_id: number;
    })[]>(
      `SELECT * FROM adkats_rolecommands`
    );

    this.userRoles.clear();

    for (const roleRow of roleRows) {
      const role: ARole = {
        roleId: roleRow.role_id,
        roleKey: roleRow.role_key,
        roleName: roleRow.role_name,
        allowedCommands: new Set(),
      };

      // Add allowed commands for this role
      for (const rc of roleCommandRows) {
        if (rc.role_id === role.roleId) {
          role.allowedCommands.add(rc.command_id);
        }
      }

      this.userRoles.set(role.roleId, role);
    }

    this.logger.debug({ count: roleRows.length }, 'Loaded roles from database');
  }

  /**
   * Register a command handler.
   */
  registerHandler(commandKey: string, handler: CommandHandler): void {
    this.handlers.set(commandKey, handler);
    this.logger.debug({ commandKey }, 'Registered command handler');
  }

  /**
   * Get a command by its text (what users type).
   */
  getCommandByText(text: string): ACommand | undefined {
    return this.commandsByText.get(text.toLowerCase());
  }

  /**
   * Get a command by its key.
   */
  getCommandByKey(key: string): ACommand | undefined {
    return this.commandsByKey.get(key);
  }

  /**
   * Get a command by its ID.
   */
  getCommandById(id: number): ACommand | undefined {
    return this.commandsById.get(id);
  }

  /**
   * Find a command using fuzzy matching.
   */
  findCommandFuzzy(text: string, maxDistance: number = 2): ACommand | null {
    const lowerText = text.toLowerCase();

    // First try exact match
    const exact = this.commandsByText.get(lowerText);
    if (exact) {
      return exact;
    }

    // Try fuzzy match
    let bestMatch: ACommand | null = null;
    let bestDistance = maxDistance + 1;

    for (const [cmdText, command] of this.commandsByText) {
      if (!isCommandEnabled(command)) {
        continue;
      }

      const distance = levenshteinDistance(lowerText, cmdText);
      if (distance < bestDistance) {
        bestDistance = distance;
        bestMatch = command;
      }
    }

    return bestMatch;
  }

  /**
   * Parse and execute a command from chat.
   */
  async executeFromChat(chat: ParsedChat): Promise<void> {
    if (!chat.isCommand || !chat.commandText) {
      return;
    }

    // Handle confirmation/cancellation
    if (chat.commandText === 'yes' || chat.commandText === 'no') {
      await this.handleConfirmation(chat.player, chat.commandText === 'yes');
      return;
    }

    // Find the command
    const command = this.findCommandFuzzy(chat.commandText);
    if (!command) {
      this.logger.debug({ text: chat.commandText }, 'Unknown command');
      return;
    }

    // Check if command is enabled
    if (!isCommandEnabled(command)) {
      this.logger.debug({ command: command.commandKey }, 'Command is disabled');
      return;
    }

    // Check permissions
    const hasPermission = await this.checkPermission(chat.player, command);
    if (!hasPermission) {
      this.logger.debug({
        player: chat.player.soldierName,
        command: command.commandKey,
      }, 'Player lacks permission');
      // Don't respond to avoid exposing command existence
      return;
    }

    // Parse target player if command requires one
    let targetPlayer: APlayer | null = null;
    let remainingArgs = chat.commandArgs;

    if (command.commandPlayerInteraction && chat.commandArgs) {
      const parsed = this.parseTargetPlayer(chat.commandArgs);
      targetPlayer = parsed.player;
      remainingArgs = parsed.remainingArgs;

      if (!targetPlayer && command.commandPlayerInteraction) {
        // Player interaction required but no valid target found
        this.logger.debug({ args: chat.commandArgs }, 'Could not find target player');
        return;
      }
    }

    // Create the record
    const record = createRecord(
      this.serverId,
      chat.player,
      targetPlayer,
      command,
      remainingArgs ?? ''
    );
    record.externalSource = RecordSource.InGame;

    // Execute the command
    await this.executeCommand(record, chat.player, remainingArgs);
  }

  /**
   * Execute a command.
   */
  async executeCommand(
    record: ARecord,
    sourcePlayer: APlayer,
    args: string | null
  ): Promise<void> {
    const command = record.command;
    if (!command) {
      this.logger.error({ recordId: record.recordId }, 'Record has no command');
      return;
    }

    // Get the handler
    const handler = this.handlers.get(command.commandKey);
    if (!handler) {
      this.logger.warn({ commandKey: command.commandKey }, 'No handler registered for command');
      return;
    }

    // Create execution context
    const ctx: CommandContext = {
      record,
      player: sourcePlayer,
      targetPlayer: record.targetPlayer,
      args,
      respond: async (message: string) => {
        // TODO: Send response via BattleCon
        this.logger.info({ player: sourcePlayer.soldierName, message }, 'Command response');
      },
      respondError: async (message: string) => {
        // TODO: Send error response via BattleCon
        this.logger.warn({ player: sourcePlayer.soldierName, message }, 'Command error response');
      },
    };

    try {
      // Emit pre-execution event
      this.eventBus.emitEvent('command:parsed', record);

      // Execute the handler
      await handler(ctx);

      // Emit post-execution event
      this.eventBus.emitEvent('command:executed', record);

      this.logger.info({
        command: command.commandText,
        source: sourcePlayer.soldierName,
        target: record.targetPlayer?.soldierName,
      }, 'Command executed');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({
        command: command.commandKey,
        error: msg,
      }, 'Command execution failed');

      this.eventBus.emitEvent('command:failed', record, error instanceof Error ? error : new Error(msg));
    }
  }

  /**
   * Parse target player from command arguments.
   */
  private parseTargetPlayer(args: string): { player: APlayer | null; remainingArgs: string | null } {
    const parts = args.trim().split(/\s+/);
    if (parts.length === 0) {
      return { player: null, remainingArgs: null };
    }

    const playerSearch = parts[0]!;
    const remainingArgs = parts.slice(1).join(' ') || null;

    // Try to find the player
    const result = this.playerService.findOnlinePlayerByPartialName(playerSearch);

    if (result === null) {
      // Try fuzzy match
      const fuzzyMatch = this.playerService.findOnlinePlayerFuzzy(playerSearch);
      return { player: fuzzyMatch, remainingArgs };
    }

    if (Array.isArray(result)) {
      // Multiple matches - take the first one or require more specific input
      // For now, take the first match
      return { player: result[0] ?? null, remainingArgs };
    }

    return { player: result, remainingArgs };
  }

  /**
   * Check if a player has permission to use a command.
   */
  async checkPermission(player: APlayer, command: ACommand): Promise<boolean> {
    // Guest role (ID 1) has basic commands
    const guestRole = this.userRoles.get(1);
    if (guestRole?.allowedCommands.has(command.commandId)) {
      return true;
    }

    // Check if player has a user account
    const userId = await this.getPlayerUserId(player.playerId);
    if (!userId) {
      return false; // Only guest commands available
    }

    // Get user's role
    const userRole = await this.getUserRole(userId);
    if (!userRole) {
      return false;
    }

    return userRole.allowedCommands.has(command.commandId);
  }

  /**
   * Get the user ID for a player.
   */
  private async getPlayerUserId(playerId: number): Promise<number | null> {
    // Check cache
    const cached = this.playerUserIds.get(playerId);
    if (cached !== undefined) {
      return cached;
    }

    // Query database
    const row = await this.db.queryOne<{ user_id: number } & RowDataPacket>(
      `SELECT user_id FROM adkats_usersoldiers WHERE player_id = ?`,
      [playerId]
    );

    const userId = row?.user_id ?? null;
    if (userId) {
      this.playerUserIds.set(playerId, userId);
    }

    return userId;
  }

  /**
   * Get a user's role.
   */
  private async getUserRole(userId: number): Promise<ARole | null> {
    const row = await this.db.queryOne<{ user_role: number } & RowDataPacket>(
      `SELECT user_role FROM adkats_users WHERE user_id = ?`,
      [userId]
    );

    if (!row) {
      return null;
    }

    return this.userRoles.get(row.user_role) ?? null;
  }

  /**
   * Set a pending confirmation for a player.
   */
  setPendingConfirmation(player: APlayer, record: ARecord): void {
    this.pendingConfirmations.set(player.soldierName, record);

    // Auto-expire after 30 seconds
    setTimeout(() => {
      if (this.pendingConfirmations.get(player.soldierName) === record) {
        this.pendingConfirmations.delete(player.soldierName);
      }
    }, 30000);
  }

  /**
   * Handle confirmation/cancellation.
   */
  private async handleConfirmation(player: APlayer, confirmed: boolean): Promise<void> {
    const pending = this.pendingConfirmations.get(player.soldierName);
    if (!pending) {
      return; // No pending command
    }

    this.pendingConfirmations.delete(player.soldierName);

    if (confirmed) {
      pending.isConfirmed = true;
      await this.executeCommand(pending, player, pending.recordMessage);
    } else {
      pending.isCancelled = true;
      this.logger.debug({ player: player.soldierName }, 'Command cancelled by user');
    }
  }

  /**
   * Get all available commands for a player (for help display).
   */
  async getAvailableCommands(player: APlayer): Promise<ACommand[]> {
    const available: ACommand[] = [];

    for (const command of this.commandsByKey.values()) {
      if (!isCommandEnabled(command)) {
        continue;
      }

      if (command.commandActive === 'Invisible') {
        continue; // Don't show invisible commands
      }

      const hasPermission = await this.checkPermission(player, command);
      if (hasPermission) {
        available.push(command);
      }
    }

    return available.sort((a, b) => a.commandText.localeCompare(b.commandText));
  }
}

/**
 * Create a new command service.
 */
export function createCommandService(
  logger: Logger,
  db: Database,
  eventBus: AdKatsEventBus,
  playerService: PlayerService,
  config: AdKatsConfig,
  serverId: number
): CommandService {
  return new CommandService(logger, db, eventBus, playerService, config, serverId);
}
