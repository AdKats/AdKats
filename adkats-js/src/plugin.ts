import type { Logger } from './core/logger.js';
import type { AdKatsConfig } from './core/config.js';
import type { AdKatsEventBus } from './core/event-bus.js';
import type { Scheduler } from './core/scheduler.js';
import type { Database } from './database/connection.js';
import type { BattleConAdapter, BattleConClient } from './core/battlecon-adapter.js';
import type { PlayerService } from './services/player.service.js';
import type { CommandService } from './services/command.service.js';
import type { BanService } from './services/ban.service.js';
import type { InfractionService } from './services/infraction.service.js';
import type { TeamService } from './services/team.service.js';
import type { ReportService } from './services/report.service.js';
import type { AntiCheatService } from './services/anticheat.service.js';
import type { ReputationService } from './services/reputation.service.js';
import type { AfkService } from './services/afk.service.js';
import type { PingService } from './services/ping.service.js';
import type { SpamBotService } from './services/spambot.service.js';
import type { SpecialPlayerService } from './services/specialplayer.service.js';
import type { APlayer } from './models/player.js';
import type { DiscordService } from './integrations/discord.js';
import type { BattlelogClient, BattlelogGameVersion } from './integrations/battlelog.js';
import type { RecordRepository } from './database/repositories/record.repository.js';

import { createLogger, createChildLogger } from './core/logger.js';
import { createEventBus } from './core/event-bus.js';
import { createScheduler } from './core/scheduler.js';
import { createDatabase } from './database/connection.js';
import { createBattleConAdapter } from './core/battlecon-adapter.js';

// Repositories
import { createPlayerRepository } from './database/repositories/player.repository.js';
import { createBanRepository } from './database/repositories/ban.repository.js';
import { createRecordRepository } from './database/repositories/record.repository.js';
import { createInfractionRepository } from './database/repositories/infraction.repository.js';
import { createSpecialPlayerRepository } from './database/repositories/specialplayer.repository.js';

// Services
import { createPlayerService } from './services/player.service.js';
import { createCommandService } from './services/command.service.js';
import { createBanService } from './services/ban.service.js';
import { createInfractionService } from './services/infraction.service.js';
import { createTeamService } from './services/team.service.js';
import { createReportService } from './services/report.service.js';
import {
  createAntiCheatService,
  type WeaponStatLibrary,
} from './services/anticheat.service.js';
import {
  createReputationService,
  createDefaultReputationConfig,
  type ReputationRepository,
} from './services/reputation.service.js';
import { createAfkService, createDefaultAfkConfig } from './services/afk.service.js';
import { createPingService, createDefaultPingConfig } from './services/ping.service.js';
import { createSpambotService, createDefaultSpamBotConfig } from './services/spambot.service.js';
import { createSpecialPlayerService } from './services/specialplayer.service.js';

// Integrations
import { createDiscordService } from './integrations/discord.js';
import { createBattlelogClient } from './integrations/battlelog.js';

// Commands
import { registerKillCommand } from './commands/admin/kill.command.js';
import { registerKickCommand } from './commands/admin/kick.command.js';
import { registerSayCommands } from './commands/admin/say.command.js';
import { registerBanCommands } from './commands/admin/ban.command.js';
import { registerPunishCommands } from './commands/admin/punish.command.js';
import { registerMoveCommands } from './commands/admin/move.command.js';
import { registerReportAdminCommands } from './commands/admin/report-admin.command.js';
import { registerInfoCommands } from './commands/admin/info.command.js';
import { registerPlayerControlCommands } from './commands/admin/player-control.command.js';
import { registerServerCommands } from './commands/admin/server.command.js';
import { registerWhitelistCommands } from './commands/admin/whitelist.command.js';
import { registerNukeCommands } from './commands/server/nuke.command.js';
import { registerRoundCommands } from './commands/server/round.command.js';
import { registerRulesCommand } from './commands/player/rules.command.js';
import { registerHelpCommand } from './commands/player/help.command.js';
import { registerTeamCommands } from './commands/player/team.command.js';
import { registerReportCommands } from './commands/player/report.command.js';
import { registerVotingCommands, VoteType, type VotingManager } from './commands/player/voting.command.js';
import type { MuteStatus, LockStatus } from './commands/admin/player-control.command.js';
import type { ParsedChat, ChatSubset } from './handlers/player-chat.handler.js';

/**
 * AdKats plugin state.
 */
export type PluginState = 'disabled' | 'starting' | 'enabled' | 'stopping' | 'error';

/**
 * AdKats plugin options.
 */
export interface AdKatsPluginOptions {
  config: AdKatsConfig;
  battlecon: BattleConClient;
  weaponStatLibrary?: WeaponStatLibrary;
}

/**
 * Default empty weapon stat library.
 */
const defaultWeaponStatLibrary: WeaponStatLibrary = {
  BF3: [],
  BF4: [],
  BFHL: [],
  BFBC2: [],
};

/**
 * Main AdKats plugin class.
 * Manages the plugin lifecycle and coordinates all services.
 */
export class AdKatsPlugin {
  // Configuration
  private config: AdKatsConfig;
  private weaponStatLibrary: WeaponStatLibrary;

  // Core components
  private logger: Logger;
  private eventBus: AdKatsEventBus;
  private scheduler: Scheduler;
  private db: Database;

  // BattleCon integration
  private bc: BattleConClient;
  private bcAdapter: BattleConAdapter | null = null;

  // External integrations
  private discordService: DiscordService | null = null;
  private battlelogClient: BattlelogClient | null = null;

  // Services
  private playerService: PlayerService | null = null;
  private commandService: CommandService | null = null;
  private banService: BanService | null = null;
  private infractionService: InfractionService | null = null;
  private teamService: TeamService | null = null;
  private reportService: ReportService | null = null;
  private antiCheatService: AntiCheatService | null = null;
  private reputationService: ReputationService | null = null;
  private afkService: AfkService | null = null;
  private pingService: PingService | null = null;
  private spamBotService: SpamBotService | null = null;
  private specialPlayerService: SpecialPlayerService | null = null;

  // Repositories (keep reference for command registration)
  private recordRepo: RecordRepository | null = null;

  // Voting manager
  private votingManager: VotingManager | null = null;

  // Player state tracking
  private mutedPlayers = new Map<number, MuteStatus>();
  private lockedPlayers = new Map<number, LockStatus>();

  // State
  private state: PluginState = 'disabled';
  private startTime: Date | null = null;
  private gameId: number = 2; // Default to BF4
  private gameVersion: BattlelogGameVersion = 'BF4';

  constructor(options: AdKatsPluginOptions) {
    this.config = options.config;
    this.bc = options.battlecon;
    this.weaponStatLibrary = options.weaponStatLibrary ?? defaultWeaponStatLibrary;

    // Initialize core components
    this.logger = createLogger({
      level: this.config.logLevel,
      pretty: this.config.logPretty,
      serverName: this.config.serverName,
    });

    this.eventBus = createEventBus(this.logger);
    this.scheduler = createScheduler(this.logger);

    this.db = createDatabase(this.config.database, this.logger);

    this.logger.info({ serverId: this.config.serverId }, 'AdKats plugin initialized');
  }

  /**
   * Get the current plugin state.
   */
  getState(): PluginState {
    return this.state;
  }

  /**
   * Get plugin uptime in milliseconds.
   */
  getUptime(): number {
    if (!this.startTime) {
      return 0;
    }
    return Date.now() - this.startTime.getTime();
  }

  /**
   * Enable the plugin.
   */
  async enable(): Promise<void> {
    if (this.state === 'enabled' || this.state === 'starting') {
      this.logger.warn('Plugin is already enabled or starting');
      return;
    }

    this.state = 'starting';
    this.startTime = new Date();
    this.logger.info('Enabling AdKats plugin...');

    try {
      // Connect to database
      await this.db.connect();

      // Initialize repositories
      const playerRepo = createPlayerRepository(
        this.db,
        createChildLogger(this.logger, { component: 'PlayerRepo' })
      );
      const banRepo = createBanRepository(
        this.db,
        createChildLogger(this.logger, { component: 'BanRepo' })
      );
      this.recordRepo = createRecordRepository(
        this.db,
        createChildLogger(this.logger, { component: 'RecordRepo' })
      );
      const infractionRepo = createInfractionRepository(
        this.db,
        createChildLogger(this.logger, { component: 'InfractionRepo' })
      );
      const specialPlayerRepo = createSpecialPlayerRepository(
        this.db,
        createChildLogger(this.logger, { component: 'SpecialPlayerRepo' })
      );

      // Initialize player service first (many services depend on it)
      this.playerService = createPlayerService(
        playerRepo,
        createChildLogger(this.logger, { component: 'PlayerService' }),
        this.gameId,
        this.config.serverId
      );

      // Initialize BattleCon adapter
      this.bcAdapter = createBattleConAdapter(
        this.bc,
        this.eventBus,
        this.playerService,
        createChildLogger(this.logger, { component: 'BattleConAdapter' })
      );
      this.bcAdapter.initialize();

      // Initialize command service
      this.commandService = createCommandService(
        createChildLogger(this.logger, { component: 'CommandService' }),
        this.db,
        this.eventBus,
        this.playerService,
        this.config,
        this.config.serverId
      );

      // Initialize special player service
      this.specialPlayerService = createSpecialPlayerService(
        createChildLogger(this.logger, { component: 'SpecialPlayerService' }),
        this.scheduler,
        specialPlayerRepo
      );

      // Initialize ban service
      this.banService = createBanService(
        createChildLogger(this.logger, { component: 'BanService' }),
        this.scheduler,
        this.bcAdapter,
        this.playerService,
        banRepo,
        this.recordRepo,
        this.commandService,
        this.config,
        this.config.serverId
      );

      // Initialize infraction service
      this.infractionService = createInfractionService(
        createChildLogger(this.logger, { component: 'InfractionService' }),
        this.config,
        this.bcAdapter,
        infractionRepo,
        this.recordRepo,
        banRepo,
        this.playerService,
        this.config.serverId
      );

      // Initialize team service
      this.teamService = createTeamService(
        createChildLogger(this.logger, { component: 'TeamService' }),
        this.bcAdapter,
        this.eventBus,
        this.playerService,
        this.config
      );

      // Initialize Discord service if configured
      if (this.config.discord.webhookUrl || this.config.discord.reportChannelWebhook) {
        this.discordService = createDiscordService(
          createChildLogger(this.logger, { component: 'DiscordService' }),
          this.config.discord.reportChannelWebhook,
          this.config.discord.adminChannelWebhook
        );
      }

      // Initialize report service
      this.reportService = createReportService(
        createChildLogger(this.logger, { component: 'ReportService' }),
        this.eventBus,
        this.bcAdapter,
        this.recordRepo,
        this.discordService
      );

      // Initialize Battlelog client if enabled
      if (this.config.battlelog.enabled) {
        this.battlelogClient = createBattlelogClient(
          createChildLogger(this.logger, { component: 'BattlelogClient' }),
          this.gameVersion,
          this.config.battlelog.rateLimitMs,
          this.config.battlelog.cacheTimeMs
        );
      }

      // Initialize anti-cheat service if enabled
      if (this.config.enableAntiCheat && this.battlelogClient) {
        this.antiCheatService = createAntiCheatService(
          createChildLogger(this.logger, { component: 'AntiCheatService' }),
          this.scheduler,
          this.config,
          this.bcAdapter,
          this.playerService,
          this.banService,
          this.eventBus,
          this.battlelogClient,
          this.weaponStatLibrary,
          this.gameVersion,
          this.config.serverId
        );
      }

      // Initialize reputation service if enabled
      if (this.config.enableReputationSystem) {
        // Create a no-op repository until we implement ReputationRepository
        const reputationRepo: ReputationRepository = {
          getReputation: async () => null,
          saveReputation: async () => {},
          getCommandCounts: async () => new Map<string, number>(),
          getRecentPunishments: async () => [],
          getRecentForgives: async () => [],
        };
        this.reputationService = createReputationService(
          createChildLogger(this.logger, { component: 'ReputationService' }),
          this.eventBus,
          reputationRepo,
          createDefaultReputationConfig(),
          this.gameId
        );
      }

      // Initialize AFK service if enabled
      if (this.config.enableAfkManager) {
        this.afkService = createAfkService(
          createChildLogger(this.logger, { component: 'AfkService' }),
          this.scheduler,
          this.eventBus,
          this.bcAdapter,
          this.playerService,
          createDefaultAfkConfig()
        );
      }

      // Initialize ping service if enabled
      if (this.config.enablePingEnforcer) {
        const pingConfig = createDefaultPingConfig();
        pingConfig.maxPing = this.config.pingEnforcer.maxPing;
        pingConfig.checkIntervalMs = this.config.pingEnforcer.checkIntervalMs;
        pingConfig.gracePeriodMs = this.config.pingEnforcer.gracePeriodMs;

        this.pingService = createPingService(
          createChildLogger(this.logger, { component: 'PingService' }),
          this.scheduler,
          this.bcAdapter,
          this.playerService,
          pingConfig
        );
      }

      // Initialize spambot service if enabled
      if (this.config.enableSpamBot) {
        this.spamBotService = createSpambotService(
          createChildLogger(this.logger, { component: 'SpamBotService' }),
          this.scheduler,
          this.bcAdapter,
          this.playerService,
          createDefaultSpamBotConfig()
        );
      }

      // Register all commands
      this.registerCommands();

      // Set up event handlers
      this.setupEventHandlers();

      // Start scheduler
      this.registerScheduledJobs();
      this.scheduler.start();

      // Emit plugin enabled event
      this.eventBus.emitEvent('plugin:enabled');

      this.state = 'enabled';
      this.logger.info('AdKats plugin enabled successfully');
    } catch (error) {
      this.state = 'error';
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to enable plugin');
      throw error;
    }
  }

  /**
   * Disable the plugin.
   */
  async disable(): Promise<void> {
    if (this.state === 'disabled' || this.state === 'stopping') {
      this.logger.warn('Plugin is already disabled or stopping');
      return;
    }

    this.state = 'stopping';
    this.logger.info('Disabling AdKats plugin...');

    try {
      // Stop scheduler (which stops all scheduled jobs)
      this.scheduler.stop();

      // Emit plugin disabled event
      this.eventBus.emitEvent('plugin:disabled');

      // Close database connection
      await this.db.close();

      // Clear services
      this.playerService = null;
      this.commandService = null;
      this.banService = null;
      this.infractionService = null;
      this.teamService = null;
      this.reportService = null;
      this.antiCheatService = null;
      this.reputationService = null;
      this.afkService = null;
      this.pingService = null;
      this.spamBotService = null;
      this.specialPlayerService = null;
      this.discordService = null;
      this.battlelogClient = null;
      this.bcAdapter = null;
      this.votingManager = null;
      this.recordRepo = null;

      this.state = 'disabled';
      this.startTime = null;
      this.logger.info('AdKats plugin disabled');
    } catch (error) {
      this.state = 'error';
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Error while disabling plugin');
      throw error;
    }
  }

  /**
   * Register all commands with the command service.
   */
  private registerCommands(): void {
    if (
      !this.commandService ||
      !this.bcAdapter ||
      !this.playerService ||
      !this.banService ||
      !this.infractionService ||
      !this.teamService ||
      !this.reportService ||
      !this.specialPlayerService ||
      !this.recordRepo
    ) {
      this.logger.error('Cannot register commands: services not initialized');
      return;
    }

    const commandLogger = createChildLogger(this.logger, { component: 'Commands' });

    // Admin commands
    registerKillCommand(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo
    );

    registerKickCommand(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo
    );

    registerSayCommands(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo
    );

    registerBanCommands({
      logger: commandLogger,
      bcAdapter: this.bcAdapter,
      commandService: this.commandService,
      recordRepo: this.recordRepo,
      banService: this.banService,
      playerService: this.playerService,
    });

    registerPunishCommands(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo,
      this.infractionService
    );

    registerMoveCommands(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo,
      this.teamService
    );

    registerReportAdminCommands(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo,
      this.reportService
    );

    registerInfoCommands({
      logger: commandLogger,
      bcAdapter: this.bcAdapter,
      commandService: this.commandService,
      recordRepo: this.recordRepo,
      playerService: this.playerService,
      db: this.db,
      serverId: this.config.serverId,
    });

    registerPlayerControlCommands({
      logger: commandLogger,
      bcAdapter: this.bcAdapter,
      commandService: this.commandService,
      recordRepo: this.recordRepo,
      db: this.db,
      serverId: this.config.serverId,
      mutedPlayers: this.mutedPlayers,
      lockedPlayers: this.lockedPlayers,
    });

    registerServerCommands({
      logger: commandLogger,
      bcAdapter: this.bcAdapter,
      commandService: this.commandService,
      recordRepo: this.recordRepo,
    });

    registerWhitelistCommands({
      logger: commandLogger,
      bcAdapter: this.bcAdapter,
      commandService: this.commandService,
      recordRepo: this.recordRepo,
      specialPlayerService: this.specialPlayerService,
      playerService: this.playerService,
    });

    // Server commands
    registerNukeCommands({
      logger: commandLogger,
      bcAdapter: this.bcAdapter,
      commandService: this.commandService,
      recordRepo: this.recordRepo,
      playerService: this.playerService,
      teamService: this.teamService,
    });

    registerRoundCommands({
      logger: commandLogger,
      bcAdapter: this.bcAdapter,
      commandService: this.commandService,
      recordRepo: this.recordRepo,
      teamService: this.teamService,
    });

    // Player commands
    registerRulesCommand(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo
    );

    registerHelpCommand(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo,
      this.config
    );

    registerTeamCommands(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo,
      this.teamService,
      this.playerService
    );

    registerReportCommands(
      commandLogger,
      this.bcAdapter,
      this.commandService,
      this.recordRepo,
      this.reportService
    );

    // Voting commands
    const { votingManager } = registerVotingCommands({
      logger: commandLogger,
      bcAdapter: this.bcAdapter,
      commandService: this.commandService,
      recordRepo: this.recordRepo,
      playerService: this.playerService,
    });
    this.votingManager = votingManager;

    this.logger.info('All commands registered');
  }

  // Service accessors
  getPlayerService(): PlayerService | null {
    return this.playerService;
  }

  getCommandService(): CommandService | null {
    return this.commandService;
  }

  getBanService(): BanService | null {
    return this.banService;
  }

  getInfractionService(): InfractionService | null {
    return this.infractionService;
  }

  getTeamService(): TeamService | null {
    return this.teamService;
  }

  getReportService(): ReportService | null {
    return this.reportService;
  }

  getAntiCheatService(): AntiCheatService | null {
    return this.antiCheatService;
  }

  getReputationService(): ReputationService | null {
    return this.reputationService;
  }

  getAfkService(): AfkService | null {
    return this.afkService;
  }

  getPingService(): PingService | null {
    return this.pingService;
  }

  getSpamBotService(): SpamBotService | null {
    return this.spamBotService;
  }

  getSpecialPlayerService(): SpecialPlayerService | null {
    return this.specialPlayerService;
  }

  getVotingManager(): VotingManager | null {
    return this.votingManager;
  }

  getEventBus(): AdKatsEventBus {
    return this.eventBus;
  }

  getLogger(): Logger {
    return this.logger;
  }

  getBattleConAdapter(): BattleConAdapter | null {
    return this.bcAdapter;
  }

  /**
   * Set up event handlers for game events.
   */
  private setupEventHandlers(): void {
    // Player join
    this.eventBus.onEvent('player:join', (player: APlayer) => {
      this.logger.info({ player: player.soldierName }, 'Player joined');

      // Check bans
      if (this.banService) {
        this.banService.checkPlayer(player).catch((err: unknown) => {
          const msg = err instanceof Error ? err.message : String(err);
          this.logger.error({ error: msg, player: player.soldierName }, 'Error checking player bans');
        });
      }

      // Check anti-cheat
      if (this.antiCheatService && player.guid) {
        this.antiCheatService.queuePlayerForCheck(player);
      }
    });

    // Player leave
    this.eventBus.onEvent('player:leave', (player: APlayer) => {
      this.logger.info({ player: player.soldierName }, 'Player left');
    });

    // Player chat - parse commands
    this.eventBus.onEvent('player:chat', (player: APlayer, message: string, subsetArray: string[]) => {
      this.logger.debug({ player: player.soldierName, message }, 'Player chat');

      // Parse commands through command service
      if (this.commandService) {
        // Build a ParsedChat object for executeFromChat
        const isCommand = message.startsWith(this.config.commandPrefix) ||
          this.config.commandPrefixAlternates.some(p => message.startsWith(p));

        if (isCommand) {
          // Remove the prefix to get command text
          let commandText = message;
          if (message.startsWith(this.config.commandPrefix)) {
            commandText = message.slice(this.config.commandPrefix.length);
          } else {
            for (const prefix of this.config.commandPrefixAlternates) {
              if (message.startsWith(prefix)) {
                commandText = message.slice(prefix.length);
                break;
              }
            }
          }

          // Parse command text into command and args
          const trimmedText = commandText.trim();
          const spaceIndex = trimmedText.indexOf(' ');
          const cmdName = spaceIndex > 0 ? trimmedText.slice(0, spaceIndex) : trimmedText;
          const cmdArgs = spaceIndex > 0 ? trimmedText.slice(spaceIndex + 1) : null;

          // Convert subset array to ChatSubset type
          const chatSubset: ChatSubset = this.parseSubset(subsetArray);

          this.commandService.executeFromChat({
            player,
            message,
            isCommand: true,
            commandText: cmdName,
            commandArgs: cmdArgs,
            subset: chatSubset,
          }).catch((err: unknown) => {
            const msg = err instanceof Error ? err.message : String(err);
            this.logger.error({ error: msg, player: player.soldierName }, 'Error parsing command');
          });
        }
      }
    });

    // Player kill - track for reputation and anti-cheat
    this.eventBus.onEvent(
      'player:kill',
      (killer: APlayer | null, victim: APlayer, weapon: string, headshot: boolean) => {
        this.logger.trace(
          {
            killer: killer?.soldierName ?? 'Server',
            victim: victim.soldierName,
            weapon,
            headshot,
          },
          'Player kill'
        );
      }
    );

    // Server events
    this.eventBus.onEvent('server:levelLoaded', (map, mode, roundNum, roundsTotal) => {
      this.logger.info({ map, mode, roundNum, roundsTotal }, 'Level loaded');

      // Cancel any active votes on new round
      if (this.votingManager) {
        // Cancel all vote types on round change
        void this.votingManager.cancelVote(VoteType.Surrender, 'Round ended');
        void this.votingManager.cancelVote(VoteType.NextMap, 'Round ended');
        void this.votingManager.cancelVote(VoteType.KickPlayer, 'Round ended');
      }
    });

    this.eventBus.onEvent('server:roundOver', (winningTeamId) => {
      this.logger.info({ winningTeamId }, 'Round over');
    });

    // Plugin errors
    this.eventBus.onEvent('plugin:error', (error: Error) => {
      this.logger.error({ error: error.message }, 'Plugin error');
    });
  }

  /**
   * Parse BattleCon subset array to ChatSubset type.
   */
  private parseSubset(subset: string[]): ChatSubset {
    // Battlefield sends subset as ['all'], ['team'], ['squad'], or ['player', 'soldierName']
    if (subset.length === 0) {
      return 'all';
    }
    const type = subset[0]?.toLowerCase();
    if (type === 'team') return 'team';
    if (type === 'squad') return 'squad';
    if (type === 'player') return 'player';
    return 'all';
  }

  /**
   * Register scheduled jobs.
   */
  private registerScheduledJobs(): void {
    // Database sync - sync online players periodically
    this.scheduler.registerIntervalJob(
      'player-sync',
      'Player Sync',
      60000, // Every minute
      async () => {
        if (this.bcAdapter?.isConnected() && this.playerService) {
          try {
            const serverPlayers = await this.bcAdapter.listPlayers();
            const serverPlayerNames = new Set(serverPlayers.map((p) => p.name));
            this.playerService.syncOnlinePlayers(serverPlayerNames);
          } catch (error) {
            const msg = error instanceof Error ? error.message : String(error);
            this.logger.warn({ error: msg }, 'Failed to sync players');
          }
        }
      }
    );
  }
}

/**
 * Create a new AdKats plugin instance.
 */
export function createAdKatsPlugin(options: AdKatsPluginOptions): AdKatsPlugin {
  return new AdKatsPlugin(options);
}
