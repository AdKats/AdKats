import type { Logger } from '../core/logger.js';
import type { AdKatsConfig } from '../core/config.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { InfractionRepository } from '../database/repositories/infraction.repository.js';
import type { RecordRepository } from '../database/repositories/record.repository.js';
import type { BanRepository } from '../database/repositories/ban.repository.js';
import type { PlayerService } from './player.service.js';
import type { APlayer, PlayerInfractions } from '../models/player.js';
import type { ARecord } from '../models/record.js';
import { createBan } from '../models/ban.js';

/**
 * Punishment types in the hierarchy.
 */
export type PunishmentType =
  | 'warn'
  | 'kill'
  | 'kick'
  | 'tban60'
  | 'tban120'
  | 'tbanday'
  | 'tbanweek'
  | 'tban2weeks'
  | 'tbanmonth'
  | 'ban';

/**
 * Result of a punishment execution.
 */
export interface PunishmentResult {
  success: boolean;
  punishmentType: PunishmentType;
  message: string;
  wasIro: boolean;
  effectivePoints: number;
  newTotalPoints: number;
}

/**
 * Result of a forgive operation.
 */
export interface ForgiveResult {
  success: boolean;
  message: string;
  pointsRemoved: number;
  newTotalPoints: number;
}

/**
 * Result of a warn operation.
 */
export interface WarnResult {
  success: boolean;
  message: string;
}

/**
 * Mapping of punishment types to their ban durations in minutes.
 */
const PUNISHMENT_DURATIONS: Record<PunishmentType, number | null> = {
  warn: null,
  kill: null,
  kick: null,
  tban60: 60,
  tban120: 120,
  tbanday: 1440,          // 24 * 60
  tbanweek: 10080,        // 7 * 24 * 60
  tban2weeks: 20160,      // 14 * 24 * 60
  tbanmonth: 43200,       // 30 * 24 * 60
  ban: null,              // Permanent ban
};

/**
 * Human-readable punishment names for messages.
 */
const PUNISHMENT_NAMES: Record<PunishmentType, string> = {
  warn: 'Warning',
  kill: 'Admin Kill',
  kick: 'Kick',
  tban60: '60-Minute Ban',
  tban120: '2-Hour Ban',
  tbanday: '1-Day Ban',
  tbanweek: '1-Week Ban',
  tban2weeks: '2-Week Ban',
  tbanmonth: '1-Month Ban',
  ban: 'Permanent Ban',
};

/**
 * Service for managing the infraction/punishment system.
 * Handles punishment hierarchy, IRO (Immediate Repeat Offense), and forgiveness.
 */
export class InfractionService {
  private logger: Logger;
  private config: AdKatsConfig;
  private bcAdapter: BattleConAdapter;
  private infractionRepo: InfractionRepository;
  private recordRepo: RecordRepository;
  private banRepo: BanRepository;
  private playerService: PlayerService;
  private serverId: number;

  constructor(
    logger: Logger,
    config: AdKatsConfig,
    bcAdapter: BattleConAdapter,
    infractionRepo: InfractionRepository,
    recordRepo: RecordRepository,
    banRepo: BanRepository,
    playerService: PlayerService,
    serverId: number
  ) {
    this.logger = logger;
    this.config = config;
    this.bcAdapter = bcAdapter;
    this.infractionRepo = infractionRepo;
    this.recordRepo = recordRepo;
    this.banRepo = banRepo;
    this.playerService = playerService;
    this.serverId = serverId;
  }

  /**
   * Issue a punishment to a player based on their current infraction points.
   * This is the main entry point for the punishment system.
   */
  async punish(
    source: APlayer,
    target: APlayer,
    reason: string,
    record: ARecord
  ): Promise<PunishmentResult> {
    try {
      // Get current infraction points
      const infractions = await this.infractionRepo.getServerPoints(target.playerId, this.serverId);
      let effectivePoints = infractions.serverPunishPoints - infractions.serverForgivePoints;

      // Check for IRO (Immediate Repeat Offense)
      const wasIro = await this.checkIro(target.playerId);
      if (wasIro) {
        // Double the effective points for IRO
        effectivePoints = effectivePoints * 2 + this.config.punishment.iroMultiplier;
        this.logger.info({
          player: target.soldierName,
          originalPoints: infractions.serverTotalPoints,
          effectivePoints,
        }, 'IRO detected - doubling punishment');
      }

      // Calculate which punishment to issue
      const punishmentType = this.calculatePunishment(effectivePoints);

      // Add a punish point BEFORE executing the punishment
      await this.infractionRepo.addPunishPoint(target.playerId, this.serverId);

      // Get the new total points after adding the punish point
      const newInfractions = await this.infractionRepo.getServerPoints(target.playerId, this.serverId);
      const newTotalPoints = newInfractions.serverPunishPoints - newInfractions.serverForgivePoints;

      // Execute the punishment
      await this.executePunishment(punishmentType, target, source, reason, record);

      // Update the player's cached infractions
      target.infractions = newInfractions;

      const punishmentName = PUNISHMENT_NAMES[punishmentType];
      const message = wasIro
        ? `${target.soldierName} has been issued a ${punishmentName} for ${reason} [IRO - Points: ${newTotalPoints}]`
        : `${target.soldierName} has been issued a ${punishmentName} for ${reason} [Points: ${newTotalPoints}]`;

      this.logger.info({
        source: source.soldierName,
        target: target.soldierName,
        punishment: punishmentType,
        reason,
        wasIro,
        effectivePoints,
        newTotalPoints,
      }, 'Punishment issued');

      return {
        success: true,
        punishmentType,
        message,
        wasIro,
        effectivePoints,
        newTotalPoints,
      };

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to issue punishment');

      return {
        success: false,
        punishmentType: 'warn',
        message: `Failed to punish ${target.soldierName}: ${msg}`,
        wasIro: false,
        effectivePoints: 0,
        newTotalPoints: 0,
      };
    }
  }

  /**
   * Forgive a player by removing infraction points.
   */
  async forgive(
    source: APlayer,
    target: APlayer,
    count: number,
    reason: string,
    record: ARecord
  ): Promise<ForgiveResult> {
    try {
      // Get current infraction points
      const infractions = await this.infractionRepo.getServerPoints(target.playerId, this.serverId);
      const currentPoints = infractions.serverPunishPoints - infractions.serverForgivePoints;

      // Limit forgive count to not go below zero
      const actualCount = Math.min(count, Math.max(0, currentPoints));

      if (actualCount <= 0) {
        return {
          success: false,
          message: `${target.soldierName} has no infraction points to forgive`,
          pointsRemoved: 0,
          newTotalPoints: currentPoints,
        };
      }

      // Add forgive points
      await this.infractionRepo.addForgivePoints(target.playerId, this.serverId, actualCount);

      // Get the new total points
      const newInfractions = await this.infractionRepo.getServerPoints(target.playerId, this.serverId);
      const newTotalPoints = newInfractions.serverPunishPoints - newInfractions.serverForgivePoints;

      // Update the player's cached infractions
      target.infractions = newInfractions;

      // Notify the target player if online
      if (target.isOnline) {
        await this.bcAdapter.sayPlayer(
          `You have been forgiven ${actualCount} infraction point(s) by ${source.soldierName}. ${reason ? `Reason: ${reason}` : ''} [Points: ${newTotalPoints}]`,
          target.soldierName
        );
      }

      this.logger.info({
        source: source.soldierName,
        target: target.soldierName,
        pointsRemoved: actualCount,
        reason,
        newTotalPoints,
      }, 'Player forgiven');

      return {
        success: true,
        message: `Forgave ${target.soldierName} ${actualCount} infraction point(s). New total: ${newTotalPoints}`,
        pointsRemoved: actualCount,
        newTotalPoints,
      };

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to forgive player');

      return {
        success: false,
        message: `Failed to forgive ${target.soldierName}: ${msg}`,
        pointsRemoved: 0,
        newTotalPoints: 0,
      };
    }
  }

  /**
   * Issue a warning to a player without adding infraction points.
   */
  async warn(
    source: APlayer,
    target: APlayer,
    reason: string,
    record: ARecord
  ): Promise<WarnResult> {
    try {
      if (!target.isOnline) {
        return {
          success: false,
          message: `Cannot warn ${target.soldierName} - player is not online`,
        };
      }

      // Send warning message to the player
      const warningMessage = `[WARNING] ${reason} - Issued by ${source.soldierName}`;
      await this.bcAdapter.sayPlayer(warningMessage, target.soldierName);

      // Optionally yell at the player for more visibility
      await this.bcAdapter.yellPlayer(
        `WARNING: ${reason}`,
        target.soldierName,
        5
      );

      this.logger.info({
        source: source.soldierName,
        target: target.soldierName,
        reason,
      }, 'Warning issued');

      return {
        success: true,
        message: `Warned ${target.soldierName}: ${reason}`,
      };

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to warn player');

      return {
        success: false,
        message: `Failed to warn ${target.soldierName}: ${msg}`,
      };
    }
  }

  /**
   * Calculate which punishment to issue based on effective points.
   * Uses the punishment hierarchy from configuration.
   */
  calculatePunishment(effectivePoints: number): PunishmentType {
    const hierarchy = this.config.punishment.hierarchy;

    // Points are 0-indexed into the hierarchy
    // Clamp to the valid range (0 to hierarchy.length - 1)
    const index = Math.min(Math.max(0, effectivePoints), hierarchy.length - 1);

    const punishment = hierarchy[index];
    if (!punishment || !this.isValidPunishmentType(punishment)) {
      // Fallback to warn if invalid
      this.logger.warn({ index, punishment }, 'Invalid punishment in hierarchy, falling back to warn');
      return 'warn';
    }

    return punishment;
  }

  /**
   * Execute a specific punishment action.
   */
  async executePunishment(
    punishmentType: PunishmentType,
    target: APlayer,
    source: APlayer,
    reason: string,
    record: ARecord
  ): Promise<void> {
    const punishmentName = PUNISHMENT_NAMES[punishmentType];
    const kickMessage = `[AdKats] ${punishmentName} by ${source.soldierName}: ${reason}`;

    switch (punishmentType) {
      case 'warn':
        await this.executeWarn(target, source, reason);
        break;

      case 'kill':
        await this.executeKill(target, source, reason);
        break;

      case 'kick':
        await this.executeKick(target, source, reason, kickMessage);
        break;

      case 'tban60':
      case 'tban120':
      case 'tbanday':
      case 'tbanweek':
      case 'tban2weeks':
      case 'tbanmonth':
        await this.executeTempBan(target, source, reason, punishmentType, record);
        break;

      case 'ban':
        await this.executePermBan(target, source, reason, record);
        break;

      default:
        this.logger.warn({ punishmentType }, 'Unknown punishment type');
        break;
    }
  }

  /**
   * Check if the player was recently punished (IRO - Immediate Repeat Offense).
   */
  private async checkIro(playerId: number): Promise<boolean> {
    const iroTimeout = this.config.punishment.iroTimeoutMinutes;
    return this.recordRepo.wasRecentlyPunished(playerId, this.serverId, iroTimeout);
  }

  /**
   * Execute a warning punishment.
   */
  private async executeWarn(target: APlayer, source: APlayer, reason: string): Promise<void> {
    if (!target.isOnline) {
      return;
    }

    const warningMessage = `[WARNING] You have been warned: ${reason}`;
    await this.bcAdapter.sayPlayer(warningMessage, target.soldierName);
    await this.bcAdapter.yellPlayer(`WARNING: ${reason}`, target.soldierName, 5);
  }

  /**
   * Execute a kill punishment.
   */
  private async executeKill(target: APlayer, source: APlayer, reason: string): Promise<void> {
    if (!target.isOnline) {
      return;
    }

    await this.bcAdapter.killPlayer(target.soldierName);
    await this.bcAdapter.sayPlayer(
      `You have been killed for: ${reason}`,
      target.soldierName
    );
    target.isAlive = false;
  }

  /**
   * Execute a kick punishment.
   */
  private async executeKick(
    target: APlayer,
    source: APlayer,
    reason: string,
    kickMessage: string
  ): Promise<void> {
    if (!target.isOnline) {
      return;
    }

    await this.bcAdapter.kickPlayer(target.soldierName, kickMessage);
    target.isOnline = false;
  }

  /**
   * Execute a temporary ban punishment.
   */
  private async executeTempBan(
    target: APlayer,
    source: APlayer,
    reason: string,
    punishmentType: PunishmentType,
    record: ARecord
  ): Promise<void> {
    const durationMinutes = PUNISHMENT_DURATIONS[punishmentType];
    if (durationMinutes === null) {
      this.logger.error({ punishmentType }, 'Temp ban has no duration');
      return;
    }

    const punishmentName = PUNISHMENT_NAMES[punishmentType];
    const banReason = `[${punishmentName}] ${reason} (Issued by ${source.soldierName})`;

    // Create ban record in database
    const ban = createBan(target, record, durationMinutes, banReason, {
      enforceGuid: true,
      enforceIp: false,
      enforceName: false,
    });

    await this.banRepo.upsert(ban);

    // Kick the player with ban message if online
    if (target.isOnline) {
      const durationSeconds = durationMinutes * 60;
      await this.bcAdapter.banByGuid(target.guid, 'seconds', durationSeconds, banReason);
      target.isOnline = false;
    }

    this.logger.info({
      target: target.soldierName,
      duration: durationMinutes,
      reason: banReason,
    }, 'Temporary ban issued');
  }

  /**
   * Execute a permanent ban punishment.
   */
  private async executePermBan(
    target: APlayer,
    source: APlayer,
    reason: string,
    record: ARecord
  ): Promise<void> {
    const banReason = `[Permanent Ban] ${reason} (Issued by ${source.soldierName})`;

    // Create ban record in database (null duration = permanent)
    const ban = createBan(target, record, null, banReason, {
      enforceGuid: true,
      enforceIp: false,
      enforceName: false,
    });

    await this.banRepo.upsert(ban);

    // Kick the player with ban message if online
    if (target.isOnline) {
      await this.bcAdapter.banByGuid(target.guid, 'perm', null, banReason);
      target.isOnline = false;
    }

    this.logger.info({
      target: target.soldierName,
      reason: banReason,
    }, 'Permanent ban issued');
  }

  /**
   * Type guard for valid punishment types.
   */
  private isValidPunishmentType(value: string): value is PunishmentType {
    return [
      'warn', 'kill', 'kick', 'tban60', 'tban120',
      'tbanday', 'tbanweek', 'tban2weeks', 'tbanmonth', 'ban'
    ].includes(value);
  }

  /**
   * Get a player's current infraction status for display.
   */
  async getInfractionStatus(playerId: number): Promise<{
    serverPoints: number;
    globalPoints: number;
    nextPunishment: PunishmentType;
    nextPunishmentName: string;
  }> {
    const infractions = await this.infractionRepo.getServerPoints(playerId, this.serverId);
    const serverPoints = infractions.serverPunishPoints - infractions.serverForgivePoints;
    const globalPoints = infractions.globalPunishPoints - infractions.globalForgivePoints;

    const nextPunishment = this.calculatePunishment(serverPoints);

    return {
      serverPoints,
      globalPoints,
      nextPunishment,
      nextPunishmentName: PUNISHMENT_NAMES[nextPunishment],
    };
  }

  /**
   * Get punishment name for a given type.
   */
  getPunishmentName(type: PunishmentType): string {
    return PUNISHMENT_NAMES[type];
  }

  /**
   * Get punishment duration in minutes for a given type.
   */
  getPunishmentDuration(type: PunishmentType): number | null {
    return PUNISHMENT_DURATIONS[type];
  }
}

/**
 * Create a new infraction service.
 */
export function createInfractionService(
  logger: Logger,
  config: AdKatsConfig,
  bcAdapter: BattleConAdapter,
  infractionRepo: InfractionRepository,
  recordRepo: RecordRepository,
  banRepo: BanRepository,
  playerService: PlayerService,
  serverId: number
): InfractionService {
  return new InfractionService(
    logger,
    config,
    bcAdapter,
    infractionRepo,
    recordRepo,
    banRepo,
    playerService,
    serverId
  );
}
