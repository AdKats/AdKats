import type { RowDataPacket } from 'mysql2/promise';
import type { Database } from '../connection.js';
import type { Logger } from '../../core/logger.js';
import type { ARecord, RecordDbRow } from '../../models/record.js';
import { recordFromDbRow, recordToDbValues } from '../../models/record.js';

/**
 * Repository for record data operations.
 */
export class RecordRepository {
  private db: Database;
  private logger: Logger;

  constructor(db: Database, logger: Logger) {
    this.db = db;
    this.logger = logger;
  }

  /**
   * Find a record by its ID.
   */
  async findById(recordId: number): Promise<ARecord | null> {
    const row = await this.db.queryOne<RecordDbRow & RowDataPacket>(
      `SELECT * FROM adkats_records_main WHERE record_id = ?`,
      [recordId]
    );

    if (!row) {
      return null;
    }

    return recordFromDbRow(row);
  }

  /**
   * Create a new record.
   */
  async create(record: ARecord): Promise<ARecord> {
    const values = recordToDbValues(record);

    const result = await this.db.execute(
      `INSERT INTO adkats_records_main
        (server_id, command_type, command_action, command_numeric, target_name, target_id, source_name, source_id, record_message, record_time, adkats_read, adkats_web)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      [
        values['server_id'],
        values['command_type'],
        values['command_action'],
        values['command_numeric'],
        values['target_name'],
        values['target_id'],
        values['source_name'],
        values['source_id'],
        values['record_message'],
        values['record_time'],
        values['adkats_read'],
        values['adkats_web'],
      ]
    );

    record.recordId = result.insertId;
    this.logger.debug({ recordId: record.recordId, commandType: record.commandType }, 'Created record');
    return record;
  }

  /**
   * Update record as read.
   */
  async markAsRead(recordId: number): Promise<void> {
    await this.db.execute(
      `UPDATE adkats_records_main SET adkats_read = 'Y' WHERE record_id = ?`,
      [recordId]
    );
  }

  /**
   * Get unread records for a server.
   */
  async findUnread(serverId: number): Promise<ARecord[]> {
    const rows = await this.db.query<(RecordDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_records_main
       WHERE server_id = ? AND adkats_read = 'N'
       ORDER BY record_time ASC`,
      [serverId]
    );

    return rows.map(recordFromDbRow);
  }

  /**
   * Get records for a target player.
   */
  async findByTargetId(targetId: number, limit: number = 50): Promise<ARecord[]> {
    const rows = await this.db.query<(RecordDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_records_main
       WHERE target_id = ?
       ORDER BY record_time DESC
       LIMIT ?`,
      [targetId, limit]
    );

    return rows.map(recordFromDbRow);
  }

  /**
   * Get records by a source player.
   */
  async findBySourceId(sourceId: number, limit: number = 50): Promise<ARecord[]> {
    const rows = await this.db.query<(RecordDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_records_main
       WHERE source_id = ?
       ORDER BY record_time DESC
       LIMIT ?`,
      [sourceId, limit]
    );

    return rows.map(recordFromDbRow);
  }

  /**
   * Get records by command type.
   */
  async findByCommandType(commandType: number, serverId?: number, limit: number = 50): Promise<ARecord[]> {
    let sql = `SELECT * FROM adkats_records_main WHERE command_type = ?`;
    const params: unknown[] = [commandType];

    if (serverId !== undefined) {
      sql += ` AND server_id = ?`;
      params.push(serverId);
    }

    sql += ` ORDER BY record_time DESC LIMIT ?`;
    params.push(limit);

    const rows = await this.db.query<(RecordDbRow & RowDataPacket)[]>(sql, params);

    return rows.map(recordFromDbRow);
  }

  /**
   * Get recent records for a server.
   */
  async findRecent(serverId: number, limit: number = 100): Promise<ARecord[]> {
    const rows = await this.db.query<(RecordDbRow & RowDataPacket)[]>(
      `SELECT * FROM adkats_records_main
       WHERE server_id = ?
       ORDER BY record_time DESC
       LIMIT ?`,
      [serverId, limit]
    );

    return rows.map(recordFromDbRow);
  }

  /**
   * Get pending reports (report commands that haven't been acted upon).
   */
  async findPendingReports(serverId: number): Promise<ARecord[]> {
    // Command type 18 = player_report
    // Look for reports that don't have a corresponding accept/deny/ignore
    const rows = await this.db.query<(RecordDbRow & RowDataPacket)[]>(
      `SELECT r.* FROM adkats_records_main r
       WHERE r.server_id = ?
       AND r.command_type = 18
       AND r.record_time > DATE_SUB(NOW(), INTERVAL 1 HOUR)
       AND NOT EXISTS (
         SELECT 1 FROM adkats_records_main r2
         WHERE r2.command_type IN (40, 41, 61)
         AND r2.record_message LIKE CONCAT('%', r.record_id, '%')
       )
       ORDER BY r.record_time DESC`,
      [serverId]
    );

    return rows.map(recordFromDbRow);
  }

  /**
   * Count punishments for a player on a server.
   */
  async countPunishments(playerId: number, serverId: number): Promise<number> {
    // Command type 9 = punish
    const row = await this.db.queryOne<{ count: number } & RowDataPacket>(
      `SELECT COUNT(*) as count FROM adkats_records_main
       WHERE target_id = ? AND server_id = ? AND command_type = 9`,
      [playerId, serverId]
    );

    return row?.count ?? 0;
  }

  /**
   * Count forgives for a player on a server.
   */
  async countForgives(playerId: number, serverId: number): Promise<number> {
    // Command type 10 = forgive
    const row = await this.db.queryOne<{ count: number } & RowDataPacket>(
      `SELECT COUNT(*) as count FROM adkats_records_main
       WHERE target_id = ? AND server_id = ? AND command_type = 10`,
      [playerId, serverId]
    );

    return row?.count ?? 0;
  }

  /**
   * Get the most recent punishment for a player.
   */
  async findLastPunishment(playerId: number, serverId?: number): Promise<ARecord | null> {
    let sql = `SELECT * FROM adkats_records_main WHERE target_id = ? AND command_type = 9`;
    const params: unknown[] = [playerId];

    if (serverId !== undefined) {
      sql += ` AND server_id = ?`;
      params.push(serverId);
    }

    sql += ` ORDER BY record_time DESC LIMIT 1`;

    const row = await this.db.queryOne<RecordDbRow & RowDataPacket>(sql, params);

    if (!row) {
      return null;
    }

    return recordFromDbRow(row);
  }

  /**
   * Check if a player was recently punished (for IRO - Immediate Repeat Offense).
   */
  async wasRecentlyPunished(playerId: number, serverId: number, withinMinutes: number): Promise<boolean> {
    const row = await this.db.queryOne<{ count: number } & RowDataPacket>(
      `SELECT COUNT(*) as count FROM adkats_records_main
       WHERE target_id = ?
       AND server_id = ?
       AND command_type = 9
       AND record_time > DATE_SUB(NOW(), INTERVAL ? MINUTE)`,
      [playerId, serverId, withinMinutes]
    );

    return (row?.count ?? 0) > 0;
  }
}

/**
 * Create a new record repository.
 */
export function createRecordRepository(db: Database, logger: Logger): RecordRepository {
  return new RecordRepository(db, logger);
}
