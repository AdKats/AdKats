import type { RowDataPacket } from 'mysql2/promise';
import type { Database } from '../connection.js';
import type { Logger } from '../../core/logger.js';
import type { PlayerInfractions } from '../../models/player.js';

/**
 * Server infraction database row.
 */
interface ServerInfractionRow extends RowDataPacket {
  player_id: number;
  server_id: number;
  punish_points: number;
  forgive_points: number;
  total_points: number;
}

/**
 * Global infraction database row.
 */
interface GlobalInfractionRow extends RowDataPacket {
  player_id: number;
  punish_points: number;
  forgive_points: number;
  total_points: number;
}

/**
 * Repository for infraction point tracking operations.
 * Handles server-specific and global infraction points.
 */
export class InfractionRepository {
  private db: Database;
  private logger: Logger;

  constructor(db: Database, logger: Logger) {
    this.db = db;
    this.logger = logger;
  }

  /**
   * Get server-specific infraction points for a player.
   */
  async getServerPoints(playerId: number, serverId: number): Promise<PlayerInfractions> {
    const row = await this.db.queryOne<ServerInfractionRow>(
      `SELECT punish_points, forgive_points, total_points
       FROM adkats_infractions_server
       WHERE player_id = ? AND server_id = ?`,
      [playerId, serverId]
    );

    const globalRow = await this.db.queryOne<GlobalInfractionRow>(
      `SELECT punish_points, forgive_points, total_points
       FROM adkats_infractions_global
       WHERE player_id = ?`,
      [playerId]
    );

    return {
      serverPunishPoints: row?.punish_points ?? 0,
      serverForgivePoints: row?.forgive_points ?? 0,
      serverTotalPoints: row?.total_points ?? 0,
      globalPunishPoints: globalRow?.punish_points ?? 0,
      globalForgivePoints: globalRow?.forgive_points ?? 0,
      globalTotalPoints: globalRow?.total_points ?? 0,
    };
  }

  /**
   * Get global infraction points for a player (across all servers).
   */
  async getGlobalPoints(playerId: number): Promise<{
    punishPoints: number;
    forgivePoints: number;
    totalPoints: number;
  }> {
    const row = await this.db.queryOne<GlobalInfractionRow>(
      `SELECT punish_points, forgive_points, total_points
       FROM adkats_infractions_global
       WHERE player_id = ?`,
      [playerId]
    );

    return {
      punishPoints: row?.punish_points ?? 0,
      forgivePoints: row?.forgive_points ?? 0,
      totalPoints: row?.total_points ?? 0,
    };
  }

  /**
   * Add a punish point to a player's record.
   * Updates both server-specific and global infraction tables.
   */
  async addPunishPoint(playerId: number, serverId: number): Promise<void> {
    // Update or insert server-specific points
    await this.db.execute(
      `INSERT INTO adkats_infractions_server (player_id, server_id, punish_points, forgive_points, total_points)
       VALUES (?, ?, 1, 0, 1)
       ON DUPLICATE KEY UPDATE
         punish_points = punish_points + 1,
         total_points = punish_points - forgive_points + 1`,
      [playerId, serverId]
    );

    // Update or insert global points
    await this.db.execute(
      `INSERT INTO adkats_infractions_global (player_id, punish_points, forgive_points, total_points)
       VALUES (?, 1, 0, 1)
       ON DUPLICATE KEY UPDATE
         punish_points = punish_points + 1,
         total_points = punish_points - forgive_points + 1`,
      [playerId]
    );

    this.logger.debug({ playerId, serverId }, 'Added punish point');
  }

  /**
   * Add forgive points to a player's record.
   * Updates both server-specific and global infraction tables.
   */
  async addForgivePoints(playerId: number, serverId: number, count: number = 1): Promise<void> {
    if (count <= 0) {
      return;
    }

    // Update or insert server-specific points
    await this.db.execute(
      `INSERT INTO adkats_infractions_server (player_id, server_id, punish_points, forgive_points, total_points)
       VALUES (?, ?, 0, ?, ?)
       ON DUPLICATE KEY UPDATE
         forgive_points = forgive_points + ?,
         total_points = punish_points - forgive_points - ?`,
      [playerId, serverId, count, -count, count, count]
    );

    // Update or insert global points
    await this.db.execute(
      `INSERT INTO adkats_infractions_global (player_id, punish_points, forgive_points, total_points)
       VALUES (?, 0, ?, ?)
       ON DUPLICATE KEY UPDATE
         forgive_points = forgive_points + ?,
         total_points = punish_points - forgive_points - ?`,
      [playerId, count, -count, count, count]
    );

    this.logger.debug({ playerId, serverId, count }, 'Added forgive points');
  }

  /**
   * Reset a player's infractions for a specific server.
   */
  async resetServerPoints(playerId: number, serverId: number): Promise<void> {
    await this.db.execute(
      `DELETE FROM adkats_infractions_server WHERE player_id = ? AND server_id = ?`,
      [playerId, serverId]
    );

    this.logger.debug({ playerId, serverId }, 'Reset server infraction points');
  }

  /**
   * Reset a player's global infractions.
   */
  async resetGlobalPoints(playerId: number): Promise<void> {
    await this.db.execute(
      `DELETE FROM adkats_infractions_global WHERE player_id = ?`,
      [playerId]
    );

    this.logger.debug({ playerId }, 'Reset global infraction points');
  }

  /**
   * Get the effective total points for a player on a server.
   * This is the value used to determine punishment level.
   */
  async getEffectivePoints(playerId: number, serverId: number): Promise<number> {
    const infractions = await this.getServerPoints(playerId, serverId);
    return infractions.serverPunishPoints - infractions.serverForgivePoints;
  }

  /**
   * Get infraction summary for multiple players.
   */
  async getMultipleServerPoints(
    playerIds: number[],
    serverId: number
  ): Promise<Map<number, PlayerInfractions>> {
    if (playerIds.length === 0) {
      return new Map();
    }

    const placeholders = playerIds.map(() => '?').join(',');
    const rows = await this.db.query<(ServerInfractionRow & {
      global_punish_points?: number;
      global_forgive_points?: number;
      global_total_points?: number;
    })[]>(
      `SELECT
        s.player_id,
        COALESCE(s.punish_points, 0) as punish_points,
        COALESCE(s.forgive_points, 0) as forgive_points,
        COALESCE(s.total_points, 0) as total_points,
        COALESCE(g.punish_points, 0) as global_punish_points,
        COALESCE(g.forgive_points, 0) as global_forgive_points,
        COALESCE(g.total_points, 0) as global_total_points
       FROM tbl_playerdata p
       LEFT JOIN adkats_infractions_server s ON p.PlayerID = s.player_id AND s.server_id = ?
       LEFT JOIN adkats_infractions_global g ON p.PlayerID = g.player_id
       WHERE p.PlayerID IN (${placeholders})`,
      [serverId, ...playerIds]
    );

    const result = new Map<number, PlayerInfractions>();

    for (const row of rows) {
      result.set(row.player_id, {
        serverPunishPoints: row.punish_points ?? 0,
        serverForgivePoints: row.forgive_points ?? 0,
        serverTotalPoints: row.total_points ?? 0,
        globalPunishPoints: row.global_punish_points ?? 0,
        globalForgivePoints: row.global_forgive_points ?? 0,
        globalTotalPoints: row.global_total_points ?? 0,
      });
    }

    // Add empty infractions for players not found
    for (const playerId of playerIds) {
      if (!result.has(playerId)) {
        result.set(playerId, {
          serverPunishPoints: 0,
          serverForgivePoints: 0,
          serverTotalPoints: 0,
          globalPunishPoints: 0,
          globalForgivePoints: 0,
          globalTotalPoints: 0,
        });
      }
    }

    return result;
  }
}

/**
 * Create a new infraction repository.
 */
export function createInfractionRepository(db: Database, logger: Logger): InfractionRepository {
  return new InfractionRepository(db, logger);
}
