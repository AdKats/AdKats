import type { Logger } from '../core/logger.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { PlayerService } from '../services/player.service.js';
import type { APlayer } from '../models/player.js';

/**
 * Handler for player kill events.
 * Tracks kills, deaths, and weapon usage for anti-cheat and statistics.
 */
export class PlayerKillHandler {
  private logger: Logger;
  private eventBus: AdKatsEventBus;
  private playerService: PlayerService;

  // Kill tracking for potential anti-cheat checks
  private recentKills: Map<string, KillRecord[]> = new Map();

  constructor(
    logger: Logger,
    eventBus: AdKatsEventBus,
    playerService: PlayerService
  ) {
    this.logger = logger;
    this.eventBus = eventBus;
    this.playerService = playerService;
  }

  /**
   * Initialize the handler by subscribing to events.
   */
  initialize(): void {
    this.eventBus.onEvent('player:kill', (killer, victim, weapon, headshot) => {
      this.handlePlayerKill(killer, victim, weapon, headshot);
    });

    this.eventBus.onEvent('server:roundOver', () => {
      this.handleRoundOver();
    });

    this.logger.debug('PlayerKillHandler initialized');
  }

  /**
   * Handle player kill event.
   */
  private handlePlayerKill(
    killer: APlayer | null,
    victim: APlayer,
    weapon: string,
    headshot: boolean
  ): void {
    // Log the kill
    this.logger.trace({
      killer: killer?.soldierName ?? 'Server',
      victim: victim.soldierName,
      weapon,
      headshot,
    }, 'Kill event');

    // Track kill for potential anti-cheat analysis
    if (killer) {
      this.recordKill(killer, victim, weapon, headshot);
    }

    // Update player states (already done in adapter, but can add additional logic here)
    // victim.isAlive is set to false by the adapter
  }

  /**
   * Record a kill for tracking.
   */
  private recordKill(
    killer: APlayer,
    victim: APlayer,
    weapon: string,
    headshot: boolean
  ): void {
    const record: KillRecord = {
      victimName: victim.soldierName,
      weapon,
      headshot,
      timestamp: Date.now(),
    };

    let kills = this.recentKills.get(killer.soldierName);
    if (!kills) {
      kills = [];
      this.recentKills.set(killer.soldierName, kills);
    }

    kills.push(record);

    // Keep only last 100 kills per player for memory efficiency
    if (kills.length > 100) {
      kills.shift();
    }
  }

  /**
   * Get recent kills for a player.
   */
  getRecentKills(playerName: string): KillRecord[] {
    return this.recentKills.get(playerName) ?? [];
  }

  /**
   * Calculate headshot percentage for a player from recent kills.
   */
  getHeadshotPercentage(playerName: string): number {
    const kills = this.recentKills.get(playerName);
    if (!kills || kills.length === 0) {
      return 0;
    }

    const headshots = kills.filter(k => k.headshot).length;
    return (headshots / kills.length) * 100;
  }

  /**
   * Get kills per minute for a player (from recent kills within timeframe).
   */
  getKillsPerMinute(playerName: string, windowMinutes: number = 5): number {
    const kills = this.recentKills.get(playerName);
    if (!kills || kills.length === 0) {
      return 0;
    }

    const cutoff = Date.now() - (windowMinutes * 60 * 1000);
    const recentKills = kills.filter(k => k.timestamp >= cutoff);

    return recentKills.length / windowMinutes;
  }

  /**
   * Handle round over - clean up tracking data.
   */
  private handleRoundOver(): void {
    // Clear old kill records on round end
    this.recentKills.clear();
    this.logger.debug('Cleared kill tracking data for new round');
  }
}

/**
 * Kill record for tracking.
 */
interface KillRecord {
  victimName: string;
  weapon: string;
  headshot: boolean;
  timestamp: number;
}

/**
 * Create a new player kill handler.
 */
export function createPlayerKillHandler(
  logger: Logger,
  eventBus: AdKatsEventBus,
  playerService: PlayerService
): PlayerKillHandler {
  return new PlayerKillHandler(logger, eventBus, playerService);
}
