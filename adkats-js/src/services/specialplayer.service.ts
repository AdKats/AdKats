import type { Logger } from '../core/logger.js';
import type { Scheduler } from '../core/scheduler.js';
import type { SpecialPlayerRepository, SpecialPlayerEntry } from '../database/repositories/specialplayer.repository.js';
import type { APlayer } from '../models/player.js';

/**
 * Special player group definition.
 * Matches the structure from adkatsspecialgroups.json.
 */
export interface SpecialGroupDefinition {
  /** Unique group ID */
  groupId: number;
  /** Group key used in database (e.g., 'whitelist_anticheat') */
  groupKey: string;
  /** Human-readable group name */
  groupName: string;
}

/**
 * Well-known special group keys.
 * These correspond to the groups defined in adkatsspecialgroups.json.
 */
export const SpecialGroupKeys = {
  // Whitelists
  WHITELIST_ANTICHEAT: 'whitelist_anticheat',
  WHITELIST_MULTIBALANCER: 'whitelist_multibalancer',
  WHITELIST_PING: 'whitelist_ping',
  WHITELIST_ADMIN_ASSISTANT: 'whitelist_adminassistant',
  WHITELIST_SPAMBOT: 'whitelist_spambot',
  WHITELIST_TEAMSWAP: 'whitelist_teamswap',
  WHITELIST_REPORT: 'whitelist_report',
  WHITELIST_POPULATOR: 'whitelist_populator',
  WHITELIST_TEAMKILL: 'whitelist_teamkill',
  WHITELIST_COMMAND_TARGET: 'whitelist_commandtarget',
  WHITELIST_SQUAD_SPLIT: 'whitelist_squadsplit',
  WHITELIST_SQUAD_SPLIT_ADVANCED: 'whitelist_squadsplit_advanced',

  // Blacklists
  BLACKLIST_DISPERSION: 'blacklist_dispersion',
  BLACKLIST_SPECTATOR: 'blacklist_spectator',
  BLACKLIST_REPORT: 'blacklist_report',
  BLACKLIST_AUTO_ASSIST: 'blacklist_autoassist',
  BLACKLIST_ALL_CAPS: 'blacklist_allcaps',

  // Slots
  SLOT_RESERVED: 'slot_reserved',
  SLOT_SPECTATOR: 'slot_spectator',

  // Challenges
  CHALLENGE_PLAY: 'challenge_play',
  CHALLENGE_AUTOKILL: 'challenge_autokill',
  CHALLENGE_IGNORE: 'challenge_ignore',
} as const;

/**
 * Type for special group keys.
 */
export type SpecialGroupKey = (typeof SpecialGroupKeys)[keyof typeof SpecialGroupKeys];

/**
 * Result of checking if a player is in a group.
 */
export interface GroupCheckResult {
  /** Whether the player is in the group */
  inGroup: boolean;
  /** The entry details if in the group */
  entry: SpecialPlayerEntry | null;
  /** Whether the entry is permanent */
  isPermanent: boolean;
  /** Time remaining in minutes (null if permanent or not in group) */
  remainingMinutes: number | null;
}

/**
 * Service for managing special player groups (whitelists/blacklists).
 * Provides caching for performance and integration with scheduler for expiration cleanup.
 */
export class SpecialPlayerService {
  private logger: Logger;
  private scheduler: Scheduler;
  private specialPlayerRepo: SpecialPlayerRepository;

  /** Cache of group definitions loaded from config */
  private groupDefinitions: Map<string, SpecialGroupDefinition> = new Map();

  /** Cache of player group memberships: Map<playerId, Set<groupKey>> */
  private playerGroupCache: Map<number, Set<string>> = new Map();

  /** Cache of all members in each group: Map<groupKey, Set<playerId>> */
  private groupMemberCache: Map<string, Set<number>> = new Map();

  /** Whether the cache has been initialized */
  private cacheInitialized = false;

  /** Job ID for the cleanup scheduler */
  private readonly CLEANUP_JOB_ID = 'specialplayer-cleanup';

  /** Maximum permanent duration in minutes (~20 years) */
  private readonly PERMANENT_DURATION_MINUTES = 10518984;

  constructor(
    logger: Logger,
    scheduler: Scheduler,
    specialPlayerRepo: SpecialPlayerRepository
  ) {
    this.logger = logger;
    this.scheduler = scheduler;
    this.specialPlayerRepo = specialPlayerRepo;
  }

  /**
   * Initialize the service with group definitions.
   * Call this during application startup.
   *
   * @param groupDefinitions - Array of group definitions from config/JSON
   */
  async initialize(groupDefinitions: SpecialGroupDefinition[]): Promise<void> {
    // Load group definitions
    for (const def of groupDefinitions) {
      this.groupDefinitions.set(def.groupKey, def);
    }

    this.logger.info({ groupCount: groupDefinitions.length }, 'Loaded special group definitions');

    // Load all group memberships into cache
    await this.refreshCache();

    // Register cleanup job to run every 5 minutes
    this.scheduler.registerIntervalJob(
      this.CLEANUP_JOB_ID,
      'Special Player Expiration Cleanup',
      5 * 60 * 1000, // 5 minutes
      () => this.cleanupExpiredEntries()
    );

    this.logger.info('Special player service initialized');
  }

  /**
   * Refresh the entire cache from the database.
   */
  async refreshCache(): Promise<void> {
    this.playerGroupCache.clear();
    this.groupMemberCache.clear();

    // Load all active entries for all known groups
    for (const groupKey of this.groupDefinitions.keys()) {
      const playerIds = await this.specialPlayerRepo.getPlayerIdsInGroup(groupKey);
      const memberSet = new Set(playerIds);
      this.groupMemberCache.set(groupKey, memberSet);

      // Also update player-centric cache
      for (const playerId of playerIds) {
        let playerGroups = this.playerGroupCache.get(playerId);
        if (!playerGroups) {
          playerGroups = new Set();
          this.playerGroupCache.set(playerId, playerGroups);
        }
        playerGroups.add(groupKey);
      }
    }

    this.cacheInitialized = true;
    this.logger.debug({ groupCount: this.groupMemberCache.size }, 'Special player cache refreshed');
  }

  /**
   * Clean up expired entries from the database.
   */
  private async cleanupExpiredEntries(): Promise<void> {
    try {
      const expiredCount = await this.specialPlayerRepo.expireOldEntries();
      if (expiredCount > 0) {
        this.logger.info({ count: expiredCount }, 'Cleaned up expired special player entries');
        // Refresh cache after cleanup
        await this.refreshCache();
      }
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Error cleaning up expired special player entries');
    }
  }

  /**
   * Check if a player is in a specific group.
   * Uses cache for fast lookups.
   *
   * @param playerId - Player's database ID
   * @param groupKey - Group key to check
   * @returns True if player is in the group
   */
  isPlayerInGroup(playerId: number, groupKey: string): boolean {
    if (!this.cacheInitialized) {
      this.logger.warn('Special player cache not initialized, returning false');
      return false;
    }

    const groupMembers = this.groupMemberCache.get(groupKey);
    return groupMembers?.has(playerId) ?? false;
  }

  /**
   * Check if a player is in a group with detailed information.
   * Queries the database for full entry details.
   *
   * @param playerId - Player's database ID
   * @param groupKey - Group key to check
   * @returns Detailed check result
   */
  async checkPlayerInGroup(playerId: number, groupKey: string): Promise<GroupCheckResult> {
    const entry = await this.specialPlayerRepo.findByPlayerId(playerId, groupKey);

    if (!entry) {
      return { inGroup: false, entry: null, isPermanent: false, remainingMinutes: null };
    }

    const now = Date.now();
    const expirationTime = entry.expirationDate.getTime();
    const remainingMs = expirationTime - now;
    const remainingMinutes = Math.max(0, Math.floor(remainingMs / 60000));

    // Consider permanent if more than 10 years remaining
    const isPermanent = remainingMinutes > this.PERMANENT_DURATION_MINUTES / 2;

    return {
      inGroup: true,
      entry,
      isPermanent,
      remainingMinutes: isPermanent ? null : remainingMinutes,
    };
  }

  /**
   * Add a player to a special group.
   *
   * @param player - The player to add
   * @param groupKey - Group key to add to
   * @param durationMinutes - Duration in minutes (null for permanent)
   * @param options - Optional server/game filtering
   * @returns The created entry
   */
  async addPlayerToGroup(
    player: APlayer,
    groupKey: string,
    durationMinutes: number | null,
    options?: { serverId?: number; gameId?: number }
  ): Promise<SpecialPlayerEntry> {
    // Validate group exists
    if (!this.groupDefinitions.has(groupKey)) {
      throw new Error(`Unknown special group: ${groupKey}`);
    }

    // Validate player ID
    if (player.playerId <= 0) {
      throw new Error('Player ID invalid, cannot add to special group');
    }

    // Add to database
    const entry = await this.specialPlayerRepo.add(
      player.playerId,
      groupKey,
      player.soldierName,
      durationMinutes,
      options
    );

    // Update cache
    let playerGroups = this.playerGroupCache.get(player.playerId);
    if (!playerGroups) {
      playerGroups = new Set();
      this.playerGroupCache.set(player.playerId, playerGroups);
    }
    playerGroups.add(groupKey);

    let groupMembers = this.groupMemberCache.get(groupKey);
    if (!groupMembers) {
      groupMembers = new Set();
      this.groupMemberCache.set(groupKey, groupMembers);
    }
    groupMembers.add(player.playerId);

    this.logger.info(
      {
        playerId: player.playerId,
        playerName: player.soldierName,
        groupKey,
        durationMinutes,
      },
      'Added player to special group'
    );

    return entry;
  }

  /**
   * Remove a player from a special group.
   *
   * @param player - The player to remove
   * @param groupKey - Group key to remove from
   * @returns True if the player was removed
   */
  async removePlayerFromGroup(player: APlayer, groupKey: string): Promise<boolean> {
    const removedCount = await this.specialPlayerRepo.remove(player.playerId, groupKey);

    if (removedCount > 0) {
      // Update cache
      const playerGroups = this.playerGroupCache.get(player.playerId);
      if (playerGroups) {
        playerGroups.delete(groupKey);
      }

      const groupMembers = this.groupMemberCache.get(groupKey);
      if (groupMembers) {
        groupMembers.delete(player.playerId);
      }

      this.logger.info(
        { playerId: player.playerId, playerName: player.soldierName, groupKey },
        'Removed player from special group'
      );
      return true;
    }

    return false;
  }

  /**
   * Get all groups a player belongs to.
   *
   * @param playerId - Player's database ID
   * @returns Array of group keys
   */
  getPlayerGroups(playerId: number): string[] {
    const groups = this.playerGroupCache.get(playerId);
    return groups ? Array.from(groups) : [];
  }

  /**
   * Get all groups a player belongs to with full details.
   *
   * @param playerId - Player's database ID
   * @param playerName - Player's name (for identifier-based lookups)
   * @returns Array of special player entries
   */
  async getPlayerGroupsDetailed(playerId: number, playerName: string): Promise<SpecialPlayerEntry[]> {
    return this.specialPlayerRepo.findMatchingForPlayer(playerId, playerName);
  }

  /**
   * Get all members of a specific group.
   *
   * @param groupKey - Group key to get members for
   * @returns Array of player IDs
   */
  getGroupMembers(groupKey: string): number[] {
    const members = this.groupMemberCache.get(groupKey);
    return members ? Array.from(members) : [];
  }

  /**
   * Get all members of a group with full details.
   *
   * @param groupKey - Group key to get members for
   * @returns Array of special player entries
   */
  async getGroupMembersDetailed(groupKey: string): Promise<SpecialPlayerEntry[]> {
    return this.specialPlayerRepo.findByGroupKey(groupKey);
  }

  /**
   * Get the count of members in a group.
   *
   * @param groupKey - Group key to count
   * @returns Number of members
   */
  getGroupMemberCount(groupKey: string): number {
    const members = this.groupMemberCache.get(groupKey);
    return members?.size ?? 0;
  }

  /**
   * Get a group definition by key.
   *
   * @param groupKey - Group key to look up
   * @returns Group definition or undefined
   */
  getGroupDefinition(groupKey: string): SpecialGroupDefinition | undefined {
    return this.groupDefinitions.get(groupKey);
  }

  /**
   * Get all group definitions.
   *
   * @returns Array of all group definitions
   */
  getAllGroupDefinitions(): SpecialGroupDefinition[] {
    return Array.from(this.groupDefinitions.values());
  }

  /**
   * Get the human-readable name for a group.
   *
   * @param groupKey - Group key to look up
   * @returns Human-readable name or the key itself if not found
   */
  getGroupName(groupKey: string): string {
    const def = this.groupDefinitions.get(groupKey);
    return def?.groupName ?? groupKey;
  }

  /**
   * Format a duration for display.
   *
   * @param durationMinutes - Duration in minutes (null for permanent)
   * @returns Human-readable duration string
   */
  formatDuration(durationMinutes: number | null): string {
    if (durationMinutes === null || durationMinutes >= this.PERMANENT_DURATION_MINUTES / 2) {
      return 'permanent';
    }

    if (durationMinutes < 60) {
      return `${Math.ceil(durationMinutes)} minute${durationMinutes !== 1 ? 's' : ''}`;
    }

    const hours = durationMinutes / 60;
    if (hours < 24) {
      const h = Math.floor(hours);
      return `${h} hour${h !== 1 ? 's' : ''}`;
    }

    const days = hours / 24;
    if (days < 7) {
      const d = Math.floor(days);
      return `${d} day${d !== 1 ? 's' : ''}`;
    }

    const weeks = days / 7;
    if (weeks < 4) {
      const w = Math.floor(weeks);
      return `${w} week${w !== 1 ? 's' : ''}`;
    }

    const months = days / 30;
    const mo = Math.floor(months);
    return `${mo} month${mo !== 1 ? 's' : ''}`;
  }

  /**
   * Format an expiration date for display.
   *
   * @param expirationDate - The expiration date
   * @returns Human-readable expiration string
   */
  formatExpiration(expirationDate: Date): string {
    const now = Date.now();
    const expirationTime = expirationDate.getTime();
    const remainingMs = expirationTime - now;

    if (remainingMs <= 0) {
      return 'expired';
    }

    const remainingMinutes = Math.floor(remainingMs / 60000);
    return this.formatDuration(remainingMinutes);
  }

  // ============================================
  // Convenience methods for common group checks
  // ============================================

  /**
   * Check if player is whitelisted from anti-cheat.
   */
  isAntiCheatWhitelisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.WHITELIST_ANTICHEAT);
  }

  /**
   * Check if player is whitelisted from ping enforcement.
   */
  isPingWhitelisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.WHITELIST_PING);
  }

  /**
   * Check if player is whitelisted from spambot messages.
   */
  isSpambotWhitelisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.WHITELIST_SPAMBOT);
  }

  /**
   * Check if player is whitelisted from auto-balance.
   */
  isBalanceWhitelisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.WHITELIST_MULTIBALANCER);
  }

  /**
   * Check if player is whitelisted from being reported.
   */
  isReportWhitelisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.WHITELIST_REPORT);
  }

  /**
   * Check if player has a reserved slot.
   */
  hasReservedSlot(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.SLOT_RESERVED);
  }

  /**
   * Check if player has a spectator slot.
   */
  hasSpectatorSlot(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.SLOT_SPECTATOR);
  }

  /**
   * Check if player is in the dispersion blacklist (balanced first).
   */
  isDispersionBlacklisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.BLACKLIST_DISPERSION);
  }

  /**
   * Check if player is blacklisted from spectating.
   */
  isSpectatorBlacklisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.BLACKLIST_SPECTATOR);
  }

  /**
   * Check if player's reports are auto-ignored.
   */
  isReportBlacklisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.BLACKLIST_REPORT);
  }

  /**
   * Check if player is whitelisted from teamkill punishment.
   */
  isTeamkillWhitelisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.WHITELIST_TEAMKILL);
  }

  /**
   * Check if player is a whitelisted populator.
   */
  isPopulatorWhitelisted(playerId: number): boolean {
    return this.isPlayerInGroup(playerId, SpecialGroupKeys.WHITELIST_POPULATOR);
  }
}

/**
 * Create a new special player service.
 */
export function createSpecialPlayerService(
  logger: Logger,
  scheduler: Scheduler,
  specialPlayerRepo: SpecialPlayerRepository
): SpecialPlayerService {
  return new SpecialPlayerService(logger, scheduler, specialPlayerRepo);
}
