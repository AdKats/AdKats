import type { RowDataPacket } from 'mysql2/promise';
import type { Database } from '../connection.js';
import type { Logger } from '../../core/logger.js';
import type { APlayer, PlayerDbRow, PlayerInfractions } from '../../models/player.js';
import { playerFromDbRow } from '../../models/player.js';

/**
 * Extended player row with infraction data.
 */
interface PlayerWithInfractionsRow extends PlayerDbRow, RowDataPacket {
  server_punish_points?: number;
  server_forgive_points?: number;
  server_total_points?: number;
  global_punish_points?: number;
  global_forgive_points?: number;
  global_total_points?: number;
}

/**
 * Repository for player data operations.
 */
export class PlayerRepository {
  private db: Database;
  private logger: Logger;

  constructor(db: Database, logger: Logger) {
    this.db = db;
    this.logger = logger;
  }

  /**
   * Find a player by their database ID.
   */
  async findById(playerId: number): Promise<APlayer | null> {
    const row = await this.db.queryOne<PlayerDbRow & RowDataPacket>(
      `SELECT * FROM tbl_playerdata WHERE PlayerID = ?`,
      [playerId]
    );

    if (!row) {
      return null;
    }

    return playerFromDbRow(row);
  }

  /**
   * Find a player by their EA GUID.
   */
  async findByGuid(guid: string, gameId?: number): Promise<APlayer | null> {
    let sql = `SELECT * FROM tbl_playerdata WHERE EAGUID = ?`;
    const params: unknown[] = [guid];

    if (gameId !== undefined) {
      sql += ` AND GameID = ?`;
      params.push(gameId);
    }

    sql += ` LIMIT 1`;

    const row = await this.db.queryOne<PlayerDbRow & RowDataPacket>(sql, params);

    if (!row) {
      return null;
    }

    return playerFromDbRow(row);
  }

  /**
   * Find a player by their soldier name.
   */
  async findByName(soldierName: string, gameId?: number): Promise<APlayer | null> {
    let sql = `SELECT * FROM tbl_playerdata WHERE SoldierName = ?`;
    const params: unknown[] = [soldierName];

    if (gameId !== undefined) {
      sql += ` AND GameID = ?`;
      params.push(gameId);
    }

    sql += ` LIMIT 1`;

    const row = await this.db.queryOne<PlayerDbRow & RowDataPacket>(sql, params);

    if (!row) {
      return null;
    }

    return playerFromDbRow(row);
  }

  /**
   * Find a player by their IP address.
   */
  async findByIp(ipAddress: string, gameId?: number): Promise<APlayer[]> {
    let sql = `SELECT * FROM tbl_playerdata WHERE IP_Address = ?`;
    const params: unknown[] = [ipAddress];

    if (gameId !== undefined) {
      sql += ` AND GameID = ?`;
      params.push(gameId);
    }

    const rows = await this.db.query<(PlayerDbRow & RowDataPacket)[]>(sql, params);

    return rows.map(playerFromDbRow);
  }

  /**
   * Find or create a player by GUID.
   */
  async findOrCreate(
    soldierName: string,
    guid: string,
    gameId: number
  ): Promise<APlayer> {
    // Try to find existing player
    let player = await this.findByGuid(guid, gameId);

    if (player) {
      // Update name if changed
      if (player.soldierName !== soldierName) {
        await this.updateName(player.playerId, soldierName);
        player.soldierName = soldierName;
      }
      return player;
    }

    // Create new player
    const result = await this.db.execute(
      `INSERT INTO tbl_playerdata (GameID, SoldierName, EAGUID) VALUES (?, ?, ?)`,
      [gameId, soldierName, guid]
    );

    player = await this.findById(result.insertId);
    if (!player) {
      throw new Error(`Failed to create player: ${soldierName}`);
    }

    this.logger.info({ playerId: player.playerId, soldierName }, 'Created new player');
    return player;
  }

  /**
   * Update a player's name.
   */
  async updateName(playerId: number, soldierName: string): Promise<void> {
    await this.db.execute(
      `UPDATE tbl_playerdata SET SoldierName = ? WHERE PlayerID = ?`,
      [soldierName, playerId]
    );
  }

  /**
   * Update a player's IP address.
   */
  async updateIp(playerId: number, ipAddress: string): Promise<void> {
    await this.db.execute(
      `UPDATE tbl_playerdata SET IP_Address = ? WHERE PlayerID = ?`,
      [ipAddress, playerId]
    );
  }

  /**
   * Update a player's clan tag.
   */
  async updateClanTag(playerId: number, clanTag: string | null): Promise<void> {
    await this.db.execute(
      `UPDATE tbl_playerdata SET ClanTag = ? WHERE PlayerID = ?`,
      [clanTag, playerId]
    );
  }

  /**
   * Get player infraction points.
   */
  async getInfractions(playerId: number, serverId: number): Promise<PlayerInfractions | null> {
    const row = await this.db.queryOne<PlayerWithInfractionsRow>(
      `SELECT
        COALESCE(s.punish_points, 0) as server_punish_points,
        COALESCE(s.forgive_points, 0) as server_forgive_points,
        COALESCE(s.total_points, 0) as server_total_points,
        COALESCE(g.punish_points, 0) as global_punish_points,
        COALESCE(g.forgive_points, 0) as global_forgive_points,
        COALESCE(g.total_points, 0) as global_total_points
      FROM tbl_playerdata p
      LEFT JOIN adkats_infractions_server s ON p.PlayerID = s.player_id AND s.server_id = ?
      LEFT JOIN adkats_infractions_global g ON p.PlayerID = g.player_id
      WHERE p.PlayerID = ?`,
      [serverId, playerId]
    );

    if (!row) {
      return null;
    }

    return {
      serverPunishPoints: row.server_punish_points ?? 0,
      serverForgivePoints: row.server_forgive_points ?? 0,
      serverTotalPoints: row.server_total_points ?? 0,
      globalPunishPoints: row.global_punish_points ?? 0,
      globalForgivePoints: row.global_forgive_points ?? 0,
      globalTotalPoints: row.global_total_points ?? 0,
    };
  }

  /**
   * Get player reputation.
   */
  async getReputation(playerId: number): Promise<number | null> {
    const row = await this.db.queryOne<{ total_rep: number } & RowDataPacket>(
      `SELECT total_rep FROM adkats_player_reputation WHERE player_id = ?`,
      [playerId]
    );

    return row?.total_rep ?? null;
  }

  /**
   * Search for players by partial name.
   */
  async searchByName(searchTerm: string, gameId?: number, limit: number = 10): Promise<APlayer[]> {
    let sql = `SELECT * FROM tbl_playerdata WHERE SoldierName LIKE ?`;
    const params: unknown[] = [`%${searchTerm}%`];

    if (gameId !== undefined) {
      sql += ` AND GameID = ?`;
      params.push(gameId);
    }

    sql += ` ORDER BY SoldierName LIMIT ?`;
    params.push(limit);

    const rows = await this.db.query<(PlayerDbRow & RowDataPacket)[]>(sql, params);

    return rows.map(playerFromDbRow);
  }

  /**
   * Get player with full data including infractions and reputation.
   */
  async findByIdWithDetails(playerId: number, serverId: number): Promise<APlayer | null> {
    const player = await this.findById(playerId);
    if (!player) {
      return null;
    }

    // Load infractions
    player.infractions = await this.getInfractions(playerId, serverId);

    // Load reputation
    player.reputation = await this.getReputation(playerId);

    return player;
  }
}

/**
 * Create a new player repository.
 */
export function createPlayerRepository(db: Database, logger: Logger): PlayerRepository {
  return new PlayerRepository(db, logger);
}
