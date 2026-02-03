import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { PlayerService } from '../../services/player.service.js';
import type { TeamService } from '../../services/team.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';
import { TeamIds } from '../../models/team.js';

/**
 * Dependencies shared by all nuke commands.
 */
interface NukeCommandDependencies {
  logger: Logger;
  bcAdapter: BattleConAdapter;
  commandService: CommandService;
  recordRepo: RecordRepository;
  playerService: PlayerService;
  teamService: TeamService;
}

/**
 * Nuke command - kills all players on a specified team.
 * Usage: @nuke [team] or @nuke (prompts for team)
 *
 * Team can be specified as:
 * - 1, 2 (team IDs)
 * - "us", "ru", "cn", etc. (faction names)
 * - "winning", "losing" (relative to current scores)
 */
export class NukeCommand extends BaseCommand {
  private playerService: PlayerService;
  private teamService: TeamService;

  constructor(deps: NukeCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.playerService = deps.playerService;
    this.teamService = deps.teamService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.NUKE];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Parse team argument
    const teamId = this.parseTeamArg(ctx.args);

    if (teamId === null) {
      await this.respondError(ctx, 'Invalid team. Use: @nuke 1, @nuke 2, or @nuke winning/losing');
      return;
    }

    // Get all players on the specified team
    const players = this.playerService.getAllOnlinePlayers()
      .filter((p) => p.teamId === teamId && p.isAlive);

    if (players.length === 0) {
      await this.respondError(ctx, `No alive players found on Team ${teamId}`);
      return;
    }

    try {
      // Kill all players on the team
      let killCount = 0;
      const errors: string[] = [];

      for (const player of players) {
        try {
          await this.bcAdapter.killPlayer(player.soldierName);
          player.isAlive = false;
          killCount++;
        } catch (error) {
          const msg = error instanceof Error ? error.message : String(error);
          errors.push(`${player.soldierName}: ${msg}`);
        }
      }

      // Log the action
      ctx.record.recordMessage = `Nuked Team ${teamId} (${killCount} players)`;
      await this.logRecord(ctx);

      // Announce to server
      await this.bcAdapter.say(`NUKE! Team ${teamId} has been nuked by ${ctx.player.soldierName}`);
      await this.bcAdapter.yell(`NUKE! Team ${teamId} nuked!`, 5);

      // Respond to admin
      if (errors.length > 0) {
        await this.respond(ctx, `Nuked ${killCount}/${players.length} players on Team ${teamId}. ${errors.length} errors.`);
      } else {
        await this.respondSuccess(ctx, `Nuked ${killCount} players on Team ${teamId}`);
      }

      this.logger.info({
        admin: ctx.player.soldierName,
        teamId,
        killCount,
        totalPlayers: players.length,
        errors: errors.length,
      }, 'Team nuked');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, teamId }, 'Failed to nuke team');
      await this.respondError(ctx, 'Failed to nuke team');
    }
  }

  /**
   * Parse team argument from user input.
   * Returns team ID (1 or 2) or null if invalid.
   */
  private parseTeamArg(arg: string | null): number | null {
    if (!arg || arg.trim() === '') {
      // Default to team 1 if no argument
      return null;
    }

    const lower = arg.toLowerCase().trim();

    // Numeric team ID
    const num = parseInt(lower, 10);
    if (!isNaN(num) && (num === TeamIds.TEAM1 || num === TeamIds.TEAM2)) {
      return num;
    }

    // Relative team (winning/losing)
    if (lower === 'winning' || lower === 'win') {
      return this.teamService.getWinningTeam();
    }
    if (lower === 'losing' || lower === 'lose') {
      const winning = this.teamService.getWinningTeam();
      if (winning === TeamIds.TEAM1) return TeamIds.TEAM2;
      if (winning === TeamIds.TEAM2) return TeamIds.TEAM1;
      return null;
    }

    // Common faction names - map to team IDs
    const team1Names = ['us', 'usa', 'usmc', 'marines', 'americans', 'american'];
    const team2Names = ['ru', 'rus', 'russia', 'russian', 'russians', 'cn', 'china', 'chinese', 'pla'];

    if (team1Names.includes(lower)) {
      return TeamIds.TEAM1;
    }
    if (team2Names.includes(lower)) {
      return TeamIds.TEAM2;
    }

    return null;
  }
}

/**
 * Nuke Winning Team command - kills all players on the winning team.
 * Usage: @nukewin or @nuke winning
 */
export class NukeWinningCommand extends BaseCommand {
  private playerService: PlayerService;
  private teamService: TeamService;

  constructor(deps: NukeCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.playerService = deps.playerService;
    this.teamService = deps.teamService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.NUKE_WINNING];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Get the winning team
    const winningTeamId = this.teamService.getWinningTeam();

    if (winningTeamId === null) {
      await this.respondError(ctx, 'Teams are currently tied - no winning team to nuke');
      return;
    }

    // Get all players on the winning team
    const players = this.playerService.getAllOnlinePlayers()
      .filter((p) => p.teamId === winningTeamId && p.isAlive);

    if (players.length === 0) {
      await this.respondError(ctx, `No alive players found on the winning team (Team ${winningTeamId})`);
      return;
    }

    try {
      // Kill all players on the winning team
      let killCount = 0;

      for (const player of players) {
        try {
          await this.bcAdapter.killPlayer(player.soldierName);
          player.isAlive = false;
          killCount++;
        } catch {
          // Continue with other players even if one fails
        }
      }

      // Log the action
      ctx.record.recordMessage = `Nuked winning Team ${winningTeamId} (${killCount} players)`;
      await this.logRecord(ctx);

      // Announce to server
      await this.bcAdapter.say(`NUKE! The winning team (Team ${winningTeamId}) has been nuked!`);
      await this.bcAdapter.yell(`NUKE! Winning team nuked!`, 5);

      // Respond to admin
      await this.respondSuccess(ctx, `Nuked ${killCount} players on winning Team ${winningTeamId}`);

      this.logger.info({
        admin: ctx.player.soldierName,
        teamId: winningTeamId,
        killCount,
      }, 'Winning team nuked');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to nuke winning team');
      await this.respondError(ctx, 'Failed to nuke winning team');
    }
  }
}

/**
 * Kick All command - kicks all players from the server.
 * Usage: @kickall [reason]
 *
 * This is a dangerous command that requires confirmation.
 */
export class KickAllCommand extends BaseCommand {
  private playerService: PlayerService;

  constructor(deps: NukeCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.playerService = deps.playerService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.KICKALL];
  }

  async execute(ctx: CommandContext): Promise<void> {
    const reason = ctx.args?.trim() || 'Server maintenance';

    // Get all online players except the admin issuing the command
    const players = this.playerService.getAllOnlinePlayers()
      .filter((p) => p.soldierName !== ctx.player.soldierName);

    if (players.length === 0) {
      await this.respondError(ctx, 'No other players to kick');
      return;
    }

    // Check if this needs confirmation
    if (!ctx.record.isConfirmed) {
      // Request confirmation
      await this.respond(ctx, `WARNING: This will kick ${players.length} players from the server!`);
      await this.respond(ctx, `Reason: ${reason}`);
      await this.respond(ctx, `Type 'yes' to confirm or 'no' to cancel`);
      this.commandService.setPendingConfirmation(ctx.player, ctx.record);
      return;
    }

    try {
      // Kick all players
      let kickCount = 0;
      const errors: string[] = [];

      for (const player of players) {
        try {
          await this.bcAdapter.kickPlayer(player.soldierName, reason);
          kickCount++;
        } catch (error) {
          const msg = error instanceof Error ? error.message : String(error);
          errors.push(`${player.soldierName}: ${msg}`);
        }
      }

      // Log the action
      ctx.record.recordMessage = `Kicked all players (${kickCount}): ${reason}`;
      await this.logRecord(ctx);

      // Respond to admin
      if (errors.length > 0) {
        await this.respond(ctx, `Kicked ${kickCount}/${players.length} players. ${errors.length} errors.`);
      } else {
        await this.respondSuccess(ctx, `Kicked ${kickCount} players from the server`);
      }

      this.logger.warn({
        admin: ctx.player.soldierName,
        kickCount,
        totalPlayers: players.length,
        reason,
        errors: errors.length,
      }, 'All players kicked from server');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to kick all players');
      await this.respondError(ctx, 'Failed to kick all players');
    }
  }
}

/**
 * Swap Nuke command - swaps all players to opposite teams (kills them in the process).
 * Usage: @swapnuke
 *
 * This effectively swaps the teams by moving everyone to the opposite side.
 */
export class SwapNukeCommand extends BaseCommand {
  private playerService: PlayerService;
  private teamService: TeamService;

  constructor(deps: NukeCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.playerService = deps.playerService;
    this.teamService = deps.teamService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.SWAPNUKE];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Get all online players
    const players = this.playerService.getAllOnlinePlayers()
      .filter((p) => p.teamId === TeamIds.TEAM1 || p.teamId === TeamIds.TEAM2);

    if (players.length === 0) {
      await this.respondError(ctx, 'No players to swap');
      return;
    }

    // Check if this needs confirmation
    if (!ctx.record.isConfirmed) {
      // Request confirmation
      await this.respond(ctx, `WARNING: This will swap all ${players.length} players to opposite teams!`);
      await this.respond(ctx, `Type 'yes' to confirm or 'no' to cancel`);
      this.commandService.setPendingConfirmation(ctx.player, ctx.record);
      return;
    }

    try {
      // Swap all players
      let swapCount = 0;
      const errors: string[] = [];

      for (const player of players) {
        try {
          const targetTeamId = player.teamId === TeamIds.TEAM1 ? TeamIds.TEAM2 : TeamIds.TEAM1;

          // Force move with kill
          await this.bcAdapter.movePlayer(player.soldierName, targetTeamId, 0, true);

          // Update local state
          player.teamId = targetTeamId;
          player.squadId = 0;
          player.isAlive = false;

          swapCount++;
        } catch (error) {
          const msg = error instanceof Error ? error.message : String(error);
          errors.push(`${player.soldierName}: ${msg}`);
        }
      }

      // Log the action
      ctx.record.recordMessage = `Swap nuked ${swapCount} players`;
      await this.logRecord(ctx);

      // Announce to server
      await this.bcAdapter.say(`SWAP NUKE! All teams have been swapped by ${ctx.player.soldierName}`);
      await this.bcAdapter.yell(`SWAP NUKE! Teams swapped!`, 5);

      // Respond to admin
      if (errors.length > 0) {
        await this.respond(ctx, `Swapped ${swapCount}/${players.length} players. ${errors.length} errors.`);
      } else {
        await this.respondSuccess(ctx, `Swapped ${swapCount} players to opposite teams`);
      }

      this.logger.info({
        admin: ctx.player.soldierName,
        swapCount,
        totalPlayers: players.length,
        errors: errors.length,
      }, 'Teams swap nuked');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to swap nuke');
      await this.respondError(ctx, 'Failed to swap teams');
    }
  }
}

/**
 * Create and register all nuke commands.
 */
export function registerNukeCommands(deps: NukeCommandDependencies): {
  nukeCommand: NukeCommand;
  nukeWinningCommand: NukeWinningCommand;
  kickAllCommand: KickAllCommand;
  swapNukeCommand: SwapNukeCommand;
} {
  const nukeCommand = new NukeCommand(deps);
  const nukeWinningCommand = new NukeWinningCommand(deps);
  const kickAllCommand = new KickAllCommand(deps);
  const swapNukeCommand = new SwapNukeCommand(deps);

  nukeCommand.register();
  nukeWinningCommand.register();
  kickAllCommand.register();
  swapNukeCommand.register();

  return { nukeCommand, nukeWinningCommand, kickAllCommand, swapNukeCommand };
}
