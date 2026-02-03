import { z } from 'zod';
import type { LogLevel } from './logger.js';

/**
 * Configuration schema for AdKats plugin.
 * Uses Zod for runtime validation.
 */
export const configSchema = z.object({
  // Database configuration
  database: z.object({
    host: z.string().default('localhost'),
    port: z.number().int().positive().default(3306),
    database: z.string(),
    user: z.string(),
    password: z.string(),
    connectionLimit: z.number().int().positive().default(10),
  }),

  // Logging
  logLevel: z.enum(['trace', 'debug', 'info', 'warn', 'error', 'fatal']).default('info'),
  logPretty: z.boolean().default(true),

  // Server identification (for multi-server setups)
  serverId: z.number().int().positive(),
  serverName: z.string().optional(),

  // Command settings
  commandPrefix: z.string().default('@'),
  commandPrefixAlternates: z.array(z.string()).default(['!', '/']),

  // Feature toggles
  enableBanEnforcer: z.boolean().default(true),
  enableAntiCheat: z.boolean().default(true),
  enableReputationSystem: z.boolean().default(true),
  enablePingEnforcer: z.boolean().default(false),
  enableAfkManager: z.boolean().default(false),
  enableSpamBot: z.boolean().default(false),
  enableTeamSwap: z.boolean().default(true),

  // Ban enforcer settings
  banEnforcer: z.object({
    checkIntervalMs: z.number().int().positive().default(30000),
    enforceGuid: z.boolean().default(true),
    enforceIp: z.boolean().default(true),
    enforceName: z.boolean().default(true),
  }).default({}),

  // Punishment settings
  punishment: z.object({
    hierarchy: z.array(z.string()).default([
      'warn',
      'kill',
      'kick',
      'tban60',
      'tban120',
      'tbanday',
      'tbanweek',
      'tban2weeks',
      'tbanmonth',
      'ban',
    ]),
    iroTimeoutMinutes: z.number().int().positive().default(10),
    iroMultiplier: z.number().positive().default(1),
  }).default({}),

  // Ping enforcer settings
  pingEnforcer: z.object({
    maxPing: z.number().int().positive().default(200),
    warningCount: z.number().int().nonnegative().default(3),
    checkIntervalMs: z.number().int().positive().default(30000),
    gracePeriodMs: z.number().int().nonnegative().default(60000),
  }).default({}),

  // External integrations
  discord: z.object({
    webhookUrl: z.string().url().optional(),
    reportChannelWebhook: z.string().url().optional(),
    adminChannelWebhook: z.string().url().optional(),
  }).default({}),

  email: z.object({
    enabled: z.boolean().default(false),
    smtpHost: z.string().optional(),
    smtpPort: z.number().int().positive().default(587),
    smtpUser: z.string().optional(),
    smtpPassword: z.string().optional(),
    fromAddress: z.string().email().optional(),
    adminAddresses: z.array(z.string().email()).default([]),
  }).default({}),

  // Battlelog integration
  battlelog: z.object({
    enabled: z.boolean().default(true),
    rateLimitMs: z.number().int().positive().default(1000),
    cacheTimeMs: z.number().int().positive().default(300000),
  }).default({}),
});

export type AdKatsConfig = z.infer<typeof configSchema>;

/**
 * Parse and validate configuration from environment variables or object.
 */
export function parseConfig(input: unknown): AdKatsConfig {
  return configSchema.parse(input);
}

/**
 * Parse configuration from environment variables.
 */
export function parseConfigFromEnv(): AdKatsConfig {
  return parseConfig({
    database: {
      host: process.env['DB_HOST'] ?? 'localhost',
      port: parseInt(process.env['DB_PORT'] ?? '3306', 10),
      database: process.env['DB_DATABASE'] ?? '',
      user: process.env['DB_USER'] ?? '',
      password: process.env['DB_PASSWORD'] ?? '',
      connectionLimit: parseInt(process.env['DB_CONNECTION_LIMIT'] ?? '10', 10),
    },
    logLevel: (process.env['LOG_LEVEL'] ?? 'info') as LogLevel,
    logPretty: process.env['LOG_PRETTY'] !== 'false',
    serverId: parseInt(process.env['SERVER_ID'] ?? '1', 10),
    serverName: process.env['SERVER_NAME'],
    commandPrefix: process.env['COMMAND_PREFIX'] ?? '@',
  });
}

/**
 * Default configuration for development/testing.
 */
export function getDefaultConfig(overrides: Partial<AdKatsConfig> = {}): AdKatsConfig {
  return parseConfig({
    database: {
      host: 'localhost',
      port: 3306,
      database: 'adkats',
      user: 'root',
      password: '',
    },
    serverId: 1,
    serverName: 'Test Server',
    logLevel: 'debug',
    ...overrides,
  });
}
