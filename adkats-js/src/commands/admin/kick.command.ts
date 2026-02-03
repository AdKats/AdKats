import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Kick command - kicks a player from the server.
 * Usage: @kick <player> [reason]
 */
export class KickCommand extends BaseCommand {
  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.KICK];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;
    const reason = ctx.args?.trim() || 'Kicked by admin';

    try {
      // Kick the player
      const kickMessage = `[AdKats] Kicked by ${ctx.player.soldierName}: ${reason}`;
      await this.bcAdapter.kickPlayer(target.soldierName, kickMessage);

      // Update player state
      target.isOnline = false;

      // Log the action
      ctx.record.recordMessage = reason;
      await this.logRecord(ctx);

      // Respond to source
      await this.respond(ctx, `Kicked ${target.soldierName} - ${reason}`);

      // Announce to server (optional, configurable)
      await this.bcAdapter.say(`${target.soldierName} was kicked: ${reason}`);

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        reason,
      }, 'Player kicked by admin');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to kick player');
      await this.respondError(ctx, `Failed to kick ${target.soldierName}`);
    }
  }
}

/**
 * Create and register the kick command.
 */
export function registerKickCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository
): KickCommand {
  const cmd = new KickCommand(logger, bcAdapter, commandService, recordRepo);
  cmd.register();
  return cmd;
}
