import cron from 'node-cron';
import type { Logger } from './logger.js';

/**
 * Job definition for the scheduler.
 */
export interface ScheduledJob {
  id: string;
  name: string;
  cronExpression?: string;
  intervalMs?: number;
  handler: () => Promise<void> | void;
  enabled: boolean;
  lastRun?: Date;
  nextRun?: Date;
  runCount: number;
  errorCount: number;
}

/**
 * Scheduler for periodic tasks.
 * Replaces C# thread-based processing with async jobs.
 */
export class Scheduler {
  private jobs: Map<string, ScheduledJob> = new Map();
  private cronTasks: Map<string, cron.ScheduledTask> = new Map();
  private intervals: Map<string, NodeJS.Timeout> = new Map();
  private running = false;
  private logger: Logger;

  constructor(logger: Logger) {
    this.logger = logger;
  }

  /**
   * Register a cron-based job.
   * @param id Unique job identifier
   * @param name Human-readable job name
   * @param cronExpression Cron expression (e.g., '* /5 * * * *' for every 5 minutes)
   * @param handler Async function to execute
   */
  registerCronJob(
    id: string,
    name: string,
    cronExpression: string,
    handler: () => Promise<void> | void
  ): void {
    if (this.jobs.has(id)) {
      throw new Error(`Job with id '${id}' already exists`);
    }

    const job: ScheduledJob = {
      id,
      name,
      cronExpression,
      handler,
      enabled: true,
      runCount: 0,
      errorCount: 0,
    };

    this.jobs.set(id, job);
    this.logger.debug({ jobId: id, cron: cronExpression }, `Registered cron job: ${name}`);
  }

  /**
   * Register an interval-based job.
   * @param id Unique job identifier
   * @param name Human-readable job name
   * @param intervalMs Interval in milliseconds
   * @param handler Async function to execute
   */
  registerIntervalJob(
    id: string,
    name: string,
    intervalMs: number,
    handler: () => Promise<void> | void
  ): void {
    if (this.jobs.has(id)) {
      throw new Error(`Job with id '${id}' already exists`);
    }

    const job: ScheduledJob = {
      id,
      name,
      intervalMs,
      handler,
      enabled: true,
      runCount: 0,
      errorCount: 0,
    };

    this.jobs.set(id, job);
    this.logger.debug({ jobId: id, intervalMs }, `Registered interval job: ${name}`);
  }

  /**
   * Start all registered jobs.
   */
  start(): void {
    if (this.running) {
      this.logger.warn('Scheduler is already running');
      return;
    }

    this.running = true;
    this.logger.info('Starting scheduler');

    for (const [id, job] of this.jobs) {
      if (!job.enabled) continue;

      if (job.cronExpression) {
        this.startCronJob(id, job);
      } else if (job.intervalMs) {
        this.startIntervalJob(id, job);
      }
    }

    this.logger.info(`Scheduler started with ${this.jobs.size} jobs`);
  }

  /**
   * Stop all running jobs.
   */
  stop(): void {
    if (!this.running) {
      return;
    }

    this.logger.info('Stopping scheduler');

    // Stop all cron tasks
    for (const [id, task] of this.cronTasks) {
      task.stop();
      this.logger.debug({ jobId: id }, 'Stopped cron job');
    }
    this.cronTasks.clear();

    // Stop all intervals
    for (const [id, interval] of this.intervals) {
      clearInterval(interval);
      this.logger.debug({ jobId: id }, 'Stopped interval job');
    }
    this.intervals.clear();

    this.running = false;
    this.logger.info('Scheduler stopped');
  }

  /**
   * Enable a job.
   */
  enableJob(id: string): void {
    const job = this.jobs.get(id);
    if (!job) {
      throw new Error(`Job '${id}' not found`);
    }

    job.enabled = true;

    if (this.running) {
      if (job.cronExpression) {
        this.startCronJob(id, job);
      } else if (job.intervalMs) {
        this.startIntervalJob(id, job);
      }
    }

    this.logger.info({ jobId: id }, `Enabled job: ${job.name}`);
  }

  /**
   * Disable a job.
   */
  disableJob(id: string): void {
    const job = this.jobs.get(id);
    if (!job) {
      throw new Error(`Job '${id}' not found`);
    }

    job.enabled = false;

    // Stop if running
    const cronTask = this.cronTasks.get(id);
    if (cronTask) {
      cronTask.stop();
      this.cronTasks.delete(id);
    }

    const interval = this.intervals.get(id);
    if (interval) {
      clearInterval(interval);
      this.intervals.delete(id);
    }

    this.logger.info({ jobId: id }, `Disabled job: ${job.name}`);
  }

  /**
   * Run a job immediately (outside of schedule).
   */
  async runJob(id: string): Promise<void> {
    const job = this.jobs.get(id);
    if (!job) {
      throw new Error(`Job '${id}' not found`);
    }

    await this.executeJob(job);
  }

  /**
   * Get status of all jobs.
   */
  getJobStatus(): ScheduledJob[] {
    return Array.from(this.jobs.values());
  }

  private startCronJob(id: string, job: ScheduledJob): void {
    if (!job.cronExpression) return;

    const task = cron.schedule(job.cronExpression, () => {
      void this.executeJob(job);
    });

    this.cronTasks.set(id, task);
    this.logger.debug({ jobId: id }, `Started cron job: ${job.name}`);
  }

  private startIntervalJob(id: string, job: ScheduledJob): void {
    if (!job.intervalMs) return;

    // Run immediately on start
    void this.executeJob(job);

    const interval = setInterval(() => {
      void this.executeJob(job);
    }, job.intervalMs);

    this.intervals.set(id, interval);
    this.logger.debug({ jobId: id }, `Started interval job: ${job.name}`);
  }

  private async executeJob(job: ScheduledJob): Promise<void> {
    const startTime = Date.now();
    job.lastRun = new Date();

    try {
      await job.handler();
      job.runCount++;

      const duration = Date.now() - startTime;
      this.logger.trace({ jobId: job.id, durationMs: duration }, `Job completed: ${job.name}`);
    } catch (error) {
      job.errorCount++;
      this.logger.error(
        { jobId: job.id, error: error instanceof Error ? error.message : String(error) },
        `Job failed: ${job.name}`
      );
    }
  }
}

/**
 * Create a new scheduler instance.
 */
export function createScheduler(logger: Logger): Scheduler {
  return new Scheduler(logger);
}
