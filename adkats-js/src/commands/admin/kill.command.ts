import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Kill command - kills a player.
 * Usage: @kill <player> [reason]
 */
export class KillCommand extends BaseCommand {
  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
  }

  getCommandKeys(): string[] {
    return [
      CommandKeys.KILL,
      CommandKeys.KILL_FORCE,
    ];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;
    const reason = ctx.args?.trim() || 'Killed by admin';

    try {
      // Kill the player
      await this.bcAdapter.killPlayer(target.soldierName);

      // Update player state
      target.isAlive = false;

      // Notify the target
      await this.bcAdapter.sayPlayer(
        `You were killed by ${ctx.player.soldierName}. Reason: ${reason}`,
        target.soldierName
      );

      // Log the action
      ctx.record.recordMessage = reason;
      await this.logRecord(ctx);

      // Respond to source
      await this.respond(ctx, `Killed ${target.soldierName}`);

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        reason,
      }, 'Player killed by admin');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to kill player');
      await this.respondError(ctx, `Failed to kill ${target.soldierName}`);
    }
  }
}

/**
 * Create and register the kill command.
 */
export function registerKillCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository
): KillCommand {
  const cmd = new KillCommand(logger, bcAdapter, commandService, recordRepo);
  cmd.register();
  return cmd;
}
