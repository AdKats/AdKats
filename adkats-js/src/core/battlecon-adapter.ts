import { EventEmitter } from 'node:events';
import type { Logger } from './logger.js';
import type { AdKatsEventBus } from './event-bus.js';
import type { PlayerService } from '../services/player.service.js';

/**
 * BattleCon client interface (from @acp/battlecon).
 * This interface defines the subset of BattleCon methods used by AdKats.
 */
export interface BattleConClient extends EventEmitter {
  host: string;
  port: number;
  loggedIn: boolean;

  // Connection methods
  connect(): Promise<void>;
  disconnect(): void;

  // Command execution
  exec(command: string | string[]): Promise<string[]>;

  // Core methods (added by core.js module)
  version(): Promise<{ game: string; version: string }>;
  help(): Promise<string[]>;
  eventsEnabled(enabled?: boolean): Promise<boolean>;
  listPlayers(): Promise<PlayerInfo[]>;
  serverInfo(): Promise<string[]>;
  login(): Promise<void>;
  logout(): Promise<void>;
  quit(): Promise<void>;

  // Utility
  tabulate(res: string[], offset?: number): TabularResult;
}

/**
 * Raw player info from listPlayers command.
 */
export interface PlayerInfo {
  name: string;
  guid: string;
  teamId: string;
  squadId: string;
  kills: string;
  deaths: string;
  score: string;
  rank: string;
  ping: string;
  type: string;
}

/**
 * Result from tabulate() method.
 */
export interface TabularResult extends Array<Record<string, string>> {
  columns: string[];
}

/**
 * BattleCon events emitted by the BF module.
 */
export interface BattleConEvents {
  'connect': () => void;
  'close': () => void;
  'error': (error: Error | string) => void;
  'login': () => void;
  'ready': () => void;
  'event': (msg: { data: string[] }) => void;
  'message': (msg: { data: string[] }) => void;

  // Player events (from BF.js)
  'player.join': (name: string, guid: string) => void;
  'player.authenticated': (name: string) => void;
  'player.leave': (name: string, info: string[]) => void;
  'player.spawn': (name: string, teamId: number) => void;
  'player.squadChange': (name: string, teamId: number, squadId: number) => void;
  'player.teamChange': (name: string, teamId: number, squadId: number) => void;
  'player.kill': (killer: string, victim: string, weapon: string, headshot: boolean) => void;
  'player.chat': (name: string, text: string, subset: string[]) => void;

  // Server events (from BF.js)
  'server.levelLoaded': (map: string, mode: string, roundNum: number, roundsTotal: number) => void;
  'server.roundOver': (winningTeamId: number) => void;
  'server.roundOverPlayers': (players: TabularResult) => void;
  'server.roundOverTeamScores': (scores: number[], targetScore: number) => void;
}

/**
 * Adapter that bridges BattleCon events to AdKats internal events.
 * Also provides typed wrappers for common RCON commands.
 */
export class BattleConAdapter {
  private bc: BattleConClient;
  private eventBus: AdKatsEventBus;
  private playerService: PlayerService;
  private logger: Logger;
  private connected = false;

  constructor(
    bc: BattleConClient,
    eventBus: AdKatsEventBus,
    playerService: PlayerService,
    logger: Logger
  ) {
    this.bc = bc;
    this.eventBus = eventBus;
    this.playerService = playerService;
    this.logger = logger;
  }

  /**
   * Set up event listeners to bridge BattleCon events to AdKats events.
   */
  initialize(): void {
    this.logger.info('Initializing BattleCon adapter');

    // Connection events
    this.bc.on('ready', () => {
      this.connected = true;
      this.logger.info('BattleCon ready');
    });

    this.bc.on('close', () => {
      this.connected = false;
      this.logger.info('BattleCon connection closed');
    });

    this.bc.on('error', (error) => {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'BattleCon error');
    });

    // Player events
    this.bc.on('player.join', (name, guid) => {
      void this.handlePlayerJoin(name, guid);
    });

    this.bc.on('player.leave', (name) => {
      this.handlePlayerLeave(name);
    });

    this.bc.on('player.spawn', (name, teamId) => {
      this.handlePlayerSpawn(name, teamId);
    });

    this.bc.on('player.kill', (killer, victim, weapon, headshot) => {
      this.handlePlayerKill(killer, victim, weapon, headshot);
    });

    this.bc.on('player.chat', (name, text, subset) => {
      this.handlePlayerChat(name, text, subset);
    });

    this.bc.on('player.teamChange', (name, teamId, squadId) => {
      this.handlePlayerTeamChange(name, teamId, squadId);
    });

    this.bc.on('player.squadChange', (name, teamId, squadId) => {
      this.handlePlayerSquadChange(name, teamId, squadId);
    });

    this.bc.on('player.authenticated', (name) => {
      this.handlePlayerAuthenticated(name);
    });

    // Server events
    this.bc.on('server.levelLoaded', (map, mode, roundNum, roundsTotal) => {
      this.eventBus.emitEvent('server:levelLoaded', map, mode, roundNum, roundsTotal);
    });

    this.bc.on('server.roundOver', (winningTeamId) => {
      this.eventBus.emitEvent('server:roundOver', winningTeamId);
    });

    this.bc.on('server.roundOverPlayers', (players) => {
      this.eventBus.emitEvent('server:roundOverPlayers', players);
    });

    this.bc.on('server.roundOverTeamScores', (scores, targetScore) => {
      this.eventBus.emitEvent('server:roundOverTeamScores', scores, targetScore);
    });

    this.logger.info('BattleCon adapter initialized');
  }

  /**
   * Check if connected to the game server.
   */
  isConnected(): boolean {
    return this.connected && this.bc.loggedIn;
  }

  // =====================================================
  // RCON Command Wrappers
  // =====================================================

  /**
   * Kill a player.
   */
  async killPlayer(name: string): Promise<void> {
    await this.bc.exec(['admin.killPlayer', name]);
  }

  /**
   * Kick a player with a reason.
   */
  async kickPlayer(name: string, reason: string): Promise<void> {
    await this.bc.exec(['admin.kickPlayer', name, reason]);
  }

  /**
   * Ban a player by GUID.
   */
  async banByGuid(guid: string, type: 'perm' | 'rounds' | 'seconds', duration: number | null, reason: string): Promise<void> {
    const cmd = ['banList.add', 'guid', guid, type];
    if (duration !== null) {
      cmd.push(String(duration));
    }
    cmd.push(reason);
    await this.bc.exec(cmd);
    await this.bc.exec(['banList.save']);
  }

  /**
   * Ban a player by name.
   */
  async banByName(name: string, type: 'perm' | 'rounds' | 'seconds', duration: number | null, reason: string): Promise<void> {
    const cmd = ['banList.add', 'name', name, type];
    if (duration !== null) {
      cmd.push(String(duration));
    }
    cmd.push(reason);
    await this.bc.exec(cmd);
    await this.bc.exec(['banList.save']);
  }

  /**
   * Ban a player by IP.
   */
  async banByIp(ip: string, type: 'perm' | 'rounds' | 'seconds', duration: number | null, reason: string): Promise<void> {
    const cmd = ['banList.add', 'ip', ip, type];
    if (duration !== null) {
      cmd.push(String(duration));
    }
    cmd.push(reason);
    await this.bc.exec(cmd);
    await this.bc.exec(['banList.save']);
  }

  /**
   * Unban a player by GUID.
   */
  async unbanByGuid(guid: string): Promise<void> {
    await this.bc.exec(['banList.remove', 'guid', guid]);
    await this.bc.exec(['banList.save']);
  }

  /**
   * Move a player to another team.
   */
  async movePlayer(name: string, teamId: number, squadId: number, forceKill: boolean): Promise<void> {
    await this.bc.exec([
      'admin.movePlayer',
      name,
      String(teamId),
      String(squadId),
      forceKill ? 'true' : 'false',
    ]);
  }

  /**
   * Send a global chat message.
   */
  async say(message: string): Promise<void> {
    await this.bc.exec(['admin.say', message, 'all']);
  }

  /**
   * Send a message to a specific team.
   */
  async sayTeam(message: string, teamId: number): Promise<void> {
    await this.bc.exec(['admin.say', message, 'team', String(teamId)]);
  }

  /**
   * Send a message to a specific squad.
   */
  async saySquad(message: string, teamId: number, squadId: number): Promise<void> {
    await this.bc.exec(['admin.say', message, 'squad', String(teamId), String(squadId)]);
  }

  /**
   * Send a private message to a player.
   */
  async sayPlayer(message: string, playerName: string): Promise<void> {
    await this.bc.exec(['admin.say', message, 'player', playerName]);
  }

  /**
   * Yell a global message.
   */
  async yell(message: string, duration: number = 5): Promise<void> {
    await this.bc.exec(['admin.yell', message, String(duration), 'all']);
  }

  /**
   * Yell a message to a specific player.
   */
  async yellPlayer(message: string, playerName: string, duration: number = 5): Promise<void> {
    await this.bc.exec(['admin.yell', message, String(duration), 'player', playerName]);
  }

  /**
   * End the current round.
   */
  async endRound(winningTeamId: number): Promise<void> {
    await this.bc.exec(['mapList.endRound', String(winningTeamId)]);
  }

  /**
   * Restart the current round.
   */
  async restartRound(): Promise<void> {
    await this.bc.exec(['mapList.restartRound']);
  }

  /**
   * Skip to the next map.
   */
  async nextLevel(): Promise<void> {
    await this.bc.exec(['mapList.runNextRound']);
  }

  /**
   * Get the current player list.
   */
  async listPlayers(): Promise<PlayerInfo[]> {
    return this.bc.listPlayers();
  }

  /**
   * Get server info.
   */
  async getServerInfo(): Promise<string[]> {
    return this.bc.serverInfo();
  }

  // =====================================================
  // Event Handlers
  // =====================================================

  private async handlePlayerJoin(name: string, guid: string): Promise<void> {
    this.logger.debug({ name, guid }, 'Player joining');

    // Get or create player record
    const player = await this.playerService.getOrCreatePlayer(name, guid);
    player.isOnline = true;

    this.eventBus.emitEvent('player:join', player);
  }

  private handlePlayerLeave(name: string): void {
    const player = this.playerService.getOnlinePlayer(name);
    if (!player) {
      this.logger.warn({ name }, 'Player leave event for unknown player');
      return;
    }

    player.isOnline = false;
    this.playerService.removeOnlinePlayer(name);
    this.eventBus.emitEvent('player:leave', player);
  }

  private handlePlayerSpawn(name: string, teamId: number): void {
    const player = this.playerService.getOnlinePlayer(name);
    if (!player) {
      this.logger.warn({ name }, 'Player spawn event for unknown player');
      return;
    }

    player.teamId = teamId;
    player.isAlive = true;
    this.eventBus.emitEvent('player:spawn', player, teamId);
  }

  private handlePlayerKill(killerName: string, victimName: string, weapon: string, headshot: boolean): void {
    const killer = killerName ? this.playerService.getOnlinePlayer(killerName) : null;
    const victim = this.playerService.getOnlinePlayer(victimName);

    if (!victim) {
      this.logger.warn({ victimName }, 'Kill event for unknown victim');
      return;
    }

    victim.isAlive = false;
    this.eventBus.emitEvent('player:kill', killer ?? null, victim, weapon, headshot);
  }

  private handlePlayerChat(name: string, text: string, subset: string[]): void {
    const player = this.playerService.getOnlinePlayer(name);
    if (!player) {
      this.logger.warn({ name }, 'Chat event for unknown player');
      return;
    }

    this.eventBus.emitEvent('player:chat', player, text, subset);
  }

  private handlePlayerTeamChange(name: string, teamId: number, squadId: number): void {
    const player = this.playerService.getOnlinePlayer(name);
    if (!player) {
      this.logger.warn({ name }, 'Team change event for unknown player');
      return;
    }

    player.teamId = teamId;
    player.squadId = squadId;
    this.eventBus.emitEvent('player:teamChange', player, teamId, squadId);
  }

  private handlePlayerSquadChange(name: string, teamId: number, squadId: number): void {
    const player = this.playerService.getOnlinePlayer(name);
    if (!player) {
      this.logger.warn({ name }, 'Squad change event for unknown player');
      return;
    }

    player.squadId = squadId;
    this.eventBus.emitEvent('player:squadChange', player, teamId, squadId);
  }

  private handlePlayerAuthenticated(name: string): void {
    const player = this.playerService.getOnlinePlayer(name);
    if (!player) {
      this.logger.warn({ name }, 'Authenticated event for unknown player');
      return;
    }

    this.eventBus.emitEvent('player:authenticated', player);
  }
}

/**
 * Create a new BattleCon adapter.
 */
export function createBattleConAdapter(
  bc: BattleConClient,
  eventBus: AdKatsEventBus,
  playerService: PlayerService,
  logger: Logger
): BattleConAdapter {
  return new BattleConAdapter(bc, eventBus, playerService, logger);
}
