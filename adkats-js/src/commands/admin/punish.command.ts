import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { InfractionService } from '../../services/infraction.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Punish command - issues a punishment based on infraction points.
 * Usage: @punish <player> [reason]
 *
 * The punishment issued depends on the player's current infraction points
 * and the configured punishment hierarchy.
 */
export class PunishCommand extends BaseCommand {
  private infractionService: InfractionService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    infractionService: InfractionService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.infractionService = infractionService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.PUNISH];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;
    const reason = ctx.args?.trim() || 'Punished by admin';

    try {
      // Issue the punishment
      const result = await this.infractionService.punish(
        ctx.player,
        target,
        reason,
        ctx.record
      );

      // Update the record with the result
      ctx.record.recordMessage = reason;
      await this.logRecord(ctx);

      if (result.success) {
        // Respond to source
        await this.respond(ctx, result.message);

        // Announce to server (optional)
        const announcement = result.wasIro
          ? `${target.soldierName} received ${this.infractionService.getPunishmentName(result.punishmentType)} for: ${reason} [IRO]`
          : `${target.soldierName} received ${this.infractionService.getPunishmentName(result.punishmentType)} for: ${reason}`;
        await this.bcAdapter.say(announcement);

      } else {
        await this.respondError(ctx, result.message);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to punish player');
      await this.respondError(ctx, `Failed to punish ${target.soldierName}`);
    }
  }
}

/**
 * Forgive command - removes infraction points from a player.
 * Usage: @forgive <player> [count] [reason]
 *
 * If count is not specified, removes 1 point.
 */
export class ForgiveCommand extends BaseCommand {
  private infractionService: InfractionService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    infractionService: InfractionService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.infractionService = infractionService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.FORGIVE];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;

    // Parse arguments: [count] [reason]
    let count = 1;
    let reason = 'Forgiven by admin';

    if (ctx.args) {
      const parts = ctx.args.trim().split(/\s+/);
      const firstPart = parts[0];

      // Check if first part is a number
      if (firstPart && /^\d+$/.test(firstPart)) {
        count = parseInt(firstPart, 10);
        reason = parts.slice(1).join(' ') || reason;
      } else {
        reason = ctx.args.trim() || reason;
      }
    }

    // Clamp count to reasonable range
    count = Math.max(1, Math.min(count, 100));

    try {
      // Issue the forgiveness
      const result = await this.infractionService.forgive(
        ctx.player,
        target,
        count,
        reason,
        ctx.record
      );

      // Update the record
      ctx.record.recordMessage = `Forgave ${result.pointsRemoved} point(s): ${reason}`;
      ctx.record.commandNumeric = result.pointsRemoved;
      await this.logRecord(ctx);

      if (result.success) {
        await this.respondSuccess(ctx, result.message);
      } else {
        await this.respondError(ctx, result.message);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to forgive player');
      await this.respondError(ctx, `Failed to forgive ${target.soldierName}`);
    }
  }
}

/**
 * Warn command - sends a warning to a player without adding infraction points.
 * Usage: @warn <player> <reason>
 */
export class WarnCommand extends BaseCommand {
  private infractionService: InfractionService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    infractionService: InfractionService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.infractionService = infractionService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.WARN];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    // Require a reason for warnings
    if (!this.requireArgs(ctx, 'a reason for the warning')) {
      return;
    }

    const target = ctx.targetPlayer!;
    const reason = ctx.args!.trim();

    try {
      // Issue the warning
      const result = await this.infractionService.warn(
        ctx.player,
        target,
        reason,
        ctx.record
      );

      // Update the record
      ctx.record.recordMessage = reason;
      await this.logRecord(ctx);

      if (result.success) {
        await this.respondSuccess(ctx, result.message);

        this.logger.info({
          source: ctx.player.soldierName,
          target: target.soldierName,
          reason,
        }, 'Player warned');

      } else {
        await this.respondError(ctx, result.message);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to warn player');
      await this.respondError(ctx, `Failed to warn ${target.soldierName}`);
    }
  }
}

/**
 * Create and register the punish commands (punish, forgive, warn).
 */
export function registerPunishCommands(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  infractionService: InfractionService
): {
  punishCommand: PunishCommand;
  forgiveCommand: ForgiveCommand;
  warnCommand: WarnCommand;
} {
  const punishCommand = new PunishCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    infractionService
  );
  punishCommand.register();

  const forgiveCommand = new ForgiveCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    infractionService
  );
  forgiveCommand.register();

  const warnCommand = new WarnCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    infractionService
  );
  warnCommand.register();

  return {
    punishCommand,
    forgiveCommand,
    warnCommand,
  };
}
