import type { Logger } from '../core/logger.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { APlayer } from '../models/player.js';

/**
 * Reputation change event data.
 */
export interface ReputationChangeEvent {
  player: APlayer;
  previousReputation: number;
  newReputation: number;
  reason: string;
  delta: number;
}

/**
 * Reputation thresholds for categorization.
 */
export interface ReputationThresholds {
  /** Reputation above this is considered "good" */
  good: number;
  /** Reputation below this is considered "bad" */
  bad: number;
}

/**
 * Command reputation weights for source (admin issuing command).
 * Map of "commandType|commandAction" to weight value.
 */
export type CommandReputationWeights = Map<string, number>;

/**
 * Configuration for the reputation service.
 */
export interface ReputationConfig {
  /** Enable/disable the reputation system */
  enabled: boolean;
  /** Base reputation for new players */
  baseReputation: number;
  /** Thresholds for good/bad reputation */
  thresholds: ReputationThresholds;
  /** Reputation weights when player is source of command */
  sourceWeights: CommandReputationWeights;
  /** Reputation weights when player is target of command */
  targetWeights: CommandReputationWeights;
  /** Days for punishment decay (punishments older than this have no effect) */
  punishmentDecayDays: number;
  /** Max reputation impact per punishment within decay window */
  punishmentMaxImpact: number;
}

/**
 * Database row for player reputation.
 */
export interface ReputationDbRow {
  player_id: number;
  game_id: number;
  target_rep: number;
  source_rep: number;
  total_rep: number;
  total_rep_co: number;
}

/**
 * Detailed reputation breakdown.
 */
export interface ReputationBreakdown {
  playerId: number;
  sourceReputation: number;
  targetReputation: number;
  totalReputation: number;
  totalReputationConstrained: number;
  category: 'good' | 'neutral' | 'bad';
}

/**
 * Repository interface for reputation data.
 */
export interface ReputationRepository {
  getReputation(playerId: number): Promise<ReputationBreakdown | null>;
  saveReputation(breakdown: ReputationBreakdown, gameId: number): Promise<void>;
  getCommandCounts(
    playerId: number,
    asSource: boolean
  ): Promise<Map<string, number>>;
  getRecentPunishments(
    playerId: number,
    days: number
  ): Promise<Array<{ recordTime: Date; pointValue: number }>>;
  getRecentForgives(
    playerId: number,
    days: number
  ): Promise<Array<{ recordTime: Date; pointValue: number }>>;
}

/**
 * ReputationService - tracks and manages player reputation.
 *
 * Reputation is calculated based on:
 * - Commands issued by the player (source reputation)
 * - Commands targeting the player (target reputation)
 * - Recent punishments and forgives with time decay
 *
 * The total reputation is constrained to a range of -1000 to 1000 using
 * a formula that prevents extreme values from growing unbounded.
 */
export class ReputationService {
  private logger: Logger;
  private eventBus: AdKatsEventBus;
  private reputationRepo: ReputationRepository;
  private config: ReputationConfig;
  private gameId: number;

  // In-memory cache of player reputations
  private reputationCache: Map<number, ReputationBreakdown> = new Map();

  constructor(
    logger: Logger,
    eventBus: AdKatsEventBus,
    reputationRepo: ReputationRepository,
    config: ReputationConfig,
    gameId: number
  ) {
    this.logger = logger;
    this.eventBus = eventBus;
    this.reputationRepo = reputationRepo;
    this.config = config;
    this.gameId = gameId;
  }

  /**
   * Initialize the reputation service.
   */
  initialize(): void {
    if (!this.config.enabled) {
      this.logger.info('Reputation system is disabled');
      return;
    }

    // Listen for command execution to update reputation
    this.eventBus.onEvent('command:executed', (record) => {
      // Update source player reputation if applicable
      if (record.sourcePlayer && record.sourceId && record.sourceId > 0) {
        void this.updateReputation(record.sourcePlayer, 'command_source');
      }
      // Update target player reputation if applicable
      if (record.targetPlayer && record.targetId && record.targetId > 0) {
        void this.updateReputation(record.targetPlayer, 'command_target');
      }
    });

    this.logger.info('Reputation service initialized');
  }

  /**
   * Get a player's current reputation.
   * Returns cached value if available, otherwise fetches from database.
   */
  async getPlayerReputation(player: APlayer): Promise<ReputationBreakdown | null> {
    if (player.playerId <= 0) {
      return null;
    }

    // Check cache first
    const cached = this.reputationCache.get(player.playerId);
    if (cached) {
      return cached;
    }

    // Fetch from database
    const breakdown = await this.reputationRepo.getReputation(player.playerId);
    if (breakdown) {
      this.reputationCache.set(player.playerId, breakdown);
    }

    return breakdown;
  }

  /**
   * Get a player's reputation value (constrained).
   * Returns the base reputation if not found.
   */
  async getReputationValue(player: APlayer): Promise<number> {
    const breakdown = await this.getPlayerReputation(player);
    return breakdown?.totalReputationConstrained ?? this.config.baseReputation;
  }

  /**
   * Update a player's reputation based on all their records.
   * This recalculates the full reputation from scratch.
   */
  async updateReputation(player: APlayer, reason: string = 'update'): Promise<ReputationBreakdown> {
    if (player.playerId <= 0) {
      throw new Error('Cannot update reputation for player without valid ID');
    }

    const previousBreakdown = await this.getPlayerReputation(player);
    const previousReputation = previousBreakdown?.totalReputationConstrained ?? this.config.baseReputation;

    // Calculate source reputation (commands issued by player)
    let sourceReputation = 0;
    const sourceCounts = await this.reputationRepo.getCommandCounts(player.playerId, true);
    for (const [typeAction, count] of sourceCounts) {
      const weight = this.config.sourceWeights.get(typeAction);
      if (weight !== undefined) {
        sourceReputation += weight * count;
      }
    }

    // Calculate target reputation (commands targeting player)
    let targetReputation = 0;
    const targetCounts = await this.reputationRepo.getCommandCounts(player.playerId, false);
    for (const [typeAction, count] of targetCounts) {
      const weight = this.config.targetWeights.get(typeAction);
      if (weight !== undefined) {
        targetReputation += weight * count;
      }
    }

    // Calculate punishment impact with time decay
    let punishmentImpact = 0;
    const recentPunishments = await this.reputationRepo.getRecentPunishments(
      player.playerId,
      this.config.punishmentDecayDays
    );
    const now = new Date();
    for (const punishment of recentPunishments) {
      const daysSince = (now.getTime() - punishment.recordTime.getTime()) / (1000 * 60 * 60 * 24);
      if (daysSince < this.config.punishmentDecayDays) {
        // Linear decay: full impact at day 0, zero impact at decayDays
        const decayFactor = (this.config.punishmentDecayDays - daysSince) / this.config.punishmentDecayDays;
        punishmentImpact -= this.config.punishmentMaxImpact * decayFactor;
      }
    }

    // Calculate forgive impact with time decay (can offset punishments but not go positive)
    let forgiveImpact = 0;
    const recentForgives = await this.reputationRepo.getRecentForgives(
      player.playerId,
      this.config.punishmentDecayDays
    );
    for (const forgive of recentForgives) {
      const daysSince = (now.getTime() - forgive.recordTime.getTime()) / (1000 * 60 * 60 * 24);
      if (daysSince < this.config.punishmentDecayDays) {
        const decayFactor = (this.config.punishmentDecayDays - daysSince) / this.config.punishmentDecayDays;
        forgiveImpact += this.config.punishmentMaxImpact * decayFactor;
      }
    }

    // Forgives can only offset punishments, not add positive reputation
    const pointReputation = Math.min(0, punishmentImpact + forgiveImpact);
    targetReputation += pointReputation;

    // Calculate total reputation
    const totalReputation = sourceReputation + targetReputation;

    // Constrain to -1000 to 1000 range using the AdKats formula
    let totalReputationConstrained: number;
    if (totalReputation >= 0) {
      totalReputationConstrained = (1000 * totalReputation) / (totalReputation + 1000);
    } else {
      totalReputationConstrained = -(1000 * Math.abs(totalReputation)) / (Math.abs(totalReputation) + 1000);
    }

    // Determine category
    let category: 'good' | 'neutral' | 'bad';
    if (totalReputationConstrained >= this.config.thresholds.good) {
      category = 'good';
    } else if (totalReputationConstrained < this.config.thresholds.bad) {
      category = 'bad';
    } else {
      category = 'neutral';
    }

    const breakdown: ReputationBreakdown = {
      playerId: player.playerId,
      sourceReputation,
      targetReputation,
      totalReputation,
      totalReputationConstrained,
      category,
    };

    // Save to database
    await this.reputationRepo.saveReputation(breakdown, this.gameId);

    // Update cache
    this.reputationCache.set(player.playerId, breakdown);

    // Update player object
    player.reputation = totalReputationConstrained;

    // Log significant changes
    const delta = totalReputationConstrained - previousReputation;
    if (Math.abs(delta) >= 10) {
      this.logger.info({
        player: player.soldierName,
        playerId: player.playerId,
        previousReputation,
        newReputation: totalReputationConstrained,
        delta,
        reason,
      }, 'Significant reputation change');
    }

    this.logger.debug({
      player: player.soldierName,
      sourceRep: sourceReputation,
      targetRep: targetReputation,
      totalRep: totalReputation,
      constrained: totalReputationConstrained,
      category,
    }, 'Updated player reputation');

    return breakdown;
  }

  /**
   * Get the reputation category for a value.
   */
  getReputationCategory(reputation: number): 'good' | 'neutral' | 'bad' {
    if (reputation >= this.config.thresholds.good) {
      return 'good';
    } else if (reputation < this.config.thresholds.bad) {
      return 'bad';
    }
    return 'neutral';
  }

  /**
   * Format reputation for display.
   */
  formatReputation(reputation: number): string {
    const category = this.getReputationCategory(reputation);
    const sign = reputation >= 0 ? '+' : '';
    const categoryLabel = category.charAt(0).toUpperCase() + category.slice(1);
    return `${sign}${reputation.toFixed(0)} (${categoryLabel})`;
  }

  /**
   * Clear cached reputation for a player.
   */
  clearCache(playerId: number): void {
    this.reputationCache.delete(playerId);
  }

  /**
   * Clear all cached reputations.
   */
  clearAllCache(): void {
    this.reputationCache.clear();
  }
}

/**
 * Create default reputation configuration.
 */
export function createDefaultReputationConfig(): ReputationConfig {
  // Default source weights (when player issues command)
  const sourceWeights = new Map<string, number>([
    // Positive actions when issuing
    ['51|51', 5],     // Assist (helping weak team)
    ['47|47', 2],     // Commend another player

    // Neutral/negative when issuing (usually admin actions)
    ['9|9', 0],       // Kick
    ['8|8', 0],       // Ban
    ['6|6', 0],       // Temp ban
  ]);

  // Default target weights (when player is targeted)
  const targetWeights = new Map<string, number>([
    // Negative when targeted
    ['9|9', -10],     // Kick (being kicked)
    ['8|8', -50],     // Permanent ban
    ['6|6', -20],     // Temp ban
    ['5|5', -5],      // Kill
    ['4|4', -2],      // Warn

    // Positive when targeted
    ['47|47', 5],     // Commended
    ['17|17', 5],     // Forgiven
  ]);

  return {
    enabled: true,
    baseReputation: 0,
    thresholds: {
      good: 75,
      bad: 0,
    },
    sourceWeights,
    targetWeights,
    punishmentDecayDays: 50,
    punishmentMaxImpact: 20,
  };
}

/**
 * Create a new reputation service.
 */
export function createReputationService(
  logger: Logger,
  eventBus: AdKatsEventBus,
  reputationRepo: ReputationRepository,
  config: ReputationConfig,
  gameId: number
): ReputationService {
  return new ReputationService(logger, eventBus, reputationRepo, config, gameId);
}
