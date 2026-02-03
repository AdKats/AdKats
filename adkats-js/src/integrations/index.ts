// Discord integration
export {
  DiscordService,
  createDiscordService,
  DiscordColors,
} from './discord.js';
export type {
  DiscordWebhookPayload,
  DiscordEmbed,
  DiscordEmbedField,
  DiscordEmbedFooter,
  DiscordReportData,
  AlertType,
} from './discord.js';

// Battlelog integration
export {
  BattlelogClient,
  BattlelogError,
  createBattlelogClient,
} from './battlelog.js';
export type {
  BattlelogGameVersion,
  BattlelogPlayerStats,
  BattlelogWeaponStats,
  BattlelogVehicleStats,
  BattlelogErrorType,
} from './battlelog.js';
