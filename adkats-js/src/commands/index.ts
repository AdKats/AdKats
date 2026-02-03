// Base command
export { BaseCommand, createSimpleHandler } from './base.command.js';
export type { CommandHandler, CommandContext } from '../services/command.service.js';

// Admin commands
export { KillCommand, registerKillCommand } from './admin/kill.command.js';
export { KickCommand, registerKickCommand } from './admin/kick.command.js';
export {
  SayCommand,
  PlayerSayCommand,
  YellCommand,
  PlayerYellCommand,
  registerSayCommands,
} from './admin/say.command.js';
export {
  BanCommand,
  TempBanCommand,
  UnbanCommand,
  registerBanCommands,
} from './admin/ban.command.js';
export {
  PunishCommand,
  ForgiveCommand,
  WarnCommand,
  registerPunishCommands,
} from './admin/punish.command.js';
export {
  MoveCommand,
  ForceMoveCommand,
  PullCommand,
  registerMoveCommands,
} from './admin/move.command.js';
export {
  AcceptCommand,
  DenyCommand,
  IgnoreCommand,
  registerReportAdminCommands,
} from './admin/report-admin.command.js';
export { registerInfoCommands } from './admin/info.command.js';
export { registerPlayerControlCommands } from './admin/player-control.command.js';
export type { MuteStatus, LockStatus } from './admin/player-control.command.js';
export { registerServerCommands } from './admin/server.command.js';
export { registerWhitelistCommands } from './admin/whitelist.command.js';

// Server commands
export { registerNukeCommands } from './server/nuke.command.js';
export { registerRoundCommands } from './server/round.command.js';

// Player commands
export { RulesCommand, registerRulesCommand } from './player/rules.command.js';
export { HelpCommand, registerHelpCommand } from './player/help.command.js';
export {
  MoveMeCommand,
  AssistCommand,
  JoinCommand,
  registerTeamCommands,
} from './player/team.command.js';
export {
  ReportCommand,
  CallAdminCommand,
  registerReportCommands,
} from './player/report.command.js';
export {
  VotingManager,
  VoteType,
  registerVotingCommands,
} from './player/voting.command.js';
export type { VoteConfig, ActiveVote } from './player/voting.command.js';
