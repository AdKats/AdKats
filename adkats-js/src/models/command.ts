/**
 * Command model - represents an AdKats command.
 * Corresponds to adkats_commands in the database.
 */
export interface ACommand {
  commandId: number;
  commandActive: CommandActive;
  commandKey: string;
  commandLogging: CommandLogging;
  commandName: string;
  commandText: string;
  commandPlayerInteraction: boolean;
  commandAccess: CommandAccess;
}

/**
 * Command active status.
 */
export type CommandActive = 'Active' | 'Disabled' | 'Invisible';

/**
 * Command logging level.
 */
export type CommandLogging = 'Log' | 'Mandatory' | 'Ignore' | 'Unable';

/**
 * Command access level - determines chat visibility.
 */
export type CommandAccess =
  | 'Any'           // Can be used from any chat channel
  | 'AnyHidden'     // Hidden from help, any channel
  | 'AnyVisible'    // Visible in help, any channel
  | 'GlobalVisible' // Visible in help, global chat only
  | 'TeamVisible'   // Visible in help, team chat only
  | 'SquadVisible'; // Visible in help, squad chat only

/**
 * Built-in command keys for reference.
 * These match the command_key values in adkats_commands.
 */
export const CommandKeys = {
  // Confirmation
  CONFIRM: 'command_confirm',
  CANCEL: 'command_cancel',

  // Player actions
  KILL: 'player_kill',
  KILL_LOWPOP: 'player_kill_lowpop',
  KILL_REPEAT: 'player_kill_repeat',
  KILL_FORCE: 'player_kill_force',
  KICK: 'player_kick',
  BAN_TEMP: 'player_ban_temp',
  BAN_PERM: 'player_ban_perm',
  BAN_FUTURE: 'player_ban_perm_future',
  UNBAN: 'player_unban',
  PUNISH: 'player_punish',
  FORGIVE: 'player_forgive',
  MUTE: 'player_mute',
  WARN: 'player_warn',
  MOVE: 'player_move',
  FMOVE: 'player_fmove',
  JOIN: 'player_join',
  PULL: 'player_pull',
  LOCK: 'player_lock',
  UNLOCK: 'player_unlock',

  // Self commands
  TEAMSWAP: 'self_teamswap',
  KILL_SELF: 'self_kill',
  RULES: 'self_rules',
  HELP: 'self_help',
  ADMINS: 'self_admins',
  LEAD: 'self_lead',
  ASSIST: 'self_assist',
  UPTIME: 'self_uptime',
  CONTEST: 'self_contest',
  REP: 'self_rep',
  SURRENDER: 'self_surrender',
  VOTENEXT: 'self_votenext',
  REPORTLIST: 'self_reportlist',
  NOSURRENDER: 'self_nosurrender',
  VOIP: 'self_voip',
  WHATIS: 'self_whatis',
  FEEDBACK: 'self_feedback',
  BATTLECRY: 'self_battlecry',
  CHALLENGE: 'self_challenge',

  // Report commands
  REPORT: 'player_report',
  REPORT_CONFIRM: 'player_report_confirm',
  REPORT_DENY: 'player_report_deny',
  REPORT_IGNORE: 'player_report_ignore',
  CALLADMIN: 'player_calladmin',
  ACCEPT: 'admin_accept',
  DENY: 'admin_deny',
  IGNORE: 'admin_ignore',

  // Communication
  SAY: 'admin_say',
  PLAYER_SAY: 'player_say',
  YELL: 'admin_yell',
  PLAYER_YELL: 'player_yell',
  TELL: 'admin_tell',
  PLAYER_TELL: 'player_tell',
  PM_SEND: 'player_pm_send',
  PM_REPLY: 'player_pm_reply',
  ADMIN_PM: 'admin_pm_send',

  // Server commands
  RESTART: 'round_restart',
  NEXTLEVEL: 'round_next',
  ENDROUND: 'round_end',
  NUKE: 'server_nuke',
  NUKE_WINNING: 'server_nuke_winning',
  SWAPNUKE: 'server_swapnuke',
  KICKALL: 'server_kickall',
  AFK: 'server_afk',
  SHUTDOWN: 'server_shutdown',
  COUNTDOWN: 'server_countdown',

  // Info commands
  FIND: 'player_find',
  PINFO: 'player_info',
  PCHAT: 'player_chat',
  LOG: 'player_log',
  ISADMIN: 'player_isadmin',
  LOADOUT: 'player_loadout',
  FLOADOUT: 'player_loadout_force',
  ILOADOUT: 'player_loadout_ignore',
  PERKS: 'player_perks',
  PING: 'player_ping',
  FPING: 'player_forceping',
  DEBUGASSIST: 'player_debugassist',

  // Special player groups
  DISPERSE: 'player_blacklistdisperse',
  UNDISPERSE: 'player_blacklistdisperse_remove',
  MBWHITELIST: 'player_whitelistbalance',
  UNMBWHITELIST: 'player_whitelistbalance_remove',
  RESERVED: 'player_slotreserved',
  UNRESERVED: 'player_slotreserved_remove',
  SPECTATOR: 'player_slotspectator',
  UNSPECTATOR: 'player_slotspectator_remove',
  ACWHITELIST: 'player_whitelistanticheat',
  UNACWHITELIST: 'player_whitelistanticheat_remove',
  PWHITELIST: 'player_whitelistping',
  UNPWHITELIST: 'player_whitelistping_remove',
  AAWHITELIST: 'player_whitelistaa',
  UNAAWHITELIST: 'player_whitelistaa_remove',
  SPAMWHITELIST: 'player_whitelistspambot',
  UNSPAMWHITELIST: 'player_whitelistspambot_remove',
  RWHITELIST: 'player_whitelistreport',
  UNRWHITELIST: 'player_whitelistreport_remove',
  POPWHITELIST: 'player_whitelistpopulator',
  UNPOPWHITELIST: 'player_whitelistpopulator_remove',
  TKWHITELIST: 'player_whitelistteamkill',
  UNTKWHITELIST: 'player_whitelistteamkill_remove',
  SPECBLACKLIST: 'player_blacklistspectator',
  UNSPECBLACKLIST: 'player_blacklistspectator_remove',
  RBLACKLIST: 'player_blacklistreport',
  UNRBLACKLIST: 'player_blacklistreport_remove',
  CWHITELIST: 'player_whitelistcommand',
  UNCWHITELIST: 'player_whitelistcommand_remove',
  AUABLACKLIST: 'player_blacklistautoassist',
  UNAUABLACKLIST: 'player_blacklistautoassist_remove',
  ALLCAPSBLACKLIST: 'player_blacklistallcaps',
  UNALLCAPSBLACKLIST: 'player_blacklistallcaps_remove',

  // System
  EXCEPTION: 'adkats_exception',
  BAN_ENFORCE: 'banenforcer_enforce',
  DEQUEUE: 'player_dequeue',
  MARK: 'player_mark',
  REPBOOST: 'player_repboost',
  CHANGENAME: 'player_changename',
  CHANGEIP: 'player_changeip',
  CHANGETAG: 'player_changetag',
  PLUGIN_RESTART: 'plugin_restart',
  PLUGIN_UPDATE: 'plugin_update',
  POP_SUCCESS: 'player_population_success',
  MAP_DETRIMENT: 'server_map_detriment',
  MAP_BENEFIT: 'server_map_benefit',

  // Polls
  POLL_TRIGGER: 'poll_trigger',
  POLL_VOTE: 'poll_vote',
  POLL_CANCEL: 'poll_cancel',
  POLL_COMPLETE: 'poll_complete',

  // Discord
  DISCORD_LINK: 'player_discordlink',

  // Battlecry
  SET_BATTLECRY: 'player_battlecry',

  // Challenges
  CHALLENGE_PLAY: 'player_challenge_play',
  CHALLENGE_IGNORE: 'player_challenge_ignore',
  CHALLENGE_AUTOKILL: 'player_challenge_autokill',
  UNCHALLENGE_AUTOKILL: 'player_challenge_autokill_remove',
  UNCHALLENGE_PLAY: 'player_challenge_play_remove',
  UNCHALLENGE_IGNORE: 'player_challenge_ignore_remove',
  CHALLENGE_COMPLETE: 'player_challenge_complete',
} as const;

/**
 * Database row representation.
 */
export interface CommandDbRow {
  command_id: number;
  command_active: CommandActive;
  command_key: string;
  command_logging: CommandLogging;
  command_name: string;
  command_text: string;
  command_playerInteraction: number;
  command_access: CommandAccess;
}

/**
 * Convert database row to ACommand.
 */
export function commandFromDbRow(row: CommandDbRow): ACommand {
  return {
    commandId: row.command_id,
    commandActive: row.command_active,
    commandKey: row.command_key,
    commandLogging: row.command_logging,
    commandName: row.command_name,
    commandText: row.command_text,
    commandPlayerInteraction: row.command_playerInteraction === 1,
    commandAccess: row.command_access,
  };
}

/**
 * Check if a command is enabled.
 */
export function isCommandEnabled(command: ACommand): boolean {
  return command.commandActive === 'Active' || command.commandActive === 'Invisible';
}

/**
 * Check if a command is visible in help.
 */
export function isCommandVisible(command: ACommand): boolean {
  return command.commandActive === 'Active' &&
    command.commandAccess !== 'AnyHidden';
}
