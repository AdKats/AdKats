import type { Logger } from '../core/logger.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { PlayerService } from './player.service.js';
import type { AdKatsConfig } from '../core/config.js';
import type { APlayer } from '../models/player.js';
import {
  type ATeam,
  type TeamManager,
  TeamIds,
  createTeam,
  createTeamManager,
  getOpposingTeamId,
  addPlayerToTeam,
  removePlayerFromTeam,
} from '../models/team.js';

/**
 * Team swap queue entry.
 */
interface SwapQueueEntry {
  player: APlayer;
  targetTeamId: number;
  requestTime: Date;
  reason: string;
}

/**
 * Pending move entry - for on-death moves.
 */
interface PendingMove {
  player: APlayer;
  targetTeamId: number;
  targetSquadId: number;
  requestedBy: APlayer | null;
  reason: string;
  forceKill: boolean;
}

/**
 * Move tracking entry - prevents abuse.
 */
interface MoveTrack {
  playerId: number;
  moveCount: number;
  lastMoveTime: Date;
}

/**
 * Team balance information.
 */
export interface TeamBalance {
  team1Count: number;
  team2Count: number;
  difference: number;
  isBalanced: boolean;
  weakerTeamId: number | null;
  strongerTeamId: number | null;
}

/**
 * TeamService - manages team state, player moves, and team swap queue.
 */
export class TeamService {
  private logger: Logger;
  private bcAdapter: BattleConAdapter;
  private eventBus: AdKatsEventBus;
  private playerService: PlayerService;
  private config: AdKatsConfig;

  // Team state
  private teamManager: TeamManager;

  // Team scores/tickets
  private teamTickets: Map<number, number> = new Map([
    [TeamIds.TEAM1, 0],
    [TeamIds.TEAM2, 0],
  ]);

  // Swap queue - players wanting to change teams
  private swapQueue: Map<string, SwapQueueEntry> = new Map();

  // Pending moves - moves that execute on player death
  private pendingMoves: Map<string, PendingMove> = new Map();

  // Move tracking - prevents abuse
  private moveTracking: Map<number, MoveTrack> = new Map();

  // Configuration
  private maxMovesPerRound: number = 3;
  private balanceThreshold: number = 2;

  constructor(
    logger: Logger,
    bcAdapter: BattleConAdapter,
    eventBus: AdKatsEventBus,
    playerService: PlayerService,
    config: AdKatsConfig
  ) {
    this.logger = logger;
    this.bcAdapter = bcAdapter;
    this.eventBus = eventBus;
    this.playerService = playerService;
    this.config = config;
    this.teamManager = createTeamManager();
  }

  /**
   * Initialize the team service and set up event listeners.
   */
  initialize(): void {
    this.logger.info('Initializing team service');

    // Listen for player deaths to process pending moves
    this.eventBus.onEvent('player:kill', (killer, victim, weapon, headshot) => {
      void this.handlePlayerDeath(victim);
    });

    // Listen for player leaves to clean up queues
    this.eventBus.onEvent('player:leave', (player) => {
      this.handlePlayerLeave(player);
    });

    // Listen for team changes to update tracking
    this.eventBus.onEvent('player:teamChange', (player, teamId, squadId) => {
      this.handleTeamChange(player, teamId, squadId);
    });

    // Listen for round end to reset state
    this.eventBus.onEvent('server:roundOver', (winningTeamId) => {
      this.handleRoundOver(winningTeamId);
    });

    // Listen for round start to reset move tracking
    this.eventBus.onEvent('server:levelLoaded', (map, mode, roundNum, roundsTotal) => {
      this.handleRoundStart(map, mode, roundNum);
    });

    // Listen for ticket updates
    this.eventBus.onEvent('server:roundOverTeamScores', (scores, targetScore) => {
      this.updateTeamTickets(scores);
    });

    this.logger.info('Team service initialized');
  }

  // =====================================================
  // Team State Management
  // =====================================================

  /**
   * Update team populations from player list.
   */
  updateTeamPopulations(): void {
    // Reset teams
    const team1 = createTeam(TeamIds.TEAM1);
    const team2 = createTeam(TeamIds.TEAM2);

    // Count players per team
    for (const player of this.playerService.getAllOnlinePlayers()) {
      if (player.teamId === TeamIds.TEAM1) {
        addPlayerToTeam(team1, player);
      } else if (player.teamId === TeamIds.TEAM2) {
        addPlayerToTeam(team2, player);
      }
    }

    this.teamManager.teams.set(TeamIds.TEAM1, team1);
    this.teamManager.teams.set(TeamIds.TEAM2, team2);

    this.logger.debug({
      team1: team1.playerCount,
      team2: team2.playerCount,
    }, 'Updated team populations');
  }

  /**
   * Get current team balance information.
   */
  getTeamBalance(): TeamBalance {
    this.updateTeamPopulations();

    const team1 = this.teamManager.teams.get(TeamIds.TEAM1);
    const team2 = this.teamManager.teams.get(TeamIds.TEAM2);

    const team1Count = team1?.playerCount ?? 0;
    const team2Count = team2?.playerCount ?? 0;
    const difference = Math.abs(team1Count - team2Count);

    let weakerTeamId: number | null = null;
    let strongerTeamId: number | null = null;

    if (team1Count < team2Count) {
      weakerTeamId = TeamIds.TEAM1;
      strongerTeamId = TeamIds.TEAM2;
    } else if (team2Count < team1Count) {
      weakerTeamId = TeamIds.TEAM2;
      strongerTeamId = TeamIds.TEAM1;
    }

    return {
      team1Count,
      team2Count,
      difference,
      isBalanced: difference <= this.balanceThreshold,
      weakerTeamId,
      strongerTeamId,
    };
  }

  /**
   * Get the weak team (fewer players or lower tickets).
   */
  getWeakTeam(): number | null {
    const balance = this.getTeamBalance();

    // First check player counts
    if (balance.weakerTeamId !== null) {
      return balance.weakerTeamId;
    }

    // If equal players, check tickets
    const team1Tickets = this.teamTickets.get(TeamIds.TEAM1) ?? 0;
    const team2Tickets = this.teamTickets.get(TeamIds.TEAM2) ?? 0;

    if (team1Tickets < team2Tickets) {
      return TeamIds.TEAM1;
    } else if (team2Tickets < team1Tickets) {
      return TeamIds.TEAM2;
    }

    return null; // Truly equal
  }

  /**
   * Get the winning team (more tickets).
   */
  getWinningTeam(): number | null {
    const team1Tickets = this.teamTickets.get(TeamIds.TEAM1) ?? 0;
    const team2Tickets = this.teamTickets.get(TeamIds.TEAM2) ?? 0;

    if (team1Tickets > team2Tickets) {
      return TeamIds.TEAM1;
    } else if (team2Tickets > team1Tickets) {
      return TeamIds.TEAM2;
    }

    return null; // Tied
  }

  /**
   * Get the opposing team ID for a player.
   */
  getOpposingTeam(player: APlayer): number {
    return getOpposingTeamId(player.teamId);
  }

  /**
   * Update team ticket counts.
   */
  private updateTeamTickets(scores: number[]): void {
    if (scores.length >= 1) {
      this.teamTickets.set(TeamIds.TEAM1, scores[0]!);
    }
    if (scores.length >= 2) {
      this.teamTickets.set(TeamIds.TEAM2, scores[1]!);
    }
  }

  // =====================================================
  // Player Move Operations
  // =====================================================

  /**
   * Move a player to a specific team/squad immediately (force kill first).
   */
  async forceMove(
    admin: APlayer | null,
    target: APlayer,
    targetTeamId?: number,
    targetSquadId: number = 0
  ): Promise<{ success: boolean; message: string }> {
    // Default to opposite team if not specified
    const teamId = targetTeamId ?? getOpposingTeamId(target.teamId);

    // Validate team ID
    if (teamId < TeamIds.TEAM1 || teamId > TeamIds.TEAM2) {
      return { success: false, message: 'Invalid team ID' };
    }

    // Check if already on target team
    if (target.teamId === teamId) {
      return { success: false, message: `${target.soldierName} is already on that team` };
    }

    try {
      // Execute the move with force kill
      await this.bcAdapter.movePlayer(target.soldierName, teamId, targetSquadId, true);

      // Update local state
      target.teamId = teamId;
      target.squadId = targetSquadId;

      // Track the move
      this.recordMove(target);

      this.logger.info({
        admin: admin?.soldierName ?? 'System',
        target: target.soldierName,
        teamId,
        squadId: targetSquadId,
      }, 'Force moved player');

      return {
        success: true,
        message: `Force moved ${target.soldierName} to Team ${teamId}`,
      };
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, target: target.soldierName }, 'Failed to force move player');
      return { success: false, message: `Failed to move ${target.soldierName}: ${msg}` };
    }
  }

  /**
   * Queue a move to be executed on player death.
   */
  queueMove(
    admin: APlayer | null,
    target: APlayer,
    targetTeamId?: number,
    targetSquadId: number = 0,
    reason: string = 'Admin move'
  ): { success: boolean; message: string } {
    // Default to opposite team if not specified
    const teamId = targetTeamId ?? getOpposingTeamId(target.teamId);

    // Check if already on target team
    if (target.teamId === teamId) {
      return { success: false, message: `${target.soldierName} is already on that team` };
    }

    // Check if already queued
    if (this.pendingMoves.has(target.soldierName)) {
      return { success: false, message: `${target.soldierName} already has a pending move` };
    }

    // Add to pending moves
    this.pendingMoves.set(target.soldierName, {
      player: target,
      targetTeamId: teamId,
      targetSquadId,
      requestedBy: admin,
      reason,
      forceKill: false,
    });

    this.logger.info({
      admin: admin?.soldierName ?? 'System',
      target: target.soldierName,
      teamId,
      squadId: targetSquadId,
      reason,
    }, 'Queued player move for death');

    return {
      success: true,
      message: `${target.soldierName} will be moved to Team ${teamId} on death`,
    };
  }

  /**
   * Cancel a pending move.
   */
  cancelPendingMove(playerName: string): boolean {
    return this.pendingMoves.delete(playerName);
  }

  /**
   * Pull a player to the admin's squad.
   */
  async pullPlayer(
    admin: APlayer,
    target: APlayer
  ): Promise<{ success: boolean; message: string }> {
    // Can't pull yourself
    if (admin.soldierName === target.soldierName) {
      return { success: false, message: 'You cannot pull yourself' };
    }

    // Move to admin's team and squad
    return this.forceMove(admin, target, admin.teamId, admin.squadId);
  }

  /**
   * Admin joins a player's squad.
   */
  async joinPlayer(
    admin: APlayer,
    target: APlayer
  ): Promise<{ success: boolean; message: string }> {
    // Can't join yourself
    if (admin.soldierName === target.soldierName) {
      return { success: false, message: 'You cannot join yourself' };
    }

    try {
      // Move admin to target's team and squad
      await this.bcAdapter.movePlayer(admin.soldierName, target.teamId, target.squadId, false);

      // Update local state
      admin.teamId = target.teamId;
      admin.squadId = target.squadId;

      this.logger.info({
        admin: admin.soldierName,
        target: target.soldierName,
        teamId: target.teamId,
        squadId: target.squadId,
      }, 'Admin joined player squad');

      return {
        success: true,
        message: `Joined ${target.soldierName}'s squad`,
      };
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, admin: admin.soldierName }, 'Failed to join player');
      return { success: false, message: `Failed to join: ${msg}` };
    }
  }

  // =====================================================
  // Team Swap Queue (Self-service)
  // =====================================================

  /**
   * Queue a player for team swap (self-service command).
   */
  queueTeamSwap(
    player: APlayer,
    reason: string = 'Team swap request'
  ): { success: boolean; message: string } {
    // Check if team swap is enabled
    if (!this.config.enableTeamSwap) {
      return { success: false, message: 'Team swap is disabled' };
    }

    // Check move limit
    if (!this.canPlayerMove(player)) {
      return { success: false, message: 'You have exceeded your move limit for this round' };
    }

    // Check if already in queue
    if (this.swapQueue.has(player.soldierName)) {
      return { success: false, message: 'You are already in the swap queue' };
    }

    // Check team balance - don't allow swapping to the stronger team
    const balance = this.getTeamBalance();
    const targetTeamId = getOpposingTeamId(player.teamId);

    if (targetTeamId === balance.strongerTeamId && balance.difference >= this.balanceThreshold) {
      return {
        success: false,
        message: 'Cannot swap to the stronger team - teams are unbalanced',
      };
    }

    // Add to swap queue
    this.swapQueue.set(player.soldierName, {
      player,
      targetTeamId,
      requestTime: new Date(),
      reason,
    });

    this.logger.info({
      player: player.soldierName,
      targetTeamId,
      reason,
    }, 'Player queued for team swap');

    return {
      success: true,
      message: `Added to team swap queue. You will be moved when a slot opens.`,
    };
  }

  /**
   * Move player to the weak team (assist command).
   */
  async assistTeam(
    player: APlayer
  ): Promise<{ success: boolean; message: string }> {
    const weakTeam = this.getWeakTeam();

    if (weakTeam === null) {
      return { success: false, message: 'Teams are currently balanced' };
    }

    // Check if already on weak team
    if (player.teamId === weakTeam) {
      return { success: false, message: 'You are already on the weaker team' };
    }

    // Check move limit
    if (!this.canPlayerMove(player)) {
      return { success: false, message: 'You have exceeded your move limit for this round' };
    }

    try {
      // Move to weak team
      await this.bcAdapter.movePlayer(player.soldierName, weakTeam, 0, false);

      // Update local state
      player.teamId = weakTeam;
      player.squadId = 0;

      // Track the move
      this.recordMove(player);

      this.logger.info({
        player: player.soldierName,
        targetTeam: weakTeam,
      }, 'Player assisted weak team');

      return {
        success: true,
        message: `Moved to the weaker team (Team ${weakTeam}). Thank you for helping balance!`,
      };
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg, player: player.soldierName }, 'Failed to assist team');
      return { success: false, message: `Failed to move: ${msg}` };
    }
  }

  /**
   * Process the swap queue - called periodically or on events.
   */
  async processSwapQueue(): Promise<void> {
    if (this.swapQueue.size === 0) {
      return;
    }

    const balance = this.getTeamBalance();

    // Process entries in order
    for (const [playerName, entry] of this.swapQueue) {
      // Check if player is still online
      const player = this.playerService.getOnlinePlayer(playerName);
      if (!player) {
        this.swapQueue.delete(playerName);
        continue;
      }

      // Check if swap would unbalance teams
      const targetTeamId = entry.targetTeamId;
      const wouldUnbalance =
        targetTeamId === balance.strongerTeamId &&
        balance.difference >= this.balanceThreshold;

      if (wouldUnbalance) {
        continue; // Skip this player, keep in queue
      }

      // Execute the swap
      try {
        await this.bcAdapter.movePlayer(player.soldierName, targetTeamId, 0, false);

        // Update local state
        player.teamId = targetTeamId;
        player.squadId = 0;

        // Track the move
        this.recordMove(player);

        // Remove from queue
        this.swapQueue.delete(playerName);

        // Notify player
        await this.bcAdapter.sayPlayer(
          `Team swap complete! You are now on Team ${targetTeamId}.`,
          player.soldierName
        );

        this.logger.info({
          player: player.soldierName,
          targetTeamId,
        }, 'Processed team swap from queue');

        // Only process one per cycle to avoid rapid changes
        break;
      } catch (error) {
        const msg = error instanceof Error ? error.message : String(error);
        this.logger.error({ error: msg, player: player.soldierName }, 'Failed to process swap');
        // Keep in queue for retry
      }
    }
  }

  /**
   * Remove a player from the swap queue.
   */
  cancelSwapQueue(playerName: string): boolean {
    return this.swapQueue.delete(playerName);
  }

  /**
   * Get the swap queue size.
   */
  getSwapQueueSize(): number {
    return this.swapQueue.size;
  }

  // =====================================================
  // Move Tracking
  // =====================================================

  /**
   * Check if a player can move (hasn't exceeded limit).
   */
  canPlayerMove(player: APlayer): boolean {
    const track = this.moveTracking.get(player.playerId);
    if (!track) {
      return true;
    }
    return track.moveCount < this.maxMovesPerRound;
  }

  /**
   * Record a move for tracking purposes.
   */
  private recordMove(player: APlayer): void {
    const track = this.moveTracking.get(player.playerId);
    if (track) {
      track.moveCount++;
      track.lastMoveTime = new Date();
    } else {
      this.moveTracking.set(player.playerId, {
        playerId: player.playerId,
        moveCount: 1,
        lastMoveTime: new Date(),
      });
    }
  }

  /**
   * Get remaining moves for a player.
   */
  getRemainingMoves(player: APlayer): number {
    const track = this.moveTracking.get(player.playerId);
    if (!track) {
      return this.maxMovesPerRound;
    }
    return Math.max(0, this.maxMovesPerRound - track.moveCount);
  }

  // =====================================================
  // Event Handlers
  // =====================================================

  /**
   * Handle player death - process pending moves.
   */
  private async handlePlayerDeath(victim: APlayer): Promise<void> {
    // Check for pending move
    const pendingMove = this.pendingMoves.get(victim.soldierName);
    if (!pendingMove) {
      return;
    }

    // Remove from pending
    this.pendingMoves.delete(victim.soldierName);

    try {
      // Execute the move (no force kill needed, player is dead)
      await this.bcAdapter.movePlayer(
        victim.soldierName,
        pendingMove.targetTeamId,
        pendingMove.targetSquadId,
        false
      );

      // Update local state
      victim.teamId = pendingMove.targetTeamId;
      victim.squadId = pendingMove.targetSquadId;

      // Track the move
      this.recordMove(victim);

      // Notify player
      await this.bcAdapter.sayPlayer(
        `You have been moved to Team ${pendingMove.targetTeamId}.`,
        victim.soldierName
      );

      this.logger.info({
        player: victim.soldierName,
        teamId: pendingMove.targetTeamId,
        squadId: pendingMove.targetSquadId,
        requestedBy: pendingMove.requestedBy?.soldierName ?? 'System',
      }, 'Executed pending move on death');

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({
        error: msg,
        player: victim.soldierName,
      }, 'Failed to execute pending move');
    }

    // Also try to process swap queue
    void this.processSwapQueue();
  }

  /**
   * Handle player leave - clean up queues.
   */
  private handlePlayerLeave(player: APlayer): void {
    this.pendingMoves.delete(player.soldierName);
    this.swapQueue.delete(player.soldierName);

    // Update team tracking
    const team = this.teamManager.teams.get(player.teamId);
    if (team) {
      removePlayerFromTeam(team, player.soldierName);
    }
  }

  /**
   * Handle team change event.
   */
  private handleTeamChange(player: APlayer, teamId: number, squadId: number): void {
    // Remove from old team
    const oldTeam = this.teamManager.teams.get(player.teamId);
    if (oldTeam) {
      removePlayerFromTeam(oldTeam, player.soldierName);
    }

    // Add to new team
    const newTeam = this.teamManager.teams.get(teamId);
    if (newTeam) {
      addPlayerToTeam(newTeam, player);
    }

    // Clear any pending moves since player changed teams
    this.pendingMoves.delete(player.soldierName);
    this.swapQueue.delete(player.soldierName);
  }

  /**
   * Handle round over - process swap queue.
   */
  private handleRoundOver(winningTeamId: number): void {
    // Process remaining swap queue at round end
    void this.processSwapQueue();

    this.logger.debug({ winningTeamId }, 'Round over - processed swap queue');
  }

  /**
   * Handle round start - reset move tracking.
   */
  private handleRoundStart(map: string, mode: string, roundNum: number): void {
    // Reset move tracking for new round
    this.moveTracking.clear();

    // Clear pending moves
    this.pendingMoves.clear();

    // Keep swap queue - they still want to swap

    // Update team manager
    this.teamManager.mapName = map;
    this.teamManager.modeName = mode;
    this.teamManager.roundNumber = roundNum;

    this.logger.debug({
      map,
      mode,
      roundNum,
    }, 'Round started - reset move tracking');
  }

  // =====================================================
  // Utility Methods
  // =====================================================

  /**
   * Get squad name from ID.
   */
  getSquadName(squadId: number): string {
    const squadNames = [
      'No Squad',
      'Alpha',
      'Bravo',
      'Charlie',
      'Delta',
      'Echo',
      'Foxtrot',
      'Golf',
      'Hotel',
    ];
    return squadNames[squadId] ?? `Squad ${squadId}`;
  }

  /**
   * Parse squad from argument string.
   * Returns squad ID or 0 if not specified/invalid.
   */
  parseSquadArg(arg: string | null): number {
    if (!arg) {
      return 0;
    }

    const lower = arg.toLowerCase().trim();

    // Try numeric
    const num = parseInt(lower, 10);
    if (!isNaN(num) && num >= 0 && num <= 8) {
      return num;
    }

    // Try name
    const squadNames: Record<string, number> = {
      alpha: 1,
      bravo: 2,
      charlie: 3,
      delta: 4,
      echo: 5,
      foxtrot: 6,
      golf: 7,
      hotel: 8,
      a: 1,
      b: 2,
      c: 3,
      d: 4,
      e: 5,
      f: 6,
      g: 7,
      h: 8,
    };

    return squadNames[lower] ?? 0;
  }
}

/**
 * Create a new team service.
 */
export function createTeamService(
  logger: Logger,
  bcAdapter: BattleConAdapter,
  eventBus: AdKatsEventBus,
  playerService: PlayerService,
  config: AdKatsConfig
): TeamService {
  return new TeamService(logger, bcAdapter, eventBus, playerService, config);
}
