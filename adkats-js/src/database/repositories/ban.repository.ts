import type { RowDataPacket } from 'mysql2/promise';
import type { Database } from '../connection.js';
import type { Logger } from '../../core/logger.js';
import type { ABan, BanDbRow, BanStatus } from '../../models/ban.js';
import { banFromDbRow, banToDbValues } from '../../models/ban.js';

/**
 * Repository for ban data operations.
 */
export class BanRepository {
  private db: Database;
  private logger: Logger;

  constructor(db: Database, logger: Logger) {
    this.db = db;
    this.logger = logger;
  }

  /**
   * Find a ban by its ID.
   */
  async findById(banId: number): Promise<ABan | null> {
    const row = await this.db.queryOne<BanDbRow & RowDataPacket>(
      `SELECT * FROM adkats_bans WHERE ban_id = ?`,
      [banId]
    );

    if (!row) {
      return null;
    }

    return banFromDbRow(row);
  }

  /**
   * Find a ban by player ID.
   */
  async findByPlayerId(playerId: number): Promise<ABan | null> {
    const row = await this.db.queryOne<BanDbRow & RowDataPacket>(
      `SELECT * FROM adkats_bans WHERE player_id = ?`,
      [playerId]
    );

    if (!row) {
      return null;
    }

    return banFromDbRow(row);
  }

  /**
   * Find an active ban by player ID.
   */
  async findActiveByPlayerId(playerId: number): Promise<ABan | null> {
    const row = await this.db.queryOne<BanDbRow & RowDataPacket>(
      `SELECT * FROM adkats_bans
       WHERE player_id = ?
       AND ban_status = 'Active'
       AND ban_endTime > NOW()`,
      [playerId]
    );

    if (!row) {
      return null;
    }

    return banFromDbRow(row);
  }

  /**
   * Check if a player is banned by GUID.
   */
  async findActiveByGuid(guid: string): Promise<ABan | null> {
    const row = await this.db.queryOne<BanDbRow & RowDataPacket>(
      `SELECT b.* FROM adkats_bans b
       INNER JOIN tbl_playerdata p ON b.player_id = p.PlayerID
       WHERE p.EAGUID = ?
       AND b.ban_status = 'Active'
       AND b.ban_endTime > NOW()
       AND b.ban_enforceGUID = 'Y'`,
      [guid]
    );

    if (!row) {
      return null;
    }

    return banFromDbRow(row);
  }

  /**
   * Check if a player is banned by IP.
   */
  async findActiveByIp(ipAddress: string): Promise<ABan | null> {
    const row = await this.db.queryOne<BanDbRow & RowDataPacket>(
      `SELECT b.* FROM adkats_bans b
       INNER JOIN tbl_playerdata p ON b.player_id = p.PlayerID
       WHERE p.IP_Address = ?
       AND b.ban_status = 'Active'
       AND b.ban_endTime > NOW()
       AND b.ban_enforceIP = 'Y'`,
      [ipAddress]
    );

    if (!row) {
      return null;
    }

    return banFromDbRow(row);
  }

  /**
   * Check if a player is banned by name.
   */
  async findActiveByName(soldierName: string): Promise<ABan | null> {
    const row = await this.db.queryOne<BanDbRow & RowDataPacket>(
      `SELECT b.* FROM adkats_bans b
       INNER JOIN tbl_playerdata p ON b.player_id = p.PlayerID
       WHERE p.SoldierName = ?
       AND b.ban_status = 'Active'
       AND b.ban_endTime > NOW()
       AND b.ban_enforceName = 'Y'`,
      [soldierName]
    );

    if (!row) {
      return null;
    }

    return banFromDbRow(row);
  }

  /**
   * Create or update a ban.
   */
  async upsert(ban: ABan): Promise<ABan> {
    const values = banToDbValues(ban);

    if (ban.banId > 0) {
      // Update existing ban
      await this.db.execute(
        `UPDATE adkats_bans SET
          latest_record_id = ?,
          ban_notes = ?,
          ban_status = ?,
          ban_startTime = ?,
          ban_endTime = ?,
          ban_enforceName = ?,
          ban_enforceGUID = ?,
          ban_enforceIP = ?,
          ban_sync = ?
        WHERE ban_id = ?`,
        [
          values['latest_record_id'],
          values['ban_notes'],
          values['ban_status'],
          values['ban_startTime'],
          values['ban_endTime'],
          values['ban_enforceName'],
          values['ban_enforceGUID'],
          values['ban_enforceIP'],
          values['ban_sync'],
          ban.banId,
        ]
      );

      this.logger.info({ banId: ban.banId }, 'Updated ban');
      return ban;
    } else {
      // Insert or update by player_id (UPSERT)
      const result = await this.db.execute(
        `INSERT INTO adkats_bans
          (player_id, latest_record_id, ban_notes, ban_status, ban_startTime, ban_endTime, ban_enforceName, ban_enforceGUID, ban_enforceIP, ban_sync)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE
          latest_record_id = VALUES(latest_record_id),
          ban_notes = VALUES(ban_notes),
          ban_status = VALUES(ban_status),
          ban_startTime = VALUES(ban_startTime),
          ban_endTime = VALUES(ban_endTime),
          ban_enforceName = VALUES(ban_enforceName),
          ban_enforceGUID = VALUES(ban_enforceGUID),
          ban_enforceIP = VALUES(ban_enforceIP),
          ban_sync = VALUES(ban_sync)`,
        [
          values['player_id'],
          values['latest_record_id'],
          values['ban_notes'],
          values['ban_status'],
          values['ban_startTime'],
          values['ban_endTime'],
          values['ban_enforceName'],
          values['ban_enforceGUID'],
          values['ban_enforceIP'],
          values['ban_sync'],
        ]
      );

      ban.banId = result.insertId || ban.banId;
      this.logger.info({ banId: ban.banId, playerId: ban.playerId }, 'Created/updated ban');
      return ban;
    }
  }

  /**
   * Update ban status.
   */
  async updateStatus(banId: number, status: BanStatus): Promise<void> {
    await this.db.execute(
      `UPDATE adkats_bans SET ban_status = ? WHERE ban_id = ?`,
      [status, banId]
    );

    this.logger.info({ banId, status }, 'Updated ban status');
  }

  /**
   * Disable a ban (soft delete).
   */
  async disable(banId: number): Promise<void> {
    await this.updateStatus(banId, 'Disabled');
  }

  /**
   * Get all active bans.
   */
  async findAllActive(): Promise<ABan[]> {
    const rows = await this.db.query<(BanDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_bans
       WHERE ban_status = 'Active'
       AND ban_endTime > NOW()`
    );

    return rows.map(banFromDbRow);
  }

  /**
   * Get expired bans that need status update.
   */
  async findExpired(): Promise<ABan[]> {
    const rows = await this.db.query<(BanDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_bans
       WHERE ban_status = 'Active'
       AND ban_endTime <= NOW()`
    );

    return rows.map(banFromDbRow);
  }

  /**
   * Mark expired bans as expired.
   */
  async expireOldBans(): Promise<number> {
    const result = await this.db.execute(
      `UPDATE adkats_bans
       SET ban_status = 'Expired'
       WHERE ban_status = 'Active'
       AND ban_endTime <= NOW()`
    );

    if (result.affectedRows > 0) {
      this.logger.info({ count: result.affectedRows }, 'Expired old bans');
    }

    return result.affectedRows;
  }

  /**
   * Search bans by player name or notes.
   */
  async search(searchTerm: string, limit: number = 20): Promise<ABan[]> {
    const rows = await this.db.query<(BanDbRow & RowDataPacket)[]>(
      `SELECT b.* FROM adkats_bans b
       INNER JOIN tbl_playerdata p ON b.player_id = p.PlayerID
       WHERE p.SoldierName LIKE ? OR b.ban_notes LIKE ?
       ORDER BY b.ban_startTime DESC
       LIMIT ?`,
      [`%${searchTerm}%`, `%${searchTerm}%`, limit]
    );

    return rows.map(banFromDbRow);
  }
}

/**
 * Create a new ban repository.
 */
export function createBanRepository(db: Database, logger: Logger): BanRepository {
  return new BanRepository(db, logger);
}
