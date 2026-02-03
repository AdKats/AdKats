import type { Logger } from '../core/logger.js';
import type { PlayerRepository } from '../database/repositories/player.repository.js';
import type { APlayer, PlayerInfractions } from '../models/player.js';
import { createPlayer, updatePlayerFromRcon } from '../models/player.js';

/**
 * Player service - manages player state and database operations.
 */
export class PlayerService {
  private logger: Logger;
  private playerRepo: PlayerRepository;
  private gameId: number;
  private serverId: number;

  // In-memory cache of online players
  private onlinePlayers: Map<string, APlayer> = new Map();

  // Cache of player IDs to names for quick lookups
  private playerIdToName: Map<number, string> = new Map();

  constructor(
    playerRepo: PlayerRepository,
    logger: Logger,
    gameId: number,
    serverId: number
  ) {
    this.playerRepo = playerRepo;
    this.logger = logger;
    this.gameId = gameId;
    this.serverId = serverId;
  }

  /**
   * Get or create a player by name and GUID.
   * This is called when a player joins the server.
   */
  async getOrCreatePlayer(name: string, guid: string): Promise<APlayer> {
    // Check cache first
    let player = this.onlinePlayers.get(name);
    if (player && player.guid === guid) {
      return player;
    }

    // Find or create in database
    player = await this.playerRepo.findOrCreate(name, guid, this.gameId);

    // Load infractions
    player.infractions = await this.playerRepo.getInfractions(player.playerId, this.serverId);

    // Load reputation
    player.reputation = await this.playerRepo.getReputation(player.playerId);

    // Add to cache
    this.onlinePlayers.set(name, player);
    this.playerIdToName.set(player.playerId, name);

    this.logger.debug({ playerId: player.playerId, name }, 'Player loaded/created');
    return player;
  }

  /**
   * Get an online player by name.
   */
  getOnlinePlayer(name: string): APlayer | undefined {
    return this.onlinePlayers.get(name);
  }

  /**
   * Get an online player by ID.
   */
  getOnlinePlayerById(playerId: number): APlayer | undefined {
    const name = this.playerIdToName.get(playerId);
    if (!name) {
      return undefined;
    }
    return this.onlinePlayers.get(name);
  }

  /**
   * Check if a player is online.
   */
  isPlayerOnline(name: string): boolean {
    return this.onlinePlayers.has(name);
  }

  /**
   * Remove an online player from the cache.
   */
  removeOnlinePlayer(name: string): boolean {
    const player = this.onlinePlayers.get(name);
    if (player) {
      this.playerIdToName.delete(player.playerId);
    }
    return this.onlinePlayers.delete(name);
  }

  /**
   * Update player data from RCON listPlayers response.
   */
  updatePlayerFromListPlayers(raw: {
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
  }): APlayer | null {
    let player = this.onlinePlayers.get(raw.name);

    if (!player) {
      // Player not in cache - shouldn't happen normally, but handle it
      this.logger.warn({ name: raw.name }, 'Player in listPlayers but not in cache');
      player = createPlayer(raw.name, raw.guid, this.gameId);
      this.onlinePlayers.set(raw.name, player);
    }

    updatePlayerFromRcon(player, raw);
    player.isOnline = true;

    return player;
  }

  /**
   * Synchronize online players with the server's player list.
   * Removes players that are no longer on the server.
   */
  syncOnlinePlayers(serverPlayerNames: Set<string>): void {
    for (const [name, player] of this.onlinePlayers) {
      if (!serverPlayerNames.has(name)) {
        player.isOnline = false;
        this.removeOnlinePlayer(name);
        this.logger.debug({ name }, 'Player removed from cache (not in server list)');
      }
    }
  }

  /**
   * Get all online players.
   */
  getAllOnlinePlayers(): APlayer[] {
    return Array.from(this.onlinePlayers.values());
  }

  /**
   * Get online player count.
   */
  getOnlinePlayerCount(): number {
    return this.onlinePlayers.size;
  }

  /**
   * Clear all online players (e.g., on round end or server restart).
   */
  clearOnlinePlayers(): void {
    this.onlinePlayers.clear();
    this.playerIdToName.clear();
    this.logger.info('Cleared online player cache');
  }

  /**
   * Find a player by partial name (fuzzy matching).
   */
  findOnlinePlayerByPartialName(partialName: string): APlayer | APlayer[] | null {
    const lowerPartial = partialName.toLowerCase();
    const matches: APlayer[] = [];

    for (const player of this.onlinePlayers.values()) {
      if (player.soldierName.toLowerCase().includes(lowerPartial)) {
        matches.push(player);
      }
    }

    if (matches.length === 0) {
      return null;
    }

    if (matches.length === 1) {
      return matches[0]!;
    }

    // Check for exact match
    const exactMatch = matches.find(
      (p) => p.soldierName.toLowerCase() === lowerPartial
    );
    if (exactMatch) {
      return exactMatch;
    }

    // Multiple matches
    return matches;
  }

  /**
   * Find a player using Levenshtein distance for fuzzy matching.
   */
  findOnlinePlayerFuzzy(name: string, maxDistance: number = 3): APlayer | null {
    const lowerName = name.toLowerCase();
    let bestMatch: APlayer | null = null;
    let bestDistance = maxDistance + 1;

    for (const player of this.onlinePlayers.values()) {
      const distance = levenshteinDistance(lowerName, player.soldierName.toLowerCase());
      if (distance < bestDistance) {
        bestDistance = distance;
        bestMatch = player;
      }
    }

    return bestMatch;
  }

  /**
   * Update a player's IP address.
   */
  async updatePlayerIp(player: APlayer, ipAddress: string): Promise<void> {
    if (player.ipAddress !== ipAddress) {
      player.ipAddress = ipAddress;
      await this.playerRepo.updateIp(player.playerId, ipAddress);
      this.logger.debug({ playerId: player.playerId, ipAddress }, 'Updated player IP');
    }
  }

  /**
   * Update a player's clan tag.
   */
  async updatePlayerClanTag(player: APlayer, clanTag: string | null): Promise<void> {
    if (player.clanTag !== clanTag) {
      player.clanTag = clanTag;
      await this.playerRepo.updateClanTag(player.playerId, clanTag);
      this.logger.debug({ playerId: player.playerId, clanTag }, 'Updated player clan tag');
    }
  }

  /**
   * Reload player infractions from database.
   */
  async reloadInfractions(player: APlayer): Promise<PlayerInfractions | null> {
    player.infractions = await this.playerRepo.getInfractions(player.playerId, this.serverId);
    return player.infractions;
  }

  /**
   * Reload player reputation from database.
   */
  async reloadReputation(player: APlayer): Promise<number | null> {
    player.reputation = await this.playerRepo.getReputation(player.playerId);
    return player.reputation;
  }

  /**
   * Search for players by name in the database.
   */
  async searchPlayers(searchTerm: string, limit: number = 10): Promise<APlayer[]> {
    return this.playerRepo.searchByName(searchTerm, this.gameId, limit);
  }

  /**
   * Get a player from database by ID.
   */
  async getPlayerById(playerId: number): Promise<APlayer | null> {
    return this.playerRepo.findByIdWithDetails(playerId, this.serverId);
  }

  /**
   * Get a player from database by GUID.
   */
  async getPlayerByGuid(guid: string): Promise<APlayer | null> {
    return this.playerRepo.findByGuid(guid, this.gameId);
  }
}

/**
 * Calculate Levenshtein distance between two strings.
 */
function levenshteinDistance(a: string, b: string): number {
  if (a.length === 0) return b.length;
  if (b.length === 0) return a.length;

  const matrix: number[][] = [];

  // Initialize first column
  for (let i = 0; i <= b.length; i++) {
    matrix[i] = [i];
  }

  // Initialize first row
  for (let j = 0; j <= a.length; j++) {
    matrix[0]![j] = j;
  }

  // Fill in the rest of the matrix
  for (let i = 1; i <= b.length; i++) {
    for (let j = 1; j <= a.length; j++) {
      if (b.charAt(i - 1) === a.charAt(j - 1)) {
        matrix[i]![j] = matrix[i - 1]![j - 1]!;
      } else {
        matrix[i]![j] = Math.min(
          matrix[i - 1]![j - 1]! + 1, // substitution
          matrix[i]![j - 1]! + 1,     // insertion
          matrix[i - 1]![j]! + 1      // deletion
        );
      }
    }
  }

  return matrix[b.length]![a.length]!;
}

/**
 * Create a new player service.
 */
export function createPlayerService(
  playerRepo: PlayerRepository,
  logger: Logger,
  gameId: number,
  serverId: number
): PlayerService {
  return new PlayerService(playerRepo, logger, gameId, serverId);
}
