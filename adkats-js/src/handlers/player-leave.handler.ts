import type { Logger } from '../core/logger.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { PlayerService } from '../services/player.service.js';
import type { APlayer } from '../models/player.js';

/**
 * Handler for player leave events.
 * Cleans up player state and logs departures.
 */
export class PlayerLeaveHandler {
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
    this.eventBus.onEvent('player:leave', (player) => {
      this.handlePlayerLeave(player);
    });

    this.logger.debug('PlayerLeaveHandler initialized');
  }

  /**
   * Handle player leave event.
   */
  private handlePlayerLeave(player: APlayer): void {
    this.logger.info({
      name: player.soldierName,
      playerId: player.playerId,
      kills: player.kills,
      deaths: player.deaths,
      score: player.score,
    }, 'Player left server');

    // Player is automatically removed from cache by the BattleCon adapter
    // Additional cleanup can be done here if needed

    // TODO: Cancel any pending actions for this player
    // TODO: Clear any queued messages for this player
  }
}

/**
 * Create a new player leave handler.
 */
export function createPlayerLeaveHandler(
  logger: Logger,
  eventBus: AdKatsEventBus,
  playerService: PlayerService
): PlayerLeaveHandler {
  return new PlayerLeaveHandler(logger, eventBus, playerService);
}
