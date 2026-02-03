import type { Logger } from '../core/logger.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { RecordRepository } from '../database/repositories/record.repository.js';
import type { APlayer } from '../models/player.js';
import type { ARecord } from '../models/record.js';
import type { ACommand } from '../models/command.js';
import type { DiscordService, DiscordReportData } from '../integrations/discord.js';
import { createRecord, RecordSource } from '../models/record.js';

/**
 * Pending report data structure.
 */
export interface PendingReport {
  reportId: number;           // Record ID from database
  sourcePlayer: APlayer;
  targetPlayer: APlayer;
  reason: string;
  timestamp: Date;
  serverId: number;
  roundId: number;            // Track which round
}

/**
 * Report configuration options.
 */
export interface ReportServiceConfig {
  /** Time in milliseconds before a report expires (default: 1 hour) */
  reportExpirationMs: number;
  /** Maximum reports per player per round (default: 3) */
  maxReportsPerPlayerPerRound: number;
  /** Server name for notifications */
  serverName: string;
  /** Server ID */
  serverId: number;
}

const DEFAULT_CONFIG: ReportServiceConfig = {
  reportExpirationMs: 60 * 60 * 1000, // 1 hour
  maxReportsPerPlayerPerRound: 3,
  serverName: 'Unknown Server',
  serverId: 1,
};

/**
 * Report action result.
 */
export interface ReportActionResult {
  success: boolean;
  message: string;
  report?: PendingReport;
}

/**
 * Report service - manages player reports and admin responses.
 */
export class ReportService {
  private logger: Logger;
  private eventBus: AdKatsEventBus;
  private bcAdapter: BattleConAdapter;
  private recordRepo: RecordRepository;
  private discordService: DiscordService | null;
  private config: ReportServiceConfig;

  // Pending reports by reportId
  private pendingReports: Map<number, PendingReport> = new Map();

  // Track report counts per player per round (key: `${playerId}-${roundId}`)
  private playerReportCounts: Map<string, number> = new Map();

  // Current round ID (incremented on round change)
  private currentRoundId: number = 0;

  // Expiration cleanup interval
  private cleanupInterval: ReturnType<typeof setInterval> | null = null;

  // Command type IDs for report-related commands
  private reportCommandId: number = 18;  // player_report
  private acceptCommandId: number = 40;  // admin_accept
  private denyCommandId: number = 41;    // admin_deny
  private ignoreCommandId: number = 61;  // admin_ignore

  constructor(
    logger: Logger,
    eventBus: AdKatsEventBus,
    bcAdapter: BattleConAdapter,
    recordRepo: RecordRepository,
    discordService: DiscordService | null,
    config: Partial<ReportServiceConfig> = {}
  ) {
    this.logger = logger;
    this.eventBus = eventBus;
    this.bcAdapter = bcAdapter;
    this.recordRepo = recordRepo;
    this.discordService = discordService;
    this.config = { ...DEFAULT_CONFIG, ...config };
  }

  /**
   * Initialize the report service.
   */
  async initialize(): Promise<void> {
    // Load pending reports from database
    await this.loadPendingReports();

    // Start cleanup interval (every 5 minutes)
    this.cleanupInterval = setInterval(() => {
      this.cleanupExpiredReports();
    }, 5 * 60 * 1000);

    // Listen for round changes
    this.eventBus.on('server:roundOver', () => {
      this.onRoundChange();
    });

    this.logger.info(
      { pendingCount: this.pendingReports.size },
      'Report service initialized'
    );
  }

  /**
   * Shutdown the report service.
   */
  shutdown(): void {
    if (this.cleanupInterval) {
      clearInterval(this.cleanupInterval);
      this.cleanupInterval = null;
    }
  }

  /**
   * Set command IDs from loaded commands.
   */
  setCommandIds(commands: {
    reportId?: number;
    acceptId?: number;
    denyId?: number;
    ignoreId?: number;
  }): void {
    if (commands.reportId) this.reportCommandId = commands.reportId;
    if (commands.acceptId) this.acceptCommandId = commands.acceptId;
    if (commands.denyId) this.denyCommandId = commands.denyId;
    if (commands.ignoreId) this.ignoreCommandId = commands.ignoreId;
  }

  /**
   * Create a new player report.
   */
  async createReport(
    source: APlayer,
    target: APlayer,
    reason: string,
    command: ACommand | null
  ): Promise<ReportActionResult> {
    // Check if player has exceeded report limit for this round
    const reportKey = `${source.playerId}-${this.currentRoundId}`;
    const currentCount = this.playerReportCounts.get(reportKey) ?? 0;

    if (currentCount >= this.config.maxReportsPerPlayerPerRound) {
      return {
        success: false,
        message: `You have reached the maximum of ${this.config.maxReportsPerPlayerPerRound} reports per round`,
      };
    }

    // Check if player is trying to report themselves
    if (source.playerId === target.playerId) {
      return {
        success: false,
        message: 'You cannot report yourself',
      };
    }

    // Check if there's already a pending report for this target from this source
    for (const report of this.pendingReports.values()) {
      if (
        report.sourcePlayer.playerId === source.playerId &&
        report.targetPlayer.playerId === target.playerId
      ) {
        return {
          success: false,
          message: `You already have a pending report against ${target.soldierName}`,
        };
      }
    }

    // Create the record in database
    const record = createRecord(
      this.config.serverId,
      source,
      target,
      command,
      reason || 'No reason provided'
    );
    record.commandType = this.reportCommandId;
    record.commandAction = this.reportCommandId;
    record.externalSource = RecordSource.InGame;

    try {
      const savedRecord = await this.recordRepo.create(record);

      // Create pending report
      const pendingReport: PendingReport = {
        reportId: savedRecord.recordId,
        sourcePlayer: source,
        targetPlayer: target,
        reason: reason || 'No reason provided',
        timestamp: new Date(),
        serverId: this.config.serverId,
        roundId: this.currentRoundId,
      };

      // Add to pending reports
      this.pendingReports.set(savedRecord.recordId, pendingReport);

      // Increment report count for source player
      this.playerReportCounts.set(reportKey, currentCount + 1);

      // Notify admins
      await this.notifyAdmins(pendingReport);

      // Emit event
      this.eventBus.emitEvent('report:created', pendingReport);

      this.logger.info(
        {
          reportId: savedRecord.recordId,
          source: source.soldierName,
          target: target.soldierName,
          reason,
        },
        'Report created'
      );

      return {
        success: true,
        message: `Report #${savedRecord.recordId} submitted against ${target.soldierName}`,
        report: pendingReport,
      };

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to create report');
      return {
        success: false,
        message: 'Failed to submit report due to a system error',
      };
    }
  }

  /**
   * Accept a report (admin action).
   */
  async acceptReport(
    admin: APlayer,
    reportId?: number,
    command?: ACommand
  ): Promise<ReportActionResult> {
    const report = this.findReport(reportId, admin);

    if (!report) {
      return {
        success: false,
        message: reportId
          ? `Report #${reportId} not found or already handled`
          : 'No pending reports found',
      };
    }

    // Remove from pending reports
    this.pendingReports.delete(report.reportId);

    // Create accept record
    const record = createRecord(
      this.config.serverId,
      admin,
      report.targetPlayer,
      command ?? null,
      `Accepted report #${report.reportId}: ${report.reason}`
    );
    record.commandType = this.acceptCommandId;
    record.commandAction = this.acceptCommandId;

    try {
      await this.recordRepo.create(record);

      // Send Discord notification
      if (this.discordService) {
        await this.discordService.sendReportActionNotification(
          report.reportId,
          'accepted',
          admin.soldierName,
          report.targetPlayer.soldierName
        );
      }

      // Emit event
      this.eventBus.emitEvent('report:accepted', report, admin);

      this.logger.info(
        {
          reportId: report.reportId,
          admin: admin.soldierName,
          target: report.targetPlayer.soldierName,
        },
        'Report accepted'
      );

      return {
        success: true,
        message: `Accepted report #${report.reportId} against ${report.targetPlayer.soldierName}`,
        report,
      };

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to accept report');
      return {
        success: false,
        message: 'Failed to accept report due to a system error',
      };
    }
  }

  /**
   * Deny a report (admin action).
   */
  async denyReport(
    admin: APlayer,
    reportId: number | undefined,
    reason: string,
    command?: ACommand
  ): Promise<ReportActionResult> {
    const report = this.findReport(reportId, admin);

    if (!report) {
      return {
        success: false,
        message: reportId
          ? `Report #${reportId} not found or already handled`
          : 'No pending reports found',
      };
    }

    // Remove from pending reports
    this.pendingReports.delete(report.reportId);

    // Create deny record
    const record = createRecord(
      this.config.serverId,
      admin,
      report.targetPlayer,
      command ?? null,
      `Denied report #${report.reportId}: ${reason || 'No reason'}`
    );
    record.commandType = this.denyCommandId;
    record.commandAction = this.denyCommandId;

    try {
      await this.recordRepo.create(record);

      // Notify the reporter that their report was denied
      await this.bcAdapter.sayPlayer(
        `Your report #${report.reportId} against ${report.targetPlayer.soldierName} was denied${reason ? `: ${reason}` : ''}`,
        report.sourcePlayer.soldierName
      );

      // Send Discord notification
      if (this.discordService) {
        await this.discordService.sendReportActionNotification(
          report.reportId,
          'denied',
          admin.soldierName,
          report.targetPlayer.soldierName,
          reason
        );
      }

      // Emit event
      this.eventBus.emitEvent('report:denied', report, admin, reason);

      this.logger.info(
        {
          reportId: report.reportId,
          admin: admin.soldierName,
          target: report.targetPlayer.soldierName,
          reason,
        },
        'Report denied'
      );

      return {
        success: true,
        message: `Denied report #${report.reportId} against ${report.targetPlayer.soldierName}`,
        report,
      };

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to deny report');
      return {
        success: false,
        message: 'Failed to deny report due to a system error',
      };
    }
  }

  /**
   * Ignore a report (admin action - no notification to reporter).
   */
  async ignoreReport(
    admin: APlayer,
    reportId?: number,
    command?: ACommand
  ): Promise<ReportActionResult> {
    const report = this.findReport(reportId, admin);

    if (!report) {
      return {
        success: false,
        message: reportId
          ? `Report #${reportId} not found or already handled`
          : 'No pending reports found',
      };
    }

    // Remove from pending reports
    this.pendingReports.delete(report.reportId);

    // Create ignore record
    const record = createRecord(
      this.config.serverId,
      admin,
      report.targetPlayer,
      command ?? null,
      `Ignored report #${report.reportId}: ${report.reason}`
    );
    record.commandType = this.ignoreCommandId;
    record.commandAction = this.ignoreCommandId;

    try {
      await this.recordRepo.create(record);

      // Send Discord notification
      if (this.discordService) {
        await this.discordService.sendReportActionNotification(
          report.reportId,
          'ignored',
          admin.soldierName,
          report.targetPlayer.soldierName
        );
      }

      // Emit event
      this.eventBus.emitEvent('report:ignored', report, admin);

      this.logger.info(
        {
          reportId: report.reportId,
          admin: admin.soldierName,
          target: report.targetPlayer.soldierName,
        },
        'Report ignored'
      );

      return {
        success: true,
        message: `Ignored report #${report.reportId} against ${report.targetPlayer.soldierName}`,
        report,
      };

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to ignore report');
      return {
        success: false,
        message: 'Failed to ignore report due to a system error',
      };
    }
  }

  /**
   * Get all pending reports for the server.
   */
  getPendingReports(): PendingReport[] {
    return Array.from(this.pendingReports.values())
      .filter(report => report.serverId === this.config.serverId)
      .sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime());
  }

  /**
   * Get a pending report by ID.
   */
  getReportById(reportId: number): PendingReport | undefined {
    return this.pendingReports.get(reportId);
  }

  /**
   * Get the number of pending reports.
   */
  getPendingReportCount(): number {
    return this.pendingReports.size;
  }

  /**
   * Notify online admins about a new report.
   */
  async notifyAdmins(report: PendingReport): Promise<void> {
    // Send in-game notification to all players (admins will see it)
    const message = `[Report #${report.reportId}] ${report.sourcePlayer.soldierName} reported ${report.targetPlayer.soldierName}: ${report.reason}`;

    // Send to global chat (admins online will see)
    await this.bcAdapter.say(message);

    // Send to Discord
    if (this.discordService) {
      const discordData: DiscordReportData = {
        reportId: report.reportId,
        sourcePlayer: report.sourcePlayer,
        targetPlayer: report.targetPlayer,
        reason: report.reason,
        serverName: this.config.serverName,
        timestamp: report.timestamp,
      };

      await this.discordService.sendReportNotification(discordData);
    }
  }

  /**
   * Find a report by ID or get the most recent pending report.
   */
  private findReport(reportId: number | undefined, _admin: APlayer): PendingReport | undefined {
    if (reportId !== undefined) {
      return this.pendingReports.get(reportId);
    }

    // Get the oldest pending report (first in, first out)
    const pending = this.getPendingReports();
    return pending.length > 0 ? pending[0] : undefined;
  }

  /**
   * Load pending reports from the database.
   */
  private async loadPendingReports(): Promise<void> {
    try {
      const records = await this.recordRepo.findPendingReports(this.config.serverId);

      for (const record of records) {
        // Skip if we don't have full player info
        if (!record.targetId || !record.sourceId) {
          continue;
        }

        // Create a minimal pending report from the record
        // Note: Player objects would ideally be fetched from PlayerService
        const pendingReport: PendingReport = {
          reportId: record.recordId,
          sourcePlayer: {
            playerId: record.sourceId,
            soldierName: record.sourceName,
            guid: '',
            gameId: 0,
            clanTag: null,
            pbGuid: null,
            ipAddress: null,
            isOnline: false,
            isAlive: false,
            teamId: 0,
            squadId: 0,
            kills: 0,
            deaths: 0,
            score: 0,
            ping: 0,
            rank: 0,
            type: 0,
            reputation: null,
            infractions: null,
            firstSeen: null,
            lastSeen: null,
          },
          targetPlayer: {
            playerId: record.targetId,
            soldierName: record.targetName,
            guid: '',
            gameId: 0,
            clanTag: null,
            pbGuid: null,
            ipAddress: null,
            isOnline: false,
            isAlive: false,
            teamId: 0,
            squadId: 0,
            kills: 0,
            deaths: 0,
            score: 0,
            ping: 0,
            rank: 0,
            type: 0,
            reputation: null,
            infractions: null,
            firstSeen: null,
            lastSeen: null,
          },
          reason: record.recordMessage,
          timestamp: record.recordTime,
          serverId: record.serverId,
          roundId: this.currentRoundId,
        };

        this.pendingReports.set(record.recordId, pendingReport);
      }

      this.logger.debug(
        { count: this.pendingReports.size },
        'Loaded pending reports from database'
      );

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to load pending reports');
    }
  }

  /**
   * Clean up expired reports.
   */
  private cleanupExpiredReports(): void {
    const now = Date.now();
    let expiredCount = 0;

    for (const [reportId, report] of this.pendingReports) {
      const age = now - report.timestamp.getTime();
      if (age >= this.config.reportExpirationMs) {
        this.pendingReports.delete(reportId);
        expiredCount++;

        this.logger.debug(
          { reportId, age: Math.round(age / 60000) },
          'Report expired'
        );
      }
    }

    if (expiredCount > 0) {
      this.logger.info({ expiredCount }, 'Cleaned up expired reports');
    }
  }

  /**
   * Handle round change - reset per-round counters.
   */
  private onRoundChange(): void {
    this.currentRoundId++;
    this.playerReportCounts.clear();

    this.logger.debug(
      { roundId: this.currentRoundId },
      'Round changed, reset report counters'
    );
  }
}

/**
 * Create a new report service instance.
 */
export function createReportService(
  logger: Logger,
  eventBus: AdKatsEventBus,
  bcAdapter: BattleConAdapter,
  recordRepo: RecordRepository,
  discordService: DiscordService | null,
  config?: Partial<ReportServiceConfig>
): ReportService {
  return new ReportService(logger, eventBus, bcAdapter, recordRepo, discordService, config);
}
