/**
 * Player model - represents a player in the game server.
 * Corresponds to tbl_playerdata in the database.
 */
export interface APlayer {
  // Database fields
  playerId: number;
  gameId: number;
  clanTag: string | null;
  soldierName: string;
  guid: string;        // EA GUID
  pbGuid: string | null;  // PunkBuster GUID
  ipAddress: string | null;

  // Runtime state (not persisted)
  isOnline: boolean;
  isAlive: boolean;
  teamId: number;
  squadId: number;
  kills: number;
  deaths: number;
  score: number;
  ping: number;
  rank: number;
  type: PlayerType;

  // Cached data
  reputation: number | null;
  infractions: PlayerInfractions | null;

  // Timestamps
  firstSeen: Date | null;
  lastSeen: Date | null;
}

/**
 * Player type enumeration.
 */
export enum PlayerType {
  Player = 0,
  Spectator = 1,
  Commander = 2,
}

/**
 * Player infraction points.
 */
export interface PlayerInfractions {
  serverPunishPoints: number;
  serverForgivePoints: number;
  serverTotalPoints: number;
  globalPunishPoints: number;
  globalForgivePoints: number;
  globalTotalPoints: number;
}

/**
 * Player ban status.
 */
export interface PlayerBanStatus {
  isBanned: boolean;
  banId: number | null;
  banStatus: BanStatus | null;
  banReason: string | null;
  banStartTime: Date | null;
  banEndTime: Date | null;
  enforceGuid: boolean;
  enforceIp: boolean;
  enforceName: boolean;
}

export type BanStatus = 'Active' | 'Expired' | 'Disabled';

/**
 * Create a new player instance with default values.
 */
export function createPlayer(
  soldierName: string,
  guid: string,
  gameId: number = 0
): APlayer {
  return {
    playerId: 0,
    gameId,
    clanTag: null,
    soldierName,
    guid,
    pbGuid: null,
    ipAddress: null,
    isOnline: false,
    isAlive: false,
    teamId: 0,
    squadId: 0,
    kills: 0,
    deaths: 0,
    score: 0,
    ping: 0,
    rank: 0,
    type: PlayerType.Player,
    reputation: null,
    infractions: null,
    firstSeen: null,
    lastSeen: null,
  };
}

/**
 * Update player from raw RCON listPlayers data.
 */
export function updatePlayerFromRcon(
  player: APlayer,
  raw: {
    name: string;
    guid: string;
    teamId: string;
    squadId: string;
    kills: string;
    deaths: string;
    score: string;
    rank: string;
    ping: string;
    type: string;
  }
): APlayer {
  player.soldierName = raw.name;
  player.teamId = parseInt(raw.teamId, 10);
  player.squadId = parseInt(raw.squadId, 10);
  player.kills = parseInt(raw.kills, 10);
  player.deaths = parseInt(raw.deaths, 10);
  player.score = parseInt(raw.score, 10);
  player.rank = parseInt(raw.rank, 10);
  player.ping = parseInt(raw.ping, 10);

  // Map type string to enum
  switch (raw.type) {
    case '1':
      player.type = PlayerType.Spectator;
      break;
    case '2':
      player.type = PlayerType.Commander;
      break;
    default:
      player.type = PlayerType.Player;
  }

  return player;
}

/**
 * Get KDR (Kill/Death Ratio) for a player.
 */
export function getPlayerKdr(player: APlayer): number {
  if (player.deaths === 0) {
    return player.kills;
  }
  return player.kills / player.deaths;
}

/**
 * Check if a player is an admin (has a linked user account with permissions).
 */
export function isPlayerAdmin(player: APlayer & { userId?: number }): boolean {
  return player.userId !== undefined && player.userId > 0;
}

/**
 * Database row representation for player data.
 */
export interface PlayerDbRow {
  PlayerID: number;
  GameID: number;
  ClanTag: string | null;
  SoldierName: string;
  EAGUID: string;
  PBGUID: string | null;
  IP_Address: string | null;
}

/**
 * Convert database row to APlayer.
 */
export function playerFromDbRow(row: PlayerDbRow): APlayer {
  return {
    playerId: row.PlayerID,
    gameId: row.GameID,
    clanTag: row.ClanTag,
    soldierName: row.SoldierName,
    guid: row.EAGUID,
    pbGuid: row.PBGUID,
    ipAddress: row.IP_Address,
    isOnline: false,
    isAlive: false,
    teamId: 0,
    squadId: 0,
    kills: 0,
    deaths: 0,
    score: 0,
    ping: 0,
    rank: 0,
    type: PlayerType.Player,
    reputation: null,
    infractions: null,
    firstSeen: null,
    lastSeen: null,
  };
}
