import type { Logger } from '../core/logger.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { PlayerService } from '../services/player.service.js';
import type { BanRepository } from '../database/repositories/ban.repository.js';
import type { APlayer } from '../models/player.js';
import type { AdKatsConfig } from '../core/config.js';

/**
 * Handler for player join events.
 * Performs ban checks, sends welcome messages, and initializes player state.
 */
export class PlayerJoinHandler {
  private logger: Logger;
  private eventBus: AdKatsEventBus;
  private bcAdapter: BattleConAdapter;
  private playerService: PlayerService;
  private banRepo: BanRepository;
  private config: AdKatsConfig;

  constructor(
    logger: Logger,
    eventBus: AdKatsEventBus,
    bcAdapter: BattleConAdapter,
    playerService: PlayerService,
    banRepo: BanRepository,
    config: AdKatsConfig
  ) {
    this.logger = logger;
    this.eventBus = eventBus;
    this.bcAdapter = bcAdapter;
    this.playerService = playerService;
    this.banRepo = banRepo;
    this.config = config;
  }

  /**
   * Initialize the handler by subscribing to events.
   */
  initialize(): void {
    this.eventBus.onEvent('player:join', (player) => {
      void this.handlePlayerJoin(player);
    });

    this.eventBus.onEvent('player:authenticated', (player) => {
      void this.handlePlayerAuthenticated(player);
    });

    this.logger.debug('PlayerJoinHandler initialized');
  }

  /**
   * Handle player join event.
   */
  private async handlePlayerJoin(player: APlayer): Promise<void> {
    this.logger.info({
      name: player.soldierName,
      guid: player.guid,
      playerId: player.playerId
    }, 'Player joined server');

    // Check for active bans if ban enforcer is enabled
    if (this.config.enableBanEnforcer) {
      await this.checkBans(player);
    }

    // Log join to database (could be a record)
    // TODO: Create join record if configured
  }

  /**
   * Handle player authenticated event (after GUID verification).
   */
  private async handlePlayerAuthenticated(player: APlayer): Promise<void> {
    this.logger.debug({ name: player.soldierName }, 'Player authenticated');

    // Additional checks after authentication
    // This is when we have the verified GUID
  }

  /**
   * Check if a player has any active bans.
   */
  private async checkBans(player: APlayer): Promise<void> {
    try {
      // Check ban by player ID first (fastest)
      let ban = await this.banRepo.findActiveByPlayerId(player.playerId);

      // If not found by ID, check by GUID
      if (!ban && player.guid && this.config.banEnforcer.enforceGuid) {
        ban = await this.banRepo.findActiveByGuid(player.guid);
      }

      // Check by IP if available
      if (!ban && player.ipAddress && this.config.banEnforcer.enforceIp) {
        ban = await this.banRepo.findActiveByIp(player.ipAddress);
      }

      // Check by name
      if (!ban && this.config.banEnforcer.enforceName) {
        ban = await this.banRepo.findActiveByName(player.soldierName);
      }

      if (ban) {
        this.logger.info({
          name: player.soldierName,
          banId: ban.banId,
          reason: ban.banNotes,
        }, 'Banned player detected');

        // Kick the banned player
        await this.enforceBan(player, ban.banNotes);
      }
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, name: player.soldierName }, 'Error checking bans');
    }
  }

  /**
   * Enforce a ban by kicking the player.
   */
  private async enforceBan(player: APlayer, reason: string): Promise<void> {
    const kickMessage = `[AdKats] You are banned: ${reason}`;

    try {
      await this.bcAdapter.kickPlayer(player.soldierName, kickMessage);
      this.logger.info({ name: player.soldierName, reason }, 'Banned player kicked');

      // Emit ban enforced event
      // TODO: Create enforcement record
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, name: player.soldierName }, 'Failed to kick banned player');
    }
  }
}

/**
 * Create a new player join handler.
 */
export function createPlayerJoinHandler(
  logger: Logger,
  eventBus: AdKatsEventBus,
  bcAdapter: BattleConAdapter,
  playerService: PlayerService,
  banRepo: BanRepository,
  config: AdKatsConfig
): PlayerJoinHandler {
  return new PlayerJoinHandler(logger, eventBus, bcAdapter, playerService, banRepo, config);
}
