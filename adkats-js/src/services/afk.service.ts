import type { Logger } from '../core/logger.js';
import type { Scheduler } from '../core/scheduler.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { PlayerService } from './player.service.js';
import type { APlayer } from '../models/player.js';

/**
 * AFK tracking entry for a player.
 */
interface AfkTrackingEntry {
  playerId: number;
  playerName: string;
  lastActivityTime: Date;
  lastActivityType: AfkActivityType;
  warningCount: number;
  lastWarningTime: Date | null;
}

/**
 * Types of activity that reset AFK timer.
 */
export type AfkActivityType = 'join' | 'spawn' | 'kill' | 'death' | 'chat' | 'teamChange' | 'squadChange';

/**
 * Configuration for the AFK service.
 */
export interface AfkConfig {
  /** Enable/disable AFK manager */
  enabled: boolean;
  /** Enable automatic kicking of AFK players */
  autoKickEnabled: boolean;
  /** Minutes of inactivity before player is considered idle */
  idleThresholdMinutes: number;
  /** Minutes of inactivity before player is kicked (if autoKick enabled) */
  kickThresholdMinutes: number;
  /** Minimum player count on server before AFK kicks are applied */
  minimumPlayersForKick: number;
  /** How often to check for AFK players (in milliseconds) */
  checkIntervalMs: number;
  /** Ignore players on the user list (admins) */
  ignoreUserList: boolean;
  /** Role names to ignore from AFK checks */
  ignoreRoles: string[];
  /** Whether chat activity resets AFK timer */
  ignoreChat: boolean;
  /** Warning message sent to idle players */
  warningMessage: string;
  /** Kick message for AFK players */
  kickMessage: string;
  /** Number of warnings before kick (0 = no warnings) */
  warningsBeforeKick: number;
}

/**
 * Result of an AFK check.
 */
export interface AfkCheckResult {
  isAfk: boolean;
  idleMinutes: number;
  shouldWarn: boolean;
  shouldKick: boolean;
}

/**
 * Callback to check if a player is exempt from AFK checks.
 */
export type AfkExemptionChecker = (player: APlayer) => Promise<boolean>;

/**
 * AfkService - detects and manages AFK (idle) players.
 *
 * Tracks player activity through kills, deaths, chat, spawns, and team changes.
 * Can warn and optionally kick players who are idle for too long.
 */
export class AfkService {
  private logger: Logger;
  private scheduler: Scheduler;
  private eventBus: AdKatsEventBus;
  private bcAdapter: BattleConAdapter;
  private playerService: PlayerService;
  private config: AfkConfig;
  private exemptionChecker: AfkExemptionChecker | null = null;

  // Tracking data
  private afkTracking: Map<string, AfkTrackingEntry> = new Map();

  // Job ID for scheduler
  private readonly AFK_CHECK_JOB_ID = 'afk-manager-check';

  constructor(
    logger: Logger,
    scheduler: Scheduler,
    eventBus: AdKatsEventBus,
    bcAdapter: BattleConAdapter,
    playerService: PlayerService,
    config: AfkConfig
  ) {
    this.logger = logger;
    this.scheduler = scheduler;
    this.eventBus = eventBus;
    this.bcAdapter = bcAdapter;
    this.playerService = playerService;
    this.config = config;
  }

  /**
   * Initialize the AFK service.
   */
  initialize(): void {
    if (!this.config.enabled) {
      this.logger.info('AFK manager is disabled');
      return;
    }

    // Set up event listeners to track player activity
    this.setupEventListeners();

    // Register scheduled job for AFK checks
    this.scheduler.registerIntervalJob(
      this.AFK_CHECK_JOB_ID,
      'AFK Manager Check',
      this.config.checkIntervalMs,
      () => this.checkAllPlayers()
    );

    this.logger.info({
      idleThreshold: this.config.idleThresholdMinutes,
      kickThreshold: this.config.kickThresholdMinutes,
      checkInterval: this.config.checkIntervalMs,
      autoKick: this.config.autoKickEnabled,
    }, 'AFK service initialized');
  }

  /**
   * Set up event listeners to track player activity.
   */
  private setupEventListeners(): void {
    // Player join - start tracking
    this.eventBus.onEvent('player:join', (player) => {
      this.recordActivity(player, 'join');
    });

    // Player leave - stop tracking
    this.eventBus.onEvent('player:leave', (player) => {
      this.afkTracking.delete(player.soldierName);
    });

    // Player spawn - activity
    this.eventBus.onEvent('player:spawn', (player) => {
      this.recordActivity(player, 'spawn');
    });

    // Player kill - activity for both killer and victim
    this.eventBus.onEvent('player:kill', (killer, victim) => {
      if (killer) {
        this.recordActivity(killer, 'kill');
      }
      this.recordActivity(victim, 'death');
    });

    // Player chat - activity (unless ignored)
    if (!this.config.ignoreChat) {
      this.eventBus.onEvent('player:chat', (player) => {
        this.recordActivity(player, 'chat');
      });
    }

    // Team change - activity
    this.eventBus.onEvent('player:teamChange', (player) => {
      this.recordActivity(player, 'teamChange');
    });

    // Squad change - activity
    this.eventBus.onEvent('player:squadChange', (player) => {
      this.recordActivity(player, 'squadChange');
    });

    // Round end - reset all tracking (fresh start each round)
    this.eventBus.onEvent('server:roundOver', () => {
      this.resetAllTracking();
    });
  }

  /**
   * Set a custom exemption checker function.
   * This allows checking against user roles, whitelists, etc.
   */
  setExemptionChecker(checker: AfkExemptionChecker): void {
    this.exemptionChecker = checker;
  }

  /**
   * Record activity for a player.
   */
  recordActivity(player: APlayer, activityType: AfkActivityType): void {
    const now = new Date();
    const existing = this.afkTracking.get(player.soldierName);

    if (existing) {
      existing.lastActivityTime = now;
      existing.lastActivityType = activityType;
    } else {
      this.afkTracking.set(player.soldierName, {
        playerId: player.playerId,
        playerName: player.soldierName,
        lastActivityTime: now,
        lastActivityType: activityType,
        warningCount: 0,
        lastWarningTime: null,
      });
    }
  }

  /**
   * Check a player's AFK status.
   */
  checkPlayerAfk(player: APlayer): AfkCheckResult {
    const tracking = this.afkTracking.get(player.soldierName);

    if (!tracking) {
      return {
        isAfk: false,
        idleMinutes: 0,
        shouldWarn: false,
        shouldKick: false,
      };
    }

    const now = new Date();
    const idleMs = now.getTime() - tracking.lastActivityTime.getTime();
    const idleMinutes = idleMs / (1000 * 60);

    const isAfk = idleMinutes >= this.config.idleThresholdMinutes;
    const shouldKick = idleMinutes >= this.config.kickThresholdMinutes;

    // Determine if we should warn
    let shouldWarn = false;
    if (isAfk && !shouldKick && this.config.warningsBeforeKick > 0) {
      // Check if enough time has passed since last warning (1 minute minimum)
      const timeSinceWarning = tracking.lastWarningTime
        ? (now.getTime() - tracking.lastWarningTime.getTime()) / (1000 * 60)
        : Infinity;

      if (timeSinceWarning >= 1 && tracking.warningCount < this.config.warningsBeforeKick) {
        shouldWarn = true;
      }
    }

    return {
      isAfk,
      idleMinutes,
      shouldWarn,
      shouldKick,
    };
  }

  /**
   * Check all online players for AFK status.
   */
  async checkAllPlayers(): Promise<void> {
    const playerCount = this.playerService.getOnlinePlayerCount();

    // Don't kick if server is below minimum player count
    const canKick = this.config.autoKickEnabled && playerCount >= this.config.minimumPlayersForKick;

    if (playerCount === 0) {
      return;
    }

    const players = this.playerService.getAllOnlinePlayers();

    for (const player of players) {
      try {
        // Check exemptions
        if (this.exemptionChecker) {
          const isExempt = await this.exemptionChecker(player);
          if (isExempt) {
            continue;
          }
        }

        const result = this.checkPlayerAfk(player);

        if (!result.isAfk) {
          continue;
        }

        const tracking = this.afkTracking.get(player.soldierName);
        if (!tracking) {
          continue;
        }

        // Handle kick
        if (result.shouldKick && canKick) {
          // Check if we've given enough warnings
          if (this.config.warningsBeforeKick === 0 || tracking.warningCount >= this.config.warningsBeforeKick) {
            await this.kickAfkPlayer(player, result.idleMinutes);
            continue;
          }
        }

        // Handle warning
        if (result.shouldWarn) {
          await this.warnAfkPlayer(player, result.idleMinutes, tracking);
        }

      } catch (error) {
        const msg = error instanceof Error ? error.message : String(error);
        this.logger.error({ error: msg, player: player.soldierName }, 'Error checking player AFK status');
      }
    }
  }

  /**
   * Warn an AFK player.
   */
  private async warnAfkPlayer(
    player: APlayer,
    idleMinutes: number,
    tracking: AfkTrackingEntry
  ): Promise<void> {
    tracking.warningCount++;
    tracking.lastWarningTime = new Date();

    const remainingMinutes = Math.ceil(this.config.kickThresholdMinutes - idleMinutes);
    const message = this.formatMessage(this.config.warningMessage, {
      player_name: player.soldierName,
      idle_minutes: Math.floor(idleMinutes).toString(),
      remaining_minutes: remainingMinutes.toString(),
      warning_count: tracking.warningCount.toString(),
      max_warnings: this.config.warningsBeforeKick.toString(),
    });

    try {
      await this.bcAdapter.sayPlayer(message, player.soldierName);

      this.logger.debug({
        player: player.soldierName,
        idleMinutes: Math.floor(idleMinutes),
        warningCount: tracking.warningCount,
      }, 'Warned AFK player');
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, player: player.soldierName }, 'Failed to warn AFK player');
    }
  }

  /**
   * Kick an AFK player.
   */
  private async kickAfkPlayer(player: APlayer, idleMinutes: number): Promise<void> {
    const message = this.formatMessage(this.config.kickMessage, {
      player_name: player.soldierName,
      idle_minutes: Math.floor(idleMinutes).toString(),
    });

    try {
      await this.bcAdapter.kickPlayer(player.soldierName, message);

      // Remove from tracking
      this.afkTracking.delete(player.soldierName);

      this.logger.info({
        player: player.soldierName,
        idleMinutes: Math.floor(idleMinutes),
      }, 'Kicked AFK player');
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, player: player.soldierName }, 'Failed to kick AFK player');
    }
  }

  /**
   * Format a message with placeholder substitution.
   */
  private formatMessage(template: string, vars: Record<string, string>): string {
    let result = template;
    for (const [key, value] of Object.entries(vars)) {
      result = result.replace(new RegExp(`%${key}%`, 'gi'), value);
    }
    return result;
  }

  /**
   * Reset tracking for all players (e.g., on round end).
   */
  resetAllTracking(): void {
    // Keep entries but reset times and warning counts
    const now = new Date();
    for (const tracking of this.afkTracking.values()) {
      tracking.lastActivityTime = now;
      tracking.warningCount = 0;
      tracking.lastWarningTime = null;
    }

    this.logger.debug('Reset all AFK tracking');
  }

  /**
   * Get the idle time for a player in minutes.
   */
  getIdleTime(playerName: string): number | null {
    const tracking = this.afkTracking.get(playerName);
    if (!tracking) {
      return null;
    }

    const now = new Date();
    return (now.getTime() - tracking.lastActivityTime.getTime()) / (1000 * 60);
  }

  /**
   * Get all currently tracked players with their idle times.
   */
  getAfkStatus(): Array<{ playerName: string; idleMinutes: number; isAfk: boolean }> {
    const result: Array<{ playerName: string; idleMinutes: number; isAfk: boolean }> = [];
    const now = new Date();

    for (const tracking of this.afkTracking.values()) {
      const idleMinutes = (now.getTime() - tracking.lastActivityTime.getTime()) / (1000 * 60);
      result.push({
        playerName: tracking.playerName,
        idleMinutes,
        isAfk: idleMinutes >= this.config.idleThresholdMinutes,
      });
    }

    return result.sort((a, b) => b.idleMinutes - a.idleMinutes);
  }

  /**
   * Manually reset AFK timer for a player.
   */
  resetPlayerTimer(playerName: string): boolean {
    const tracking = this.afkTracking.get(playerName);
    if (!tracking) {
      return false;
    }

    tracking.lastActivityTime = new Date();
    tracking.warningCount = 0;
    tracking.lastWarningTime = null;
    return true;
  }

  /**
   * Enable the AFK manager.
   */
  enable(): void {
    this.scheduler.enableJob(this.AFK_CHECK_JOB_ID);
    this.logger.info('AFK manager enabled');
  }

  /**
   * Disable the AFK manager.
   */
  disable(): void {
    this.scheduler.disableJob(this.AFK_CHECK_JOB_ID);
    this.logger.info('AFK manager disabled');
  }
}

/**
 * Create default AFK configuration.
 */
export function createDefaultAfkConfig(): AfkConfig {
  return {
    enabled: false,
    autoKickEnabled: true,
    idleThresholdMinutes: 5,
    kickThresholdMinutes: 10,
    minimumPlayersForKick: 20,
    checkIntervalMs: 30000,
    ignoreUserList: true,
    ignoreRoles: [],
    ignoreChat: false,
    warningMessage: '[AdKats] %player_name%, you have been idle for %idle_minutes% minutes. You will be kicked in %remaining_minutes% minutes if you remain inactive.',
    kickMessage: '[AdKats] Kicked for being AFK (%idle_minutes% minutes idle)',
    warningsBeforeKick: 2,
  };
}

/**
 * Create a new AFK service.
 */
export function createAfkService(
  logger: Logger,
  scheduler: Scheduler,
  eventBus: AdKatsEventBus,
  bcAdapter: BattleConAdapter,
  playerService: PlayerService,
  config: AfkConfig
): AfkService {
  return new AfkService(logger, scheduler, eventBus, bcAdapter, playerService, config);
}
