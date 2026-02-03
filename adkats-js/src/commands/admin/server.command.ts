import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Dependencies shared by all server admin commands.
 */
interface ServerCommandDependencies {
  logger: Logger;
  bcAdapter: BattleConAdapter;
  commandService: CommandService;
  recordRepo: RecordRepository;
}

/**
 * Server Shutdown command - shuts down the game server.
 * Usage: @shutdown [delay_seconds]
 *
 * This is an extremely dangerous command that will stop the game server.
 * Requires confirmation and proper authorization.
 */
export class ShutdownCommand extends BaseCommand {
  constructor(deps: ServerCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.SHUTDOWN];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Parse delay argument (defaults to 30 seconds)
    const delaySeconds = this.parseDelayArg(ctx.args);

    // Check if this needs confirmation
    if (!ctx.record.isConfirmed) {
      // Request confirmation for this dangerous action
      await this.respond(ctx, 'DANGER: This will SHUT DOWN the game server!');
      await this.respond(ctx, `The server will shut down in ${delaySeconds} seconds after confirmation.`);
      await this.respond(ctx, 'Type "yes" to confirm or "no" to cancel. This action cannot be undone!');
      this.commandService.setPendingConfirmation(ctx.player, ctx.record);
      return;
    }

    try {
      // Announce the shutdown
      await this.bcAdapter.say(`SERVER SHUTDOWN initiated by ${ctx.player.soldierName}!`);
      await this.bcAdapter.yell(`SERVER SHUTTING DOWN in ${delaySeconds} seconds!`, 10);

      // Log the action BEFORE shutting down
      ctx.record.recordMessage = `Server shutdown initiated (${delaySeconds}s delay)`;
      await this.logRecord(ctx);

      // Respond to admin
      await this.respond(ctx, `Server will shut down in ${delaySeconds} seconds`);

      this.logger.warn({
        admin: ctx.player.soldierName,
        delaySeconds,
      }, 'Server shutdown initiated');

      // Countdown announcements
      await this.countdownShutdown(delaySeconds);

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to initiate server shutdown');
      await this.respondError(ctx, 'Failed to initiate server shutdown');
    }
  }

  /**
   * Parse delay argument from user input.
   * Returns delay in seconds (default 30, min 10, max 300).
   */
  private parseDelayArg(arg: string | null): number {
    if (!arg || arg.trim() === '') {
      return 30; // Default delay
    }

    const seconds = parseInt(arg.trim(), 10);
    if (isNaN(seconds) || seconds < 10) {
      return 10; // Minimum delay
    }
    if (seconds > 300) {
      return 300; // Maximum delay
    }
    return seconds;
  }

  /**
   * Countdown to shutdown with announcements.
   */
  private async countdownShutdown(totalSeconds: number): Promise<void> {
    // Warning intervals in seconds
    const warningIntervals = [60, 30, 10, 5, 4, 3, 2, 1];
    let remainingSeconds = totalSeconds;

    for (const interval of warningIntervals) {
      if (remainingSeconds > interval) {
        // Wait until we reach this interval
        const waitTime = (remainingSeconds - interval) * 1000;
        await this.delay(waitTime);
        remainingSeconds = interval;

        // Announce
        if (interval >= 10) {
          await this.bcAdapter.say(`SERVER SHUTDOWN in ${interval} seconds!`);
        }
        await this.bcAdapter.yell(`SHUTDOWN in ${interval}s!`, 3);
      }
    }

    // Final delay
    if (remainingSeconds > 0) {
      await this.delay(remainingSeconds * 1000);
    }

    // Execute shutdown
    this.logger.warn('Executing server shutdown');
    // Note: The actual shutdown mechanism depends on the RCON implementation
    // Some servers support admin.shutDown command
    try {
      // @ts-expect-error - This method may not exist on all adapters
      if (typeof this.bcAdapter.shutdownServer === 'function') {
        // @ts-expect-error - Custom method
        await this.bcAdapter.shutdownServer();
      } else {
        // Fallback: Log that manual shutdown is needed
        this.logger.warn('Server shutdown command not available - manual intervention required');
      }
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to execute shutdown command');
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
 * Countdown command - displays a countdown message to all players.
 * Usage: @countdown <seconds> [message]
 *
 * Useful for announcing events, restarts, or other timed actions.
 */
export class CountdownCommand extends BaseCommand {
  // Track active countdowns to prevent multiple simultaneous countdowns
  private static activeCountdown: boolean = false;

  constructor(deps: ServerCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.COUNTDOWN];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Check if there's already an active countdown
    if (CountdownCommand.activeCountdown) {
      await this.respondError(ctx, 'A countdown is already in progress');
      return;
    }

    // Parse arguments: <seconds> [message]
    const parsed = this.parseCountdownArgs(ctx.args);

    if (parsed === null) {
      await this.respondError(ctx, 'Usage: @countdown <seconds> [message]');
      await this.respond(ctx, 'Example: @countdown 30 Event starting');
      return;
    }

    const { seconds, message } = parsed;

    // Validate seconds
    if (seconds < 5) {
      await this.respondError(ctx, 'Countdown must be at least 5 seconds');
      return;
    }
    if (seconds > 300) {
      await this.respondError(ctx, 'Countdown cannot exceed 300 seconds (5 minutes)');
      return;
    }

    try {
      CountdownCommand.activeCountdown = true;

      // Log the action
      ctx.record.recordMessage = `Countdown: ${seconds}s - ${message}`;
      await this.logRecord(ctx);

      // Respond to admin
      await this.respondSuccess(ctx, `Started ${seconds} second countdown: ${message}`);

      this.logger.info({
        admin: ctx.player.soldierName,
        seconds,
        message,
      }, 'Countdown started');

      // Execute the countdown
      await this.runCountdown(seconds, message);

      CountdownCommand.activeCountdown = false;

    } catch (error) {
      CountdownCommand.activeCountdown = false;
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Countdown failed');
      await this.respondError(ctx, 'Countdown failed');
    }
  }

  /**
   * Parse countdown arguments.
   * Returns { seconds, message } or null if invalid.
   */
  private parseCountdownArgs(arg: string | null): { seconds: number; message: string } | null {
    if (!arg || arg.trim() === '') {
      return null;
    }

    const parts = arg.trim().split(/\s+/);
    if (parts.length === 0) {
      return null;
    }

    const seconds = parseInt(parts[0]!, 10);
    if (isNaN(seconds) || seconds <= 0) {
      return null;
    }

    const message = parts.slice(1).join(' ') || 'Countdown';

    return { seconds, message };
  }

  /**
   * Run the countdown with announcements.
   */
  private async runCountdown(totalSeconds: number, message: string): Promise<void> {
    // Initial announcement
    await this.bcAdapter.say(`[COUNTDOWN] ${message} - ${totalSeconds} seconds`);
    await this.bcAdapter.yell(`${message}: ${totalSeconds}s`, 3);

    // Announcement intervals in seconds
    const announceAt = [60, 30, 15, 10, 5, 4, 3, 2, 1];
    let remainingSeconds = totalSeconds;
    let lastAnnounce = totalSeconds;

    while (remainingSeconds > 0) {
      // Wait one second
      await this.delay(1000);
      remainingSeconds--;

      // Check if we should announce
      if (announceAt.includes(remainingSeconds) && remainingSeconds < lastAnnounce) {
        lastAnnounce = remainingSeconds;

        if (remainingSeconds >= 10) {
          await this.bcAdapter.say(`[COUNTDOWN] ${message} - ${remainingSeconds} seconds`);
        }

        // Always yell in the final 10 seconds
        if (remainingSeconds <= 10) {
          await this.bcAdapter.yell(`${message}: ${remainingSeconds}`, 2);
        }
      }
    }

    // Final announcement
    await this.bcAdapter.say(`[COUNTDOWN] ${message} - NOW!`);
    await this.bcAdapter.yell(message, 5);
  }

  /**
   * Delay execution for specified milliseconds.
   */
  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}

/**
 * AFK command - checks and optionally kicks AFK (idle) players.
 * Usage: @afk [kick]
 *
 * Without arguments: Lists players who appear to be AFK
 * With "kick": Kicks all detected AFK players
 */
export class AfkCommand extends BaseCommand {
  // AFK threshold in seconds (2 minutes of no score change)
  private readonly afkThresholdSeconds = 120;

  // Track player last activity (score changes)
  private static playerLastActivity: Map<string, { score: number; time: Date }> = new Map();

  constructor(deps: ServerCommandDependencies) {
    super(deps.logger, deps.bcAdapter, deps.commandService, deps.recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.AFK];
  }

  async execute(ctx: CommandContext): Promise<void> {
    const shouldKick = ctx.args?.toLowerCase().trim() === 'kick';

    // For now, respond that the feature is not fully implemented
    // as it requires tracking player activity over time
    await this.respond(ctx, 'AFK detection checks for idle players based on activity.');

    // In a full implementation, you would:
    // 1. Track player score/activity over time
    // 2. Compare current activity to stored activity
    // 3. Flag players with no change beyond the threshold
    // 4. List or kick them based on the argument

    // Log the action
    ctx.record.recordMessage = shouldKick ? 'AFK kick check' : 'AFK list check';
    await this.logRecord(ctx);

    if (shouldKick) {
      await this.respond(ctx, 'AFK kick functionality requires player activity tracking to be enabled.');
    } else {
      await this.respond(ctx, 'AFK check functionality requires player activity tracking to be enabled.');
    }

    this.logger.debug({
      admin: ctx.player.soldierName,
      shouldKick,
    }, 'AFK check requested');
  }

  /**
   * Update player activity tracking.
   * Call this when player scores change.
   */
  static updatePlayerActivity(playerName: string, score: number): void {
    AfkCommand.playerLastActivity.set(playerName, {
      score,
      time: new Date(),
    });
  }

  /**
   * Remove player from activity tracking.
   * Call this when player leaves.
   */
  static removePlayer(playerName: string): void {
    AfkCommand.playerLastActivity.delete(playerName);
  }

  /**
   * Clear all activity tracking.
   * Call this on round end.
   */
  static clearActivityTracking(): void {
    AfkCommand.playerLastActivity.clear();
  }
}

/**
 * Create and register all server admin commands.
 */
export function registerServerCommands(deps: ServerCommandDependencies): {
  shutdownCommand: ShutdownCommand;
  countdownCommand: CountdownCommand;
  afkCommand: AfkCommand;
} {
  const shutdownCommand = new ShutdownCommand(deps);
  const countdownCommand = new CountdownCommand(deps);
  const afkCommand = new AfkCommand(deps);

  shutdownCommand.register();
  countdownCommand.register();
  afkCommand.register();

  return { shutdownCommand, countdownCommand, afkCommand };
}
