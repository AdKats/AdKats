import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { ReportService } from '../../services/report.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Accept command - admin accepts a pending report.
 * Usage: @accept [reportId]
 *
 * If no reportId is provided, accepts the oldest pending report.
 */
export class AcceptCommand extends BaseCommand {
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
    return [CommandKeys.ACCEPT];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Parse optional report ID from args
    const reportId = this.parseReportId(ctx.args);

    try {
      const result = await this.reportService.acceptReport(
        ctx.player,
        reportId,
        ctx.record.command ?? undefined
      );

      if (result.success) {
        await this.respondSuccess(ctx, result.message);

        // Show target info to admin
        if (result.report) {
          const target = result.report.targetPlayer;
          await this.respond(
            ctx,
            `Target: ${target.soldierName} | K/D: ${target.kills}/${target.deaths} | Score: ${target.score}`
          );
        }
      } else {
        await this.respondError(ctx, result.message);
      }

      // Log the action
      ctx.record.recordMessage = `Accepted report${reportId ? ` #${reportId}` : ''}`;
      await this.logRecord(ctx);

      this.logger.info({
        admin: ctx.player.soldierName,
        reportId: result.report?.reportId,
        success: result.success,
      }, 'Accept command executed');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to accept report');
      await this.respondError(ctx, 'Failed to accept report');
    }
  }

  /**
   * Parse report ID from command arguments.
   */
  private parseReportId(args: string | null): number | undefined {
    if (!args || args.trim().length === 0) {
      return undefined;
    }

    const trimmed = args.trim();

    // Handle "#123" format
    const idStr = trimmed.startsWith('#') ? trimmed.slice(1) : trimmed;
    const id = parseInt(idStr, 10);

    return isNaN(id) ? undefined : id;
  }
}

/**
 * Deny command - admin denies a pending report.
 * Usage: @deny [reportId] [reason]
 *
 * If no reportId is provided, denies the oldest pending report.
 * The reporter is notified of the denial.
 */
export class DenyCommand extends BaseCommand {
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
    return [CommandKeys.DENY];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Parse report ID and reason from args
    const { reportId, reason } = this.parseArgs(ctx.args);

    try {
      const result = await this.reportService.denyReport(
        ctx.player,
        reportId,
        reason,
        ctx.record.command ?? undefined
      );

      if (result.success) {
        await this.respondSuccess(ctx, result.message);
      } else {
        await this.respondError(ctx, result.message);
      }

      // Log the action
      ctx.record.recordMessage = `Denied report${reportId ? ` #${reportId}` : ''}: ${reason || 'No reason'}`;
      await this.logRecord(ctx);

      this.logger.info({
        admin: ctx.player.soldierName,
        reportId: result.report?.reportId,
        reason,
        success: result.success,
      }, 'Deny command executed');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to deny report');
      await this.respondError(ctx, 'Failed to deny report');
    }
  }

  /**
   * Parse report ID and reason from command arguments.
   * Format: [reportId] [reason] or just [reason]
   */
  private parseArgs(args: string | null): { reportId: number | undefined; reason: string } {
    if (!args || args.trim().length === 0) {
      return { reportId: undefined, reason: '' };
    }

    const parts = args.trim().split(/\s+/);
    const firstPart = parts[0]!;

    // Check if first part is a report ID
    let idStr = firstPart;
    if (firstPart.startsWith('#')) {
      idStr = firstPart.slice(1);
    }

    const id = parseInt(idStr, 10);

    if (!isNaN(id) && id > 0) {
      // First part is a report ID
      return {
        reportId: id,
        reason: parts.slice(1).join(' '),
      };
    }

    // First part is not a report ID, treat entire args as reason
    return {
      reportId: undefined,
      reason: args.trim(),
    };
  }
}

/**
 * Ignore command - admin ignores a pending report without notifying reporter.
 * Usage: @ignore [reportId]
 *
 * If no reportId is provided, ignores the oldest pending report.
 * Unlike deny, the reporter is NOT notified.
 */
export class IgnoreCommand extends BaseCommand {
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
    return [CommandKeys.IGNORE];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Parse optional report ID from args
    const reportId = this.parseReportId(ctx.args);

    try {
      const result = await this.reportService.ignoreReport(
        ctx.player,
        reportId,
        ctx.record.command ?? undefined
      );

      if (result.success) {
        await this.respondSuccess(ctx, result.message);
      } else {
        await this.respondError(ctx, result.message);
      }

      // Log the action
      ctx.record.recordMessage = `Ignored report${reportId ? ` #${reportId}` : ''}`;
      await this.logRecord(ctx);

      this.logger.info({
        admin: ctx.player.soldierName,
        reportId: result.report?.reportId,
        success: result.success,
      }, 'Ignore command executed');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to ignore report');
      await this.respondError(ctx, 'Failed to ignore report');
    }
  }

  /**
   * Parse report ID from command arguments.
   */
  private parseReportId(args: string | null): number | undefined {
    if (!args || args.trim().length === 0) {
      return undefined;
    }

    const trimmed = args.trim();

    // Handle "#123" format
    const idStr = trimmed.startsWith('#') ? trimmed.slice(1) : trimmed;
    const id = parseInt(idStr, 10);

    return isNaN(id) ? undefined : id;
  }
}

/**
 * Create and register the accept command.
 */
export function registerAcceptCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  reportService: ReportService
): AcceptCommand {
  const cmd = new AcceptCommand(logger, bcAdapter, commandService, recordRepo, reportService);
  cmd.register();
  return cmd;
}

/**
 * Create and register the deny command.
 */
export function registerDenyCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  reportService: ReportService
): DenyCommand {
  const cmd = new DenyCommand(logger, bcAdapter, commandService, recordRepo, reportService);
  cmd.register();
  return cmd;
}

/**
 * Create and register the ignore command.
 */
export function registerIgnoreCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  reportService: ReportService
): IgnoreCommand {
  const cmd = new IgnoreCommand(logger, bcAdapter, commandService, recordRepo, reportService);
  cmd.register();
  return cmd;
}

/**
 * Register all admin report commands.
 */
export function registerReportAdminCommands(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  reportService: ReportService
): { accept: AcceptCommand; deny: DenyCommand; ignore: IgnoreCommand } {
  return {
    accept: registerAcceptCommand(logger, bcAdapter, commandService, recordRepo, reportService),
    deny: registerDenyCommand(logger, bcAdapter, commandService, recordRepo, reportService),
    ignore: registerIgnoreCommand(logger, bcAdapter, commandService, recordRepo, reportService),
  };
}
