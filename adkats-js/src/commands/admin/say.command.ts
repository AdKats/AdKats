import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Say command - broadcasts a message to all players.
 * Usage: @say <message>
 */
export class SayCommand extends BaseCommand {
  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.SAY];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a message
    if (!this.requireArgs(ctx, 'message')) {
      return;
    }

    const message = ctx.args!.trim();

    try {
      // Send the message
      await this.bcAdapter.say(`[Admin] ${message}`);

      // Log the action
      ctx.record.recordMessage = message;
      await this.logRecord(ctx);

      this.logger.info({
        source: ctx.player.soldierName,
        message,
      }, 'Admin say');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to send admin say');
      await this.respondError(ctx, 'Failed to send message');
    }
  }
}

/**
 * Player say command - sends a message to a specific player.
 * Usage: @psay <player> <message>
 */
export class PlayerSayCommand extends BaseCommand {
  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.PLAYER_SAY];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    // Require a message
    if (!this.requireArgs(ctx, 'message')) {
      return;
    }

    const target = ctx.targetPlayer!;
    const message = ctx.args!.trim();

    try {
      // Send the message to the player
      await this.bcAdapter.sayPlayer(`[Admin] ${message}`, target.soldierName);

      // Log the action
      ctx.record.recordMessage = message;
      await this.logRecord(ctx);

      // Confirm to source
      await this.respond(ctx, `Message sent to ${target.soldierName}`);

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        message,
      }, 'Player say');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to send player say');
      await this.respondError(ctx, `Failed to send message to ${target.soldierName}`);
    }
  }
}

/**
 * Yell command - yells a message to all players.
 * Usage: @yell <message>
 */
export class YellCommand extends BaseCommand {
  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.YELL];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a message
    if (!this.requireArgs(ctx, 'message')) {
      return;
    }

    const message = ctx.args!.trim();

    try {
      // Yell the message (5 seconds duration)
      await this.bcAdapter.yell(`[Admin] ${message}`, 5);

      // Log the action
      ctx.record.recordMessage = message;
      await this.logRecord(ctx);

      this.logger.info({
        source: ctx.player.soldierName,
        message,
      }, 'Admin yell');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to send admin yell');
      await this.respondError(ctx, 'Failed to yell message');
    }
  }
}

/**
 * Player yell command - yells a message to a specific player.
 * Usage: @pyell <player> <message>
 */
export class PlayerYellCommand extends BaseCommand {
  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.PLAYER_YELL];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    // Require a message
    if (!this.requireArgs(ctx, 'message')) {
      return;
    }

    const target = ctx.targetPlayer!;
    const message = ctx.args!.trim();

    try {
      // Yell the message to the player
      await this.bcAdapter.yellPlayer(`[Admin] ${message}`, target.soldierName, 5);

      // Log the action
      ctx.record.recordMessage = message;
      await this.logRecord(ctx);

      // Confirm to source
      await this.respond(ctx, `Yelled at ${target.soldierName}`);

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        message,
      }, 'Player yell');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to yell at player');
      await this.respondError(ctx, `Failed to yell at ${target.soldierName}`);
    }
  }
}

/**
 * Create and register say commands.
 */
export function registerSayCommands(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository
): void {
  new SayCommand(logger, bcAdapter, commandService, recordRepo).register();
  new PlayerSayCommand(logger, bcAdapter, commandService, recordRepo).register();
  new YellCommand(logger, bcAdapter, commandService, recordRepo).register();
  new PlayerYellCommand(logger, bcAdapter, commandService, recordRepo).register();
}
