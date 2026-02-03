import type { Logger } from '../../core/logger.js';
import type { BattleConAdapter } from '../../core/battlecon-adapter.js';
import type { CommandService, CommandContext } from '../../services/command.service.js';
import type { RecordRepository } from '../../database/repositories/record.repository.js';
import type { PlayerService } from '../../services/player.service.js';
import type { APlayer } from '../../models/player.js';
import { BaseCommand } from '../base.command.js';
import { CommandKeys } from '../../models/command.js';

/**
 * Vote types supported by the voting system.
 */
export enum VoteType {
  Surrender = 'surrender',
  NextMap = 'nextmap',
  KickPlayer = 'kick',
}

/**
 * Active vote state.
 */
export interface ActiveVote {
  type: VoteType;
  initiator: APlayer;
  targetPlayer?: APlayer;  // For votekick
  reason?: string;
  startTime: Date;
  endTime: Date;
  votesFor: Set<number>;   // Player IDs who voted yes
  votesAgainst: Set<number>; // Player IDs who voted no
  teamId?: number;         // For team-specific votes like surrender
  announced: boolean;
}

/**
 * Vote configuration.
 */
export interface VoteConfig {
  /** Duration of vote in seconds */
  voteDuration: number;
  /** Percentage of eligible voters required to pass (0-100) */
  passPercentage: number;
  /** Minimum number of voters required */
  minVoters: number;
  /** Cooldown between votes of same type in seconds */
  cooldownSeconds: number;
  /** Whether to allow votes during low population */
  allowLowPop: boolean;
  /** Minimum players for vote to be allowed */
  minPlayersForVote: number;
}

/**
 * Default vote configuration.
 */
const DEFAULT_VOTE_CONFIG: VoteConfig = {
  voteDuration: 60,
  passPercentage: 60,
  minVoters: 3,
  cooldownSeconds: 300,
  allowLowPop: false,
  minPlayersForVote: 8,
};

/**
 * Voting manager - handles active votes and vote processing.
 */
export class VotingManager {
  private logger: Logger;
  private bcAdapter: BattleConAdapter;
  private playerService: PlayerService;
  private config: VoteConfig;

  private activeVotes: Map<VoteType, ActiveVote> = new Map();
  private cooldowns: Map<VoteType, Date> = new Map();
  private voteTimers: Map<VoteType, NodeJS.Timeout> = new Map();

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    playerService: PlayerService,
    config: Partial<VoteConfig> = {}
  ) {
    this.logger = logger;
    this.bcAdapter = bcAdapter;
    this.playerService = playerService;
    this.config = { ...DEFAULT_VOTE_CONFIG, ...config };
  }

  /**
   * Start a new vote.
   */
  async startVote(
    type: VoteType,
    initiator: APlayer,
    options: {
      targetPlayer?: APlayer;
      reason?: string;
      teamId?: number;
    } = {}
  ): Promise<{ success: boolean; message: string }> {
    // Check if vote of this type is already active
    if (this.activeVotes.has(type)) {
      return { success: false, message: `A ${type} vote is already in progress` };
    }

    // Check cooldown
    const cooldownEnd = this.cooldowns.get(type);
    if (cooldownEnd && cooldownEnd > new Date()) {
      const remaining = Math.ceil((cooldownEnd.getTime() - Date.now()) / 1000);
      return {
        success: false,
        message: `${type} vote is on cooldown. ${remaining}s remaining.`,
      };
    }

    // Check minimum players
    const playerCount = this.playerService.getOnlinePlayerCount();
    if (!this.config.allowLowPop && playerCount < this.config.minPlayersForVote) {
      return {
        success: false,
        message: `Not enough players for vote (need ${this.config.minPlayersForVote})`,
      };
    }

    // Create the vote
    const now = new Date();
    const vote: ActiveVote = {
      type,
      initiator,
      targetPlayer: options.targetPlayer,
      reason: options.reason,
      startTime: now,
      endTime: new Date(now.getTime() + this.config.voteDuration * 1000),
      votesFor: new Set([initiator.playerId]), // Initiator automatically votes yes
      votesAgainst: new Set(),
      teamId: options.teamId,
      announced: false,
    };

    this.activeVotes.set(type, vote);

    // Set timer to end vote
    const timer = setTimeout(() => {
      void this.endVote(type);
    }, this.config.voteDuration * 1000);
    this.voteTimers.set(type, timer);

    // Announce the vote
    await this.announceVote(vote);

    this.logger.info({
      type,
      initiator: initiator.soldierName,
      target: options.targetPlayer?.soldierName,
      reason: options.reason,
    }, 'Vote started');

    return { success: true, message: `Vote started! Type @yes or @no to vote.` };
  }

  /**
   * Cast a vote.
   */
  castVote(type: VoteType, player: APlayer, voteYes: boolean): { success: boolean; message: string } {
    const vote = this.activeVotes.get(type);
    if (!vote) {
      return { success: false, message: `No active ${type} vote` };
    }

    // Check if player already voted
    if (vote.votesFor.has(player.playerId) || vote.votesAgainst.has(player.playerId)) {
      return { success: false, message: 'You have already voted' };
    }

    // Check team restriction for surrender votes
    if (vote.teamId !== undefined && player.teamId !== vote.teamId) {
      return { success: false, message: 'You cannot vote on this' };
    }

    // Cast the vote
    if (voteYes) {
      vote.votesFor.add(player.playerId);
    } else {
      vote.votesAgainst.add(player.playerId);
    }

    const totalVotes = vote.votesFor.size + vote.votesAgainst.size;

    this.logger.debug({
      type,
      player: player.soldierName,
      voteYes,
      totalVotes,
    }, 'Vote cast');

    return {
      success: true,
      message: `Vote recorded. Current: ${vote.votesFor.size} yes, ${vote.votesAgainst.size} no`,
    };
  }

  /**
   * End a vote and process the result.
   */
  async endVote(type: VoteType): Promise<{ passed: boolean; message: string } | null> {
    const vote = this.activeVotes.get(type);
    if (!vote) {
      return null;
    }

    // Clear the timer
    const timer = this.voteTimers.get(type);
    if (timer) {
      clearTimeout(timer);
      this.voteTimers.delete(type);
    }

    // Remove from active votes
    this.activeVotes.delete(type);

    // Set cooldown
    this.cooldowns.set(
      type,
      new Date(Date.now() + this.config.cooldownSeconds * 1000)
    );

    // Calculate results
    const totalVotes = vote.votesFor.size + vote.votesAgainst.size;
    const yesPercentage = totalVotes > 0 ? (vote.votesFor.size / totalVotes) * 100 : 0;
    const passed = totalVotes >= this.config.minVoters && yesPercentage >= this.config.passPercentage;

    const resultMessage = passed
      ? `Vote passed! (${vote.votesFor.size} yes, ${vote.votesAgainst.size} no)`
      : `Vote failed. (${vote.votesFor.size} yes, ${vote.votesAgainst.size} no)`;

    // Announce result
    await this.bcAdapter.say(`[Vote] ${resultMessage}`);

    this.logger.info({
      type,
      passed,
      votesFor: vote.votesFor.size,
      votesAgainst: vote.votesAgainst.size,
      percentage: yesPercentage.toFixed(1),
    }, 'Vote ended');

    return { passed, message: resultMessage };
  }

  /**
   * Cancel an active vote.
   */
  async cancelVote(type: VoteType, reason: string = 'cancelled'): Promise<boolean> {
    const vote = this.activeVotes.get(type);
    if (!vote) {
      return false;
    }

    // Clear the timer
    const timer = this.voteTimers.get(type);
    if (timer) {
      clearTimeout(timer);
      this.voteTimers.delete(type);
    }

    // Remove from active votes
    this.activeVotes.delete(type);

    // Announce cancellation
    await this.bcAdapter.say(`[Vote] ${type} vote ${reason}`);

    this.logger.info({ type, reason }, 'Vote cancelled');

    return true;
  }

  /**
   * Get active vote of a type.
   */
  getActiveVote(type: VoteType): ActiveVote | null {
    return this.activeVotes.get(type) ?? null;
  }

  /**
   * Check if there's any active vote.
   */
  hasActiveVote(): boolean {
    return this.activeVotes.size > 0;
  }

  /**
   * Announce a vote to the server.
   */
  private async announceVote(vote: ActiveVote): Promise<void> {
    let message = '';

    switch (vote.type) {
      case VoteType.Surrender:
        message = `[Vote] ${vote.initiator.soldierName} started a SURRENDER vote. Type @yes or @no`;
        break;
      case VoteType.NextMap:
        message = `[Vote] ${vote.initiator.soldierName} started a NEXT MAP vote. Type @yes or @no`;
        break;
      case VoteType.KickPlayer:
        message = `[Vote] ${vote.initiator.soldierName} started a vote to KICK ${vote.targetPlayer?.soldierName}. Reason: ${vote.reason ?? 'No reason'}`;
        break;
    }

    await this.bcAdapter.say(message);
    vote.announced = true;
  }
}

/**
 * Surrender command - start a surrender vote for your team.
 * Usage: @surrender
 *
 * Allows a team to vote to end the round early.
 */
export class SurrenderCommand extends BaseCommand {
  private votingManager: VotingManager;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    votingManager: VotingManager
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.votingManager = votingManager;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.SURRENDER];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Player must be on a team
    if (ctx.player.teamId <= 0) {
      await this.respondError(ctx, 'You must be on a team to start a surrender vote');
      return;
    }

    try {
      const result = await this.votingManager.startVote(
        VoteType.Surrender,
        ctx.player,
        { teamId: ctx.player.teamId }
      );

      if (result.success) {
        await this.respondSuccess(ctx, result.message);

        // Log the record
        ctx.record.recordMessage = 'Started surrender vote';
        await this.logRecord(ctx);
      } else {
        await this.respondError(ctx, result.message);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to start surrender vote');
      await this.respondError(ctx, 'Failed to start vote');
    }
  }
}

/**
 * VoteNext command - start a vote to skip to next map.
 * Usage: @votenext
 */
export class VoteNextCommand extends BaseCommand {
  private votingManager: VotingManager;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    votingManager: VotingManager
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.votingManager = votingManager;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.VOTENEXT];
  }

  async execute(ctx: CommandContext): Promise<void> {
    try {
      const result = await this.votingManager.startVote(
        VoteType.NextMap,
        ctx.player
      );

      if (result.success) {
        await this.respondSuccess(ctx, result.message);

        // Log the record
        ctx.record.recordMessage = 'Started next map vote';
        await this.logRecord(ctx);
      } else {
        await this.respondError(ctx, result.message);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to start next map vote');
      await this.respondError(ctx, 'Failed to start vote');
    }
  }
}

/**
 * VoteKick command - start a vote to kick a player.
 * Usage: @votekick <player> [reason]
 */
export class VoteKickCommand extends BaseCommand {
  private votingManager: VotingManager;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    votingManager: VotingManager
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.votingManager = votingManager;
  }

  getCommandKeys(): string[] {
    // Using a custom key since VOTEKICK may not be in CommandKeys
    return ['player_votekick'];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Require a target player
    if (!this.requireTarget(ctx)) {
      await this.respond(ctx, 'Usage: @votekick <player> [reason]');
      return;
    }

    const target = ctx.targetPlayer!;

    // Cannot votekick yourself
    if (target.playerId === ctx.player.playerId) {
      await this.respondError(ctx, 'You cannot vote to kick yourself');
      return;
    }

    const reason = ctx.args?.trim() || 'Votekick';

    try {
      const result = await this.votingManager.startVote(
        VoteType.KickPlayer,
        ctx.player,
        { targetPlayer: target, reason }
      );

      if (result.success) {
        await this.respondSuccess(ctx, result.message);

        // Log the record
        ctx.record.recordMessage = `Votekick ${target.soldierName}: ${reason}`;
        await this.logRecord(ctx);
      } else {
        await this.respondError(ctx, result.message);
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to start votekick');
      await this.respondError(ctx, 'Failed to start vote');
    }
  }
}

/**
 * Yes/No vote commands for responding to active votes.
 */
export class VoteYesCommand extends BaseCommand {
  private votingManager: VotingManager;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    votingManager: VotingManager
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.votingManager = votingManager;
  }

  getCommandKeys(): string[] {
    return [CommandKeys.POLL_VOTE, 'vote_yes'];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Find any active vote the player can vote on
    for (const type of [VoteType.Surrender, VoteType.NextMap, VoteType.KickPlayer]) {
      const vote = this.votingManager.getActiveVote(type);
      if (vote) {
        // Check team restriction for surrender
        if (vote.teamId !== undefined && ctx.player.teamId !== vote.teamId) {
          continue;
        }

        const result = this.votingManager.castVote(type, ctx.player, true);
        if (result.success) {
          await this.respond(ctx, result.message);
        } else {
          await this.respondError(ctx, result.message);
        }
        return;
      }
    }

    await this.respondError(ctx, 'No active vote to vote on');
  }
}

export class VoteNoCommand extends BaseCommand {
  private votingManager: VotingManager;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    commandService: CommandService,
    recordRepo: RecordRepository,
    votingManager: VotingManager
  ) {
    super(logger, bcAdapter, commandService, recordRepo);
    this.votingManager = votingManager;
  }

  getCommandKeys(): string[] {
    return ['vote_no'];
  }

  async execute(ctx: CommandContext): Promise<void> {
    // Find any active vote the player can vote on
    for (const type of [VoteType.Surrender, VoteType.NextMap, VoteType.KickPlayer]) {
      const vote = this.votingManager.getActiveVote(type);
      if (vote) {
        // Check team restriction for surrender
        if (vote.teamId !== undefined && ctx.player.teamId !== vote.teamId) {
          continue;
        }

        const result = this.votingManager.castVote(type, ctx.player, false);
        if (result.success) {
          await this.respond(ctx, result.message);
        } else {
          await this.respondError(ctx, result.message);
        }
        return;
      }
    }

    await this.respondError(ctx, 'No active vote to vote on');
  }
}

/**
 * Dependencies required for voting commands.
 */
export interface VotingCommandDependencies {
  logger: Logger;
  bcAdapter: BattleConAdapter;
  commandService: CommandService;
  recordRepo: RecordRepository;
  playerService: PlayerService;
  voteConfig?: Partial<VoteConfig>;
}

/**
 * Create and register all voting commands.
 */
export function registerVotingCommands(deps: VotingCommandDependencies): {
  votingManager: VotingManager;
  surrender: SurrenderCommand;
  voteNext: VoteNextCommand;
  voteKick: VoteKickCommand;
  voteYes: VoteYesCommand;
  voteNo: VoteNoCommand;
} {
  const { logger, bcAdapter, commandService, recordRepo, playerService, voteConfig } = deps;

  // Create voting manager
  const votingManager = new VotingManager(
    logger,
    bcAdapter,
    playerService,
    voteConfig
  );

  // Create and register commands
  const surrender = new SurrenderCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    votingManager
  );
  surrender.register();

  const voteNext = new VoteNextCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    votingManager
  );
  voteNext.register();

  const voteKick = new VoteKickCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    votingManager
  );
  voteKick.register();

  const voteYes = new VoteYesCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    votingManager
  );
  voteYes.register();

  const voteNo = new VoteNoCommand(
    logger,
    bcAdapter,
    commandService,
    recordRepo,
    votingManager
  );
  voteNo.register();

  return { votingManager, surrender, voteNext, voteKick, voteYes, voteNo };
}

// Note: VotingManager is already exported above at line 68
// Types VoteConfig and ActiveVote are exported as interfaces earlier in the file
