// Player service
export { PlayerService, createPlayerService } from './player.service.js';

// Command service
export { CommandService, createCommandService } from './command.service.js';
export type { CommandContext, CommandHandler } from './command.service.js';

// Ban service
export { BanService, createBanService } from './ban.service.js';
export type { BanOptions, BanCheckResult } from './ban.service.js';

// Infraction service
export { InfractionService, createInfractionService } from './infraction.service.js';
export type { PunishmentType, PunishmentResult, ForgiveResult, WarnResult } from './infraction.service.js';

// Team service
export { TeamService, createTeamService } from './team.service.js';
export type { TeamBalance } from './team.service.js';

// Report service
export { ReportService, createReportService } from './report.service.js';
export type { PendingReport, ReportServiceConfig, ReportActionResult } from './report.service.js';

// Anti-cheat service
export { AntiCheatService, createAntiCheatService } from './anticheat.service.js';
export type { AntiCheatConfig, AntiCheatViolation, AntiCheatCheckResult } from './anticheat.service.js';

// Reputation service
export { ReputationService, createReputationService, createDefaultReputationConfig } from './reputation.service.js';
export type { ReputationConfig, ReputationBreakdown } from './reputation.service.js';

// AFK service
export { AfkService, createAfkService, createDefaultAfkConfig } from './afk.service.js';
export type { AfkConfig, AfkCheckResult } from './afk.service.js';

// Ping service
export { PingService, createPingService, createDefaultPingConfig } from './ping.service.js';
export type { PingConfig, PingCheckResult } from './ping.service.js';

// SpamBot service
export { SpamBotService, createSpambotService, createDefaultSpamBotConfig } from './spambot.service.js';
export type { SpamBotConfig, SpamBotMessageType } from './spambot.service.js';

// Special player service
export { SpecialPlayerService, createSpecialPlayerService, SpecialGroupKeys } from './specialplayer.service.js';
export type { SpecialGroupDefinition, GroupCheckResult } from './specialplayer.service.js';
