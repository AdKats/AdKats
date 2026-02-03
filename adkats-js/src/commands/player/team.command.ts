import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { TeamService } from '../../services/team.service.js';
import type { PlayerService } from '../../services/player.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * MoveMe command - player requests to be moved to the other team.
 * Usage: @moveme
 */
export class MoveMeCommand extends BaseCommand {
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
    return [CommandKeys.TEAMSWAP];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Queue the player for team swap
    const result = this.teamService.queueTeamSwap(
      ctx.player,
      'Self-requested team swap'
    );

    if (!result.success) {
      await this.respondError(ctx, result.message);
      return;
    }

    // Update record message
    const targetTeamId = this.teamService.getOpposingTeam(ctx.player);
    ctx.record.recordMessage = `Requested swap to Team ${targetTeamId}`;
    ctx.record.targetId = ctx.player.playerId;
    ctx.record.targetName = ctx.player.soldierName;

    // Log the action
    await this.logRecord(ctx);

    // Respond to player
    await this.respond(ctx, result.message);

    // Show queue position
    const queueSize = this.teamService.getSwapQueueSize();
    if (queueSize > 1) {
      await this.respond(ctx, `Queue position: ${queueSize}`);
    }

    this.logger.info({
      player: ctx.player.soldierName,
      targetTeamId,
      queueSize,
    }, 'Player requested team swap');
  }
}

/**
 * Assist command - player moves to the weaker team.
 * Usage: @assist
 */
export class AssistCommand extends BaseCommand {
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
    return [CommandKeys.ASSIST];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Get team balance info
    const balance = this.teamService.getTeamBalance();

    // Check if teams are balanced
    if (balance.isBalanced) {
      await this.respond(ctx, 'Teams are currently balanced. No assistance needed.');
      return;
    }

    // Check if player is already on weak team
    const weakTeam = this.teamService.getWeakTeam();
    if (weakTeam === ctx.player.teamId) {
      await this.respond(ctx, 'You are already on the weaker team. Thank you for helping!');
      return;
    }

    // Attempt to assist
    const result = await this.teamService.assistTeam(ctx.player);

    if (!result.success) {
      await this.respondError(ctx, result.message);
      return;
    }

    // Update record message
    ctx.record.recordMessage = `Assisted Team ${weakTeam}`;
    ctx.record.targetId = ctx.player.playerId;
    ctx.record.targetName = ctx.player.soldierName;

    // Log the action
    await this.logRecord(ctx);

    // Respond to player
    await this.respond(ctx, result.message);

    // Announce to server (positive reinforcement)
    await this.bcAdapter.say(
      `${ctx.player.soldierName} volunteered to help balance the teams!`
    );

    this.logger.info({
      player: ctx.player.soldierName,
      targetTeamId: weakTeam,
      balance,
    }, 'Player assisted weak team');
  }
}

/**
 * Join command - player joins another player's squad.
 * Usage: @join <player>
 */
export class JoinCommand extends BaseCommand {
  private teamService: TeamService;
  private playerService: PlayerService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    teamService: TeamService,
    playerService: PlayerService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.teamService = teamService;
    this.playerService = playerService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.JOIN];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;

    // Can't join yourself
    if (ctx.player.soldierName === target.soldierName) {
      await this.respondError(ctx, 'You cannot join yourself');
      return;
    }

    // Check move limit
    if (!this.teamService.canPlayerMove(ctx.player)) {
      const remaining = this.teamService.getRemainingMoves(ctx.player);
      await this.respondError(
        ctx,
        `You have used all your moves this round. Remaining: ${remaining}`
      );
      return;
    }

    // Execute the join
    const result = await this.teamService.joinPlayer(ctx.player, target);

    if (!result.success) {
      await this.respondError(ctx, result.message);
      return;
    }

    // Update record message
    const squadName = this.teamService.getSquadName(target.squadId);
    ctx.record.recordMessage = `Joined ${target.soldierName}'s squad (${squadName})`;

    // Log the action
    await this.logRecord(ctx);

    // Respond to player
    await this.respond(ctx, result.message);

    // Notify target
    await this.bcAdapter.sayPlayer(
      `${ctx.player.soldierName} has joined your squad.`,
      target.soldierName
    );

    this.logger.info({
      player: ctx.player.soldierName,
      target: target.soldierName,
      teamId: target.teamId,
      squadId: target.squadId,
    }, 'Player joined another player squad');
  }
}

/**
 * Create and register all player team commands.
 */
export function registerTeamCommands(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  teamService: TeamService,
  playerService: PlayerService
): { moveme: MoveMeCommand; assist: AssistCommand; join: JoinCommand } {
  const moveme = new MoveMeCommand(logger, bcAdapter, commandService, recordRepo, teamService);
  const assist = new AssistCommand(logger, bcAdapter, commandService, recordRepo, teamService);
  const join = new JoinCommand(logger, bcAdapter, commandService, recordRepo, teamService, playerService);

  moveme.register();
  assist.register();
  join.register();

  return { moveme, assist, join };
}
