import type { RowDataPacket } from 'mysql2/promise';
import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { Database } from '../../database/connection.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Mute status tracking for players.
 * In-memory cache synchronized with database.
 */
export interface MuteStatus {
  playerId: number;
  mutedBy: string;
  reason: string;
  mutedAt: Date;
  expiresAt: Date | null; // null = permanent
}

/**
 * Lock status tracking for players.
 * Prevents admin commands from being executed on the player.
 */
export interface LockStatus {
  playerId: number;
  lockedBy: string;
  reason: string;
  lockedAt: Date;
}

/**
 * Mute command - prevent a player from using chat.
 * Usage: @mute <player> [duration] [reason]
 *
 * Duration can be specified in minutes (e.g., 30) or with suffix (30m, 1h, 1d).
 * If no duration specified, mute is permanent until unmuted.
 */
export class MuteCommand extends BaseCommand {
  private db: Database;
  private serverId: number;
  private mutedPlayers: Map<number, MuteStatus>;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    db: Database,
    serverId: number,
    mutedPlayers: Map<number, MuteStatus>
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.db = db;
    this.serverId = serverId;
    this.mutedPlayers = mutedPlayers;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.MUTE];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target player
    if (!this.requireTarget(ctx)) {
      await this.respond(ctx, 'Usage: @mute <player> [duration] [reason]');
      return;
    }

    const target = ctx.targetPlayer!;

    // Check if already muted
    if (this.mutedPlayers.has(target.playerId)) {
      await this.respondError(ctx, `${target.soldierName} is already muted`);
      return;
    }

    // Parse arguments: [duration] [reason]
    let durationMinutes: number | null = null;
    let reason = 'Muted by admin';

    if (ctx.args) {
      const parts = ctx.args.trim().split(/\s+/);
      const firstPart = parts[0];

      // Try to parse duration from first part
      if (firstPart) {
        const parsed = this.parseDuration(firstPart);
        if (parsed !== null) {
          durationMinutes = parsed;
          reason = parts.slice(1).join(' ') || reason;
        } else {
          // First part is not a duration, treat all as reason
          reason = ctx.args.trim();
        }
      }
    }

    try {
      // Calculate expiration time
      const now = new Date();
      const expiresAt = durationMinutes !== null
        ? new Date(now.getTime() + durationMinutes * 60 * 1000)
        : null;

      // Create mute status
      const muteStatus: MuteStatus = {
        playerId: target.playerId,
        mutedBy: ctx.player.soldierName,
        reason,
        mutedAt: now,
        expiresAt,
      };

      // Store in database
      await this.db.execute(
        `INSERT INTO adkats_player_mutes (player_id, server_id, muted_by, reason, muted_at, expires_at)
         VALUES (?, ?, ?, ?, ?, ?)
         ON DUPLICATE KEY UPDATE muted_by = ?, reason = ?, muted_at = ?, expires_at = ?`,
        [
          target.playerId, this.serverId, ctx.player.soldierName, reason, now, expiresAt,
          ctx.player.soldierName, reason, now, expiresAt,
        ]
      );

      // Store in memory
      this.mutedPlayers.set(target.playerId, muteStatus);

      // Update record
      ctx.record.recordMessage = reason;
      await this.logRecord(ctx);

      // Format duration message
      const durationMsg = durationMinutes !== null
        ? `for ${this.formatDuration(durationMinutes)}`
        : 'permanently';

      await this.respondSuccess(ctx, `${target.soldierName} has been muted ${durationMsg}`);

      // Notify the player
      await this.bcAdapter.sayPlayer(
        `You have been muted ${durationMsg}. Reason: ${reason}`,
        target.soldierName
      );

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        duration: durationMinutes,
        reason,
      }, 'Player muted');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to mute player');
      await this.respondError(ctx, `Failed to mute ${target.soldierName}`);
    }
  }

  /**
   * Parse duration string to minutes.
   * Supports: 30, 30m, 1h, 2d formats.
   */
  private parseDuration(str: string): number | null {
    // Check for numeric only (minutes)
    if (/^\d+$/.test(str)) {
      return parseInt(str, 10);
    }

    // Check for suffix format
    const match = str.match(/^(\d+)(m|h|d)$/i);
    if (!match) {
      return null;
    }

    const value = parseInt(match[1]!, 10);
    const unit = match[2]!.toLowerCase();

    switch (unit) {
      case 'm':
        return value;
      case 'h':
        return value * 60;
      case 'd':
        return value * 60 * 24;
      default:
        return null;
    }
  }

  /**
   * Format duration in minutes to human-readable string.
   */
  private formatDuration(minutes: number): string {
    if (minutes < 60) {
      return `${minutes} minute${minutes !== 1 ? 's' : ''}`;
    }
    if (minutes < 60 * 24) {
      const hours = Math.floor(minutes / 60);
      return `${hours} hour${hours !== 1 ? 's' : ''}`;
    }
    const days = Math.floor(minutes / (60 * 24));
    return `${days} day${days !== 1 ? 's' : ''}`;
  }
}

/**
 * Unmute command - remove mute from a player.
 * Usage: @unmute <player>
 */
export class UnmuteCommand extends BaseCommand {
  private db: Database;
  private serverId: number;
  private mutedPlayers: Map<number, MuteStatus>;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    db: Database,
    serverId: number,
    mutedPlayers: Map<number, MuteStatus>
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.db = db;
    this.serverId = serverId;
    this.mutedPlayers = mutedPlayers;
  }

  getCommandKeys(): string[] {
    // Using a custom key since UNMUTE may not be in CommandKeys
    return ['player_unmute'];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target player
    if (!this.requireTarget(ctx)) {
      await this.respond(ctx, 'Usage: @unmute <player>');
      return;
    }

    const target = ctx.targetPlayer!;

    // Check if actually muted
    if (!this.mutedPlayers.has(target.playerId)) {
      await this.respondError(ctx, `${target.soldierName} is not muted`);
      return;
    }

    try {
      // Remove from database
      await this.db.execute(
        `DELETE FROM adkats_player_mutes WHERE player_id = ? AND server_id = ?`,
        [target.playerId, this.serverId]
      );

      // Remove from memory
      this.mutedPlayers.delete(target.playerId);

      // Update record
      ctx.record.recordMessage = `Unmuted by ${ctx.player.soldierName}`;
      await this.logRecord(ctx);

      await this.respondSuccess(ctx, `${target.soldierName} has been unmuted`);

      // Notify the player
      await this.bcAdapter.sayPlayer(
        'You have been unmuted.',
        target.soldierName
      );

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
      }, 'Player unmuted');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to unmute player');
      await this.respondError(ctx, `Failed to unmute ${target.soldierName}`);
    }
  }
}

/**
 * Lock command - prevent admin commands from being used on a player.
 * Usage: @lock <player> [reason]
 *
 * This is used to protect players from accidental admin actions,
 * for example when an admin is investigating a player.
 */
export class LockCommand extends BaseCommand {
  private lockedPlayers: Map<number, LockStatus>;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    lockedPlayers: Map<number, LockStatus>
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.lockedPlayers = lockedPlayers;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.LOCK];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target player
    if (!this.requireTarget(ctx)) {
      await this.respond(ctx, 'Usage: @lock <player> [reason]');
      return;
    }

    const target = ctx.targetPlayer!;

    // Check if already locked
    const existingLock = this.lockedPlayers.get(target.playerId);
    if (existingLock) {
      await this.respondError(
        ctx,
        `${target.soldierName} is already locked by ${existingLock.lockedBy}`
      );
      return;
    }

    const reason = ctx.args?.trim() || 'Locked by admin';

    try {
      // Create lock status
      const lockStatus: LockStatus = {
        playerId: target.playerId,
        lockedBy: ctx.player.soldierName,
        reason,
        lockedAt: new Date(),
      };

      // Store in memory (locks are temporary, session-based)
      this.lockedPlayers.set(target.playerId, lockStatus);

      // Update record
      ctx.record.recordMessage = reason;
      await this.logRecord(ctx);

      await this.respondSuccess(
        ctx,
        `${target.soldierName} is now locked. Admin commands will be blocked.`
      );

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        reason,
      }, 'Player locked');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to lock player');
      await this.respondError(ctx, `Failed to lock ${target.soldierName}`);
    }
  }
}

/**
 * Unlock command - remove lock from a player.
 * Usage: @unlock <player>
 */
export class UnlockCommand extends BaseCommand {
  private lockedPlayers: Map<number, LockStatus>;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    lockedPlayers: Map<number, LockStatus>
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.lockedPlayers = lockedPlayers;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.UNLOCK];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target player
    if (!this.requireTarget(ctx)) {
      await this.respond(ctx, 'Usage: @unlock <player>');
      return;
    }

    const target = ctx.targetPlayer!;

    // Check if actually locked
    const lockStatus = this.lockedPlayers.get(target.playerId);
    if (!lockStatus) {
      await this.respondError(ctx, `${target.soldierName} is not locked`);
      return;
    }

    try {
      // Remove from memory
      this.lockedPlayers.delete(target.playerId);

      // Update record
      ctx.record.recordMessage = `Unlocked by ${ctx.player.soldierName} (was locked by ${lockStatus.lockedBy})`;
      await this.logRecord(ctx);

      await this.respondSuccess(ctx, `${target.soldierName} has been unlocked`);

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        previousLockedBy: lockStatus.lockedBy,
      }, 'Player unlocked');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to unlock player');
      await this.respondError(ctx, `Failed to unlock ${target.soldierName}`);
    }
  }
}

/**
 * Check if a player is muted.
 */
export function isPlayerMuted(
  playerId: number,
  mutedPlayers: Map<number, MuteStatus>
): boolean {
  const status = mutedPlayers.get(playerId);
  if (!status) {
    return false;
  }

  // Check if mute has expired
  if (status.expiresAt && status.expiresAt < new Date()) {
    mutedPlayers.delete(playerId);
    return false;
  }

  return true;
}

/**
 * Check if a player is locked.
 */
export function isPlayerLocked(
  playerId: number,
  lockedPlayers: Map<number, LockStatus>
): boolean {
  return lockedPlayers.has(playerId);
}

/**
 * Get lock status for a player.
 */
export function getPlayerLockStatus(
  playerId: number,
  lockedPlayers: Map<number, LockStatus>
): LockStatus | null {
  return lockedPlayers.get(playerId) ?? null;
}

/**
 * Load muted players from database.
 */
export async function loadMutedPlayers(
  db: Database,
  serverId: number
): Promise<Map<number, MuteStatus>> {
  const mutedPlayers = new Map<number, MuteStatus>();

  const rows = await db.query<(RowDataPacket & {
    player_id: number;
    muted_by: string;
    reason: string;
    muted_at: Date;
    expires_at: Date | null;
  })[]>(
    `SELECT player_id, muted_by, reason, muted_at, expires_at
     FROM adkats_player_mutes
     WHERE server_id = ? AND (expires_at IS NULL OR expires_at > NOW())`,
    [serverId]
  );

  for (const row of rows) {
    mutedPlayers.set(row.player_id, {
      playerId: row.player_id,
      mutedBy: row.muted_by,
      reason: row.reason,
      mutedAt: row.muted_at,
      expiresAt: row.expires_at,
    });
  }

  return mutedPlayers;
}

/**
 * Dependencies required for player control commands.
 */
export interface PlayerControlDependencies {
  logger: Logger;
  bcAdapter: BattleConAdapter;
  commandService: CommandService;
  recordRepo: RecordRepository;
  db: Database;
  serverId: number;
  mutedPlayers: Map<number, MuteStatus>;
  lockedPlayers: Map<number, LockStatus>;
}

/**
 * Create and register all player control commands.
 */
export function registerPlayerControlCommands(deps: PlayerControlDependencies): {
  mute: MuteCommand;
  unmute: UnmuteCommand;
  lock: LockCommand;
  unlock: UnlockCommand;
} {
  const {
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    db,
    serverId,
    mutedPlayers,
    lockedPlayers,
  } = deps;

  const mute = new MuteCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    db,
    serverId,
    mutedPlayers
  );
  mute.register();

  const unmute = new UnmuteCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    db,
    serverId,
    mutedPlayers
  );
  unmute.register();

  const lock = new LockCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    lockedPlayers
  );
  lock.register();

  const unlock = new UnlockCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    lockedPlayers
  );
  unlock.register();

  return { mute, unmute, lock, unlock };
}
