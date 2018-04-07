-- AdKats Database Setup Script
-- Version 7.0.0.0 (2017-10-15)
-- Daniel J. Gradinjan (ColColonCleaner)

SET FOREIGN_KEY_CHECKS=0;
SET SQL_MODE="NO_AUTO_VALUE_ON_ZERO";
SET time_zone = "+00:00";

DELIMITER $$

DROP PROCEDURE IF EXISTS addLogPlayerID $$
CREATE PROCEDURE addLogPlayerID()
BEGIN

-- add logPlayerID column safely
IF NOT EXISTS( (SELECT * FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
        AND COLUMN_NAME='logPlayerID' AND TABLE_NAME='tbl_chatlog') ) THEN
    ALTER TABLE `tbl_chatlog` ADD COLUMN `logPlayerID` INT(10) UNSIGNED DEFAULT NULL;
	ALTER TABLE `tbl_chatlog` ADD INDEX (`logPlayerID`);
	ALTER TABLE `tbl_chatlog` ADD CONSTRAINT `tbl_chatlog_ibfk_player_id` FOREIGN KEY (`logPlayerID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE;
	UPDATE 
		`tbl_chatlog`
	INNER JOIN 
		`tbl_playerdata`
	ON 
		`tbl_chatlog`.`logSoldierName` = `tbl_playerdata`.`SoldierName` 
	SET 
		`tbl_chatlog`.`logPlayerID` = `tbl_playerdata`.`PlayerID`
	WHERE 
		`tbl_playerdata`.`SoldierName` <> 'AutoAdmin' 
	AND 
		`tbl_playerdata`.`SoldierName` <> 'AdKats' 
	AND 
		`tbl_playerdata`.`SoldierName` <> 'Server' 
	AND 
		`tbl_playerdata`.`SoldierName` <> 'BanEnforcer'
	AND 
		`tbl_chatlog`.`logPlayerID` IS NULL;
END IF;

END $$

CALL addLogPlayerID() $$

DROP TRIGGER IF EXISTS `tbl_chatlog_player_id_insert`$$
CREATE TRIGGER `tbl_chatlog_player_id_insert` BEFORE INSERT ON `tbl_chatlog`
  FOR EACH ROW BEGIN 
    SET NEW.logPlayerID = (SELECT `tbl_playerdata`.`PlayerID` FROM `tbl_server`
      INNER JOIN `tbl_games` ON `tbl_server`.`GameID` = `tbl_games`.`GameID`
      INNER JOIN `tbl_playerdata` ON `tbl_games`.`GameID` = `tbl_playerdata`.`GameID`
      WHERE `tbl_playerdata`.`SoldierName` = NEW.logSoldierName AND `tbl_server`.`ServerID` = NEW.ServerID LIMIT 1);
  END
$$

DROP TRIGGER IF EXISTS `Player_Update_BlankDataFix`$$
DROP TRIGGER IF EXISTS `Player_Update_BlankDataFix2`$$
CREATE TRIGGER `Player_Update_BlankDataFix2` BEFORE UPDATE ON `tbl_playerdata`
 FOR EACH ROW BEGIN
    IF (NEW.SoldierName IS NULL OR CHAR_LENGTH(NEW.SoldierName) = 0) 
       AND OLD.SoldierName IS NOT NULL
       AND CHAR_LENGTH(OLD.SoldierName) > 0
        THEN SET NEW.SoldierName = OLD.SoldierName;
    END IF;
    IF (NEW.EAGUID IS NULL OR CHAR_LENGTH(NEW.EAGUID) = 0)
        AND OLD.EAGUID IS NOT NULL 
        AND CHAR_LENGTH(OLD.EAGUID) > 0
        THEN SET NEW.EAGUID = OLD.EAGUID;
    END IF;
    IF (NEW.PBGUID IS NULL OR CHAR_LENGTH(NEW.PBGUID) = 0)
        AND OLD.PBGUID IS NOT NULL 
        AND CHAR_LENGTH(OLD.PBGUID) > 0
        THEN SET NEW.PBGUID = OLD.PBGUID;
    END IF;
    IF (NEW.IP_Address IS NULL OR CHAR_LENGTH(NEW.IP_Address) = 0)
        AND OLD.IP_Address IS NOT NULL 
        AND CHAR_LENGTH(OLD.IP_Address) > 0
        AND OLD.IP_Address <> '127.0.0.1'
        THEN SET NEW.IP_Address = OLD.IP_Address;
    END IF;
    IF NEW.ClanTag IS NULL
        THEN SET NEW.ClanTag = OLD.ClanTag;
    END IF;
END$$

DELIMITER ;

DROP TABLE IF EXISTS `adkats_battlelog_players`;
CREATE TABLE `adkats_battlelog_players` (
  `player_id` int(10) unsigned NOT NULL,
  `persona_id` bigint(20) unsigned NOT NULL,
  `user_id` bigint(20) unsigned NOT NULL,
  `gravatar` varchar(32) COLLATE utf8_unicode_ci DEFAULT NULL,
  `persona_banned` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`player_id`),
  UNIQUE KEY `adkats_battlelog_players_player_id_persona_id_unique` (`player_id`,`persona_id`),
  KEY `adkats_battlelog_players_persona_id_index` (`persona_id`),
  KEY `adkats_battlelog_players_user_id_index` (`user_id`),
  CONSTRAINT `adkats_battlelog_players_ibfk_1` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Player Battlelog Info';

DROP TABLE IF EXISTS `adkats_battlecries`;
CREATE TABLE IF NOT EXISTS `adkats_battlecries`( 
  `player_id` int(10) UNSIGNED NOT NULL,
  `player_battlecry` varchar(300) COLLATE utf8_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Battlecries List';

DROP TABLE IF EXISTS `adkats_bans`;
CREATE TABLE IF NOT EXISTS `adkats_bans` (
  `ban_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `player_id` int(11) unsigned NOT NULL,
  `latest_record_id` int(11) unsigned NOT NULL,
  `ban_notes` varchar(150) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'NoNotes',
  `ban_status` enum('Active','Expired','Disabled') COLLATE utf8_unicode_ci NOT NULL DEFAULT 'Active',
  `ban_startTime` datetime NOT NULL,
  `ban_endTime` datetime NOT NULL,
  `ban_enforceName` enum('Y','N') COLLATE utf8_unicode_ci NOT NULL DEFAULT 'N',
  `ban_enforceGUID` enum('Y','N') COLLATE utf8_unicode_ci NOT NULL DEFAULT 'Y',
  `ban_enforceIP` enum('Y','N') COLLATE utf8_unicode_ci NOT NULL DEFAULT 'N',
  `ban_sync` varchar(100) COLLATE utf8_unicode_ci NOT NULL DEFAULT '-sync-',
  PRIMARY KEY (`ban_id`),
  UNIQUE KEY `player_id_UNIQUE` (`player_id`),
  KEY `adkats_bans_fk_latest_record_id` (`latest_record_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Ban List';

DROP TABLE IF EXISTS `adkats_commands`;
CREATE TABLE IF NOT EXISTS `adkats_commands` (
  `command_id` int(11) unsigned NOT NULL,
  `command_active` enum('Active','Disabled','Invisible') CHARACTER SET 'utf8' COLLATE 'utf8_unicode_ci' NOT NULL DEFAULT 'Active',
  `command_key` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
  `command_logging` ENUM('Log','Mandatory','Ignore', 'Unable') CHARACTER SET 'utf8' COLLATE 'utf8_unicode_ci' NOT NULL DEFAULT 'Log',
  `command_name` varchar(255) COLLATE utf8_unicode_ci NOT NULL,
  `command_text` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
  `command_playerInteraction` BOOLEAN NOT NULL,
  `command_access` enum('Any','AnyHidden','AnyVisible','GlobalVisible','TeamVisible','SquadVisible') CHARACTER SET 'utf8' COLLATE 'utf8_unicode_ci' NOT NULL DEFAULT 'Any',
  PRIMARY KEY (`command_id`),
  UNIQUE KEY `command_key_UNIQUE` (`command_key`),
  UNIQUE KEY `command_text_UNIQUE` (`command_text`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Command List';

REPLACE INTO `adkats_commands` VALUES(1, 'Active', 'command_confirm', 'Unable', 'Confirm Command', 'yes', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(2, 'Active', 'command_cancel', 'Unable', 'Cancel Command', 'no', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(3, 'Active', 'player_kill', 'Log', 'Kill Player', 'kill', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(4, 'Invisible', 'player_kill_lowpop', 'Log', 'Kill Player (Low Population)', 'lowpopkill', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(5, 'Invisible', 'player_kill_repeat', 'Log', 'Kill Player (Repeat Kill)', 'repeatkill', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(6, 'Active', 'player_kick', 'Log', 'Kick Player', 'kick', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(7, 'Active', 'player_ban_temp', 'Log', 'Temp-Ban Player', 'tban', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(8, 'Active', 'player_ban_perm', 'Log', 'Permaban Player', 'ban', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(9, 'Active', 'player_punish', 'Mandatory', 'Punish Player', 'punish', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(10, 'Active', 'player_forgive', 'Mandatory', 'Forgive Player', 'forgive', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(11, 'Active', 'player_mute', 'Log', 'Mute Player', 'mute', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(12, 'Active', 'player_join', 'Log', 'Join Player', 'join', FALSE, 'Any');
-- Command 13 permanently removed
REPLACE INTO `adkats_commands` VALUES(14, 'Active', 'player_move', 'Log', 'On-Death Move Player', 'move', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(15, 'Active', 'player_fmove', 'Log', 'Force Move Player', 'fmove', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(16, 'Active', 'self_teamswap', 'Log', 'Teamswap Self', 'moveme', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(17, 'Active', 'self_kill', 'Log', 'Kill Self', 'killme', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(18, 'Active', 'player_report', 'Log', 'Report Player', 'report', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(19, 'Invisible', 'player_report_confirm', 'Log', 'Report Player (Confirmed)', 'confirmreport', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(20, 'Active', 'player_calladmin', 'Log', 'Call Admin on Player', 'admin', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(21, 'Active', 'admin_say', 'Log', 'Admin Say', 'say', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(22, 'Active', 'player_say', 'Log', 'Player Say', 'psay', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(23, 'Active', 'admin_yell', 'Log', 'Admin Yell', 'yell', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(24, 'Active', 'player_yell', 'Log', 'Player Yell', 'pyell', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(25, 'Active', 'admin_tell', 'Log', 'Admin Tell', 'tell', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(26, 'Active', 'player_tell', 'Log', 'Player Tell', 'ptell', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(27, 'Active', 'self_whatis', 'Unable', 'What Is', 'whatis', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(28, 'Active', 'self_voip', 'Unable', 'VOIP', 'voip', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(29, 'Active', 'self_rules', 'Log', 'Request Rules', 'rules', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(30, 'Active', 'round_restart', 'Log', 'Restart Current Round', 'restart', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(31, 'Active', 'round_next', 'Log', 'Run Next Round', 'nextlevel', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(32, 'Active', 'round_end', 'Log', 'End Current Round', 'endround', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(33, 'Active', 'server_nuke', 'Log', 'Server Nuke', 'nuke', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(34, 'Active', 'server_kickall', 'Log', 'Kick All Guests', 'kickall', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(35, 'Invisible', 'adkats_exception', 'Mandatory', 'Logged Exception', 'logexception', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(36, 'Invisible', 'banenforcer_enforce', 'Mandatory', 'Enforce Active Ban', 'enforceban', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(37, 'Active', 'player_unban', 'Log', 'Unban Player', 'unban', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(38, 'Active', 'self_admins', 'Log', 'Request Online Admins', 'admins', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(39, 'Active', 'self_lead', 'Log', 'Lead Current Squad', 'lead', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(40, 'Active', 'admin_accept', 'Log', 'Accept Round Report', 'accept', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(41, 'Active', 'admin_deny', 'Log', 'Deny Round Report', 'deny', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(42, 'Invisible', 'player_report_deny', 'Log', 'Report Player (Denied)', 'denyreport', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(43, 'Active', 'server_swapnuke', 'Log', 'SwapNuke Server', 'swapnuke', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(44, 'Active', 'player_blacklistdisperse', 'Log', 'Blacklist Disperse Player', 'disperse', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(45, 'Active', 'player_whitelistbalance', 'Log', 'Autobalance Whitelist Player', 'mbwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(46, 'Active', 'player_slotreserved', 'Log', 'Reserved Slot Player', 'reserved', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(47, 'Active', 'player_slotspectator', 'Log', 'Spectator Slot Player', 'spectator', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(48, 'Invisible', 'player_changename', 'Log', 'Player Changed Name', 'changename', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(49, 'Invisible', 'player_changeip', 'Log', 'Player Changed IP', 'changeip', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(50, 'Active', 'player_ban_perm_future', 'Log', 'Future Permaban Player', 'fban', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(51, 'Active', 'self_assist', 'Log', 'Assist Losing Team', 'assist', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(52, 'Active', 'self_uptime', 'Log', 'Request Uptimes', 'uptime', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(53, 'Active', 'self_contest', 'Log', 'Contest Report', 'contest', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(54, 'Active', 'player_kill_force', 'Log', 'Kill Player (Force)', 'fkill', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(55, 'Active', 'player_info', 'Log', 'Fetch Player Info', 'pinfo', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(56, 'Active', 'player_dequeue', 'Log', 'Dequeue Player Action', 'deq', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(57, 'Active', 'self_help', 'Log', 'Request Server Commands', 'help', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(58, 'Active', 'player_find', 'Log', 'Find Player', 'find', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(59, 'Active', 'server_afk', 'Log', 'Manage AFK Players', 'afk', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(60, 'Active', 'player_pull', 'Log', 'Pull Player', 'pull', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(61, 'Active', 'admin_ignore', 'Log', 'Ignore Round Report', 'ignore', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(62, 'Invisible', 'player_report_ignore', 'Log', 'Report Player (Ignored)', 'ignorereport', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(63, 'Active', 'player_mark', 'Unable', 'Mark Player', 'mark', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(64, 'Active', 'player_chat', 'Log', 'Fetch Player Chat', 'pchat', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(65, 'Active', 'player_whitelisthackerchecker', 'Log', 'Hacker-Checker Whitelist Player', 'hcwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(66, 'Active', 'player_lock', 'Log', 'Lock Player Commands', 'lock', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(67, 'Active', 'player_unlock', 'Log', 'Unlock Player Commands', 'unlock', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(68, 'Active', 'self_rep', 'Log', 'Request Server Reputation', 'rep', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(69, 'Invisible', 'player_repboost', 'Log', 'Boost Player Reputation', 'rboost', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(70, 'Active', 'player_log', 'Log', 'Log Player Information', 'log', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(71, 'Active', 'player_whitelistping', 'Log', 'Ping Whitelist Player', 'pwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(72, 'Invisible', 'player_ban_temp_old', 'Log', 'Previous Temp Ban', 'pretban', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(73, 'Invisible', 'player_ban_perm_old', 'Log', 'Previous Perm Ban', 'preban', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(74, 'Active', 'player_pm_send', 'Unable', 'Player Private Message', 'msg', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(75, 'Active', 'player_pm_reply', 'Unable', 'Player Private Reply', 'r', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(76, 'Active', 'admin_pm_send', 'Unable', 'Admin Private Message', 'adminmsg', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(77, 'Active', 'player_whitelistaa', 'Log', 'AA Whitelist Player', 'aawhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(78, 'Active', 'self_surrender', 'Log', 'Vote Surrender', 'surrender', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(79, 'Active', 'self_votenext', 'Log', 'Vote Next Round', 'votenext', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(80, 'Active', 'self_reportlist', 'Log', 'List Round Reports', 'reportlist', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(81, 'Active', 'plugin_restart', 'Log', 'Restart AdKats', 'prestart', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(82, 'Active', 'server_shutdown', 'Log', 'Shutdown Server', 'shutdown', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(83, 'Active', 'self_nosurrender', 'Log', 'Vote Against Surrender', 'nosurrender', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(84, 'Active', 'player_whitelistspambot', 'Log', 'SpamBot Whitelist Player', 'spamwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(85, 'Invisible', 'player_pm_start', 'Log', 'Player Private Message Start', 'pmstart', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(86, 'Invisible', 'player_pm_transmit', 'Log', 'Player Private Message Transmit', 'pmtransmit', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(87, 'Invisible', 'player_pm_cancel', 'Log', 'Player Private Message Cancel', 'pmcancel', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(88, 'Invisible', 'player_population_success', 'Log', 'Player Successfully Populated Server', 'popsuccess', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(89, 'Invisible', 'server_map_detriment', 'Log', 'Map Detriment Log', 'mapdetriment', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(90, 'Invisible', 'server_map_benefit', 'Log', 'Map Benefit Log', 'mapbenefit', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(91, 'Active', 'plugin_update', 'Unable', 'Update AdKats', 'pupdate', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(92, 'Active', 'player_warn', 'Log', 'Warn Player', 'warn', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(93, 'Active', 'server_countdown', 'Log', 'Run Countdown', 'cdown', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(94, 'Active', 'player_whitelistreport', 'Log', 'Report Whitelist Player', 'rwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(95, 'Active', 'player_whitelistreport_remove', 'Log', 'Remove Report Whitelist', 'unrwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(96, 'Active', 'player_whitelistspambot_remove', 'Log', 'Remove SpamBot Whitelist', 'unspamwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(97, 'Active', 'player_whitelistaa_remove', 'Log', 'Remove AA Whitelist', 'unaawhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(98, 'Active', 'player_whitelistping_remove', 'Log', 'Remove Ping Whitelist', 'unpwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(99, 'Active', 'player_whitelisthackerchecker_remove', 'Log', 'Remove Hacker-Checker Whitelist', 'unhcwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(100, 'Active', 'player_slotspectator_remove', 'Log', 'Remove Spectator Slot', 'unspectator', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(101, 'Active', 'player_slotreserved_remove', 'Log', 'Remove Reserved Slot', 'unreserved', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(102, 'Active', 'player_whitelistbalance_remove', 'Log', 'Remove Autobalance Whitelist', 'unmbwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(103, 'Active', 'player_blacklistdisperse_remove', 'Log', 'Remove Autobalance Dispersion', 'undisperse', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(104, 'Active', 'player_whitelistpopulator', 'Log', 'Populator Whitelist Player', 'popwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(105, 'Active', 'player_whitelistpopulator_remove', 'Log', 'Remove Populator Whitelist', 'unpopwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(106, 'Active', 'player_whitelistteamkill', 'Log', 'TeamKillTracker Whitelist Player', 'tkwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(107, 'Active', 'player_whitelistteamkill_remove', 'Log', 'Remove TeamKillTracker Whitelist', 'untkwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(108, 'Invisible', 'self_assist_unconfirmed', 'Log', 'Unconfirmed Assist', 'uassist', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(109, 'Active', 'player_blacklistspectator', 'Log', 'Spectator Blacklist Player', 'specblacklist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(110, 'Active', 'player_blacklistspectator_remove', 'Log', 'Remove Spectator Blacklist', 'unspecblacklist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(111, 'Active', 'player_blacklistreport', 'Log', 'Report Source Blacklist', 'rblacklist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(112, 'Active', 'player_blacklistreport_remove', 'Log', 'Remove Report Source Blacklist', 'unrblacklist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(113, 'Active', 'player_whitelistcommand', 'Log', 'Command Target Whitelist', 'cwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(114, 'Active', 'player_whitelistcommand_remove', 'Log', 'Remove Command Target Whitelist', 'uncwhitelist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(115, 'Active', 'player_blacklistautoassist', 'Log', 'Auto-Assist Blacklist', 'auablacklist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(116, 'Active', 'player_blacklistautoassist_remove', 'Log', 'Remove Auto-Assist Blacklist', 'unauablacklist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(117, 'Active', 'player_isadmin', 'Log', 'Fetch Admin Status', 'isadmin', FALSE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(118, 'Active', 'self_feedback', 'Log', 'Give Server Feedback', 'feedback', FALSE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(119, 'Active', 'player_loadout', 'Log', 'Fetch Player Loadout', 'loadout', FALSE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(120, 'Active', 'player_loadout_force', 'Log', 'Force Player Loadout', 'floadout', TRUE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(121, 'Active', 'self_battlecry', 'Log', 'Set Own Battlecry', 'battlecry', FALSE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(122, 'Active', 'player_battlecry', 'Log', 'Set Player Battlecry', 'setbattlecry', TRUE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(123, 'Active', 'player_perks', 'Log', 'Fetch Player Perks', 'perks', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(124, 'Active', 'player_ping', 'Log', 'Fetch Player Ping', 'ping', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(125, 'Active', 'player_forceping', 'Log', 'Force Manual Player Ping', 'fping', TRUE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(126, 'Active', 'player_debugassist', 'Log', 'Debug Assist Losing Team', 'debugassist', FALSE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(127, 'Invisible', 'player_changetag', 'Mandatory', 'Player Changed Clan Tag', 'changetag', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(128, 'Active', 'player_discordlink', 'Log', 'Link Player to Discord Member', 'discordlink', TRUE, 'AnyHidden');
REPLACE INTO `adkats_commands` VALUES(129, 'Active', 'player_blacklistallcaps', 'Log', 'All-Caps Chat Blacklist', 'allcapsblacklist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(130, 'Active', 'player_blacklistallcaps_remove', 'Log', 'Remove All-Caps Chat Blacklist', 'unallcapsblacklist', TRUE, 'Any');
REPLACE INTO `adkats_commands` VALUES(131, 'Active', 'poll_trigger', 'Log', 'Trigger Poll', 'poll', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(132, 'Active', 'poll_vote', 'Log', 'Vote In Poll', 'vote', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(133, 'Active', 'poll_cancel', 'Unable', 'Cancel Active Poll', 'pollcancel', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(134, 'Active', 'poll_complete', 'Unable', 'Complete Active Poll', 'pollcomplete', FALSE, 'Any');
REPLACE INTO `adkats_commands` VALUES(135, 'Active', 'server_nuke_winning', 'Log', 'Server Nuke Winning Team', 'wnuke', TRUE, 'Any');

DROP TABLE IF EXISTS `adkats_infractions_global`;
CREATE TABLE IF NOT EXISTS `adkats_infractions_global` (
  `player_id` int(11) unsigned NOT NULL,
  `punish_points` int(11) NOT NULL,
  `forgive_points` int(11) NOT NULL,
  `total_points` int(11) NOT NULL,
  PRIMARY KEY (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Global Player Infraction Points';

DROP TABLE IF EXISTS `adkats_infractions_server`;
CREATE TABLE IF NOT EXISTS `adkats_infractions_server` (
  `player_id` int(11) unsigned NOT NULL,
  `server_id` smallint(5) unsigned NOT NULL,
  `punish_points` int(11) NOT NULL,
  `forgive_points` int(11) NOT NULL,
  `total_points` int(11) NOT NULL,
  PRIMARY KEY (`player_id`,`server_id`),
  KEY `adkats_infractions_server_fk_server_id` (`server_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Server Specific Player Infraction Points';

DROP TABLE IF EXISTS `adkats_records_debug`;
CREATE TABLE IF NOT EXISTS `adkats_records_debug` (
  `record_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `server_id` smallint(5) unsigned NOT NULL,
  `command_type` int(11) unsigned NOT NULL,
  `command_action` int(11) unsigned NOT NULL,
  `command_numeric` int(11) NOT NULL DEFAULT '0',
  `target_name` varchar(45) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'NoTarget',
  `target_id` int(11) unsigned DEFAULT NULL,
  `source_name` varchar(45) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'NoSource',
  `source_id` int(11) unsigned DEFAULT NULL,
  `record_message` varchar(500) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'NoMessage',
  `record_time` datetime NOT NULL,
  `adkats_read` enum('Y','N') COLLATE utf8_unicode_ci NOT NULL DEFAULT 'N',
  `adkats_web` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`record_id`),
  KEY `adkats_records_debug_fk_server_id` (`server_id`),
  KEY `adkats_records_debug_fk_command_type` (`command_type`),
  KEY `adkats_records_debug_fk_command_action` (`command_action`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Debug Records';

DROP TABLE IF EXISTS `adkats_records_main`;
CREATE TABLE IF NOT EXISTS `adkats_records_main` (
  `record_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `server_id` smallint(5) unsigned NOT NULL,
  `command_type` int(11) unsigned NOT NULL,
  `command_action` int(11) unsigned NOT NULL,
  `command_numeric` int(11) NOT NULL DEFAULT '0',
  `target_name` varchar(45) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'NoTarget',
  `target_id` int(11) unsigned DEFAULT NULL,
  `source_name` varchar(45) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'NoSource',
  `source_id` int(11) unsigned DEFAULT NULL,
  `record_message` varchar(500) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'NoMessage',
  `record_time` datetime NOT NULL,
  `adkats_read` enum('Y','N') COLLATE utf8_unicode_ci NOT NULL DEFAULT 'N',
  `adkats_web` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`record_id`),
  KEY `adkats_records_main_fk_server_id` (`server_id`),
  KEY `adkats_records_main_fk_command_type` (`command_type`),
  KEY `adkats_records_main_fk_command_action` (`command_action`),
  KEY `adkats_records_main_fk_target_id` (`target_id`),
  KEY `adkats_records_main_fk_source_id` (`source_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Main Records';
DROP TRIGGER IF EXISTS `adkats_infraction_point_delete`;
DELIMITER $$
CREATE TRIGGER `adkats_infraction_point_delete` AFTER DELETE ON `adkats_records_main`
 FOR EACH ROW BEGIN 
        DECLARE command_type VARCHAR(45);
        DECLARE server_id INT(11);
        DECLARE player_id INT(11);
        SET command_type = OLD.command_type;
        SET server_id = OLD.server_id;
        SET player_id = OLD.target_id;

        IF(command_type = 9) THEN
            INSERT INTO `adkats_infractions_server` 
                (`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
            VALUES 
                (player_id, server_id, 0, 0, 0) 
            ON DUPLICATE KEY UPDATE 
                `punish_points` = `punish_points` - 1, 
                `total_points` = `total_points` - 1;
            INSERT INTO `adkats_infractions_global` 
                (`player_id`, `punish_points`, `forgive_points`, `total_points`) 
            VALUES 
                (player_id, 0, 0, 0) 
            ON DUPLICATE KEY UPDATE 
                `punish_points` = `punish_points` - 1, 
                `total_points` = `total_points` - 1;
        ELSEIF (command_type = 10) THEN
            INSERT INTO `adkats_infractions_server` 
                (`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
            VALUES 
                (player_id, server_id, 0, 0, 0) 
            ON DUPLICATE KEY UPDATE 
                `forgive_points` = `forgive_points` - 1, 
                `total_points` = `total_points` + 1;
            INSERT INTO `adkats_infractions_global` 
                (`player_id`, `punish_points`, `forgive_points`, `total_points`) 
            VALUES 
                (player_id, 0, 0, 0) 
            ON DUPLICATE KEY UPDATE 
                `forgive_points` = `forgive_points` - 1, 
                `total_points` = `total_points` + 1;
        END IF;
    END
$$
DELIMITER ;
DROP TRIGGER IF EXISTS `adkats_infraction_point_insert`;
DELIMITER $$
CREATE TRIGGER `adkats_infraction_point_insert` BEFORE INSERT ON `adkats_records_main`
 FOR EACH ROW BEGIN 
        DECLARE command_type VARCHAR(45);
        DECLARE server_id INT(11);
        DECLARE player_id INT(11);
        SET command_type = NEW.command_type;
        SET server_id = NEW.server_id;
        SET player_id = NEW.target_id;

        IF(command_type = 9) THEN
            INSERT INTO `adkats_infractions_server` 
                (`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
            VALUES 
                (player_id, server_id, 1, 0, 1) 
            ON DUPLICATE KEY UPDATE 
                `punish_points` = `punish_points` + 1, 
                `total_points` = `total_points` + 1;
            INSERT INTO `adkats_infractions_global` 
                (`player_id`, `punish_points`, `forgive_points`, `total_points`) 
            VALUES 
                (player_id, 1, 0, 1) 
            ON DUPLICATE KEY UPDATE 
                `punish_points` = `punish_points` + 1, 
                `total_points` = `total_points` + 1;
        ELSEIF (command_type = 10) THEN
            INSERT INTO `adkats_infractions_server` 
                (`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
            VALUES 
                (player_id, server_id, 0, 1, -1) 
            ON DUPLICATE KEY UPDATE 
                `forgive_points` = `forgive_points` + 1, 
                `total_points` = `total_points` - 1;
            INSERT INTO `adkats_infractions_global` 
                (`player_id`, `punish_points`, `forgive_points`, `total_points`) 
            VALUES 
                (player_id, 0, 1, -1) 
            ON DUPLICATE KEY UPDATE 
                `forgive_points` = `forgive_points` + 1, 
                `total_points` = `total_points` - 1;
        END IF;
    END
$$
DELIMITER ;

DROP TABLE IF EXISTS `adkats_rolecommands`;
CREATE TABLE IF NOT EXISTS `adkats_rolecommands` (
  `role_id` int(11) unsigned NOT NULL,
  `command_id` int(11) unsigned NOT NULL,
  PRIMARY KEY (`role_id`,`command_id`),
  KEY `adkats_rolecommands_fk_role` (`role_id`),
  KEY `adkats_rolecommands_fk_command` (`command_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Connection of commands to roles';

-- Default Guest Role
REPLACE INTO `adkats_rolecommands` VALUES(1, 1);
REPLACE INTO `adkats_rolecommands` VALUES(1, 2);
REPLACE INTO `adkats_rolecommands` VALUES(1, 12);
REPLACE INTO `adkats_rolecommands` VALUES(1, 17);
REPLACE INTO `adkats_rolecommands` VALUES(1, 18);
REPLACE INTO `adkats_rolecommands` VALUES(1, 20);
REPLACE INTO `adkats_rolecommands` VALUES(1, 27);
REPLACE INTO `adkats_rolecommands` VALUES(1, 28);
REPLACE INTO `adkats_rolecommands` VALUES(1, 29);
REPLACE INTO `adkats_rolecommands` VALUES(1, 51);
REPLACE INTO `adkats_rolecommands` VALUES(1, 57);
REPLACE INTO `adkats_rolecommands` VALUES(1, 58);
REPLACE INTO `adkats_rolecommands` VALUES(1, 68);
REPLACE INTO `adkats_rolecommands` VALUES(1, 74);
REPLACE INTO `adkats_rolecommands` VALUES(1, 75);
REPLACE INTO `adkats_rolecommands` VALUES(1, 76);
REPLACE INTO `adkats_rolecommands` VALUES(1, 117);
REPLACE INTO `adkats_rolecommands` VALUES(1, 118);

-- Full Admin Role
REPLACE INTO `adkats_rolecommands` VALUES(2, 1);
REPLACE INTO `adkats_rolecommands` VALUES(2, 2);
REPLACE INTO `adkats_rolecommands` VALUES(2, 3);
REPLACE INTO `adkats_rolecommands` VALUES(2, 4);
REPLACE INTO `adkats_rolecommands` VALUES(2, 5);
REPLACE INTO `adkats_rolecommands` VALUES(2, 6);
REPLACE INTO `adkats_rolecommands` VALUES(2, 7);
REPLACE INTO `adkats_rolecommands` VALUES(2, 8);
REPLACE INTO `adkats_rolecommands` VALUES(2, 9);
REPLACE INTO `adkats_rolecommands` VALUES(2, 10);
REPLACE INTO `adkats_rolecommands` VALUES(2, 11);
REPLACE INTO `adkats_rolecommands` VALUES(2, 12);
REPLACE INTO `adkats_rolecommands` VALUES(2, 14);
REPLACE INTO `adkats_rolecommands` VALUES(2, 15);
REPLACE INTO `adkats_rolecommands` VALUES(2, 16);
REPLACE INTO `adkats_rolecommands` VALUES(2, 17);
REPLACE INTO `adkats_rolecommands` VALUES(2, 18);
REPLACE INTO `adkats_rolecommands` VALUES(2, 19);
REPLACE INTO `adkats_rolecommands` VALUES(2, 20);
REPLACE INTO `adkats_rolecommands` VALUES(2, 21);
REPLACE INTO `adkats_rolecommands` VALUES(2, 22);
REPLACE INTO `adkats_rolecommands` VALUES(2, 23);
REPLACE INTO `adkats_rolecommands` VALUES(2, 24);
REPLACE INTO `adkats_rolecommands` VALUES(2, 25);
REPLACE INTO `adkats_rolecommands` VALUES(2, 26);
REPLACE INTO `adkats_rolecommands` VALUES(2, 27);
REPLACE INTO `adkats_rolecommands` VALUES(2, 28);
REPLACE INTO `adkats_rolecommands` VALUES(2, 29);
REPLACE INTO `adkats_rolecommands` VALUES(2, 30);
REPLACE INTO `adkats_rolecommands` VALUES(2, 31);
REPLACE INTO `adkats_rolecommands` VALUES(2, 32);
REPLACE INTO `adkats_rolecommands` VALUES(2, 33);
REPLACE INTO `adkats_rolecommands` VALUES(2, 34);
REPLACE INTO `adkats_rolecommands` VALUES(2, 35);
REPLACE INTO `adkats_rolecommands` VALUES(2, 36);
REPLACE INTO `adkats_rolecommands` VALUES(2, 37);
REPLACE INTO `adkats_rolecommands` VALUES(2, 38);
REPLACE INTO `adkats_rolecommands` VALUES(2, 39);
REPLACE INTO `adkats_rolecommands` VALUES(2, 40);
REPLACE INTO `adkats_rolecommands` VALUES(2, 41);
REPLACE INTO `adkats_rolecommands` VALUES(2, 43);
REPLACE INTO `adkats_rolecommands` VALUES(2, 44);
REPLACE INTO `adkats_rolecommands` VALUES(2, 45);
REPLACE INTO `adkats_rolecommands` VALUES(2, 46);
REPLACE INTO `adkats_rolecommands` VALUES(2, 47);
REPLACE INTO `adkats_rolecommands` VALUES(2, 50);
REPLACE INTO `adkats_rolecommands` VALUES(2, 51);
REPLACE INTO `adkats_rolecommands` VALUES(2, 52);
REPLACE INTO `adkats_rolecommands` VALUES(2, 53);
REPLACE INTO `adkats_rolecommands` VALUES(2, 54);
REPLACE INTO `adkats_rolecommands` VALUES(2, 55);
REPLACE INTO `adkats_rolecommands` VALUES(2, 56);
REPLACE INTO `adkats_rolecommands` VALUES(2, 57);
REPLACE INTO `adkats_rolecommands` VALUES(2, 58);
REPLACE INTO `adkats_rolecommands` VALUES(2, 59);
REPLACE INTO `adkats_rolecommands` VALUES(2, 60);
REPLACE INTO `adkats_rolecommands` VALUES(2, 61);
REPLACE INTO `adkats_rolecommands` VALUES(2, 63);
REPLACE INTO `adkats_rolecommands` VALUES(2, 64);
REPLACE INTO `adkats_rolecommands` VALUES(2, 65);
REPLACE INTO `adkats_rolecommands` VALUES(2, 66);
REPLACE INTO `adkats_rolecommands` VALUES(2, 67);
REPLACE INTO `adkats_rolecommands` VALUES(2, 68);
REPLACE INTO `adkats_rolecommands` VALUES(2, 70);
REPLACE INTO `adkats_rolecommands` VALUES(2, 71);
REPLACE INTO `adkats_rolecommands` VALUES(2, 74);
REPLACE INTO `adkats_rolecommands` VALUES(2, 75);
REPLACE INTO `adkats_rolecommands` VALUES(2, 76);
REPLACE INTO `adkats_rolecommands` VALUES(2, 77);
REPLACE INTO `adkats_rolecommands` VALUES(2, 78);
REPLACE INTO `adkats_rolecommands` VALUES(2, 79);
REPLACE INTO `adkats_rolecommands` VALUES(2, 80);
REPLACE INTO `adkats_rolecommands` VALUES(2, 81);
REPLACE INTO `adkats_rolecommands` VALUES(2, 82);
REPLACE INTO `adkats_rolecommands` VALUES(2, 83);
REPLACE INTO `adkats_rolecommands` VALUES(2, 84);
REPLACE INTO `adkats_rolecommands` VALUES(2, 91);
REPLACE INTO `adkats_rolecommands` VALUES(2, 92);
REPLACE INTO `adkats_rolecommands` VALUES(2, 93);
REPLACE INTO `adkats_rolecommands` VALUES(2, 94);
REPLACE INTO `adkats_rolecommands` VALUES(2, 95);
REPLACE INTO `adkats_rolecommands` VALUES(2, 96);
REPLACE INTO `adkats_rolecommands` VALUES(2, 97);
REPLACE INTO `adkats_rolecommands` VALUES(2, 98);
REPLACE INTO `adkats_rolecommands` VALUES(2, 99);
REPLACE INTO `adkats_rolecommands` VALUES(2, 100);
REPLACE INTO `adkats_rolecommands` VALUES(2, 101);
REPLACE INTO `adkats_rolecommands` VALUES(2, 102);
REPLACE INTO `adkats_rolecommands` VALUES(2, 107);
REPLACE INTO `adkats_rolecommands` VALUES(2, 109);
REPLACE INTO `adkats_rolecommands` VALUES(2, 110);
REPLACE INTO `adkats_rolecommands` VALUES(2, 111);
REPLACE INTO `adkats_rolecommands` VALUES(2, 112);
REPLACE INTO `adkats_rolecommands` VALUES(2, 113);
REPLACE INTO `adkats_rolecommands` VALUES(2, 114);
REPLACE INTO `adkats_rolecommands` VALUES(2, 115);
REPLACE INTO `adkats_rolecommands` VALUES(2, 116);
REPLACE INTO `adkats_rolecommands` VALUES(2, 117);
REPLACE INTO `adkats_rolecommands` VALUES(2, 118);

DROP TABLE IF EXISTS `adkats_roles`;
CREATE TABLE IF NOT EXISTS `adkats_roles` (
  `role_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `role_key` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
  `role_name` varchar(255) COLLATE utf8_unicode_ci NOT NULL,
  PRIMARY KEY (`role_id`),
  UNIQUE KEY `role_key_UNIQUE` (`role_key`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Role List';

REPLACE INTO `adkats_roles` VALUES(1, 'guest_default', 'Default Guest');
REPLACE INTO `adkats_roles` VALUES(2, 'admin_full', 'Full Admin');

DROP TABLE IF EXISTS `adkats_settings`;
CREATE TABLE IF NOT EXISTS `adkats_settings` (
  `server_id` smallint(5) unsigned NOT NULL,
  `setting_name` varchar(200) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'SettingName',
  `setting_type` varchar(45) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'SettingType',
  `setting_value` varchar(3000) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'SettingValue',
  PRIMARY KEY (`server_id`,`setting_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Server Setting List';

DROP TABLE IF EXISTS `adkats_users`;
CREATE TABLE IF NOT EXISTS `adkats_users` (
  `user_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `user_name` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
  `user_email` varchar(255) COLLATE utf8_unicode_ci,
  `user_phone` varchar(45) COLLATE utf8_unicode_ci,
  `user_role` int(11) unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`user_id`),
  KEY `adkats_users_fk_role` (`user_role`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - User List';

DROP TABLE IF EXISTS `adkats_usersoldiers`;
CREATE TABLE IF NOT EXISTS `adkats_usersoldiers` (
  `user_id` int(11) unsigned NOT NULL,
  `player_id` int(10) unsigned NOT NULL,
  PRIMARY KEY (`user_id`,`player_id`),
  KEY `adkats_usersoldiers_fk_user` (`user_id`),
  KEY `adkats_usersoldiers_fk_player` (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Connection of users to soldiers';

DROP TABLE IF EXISTS `adkats_specialplayers`;
CREATE TABLE IF NOT EXISTS `adkats_specialplayers`( 
  `specialplayer_id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `player_group` varchar(30) COLLATE utf8_unicode_ci NOT NULL,
  `player_id` int(10) UNSIGNED DEFAULT NULL,
  `player_game` tinyint(4) UNSIGNED DEFAULT NULL,
  `player_server` smallint(5) UNSIGNED DEFAULT NULL,
  `player_identifier` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
  `player_effective` DATETIME NOT NULL,
  `player_expiration` DATETIME NOT NULL,
  PRIMARY KEY (`specialplayer_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Special Player List';

DROP TABLE IF EXISTS `adkats_player_reputation`;
CREATE TABLE IF NOT EXISTS `adkats_player_reputation` (
  `player_id` int(10) unsigned NOT NULL,
  `game_id` tinyint(4) unsigned NOT NULL,
  `target_rep` float NOT NULL,
  `source_rep` float NOT NULL,
  `total_rep` float NOT NULL,
  `total_rep_co` float NOT NULL,
  PRIMARY KEY (`player_id`),
  KEY `game_id` (`game_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Player Reputation';

DROP TABLE IF EXISTS `adkats_orchestration`;
CREATE TABLE IF NOT EXISTS `adkats_orchestration` (
	`setting_id` int(10) NOT NULL AUTO_INCREMENT,
	`setting_server` SMALLINT(5) NOT NULL,
	`setting_plugin` VARCHAR(100) NOT NULL,
	`setting_name` VARCHAR(100) NOT NULL,
	`setting_value` VARCHAR (2000) NOT NULL,
	PRIMARY KEY (`setting_id`),
	UNIQUE(`setting_server`, `setting_plugin`, `setting_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Plugin Orchestration';

DROP TABLE IF EXISTS `tbl_extendedroundstats`;
CREATE TABLE `tbl_extendedroundstats` (
	`roundstat_id` int(10) unsigned NOT NULL AUTO_INCREMENT,
	`server_id` smallint(5) unsigned NOT NULL,
	`round_id` int(10) unsigned NOT NULL,
	`round_elapsedTimeSec` int(10) unsigned NOT NULL,
	`team1_count` int(10) unsigned NOT NULL,
	`team2_count` int(10) unsigned NOT NULL,
	`team1_score` int(10) NOT NULL,
	`team2_score` int(10) NOT NULL,
	`team1_spm` double NOT NULL,
	`team2_spm` double NOT NULL,
	`team1_tickets` int(10) NOT NULL,
	`team2_tickets` int(10) NOT NULL,
	`team1_tpm` double NOT NULL,
	`team2_tpm` double NOT NULL,
	`roundstat_time` datetime NOT NULL,
	PRIMARY KEY (`roundstat_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Extended Round Stats';

DROP TABLE IF EXISTS `adkats_statistics`;
CREATE TABLE `adkats_statistics` (
  `stat_id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `server_id` SMALLINT(5) UNSIGNED NOT NULL,
  `round_id` INT(10) UNSIGNED NOT NULL,
  `stat_type` varchar(50) NOT NULL,
  `target_name` varchar(50) NOT NULL,
  `target_id` INT(10) UNSIGNED DEFAULT NULL,
  `stat_value` FLOAT NOT NULL,
  `stat_comment` TEXT,
  `stat_time` DATETIME NOT NULL DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (`stat_id`),
  KEY `server_id` (`server_id`),
  KEY `stat_type` (`stat_type`),
  KEY `target_id` (`target_id`),
  KEY `stat_time` (`stat_time`),
  CONSTRAINT `adkats_statistics_target_id_fk` FOREIGN KEY (`target_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `adkats_statistics_server_id_fk` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Statistics';

DROP TABLE IF EXISTS `adkats_rolegroups`;
CREATE TABLE `adkats_rolegroups` (
  `role_id` int(11) unsigned NOT NULL,
  `group_key` VARCHAR(100) NOT NULL,
  PRIMARY KEY (`role_id`,`group_key`),
  KEY `adkats_rolegroups_fk_role` (`role_id`),
  KEY `adkats_rolegroups_fk_command` (`group_key`),
  CONSTRAINT `adkats_rolegroups_fk_role` FOREIGN KEY (`role_id`) REFERENCES `adkats_roles` (`role_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Connection of groups to roles';

DROP TABLE IF EXISTS `adkats_challenge_entry_detail`;
DROP TABLE IF EXISTS `adkats_challenge_entry`;
DROP TABLE IF EXISTS `adkats_challenge_rule`;
DROP TABLE IF EXISTS `adkats_challenge_definition_detail`;
DROP TABLE IF EXISTS `adkats_challenge_definition`;

CREATE TABLE `adkats_challenge_definition` (
  `ID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `Name` varchar(200) COLLATE utf8_unicode_ci NOT NULL,
  `CreateTime` datetime NOT NULL,
  `ModifyTime` datetime NOT NULL,
  PRIMARY KEY (`ID`),
  UNIQUE KEY `adkats_challenge_definition_idx_Name` (`Name`),
  KEY `adkats_challenge_definition_idx_CreateTime` (`CreateTime`),
  KEY `adkats_challenge_definition_idx_ModifyTime` (`ModifyTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Definitions';

CREATE TABLE `adkats_challenge_definition_detail` (
  `DefID` int(10) unsigned NOT NULL,
  `DetailID` int(10) unsigned NOT NULL,
  `Type` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
  `Damage` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
  `WeaponCount` int(10) unsigned NOT NULL,
  `Weapon` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
  `KillCount` int(10) unsigned NOT NULL,
  `CreateTime` datetime NOT NULL,
  `ModifyTime` datetime NOT NULL,
  PRIMARY KEY (`DefID`, `DetailID`),
  KEY `adkats_challenge_definition_detail_idx_CreateTime` (`CreateTime`),
  KEY `adkats_challenge_definition_detail_idx_ModifyTime` (`ModifyTime`),
  CONSTRAINT `adkats_challenge_definition_detail_fk_DefID` FOREIGN KEY (`DefID`) REFERENCES `adkats_challenge_definition` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Definition Details';

CREATE TABLE `adkats_challenge_rule` (
  `ID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `ServerID` smallint(5) unsigned NOT NULL,
  `DefID` int(10) unsigned NOT NULL,
  `Enabled` int(1) unsigned NOT NULL DEFAULT 1,
  `Name` varchar(200) COLLATE utf8_unicode_ci NOT NULL,
  `Tier` int(10) unsigned NOT NULL DEFAULT 1,
  `CompletionType` varchar(100) COLLATE utf8_unicode_ci NOT NULL DEFAULT "None",
  `RoundCount` int(10) unsigned NOT NULL DEFAULT 1,
  `DurationMinutes` int(10) unsigned NOT NULL DEFAULT 60, -- 4294967295
  `DeathCount` int(10) unsigned NOT NULL DEFAULT 1,
  `CreateTime` datetime NOT NULL,
  `ModifyTime` datetime NOT NULL,
  `RoundLastUsedTime` datetime NOT NULL DEFAULT "1970-01-01 00:00:00",
  `PersonalLastUsedTime` datetime NOT NULL DEFAULT "1970-01-01 00:00:00",
  PRIMARY KEY (`ID`),
  UNIQUE KEY `adkats_challenge_rule_idx_Name_Server` (`Name`, `ServerID`),
  KEY `adkats_challenge_rule_idx_ServerID` (`ServerID`),
  KEY `adkats_challenge_rule_idx_DefID` (`DefID`),
  KEY `adkats_challenge_rule_idx_CreateTime` (`CreateTime`),
  KEY `adkats_challenge_rule_idx_ModifyTime` (`ModifyTime`),
  KEY `adkats_challenge_rule_idx_RoundLastUsedTime` (`RoundLastUsedTime`),
  KEY `adkats_challenge_rule_idx_PersonalLastUsedTime` (`PersonalLastUsedTime`),
  CONSTRAINT `adkats_challenge_rule_fk_ServerID` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE NO ACTION ON UPDATE CASCADE, -- No action for delete. If people move their servers, don't want to lose this record.
  CONSTRAINT `adkats_challenge_rule_fk_DefID` FOREIGN KEY (`DefID`) REFERENCES `adkats_challenge_definition` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Rules';

CREATE TABLE `adkats_challenge_entry` (
  `ID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `PlayerID` int(10) unsigned NOT NULL,
  `RuleID` int(10) unsigned NOT NULL,
  `Completed` int(1) unsigned NOT NULL,
  `Failed` int(1) unsigned NOT NULL,
  `Canceled` int(1) unsigned NOT NULL,
  `StartRound` int(10) unsigned NOT NULL,
  `StartTime` datetime NOT NULL,
  `CompleteTime` datetime NOT NULL,
  PRIMARY KEY (`ID`),
  KEY `adkats_challenge_entry_idx_PlayerID` (`PlayerID`),
  KEY `adkats_challenge_entry_idx_RuleID` (`RuleID`),
  KEY `adkats_challenge_entry_idx_StartTime` (`StartTime`),
  KEY `adkats_challenge_entry_idx_CompleteTime` (`CompleteTime`),
  CONSTRAINT `adkats_challenge_entry_fk_Play erID` FOREIGN KEY (`PlayerID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `adkats_challenge_entry_fk_RuleID` FOREIGN KEY (`RuleID`) REFERENCES `adkats_challenge_rule` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Entries';

CREATE TABLE `adkats_challenge_entry_detail` (
  `EntryID` int(10) unsigned NOT NULL,
  `DetailID` int(10) unsigned NOT NULL,
  `VictimID` int(10) unsigned NOT NULL,
  `Weapon` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
  `RoundID` int(10) unsigned NOT NULL,
  `DetailTime` datetime NOT NULL,
  PRIMARY KEY (`EntryID`, `DetailID`),
  KEY `adkats_challenge_entry_detail_idx_VictimID` (`VictimID`),
  KEY `adkats_challenge_entry_detail_idx_DetailTime` (`DetailTime`),
  CONSTRAINT `adkats_challenge_entry_detail_fk_EntryID` FOREIGN KEY (`EntryID`) REFERENCES `adkats_challenge_entry` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `adkats_challenge_entry_detail_fk_VictimID` FOREIGN KEY (`VictimID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Entry Details';

SET FOREIGN_KEY_CHECKS=1;

ALTER TABLE `adkats_bans`
  ADD CONSTRAINT `adkats_bans_fk_latest_record_id` FOREIGN KEY (`latest_record_id`) REFERENCES `adkats_records_main` (`record_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_bans_fk_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_infractions_global`
  ADD CONSTRAINT `adkats_infractions_global_fk_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_infractions_server`
  ADD CONSTRAINT `adkats_infractions_server_fk_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_infractions_server_fk_server_id` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_records_debug`
  ADD CONSTRAINT `adkats_records_debug_fk_command_action` FOREIGN KEY (`command_action`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_records_debug_fk_command_type` FOREIGN KEY (`command_type`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_records_debug_fk_server_id` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_records_main`
  ADD CONSTRAINT `adkats_records_main_fk_command_action` FOREIGN KEY (`command_action`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_records_main_fk_command_type` FOREIGN KEY (`command_type`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_records_main_fk_server_id` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_records_main_fk_source_id` FOREIGN KEY (`source_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_records_main_fk_target_id` FOREIGN KEY (`target_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_rolecommands`
  ADD CONSTRAINT `adkats_rolecommands_fk_role` FOREIGN KEY (`role_id`) REFERENCES `adkats_roles` (`role_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_rolecommands_fk_command` FOREIGN KEY (`command_id`) REFERENCES `adkats_commands` (`command_id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_settings`
  ADD CONSTRAINT `adkats_settings_fk_server_id` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_users`
  ADD CONSTRAINT `adkats_users_fk_role` FOREIGN KEY (`user_role`) REFERENCES `adkats_roles` (`role_id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_usersoldiers`
  ADD CONSTRAINT `adkats_usersoldiers_fk_player` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `adkats_usersoldiers_fk_user` FOREIGN KEY (`user_id`) REFERENCES `adkats_users` (`user_id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `adkats_specialplayers`
  ADD CONSTRAINT `adkats_specialplayers_game_id` FOREIGN KEY (`player_game`) REFERENCES `tbl_games`(`GameID`) ON UPDATE NO ACTION ON DELETE CASCADE, 
  ADD CONSTRAINT `adkats_specialplayers_server_id` FOREIGN KEY (`player_server`) REFERENCES `tbl_server`(`ServerID`) ON UPDATE NO ACTION ON DELETE CASCADE, 
  ADD CONSTRAINT `adkats_specialplayers_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata`(`PlayerID`) ON UPDATE NO ACTION ON DELETE CASCADE;

ALTER TABLE `adkats_battlecries` 
  ADD CONSTRAINT `adkats_battlecries_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata`(`PlayerID`) ON UPDATE NO ACTION ON DELETE CASCADE;
