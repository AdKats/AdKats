import mysql, { type Pool, type PoolConnection, type RowDataPacket, type ResultSetHeader } from 'mysql2/promise';
import type { Logger } from '../core/logger.js';
import type { AdKatsConfig } from '../core/config.js';

/**
 * Database connection pool wrapper.
 * Provides typed query methods and connection management.
 */
export class Database {
  private pool: Pool;
  private logger: Logger;
  private connected = false;

  constructor(config: AdKatsConfig['database'], logger: Logger) {
    this.logger = logger;

    this.pool = mysql.createPool({
      host: config.host,
      port: config.port,
      database: config.database,
      user: config.user,
      password: config.password,
      waitForConnections: true,
      connectionLimit: config.connectionLimit,
      queueLimit: 0,
      enableKeepAlive: true,
      keepAliveInitialDelay: 10000,
      // Use named placeholders for cleaner queries
      namedPlaceholders: true,
    });

    this.logger.info(
      { host: config.host, port: config.port, database: config.database },
      'Database pool created'
    );
  }

  /**
   * Test the database connection.
   */
  async connect(): Promise<void> {
    try {
      const conn = await this.pool.getConnection();
      this.logger.info('Database connection established');
      conn.release();
      this.connected = true;
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Database connection failed');
      throw error;
    }
  }

  /**
   * Close all connections in the pool.
   */
  async close(): Promise<void> {
    await this.pool.end();
    this.connected = false;
    this.logger.info('Database pool closed');
  }

  /**
   * Check if connected to the database.
   */
  isConnected(): boolean {
    return this.connected;
  }

  /**
   * Execute a query and return typed results.
   */
  async query<T extends RowDataPacket[]>(
    sql: string,
    params?: Record<string, unknown> | unknown[]
  ): Promise<T> {
    const [rows] = await this.pool.execute<T>(sql, params);
    return rows;
  }

  /**
   * Execute an INSERT/UPDATE/DELETE and return the result.
   */
  async execute(
    sql: string,
    params?: Record<string, unknown> | unknown[]
  ): Promise<ResultSetHeader> {
    const [result] = await this.pool.execute<ResultSetHeader>(sql, params);
    return result;
  }

  /**
   * Execute a query and return the first row or null.
   */
  async queryOne<T extends RowDataPacket>(
    sql: string,
    params?: Record<string, unknown> | unknown[]
  ): Promise<T | null> {
    const rows = await this.query<T[]>(sql, params);
    return rows[0] ?? null;
  }

  /**
   * Get a connection from the pool for transactions.
   */
  async getConnection(): Promise<PoolConnection> {
    return this.pool.getConnection();
  }

  /**
   * Execute multiple queries in a transaction.
   */
  async transaction<T>(
    callback: (conn: PoolConnection) => Promise<T>
  ): Promise<T> {
    const conn = await this.pool.getConnection();
    try {
      await conn.beginTransaction();
      const result = await callback(conn);
      await conn.commit();
      return result;
    } catch (error) {
      await conn.rollback();
      throw error;
    } finally {
      conn.release();
    }
  }

  /**
   * Get the underlying pool (for direct access if needed).
   */
  getPool(): Pool {
    return this.pool;
  }
}

/**
 * Create a new database instance.
 */
export function createDatabase(
  config: AdKatsConfig['database'],
  logger: Logger
): Database {
  return new Database(config, logger);
}
