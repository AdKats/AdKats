import type { APlayer } from './player.js';

/**
 * Team model - represents a team in the game.
 */
export interface ATeam {
  teamId: number;
  teamName: string;
  teamKey: string;
  playerCount: number;
  score: number;
  tickets: number;

  // Runtime state
  players: Map<string, APlayer>;
}

/**
 * Standard team IDs in Battlefield games.
 */
export const TeamIds = {
  NEUTRAL: 0,
  TEAM1: 1,
  TEAM2: 2,
  TEAM3: 3,
  TEAM4: 4,
} as const;

/**
 * Create a new team instance.
 */
export function createTeam(teamId: number, teamName?: string): ATeam {
  return {
    teamId,
    teamName: teamName ?? `Team ${teamId}`,
    teamKey: `team${teamId}`,
    playerCount: 0,
    score: 0,
    tickets: 0,
    players: new Map(),
  };
}

/**
 * Add a player to a team.
 */
export function addPlayerToTeam(team: ATeam, player: APlayer): void {
  team.players.set(player.soldierName, player);
  team.playerCount = team.players.size;
}

/**
 * Remove a player from a team.
 */
export function removePlayerFromTeam(team: ATeam, playerName: string): boolean {
  const result = team.players.delete(playerName);
  team.playerCount = team.players.size;
  return result;
}

/**
 * Get a player from a team by name.
 */
export function getPlayerFromTeam(team: ATeam, playerName: string): APlayer | undefined {
  return team.players.get(playerName);
}

/**
 * Get team statistics.
 */
export interface TeamStats {
  totalKills: number;
  totalDeaths: number;
  totalScore: number;
  averagePing: number;
  averageKdr: number;
}

export function getTeamStats(team: ATeam): TeamStats {
  let totalKills = 0;
  let totalDeaths = 0;
  let totalScore = 0;
  let totalPing = 0;
  let playerCount = 0;

  for (const player of team.players.values()) {
    totalKills += player.kills;
    totalDeaths += player.deaths;
    totalScore += player.score;
    totalPing += player.ping;
    playerCount++;
  }

  const averagePing = playerCount > 0 ? totalPing / playerCount : 0;
  const averageKdr = totalDeaths > 0 ? totalKills / totalDeaths : totalKills;

  return {
    totalKills,
    totalDeaths,
    totalScore,
    averagePing,
    averageKdr,
  };
}

/**
 * Team manager - manages all teams in a round.
 */
export interface TeamManager {
  teams: Map<number, ATeam>;
  roundNumber: number;
  mapName: string;
  modeName: string;
}

/**
 * Create a new team manager.
 */
export function createTeamManager(): TeamManager {
  return {
    teams: new Map([
      [TeamIds.TEAM1, createTeam(TeamIds.TEAM1)],
      [TeamIds.TEAM2, createTeam(TeamIds.TEAM2)],
    ]),
    roundNumber: 0,
    mapName: '',
    modeName: '',
  };
}

/**
 * Get a team by ID.
 */
export function getTeam(manager: TeamManager, teamId: number): ATeam | undefined {
  return manager.teams.get(teamId);
}

/**
 * Get the opposing team ID.
 */
export function getOpposingTeamId(teamId: number): number {
  // Simple 2-team logic; extend for 4-team modes
  return teamId === TeamIds.TEAM1 ? TeamIds.TEAM2 : TeamIds.TEAM1;
}

/**
 * Check if teams are balanced (within threshold).
 */
export function areTeamsBalanced(manager: TeamManager, threshold: number = 2): boolean {
  const team1 = manager.teams.get(TeamIds.TEAM1);
  const team2 = manager.teams.get(TeamIds.TEAM2);

  if (!team1 || !team2) {
    return true;
  }

  return Math.abs(team1.playerCount - team2.playerCount) <= threshold;
}

/**
 * Get the team with fewer players.
 */
export function getWeakerTeam(manager: TeamManager): ATeam | null {
  const team1 = manager.teams.get(TeamIds.TEAM1);
  const team2 = manager.teams.get(TeamIds.TEAM2);

  if (!team1 || !team2) {
    return null;
  }

  if (team1.playerCount < team2.playerCount) {
    return team1;
  } else if (team2.playerCount < team1.playerCount) {
    return team2;
  }

  // Same player count, check scores/tickets
  if (team1.tickets < team2.tickets) {
    return team1;
  } else if (team2.tickets < team1.tickets) {
    return team2;
  }

  return null; // Truly equal
}
