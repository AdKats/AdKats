import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { AdKatsConfig } from '../../core/config.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys, isCommandVisible } from '../../models/command.js';

/**
 * Help command - displays available commands to the player.
 * Usage: @help [command]
 */
export class HelpCommand extends BaseCommand {
  private config: AdKatsConfig;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    config: AdKatsConfig
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.config = config;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.HELP];
  }

  async execute(ctx: CommandContext): Promise<void> {
    const searchTerm = ctx.args?.trim();

    try {
      if (searchTerm) {
        // Show help for a specific command
        await this.showCommandHelp(ctx, searchTerm);
      } else {
        // Show list of available commands
        await this.showCommandList(ctx);
      }

      // Log the request
      ctx.record.recordMessage = searchTerm || 'Requested command list';
      await this.logRecord(ctx);

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to show help');
      await this.respondError(ctx, 'Failed to display help');
    }
  }

  /**
   * Show list of available commands.
   */
  private async showCommandList(ctx: CommandContext): Promise<void> {
    const commands = await this.commandService.getAvailableCommands(ctx.player);
    const visibleCommands = commands.filter(isCommandVisible);

    if (visibleCommands.length === 0) {
      await this.respond(ctx, 'No commands available');
      return;
    }

    // Group commands by category (based on command key prefix)
    const categories: Map<string, string[]> = new Map();

    for (const cmd of visibleCommands) {
      const category = this.getCommandCategory(cmd.commandKey);
      if (!categories.has(category)) {
        categories.set(category, []);
      }
      categories.get(category)!.push(cmd.commandText);
    }

    // Send header
    await this.bcAdapter.sayPlayer(
      `=== Available Commands (prefix: ${this.config.commandPrefix}) ===`,
      ctx.player.soldierName
    );

    // Send commands by category
    for (const [category, cmds] of categories) {
      const cmdList = cmds.sort().join(', ');
      await this.bcAdapter.sayPlayer(`[${category}] ${cmdList}`, ctx.player.soldierName);
    }

    await this.bcAdapter.sayPlayer(
      `Type ${this.config.commandPrefix}help <command> for details`,
      ctx.player.soldierName
    );

    this.logger.debug({
      player: ctx.player.soldierName,
      commandCount: visibleCommands.length,
    }, 'Showed command list');
  }

  /**
   * Show help for a specific command.
   */
  private async showCommandHelp(ctx: CommandContext, searchTerm: string): Promise<void> {
    const command = this.commandService.findCommandFuzzy(searchTerm);

    if (!command) {
      await this.respond(ctx, `Unknown command: ${searchTerm}`);
      return;
    }

    // Check if player can use this command
    const canUse = await this.commandService['checkPermission'](ctx.player, command);

    const usageInfo = this.getCommandUsage(command.commandKey, command.commandText);

    await this.bcAdapter.sayPlayer(
      `=== ${command.commandName} ===`,
      ctx.player.soldierName
    );
    await this.bcAdapter.sayPlayer(
      `Usage: ${this.config.commandPrefix}${usageInfo}`,
      ctx.player.soldierName
    );

    if (!canUse) {
      await this.bcAdapter.sayPlayer(
        '(You do not have permission to use this command)',
        ctx.player.soldierName
      );
    }

    this.logger.debug({
      player: ctx.player.soldierName,
      command: command.commandText,
    }, 'Showed command help');
  }

  /**
   * Get command category from command key.
   */
  private getCommandCategory(commandKey: string): string {
    if (commandKey.startsWith('player_')) {
      return 'Player';
    }
    if (commandKey.startsWith('self_')) {
      return 'Self';
    }
    if (commandKey.startsWith('admin_')) {
      return 'Admin';
    }
    if (commandKey.startsWith('server_')) {
      return 'Server';
    }
    if (commandKey.startsWith('round_')) {
      return 'Round';
    }
    return 'Other';
  }

  /**
   * Get usage string for a command.
   */
  private getCommandUsage(commandKey: string, commandText: string): string {
    // Map command keys to usage strings
    const usageMap: Record<string, string> = {
      [CommandKeys.KILL]: `${commandText} <player> [reason]`,
      [CommandKeys.KICK]: `${commandText} <player> [reason]`,
      [CommandKeys.BAN_TEMP]: `${commandText} <player> <duration> [reason]`,
      [CommandKeys.BAN_PERM]: `${commandText} <player> [reason]`,
      [CommandKeys.PUNISH]: `${commandText} <player> [reason]`,
      [CommandKeys.FORGIVE]: `${commandText} <player> [count]`,
      [CommandKeys.MOVE]: `${commandText} <player>`,
      [CommandKeys.FMOVE]: `${commandText} <player>`,
      [CommandKeys.SAY]: `${commandText} <message>`,
      [CommandKeys.YELL]: `${commandText} <message>`,
      [CommandKeys.PLAYER_SAY]: `${commandText} <player> <message>`,
      [CommandKeys.PLAYER_YELL]: `${commandText} <player> <message>`,
      [CommandKeys.REPORT]: `${commandText} <player> [reason]`,
      [CommandKeys.CALLADMIN]: `${commandText} <player> [reason]`,
      [CommandKeys.RULES]: commandText,
      [CommandKeys.HELP]: `${commandText} [command]`,
      [CommandKeys.ADMINS]: commandText,
      [CommandKeys.REP]: commandText,
    };

    return usageMap[commandKey] ?? commandText;
  }
}

/**
 * Create and register the help command.
 */
export function registerHelpCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository,
  config: AdKatsConfig
): HelpCommand {
  const cmd = new HelpCommand(logger, bcAdapter, commandService, recordRepo, config);
  cmd.register();
  return cmd;
}
