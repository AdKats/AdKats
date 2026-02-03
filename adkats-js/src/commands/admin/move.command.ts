import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { TeamService } from '../../services/team.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Move command - moves a player to the opposite team on their next death.
 * Usage: @move <player> [squad]
 */
export class MoveCommand extends BaseCommand {
  private teamService: TeamService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    teamService: TeamService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.teamService = teamService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.MOVE];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;
    const squadId = this.teamService.parseSquadArg(ctx.args);
    const targetTeamId = this.teamService.getOpposingTeam(target);

    // Check if already on target team
    if (target.teamId === targetTeamId) {
      await this.respondError(ctx, `${target.soldierName} is already on the opposite team`);
      return;
    }

    // Queue the move
    const result = this.teamService.queueMove(
      ctx.player,
      target,
      targetTeamId,
      squadId,
      ctx.record.recordMessage || 'Admin move'
    );

    if (!result.success) {
      await this.respondError(ctx, result.message);
      return;
    }

    // Update record message
    ctx.record.recordMessage = `Move to Team ${targetTeamId}${squadId > 0 ? ` Squad ${this.teamService.getSquadName(squadId)}` : ''}`;

    // Log the action
    await this.logRecord(ctx);

    // Respond to admin
    const squadInfo = squadId > 0 ? ` (${this.teamService.getSquadName(squadId)})` : '';
    await this.respond(
      ctx,
      `${target.soldierName} will be moved to Team ${targetTeamId}${squadInfo} on death`
    );

    // Notify target
    await this.bcAdapter.sayPlayer(
      `You will be moved to Team ${targetTeamId}${squadInfo} on your next death.`,
      target.soldierName
    );

    this.logger.info({
      admin: ctx.player.soldierName,
      target: target.soldierName,
      targetTeamId,
      squadId,
    }, 'Queued player move on death');
  }
}

/**
 * Force Move command - immediately kills and moves a player to the opposite team.
 * Usage: @fmove <player> [squad]
 */
export class ForceMoveCommand extends BaseCommand {
  private teamService: TeamService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    teamService: TeamService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.teamService = teamService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.FMOVE];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;
    const squadId = this.teamService.parseSquadArg(ctx.args);
    const targetTeamId = this.teamService.getOpposingTeam(target);

    // Execute the force move
    const result = await this.teamService.forceMove(
      ctx.player,
      target,
      targetTeamId,
      squadId
    );

    if (!result.success) {
      await this.respondError(ctx, result.message);
      return;
    }

    // Update record message
    ctx.record.recordMessage = `Force moved to Team ${targetTeamId}${squadId > 0 ? ` Squad ${this.teamService.getSquadName(squadId)}` : ''}`;

    // Log the action
    await this.logRecord(ctx);

    // Respond to admin
    const squadInfo = squadId > 0 ? ` (${this.teamService.getSquadName(squadId)})` : '';
    await this.respond(ctx, `Force moved ${target.soldierName} to Team ${targetTeamId}${squadInfo}`);

    // Announce to server
    await this.bcAdapter.say(`${target.soldierName} was moved to Team ${targetTeamId} by admin.`);

    this.logger.info({
      admin: ctx.player.soldierName,
      target: target.soldierName,
      targetTeamId,
      squadId,
    }, 'Force moved player');
  }
}

/**
 * Pull command - pulls a player to the admin's squad.
 * Usage: @pull <player>
 */
export class PullCommand extends BaseCommand {
  private teamService: TeamService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    teamService: TeamService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.teamService = teamService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.PULL];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;

    // Can't pull yourself
    if (ctx.player.soldierName === target.soldierName) {
      await this.respondError(ctx, 'You cannot pull yourself');
      return;
    }

    // Execute the pull
    const result = await this.teamService.pullPlayer(ctx.player, target);

    if (!result.success) {
      await this.respondError(ctx, result.message);
      return;
    }

    // Update record message
    const squadName = this.teamService.getSquadName(ctx.player.squadId);
    ctx.record.recordMessage = `Pulled to Team ${ctx.player.teamId} ${squadName}`;

    // Log the action
    await this.logRecord(ctx);

    // Respond to admin
    await this.respond(ctx, `Pulled ${target.soldierName} to your squad (${squadName})`);

    // Notify target
    await this.bcAdapter.sayPlayer(
      `You have been pulled to ${ctx.player.soldierName}'s squad.`,
      target.soldierName
    );

    this.logger.info({
      admin: ctx.player.soldierName,
      target: target.soldierName,
      teamId: ctx.player.teamId,
      squadId: ctx.player.squadId,
    }, 'Pulled player to admin squad');
  }
}

/**
 * Create and register all admin move commands.
 */
export function registerMoveCommands(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  teamService: TeamService
): { move: MoveCommand; fmove: ForceMoveCommand; pull: PullCommand } {
  const move = new MoveCommand(logger, bcAdapter, commandService, recordRepo, teamService);
  const fmove = new ForceMoveCommand(logger, bcAdapter, commandService, recordRepo, teamService);
  const pull = new PullCommand(logger, bcAdapter, commandService, recordRepo, teamService);

  move.register();
  fmove.register();
  pull.register();

  return { move, fmove, pull };
}
