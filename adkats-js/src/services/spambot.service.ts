import type { Logger } from '../core/logger.js';
import type { Scheduler } from '../core/scheduler.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { PlayerService } from './player.service.js';
import type { APlayer } from '../models/player.js';

/**
 * Message type for SpamBot.
 */
export type SpamBotMessageType = 'say' | 'yell' | 'tell';

/**
 * A configured message list.
 */
interface MessageList {
  type: SpamBotMessageType;
  messages: string[];
  currentIndex: number;
  delaySeconds: number;
  lastPostTime: Date;
}

/**
 * Server information for placeholder substitution.
 */
export interface ServerInfo {
  serverName: string;
  mapName: string;
  modeName: string;
  playerCount: number;
  maxPlayers: number;
  ticketCount1: number;
  ticketCount2: number;
  roundNumber: number;
  roundsTotal: number;
}

/**
 * Configuration for the SpamBot service.
 */
export interface SpamBotConfig {
  /** Enable/disable SpamBot */
  enabled: boolean;
  /** Messages to broadcast via say (global chat) */
  sayMessages: string[];
  /** Delay between say messages (seconds) */
  sayDelaySeconds: number;
  /** Messages to broadcast via yell (big screen) */
  yellMessages: string[];
  /** Delay between yell messages (seconds) */
  yellDelaySeconds: number;
  /** Messages to send via tell (private message to each player) */
  tellMessages: string[];
  /** Delay between tell messages (seconds) */
  tellDelaySeconds: number;
  /** Exclude admins and whitelisted players from messages */
  excludeAdminsAndWhitelist: boolean;
  /** Yell duration in seconds */
  yellDurationSeconds: number;
  /** Minimum players required to send messages */
  minimumPlayers: number;
  /** Check interval (ms) - how often to check if messages should be sent */
  checkIntervalMs: number;
}

/**
 * Callback to check if a player is an admin or whitelisted.
 */
export type AdminWhitelistChecker = (player: APlayer) => Promise<boolean>;

/**
 * Callback to get current server information.
 */
export type ServerInfoProvider = () => ServerInfo;

/**
 * SpamBotService - sends automated server messages.
 *
 * Supports three message types:
 * - say: Global chat messages
 * - yell: Big screen messages (appears on everyone's screen)
 * - tell: Private messages to each player
 *
 * Messages cycle sequentially through their lists and support
 * placeholder variables for dynamic content.
 */
export class SpamBotService {
  private logger: Logger;
  private scheduler: Scheduler;
  private bcAdapter: BattleConAdapter;
  private playerService: PlayerService;
  private config: SpamBotConfig;
  private adminWhitelistChecker: AdminWhitelistChecker | null = null;
  private serverInfoProvider: ServerInfoProvider | null = null;

  // Message lists
  private sayList: MessageList;
  private yellList: MessageList;
  private tellList: MessageList;

  // Job ID for scheduler
  private readonly SPAMBOT_JOB_ID = 'spambot-check';

  constructor(
    logger: Logger,
    scheduler: Scheduler,
    bcAdapter: BattleConAdapter,
    playerService: PlayerService,
    config: SpamBotConfig
  ) {
    this.logger = logger;
    this.scheduler = scheduler;
    this.bcAdapter = bcAdapter;
    this.playerService = playerService;
    this.config = config;

    // Initialize message lists
    const now = new Date();
    const pastTime = new Date(now.getTime() - config.sayDelaySeconds * 1000);

    this.sayList = {
      type: 'say',
      messages: [...config.sayMessages],
      currentIndex: 0,
      delaySeconds: config.sayDelaySeconds,
      lastPostTime: pastTime,
    };

    this.yellList = {
      type: 'yell',
      messages: [...config.yellMessages],
      currentIndex: 0,
      delaySeconds: config.yellDelaySeconds,
      lastPostTime: new Date(now.getTime() - config.yellDelaySeconds * 1000),
    };

    this.tellList = {
      type: 'tell',
      messages: [...config.tellMessages],
      currentIndex: 0,
      delaySeconds: config.tellDelaySeconds,
      lastPostTime: new Date(now.getTime() - config.tellDelaySeconds * 1000),
    };
  }

  /**
   * Initialize the SpamBot service.
   */
  initialize(): void {
    if (!this.config.enabled) {
      this.logger.info('SpamBot is disabled');
      return;
    }

    // Register scheduled job
    this.scheduler.registerIntervalJob(
      this.SPAMBOT_JOB_ID,
      'SpamBot Message Check',
      this.config.checkIntervalMs,
      () => this.processMessages()
    );

    this.logger.info({
      sayCount: this.sayList.messages.length,
      yellCount: this.yellList.messages.length,
      tellCount: this.tellList.messages.length,
      sayDelay: this.config.sayDelaySeconds,
      yellDelay: this.config.yellDelaySeconds,
      tellDelay: this.config.tellDelaySeconds,
    }, 'SpamBot service initialized');
  }

  /**
   * Set admin/whitelist checker for message exclusions.
   */
  setAdminWhitelistChecker(checker: AdminWhitelistChecker): void {
    this.adminWhitelistChecker = checker;
  }

  /**
   * Set server info provider for placeholder substitution.
   */
  setServerInfoProvider(provider: ServerInfoProvider): void {
    this.serverInfoProvider = provider;
  }

  /**
   * Process all message lists.
   */
  private async processMessages(): Promise<void> {
    const playerCount = this.playerService.getOnlinePlayerCount();

    // Don't send messages if below minimum player count
    if (playerCount < this.config.minimumPlayers) {
      return;
    }

    // Process each message type
    await this.processMessageList(this.sayList);
    await this.processMessageList(this.yellList);
    await this.processMessageList(this.tellList);
  }

  /**
   * Process a single message list.
   */
  private async processMessageList(list: MessageList): Promise<void> {
    if (list.messages.length === 0) {
      return;
    }

    const now = new Date();
    const timeSinceLastPost = (now.getTime() - list.lastPostTime.getTime()) / 1000;

    // Check if enough time has passed
    if (timeSinceLastPost < list.delaySeconds) {
      return;
    }

    // Find a valid message (skip map/mode specific messages that don't match)
    let message: string | null = null;
    let attempts = 0;

    while (attempts < list.messages.length) {
      const candidateMessage = list.messages[list.currentIndex];
      if (candidateMessage !== undefined) {
        const validatedMessage = this.validateMessage(candidateMessage);

        if (validatedMessage) {
          message = validatedMessage;
          break;
        }
      }

      // Move to next message
      list.currentIndex = (list.currentIndex + 1) % list.messages.length;
      attempts++;
    }

    if (!message) {
      return;
    }

    // Replace placeholders
    message = this.replacePlaceholders(message);

    // Send the message
    try {
      await this.sendMessage(list.type, message);
      list.lastPostTime = now;

      // Move to next message for next time
      list.currentIndex = (list.currentIndex + 1) % list.messages.length;

      this.logger.debug({
        type: list.type,
        message: message.substring(0, 50),
      }, 'SpamBot message sent');
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, type: list.type }, 'Failed to send SpamBot message');
    }
  }

  /**
   * Validate a message against current map/mode restrictions.
   * Returns the processed message or null if it should be skipped.
   */
  private validateMessage(message: string): string | null {
    // Check for map/mode prefixes (e.g., "Conquest/Message here" or "Operation Locker/Message")
    const serverInfo = this.serverInfoProvider?.();

    if (!serverInfo) {
      // No server info, just remove any prefix patterns and return
      return this.removeMapModePrefix(message);
    }

    // Check for map prefix
    const mapPrefixMatch = message.match(/^([^/]+)\//);
    if (mapPrefixMatch) {
      const prefix = mapPrefixMatch[1];
      if (prefix) {
        const prefixLower = prefix.toLowerCase();

        // Check if prefix is a map name
        if (this.isMapName(prefixLower)) {
          if (!serverInfo.mapName.toLowerCase().includes(prefixLower)) {
            return null; // Map doesn't match, skip message
          }
          // Map matches, remove prefix
          message = message.substring(mapPrefixMatch[0].length);
        }

        // Check if prefix is a mode name
        else if (this.isModeName(prefixLower)) {
          if (!serverInfo.modeName.toLowerCase().includes(prefixLower)) {
            return null; // Mode doesn't match, skip message
          }
          // Mode matches, remove prefix
          message = message.substring(mapPrefixMatch[0].length);
        }
      }
    }

    // Check for a second prefix (mode/map can be in either order)
    const secondMatch = message.match(/^([^/]+)\//);
    if (secondMatch) {
      const prefix = secondMatch[1];
      if (prefix) {
        const prefixLower = prefix.toLowerCase();

        if (this.isMapName(prefixLower)) {
          if (!serverInfo.mapName.toLowerCase().includes(prefixLower)) {
            return null;
          }
          message = message.substring(secondMatch[0].length);
        } else if (this.isModeName(prefixLower)) {
          if (!serverInfo.modeName.toLowerCase().includes(prefixLower)) {
            return null;
          }
          message = message.substring(secondMatch[0].length);
        }
      }
    }

    return message.trim();
  }

  /**
   * Remove any map/mode prefix from a message.
   */
  private removeMapModePrefix(message: string): string {
    // Remove up to two prefixes
    let result = message;
    for (let i = 0; i < 2; i++) {
      const match = result.match(/^([^/]+)\//);
      if (match && match[1] && (this.isMapName(match[1].toLowerCase()) || this.isModeName(match[1].toLowerCase()))) {
        result = result.substring(match[0].length);
      } else {
        break;
      }
    }
    return result.trim();
  }

  /**
   * Check if a string is a known map name.
   */
  private isMapName(name: string): boolean {
    const maps = [
      'metro', 'locker', 'siege', 'golmud', 'paracel', 'lancang', 'flood',
      'zavod', 'dawnbreaker', 'propaganda', 'lumphini', 'pearl', 'dragon',
      'caspian', 'firestorm', 'canals', 'bazaar', 'tehran', 'damavand',
      'kharg', 'noshar', 'seine', 'armored', 'sharqi', 'gulf', 'wake',
    ];
    return maps.some(m => name.includes(m));
  }

  /**
   * Check if a string is a known mode name.
   */
  private isModeName(name: string): boolean {
    const modes = [
      'conquest', 'rush', 'tdm', 'team deathmatch', 'domination', 'obliteration',
      'defuse', 'ctf', 'capture the flag', 'chainlink', 'carrier assault',
      'air superiority', 'gunmaster', 'squad deathmatch', 'scavenger',
    ];
    return modes.some(m => name.includes(m));
  }

  /**
   * Replace placeholders in a message.
   */
  private replacePlaceholders(message: string): string {
    const serverInfo = this.serverInfoProvider?.();
    const playerCount = this.playerService.getOnlinePlayerCount();

    const replacements: Record<string, string> = {
      '%server_name%': serverInfo?.serverName ?? 'Server',
      '%player_count%': String(playerCount),
      '%max_players%': String(serverInfo?.maxPlayers ?? 64),
      '%map_name%': serverInfo?.mapName ?? 'Unknown',
      '%mode_name%': serverInfo?.modeName ?? 'Unknown',
      '%ticket_count_1%': String(serverInfo?.ticketCount1 ?? 0),
      '%ticket_count_2%': String(serverInfo?.ticketCount2 ?? 0),
      '%round_number%': String(serverInfo?.roundNumber ?? 1),
      '%rounds_total%': String(serverInfo?.roundsTotal ?? 1),
      '%slots_available%': String((serverInfo?.maxPlayers ?? 64) - playerCount),
    };

    let result = message;
    for (const [placeholder, value] of Object.entries(replacements)) {
      result = result.replace(new RegExp(placeholder, 'gi'), value);
    }

    return result;
  }

  /**
   * Send a message of the specified type.
   */
  private async sendMessage(type: SpamBotMessageType, message: string): Promise<void> {
    const playerCount = this.playerService.getOnlinePlayerCount();
    const fullMessage = `[SpamBot] ${message}`;

    if (this.config.excludeAdminsAndWhitelist && this.adminWhitelistChecker) {
      // Send to non-admin/whitelist players only
      const players = this.playerService.getAllOnlinePlayers();

      for (const player of players) {
        const isExempt = await this.adminWhitelistChecker(player);
        if (isExempt) {
          continue;
        }

        switch (type) {
          case 'say':
            await this.bcAdapter.sayPlayer(fullMessage, player.soldierName);
            break;
          case 'yell':
            await this.bcAdapter.yellPlayer(fullMessage, player.soldierName, this.config.yellDurationSeconds);
            break;
          case 'tell':
            // Tell is same as private say
            await this.bcAdapter.sayPlayer(fullMessage, player.soldierName);
            break;
        }
      }
    } else {
      // Send to everyone
      switch (type) {
        case 'say':
          await this.bcAdapter.say(fullMessage);
          break;
        case 'yell':
          await this.bcAdapter.yell(fullMessage, this.config.yellDurationSeconds);
          break;
        case 'tell':
          // Tell goes to each player individually
          const players = this.playerService.getAllOnlinePlayers();
          for (const player of players) {
            await this.bcAdapter.sayPlayer(fullMessage, player.soldierName);
          }
          break;
      }
    }
  }

  /**
   * Add a message to a list.
   */
  addMessage(type: SpamBotMessageType, message: string): void {
    const list = this.getMessageList(type);
    list.messages.push(message);

    this.logger.debug({ type, message }, 'Added SpamBot message');
  }

  /**
   * Remove a message from a list by index.
   */
  removeMessage(type: SpamBotMessageType, index: number): boolean {
    const list = this.getMessageList(type);

    if (index < 0 || index >= list.messages.length) {
      return false;
    }

    list.messages.splice(index, 1);

    // Adjust current index if needed
    if (list.currentIndex >= list.messages.length) {
      list.currentIndex = 0;
    }

    this.logger.debug({ type, index }, 'Removed SpamBot message');
    return true;
  }

  /**
   * Clear all messages from a list.
   */
  clearMessages(type: SpamBotMessageType): void {
    const list = this.getMessageList(type);
    list.messages = [];
    list.currentIndex = 0;

    this.logger.debug({ type }, 'Cleared SpamBot messages');
  }

  /**
   * Get messages for a specific type.
   */
  getMessages(type: SpamBotMessageType): string[] {
    return [...this.getMessageList(type).messages];
  }

  /**
   * Set the delay for a message type.
   */
  setDelay(type: SpamBotMessageType, delaySeconds: number): void {
    const list = this.getMessageList(type);
    list.delaySeconds = delaySeconds;

    this.logger.debug({ type, delaySeconds }, 'Updated SpamBot delay');
  }

  /**
   * Get the appropriate message list for a type.
   */
  private getMessageList(type: SpamBotMessageType): MessageList {
    switch (type) {
      case 'say':
        return this.sayList;
      case 'yell':
        return this.yellList;
      case 'tell':
        return this.tellList;
    }
  }

  /**
   * Force send a message immediately (bypasses delay).
   */
  async sendImmediate(type: SpamBotMessageType, message: string): Promise<void> {
    const processedMessage = this.replacePlaceholders(message);
    await this.sendMessage(type, processedMessage);
  }

  /**
   * Get status of all message lists.
   */
  getStatus(): {
    sayMessages: number;
    sayDelay: number;
    sayTimeSincePost: number;
    yellMessages: number;
    yellDelay: number;
    yellTimeSincePost: number;
    tellMessages: number;
    tellDelay: number;
    tellTimeSincePost: number;
  } {
    const now = new Date();

    return {
      sayMessages: this.sayList.messages.length,
      sayDelay: this.sayList.delaySeconds,
      sayTimeSincePost: (now.getTime() - this.sayList.lastPostTime.getTime()) / 1000,
      yellMessages: this.yellList.messages.length,
      yellDelay: this.yellList.delaySeconds,
      yellTimeSincePost: (now.getTime() - this.yellList.lastPostTime.getTime()) / 1000,
      tellMessages: this.tellList.messages.length,
      tellDelay: this.tellList.delaySeconds,
      tellTimeSincePost: (now.getTime() - this.tellList.lastPostTime.getTime()) / 1000,
    };
  }

  /**
   * Enable the SpamBot.
   */
  enable(): void {
    this.scheduler.enableJob(this.SPAMBOT_JOB_ID);
    this.logger.info('SpamBot enabled');
  }

  /**
   * Disable the SpamBot.
   */
  disable(): void {
    this.scheduler.disableJob(this.SPAMBOT_JOB_ID);
    this.logger.info('SpamBot disabled');
  }
}

/**
 * Create default SpamBot configuration.
 */
export function createDefaultSpamBotConfig(): SpamBotConfig {
  return {
    enabled: false,
    sayMessages: [],
    sayDelaySeconds: 300,
    yellMessages: [],
    yellDelaySeconds: 600,
    tellMessages: [],
    tellDelaySeconds: 900,
    excludeAdminsAndWhitelist: false,
    yellDurationSeconds: 5,
    minimumPlayers: 1,
    checkIntervalMs: 500,
  };
}

/**
 * Create a new SpamBot service.
 */
export function createSpambotService(
  logger: Logger,
  scheduler: Scheduler,
  bcAdapter: BattleConAdapter,
  playerService: PlayerService,
  config: SpamBotConfig
): SpamBotService {
  return new SpamBotService(logger, scheduler, bcAdapter, playerService, config);
}
