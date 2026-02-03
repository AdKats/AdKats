import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { SpecialPlayerService } from '../../services/specialplayer.service.js';
import type { PlayerService } from '../../services/player.service.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';
import { SpecialGroupKeys } from '../../services/specialplayer.service.js';
import { parseDuration } from '../../utils/time.js';

/**
 * Dependencies shared by all whitelist commands.
 */
interface WhitelistCommandDependencies {
  logger: Logger;
  bcAdapter: BattleConAdapter;
  commandService: CommandService;
  recordRepo: RecordRepository;
  specialPlayerService: SpecialPlayerService;
  playerService: PlayerService;
}

/**
 * Base class for whitelist/blacklist commands.
 * Provides common functionality for adding/removing players from special groups.
 */
abstract class BaseWhitelistCommand extends BaseCommand {
  protected specialPlayerService: SpecialPlayerService;
  protected playerService: PlayerService;

  constructor(deps: WhitelistCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
    this.specialPlayerService = deps.specialPlayerService;
    this.playerService = deps.playerService;
  }

  /**
   * Get the group key this command manages.
   */
  protected abstract getGroupKey(): string;

  /**
   * Get the human-readable group name for messages.
   */
  protected getGroupDisplayName(): string {
    return this.specialPlayerService.getGroupName(this.getGroupKey());
  }

  /**
   * Parse duration from command arguments.
   * Returns duration in minutes, null for permanent, or undefined if invalid.
   */
  protected parseDurationFromArgs(args: string | null | undefined): { durationMinutes: number | null; reason: string } | null {
    if (!args || args.trim().length === 0) {
      // No args means permanent
      return { durationMinutes: null, reason: '' };
    }

    const parts = args.trim().split(/\s+/);
    const firstPart = parts[0]!;

    // Try to parse as duration
    const parsed = parseDuration(firstPart);

    if (parsed === undefined) {
      // Invalid duration format - treat entire args as reason, use permanent
      return { durationMinutes: null, reason: args.trim() };
    }

    // Valid duration (including null for permanent keywords like "perm")
    const reason = parts.slice(1).join(' ');
    return { durationMinutes: parsed, reason };
  }

  /**
   * Execute an add-to-group operation.
   */
  protected async executeAdd(ctx: CommandContext): Promise<void> {
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;
    const groupKey = this.getGroupKey();
    const groupName = this.getGroupDisplayName();

    // Parse duration from arguments
    const parsed = this.parseDurationFromArgs(ctx.args);
    if (!parsed) {
      await this.respondError(ctx, 'Invalid duration format');
      return;
    }

    const { durationMinutes } = parsed;

    try {
      // Check if already in group
      const existing = await this.specialPlayerService.checkPlayerInGroup(target.playerId, groupKey);
      if (existing.inGroup) {
        await this.respond(
          ctx,
          `${target.soldierName} is already in ${groupName} (expires: ${this.specialPlayerService.formatExpiration(existing.entry!.expirationDate)})`
        );
      }

      // Add to group
      await this.specialPlayerService.addPlayerToGroup(target, groupKey, durationMinutes);

      // Format duration for display
      const durationStr = this.specialPlayerService.formatDuration(durationMinutes);

      // Update record
      ctx.record.recordMessage = `Added to ${groupName} for ${durationStr}`;
      ctx.record.commandNumeric = durationMinutes ?? 10518984;
      await this.logRecord(ctx);

      // Respond
      await this.respond(ctx, `${target.soldierName} added to ${groupName} for ${durationStr}`);

      this.logger.info({
        source: ctx.player.soldierName,
        target: target.soldierName,
        groupKey,
        durationMinutes,
      }, `Player added to ${groupName}`);

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName, groupKey }, 'Failed to add player to group');
      await this.respondError(ctx, `Failed to add ${target.soldierName} to ${groupName}`);
    }
  }

  /**
   * Execute a remove-from-group operation.
   */
  protected async executeRemove(ctx: CommandContext): Promise<void> {
    if (!this.requireTarget(ctx)) {
      return;
    }

    const target = ctx.targetPlayer!;
    const groupKey = this.getGroupKey();
    const groupName = this.getGroupDisplayName();

    try {
      // Check if in group
      const existing = await this.specialPlayerService.checkPlayerInGroup(target.playerId, groupKey);
      if (!existing.inGroup) {
        await this.respondError(ctx, `${target.soldierName} is not in ${groupName}`);
        return;
      }

      // Remove from group
      const removed = await this.specialPlayerService.removePlayerFromGroup(target, groupKey);

      if (removed) {
        // Update record
        ctx.record.recordMessage = `Removed from ${groupName}`;
        await this.logRecord(ctx);

        // Respond
        await this.respond(ctx, `${target.soldierName} removed from ${groupName}`);

        this.logger.info({
          source: ctx.player.soldierName,
          target: target.soldierName,
          groupKey,
        }, `Player removed from ${groupName}`);
      } else {
        await this.respondError(ctx, `Failed to remove ${target.soldierName} from ${groupName}`);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName, groupKey }, 'Failed to remove player from group');
      await this.respondError(ctx, `Failed to remove ${target.soldierName} from ${groupName}`);
    }
  }
}

// ============================================
// Spambot Whitelist Commands
// ============================================

/**
 * Add player to spambot whitelist.
 * Usage: !spamwhitelist <player> [duration]
 */
export class SpamWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.SPAMWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_SPAMBOT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from spambot whitelist.
 * Usage: !unspamwhitelist <player>
 */
export class UnSpamWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNSPAMWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_SPAMBOT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Anti-Cheat Whitelist Commands
// ============================================

/**
 * Add player to anti-cheat whitelist.
 * Usage: !acwhitelist <player> [duration]
 */
export class ACWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.ACWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_ANTICHEAT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from anti-cheat whitelist.
 * Usage: !unacwhitelist <player>
 */
export class UnACWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNACWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_ANTICHEAT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Ping Whitelist Commands
// ============================================

/**
 * Add player to ping whitelist.
 * Usage: !pingwhitelist <player> [duration]
 */
export class PingWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.PWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_PING;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from ping whitelist.
 * Usage: !unpingwhitelist <player>
 */
export class UnPingWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNPWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_PING;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Reserved Slot Commands
// ============================================

/**
 * Add player to reserved slot list.
 * Usage: !reserved <player> [duration]
 */
export class ReservedSlotCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.RESERVED];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.SLOT_RESERVED;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from reserved slot list.
 * Usage: !unreserved <player>
 */
export class UnReservedSlotCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNRESERVED];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.SLOT_RESERVED;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Report Whitelist Commands
// ============================================

/**
 * Add player to report whitelist (cannot be reported).
 * Usage: !rwhitelist <player> [duration]
 */
export class ReportWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.RWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_REPORT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from report whitelist.
 * Usage: !unrwhitelist <player>
 */
export class UnReportWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNRWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_REPORT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Balance Whitelist Commands
// ============================================

/**
 * Add player to balance whitelist (won't be auto-balanced).
 * Usage: !mbwhitelist <player> [duration]
 */
export class BalanceWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.MBWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_MULTIBALANCER;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from balance whitelist.
 * Usage: !unmbwhitelist <player>
 */
export class UnBalanceWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNMBWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_MULTIBALANCER;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Dispersion Blacklist Commands
// ============================================

/**
 * Add player to dispersion blacklist (balanced first).
 * Usage: !disperse <player> [duration]
 */
export class DisperseCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.DISPERSE];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_DISPERSION;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from dispersion blacklist.
 * Usage: !undisperse <player>
 */
export class UnDisperseCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNDISPERSE];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_DISPERSION;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Spectator Slot Commands
// ============================================

/**
 * Add player to spectator slot list.
 * Usage: !spectator <player> [duration]
 */
export class SpectatorSlotCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.SPECTATOR];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.SLOT_SPECTATOR;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from spectator slot list.
 * Usage: !unspectator <player>
 */
export class UnSpectatorSlotCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNSPECTATOR];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.SLOT_SPECTATOR;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Teamkill Whitelist Commands
// ============================================

/**
 * Add player to teamkill whitelist (exempt from TK punishment).
 * Usage: !tkwhitelist <player> [duration]
 */
export class TKWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.TKWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_TEAMKILL;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from teamkill whitelist.
 * Usage: !untkwhitelist <player>
 */
export class UnTKWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNTKWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_TEAMKILL;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Admin Assistant Whitelist Commands
// ============================================

/**
 * Add player to admin assistant whitelist.
 * Usage: !aawhitelist <player> [duration]
 */
export class AAWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.AAWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_ADMIN_ASSISTANT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from admin assistant whitelist.
 * Usage: !unaawhitelist <player>
 */
export class UnAAWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNAAWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_ADMIN_ASSISTANT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Populator Whitelist Commands
// ============================================

/**
 * Add player to populator whitelist.
 * Usage: !popwhitelist <player> [duration]
 */
export class PopWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.POPWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_POPULATOR;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from populator whitelist.
 * Usage: !unpopwhitelist <player>
 */
export class UnPopWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNPOPWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_POPULATOR;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Spectator Blacklist Commands
// ============================================

/**
 * Add player to spectator blacklist (cannot spectate).
 * Usage: !specblacklist <player> [duration]
 */
export class SpecBlacklistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.SPECBLACKLIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_SPECTATOR;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from spectator blacklist.
 * Usage: !unspecblacklist <player>
 */
export class UnSpecBlacklistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNSPECBLACKLIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_SPECTATOR;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Report Blacklist Commands
// ============================================

/**
 * Add player to report blacklist (reports are auto-ignored).
 * Usage: !rblacklist <player> [duration]
 */
export class ReportBlacklistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.RBLACKLIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_REPORT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from report blacklist.
 * Usage: !unrblacklist <player>
 */
export class UnReportBlacklistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNRBLACKLIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_REPORT;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Command Target Whitelist Commands
// ============================================

/**
 * Add player to command target whitelist (cannot be targeted by commands).
 * Usage: !cwhitelist <player> [duration]
 */
export class CommandWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.CWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_COMMAND_TARGET;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from command target whitelist.
 * Usage: !uncwhitelist <player>
 */
export class UnCommandWhitelistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNCWHITELIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.WHITELIST_COMMAND_TARGET;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// Auto-Assist Blacklist Commands
// ============================================

/**
 * Add player to auto-assist blacklist.
 * Usage: !auablacklist <player> [duration]
 */
export class AutoAssistBlacklistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.AUABLACKLIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_AUTO_ASSIST;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from auto-assist blacklist.
 * Usage: !unauablacklist <player>
 */
export class UnAutoAssistBlacklistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNAUABLACKLIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_AUTO_ASSIST;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

// ============================================
// All-Caps Blacklist Commands
// ============================================

/**
 * Add player to all-caps blacklist.
 * Usage: !allcapsblacklist <player> [duration]
 */
export class AllCapsBlacklistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.ALLCAPSBLACKLIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_ALL_CAPS;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeAdd(ctx);
  }
}

/**
 * Remove player from all-caps blacklist.
 * Usage: !unallcapsblacklist <player>
 */
export class UnAllCapsBlacklistCommand extends BaseWhitelistCommand {
  getCommandKeys(): string[] {
    return [CommandKeys.UNALLCAPSBLACKLIST];
  }

  protected getGroupKey(): string {
    return SpecialGroupKeys.BLACKLIST_ALL_CAPS;
  }

  async execute(ctx: CommandContext): Promise<void> {
    await this.executeRemove(ctx);
  }
}

/**
 * Create and register all whitelist/blacklist commands.
 */
export function registerWhitelistCommands(deps: WhitelistCommandDependencies): {
  // Spambot
  spamWhitelist: SpamWhitelistCommand;
  unSpamWhitelist: UnSpamWhitelistCommand;
  // Anti-Cheat
  acWhitelist: ACWhitelistCommand;
  unACWhitelist: UnACWhitelistCommand;
  // Ping
  pingWhitelist: PingWhitelistCommand;
  unPingWhitelist: UnPingWhitelistCommand;
  // Reserved
  reserved: ReservedSlotCommand;
  unReserved: UnReservedSlotCommand;
  // Report Whitelist
  reportWhitelist: ReportWhitelistCommand;
  unReportWhitelist: UnReportWhitelistCommand;
  // Balance
  balanceWhitelist: BalanceWhitelistCommand;
  unBalanceWhitelist: UnBalanceWhitelistCommand;
  // Disperse
  disperse: DisperseCommand;
  unDisperse: UnDisperseCommand;
  // Spectator Slot
  spectator: SpectatorSlotCommand;
  unSpectator: UnSpectatorSlotCommand;
  // TK Whitelist
  tkWhitelist: TKWhitelistCommand;
  unTKWhitelist: UnTKWhitelistCommand;
  // AA Whitelist
  aaWhitelist: AAWhitelistCommand;
  unAAWhitelist: UnAAWhitelistCommand;
  // Populator
  popWhitelist: PopWhitelistCommand;
  unPopWhitelist: UnPopWhitelistCommand;
  // Spec Blacklist
  specBlacklist: SpecBlacklistCommand;
  unSpecBlacklist: UnSpecBlacklistCommand;
  // Report Blacklist
  reportBlacklist: ReportBlacklistCommand;
  unReportBlacklist: UnReportBlacklistCommand;
  // Command Whitelist
  commandWhitelist: CommandWhitelistCommand;
  unCommandWhitelist: UnCommandWhitelistCommand;
  // Auto-Assist Blacklist
  autoAssistBlacklist: AutoAssistBlacklistCommand;
  unAutoAssistBlacklist: UnAutoAssistBlacklistCommand;
  // All-Caps Blacklist
  allCapsBlacklist: AllCapsBlacklistCommand;
  unAllCapsBlacklist: UnAllCapsBlacklistCommand;
} {
  // Create all command instances
  const spamWhitelist = new SpamWhitelistCommand(deps);
  const unSpamWhitelist = new UnSpamWhitelistCommand(deps);
  const acWhitelist = new ACWhitelistCommand(deps);
  const unACWhitelist = new UnACWhitelistCommand(deps);
  const pingWhitelist = new PingWhitelistCommand(deps);
  const unPingWhitelist = new UnPingWhitelistCommand(deps);
  const reserved = new ReservedSlotCommand(deps);
  const unReserved = new UnReservedSlotCommand(deps);
  const reportWhitelist = new ReportWhitelistCommand(deps);
  const unReportWhitelist = new UnReportWhitelistCommand(deps);
  const balanceWhitelist = new BalanceWhitelistCommand(deps);
  const unBalanceWhitelist = new UnBalanceWhitelistCommand(deps);
  const disperse = new DisperseCommand(deps);
  const unDisperse = new UnDisperseCommand(deps);
  const spectator = new SpectatorSlotCommand(deps);
  const unSpectator = new UnSpectatorSlotCommand(deps);
  const tkWhitelist = new TKWhitelistCommand(deps);
  const unTKWhitelist = new UnTKWhitelistCommand(deps);
  const aaWhitelist = new AAWhitelistCommand(deps);
  const unAAWhitelist = new UnAAWhitelistCommand(deps);
  const popWhitelist = new PopWhitelistCommand(deps);
  const unPopWhitelist = new UnPopWhitelistCommand(deps);
  const specBlacklist = new SpecBlacklistCommand(deps);
  const unSpecBlacklist = new UnSpecBlacklistCommand(deps);
  const reportBlacklist = new ReportBlacklistCommand(deps);
  const unReportBlacklist = new UnReportBlacklistCommand(deps);
  const commandWhitelist = new CommandWhitelistCommand(deps);
  const unCommandWhitelist = new UnCommandWhitelistCommand(deps);
  const autoAssistBlacklist = new AutoAssistBlacklistCommand(deps);
  const unAutoAssistBlacklist = new UnAutoAssistBlacklistCommand(deps);
  const allCapsBlacklist = new AllCapsBlacklistCommand(deps);
  const unAllCapsBlacklist = new UnAllCapsBlacklistCommand(deps);

  // Register all commands
  spamWhitelist.register();
  unSpamWhitelist.register();
  acWhitelist.register();
  unACWhitelist.register();
  pingWhitelist.register();
  unPingWhitelist.register();
  reserved.register();
  unReserved.register();
  reportWhitelist.register();
  unReportWhitelist.register();
  balanceWhitelist.register();
  unBalanceWhitelist.register();
  disperse.register();
  unDisperse.register();
  spectator.register();
  unSpectator.register();
  tkWhitelist.register();
  unTKWhitelist.register();
  aaWhitelist.register();
  unAAWhitelist.register();
  popWhitelist.register();
  unPopWhitelist.register();
  specBlacklist.register();
  unSpecBlacklist.register();
  reportBlacklist.register();
  unReportBlacklist.register();
  commandWhitelist.register();
  unCommandWhitelist.register();
  autoAssistBlacklist.register();
  unAutoAssistBlacklist.register();
  allCapsBlacklist.register();
  unAllCapsBlacklist.register();

  return {
    spamWhitelist,
    unSpamWhitelist,
    acWhitelist,
    unACWhitelist,
    pingWhitelist,
    unPingWhitelist,
    reserved,
    unReserved,
    reportWhitelist,
    unReportWhitelist,
    balanceWhitelist,
    unBalanceWhitelist,
    disperse,
    unDisperse,
    spectator,
    unSpectator,
    tkWhitelist,
    unTKWhitelist,
    aaWhitelist,
    unAAWhitelist,
    popWhitelist,
    unPopWhitelist,
    specBlacklist,
    unSpecBlacklist,
    reportBlacklist,
    unReportBlacklist,
    commandWhitelist,
    unCommandWhitelist,
    autoAssistBlacklist,
    unAutoAssistBlacklist,
    allCapsBlacklist,
    unAllCapsBlacklist,
  };
}
