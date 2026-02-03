import { pino, type Logger as PinoLogger } from 'pino';

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';

export interface LoggerOptions {
  level: LogLevel;
  pretty: boolean;
  serverName?: string;
}

const defaultOptions: LoggerOptions = {
  level: 'info',
  pretty: process.env['NODE_ENV'] !== 'production',
};

/**
 * Create a logger instance for AdKats.
 * Uses pino for high-performance structured logging.
 */
export function createLogger(options: Partial<LoggerOptions> = {}) {
  const opts = { ...defaultOptions, ...options };

  const transport = opts.pretty
    ? {
        target: 'pino-pretty',
        options: {
          colorize: true,
          translateTime: 'SYS:standard',
          ignore: 'pid,hostname',
        },
      }
    : undefined;

  const logger = pino({
    level: opts.level,
    transport,
    base: opts.serverName ? { server: opts.serverName } : undefined,
  });

  return logger;
}

/**
 * Create a child logger with additional context.
 */
export function createChildLogger(
  parent: PinoLogger,
  context: Record<string, unknown>
) {
  return parent.child(context);
}

export type Logger = PinoLogger;

// Default logger instance
export const logger = createLogger();
