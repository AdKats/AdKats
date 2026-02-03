import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { TeamService } from '../../services/team.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';
import { TeamIds } from '../../models/team.js';

/**
 * Dependencies shared by all round commands.
 */
interface RoundCommandDependencies {
  logger: Logger;
  bcAdapter: BattleConAdapter;
  commandService: CommandService;
  recordRepo: RecordRepository;
  teamService: TeamService;
}

/**
 * Restart Round command - restarts the current round.
 * Usage: @restart
 *
 * This command restarts the round to the beginning with fresh scores.
 */
export class RestartRoundCommand extends BaseCommand {
  constructor(deps: RoundCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.RESTART];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Check if this needs confirmation
    if (!ctx.record.isConfirmed) {
      // Request confirmation for this potentially disruptive action
      await this.respond(ctx, 'WARNING: This will restart the current round!');
      await this.respond(ctx, 'All progress will be lost. Type "yes" to confirm or "no" to cancel.');
      this.commandService.setPendingConfirmation(ctx.player, ctx.record);
      return;
    }

    try {
      // Announce the restart before it happens
      await this.bcAdapter.say(`Round will be restarted by ${ctx.player.soldierName}`);
      await this.bcAdapter.yell('ROUND RESTARTING!', 5);

      // Small delay to let players see the message
      await this.delay(2000);

      // Restart the round
      await this.bcAdapter.restartRound();

      // Log the action
      ctx.record.recordMessage = 'Round restarted';
      await this.logRecord(ctx);

      // Respond to admin
      await this.respondSuccess(ctx, 'Round restarted');

      this.logger.info({
        admin: ctx.player.soldierName,
      }, 'Round restarted by admin');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to restart round');
      await this.respondError(ctx, 'Failed to restart round');
    }
  }

  /**
   * Delay execution for specified milliseconds.
   */
  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}

/**
 * Next Level command - skips to the next map in rotation.
 * Usage: @nextlevel or @nextmap
 */
export class NextLevelCommand extends BaseCommand {
  constructor(deps: RoundCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.NEXTLEVEL];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Check if this needs confirmation
    if (!ctx.record.isConfirmed) {
      // Request confirmation for this potentially disruptive action
      await this.respond(ctx, 'WARNING: This will skip to the next map in rotation!');
      await this.respond(ctx, 'Type "yes" to confirm or "no" to cancel.');
      this.commandService.setPendingConfirmation(ctx.player, ctx.record);
      return;
    }

    try {
      // Announce the map change before it happens
      await this.bcAdapter.say(`Skipping to next map - requested by ${ctx.player.soldierName}`);
      await this.bcAdapter.yell('LOADING NEXT MAP!', 5);

      // Small delay to let players see the message
      await this.delay(2000);

      // Skip to next level
      await this.bcAdapter.nextLevel();

      // Log the action
      ctx.record.recordMessage = 'Skipped to next level';
      await this.logRecord(ctx);

      // Respond to admin
      await this.respondSuccess(ctx, 'Skipping to next level');

      this.logger.info({
        admin: ctx.player.soldierName,
      }, 'Next level command executed');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to skip to next level');
      await this.respondError(ctx, 'Failed to skip to next level');
    }
  }

  /**
   * Delay execution for specified milliseconds.
   */
  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}

/**
 * Round End command - ends the current round with a specified winner.
 * Usage: @roundend [team] or @endround [team]
 *
 * Team can be specified as:
 * - 1, 2 (team IDs)
 * - "winning" (the currently winning team wins)
 * - "losing" (the currently losing team wins - upset!)
 */
export class RoundEndCommand extends BaseCommand {
  private teamService: TeamService;

  constructor(deps: RoundCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.teamService = deps.teamService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.ENDROUND];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Parse team argument
    const winningTeamId = this.parseTeamArg(ctx.args);

    if (winningTeamId === null) {
      await this.respondError(ctx, 'Invalid team. Use: @endround 1, @endround 2, or @endround winning/losing');
      return;
    }

    // Check if this needs confirmation
    if (!ctx.record.isConfirmed) {
      // Request confirmation for this action
      await this.respond(ctx, `WARNING: This will end the round with Team ${winningTeamId} as the winner!`);
      await this.respond(ctx, 'Type "yes" to confirm or "no" to cancel.');
      this.commandService.setPendingConfirmation(ctx.player, ctx.record);
      return;
    }

    try {
      // Announce the round end
      await this.bcAdapter.say(`Round ending - Team ${winningTeamId} wins! (Admin: ${ctx.player.soldierName})`);
      await this.bcAdapter.yell(`ROUND OVER! Team ${winningTeamId} WINS!`, 5);

      // Small delay to let players see the message
      await this.delay(2000);

      // End the round with the specified winner
      await this.bcAdapter.endRound(winningTeamId);

      // Log the action
      ctx.record.recordMessage = `Round ended - Team ${winningTeamId} declared winner`;
      await this.logRecord(ctx);

      // Respond to admin
      await this.respondSuccess(ctx, `Round ended with Team ${winningTeamId} as winner`);

      this.logger.info({
        admin: ctx.player.soldierName,
        winningTeamId,
      }, 'Round ended by admin');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, winningTeamId }, 'Failed to end round');
      await this.respondError(ctx, 'Failed to end round');
    }
  }

  /**
   * Parse team argument from user input.
   * Returns team ID (1 or 2) or null if invalid.
   */
  private parseTeamArg(arg: string | null): number | null {
    if (!arg || arg.trim() === '') {
      // Default to the currently winning team
      return this.teamService.getWinningTeam() ?? TeamIds.TEAM1;
    }

    const lower = arg.toLowerCase().trim();

    // Numeric team ID
    const num = parseInt(lower, 10);
    if (!isNaN(num) && (num === TeamIds.TEAM1 || num === TeamIds.TEAM2)) {
      return num;
    }

    // Relative team (winning/losing)
    if (lower === 'winning' || lower === 'win') {
      return this.teamService.getWinningTeam() ?? TeamIds.TEAM1;
    }
    if (lower === 'losing' || lower === 'lose') {
      const winning = this.teamService.getWinningTeam();
      if (winning === TeamIds.TEAM1) return TeamIds.TEAM2;
      if (winning === TeamIds.TEAM2) return TeamIds.TEAM1;
      return TeamIds.TEAM2; // Default if tied
    }

    // Common faction names - map to team IDs
    const team1Names = ['us', 'usa', 'usmc', 'marines'];
    const team2Names = ['ru', 'rus', 'russia', 'cn', 'china'];

    if (team1Names.includes(lower)) {
      return TeamIds.TEAM1;
    }
    if (team2Names.includes(lower)) {
      return TeamIds.TEAM2;
    }

    return null;
  }

  /**
   * Delay execution for specified milliseconds.
   */
  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}

/**
 * Create and register all round commands.
 */
export function registerRoundCommands(deps: RoundCommandDependencies): {
  restartRoundCommand: RestartRoundCommand;
  nextLevelCommand: NextLevelCommand;
  roundEndCommand: RoundEndCommand;
} {
  const restartRoundCommand = new RestartRoundCommand(deps);
  const nextLevelCommand = new NextLevelCommand(deps);
  const roundEndCommand = new RoundEndCommand(deps);

  restartRoundCommand.register();
  nextLevelCommand.register();
  roundEndCommand.register();

  return { restartRoundCommand, nextLevelCommand, roundEndCommand };
}
