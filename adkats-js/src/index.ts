/**
 * AdKats TypeScript Plugin for BattleCon
 *
 * Advanced in-game admin and ban enforcement plugin for Battlefield servers.
 *
 * @packageDocumentation
 */

import type { BattleConClient } from './core/battlecon-adapter.js';
import type { AdKatsConfig } from './core/config.js';

import { AdKatsPlugin, createAdKatsPlugin } from './plugin.js';
import { parseConfig, parseConfigFromEnv, getDefaultConfig } from './core/config.js';

// Re-export types
export type { AdKatsConfig } from './core/config.js';
export type { AdKatsPluginOptions } from './plugin.js';
export type { BattleConClient, BattleConEvents, PlayerInfo } from './core/battlecon-adapter.js';
export type { Logger, LogLevel, LoggerOptions } from './core/logger.js';
export type { AdKatsEvents, AdKatsEventBus } from './core/event-bus.js';

// Re-export models
export * from './models/index.js';

// Re-export utilities
export { parseConfig, parseConfigFromEnv, getDefaultConfig };
export { createLogger, createChildLogger } from './core/logger.js';
export { createEventBus } from './core/event-bus.js';
export { createScheduler } from './core/scheduler.js';
export { createDatabase } from './database/connection.js';
export { levenshteinDistance, findBestMatch, findAllMatches } from './utils/levenshtein.js';
export { formatDuration, parseDuration, formatRelative } from './utils/time.js';

// Re-export plugin
export { AdKatsPlugin, createAdKatsPlugin };

/**
 * AdKats plugin factory function for BattleCon.
 *
 * Usage:
 * ```typescript
 * import BattleCon from '@acp/battlecon';
 * import AdKats from '@adkats/plugin';
 *
 * const bc = new BattleCon('host', 47200, 'password');
 * bc.use('BF4');
 *
 * const adkats = AdKats(bc, {
 *   database: {
 *     host: 'localhost',
 *     database: 'adkats',
 *     user: 'root',
 *     password: 'password',
 *   },
 *   serverId: 1,
 * });
 *
 * await bc.connect();
 * await adkats.enable();
 * ```
 */
export default function AdKats(
  bc: BattleConClient,
  config: Partial<AdKatsConfig> & { database: AdKatsConfig['database']; serverId: number }
): AdKatsPlugin {
  const fullConfig = parseConfig({
    ...getDefaultConfig(),
    ...config,
  });

  return createAdKatsPlugin({
    config: fullConfig,
    battlecon: bc,
  });
}

/**
 * Version information.
 */
export const VERSION = '8.0.0-alpha.1';
export const NAME = 'AdKats';
export const AUTHOR = 'ColColonCleaner';
