import type { APlayer } from './player.js';
import type { ACommand } from './command.js';

/**
 * Record model - represents an action taken in the game.
 * Corresponds to adkats_records_main in the database.
 */
export interface ARecord {
  // Database fields
  recordId: number;
  serverId: number;
  commandType: number;
  commandAction: number;
  commandNumeric: number;
  targetName: string;
  targetId: number | null;
  sourceName: string;
  sourceId: number | null;
  recordMessage: string;
  recordTime: Date;
  adkatsRead: boolean;
  adkatsWeb: boolean;

  // Runtime state (not persisted)
  targetPlayer: APlayer | null;
  sourcePlayer: APlayer | null;
  command: ACommand | null;

  // For command parsing
  isConfirmed: boolean;
  isCancelled: boolean;
  externalSource: RecordSource;
}

/**
 * Source of the record/command.
 */
export enum RecordSource {
  InGame = 'InGame',
  External = 'External',
  Database = 'Database',
  Automated = 'Automated',
}

/**
 * Create a new record instance.
 */
export function createRecord(
  serverId: number,
  source: APlayer | null,
  target: APlayer | null,
  command: ACommand | null,
  message: string = ''
): ARecord {
  return {
    recordId: 0,
    serverId,
    commandType: command?.commandId ?? 0,
    commandAction: command?.commandId ?? 0,
    commandNumeric: 0,
    targetName: target?.soldierName ?? 'NoTarget',
    targetId: target?.playerId ?? null,
    sourceName: source?.soldierName ?? 'AdKats',
    sourceId: source?.playerId ?? null,
    recordMessage: message || 'NoMessage',
    recordTime: new Date(),
    adkatsRead: false,
    adkatsWeb: false,
    targetPlayer: target,
    sourcePlayer: source,
    command,
    isConfirmed: false,
    isCancelled: false,
    externalSource: source ? RecordSource.InGame : RecordSource.Automated,
  };
}

/**
 * Create an automated record (no source player).
 */
export function createAutomatedRecord(
  serverId: number,
  target: APlayer | null,
  command: ACommand | null,
  message: string,
  sourceName: string = 'AdKats'
): ARecord {
  const record = createRecord(serverId, null, target, command, message);
  record.sourceName = sourceName;
  record.externalSource = RecordSource.Automated;
  return record;
}

/**
 * Database row representation for record data.
 */
export interface RecordDbRow {
  record_id: number;
  server_id: number;
  command_type: number;
  command_action: number;
  command_numeric: number;
  target_name: string;
  target_id: number | null;
  source_name: string;
  source_id: number | null;
  record_message: string;
  record_time: Date;
  adkats_read: 'Y' | 'N';
  adkats_web: number;
}

/**
 * Convert database row to ARecord.
 */
export function recordFromDbRow(row: RecordDbRow): ARecord {
  return {
    recordId: row.record_id,
    serverId: row.server_id,
    commandType: row.command_type,
    commandAction: row.command_action,
    commandNumeric: row.command_numeric,
    targetName: row.target_name,
    targetId: row.target_id,
    sourceName: row.source_name,
    sourceId: row.source_id,
    recordMessage: row.record_message,
    recordTime: row.record_time,
    adkatsRead: row.adkats_read === 'Y',
    adkatsWeb: row.adkats_web === 1,
    targetPlayer: null,
    sourcePlayer: null,
    command: null,
    isConfirmed: false,
    isCancelled: false,
    externalSource: RecordSource.Database,
  };
}

/**
 * Convert ARecord to database insert values.
 */
export function recordToDbValues(record: ARecord): Record<string, unknown> {
  return {
    server_id: record.serverId,
    command_type: record.commandType,
    command_action: record.commandAction,
    command_numeric: record.commandNumeric,
    target_name: record.targetName,
    target_id: record.targetId,
    source_name: record.sourceName,
    source_id: record.sourceId,
    record_message: record.recordMessage,
    record_time: record.recordTime,
    adkats_read: record.adkatsRead ? 'Y' : 'N',
    adkats_web: record.adkatsWeb ? 1 : 0,
  };
}
