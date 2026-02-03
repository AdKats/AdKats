import type { Logger } from '../core/logger.js';
import type { Scheduler } from '../core/scheduler.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { PlayerService } from './player.service.js';
import type { APlayer } from '../models/player.js';

/**
 * Ping tracking entry for a player.
 */
interface PingTrackingEntry {
  playerId: number;
  playerName: string;
  samples: PingSample[];
  joinTime: Date;
  warningCount: number;
  lastWarningTime: Date | null;
  lastKickTime: Date | null;
}

/**
 * A single ping sample.
 */
interface PingSample {
  ping: number;
  timestamp: Date;
}

/**
 * Configuration for the ping service.
 */
export interface PingConfig {
  /** Enable/disable ping enforcer */
  enabled: boolean;
  /** Maximum allowed ping (ms) - players above this get warned/kicked */
  maxPing: number;
  /** Duration in seconds for moving average calculation */
  movingAverageDurationSeconds: number;
  /** Number of warnings before kick */
  warningsBeforeKick: number;
  /** Grace period after join (ms) before enforcing ping */
  gracePeriodMs: number;
  /** Minimum players on server before ping enforcement */
  minimumPlayersForEnforcement: number;
  /** How often to check pings (ms) */
  checkIntervalMs: number;
  /** Kick players with missing ping data */
  kickMissingPings: boolean;
  /** Ignore players on user list (admins) */
  ignoreUserList: boolean;
  /** Role names to ignore from ping checks */
  ignoreRoles: string[];
  /** Message prefix for kick reason */
  kickMessagePrefix: string;
  /** Display warnings in Procon chat */
  displayProconChat: boolean;
}

/**
 * Result of a ping check.
 */
export interface PingCheckResult {
  playerName: string;
  currentPing: number;
  averagePing: number;
  sampleCount: number;
  isOverLimit: boolean;
  warningCount: number;
  inGracePeriod: boolean;
}

/**
 * Callback to check if a player is exempt from ping checks.
 */
export type PingExemptionChecker = (player: APlayer) => Promise<boolean>;

/**
 * PingService - monitors and enforces ping limits.
 *
 * Uses a moving average over a configurable time window to smooth out
 * ping spikes. Players consistently over the limit receive warnings
 * before being kicked.
 */
export class PingService {
  private logger: Logger;
  private scheduler: Scheduler;
  private bcAdapter: BattleConAdapter;
  private playerService: PlayerService;
  private config: PingConfig;
  private exemptionChecker: PingExemptionChecker | null = null;

  // Tracking data
  private pingTracking: Map<string, PingTrackingEntry> = new Map();

  // Job ID for scheduler
  private readonly PING_CHECK_JOB_ID = 'ping-enforcer-check';

  constructor(
    logger: Logger,
    scheduler: Scheduler,
    bcAdapter: BattleConAdapter,
    playerService: PlayerService,
    config: PingConfig
  ) {
    this.logger = logger;
    this.scheduler = scheduler;
    this.bcAdapter = bcAdapter;
    this.playerService = playerService;
    this.config = config;
  }

  /**
   * Initialize the ping service.
   */
  initialize(): void {
    if (!this.config.enabled) {
      this.logger.info('Ping enforcer is disabled');
      return;
    }

    // Register scheduled job for ping checks
    this.scheduler.registerIntervalJob(
      this.PING_CHECK_JOB_ID,
      'Ping Enforcer Check',
      this.config.checkIntervalMs,
      () => this.checkAllPlayers()
    );

    this.logger.info({
      maxPing: this.config.maxPing,
      movingAverageSeconds: this.config.movingAverageDurationSeconds,
      warningsBeforeKick: this.config.warningsBeforeKick,
      gracePeriodMs: this.config.gracePeriodMs,
    }, 'Ping service initialized');
  }

  /**
   * Set a custom exemption checker function.
   */
  setExemptionChecker(checker: PingExemptionChecker): void {
    this.exemptionChecker = checker;
  }

  /**
   * Record a ping sample for a player.
   * Called periodically when player list is updated.
   */
  recordPingSample(player: APlayer): void {
    const now = new Date();
    const ping = player.ping;

    // Skip invalid pings
    if (ping < 0 || ping > 9999) {
      return;
    }

    let tracking = this.pingTracking.get(player.soldierName);

    if (!tracking) {
      tracking = {
        playerId: player.playerId,
        playerName: player.soldierName,
        samples: [],
        joinTime: now,
        warningCount: 0,
        lastWarningTime: null,
        lastKickTime: null,
      };
      this.pingTracking.set(player.soldierName, tracking);
    }

    // Add new sample
    tracking.samples.push({ ping, timestamp: now });

    // Remove old samples outside the moving average window
    const cutoffTime = new Date(now.getTime() - this.config.movingAverageDurationSeconds * 1000);
    tracking.samples = tracking.samples.filter(s => s.timestamp >= cutoffTime);
  }

  /**
   * Calculate the moving average ping for a player.
   */
  calculateMovingAverage(playerName: string): number | null {
    const tracking = this.pingTracking.get(playerName);

    if (!tracking || tracking.samples.length === 0) {
      return null;
    }

    const sum = tracking.samples.reduce((acc, s) => acc + s.ping, 0);
    return sum / tracking.samples.length;
  }

  /**
   * Check a player's ping status.
   */
  checkPlayerPing(player: APlayer): PingCheckResult {
    const tracking = this.pingTracking.get(player.soldierName);
    const now = new Date();

    // Check if in grace period
    const inGracePeriod = tracking
      ? (now.getTime() - tracking.joinTime.getTime()) < this.config.gracePeriodMs
      : true;

    const averagePing = this.calculateMovingAverage(player.soldierName);

    return {
      playerName: player.soldierName,
      currentPing: player.ping,
      averagePing: averagePing ?? player.ping,
      sampleCount: tracking?.samples.length ?? 0,
      isOverLimit: (averagePing ?? player.ping) > this.config.maxPing,
      warningCount: tracking?.warningCount ?? 0,
      inGracePeriod,
    };
  }

  /**
   * Check all online players for ping violations.
   */
  async checkAllPlayers(): Promise<void> {
    const playerCount = this.playerService.getOnlinePlayerCount();

    // Don't enforce if below minimum player count
    if (playerCount < this.config.minimumPlayersForEnforcement) {
      return;
    }

    // First, update all player pings
    const players = this.playerService.getAllOnlinePlayers();

    // Clean up tracking for players who have left
    const onlinePlayerNames = new Set(players.map(p => p.soldierName));
    for (const name of this.pingTracking.keys()) {
      if (!onlinePlayerNames.has(name)) {
        this.pingTracking.delete(name);
      }
    }

    // Record ping samples for all players
    for (const player of players) {
      this.recordPingSample(player);
    }

    // Now check each player
    for (const player of players) {
      try {
        await this.processPlayerPing(player);
      } catch (error) {
        const msg = error instanceof Error ? error.message : String(error);
        this.logger.error({ error: msg, player: player.soldierName }, 'Error checking player ping');
      }
    }
  }

  /**
   * Process ping check for a single player.
   */
  private async processPlayerPing(player: APlayer): Promise<void> {
    // Check exemptions
    if (this.exemptionChecker) {
      const isExempt = await this.exemptionChecker(player);
      if (isExempt) {
        return;
      }
    }

    const result = this.checkPlayerPing(player);
    const tracking = this.pingTracking.get(player.soldierName);

    // Skip if in grace period
    if (result.inGracePeriod) {
      return;
    }

    // Check for missing ping data
    if (this.config.kickMissingPings && result.sampleCount === 0 && player.ping <= 0) {
      this.logger.warn({
        player: player.soldierName,
        ping: player.ping,
      }, 'Player has missing ping data');
      return;
    }

    // Check if over limit
    if (!result.isOverLimit) {
      // Reset warnings if ping is good
      if (tracking && tracking.warningCount > 0) {
        tracking.warningCount = 0;
        this.logger.debug({
          player: player.soldierName,
          averagePing: result.averagePing,
        }, 'Player ping improved, reset warnings');
      }
      return;
    }

    if (!tracking) {
      return;
    }

    // Player is over the limit
    const now = new Date();

    // Check if enough time has passed since last warning (30 seconds minimum)
    const timeSinceWarning = tracking.lastWarningTime
      ? (now.getTime() - tracking.lastWarningTime.getTime()) / 1000
      : Infinity;

    if (timeSinceWarning < 30) {
      return;
    }

    // Increment warning count
    tracking.warningCount++;
    tracking.lastWarningTime = now;

    // Check if we should kick
    if (tracking.warningCount > this.config.warningsBeforeKick) {
      await this.kickForPing(player, result.averagePing);
      return;
    }

    // Send warning
    await this.warnForPing(player, result.averagePing, tracking.warningCount);
  }

  /**
   * Warn a player about high ping.
   */
  private async warnForPing(player: APlayer, averagePing: number, warningCount: number): Promise<void> {
    const remainingWarnings = this.config.warningsBeforeKick - warningCount;
    const message = `[AdKats] Your ping (${Math.round(averagePing)}ms) exceeds the limit (${this.config.maxPing}ms). ` +
      `Warning ${warningCount}/${this.config.warningsBeforeKick}. ` +
      `${remainingWarnings > 0 ? `${remainingWarnings} warning(s) remaining.` : 'You will be kicked soon.'}`;

    try {
      await this.bcAdapter.sayPlayer(message, player.soldierName);

      this.logger.debug({
        player: player.soldierName,
        averagePing: Math.round(averagePing),
        warningCount,
      }, 'Warned player for high ping');

      if (this.config.displayProconChat) {
        this.logger.info({
          player: player.soldierName,
          averagePing: Math.round(averagePing),
          warningCount,
          maxPing: this.config.maxPing,
        }, 'Ping warning issued');
      }
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, player: player.soldierName }, 'Failed to warn player for ping');
    }
  }

  /**
   * Kick a player for high ping.
   */
  private async kickForPing(player: APlayer, averagePing: number): Promise<void> {
    const kickReason = `${this.config.kickMessagePrefix} Your ping: ${Math.round(averagePing)}ms, Limit: ${this.config.maxPing}ms`;

    try {
      await this.bcAdapter.kickPlayer(player.soldierName, kickReason);

      // Remove from tracking
      this.pingTracking.delete(player.soldierName);

      this.logger.info({
        player: player.soldierName,
        averagePing: Math.round(averagePing),
        maxPing: this.config.maxPing,
      }, 'Kicked player for high ping');
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, player: player.soldierName }, 'Failed to kick player for ping');
    }
  }

  /**
   * Get ping statistics for all tracked players.
   */
  getPingStats(): Array<{
    playerName: string;
    currentPing: number;
    averagePing: number;
    sampleCount: number;
    warningCount: number;
  }> {
    const result: Array<{
      playerName: string;
      currentPing: number;
      averagePing: number;
      sampleCount: number;
      warningCount: number;
    }> = [];

    for (const tracking of this.pingTracking.values()) {
      const player = this.playerService.getOnlinePlayer(tracking.playerName);
      const averagePing = this.calculateMovingAverage(tracking.playerName);

      result.push({
        playerName: tracking.playerName,
        currentPing: player?.ping ?? 0,
        averagePing: averagePing ?? 0,
        sampleCount: tracking.samples.length,
        warningCount: tracking.warningCount,
      });
    }

    return result.sort((a, b) => b.averagePing - a.averagePing);
  }

  /**
   * Get ping info for a specific player.
   */
  getPlayerPingInfo(playerName: string): PingCheckResult | null {
    const player = this.playerService.getOnlinePlayer(playerName);
    if (!player) {
      return null;
    }
    return this.checkPlayerPing(player);
  }

  /**
   * Clear tracking for a player (e.g., on manual reset).
   */
  clearPlayerTracking(playerName: string): void {
    this.pingTracking.delete(playerName);
  }

  /**
   * Reset warning count for a player.
   */
  resetPlayerWarnings(playerName: string): boolean {
    const tracking = this.pingTracking.get(playerName);
    if (!tracking) {
      return false;
    }
    tracking.warningCount = 0;
    tracking.lastWarningTime = null;
    return true;
  }

  /**
   * Enable the ping enforcer.
   */
  enable(): void {
    this.scheduler.enableJob(this.PING_CHECK_JOB_ID);
    this.logger.info('Ping enforcer enabled');
  }

  /**
   * Disable the ping enforcer.
   */
  disable(): void {
    this.scheduler.disableJob(this.PING_CHECK_JOB_ID);
    this.logger.info('Ping enforcer disabled');
  }

  /**
   * Update the maximum ping limit.
   */
  setMaxPing(maxPing: number): void {
    this.config.maxPing = maxPing;
    this.logger.info({ maxPing }, 'Updated max ping limit');
  }
}

/**
 * Create default ping configuration.
 */
export function createDefaultPingConfig(): PingConfig {
  return {
    enabled: false,
    maxPing: 200,
    movingAverageDurationSeconds: 180,
    warningsBeforeKick: 3,
    gracePeriodMs: 60000,
    minimumPlayersForEnforcement: 10,
    checkIntervalMs: 30000,
    kickMissingPings: false,
    ignoreUserList: true,
    ignoreRoles: [],
    kickMessagePrefix: '[AdKats] Kicked for high ping.',
    displayProconChat: true,
  };
}

/**
 * Create a new ping service.
 */
export function createPingService(
  logger: Logger,
  scheduler: Scheduler,
  bcAdapter: BattleConAdapter,
  playerService: PlayerService,
  config: PingConfig
): PingService {
  return new PingService(logger, scheduler, bcAdapter, playerService, config);
}
