import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { BanService } from '../../services/ban.service.js';
import type { PlayerService } from '../../services/player.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';
import { parseDuration, formatDuration } from '../../utils/time.js';
import { isBanPermanent, getBanDurationString } from '../../models/ban.js';

/**
 * Dependencies shared by all ban commands.
 */
interface BanCommandDependencies {
  logger: Logger;
  bcAdapter: BattleConAdapter;
  commandService: CommandService;
  recordRepo: RecordRepository;
  banService: BanService;
  playerService: PlayerService;
}

/**
 * Permanent ban command - permanently bans a player from the server.
 * Usage: @ban <player> [reason]
 */
export class BanCommand extends BaseCommand {
  private banService: BanService;

  constructor(deps: BanCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.banService = deps.banService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.BAN_PERM];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;
    const reason = ctx.args?.trim() || 'Permanently banned by admin';

    try {
      // Create the permanent ban (null duration = permanent)
      const ban = await this.banService.banPlayer(
        target,
        ctx.player,
        null, // Permanent
        reason,
        {
          enforceGuid: true,
          enforceIp: false,
          enforceName: false,
        },
        ctx.record
      );

      // Update the record with the ban info
      ctx.record.recordMessage = reason;
      ctx.record.commandNumeric = ban.banId;
      await this.logRecord(ctx);

      // Respond to source
      await this.respond(ctx, `Permanently banned ${target.soldierName}: ${reason}`);

      // Announce to server
      await this.bcAdapter.say(`${target.soldierName} was permanently banned: ${reason}`);

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        banId: ban.banId,
        reason,
      }, 'Player permanently banned');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to ban player');
      await this.respondError(ctx, `Failed to ban ${target.soldierName}`);
    }
  }
}

/**
 * Temporary ban command - temporarily bans a player for a specified duration.
 * Usage: @tban <player> <duration> [reason]
 * Duration examples: 5m, 1h, 2d, 1w, 1mo
 */
export class TempBanCommand extends BaseCommand {
  private banService: BanService;

  constructor(deps: BanCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.banService = deps.banService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.BAN_TEMP];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target
    if (!this.requireTarget(ctx)) {
      return;
    }

    // Require arguments (duration and optionally reason)
    if (!this.requireArgs(ctx, 'duration')) {
      await this.respond(ctx, 'Usage: @tban <player> <duration> [reason]');
      await this.respond(ctx, 'Duration examples: 5m, 1h, 2d, 1w, 1mo');
      return;
    }

    const target = ctx.targetPlayer!;
    const args = ctx.args!.trim();

    // Parse duration from the first argument
    const parts = args.split(/\s+/);
    const durationStr = parts[0]!;
    const reason = parts.slice(1).join(' ') || 'Temporarily banned by admin';

    // Parse the duration
    const durationMinutes = parseDuration(durationStr);

    if (durationMinutes === undefined) {
      await this.respondError(ctx, `Invalid duration: ${durationStr}`);
      await this.respond(ctx, 'Duration examples: 5m, 1h, 2d, 1w, 1mo');
      return;
    }

    // null means permanent, which is not allowed for temp ban
    if (durationMinutes === null) {
      await this.respondError(ctx, 'Use @ban for permanent bans, not @tban');
      return;
    }

    // Minimum 1 minute
    if (durationMinutes < 1) {
      await this.respondError(ctx, 'Ban duration must be at least 1 minute');
      return;
    }

    try {
      // Create the temporary ban
      const ban = await this.banService.banPlayer(
        target,
        ctx.player,
        durationMinutes,
        reason,
        {
          enforceGuid: true,
          enforceIp: false,
          enforceName: false,
        },
        ctx.record
      );

      // Update the record with the ban info
      ctx.record.recordMessage = reason;
      ctx.record.commandNumeric = Math.round(durationMinutes);
      await this.logRecord(ctx);

      // Format the duration for display
      const durationDisplay = formatDuration(durationMinutes);

      // Respond to source
      await this.respond(ctx, `Banned ${target.soldierName} for ${durationDisplay}: ${reason}`);

      // Announce to server
      await this.bcAdapter.say(`${target.soldierName} was banned for ${durationDisplay}: ${reason}`);

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        banId: ban.banId,
        durationMinutes,
        reason,
      }, 'Player temporarily banned');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to temp ban player');
      await this.respondError(ctx, `Failed to ban ${target.soldierName}`);
    }
  }
}

/**
 * Unban command - removes a ban from a player.
 * Usage: @unban <player|banid>
 * Can specify either a player name or a ban ID directly.
 */
export class UnbanCommand extends BaseCommand {
  private banService: BanService;
  private playerService: PlayerService;

  constructor(deps: BanCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.banService = deps.banService;
    this.playerService = deps.playerService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.UNBAN];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // We need either a target player or arguments
    if (!ctx.targetPlayer && !ctx.args) {
      await this.respondError(ctx, 'Usage: @unban <player|banid>');
      return;
    }

    try {
      let ban = null;
      let targetName = '';

      // Try to find the ban
      if (ctx.targetPlayer) {
        // Target is a known player - find their ban
        ban = await this.banService.getActiveBan(ctx.targetPlayer.playerId);
        if (!ban) {
          ban = await this.banService.getBanByPlayerId(ctx.targetPlayer.playerId);
        }
        targetName = ctx.targetPlayer.soldierName;
      } else if (ctx.args) {
        const arg = ctx.args.trim();

        // Check if the argument is a numeric ban ID
        const banId = parseInt(arg, 10);
        if (!isNaN(banId) && banId > 0) {
          ban = await this.banService.getBanById(banId);
          if (ban) {
            targetName = `Ban #${banId}`;
          }
        }

        // If not found by ID, try searching by player name
        if (!ban) {
          const searchResults = await this.banService.searchBans(arg, 1);
          if (searchResults.length > 0) {
            ban = searchResults[0]!;
            targetName = arg;
          }
        }

        // Also try searching for a player in the database
        if (!ban) {
          const players = await this.playerService.searchPlayers(arg, 1);
          if (players.length > 0) {
            const foundPlayer = players[0]!;
            ban = await this.banService.getBanByPlayerId(foundPlayer.playerId);
            if (ban) {
              targetName = foundPlayer.soldierName;
            }
          }
        }
      }

      if (!ban) {
        await this.respondError(ctx, 'No ban found for the specified player or ban ID');
        return;
      }

      // Check if ban is already inactive
      if (ban.banStatus !== 'Active') {
        await this.respondError(ctx, `Ban #${ban.banId} is already ${ban.banStatus.toLowerCase()}`);
        return;
      }

      // Remove the ban
      await this.banService.unbanPlayer(ban, ctx.player, ctx.record);

      // Log the record
      ctx.record.recordMessage = `Unbanned: ${ban.banNotes}`;
      ctx.record.targetId = ban.playerId;
      await this.logRecord(ctx);

      // Get ban info for response
      const banInfo = isBanPermanent(ban) ? 'Permanent' : getBanDurationString(ban);

      // Respond to source
      await this.respond(ctx, `Unbanned ${targetName} (Ban #${ban.banId}, was: ${banInfo})`);

      this.logger.info({
        source: ctx.player.soldierName,
        banId: ban.banId,
        playerId: ban.playerId,
        originalReason: ban.banNotes,
      }, 'Player unbanned');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to unban player');
      await this.respondError(ctx, 'Failed to unban player');
    }
  }
}

/**
 * Create and register all ban commands.
 */
export function registerBanCommands(deps: BanCommandDependencies): {
  banCommand: BanCommand;
  tempBanCommand: TempBanCommand;
  unbanCommand: UnbanCommand;
} {
  const banCommand = new BanCommand(deps);
  const tempBanCommand = new TempBanCommand(deps);
  const unbanCommand = new UnbanCommand(deps);

  banCommand.register();
  tempBanCommand.register();
  unbanCommand.register();

  return { banCommand, tempBanCommand, unbanCommand };
}
