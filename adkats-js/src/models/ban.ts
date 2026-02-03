import type { APlayer } from './player.js';
import type { ARecord } from './record.js';

/**
 * Ban model - represents a player ban.
 * Corresponds to adkats_bans in the database.
 */
export interface ABan {
  banId: number;
  playerId: number;
  latestRecordId: number;
  banNotes: string;
  banStatus: BanStatus;
  banStartTime: Date;
  banEndTime: Date;
  banEnforceName: boolean;
  banEnforceGuid: boolean;
  banEnforceIp: boolean;
  banSync: string;

  // Runtime associations (not persisted)
  player: APlayer | null;
  latestRecord: ARecord | null;
}

/**
 * Ban status enumeration.
 */
export type BanStatus = 'Active' | 'Expired' | 'Disabled';

/**
 * Create a new ban instance.
 */
export function createBan(
  player: APlayer,
  record: ARecord,
  durationMinutes: number | null,
  reason: string,
  options: Partial<{
    enforceName: boolean;
    enforceGuid: boolean;
    enforceIp: boolean;
  }> = {}
): ABan {
  const now = new Date();
  const endTime = durationMinutes
    ? new Date(now.getTime() + durationMinutes * 60000)
    : new Date('9999-12-31T23:59:59Z'); // Permanent ban

  return {
    banId: 0,
    playerId: player.playerId,
    latestRecordId: record.recordId,
    banNotes: reason,
    banStatus: 'Active',
    banStartTime: now,
    banEndTime: endTime,
    banEnforceName: options.enforceName ?? false,
    banEnforceGuid: options.enforceGuid ?? true,
    banEnforceIp: options.enforceIp ?? false,
    banSync: '-sync-',
    player,
    latestRecord: record,
  };
}

/**
 * Check if a ban is currently active.
 */
export function isBanActive(ban: ABan): boolean {
  if (ban.banStatus !== 'Active') {
    return false;
  }
  return new Date() < ban.banEndTime;
}

/**
 * Check if a ban is permanent.
 */
export function isBanPermanent(ban: ABan): boolean {
  // A ban ending after 2099 is considered permanent
  return ban.banEndTime.getFullYear() > 2099;
}

/**
 * Get the remaining duration of a ban in minutes.
 */
export function getBanRemainingMinutes(ban: ABan): number {
  if (!isBanActive(ban)) {
    return 0;
  }
  if (isBanPermanent(ban)) {
    return Infinity;
  }
  const now = new Date();
  return Math.max(0, (ban.banEndTime.getTime() - now.getTime()) / 60000);
}

/**
 * Get a human-readable duration string for the ban.
 */
export function getBanDurationString(ban: ABan): string {
  if (isBanPermanent(ban)) {
    return 'Permanent';
  }

  const minutes = getBanRemainingMinutes(ban);
  if (minutes <= 0) {
    return 'Expired';
  }

  if (minutes < 60) {
    return `${Math.ceil(minutes)} minutes`;
  }

  const hours = minutes / 60;
  if (hours < 24) {
    return `${Math.ceil(hours)} hours`;
  }

  const days = hours / 24;
  if (days < 7) {
    return `${Math.ceil(days)} days`;
  }

  const weeks = days / 7;
  if (weeks < 4) {
    return `${Math.ceil(weeks)} weeks`;
  }

  const months = days / 30;
  return `${Math.ceil(months)} months`;
}

/**
 * Database row representation.
 */
export interface BanDbRow {
  ban_id: number;
  player_id: number;
  latest_record_id: number;
  ban_notes: string;
  ban_status: BanStatus;
  ban_startTime: Date;
  ban_endTime: Date;
  ban_enforceName: 'Y' | 'N';
  ban_enforceGUID: 'Y' | 'N';
  ban_enforceIP: 'Y' | 'N';
  ban_sync: string;
}

/**
 * Convert database row to ABan.
 */
export function banFromDbRow(row: BanDbRow): ABan {
  return {
    banId: row.ban_id,
    playerId: row.player_id,
    latestRecordId: row.latest_record_id,
    banNotes: row.ban_notes,
    banStatus: row.ban_status,
    banStartTime: row.ban_startTime,
    banEndTime: row.ban_endTime,
    banEnforceName: row.ban_enforceName === 'Y',
    banEnforceGuid: row.ban_enforceGUID === 'Y',
    banEnforceIp: row.ban_enforceIP === 'Y',
    banSync: row.ban_sync,
    player: null,
    latestRecord: null,
  };
}

/**
 * Convert ABan to database insert/update values.
 */
export function banToDbValues(ban: ABan): Record<string, unknown> {
  return {
    player_id: ban.playerId,
    latest_record_id: ban.latestRecordId,
    ban_notes: ban.banNotes,
    ban_status: ban.banStatus,
    ban_startTime: ban.banStartTime,
    ban_endTime: ban.banEndTime,
    ban_enforceName: ban.banEnforceName ? 'Y' : 'N',
    ban_enforceGUID: ban.banEnforceGuid ? 'Y' : 'N',
    ban_enforceIP: ban.banEnforceIp ? 'Y' : 'N',
    ban_sync: ban.banSync,
  };
}
