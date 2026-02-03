import type { Logger } from '../core/logger.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { PlayerService } from '../services/player.service.js';

/**
 * Handler for round end events.
 * Manages end-of-round statistics and cleanup.
 */
export class RoundEndHandler {
  private logger: Logger;
  private eventBus: AdKatsEventBus;
  private playerService: PlayerService;

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
    this.eventBus.onEvent('server:roundOver', (winningTeamId) => {
      this.handleRoundOver(winningTeamId);
    });

    this.eventBus.onEvent('server:roundOverPlayers', (players) => {
      this.handleRoundOverPlayers(players);
    });

    this.eventBus.onEvent('server:roundOverTeamScores', (scores, targetScore) => {
      this.handleRoundOverTeamScores(scores, targetScore);
    });

    this.eventBus.onEvent('server:levelLoaded', (map, mode, roundNum, roundsTotal) => {
      this.handleLevelLoaded(map, mode, roundNum, roundsTotal);
    });

    this.logger.debug('RoundEndHandler initialized');
  }

  /**
   * Handle round over event.
   */
  private handleRoundOver(winningTeamId: number): void {
    this.logger.info({ winningTeamId }, 'Round ended');

    // TODO: Record round statistics
    // TODO: Process any end-of-round actions
  }

  /**
   * Handle round over players data.
   */
  private handleRoundOverPlayers(players: unknown[]): void {
    this.logger.debug({ playerCount: players.length }, 'Round over players received');

    // This contains final player statistics for the round
    // Can be used for round statistics recording
  }

  /**
   * Handle round over team scores.
   */
  private handleRoundOverTeamScores(scores: number[], targetScore: number): void {
    this.logger.debug({ scores, targetScore }, 'Round over team scores');

    // Record team scores for statistics
  }

  /**
   * Handle level loaded (new round starting).
   */
  private handleLevelLoaded(
    map: string,
    mode: string,
    roundNum: number,
    roundsTotal: number
  ): void {
    this.logger.info({
      map,
      mode,
      round: `${roundNum}/${roundsTotal}`,
    }, 'New level loaded');

    // Reset round-specific state
    // Player kills/deaths are reset by the game server

    // TODO: Send round start message if configured
    // TODO: Reset any round-specific tracking
  }
}

/**
 * Create a new round end handler.
 */
export function createRoundEndHandler(
  logger: Logger,
  eventBus: AdKatsEventBus,
  playerService: PlayerService
): RoundEndHandler {
  return new RoundEndHandler(logger, eventBus, playerService);
}
