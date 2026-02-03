import type { Logger } from '../core/logger.js';
import type { APlayer } from '../models/player.js';
import type { ABan } from '../models/ban.js';

/**
 * Discord embed field.
 */
export interface DiscordEmbedField {
  name: string;
  value: string;
  inline?: boolean;
}

/**
 * Discord embed footer.
 */
export interface DiscordEmbedFooter {
  text: string;
  icon_url?: string;
}

/**
 * Discord embed structure.
 */
export interface DiscordEmbed {
  title?: string;
  description?: string;
  color?: number;
  fields?: DiscordEmbedField[];
  timestamp?: string;
  footer?: DiscordEmbedFooter;
  url?: string;
  thumbnail?: { url: string };
}

/**
 * Discord webhook payload.
 */
export interface DiscordWebhookPayload {
  username?: string;
  avatar_url?: string;
  content?: string;
  embeds?: DiscordEmbed[];
}

/**
 * Report data for Discord notification.
 */
export interface DiscordReportData {
  reportId: number;
  sourcePlayer: APlayer;
  targetPlayer: APlayer;
  reason: string;
  serverName: string;
  timestamp: Date;
}

/**
 * Discord embed colors.
 */
export const DiscordColors = {
  GREEN: 0x00ff00,
  RED: 0xff0000,
  YELLOW: 0xffff00,
  BLUE: 0x0000ff,
  ORANGE: 0xff8c00,
  PURPLE: 0x9b59b6,
} as const;

/**
 * Alert types for admin notifications.
 */
export type AlertType = 'info' | 'warning' | 'error' | 'success';

/**
 * Discord integration service for sending webhooks.
 */
export class DiscordService {
  private logger: Logger;
  private reportChannelWebhook: string | undefined;
  private adminChannelWebhook: string | undefined;
  private botUsername: string;
  private botAvatarUrl: string | undefined;

  constructor(
    logger: Logger,
    reportChannelWebhook?: string,
    adminChannelWebhook?: string,
    botUsername: string = 'AdKats',
    botAvatarUrl?: string
  ) {
    this.logger = logger;
    this.reportChannelWebhook = reportChannelWebhook;
    this.adminChannelWebhook = adminChannelWebhook;
    this.botUsername = botUsername;
    this.botAvatarUrl = botAvatarUrl;
  }

  /**
   * Check if the report channel webhook is configured.
   */
  isReportChannelConfigured(): boolean {
    return !!this.reportChannelWebhook;
  }

  /**
   * Check if the admin channel webhook is configured.
   */
  isAdminChannelConfigured(): boolean {
    return !!this.adminChannelWebhook;
  }

  /**
   * Send a report notification to the report channel.
   */
  async sendReportNotification(report: DiscordReportData): Promise<boolean> {
    if (!this.reportChannelWebhook) {
      this.logger.debug('Report channel webhook not configured, skipping Discord notification');
      return false;
    }

    const embed: DiscordEmbed = {
      title: 'Player Report',
      description: `A player has been reported on **${report.serverName}**`,
      color: DiscordColors.ORANGE,
      fields: [
        {
          name: 'Report ID',
          value: `#${report.reportId}`,
          inline: true,
        },
        {
          name: 'Reported By',
          value: report.sourcePlayer.soldierName,
          inline: true,
        },
        {
          name: 'Target Player',
          value: report.targetPlayer.soldierName,
          inline: true,
        },
        {
          name: 'Reason',
          value: report.reason || 'No reason provided',
          inline: false,
        },
        {
          name: 'Target Info',
          value: this.formatPlayerInfo(report.targetPlayer),
          inline: false,
        },
      ],
      timestamp: report.timestamp.toISOString(),
      footer: {
        text: `Server: ${report.serverName}`,
      },
    };

    return this.sendWebhook(this.reportChannelWebhook, {
      username: this.botUsername,
      avatar_url: this.botAvatarUrl,
      embeds: [embed],
    });
  }

  /**
   * Send an admin alert to the admin channel.
   */
  async sendAdminAlert(message: string, type: AlertType = 'info', details?: Record<string, string>): Promise<boolean> {
    if (!this.adminChannelWebhook) {
      this.logger.debug('Admin channel webhook not configured, skipping Discord notification');
      return false;
    }

    const colorMap: Record<AlertType, number> = {
      info: DiscordColors.BLUE,
      warning: DiscordColors.YELLOW,
      error: DiscordColors.RED,
      success: DiscordColors.GREEN,
    };

    const titleMap: Record<AlertType, string> = {
      info: 'Information',
      warning: 'Warning',
      error: 'Error',
      success: 'Success',
    };

    const fields: DiscordEmbedField[] = [];
    if (details) {
      for (const [key, value] of Object.entries(details)) {
        fields.push({
          name: key,
          value: value,
          inline: true,
        });
      }
    }

    const embed: DiscordEmbed = {
      title: titleMap[type],
      description: message,
      color: colorMap[type],
      fields: fields.length > 0 ? fields : undefined,
      timestamp: new Date().toISOString(),
    };

    return this.sendWebhook(this.adminChannelWebhook, {
      username: this.botUsername,
      avatar_url: this.botAvatarUrl,
      embeds: [embed],
    });
  }

  /**
   * Send a ban notification to the admin channel.
   */
  async sendBanNotification(
    ban: ABan,
    targetPlayer: APlayer,
    adminName: string,
    serverName: string
  ): Promise<boolean> {
    if (!this.adminChannelWebhook) {
      this.logger.debug('Admin channel webhook not configured, skipping Discord notification');
      return false;
    }

    const isPermanent = ban.banEndTime === null || ban.banStatus === 'Active';
    const banTypeLabel = isPermanent ? 'Permanent Ban' : 'Temporary Ban';

    const embed: DiscordEmbed = {
      title: banTypeLabel,
      description: `A player has been banned on **${serverName}**`,
      color: DiscordColors.RED,
      fields: [
        {
          name: 'Player',
          value: targetPlayer.soldierName,
          inline: true,
        },
        {
          name: 'Banned By',
          value: adminName,
          inline: true,
        },
        {
          name: 'GUID',
          value: targetPlayer.guid || 'Unknown',
          inline: true,
        },
        {
          name: 'Reason',
          value: ban.banNotes || 'No reason provided',
          inline: false,
        },
      ],
      timestamp: new Date().toISOString(),
      footer: {
        text: `Ban ID: ${ban.banId}`,
      },
    };

    // Add ban duration if temporary
    if (!isPermanent && ban.banEndTime) {
      embed.fields!.push({
        name: 'Expires',
        value: ban.banEndTime.toISOString(),
        inline: true,
      });
    }

    return this.sendWebhook(this.adminChannelWebhook, {
      username: this.botUsername,
      avatar_url: this.botAvatarUrl,
      embeds: [embed],
    });
  }

  /**
   * Send a report action notification (accepted/denied/ignored).
   */
  async sendReportActionNotification(
    reportId: number,
    action: 'accepted' | 'denied' | 'ignored',
    adminName: string,
    targetPlayerName: string,
    reason?: string
  ): Promise<boolean> {
    if (!this.reportChannelWebhook) {
      return false;
    }

    const colorMap = {
      accepted: DiscordColors.GREEN,
      denied: DiscordColors.RED,
      ignored: DiscordColors.YELLOW,
    };

    const embed: DiscordEmbed = {
      title: `Report ${action.charAt(0).toUpperCase() + action.slice(1)}`,
      color: colorMap[action],
      fields: [
        {
          name: 'Report ID',
          value: `#${reportId}`,
          inline: true,
        },
        {
          name: 'Admin',
          value: adminName,
          inline: true,
        },
        {
          name: 'Target',
          value: targetPlayerName,
          inline: true,
        },
      ],
      timestamp: new Date().toISOString(),
    };

    if (reason) {
      embed.fields!.push({
        name: 'Reason',
        value: reason,
        inline: false,
      });
    }

    return this.sendWebhook(this.reportChannelWebhook, {
      username: this.botUsername,
      avatar_url: this.botAvatarUrl,
      embeds: [embed],
    });
  }

  /**
   * Format player info for embed field.
   */
  private formatPlayerInfo(player: APlayer): string {
    const parts: string[] = [];

    if (player.clanTag) {
      parts.push(`Clan: [${player.clanTag}]`);
    }

    parts.push(`K/D: ${player.kills}/${player.deaths}`);
    parts.push(`Score: ${player.score}`);
    parts.push(`Ping: ${player.ping}ms`);

    if (player.reputation !== null) {
      parts.push(`Rep: ${player.reputation.toFixed(1)}`);
    }

    return parts.join(' | ');
  }

  /**
   * Send a webhook payload to the specified URL.
   */
  private async sendWebhook(webhookUrl: string, payload: DiscordWebhookPayload): Promise<boolean> {
    try {
      const response = await fetch(webhookUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        const errorText = await response.text();
        this.logger.error(
          { status: response.status, statusText: response.statusText, body: errorText },
          'Discord webhook request failed'
        );
        return false;
      }

      this.logger.debug('Discord webhook sent successfully');
      return true;

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ error: msg }, 'Failed to send Discord webhook');
      return false;
    }
  }
}

/**
 * Create a new Discord service instance.
 */
export function createDiscordService(
  logger: Logger,
  reportChannelWebhook?: string,
  adminChannelWebhook?: string,
  botUsername?: string,
  botAvatarUrl?: string
): DiscordService {
  return new DiscordService(logger, reportChannelWebhook, adminChannelWebhook, botUsername, botAvatarUrl);
}
