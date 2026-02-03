import type { RowDataPacket } from 'mysql2/promise';
import type { Database } from '../connection.js';
import type { Logger } from '../../core/logger.js';

/**
 * Special player entry - represents a player's membership in a special group.
 * Maps to adkats_specialplayers table.
 */
export interface SpecialPlayerEntry {
  /** Unique ID of this entry */
  specialplayerId: number;
  /** The group key (e.g., 'whitelist_anticheat', 'slot_reserved') */
  groupKey: string;
  /** Player ID from tbl_playerdata (can be null for identifier-based entries) */
  playerId: number | null;
  /** Game ID (optional, for game-specific groups) */
  gameId: number | null;
  /** Server ID (optional, for server-specific groups) */
  serverId: number | null;
  /** Player identifier (name or other identifier, for entries without player_id) */
  playerIdentifier: string | null;
  /** When this entry became effective */
  effectiveDate: Date;
  /** When this entry expires */
  expirationDate: Date;
}

/**
 * Database row representation for special players.
 */
export interface SpecialPlayerDbRow {
  specialplayer_id: number;
  player_group: string;
  player_id: number | null;
  player_game: number | null;
  player_server: number | null;
  player_identifier: string | null;
  player_effective: Date;
  player_expiration: Date;
}

/**
 * Convert database row to SpecialPlayerEntry.
 */
export function specialPlayerFromDbRow(row: SpecialPlayerDbRow): SpecialPlayerEntry {
  return {
    specialplayerId: row.specialplayer_id,
    groupKey: row.player_group,
    playerId: row.player_id,
    gameId: row.player_game,
    serverId: row.player_server,
    playerIdentifier: row.player_identifier,
    effectiveDate: new Date(row.player_effective),
    expirationDate: new Date(row.player_expiration),
  };
}

/**
 * Check if a special player entry is currently active (not expired).
 */
export function isSpecialPlayerActive(entry: SpecialPlayerEntry): boolean {
  return entry.expirationDate.getTime() > Date.now();
}

/**
 * Check if a special player entry is permanent (expires in 20+ years).
 */
export function isSpecialPlayerPermanent(entry: SpecialPlayerEntry): boolean {
  const twentyYearsMs = 20 * 365 * 24 * 60 * 60 * 1000;
  return entry.expirationDate.getTime() - Date.now() > twentyYearsMs;
}

/**
 * Repository for special player group operations.
 * Handles whitelist/blacklist memberships in adkats_specialplayers table.
 */
export class SpecialPlayerRepository {
  private db: Database;
  private logger: Logger;

  constructor(db: Database, logger: Logger) {
    this.db = db;
    this.logger = logger;
  }

  /**
   * Find a special player entry by player ID and group key.
   * Returns the active entry if one exists.
   */
  async findByPlayerId(playerId: number, groupKey: string): Promise<SpecialPlayerEntry | null> {
    const row = await this.db.queryOne<SpecialPlayerDbRow & RowDataPacket>(
      `SELECT * FROM adkats_specialplayers
       WHERE player_id = ?
       AND player_group = ?
       AND player_expiration > UTC_TIMESTAMP()
       ORDER BY player_expiration DESC
       LIMIT 1`,
      [playerId, groupKey]
    );

    if (!row) {
      return null;
    }

    return specialPlayerFromDbRow(row);
  }

  /**
   * Find all entries for a player ID (all groups).
   */
  async findAllByPlayerId(playerId: number): Promise<SpecialPlayerEntry[]> {
    const rows = await this.db.query<(SpecialPlayerDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_specialplayers
       WHERE player_id = ?
       AND player_expiration > UTC_TIMESTAMP()
       ORDER BY player_group`,
      [playerId]
    );

    return rows.map(specialPlayerFromDbRow);
  }

  /**
   * Find all active entries for a group key.
   */
  async findByGroupKey(groupKey: string): Promise<SpecialPlayerEntry[]> {
    const rows = await this.db.query<(SpecialPlayerDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_specialplayers
       WHERE player_group = ?
       AND player_expiration > UTC_TIMESTAMP()
       ORDER BY player_id`,
      [groupKey]
    );

    return rows.map(specialPlayerFromDbRow);
  }

  /**
   * Find entries by group key with optional server/game filtering.
   */
  async findByGroupKeyFiltered(
    groupKey: string,
    options?: { serverId?: number; gameId?: number }
  ): Promise<SpecialPlayerEntry[]> {
    let sql = `SELECT * FROM adkats_specialplayers
               WHERE player_group = ?
               AND player_expiration > UTC_TIMESTAMP()`;
    const params: (string | number)[] = [groupKey];

    if (options?.serverId !== undefined) {
      sql += ` AND (player_server IS NULL OR player_server = ?)`;
      params.push(options.serverId);
    }

    if (options?.gameId !== undefined) {
      sql += ` AND (player_game IS NULL OR player_game = ?)`;
      params.push(options.gameId);
    }

    sql += ` ORDER BY player_id`;

    const rows = await this.db.query<(SpecialPlayerDbRow & RowDataPacket)[]>(sql, params);

    return rows.map(specialPlayerFromDbRow);
  }

  /**
   * Add a player to a special group.
   * First removes any existing entry for this player/group, then inserts new entry.
   *
   * @param playerId - Player's database ID
   * @param groupKey - Group key (e.g., 'whitelist_anticheat')
   * @param playerName - Player's soldier name (used as identifier backup)
   * @param durationMinutes - Duration in minutes (null for permanent, uses 20 years)
   * @param options - Optional server/game specific settings
   */
  async add(
    playerId: number,
    groupKey: string,
    playerName: string,
    durationMinutes: number | null,
    options?: { serverId?: number; gameId?: number }
  ): Promise<SpecialPlayerEntry> {
    // Maximum duration is ~20 years (same as C# code: 10518984 minutes)
    const MAX_DURATION_MINUTES = 10518984;
    const effectiveDuration = durationMinutes === null
      ? MAX_DURATION_MINUTES
      : Math.min(durationMinutes, MAX_DURATION_MINUTES);

    // First, remove any existing entries for this player/group combination
    await this.db.execute(
      `DELETE FROM adkats_specialplayers
       WHERE player_group = ?
       AND (player_id = ? OR player_identifier = ?)`,
      [groupKey, playerId, playerName]
    );

    // Insert the new entry
    const result = await this.db.execute(
      `INSERT INTO adkats_specialplayers
        (player_group, player_id, player_identifier, player_game, player_server, player_effective, player_expiration)
       VALUES (?, ?, ?, ?, ?, UTC_TIMESTAMP(), DATE_ADD(UTC_TIMESTAMP(), INTERVAL ? MINUTE))`,
      [
        groupKey,
        playerId,
        playerName,
        options?.gameId ?? null,
        options?.serverId ?? null,
        effectiveDuration,
      ]
    );

    this.logger.info(
      { playerId, groupKey, durationMinutes: effectiveDuration, insertId: result.insertId },
      'Added player to special group'
    );

    // Fetch and return the created entry
    const entry = await this.findByPlayerId(playerId, groupKey);
    if (!entry) {
      throw new Error('Failed to retrieve created special player entry');
    }

    return entry;
  }

  /**
   * Remove a player from a special group.
   *
   * @param playerId - Player's database ID
   * @param groupKey - Group key to remove from
   * @returns Number of entries removed
   */
  async remove(playerId: number, groupKey: string): Promise<number> {
    const result = await this.db.execute(
      `DELETE FROM adkats_specialplayers
       WHERE player_id = ?
       AND player_group = ?`,
      [playerId, groupKey]
    );

    if (result.affectedRows > 0) {
      this.logger.info({ playerId, groupKey, count: result.affectedRows }, 'Removed player from special group');
    }

    return result.affectedRows;
  }

  /**
   * Remove a player from a group by player name/identifier.
   */
  async removeByIdentifier(playerIdentifier: string, groupKey: string): Promise<number> {
    const result = await this.db.execute(
      `DELETE FROM adkats_specialplayers
       WHERE player_identifier = ?
       AND player_group = ?`,
      [playerIdentifier, groupKey]
    );

    if (result.affectedRows > 0) {
      this.logger.info(
        { playerIdentifier, groupKey, count: result.affectedRows },
        'Removed player from special group by identifier'
      );
    }

    return result.affectedRows;
  }

  /**
   * Remove a specific entry by its ID.
   */
  async removeById(specialplayerId: number): Promise<boolean> {
    const result = await this.db.execute(
      `DELETE FROM adkats_specialplayers WHERE specialplayer_id = ?`,
      [specialplayerId]
    );

    if (result.affectedRows > 0) {
      this.logger.info({ specialplayerId }, 'Removed special player entry by ID');
      return true;
    }

    return false;
  }

  /**
   * Check if a player is in a specific group.
   * Returns true if player has an active (non-expired) entry.
   */
  async isPlayerInGroup(playerId: number, groupKey: string): Promise<boolean> {
    const row = await this.db.queryOne<{ count: number } & RowDataPacket>(
      `SELECT COUNT(*) as count FROM adkats_specialplayers
       WHERE player_id = ?
       AND player_group = ?
       AND player_expiration > UTC_TIMESTAMP()`,
      [playerId, groupKey]
    );

    return row !== null && row.count > 0;
  }

  /**
   * Check if a player (by name) is in a specific group.
   * Checks both player_id and player_identifier.
   */
  async isPlayerInGroupByName(playerName: string, groupKey: string): Promise<boolean> {
    const row = await this.db.queryOne<{ count: number } & RowDataPacket>(
      `SELECT COUNT(*) as count FROM adkats_specialplayers
       WHERE player_identifier = ?
       AND player_group = ?
       AND player_expiration > UTC_TIMESTAMP()`,
      [playerName, groupKey]
    );

    return row !== null && row.count > 0;
  }

  /**
   * Get all groups a player belongs to.
   */
  async getPlayerGroups(playerId: number): Promise<string[]> {
    const rows = await this.db.query<({ player_group: string } & RowDataPacket)[]>(
      `SELECT DISTINCT player_group FROM adkats_specialplayers
       WHERE player_id = ?
       AND player_expiration > UTC_TIMESTAMP()
       ORDER BY player_group`,
      [playerId]
    );

    return rows.map((row) => row.player_group);
  }

  /**
   * Expire old entries by updating or deleting them.
   * This is a maintenance operation to clean up the table.
   * Returns the number of entries that were expired/removed.
   */
  async expireOldEntries(): Promise<number> {
    const result = await this.db.execute(
      `DELETE FROM adkats_specialplayers
       WHERE player_expiration <= UTC_TIMESTAMP()`
    );

    if (result.affectedRows > 0) {
      this.logger.info({ count: result.affectedRows }, 'Expired old special player entries');
    }

    return result.affectedRows;
  }

  /**
   * Get all player IDs in a specific group.
   * Useful for bulk operations or caching.
   */
  async getPlayerIdsInGroup(groupKey: string): Promise<number[]> {
    const rows = await this.db.query<({ player_id: number } & RowDataPacket)[]>(
      `SELECT DISTINCT player_id FROM adkats_specialplayers
       WHERE player_group = ?
       AND player_id IS NOT NULL
       AND player_expiration > UTC_TIMESTAMP()`,
      [groupKey]
    );

    return rows.map((row) => row.player_id);
  }

  /**
   * Count members in a specific group.
   */
  async countGroupMembers(groupKey: string): Promise<number> {
    const row = await this.db.queryOne<{ count: number } & RowDataPacket>(
      `SELECT COUNT(*) as count FROM adkats_specialplayers
       WHERE player_group = ?
       AND player_expiration > UTC_TIMESTAMP()`,
      [groupKey]
    );

    return row?.count ?? 0;
  }

  /**
   * Find matching entries for a player across all groups.
   * Used for display purposes.
   */
  async findMatchingForPlayer(
    playerId: number,
    playerName: string
  ): Promise<SpecialPlayerEntry[]> {
    const rows = await this.db.query<(SpecialPlayerDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_specialplayers
       WHERE (player_id = ? OR player_identifier = ?)
       AND player_expiration > UTC_TIMESTAMP()
       ORDER BY player_group`,
      [playerId, playerName]
    );

    return rows.map(specialPlayerFromDbRow);
  }

  /**
   * Update the expiration date of an existing entry.
   */
  async updateExpiration(
    specialplayerId: number,
    newExpirationMinutes: number
  ): Promise<boolean> {
    const result = await this.db.execute(
      `UPDATE adkats_specialplayers
       SET player_expiration = DATE_ADD(UTC_TIMESTAMP(), INTERVAL ? MINUTE)
       WHERE specialplayer_id = ?`,
      [newExpirationMinutes, specialplayerId]
    );

    if (result.affectedRows > 0) {
      this.logger.info({ specialplayerId, newExpirationMinutes }, 'Updated special player expiration');
      return true;
    }

    return false;
  }
}

/**
 * Create a new special player repository.
 */
export function createSpecialPlayerRepository(db: Database, logger: Logger): SpecialPlayerRepository {
  return new SpecialPlayerRepository(db, logger);
}
