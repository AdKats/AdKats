import type { Logger } from '../core/logger.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { APlayer } from '../models/player.js';
import type { AdKatsConfig } from '../core/config.js';

/**
 * Chat subset types from BF events.
 */
export type ChatSubset = 'all' | 'team' | 'squad' | 'player';

/**
 * Parsed chat message.
 */
export interface ParsedChat {
  player: APlayer;
  message: string;
  subset: ChatSubset;
  isCommand: boolean;
  commandText: string | null;
  commandArgs: string | null;
}

/**
 * Handler for player chat events.
 * Parses commands and forwards to command service.
 */
export class PlayerChatHandler {
  private logger: Logger;
  private eventBus: AdKatsEventBus;
  private config: AdKatsConfig;

  // Command handler callback
  private onCommand: ((chat: ParsedChat) => Promise<void>) | null = null;

  constructor(
    logger: Logger,
    eventBus: AdKatsEventBus,
    config: AdKatsConfig
  ) {
    this.logger = logger;
    this.eventBus = eventBus;
    this.config = config;
  }

  /**
   * Initialize the handler by subscribing to events.
   */
  initialize(): void {
    this.eventBus.onEvent('player:chat', (player, message, subset) => {
      void this.handlePlayerChat(player, message, subset);
    });

    this.logger.debug('PlayerChatHandler initialized');
  }

  /**
   * Set the command handler callback.
   */
  setCommandHandler(handler: (chat: ParsedChat) => Promise<void>): void {
    this.onCommand = handler;
  }

  /**
   * Handle player chat event.
   */
  private async handlePlayerChat(
    player: APlayer,
    message: string,
    rawSubset: string[]
  ): Promise<void> {
    // Parse subset
    const subset = this.parseSubset(rawSubset);

    // Log chat
    this.logger.trace({
      player: player.soldierName,
      message,
      subset,
    }, 'Player chat');

    // Check if message is a command
    const parsedChat = this.parseChat(player, message, subset);

    if (parsedChat.isCommand && this.onCommand) {
      this.logger.debug({
        player: player.soldierName,
        command: parsedChat.commandText,
        args: parsedChat.commandArgs,
      }, 'Command detected');

      try {
        await this.onCommand(parsedChat);
      } catch (error) {
        const msg = error instanceof Error ? error.message : String(error);
        this.logger.error({ error: msg, player: player.soldierName }, 'Error handling command');
      }
    }
  }

  /**
   * Parse chat subset from raw event data.
   */
  private parseSubset(rawSubset: string[]): ChatSubset {
    if (rawSubset.length === 0) {
      return 'all';
    }

    const type = rawSubset[0]?.toLowerCase();
    switch (type) {
      case 'team':
        return 'team';
      case 'squad':
        return 'squad';
      case 'player':
        return 'player';
      default:
        return 'all';
    }
  }

  /**
   * Parse chat message to detect commands.
   */
  private parseChat(player: APlayer, message: string, subset: ChatSubset): ParsedChat {
    const trimmedMessage = message.trim();

    // Check all command prefixes
    const prefixes = [
      this.config.commandPrefix,
      ...this.config.commandPrefixAlternates,
    ];

    for (const prefix of prefixes) {
      if (trimmedMessage.startsWith(prefix)) {
        const commandPart = trimmedMessage.slice(prefix.length).trim();
        const spaceIndex = commandPart.indexOf(' ');

        let commandText: string;
        let commandArgs: string | null;

        if (spaceIndex === -1) {
          commandText = commandPart.toLowerCase();
          commandArgs = null;
        } else {
          commandText = commandPart.slice(0, spaceIndex).toLowerCase();
          commandArgs = commandPart.slice(spaceIndex + 1).trim() || null;
        }

        return {
          player,
          message: trimmedMessage,
          subset,
          isCommand: true,
          commandText,
          commandArgs,
        };
      }
    }

    // Not a command
    return {
      player,
      message: trimmedMessage,
      subset,
      isCommand: false,
      commandText: null,
      commandArgs: null,
    };
  }
}

/**
 * Create a new player chat handler.
 */
export function createPlayerChatHandler(
  logger: Logger,
  eventBus: AdKatsEventBus,
  config: AdKatsConfig
): PlayerChatHandler {
  return new PlayerChatHandler(logger, eventBus, config);
}
