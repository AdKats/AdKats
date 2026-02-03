import type { Logger } from '../core/logger.js';

/**
 * Game versions supported by Battlelog.
 */
export type BattlelogGameVersion = 'BF3' | 'BF4' | 'BFHL';

/**
 * Battlelog player overview stats.
 */
export interface BattlelogPlayerStats {
  personaId: string;
  personaName: string;
  rank: number;
  skill: number;
  kills: number;
  deaths: number;
  killStreak: number;
  headshots: number;
  timePlayed: number;
  accuracy: number;
  kdRatio: number;
  weaponStats: BattlelogWeaponStats[];
  vehicleStats: BattlelogVehicleStats[];
  fetchTime: Date;
}

/**
 * Battlelog weapon statistics.
 */
export interface BattlelogWeaponStats {
  weaponId: string;
  weaponName: string;
  category: string;
  categorySid: string;
  kills: number;
  headshots: number;
  hits: number;
  shots: number;
  timeEquipped: number;
  // Computed stats
  accuracy: number;
  dps: number;
  hskr: number;
  kpm: number;
}

/**
 * Battlelog vehicle statistics.
 */
export interface BattlelogVehicleStats {
  vehicleId: string;
  vehicleName: string;
  category: string;
  kills: number;
  timeIn: number;
  // Computed
  kpm: number;
}

/**
 * Battlelog error types.
 */
export type BattlelogErrorType =
  | 'PLAYER_NOT_FOUND'
  | 'PERSONA_NOT_FOUND'
  | 'STATS_NOT_AVAILABLE'
  | 'RATE_LIMITED'
  | 'NETWORK_ERROR'
  | 'PARSE_ERROR';

/**
 * Battlelog error class.
 */
export class BattlelogError extends Error {
  type: BattlelogErrorType;

  constructor(message: string, type: BattlelogErrorType) {
    super(message);
    this.name = 'BattlelogError';
    this.type = type;
  }
}

/**
 * Cache entry for player stats.
 */
interface CacheEntry<T> {
  data: T;
  expiresAt: number;
}

/**
 * Rate limiter state.
 */
interface RateLimiterState {
  lastRequestTime: number;
  queue: Array<{
    resolve: () => void;
    reject: (error: Error) => void;
  }>;
  processing: boolean;
}

/**
 * Battlelog API endpoints.
 */
const BATTLELOG_ENDPOINTS = {
  BF3: {
    overview: 'http://battlelog.battlefield.com/bf3/overviewPopulateStats',
    weapons: 'http://battlelog.battlefield.com/bf3/weaponsPopulateStats',
    search: 'http://battlelog.battlefield.com/bf3/search/query',
    persona: 'http://battlelog.battlefield.com/bf3/user',
  },
  BF4: {
    overview: 'http://battlelog.battlefield.com/bf4/warsawoverviewpopulate',
    weapons: 'http://battlelog.battlefield.com/bf4/warsawWeaponsPopulate',
    search: 'http://battlelog.battlefield.com/bf4/search/query',
    persona: 'http://battlelog.battlefield.com/bf4/user',
  },
  BFHL: {
    overview: 'http://battlelog.battlefield.com/bfh/warsawoverviewpopulate',
    weapons: 'http://battlelog.battlefield.com/bfh/warsawWeaponsPopulate',
    search: 'http://battlelog.battlefield.com/bfh/search/query',
    persona: 'http://battlelog.battlefield.com/bfh/user',
  },
} as const;

/**
 * Battlelog API client.
 * Handles rate limiting, caching, and fetching player stats from Battlelog.
 */
export class BattlelogClient {
  private logger: Logger;
  private gameVersion: BattlelogGameVersion;
  private rateLimitMs: number;
  private cacheTtlMs: number;

  // In-memory caches
  private personaIdCache: Map<string, CacheEntry<string>> = new Map();
  private statsCache: Map<string, CacheEntry<BattlelogPlayerStats>> = new Map();

  // Rate limiter
  private rateLimiter: RateLimiterState = {
    lastRequestTime: 0,
    queue: [],
    processing: false,
  };

  // User-Agent for requests
  private readonly USER_AGENT = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36';

  constructor(
    logger: Logger,
    gameVersion: BattlelogGameVersion,
    rateLimitMs: number = 1000,
    cacheTtlMs: number = 300000
  ) {
    this.logger = logger;
    this.gameVersion = gameVersion;
    this.rateLimitMs = rateLimitMs;
    this.cacheTtlMs = cacheTtlMs;
  }

  /**
   * Get player stats by soldier name.
   * Will search for the player if persona ID is not cached.
   */
  async getPlayerStats(soldierName: string): Promise<BattlelogPlayerStats | null> {
    // Check cache first
    const cached = this.statsCache.get(soldierName);
    if (cached && cached.expiresAt > Date.now()) {
      this.logger.debug({ soldierName }, 'Battlelog stats cache hit');
      return cached.data;
    }

    try {
      // Get persona ID
      const personaId = await this.getPersonaId(soldierName);
      if (!personaId) {
        return null;
      }

      // Fetch overview and weapon stats
      const [overview, weapons] = await Promise.all([
        this.fetchOverviewStats(personaId),
        this.fetchWeaponStats(personaId),
      ]);

      if (!overview) {
        return null;
      }

      const stats: BattlelogPlayerStats = {
        ...overview,
        weaponStats: weapons ?? [],
        vehicleStats: [],
        fetchTime: new Date(),
      };

      // Cache the result
      this.statsCache.set(soldierName, {
        data: stats,
        expiresAt: Date.now() + this.cacheTtlMs,
      });

      this.logger.debug({ soldierName, personaId }, 'Battlelog stats fetched');
      return stats;

    } catch (error) {
      if (error instanceof BattlelogError) {
        this.logger.warn({ soldierName, errorType: error.type }, `Battlelog error: ${error.message}`);
      } else {
        const msg = error instanceof Error ? error.message : String(error);
        this.logger.error({ soldierName, error: msg }, 'Failed to fetch Battlelog stats');
      }
      return null;
    }
  }

  /**
   * Get persona ID for a soldier name.
   */
  async getPersonaId(soldierName: string): Promise<string | null> {
    // Check cache
    const cached = this.personaIdCache.get(soldierName);
    if (cached && cached.expiresAt > Date.now()) {
      return cached.data;
    }

    try {
      await this.acquireRateLimit();

      const searchUrl = `${BATTLELOG_ENDPOINTS[this.gameVersion].search}/?query=${encodeURIComponent(soldierName)}&post-check-sum=`;

      const response = await fetch(searchUrl, {
        headers: {
          'User-Agent': this.USER_AGENT,
          'Accept': 'application/json',
          'X-AjaxNavigation': '1',
        },
      });

      if (!response.ok) {
        if (response.status === 429) {
          throw new BattlelogError('Rate limited by Battlelog', 'RATE_LIMITED');
        }
        throw new BattlelogError(`HTTP ${response.status}`, 'NETWORK_ERROR');
      }

      const data = await response.json() as BattlelogSearchResponse;

      if (!data.data || !data.data.soldiers || data.data.soldiers.length === 0) {
        this.logger.debug({ soldierName }, 'Player not found on Battlelog');
        return null;
      }

      // Find exact match (case-insensitive)
      const soldier = data.data.soldiers.find(
        (s: BattlelogSoldierResult) => s.persona.personaName.toLowerCase() === soldierName.toLowerCase()
      );

      if (!soldier) {
        this.logger.debug({ soldierName }, 'No exact match found on Battlelog');
        return null;
      }

      const personaId = soldier.persona.personaId.toString();

      // Cache the result
      this.personaIdCache.set(soldierName, {
        data: personaId,
        expiresAt: Date.now() + this.cacheTtlMs * 2, // Cache persona IDs longer
      });

      return personaId;

    } catch (error) {
      if (error instanceof BattlelogError) {
        throw error;
      }
      const msg = error instanceof Error ? error.message : String(error);
      throw new BattlelogError(msg, 'NETWORK_ERROR');
    }
  }

  /**
   * Fetch overview stats from Battlelog.
   */
  private async fetchOverviewStats(personaId: string): Promise<Omit<BattlelogPlayerStats, 'weaponStats' | 'vehicleStats' | 'fetchTime'> | null> {
    await this.acquireRateLimit();

    const url = `${BATTLELOG_ENDPOINTS[this.gameVersion].overview}/${personaId}/1/`;

    try {
      const response = await fetch(url, {
        headers: {
          'User-Agent': this.USER_AGENT,
          'Accept': 'application/json',
          'X-AjaxNavigation': '1',
        },
      });

      if (!response.ok) {
        if (response.status === 429) {
          throw new BattlelogError('Rate limited by Battlelog', 'RATE_LIMITED');
        }
        throw new BattlelogError(`HTTP ${response.status}`, 'NETWORK_ERROR');
      }

      const data = await response.json() as BattlelogOverviewResponse;

      if (!data.data || !data.data.overviewStats) {
        throw new BattlelogError('Stats not available', 'STATS_NOT_AVAILABLE');
      }

      const stats = data.data.overviewStats;

      const kills = stats.kills ?? 0;
      const deaths = stats.deaths ?? 0;

      return {
        personaId,
        personaName: data.data.soldiername ?? '',
        rank: stats.rank ?? 0,
        skill: stats.skill ?? 0,
        kills,
        deaths,
        killStreak: stats.killStreakBonus ?? 0,
        headshots: stats.headshots ?? 0,
        timePlayed: stats.timePlayed ?? 0,
        accuracy: stats.accuracy ?? 0,
        kdRatio: deaths > 0 ? kills / deaths : kills,
      };

    } catch (error) {
      if (error instanceof BattlelogError) {
        throw error;
      }
      const msg = error instanceof Error ? error.message : String(error);
      throw new BattlelogError(msg, 'PARSE_ERROR');
    }
  }

  /**
   * Fetch weapon stats from Battlelog.
   */
  private async fetchWeaponStats(personaId: string): Promise<BattlelogWeaponStats[]> {
    await this.acquireRateLimit();

    const url = `${BATTLELOG_ENDPOINTS[this.gameVersion].weapons}/${personaId}/1/`;

    try {
      const response = await fetch(url, {
        headers: {
          'User-Agent': this.USER_AGENT,
          'Accept': 'application/json',
          'X-AjaxNavigation': '1',
        },
      });

      if (!response.ok) {
        if (response.status === 429) {
          throw new BattlelogError('Rate limited by Battlelog', 'RATE_LIMITED');
        }
        throw new BattlelogError(`HTTP ${response.status}`, 'NETWORK_ERROR');
      }

      const data = await response.json() as BattlelogWeaponsResponse;

      if (!data.data || !data.data.mainWeaponStats) {
        this.logger.debug({ personaId }, 'No weapon stats available');
        return [];
      }

      const weapons: BattlelogWeaponStats[] = [];

      for (const weapon of data.data.mainWeaponStats) {
        const kills = weapon.stat?.kills ?? weapon.kills ?? 0;
        const headshots = weapon.stat?.headshots ?? weapon.headshots ?? 0;
        const hits = weapon.stat?.hits ?? weapon.shots?.hit ?? 0;
        const shots = weapon.stat?.shots ?? weapon.shots?.fired ?? 0;
        const timeEquipped = weapon.stat?.timeEquipped ?? weapon.timeEquipped ?? 0;

        // Calculate computed stats
        const accuracy = shots > 0 ? (hits / shots) * 100 : 0;
        const hskr = kills > 0 ? headshots / kills : 0;
        const kpm = timeEquipped > 0 ? (kills / (timeEquipped / 60)) : 0;

        // Calculate DPS (damage per shot based on kills/hits ratio)
        // Using 100 as standard soldier health
        const dps = hits > 0 ? (kills / hits) * 100 : 0;

        weapons.push({
          weaponId: weapon.slug ?? weapon.guid ?? '',
          weaponName: weapon.name ?? weapon.slug ?? '',
          category: weapon.category ?? '',
          categorySid: weapon.categorySID ?? '',
          kills,
          headshots,
          hits,
          shots,
          timeEquipped,
          accuracy,
          dps,
          hskr,
          kpm,
        });
      }

      return weapons;

    } catch (error) {
      if (error instanceof BattlelogError) {
        throw error;
      }
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.warn({ personaId, error: msg }, 'Failed to fetch weapon stats');
      return [];
    }
  }

  /**
   * Acquire rate limit token.
   * Waits until the rate limit allows another request.
   */
  private async acquireRateLimit(): Promise<void> {
    const now = Date.now();
    const timeSinceLastRequest = now - this.rateLimiter.lastRequestTime;

    if (timeSinceLastRequest >= this.rateLimitMs) {
      this.rateLimiter.lastRequestTime = now;
      return;
    }

    // Need to wait
    const waitTime = this.rateLimitMs - timeSinceLastRequest;
    await this.sleep(waitTime);
    this.rateLimiter.lastRequestTime = Date.now();
  }

  /**
   * Sleep for the specified duration.
   */
  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  /**
   * Clear all caches.
   */
  clearCache(): void {
    this.personaIdCache.clear();
    this.statsCache.clear();
    this.logger.info('Battlelog cache cleared');
  }

  /**
   * Clear expired cache entries.
   */
  cleanupCache(): void {
    const now = Date.now();
    let cleaned = 0;

    for (const [key, entry] of this.personaIdCache) {
      if (entry.expiresAt <= now) {
        this.personaIdCache.delete(key);
        cleaned++;
      }
    }

    for (const [key, entry] of this.statsCache) {
      if (entry.expiresAt <= now) {
        this.statsCache.delete(key);
        cleaned++;
      }
    }

    if (cleaned > 0) {
      this.logger.debug({ cleaned }, 'Cleaned expired Battlelog cache entries');
    }
  }

  /**
   * Get cache statistics.
   */
  getCacheStats(): { personaIds: number; stats: number } {
    return {
      personaIds: this.personaIdCache.size,
      stats: this.statsCache.size,
    };
  }

  /**
   * Check if a persona ID is cached for a player.
   */
  hasPersonaId(soldierName: string): boolean {
    const cached = this.personaIdCache.get(soldierName);
    return cached !== undefined && cached.expiresAt > Date.now();
  }

  /**
   * Check if stats are cached for a player.
   */
  hasStats(soldierName: string): boolean {
    const cached = this.statsCache.get(soldierName);
    return cached !== undefined && cached.expiresAt > Date.now();
  }

  /**
   * Set the game version.
   */
  setGameVersion(gameVersion: BattlelogGameVersion): void {
    this.gameVersion = gameVersion;
    this.logger.info({ gameVersion }, 'Battlelog game version updated');
  }

  /**
   * Set rate limit interval.
   */
  setRateLimit(rateLimitMs: number): void {
    this.rateLimitMs = rateLimitMs;
    this.logger.info({ rateLimitMs }, 'Battlelog rate limit updated');
  }

  /**
   * Set cache TTL.
   */
  setCacheTtl(cacheTtlMs: number): void {
    this.cacheTtlMs = cacheTtlMs;
    this.logger.info({ cacheTtlMs }, 'Battlelog cache TTL updated');
  }
}

// =====================================================
// Battlelog API Response Types
// =====================================================

interface BattlelogSearchResponse {
  type: string;
  message: string;
  data?: {
    soldiers?: BattlelogSoldierResult[];
  };
}

interface BattlelogSoldierResult {
  persona: {
    personaId: number;
    personaName: string;
    namespace: string;
    clanTag?: string;
  };
  user?: {
    username: string;
    userId: number;
  };
}

interface BattlelogOverviewResponse {
  type: string;
  message: string;
  data?: {
    soldiername?: string;
    overviewStats?: {
      rank?: number;
      skill?: number;
      kills?: number;
      deaths?: number;
      killStreakBonus?: number;
      headshots?: number;
      timePlayed?: number;
      accuracy?: number;
      wins?: number;
      losses?: number;
    };
  };
}

interface BattlelogWeaponsResponse {
  type: string;
  message: string;
  data?: {
    mainWeaponStats?: BattlelogWeaponData[];
  };
}

interface BattlelogWeaponData {
  slug?: string;
  guid?: string;
  name?: string;
  category?: string;
  categorySID?: string;
  kills?: number;
  headshots?: number;
  timeEquipped?: number;
  stat?: {
    kills?: number;
    headshots?: number;
    hits?: number;
    shots?: number;
    timeEquipped?: number;
  };
  shots?: {
    hit?: number;
    fired?: number;
  };
}

/**
 * Create a new Battlelog client instance.
 */
export function createBattlelogClient(
  logger: Logger,
  gameVersion: BattlelogGameVersion,
  rateLimitMs?: number,
  cacheTtlMs?: number
): BattlelogClient {
  return new BattlelogClient(logger, gameVersion, rateLimitMs, cacheTtlMs);
}
