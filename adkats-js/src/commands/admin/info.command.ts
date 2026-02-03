import type { RowDataPacket } from 'mysql2/promise';
import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { PlayerService } from '../../services/player.service.js';
import type { Database } from '../../database/connection.js';
import type { APlayer } from '../../models/player.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';
import { getPlayerKdr } from '../../models/player.js';

/**
 * Chat log entry from database.
 */
interface ChatLogRow extends RowDataPacket {
  logDate: Date;
  logMessage: string;
  logSubset: string;
}

/**
 * Player info command - shows detailed player information.
 * Usage: @pinfo <player>
 *
 * Displays GUID, IP (masked), playtime, infractions, and reputation.
 */
export class PInfoCommand extends BaseCommand {
  private playerService: PlayerService;
  private db: Database;
  private serverId: number;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    playerService: PlayerService,
    db: Database,
    serverId: number
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.playerService = playerService;
    this.db = db;
    this.serverId = serverId;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.PINFO];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target player
    if (!this.requireTarget(ctx)) {
      await this.respond(ctx, 'Usage: @pinfo <player>');
      return;
    }

    const target = ctx.targetPlayer!;

    try {
      // Get full player details from database
      const fullPlayer = await this.playerService.getPlayerById(target.playerId);
      if (!fullPlayer) {
        await this.respondError(ctx, `Could not load player info for ${target.soldierName}`);
        return;
      }

      // Get playtime from stats if available
      const playtime = await this.getPlayerPlaytime(target.playerId);

      // Format GUID (partial for privacy)
      const guidDisplay = this.maskGuid(fullPlayer.guid);

      // Format IP (masked for privacy)
      const ipDisplay = fullPlayer.ipAddress ? this.maskIp(fullPlayer.ipAddress) : 'Unknown';

      // Build response
      const lines: string[] = [
        `[Player Info] ${fullPlayer.soldierName}`,
        `GUID: ${guidDisplay}`,
        `IP: ${ipDisplay}`,
      ];

      if (playtime !== null) {
        lines.push(`Playtime: ${this.formatPlaytime(playtime)}`);
      }

      if (fullPlayer.firstSeen) {
        lines.push(`First seen: ${fullPlayer.firstSeen.toISOString().split('T')[0]}`);
      }

      // Add infraction info
      if (fullPlayer.infractions) {
        const inf = fullPlayer.infractions;
        lines.push(`Infractions: ${inf.serverTotalPoints} (P:${inf.serverPunishPoints} F:${inf.serverForgivePoints})`);
        if (inf.globalTotalPoints !== inf.serverTotalPoints) {
          lines.push(`Global: ${inf.globalTotalPoints} (P:${inf.globalPunishPoints} F:${inf.globalForgivePoints})`);
        }
      }

      // Add reputation if available
      if (fullPlayer.reputation !== null) {
        lines.push(`Reputation: ${fullPlayer.reputation.toFixed(1)}`);
      }

      // Send each line as a message
      for (const line of lines) {
        await this.respond(ctx, line);
      }

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
      }, 'Player info command executed');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to get player info');
      await this.respondError(ctx, `Failed to get info for ${target.soldierName}`);
    }
  }

  /**
   * Get player playtime in minutes from stats table.
   */
  private async getPlayerPlaytime(playerId: number): Promise<number | null> {
    const row = await this.db.queryOne<{ Playtime: number } & RowDataPacket>(
      `SELECT Playtime FROM tbl_playerstats WHERE StatsID = ?`,
      [playerId]
    );
    return row?.Playtime ?? null;
  }

  /**
   * Mask GUID for privacy (show first and last 4 characters).
   */
  private maskGuid(guid: string): string {
    if (guid.length <= 8) {
      return guid;
    }
    return `${guid.substring(0, 4)}...${guid.substring(guid.length - 4)}`;
  }

  /**
   * Mask IP address for privacy (show first two octets).
   */
  private maskIp(ip: string): string {
    const parts = ip.split('.');
    if (parts.length === 4) {
      return `${parts[0]}.${parts[1]}.*.*`;
    }
    return ip.substring(0, Math.min(ip.length, 8)) + '...';
  }

  /**
   * Format playtime from minutes to human-readable string.
   */
  private formatPlaytime(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours > 0) {
      return `${hours}h ${mins}m`;
    }
    return `${mins}m`;
  }
}

/**
 * Player stats command - shows kill/death stats for a player.
 * Usage: @pstats [player]
 *
 * If no player specified, shows stats for the command issuer.
 */
export class PStatsCommand extends BaseCommand {
  private playerService: PlayerService;
  private db: Database;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    playerService: PlayerService,
    db: Database
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.playerService = playerService;
    this.db = db;
  }

  getCommandKeys(): string[] {
    // Note: Using PINFO key as secondary; you may want to add a PSTATS key
    return ['player_stats'];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Use target player if specified, otherwise use the command issuer
    const target = ctx.targetPlayer ?? ctx.player;

    try {
      // Get current round stats (from in-memory)
      const roundKills = target.kills;
      const roundDeaths = target.deaths;
      const roundKdr = getPlayerKdr(target);

      // Get all-time stats from database
      const allTimeStats = await this.getAllTimeStats(target.playerId);

      // Build response
      const lines: string[] = [
        `[Stats] ${target.soldierName}`,
        `Round: ${roundKills}K / ${roundDeaths}D (KDR: ${roundKdr.toFixed(2)})`,
      ];

      if (allTimeStats) {
        const allTimeKdr = allTimeStats.deaths > 0
          ? allTimeStats.kills / allTimeStats.deaths
          : allTimeStats.kills;
        lines.push(`All-time: ${allTimeStats.kills}K / ${allTimeStats.deaths}D (KDR: ${allTimeKdr.toFixed(2)})`);
        lines.push(`Score: ${allTimeStats.score} | Headshots: ${allTimeStats.headshots}`);
      }

      // Send each line as a message
      for (const line of lines) {
        await this.respond(ctx, line);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to get player stats');
      await this.respondError(ctx, `Failed to get stats for ${target.soldierName}`);
    }
  }

  /**
   * Get all-time stats from the database.
   */
  private async getAllTimeStats(playerId: number): Promise<{
    kills: number;
    deaths: number;
    score: number;
    headshots: number;
  } | null> {
    const row = await this.db.queryOne<{
      Kills: number;
      Deaths: number;
      Score: number;
      Headshots: number;
    } & RowDataPacket>(
      `SELECT Kills, Deaths, Score, Headshots FROM tbl_playerstats WHERE StatsID = ?`,
      [playerId]
    );

    if (!row) {
      return null;
    }

    return {
      kills: row.Kills,
      deaths: row.Deaths,
      score: row.Score,
      headshots: row.Headshots,
    };
  }
}

/**
 * Lookup command - search for players in the database by name.
 * Usage: @lookup <name>
 *
 * Searches the database for players matching the name (partial match).
 */
export class LookupCommand extends BaseCommand {
  private playerService: PlayerService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    playerService: PlayerService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.playerService = playerService;
  }

  getCommandKeys(): string[] {
    // Add a lookup command key if not in CommandKeys
    return ['player_lookup'];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require search term
    if (!this.requireArgs(ctx, 'a player name to search for')) {
      await this.respond(ctx, 'Usage: @lookup <name>');
      return;
    }

    const searchTerm = ctx.args!.trim();

    try {
      // Search the database for matching players
      const players = await this.playerService.searchPlayers(searchTerm, 10);

      if (players.length === 0) {
        await this.respond(ctx, `No players found matching "${searchTerm}"`);
        return;
      }

      await this.respond(ctx, `[Lookup] Found ${players.length} player(s):`);

      for (const player of players) {
        const isOnline = this.playerService.isPlayerOnline(player.soldierName);
        const status = isOnline ? '[ONLINE]' : '';
        await this.respond(ctx, `${player.soldierName} ${status}`);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, searchTerm }, 'Failed to lookup players');
      await this.respondError(ctx, 'Failed to search for players');
    }
  }
}

/**
 * Find command - find online players matching a name.
 * Usage: @find <name>
 *
 * Searches currently online players for matches.
 */
export class FindCommand extends BaseCommand {
  private playerService: PlayerService;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    playerService: PlayerService
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.playerService = playerService;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.FIND];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require search term
    if (!this.requireArgs(ctx, 'a player name to search for')) {
      await this.respond(ctx, 'Usage: @find <name>');
      return;
    }

    const searchTerm = ctx.args!.trim().toLowerCase();

    try {
      // Search online players
      const result = this.playerService.findOnlinePlayerByPartialName(searchTerm);

      if (result === null) {
        await this.respond(ctx, `No online players found matching "${searchTerm}"`);
        return;
      }

      // Single match
      if (!Array.isArray(result)) {
        await this.respond(ctx, `[Found] ${result.soldierName} - Team ${result.teamId}, Squad ${result.squadId}`);
        return;
      }

      // Multiple matches
      await this.respond(ctx, `[Find] Found ${result.length} player(s):`);
      for (const player of result.slice(0, 10)) {
        await this.respond(ctx, `${player.soldierName} - Team ${player.teamId}`);
      }

      if (result.length > 10) {
        await this.respond(ctx, `... and ${result.length - 10} more`);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, searchTerm }, 'Failed to find players');
      await this.respondError(ctx, 'Failed to find players');
    }
  }
}

/**
 * Chat command - show a player's recent chat history.
 * Usage: @chat <player> [count]
 *
 * Shows the last N chat messages from a player (default 5).
 */
export class ChatCommand extends BaseCommand {
  private db: Database;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    db: Database
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.db = db;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.PCHAT];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target player
    if (!this.requireTarget(ctx)) {
      await this.respond(ctx, 'Usage: @chat <player> [count]');
      return;
    }

    const target = ctx.targetPlayer!;

    // Parse optional count from args
    let count = 5;
    if (ctx.args) {
      const parsed = parseInt(ctx.args.trim(), 10);
      if (!isNaN(parsed) && parsed > 0) {
        count = Math.min(parsed, 20); // Cap at 20 messages
      }
    }

    try {
      // Get recent chat from database
      const chatLogs = await this.db.query<ChatLogRow[]>(
        `SELECT logDate, logMessage, logSubset
         FROM tbl_chatlog
         WHERE logPlayerID = ?
         ORDER BY logDate DESC
         LIMIT ?`,
        [target.playerId, count]
      );

      if (chatLogs.length === 0) {
        await this.respond(ctx, `No chat history found for ${target.soldierName}`);
        return;
      }

      await this.respond(ctx, `[Chat] Last ${chatLogs.length} messages from ${target.soldierName}:`);

      for (const log of chatLogs.reverse()) {
        const time = log.logDate.toISOString().split('T')[1]?.substring(0, 8) ?? '';
        const subset = log.logSubset ? `[${log.logSubset}]` : '';
        await this.respond(ctx, `${time} ${subset}: ${log.logMessage}`);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to get chat history');
      await this.respondError(ctx, `Failed to get chat history for ${target.soldierName}`);
    }
  }
}

/**
 * Dependencies required for info commands.
 */
export interface InfoCommandDependencies {
  logger: Logger;
  bcAdapter: BattleConAdapter;
  commandService: CommandService;
  recordRepo: RecordRepository;
  playerService: PlayerService;
  db: Database;
  serverId: number;
}

/**
 * Create and register all player info commands.
 */
export function registerInfoCommands(deps: InfoCommandDependencies): {
  pinfo: PInfoCommand;
  pstats: PStatsCommand;
  lookup: LookupCommand;
  find: FindCommand;
  chat: ChatCommand;
} {
  const { logger, bcAdapter, commandService, recordRepo, playerService, db, serverId } = deps;

  const pinfo = new PInfoCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    playerService,
    db,
    serverId
  );
  pinfo.register();

  const pstats = new PStatsCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    playerService,
    db
  );
  pstats.register();

  const lookup = new LookupCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    playerService
  );
  lookup.register();

  const find = new FindCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    playerService
  );
  find.register();

  const chat = new ChatCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    db
  );
  chat.register();

  return { pinfo, pstats, lookup, find, chat };
}
