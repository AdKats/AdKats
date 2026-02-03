import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { ReportService } from '../../services/report.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Report command - allows players to report other players.
 * Usage: @report <player> [reason]
 */
export class ReportCommand extends BaseCommand {
  private reportService: ReportService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    reportService: ReportService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.reportService = reportService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.REPORT];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target player
    if (!this.requireTarget(ctx)) {
      await this.respond(ctx, 'Usage: @report <player> [reason]');
      return;
    }

    const target = ctx.targetPlayer!;
    const reason = ctx.args?.trim() || '';

    try {
      // Create the report
      const result = await this.reportService.createReport(
        ctx.player,
        target,
        reason,
        ctx.record.command
      );

      if (result.success) {
        await this.respondSuccess(ctx, result.message);

        // Also notify the reporter
        await this.respond(ctx, 'Your report has been sent to the admins.');
      } else {
        await this.respondError(ctx, result.message);
      }

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        reason,
        success: result.success,
      }, 'Report command executed');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to create report');
      await this.respondError(ctx, 'Failed to submit report');
    }
  }
}

/**
 * CallAdmin command - allows players to call for admin assistance.
 * Usage: @calladmin [reason]
 *
 * Unlike report, this doesn't target a specific player - it's a general
 * request for admin attention.
 */
export class CallAdminCommand extends BaseCommand {
  private reportService: ReportService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    reportService: ReportService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.reportService = reportService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.CALLADMIN];
  }

  async execute(ctx: CommandContext): Promise<void> {
    const reason = ctx.args?.trim() || 'Admin assistance requested';

    try {
      // For calladmin, the source player is also the target (they're the subject)
      const result = await this.reportService.createReport(
        ctx.player,
        ctx.player, // Target is self - this is a special case
        `[CALLADMIN] ${reason}`,
        ctx.record.command
      );

      // Note: createReport will reject self-reports, so we handle this differently
      // For calladmin, we directly notify admins without creating a report record

      // Send notification to server
      await this.bcAdapter.say(
        `[Admin Call] ${ctx.player.soldierName} is requesting admin assistance: ${reason}`
      );

      await this.respondSuccess(ctx, 'Your request has been sent to the admins.');

      this.logger.info({
        source: ctx.player.soldierName,
        reason,
      }, 'CallAdmin command executed');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to process calladmin');
      await this.respondError(ctx, 'Failed to call admin');
    }
  }
}

/**
 * Create and register the report command.
 */
export function registerReportCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  reportService: ReportService
): ReportCommand {
  const cmd = new ReportCommand(logger, bcAdapter, commandService, recordRepo, reportService);
  cmd.register();
  return cmd;
}

/**
 * Create and register the calladmin command.
 */
export function registerCallAdminCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  reportService: ReportService
): CallAdminCommand {
  const cmd = new CallAdminCommand(logger, bcAdapter, commandService, recordRepo, reportService);
  cmd.register();
  return cmd;
}

/**
 * Register all player report commands.
 */
export function registerReportCommands(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  reportService: ReportService
): { report: ReportCommand; callAdmin: CallAdminCommand } {
  return {
    report: registerReportCommand(logger, bcAdapter, commandService, recordRepo, reportService),
    callAdmin: registerCallAdminCommand(logger, bcAdapter, commandService, recordRepo, reportService),
  };
}
