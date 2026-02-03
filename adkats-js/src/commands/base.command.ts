import type { Logger } from '../core/logger.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { CommandService, CommandContext, CommandHandler } from '../services/command.service.js';
import type { RecordRepository } from '../database/repositories/record.repository.js';

/**
 * Base class for command implementations.
 * Provides common functionality for all commands.
 */
export abstract class BaseCommand {
  protected logger: Logger;
  protected bcAdapter: BattleConAdapter;
  protected commandService: CommandService;
  protected recordRepo: RecordRepository;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository
  ) {
    this.logger = logger;
    this.bcAdapter = bcAdapter;
    this.commandService = commandService;
    this.recordRepo = recordRepo;
  }

  /**
   * Get the command key(s) this handler handles.
   */
  abstract getCommandKeys(): string[];

  /**
   * Execute the command.
   */
  abstract execute(ctx: CommandContext): Promise<void>;

  /**
   * Register this command with the command service.
   */
  register(): void {
    const handler: CommandHandler = (ctx) => this.execute(ctx);

    for (const key of this.getCommandKeys()) {
      this.commandService.registerHandler(key, handler);
      this.logger.debug({ commandKey: key }, 'Registered command handler');
    }
  }

  /**
   * Send a message to the command source.
   */
  protected async respond(ctx: CommandContext, message: string): Promise<void> {
    await this.bcAdapter.sayPlayer(message, ctx.player.soldierName);
  }

  /**
   * Send an error message to the command source.
   */
  protected async respondError(ctx: CommandContext, message: string): Promise<void> {
    await this.bcAdapter.sayPlayer(`[Error] ${message}`, ctx.player.soldierName);
  }

  /**
   * Send a success message to the command source.
   */
  protected async respondSuccess(ctx: CommandContext, message: string): Promise<void> {
    await this.bcAdapter.sayPlayer(`[Success] ${message}`, ctx.player.soldierName);
  }

  /**
   * Log the command execution to database.
   */
  protected async logRecord(ctx: CommandContext): Promise<void> {
    try {
      await this.recordRepo.create(ctx.record);
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to log command record');
    }
  }

  /**
   * Require a target player for the command.
   */
  protected requireTarget(ctx: CommandContext): boolean {
    if (!ctx.targetPlayer) {
      void this.respondError(ctx, 'No target player specified');
      return false;
    }
    return true;
  }

  /**
   * Require arguments for the command.
   */
  protected requireArgs(ctx: CommandContext, description: string = 'arguments'): boolean {
    if (!ctx.args || ctx.args.trim().length === 0) {
      void this.respondError(ctx, `Missing ${description}`);
      return false;
    }
    return true;
  }
}

/**
 * Helper to create a simple command handler function.
 */
export function createSimpleHandler(
  bcAdapter: BattleConAdapter,
  handler: (ctx: CommandContext, bc: BattleConAdapter) => Promise<void>
): CommandHandler {
  return async (ctx: CommandContext) => {
    await handler(ctx, bcAdapter);
  };
}
