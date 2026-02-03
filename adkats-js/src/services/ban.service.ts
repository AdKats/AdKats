import type { Logger } from '../core/logger.js';
import type { Scheduler } from '../core/scheduler.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { AdKatsConfig } from '../core/config.js';
import type { PlayerService } from './player.service.js';
import type { BanRepository } from '../database/repositories/ban.repository.js';
import type { RecordRepository } from '../database/repositories/record.repository.js';
import type { CommandService } from './command.service.js';
import type { APlayer } from '../models/player.js';
import type { ARecord } from '../models/record.js';
import type { ABan } from '../models/ban.js';
import { createBan, isBanActive, isBanPermanent, getBanDurationString } from '../models/ban.js';
import { createAutomatedRecord } from '../models/record.js';
import { CommandKeys } from '../models/command.js';

/**
 * Options for creating a ban.
 */
export interface BanOptions {
  enforceName?: boolean;
  enforceGuid?: boolean;
  enforceIp?: boolean;
}

/**
 * Result of a ban check.
 */
export interface BanCheckResult {
  isBanned: boolean;
  ban: ABan | null;
  matchType: 'guid' | 'ip' | 'name' | null;
}

/**
 * Service for ban enforcement and management.
 * Handles checking players against bans, creating/removing bans,
 * and syncing bans across servers.
 */
export class BanService {
  private logger: Logger;
  private scheduler: Scheduler;
  private bcAdapter: BattleConAdapter;
  private playerService: PlayerService;
  private banRepo: BanRepository;
  private recordRepo: RecordRepository;
  private commandService: CommandService;
  private config: AdKatsConfig;
  private serverId: number;

  // Job ID for the ban check scheduler
  private readonly BAN_CHECK_JOB_ID = 'ban-enforcer-check';

  // Cache of recently checked players to avoid duplicate checks
  private recentlyChecked: Map<string, number> = new Map();
  private readonly CHECK_CACHE_MS = 30000; // Don't re-check player within 30 seconds

  constructor(
    logger: Logger,
    scheduler: Scheduler,
    bcAdapter: BattleConAdapter,
    playerService: PlayerService,
    banRepo: BanRepository,
    recordRepo: RecordRepository,
    commandService: CommandService,
    config: AdKatsConfig,
    serverId: number
  ) {
    this.logger = logger;
    this.scheduler = scheduler;
    this.bcAdapter = bcAdapter;
    this.playerService = playerService;
    this.banRepo = banRepo;
    this.recordRepo = recordRepo;
    this.commandService = commandService;
    this.config = config;
    this.serverId = serverId;
  }

  /**
   * Initialize the ban service and register scheduled jobs.
   */
  initialize(): void {
    if (!this.config.enableBanEnforcer) {
      this.logger.info('Ban enforcer is disabled in configuration');
      return;
    }

    // Register the scheduled job for checking all online players
    const checkInterval = this.config.banEnforcer.checkIntervalMs;
    this.scheduler.registerIntervalJob(
      this.BAN_CHECK_JOB_ID,
      'Ban Enforcer Check',
      checkInterval,
      () => this.checkAllOnlinePlayers()
    );

    this.logger.info({ checkIntervalMs: checkInterval }, 'Ban service initialized');
  }

  /**
   * Check if a player is banned.
   * Called when a player joins or on scheduled intervals.
   */
  async checkPlayer(player: APlayer): Promise<BanCheckResult> {
    // Check if we recently checked this player
    const lastCheck = this.recentlyChecked.get(player.soldierName);
    if (lastCheck && Date.now() - lastCheck < this.CHECK_CACHE_MS) {
      return { isBanned: false, ban: null, matchType: null };
    }

    this.recentlyChecked.set(player.soldierName, Date.now());

    try {
      // Check by GUID (most common and reliable)
      if (this.config.banEnforcer.enforceGuid && player.guid) {
        const ban = await this.banRepo.findActiveByGuid(player.guid);
        if (ban && isBanActive(ban)) {
          this.logger.info(
            { player: player.soldierName, banId: ban.banId, matchType: 'guid' },
            'Player matched ban by GUID'
          );
          await this.enforceBan(player, ban, 'guid');
          return { isBanned: true, ban, matchType: 'guid' };
        }
      }

      // Check by IP
      if (this.config.banEnforcer.enforceIp && player.ipAddress) {
        const ban = await this.banRepo.findActiveByIp(player.ipAddress);
        if (ban && isBanActive(ban)) {
          this.logger.info(
            { player: player.soldierName, banId: ban.banId, matchType: 'ip' },
            'Player matched ban by IP'
          );
          await this.enforceBan(player, ban, 'ip');
          return { isBanned: true, ban, matchType: 'ip' };
        }
      }

      // Check by Name (least reliable, usually disabled)
      if (this.config.banEnforcer.enforceName) {
        const ban = await this.banRepo.findActiveByName(player.soldierName);
        if (ban && isBanActive(ban)) {
          this.logger.info(
            { player: player.soldierName, banId: ban.banId, matchType: 'name' },
            'Player matched ban by Name'
          );
          await this.enforceBan(player, ban, 'name');
          return { isBanned: true, ban, matchType: 'name' };
        }
      }

      // Also check by player ID directly (for existing bans)
      if (player.playerId > 0) {
        const ban = await this.banRepo.findActiveByPlayerId(player.playerId);
        if (ban && isBanActive(ban)) {
          this.logger.info(
            { player: player.soldierName, banId: ban.banId, matchType: 'guid' },
            'Player matched ban by player ID'
          );
          await this.enforceBan(player, ban, 'guid');
          return { isBanned: true, ban, matchType: 'guid' };
        }
      }

      return { isBanned: false, ban: null, matchType: null };
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, player: player.soldierName }, 'Error checking player ban status');
      return { isBanned: false, ban: null, matchType: null };
    }
  }

  /**
   * Check all online players against the ban list.
   * Called by the scheduler on a regular interval.
   */
  async checkAllOnlinePlayers(): Promise<void> {
    const players = this.playerService.getAllOnlinePlayers();

    if (players.length === 0) {
      return;
    }

    this.logger.debug({ playerCount: players.length }, 'Checking all online players for bans');

    // Also expire old bans
    try {
      const expiredCount = await this.banRepo.expireOldBans();
      if (expiredCount > 0) {
        this.logger.info({ count: expiredCount }, 'Expired old bans');
      }
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Error expiring old bans');
    }

    // Check each player
    for (const player of players) {
      try {
        await this.checkPlayer(player);
      } catch (error) {
        const msg = error instanceof Error ? error.message : String(error);
        this.logger.error({ error: msg, player: player.soldierName }, 'Error checking player');
      }
    }
  }

  /**
   * Enforce a ban on a player by kicking them.
   */
  private async enforceBan(player: APlayer, ban: ABan, matchType: 'guid' | 'ip' | 'name'): Promise<void> {
    // Build the kick message
    const durationStr = getBanDurationString(ban);
    const kickMessage = `[AdKats] You are banned: ${ban.banNotes} (${durationStr})`;

    try {
      // Kick the player
      await this.bcAdapter.kickPlayer(player.soldierName, kickMessage);

      // Mark player as offline
      player.isOnline = false;
      this.playerService.removeOnlinePlayer(player.soldierName);

      // Log the enforcement as a record
      const command = this.commandService.getCommandByKey(CommandKeys.BAN_ENFORCE);
      if (command) {
        const record = createAutomatedRecord(
          this.serverId,
          player,
          command,
          `Ban enforcement: ${ban.banNotes} [${matchType}]`,
          'BanEnforcer'
        );
        await this.recordRepo.create(record);
      }

      this.logger.info({
        player: player.soldierName,
        banId: ban.banId,
        matchType,
        reason: ban.banNotes,
      }, 'Ban enforced - player kicked');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({
        error: msg,
        player: player.soldierName,
        banId: ban.banId,
      }, 'Failed to enforce ban');
    }
  }

  /**
   * Create a ban for a player.
   * @param target - The player to ban
   * @param source - The admin issuing the ban (null for automated)
   * @param durationMinutes - Ban duration in minutes (null for permanent)
   * @param reason - Ban reason
   * @param options - Ban enforcement options
   * @param record - Optional existing record to associate with the ban
   */
  async banPlayer(
    target: APlayer,
    source: APlayer | null,
    durationMinutes: number | null,
    reason: string,
    options: BanOptions = {},
    record?: ARecord
  ): Promise<ABan> {
    // Get or create the command record
    let banRecord = record;
    if (!banRecord) {
      const commandKey = durationMinutes === null ? CommandKeys.BAN_PERM : CommandKeys.BAN_TEMP;
      const command = this.commandService.getCommandByKey(commandKey);
      banRecord = createAutomatedRecord(
        this.serverId,
        target,
        command ?? null,
        reason,
        source?.soldierName ?? 'AdKats'
      );
      banRecord.sourceId = source?.playerId ?? null;
      banRecord.sourcePlayer = source;
      banRecord = await this.recordRepo.create(banRecord);
    }

    // Create the ban
    const ban = createBan(target, banRecord, durationMinutes, reason, {
      enforceName: options.enforceName ?? false,
      enforceGuid: options.enforceGuid ?? true,
      enforceIp: options.enforceIp ?? false,
    });

    // Save to database
    const savedBan = await this.banRepo.upsert(ban);

    this.logger.info({
      banId: savedBan.banId,
      target: target.soldierName,
      source: source?.soldierName ?? 'AdKats',
      duration: durationMinutes,
      reason,
    }, 'Ban created');

    // If player is online, kick them immediately
    if (target.isOnline) {
      await this.enforceBan(target, savedBan, 'guid');
    }

    return savedBan;
  }

  /**
   * Remove (disable) a ban.
   * @param ban - The ban to remove
   * @param source - The admin removing the ban
   * @param record - Optional existing record to associate
   */
  async unbanPlayer(
    ban: ABan,
    source: APlayer | null,
    record?: ARecord
  ): Promise<void> {
    // Create a record for the unban
    if (!record) {
      const command = this.commandService.getCommandByKey(CommandKeys.UNBAN);
      const unbanRecord = createAutomatedRecord(
        this.serverId,
        ban.player,
        command ?? null,
        `Unbanned: ${ban.banNotes}`,
        source?.soldierName ?? 'AdKats'
      );
      unbanRecord.sourceId = source?.playerId ?? null;
      unbanRecord.targetId = ban.playerId;
      await this.recordRepo.create(unbanRecord);
    }

    // Disable the ban
    await this.banRepo.disable(ban.banId);

    this.logger.info({
      banId: ban.banId,
      playerId: ban.playerId,
      source: source?.soldierName ?? 'AdKats',
    }, 'Ban removed');
  }

  /**
   * Get a ban by ID.
   */
  async getBanById(banId: number): Promise<ABan | null> {
    return this.banRepo.findById(banId);
  }

  /**
   * Get a ban for a player by player ID.
   */
  async getBanByPlayerId(playerId: number): Promise<ABan | null> {
    return this.banRepo.findByPlayerId(playerId);
  }

  /**
   * Get active ban for a player.
   */
  async getActiveBan(playerId: number): Promise<ABan | null> {
    return this.banRepo.findActiveByPlayerId(playerId);
  }

  /**
   * Search for bans by player name or notes.
   */
  async searchBans(searchTerm: string, limit: number = 20): Promise<ABan[]> {
    return this.banRepo.search(searchTerm, limit);
  }

  /**
   * Enable the ban enforcer scheduled job.
   */
  enableBanEnforcer(): void {
    this.scheduler.enableJob(this.BAN_CHECK_JOB_ID);
    this.logger.info('Ban enforcer enabled');
  }

  /**
   * Disable the ban enforcer scheduled job.
   */
  disableBanEnforcer(): void {
    this.scheduler.disableJob(this.BAN_CHECK_JOB_ID);
    this.logger.info('Ban enforcer disabled');
  }

  /**
   * Clear the recently checked cache.
   */
  clearCache(): void {
    this.recentlyChecked.clear();
  }

  /**
   * Format a ban for display.
   */
  formatBanInfo(ban: ABan): string {
    const duration = isBanPermanent(ban) ? 'Permanent' : getBanDurationString(ban);
    const status = isBanActive(ban) ? 'Active' : ban.banStatus;
    return `Ban #${ban.banId} [${status}] - ${duration} - ${ban.banNotes}`;
  }
}

/**
 * Create a new ban service.
 */
export function createBanService(
  logger: Logger,
  scheduler: Scheduler,
  bcAdapter: BattleConAdapter,
  playerService: PlayerService,
  banRepo: BanRepository,
  recordRepo: RecordRepository,
  commandService: CommandService,
  config: AdKatsConfig,
  serverId: number
): BanService {
  return new BanService(
    logger,
    scheduler,
    bcAdapter,
    playerService,
    banRepo,
    recordRepo,
    commandService,
    config,
    serverId
  );
}
