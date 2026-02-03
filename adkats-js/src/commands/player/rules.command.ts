import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Rules command - displays server rules to the player.
 * Usage: @rules
 */
export class RulesCommand extends BaseCommand {
  // Server rules - should be loaded from config/database
  private rules: string[] = [
    'Rule 1: No cheating or exploiting',
    'Rule 2: No racism or hate speech',
    'Rule 3: No spawn camping',
    'Rule 4: No base camping',
    'Rule 5: Respect all players and admins',
  ];

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
  }

  getCommandKeys(): string[] {
    return [CommandKeys.RULES];
  }

  /**
   * Set the server rules.
   */
  setRules(rules: string[]): void {
    this.rules = rules;
  }

  async execute(ctx: CommandContext): Promise<void> {
    try {
      // Send rules to the player
      await this.bcAdapter.sayPlayer('=== Server Rules ===', ctx.player.soldierName);

      for (const rule of this.rules) {
        await this.bcAdapter.sayPlayer(rule, ctx.player.soldierName);
      }

      // Log the request
      ctx.record.recordMessage = 'Requested rules';
      await this.logRecord(ctx);

      this.logger.debug({
        player: ctx.player.soldierName,
      }, 'Player requested rules');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to send rules');
      await this.respondError(ctx, 'Failed to display rules');
    }
  }
}

/**
 * Create and register the rules command.
 */
export function registerRulesCommand(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  commandService: CommandService,
  recordRepo: RecordRepository
): RulesCommand {
  const cmd = new RulesCommand(logger, bcAdapter, commandService, recordRepo);
  cmd.register();
  return cmd;
}
