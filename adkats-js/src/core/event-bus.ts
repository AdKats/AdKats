import { EventEmitter } from 'node:events';
import type { Logger } from './logger.js';
import type { APlayer } from '../models/player.js';
import type { ARecord } from '../models/record.js';

/**
 * AdKats internal event types.
 * These events are used for communication between components.
 */
export interface AdKatsEvents {
  // Player events
  'player:join': (player: APlayer) => void;
  'player:leave': (player: APlayer) => void;
  'player:spawn': (player: APlayer, teamId: number) => void;
  'player:kill': (killer: APlayer | null, victim: APlayer, weapon: string, headshot: boolean) => void;
  'player:chat': (player: APlayer, message: string, subset: string[]) => void;
  'player:teamChange': (player: APlayer, teamId: number, squadId: number) => void;
  'player:squadChange': (player: APlayer, teamId: number, squadId: number) => void;
  'player:authenticated': (player: APlayer) => void;

  // Server events
  'server:levelLoaded': (map: string, mode: string, roundNum: number, roundsTotal: number) => void;
  'server:roundOver': (winningTeamId: number) => void;
  'server:roundOverPlayers': (players: unknown[]) => void;
  'server:roundOverTeamScores': (scores: number[], targetScore: number) => void;

  // Command events
  'command:parsed': (record: ARecord) => void;
  'command:executed': (record: ARecord) => void;
  'command:failed': (record: ARecord, error: Error) => void;

  // Action events
  'action:pending': (record: ARecord) => void;
  'action:executed': (record: ARecord) => void;
  'action:failed': (record: ARecord, error: Error) => void;

  // Ban events
  'ban:issued': (record: ARecord) => void;
  'ban:enforced': (player: APlayer, record: ARecord) => void;
  'ban:removed': (record: ARecord) => void;

  // Report events
  'report:created': (report: unknown) => void;
  'report:accepted': (report: unknown, admin: APlayer) => void;
  'report:denied': (report: unknown, admin: APlayer, reason: string) => void;
  'report:ignored': (report: unknown, admin: APlayer) => void;

  // Plugin lifecycle
  'plugin:enabled': () => void;
  'plugin:disabled': () => void;
  'plugin:error': (error: Error) => void;
}

/**
 * Type-safe event emitter for AdKats internal events.
 */
export class AdKatsEventBus extends EventEmitter {
  private logger?: Logger;

  constructor(logger?: Logger) {
    super();
    this.logger = logger;
    this.setMaxListeners(50); // AdKats has many event listeners
  }

  /**
   * Emit an event with type safety.
   */
  emitEvent<K extends keyof AdKatsEvents>(
    event: K,
    ...args: Parameters<AdKatsEvents[K]>
  ): boolean {
    this.logger?.trace({ event, args }, 'Event emitted');
    return this.emit(event, ...args);
  }

  /**
   * Listen for an event with type safety.
   */
  onEvent<K extends keyof AdKatsEvents>(
    event: K,
    listener: AdKatsEvents[K]
  ): this {
    return this.on(event, listener as (...args: unknown[]) => void);
  }

  /**
   * Listen for an event once with type safety.
   */
  onceEvent<K extends keyof AdKatsEvents>(
    event: K,
    listener: AdKatsEvents[K]
  ): this {
    return this.once(event, listener as (...args: unknown[]) => void);
  }

  /**
   * Remove an event listener with type safety.
   */
  offEvent<K extends keyof AdKatsEvents>(
    event: K,
    listener: AdKatsEvents[K]
  ): this {
    return this.off(event, listener as (...args: unknown[]) => void);
  }
}

/**
 * Create a new event bus instance.
 */
export function createEventBus(logger?: Logger): AdKatsEventBus {
  return new AdKatsEventBus(logger);
}
