-- AdKats Setup Script by ColColonCleaner
-- Version 4.0.0.0

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
        SET NEW.logPlayerID = (SELECT `PlayerID` FROM `tbl_playerdata` WHERE `SoldierName` = NEW.logSoldierName LIMIT 1);
    END
$$

DELIMITER ;

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
  `command_active` enum('Active','Disabled','Invisible') COLLATE utf8_unicode_ci NOT NULL DEFAULT 'Active',
  `command_key` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
  `command_logging` ENUM('Log','Mandatory','Ignore', 'Unable') CHARACTER SET 'utf8' COLLATE 'utf8_unicode_ci' NOT NULL DEFAULT 'Log',
  `command_name` varchar(255) COLLATE utf8_unicode_ci NOT NULL,
  `command_text` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
  `command_playerInteraction` BOOLEAN NOT NULL,
  PRIMARY KEY (`command_id`),
  UNIQUE KEY `command_key_UNIQUE` (`command_key`),
  UNIQUE KEY `command_text_UNIQUE` (`command_text`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Command List';

INSERT INTO `adkats_commands` VALUES(1, 'Active', 'command_confirm', 'Unable', 'Confirm Command', 'yes', FALSE);
INSERT INTO `adkats_commands` VALUES(2, 'Active', 'command_cancel', 'Unable', 'Cancel Command', 'no', FALSE);
INSERT INTO `adkats_commands` VALUES(3, 'Active', 'player_kill', 'Log', 'Kill Player', 'kill', TRUE);
INSERT INTO `adkats_commands` VALUES(4, 'Invisible', 'player_kill_lowpop', 'Log', 'Kill Player (Low Population)', 'lowpopkill', TRUE);
INSERT INTO `adkats_commands` VALUES(5, 'Invisible', 'player_kill_repeat', 'Log', 'Kill Player (Repeat Kill)', 'repeatkill', TRUE);
INSERT INTO `adkats_commands` VALUES(6, 'Active', 'player_kick', 'Log', 'Kick Player', 'kick', TRUE);
INSERT INTO `adkats_commands` VALUES(7, 'Active', 'player_ban_temp', 'Log', 'Temp-Ban Player', 'tban', TRUE);
INSERT INTO `adkats_commands` VALUES(8, 'Active', 'player_ban_perm', 'Log', 'Permaban Player', 'ban', TRUE);
INSERT INTO `adkats_commands` VALUES(9, 'Active', 'player_punish', 'Mandatory', 'Punish Player', 'punish', TRUE);
INSERT INTO `adkats_commands` VALUES(10, 'Active', 'player_forgive', 'Mandatory', 'Forgive Player', 'forgive', TRUE);
INSERT INTO `adkats_commands` VALUES(11, 'Active', 'player_mute', 'Log', 'Mute Player', 'mute', TRUE);
INSERT INTO `adkats_commands` VALUES(12, 'Active', 'player_join', 'Log', 'Join Player', 'join', FALSE);
INSERT INTO `adkats_commands` VALUES(13, 'Active', 'player_roundwhitelist', 'Ignore', 'Round Whitelist Player', 'roundwhitelist', TRUE);
INSERT INTO `adkats_commands` VALUES(14, 'Active', 'player_move', 'Log', 'On-Death Move Player', 'move', TRUE);
INSERT INTO `adkats_commands` VALUES(15, 'Active', 'player_fmove', 'Log', 'Force Move Player', 'fmove', TRUE);
INSERT INTO `adkats_commands` VALUES(16, 'Active', 'self_teamswap', 'Log', 'Teamswap Self', 'moveme', FALSE);
INSERT INTO `adkats_commands` VALUES(17, 'Active', 'self_kill', 'Log', 'Kill Self', 'killme', FALSE);
INSERT INTO `adkats_commands` VALUES(18, 'Active', 'player_report', 'Log', 'Report Player', 'report', FALSE);
INSERT INTO `adkats_commands` VALUES(19, 'Invisible', 'player_report_confirm', 'Log', 'Report Player (Confirmed)', 'confirmreport', TRUE);
INSERT INTO `adkats_commands` VALUES(20, 'Active', 'player_calladmin', 'Log', 'Call Admin on Player', 'admin', FALSE);
INSERT INTO `adkats_commands` VALUES(21, 'Active', 'admin_say', 'Log', 'Admin Say', 'say', TRUE);
INSERT INTO `adkats_commands` VALUES(22, 'Active', 'player_say', 'Log', 'Player Say', 'psay', TRUE);
INSERT INTO `adkats_commands` VALUES(23, 'Active', 'admin_yell', 'Log', 'Admin Yell', 'yell', TRUE);
INSERT INTO `adkats_commands` VALUES(24, 'Active', 'player_yell', 'Log', 'Player Yell', 'pyell', TRUE);
INSERT INTO `adkats_commands` VALUES(25, 'Active', 'admin_tell', 'Log', 'Admin Tell', 'tell', TRUE);
INSERT INTO `adkats_commands` VALUES(26, 'Active', 'player_tell', 'Log', 'Player Tell', 'ptell', TRUE);
INSERT INTO `adkats_commands` VALUES(27, 'Active', 'self_whatis', 'Unable', 'What Is', 'whatis', FALSE);
INSERT INTO `adkats_commands` VALUES(28, 'Active', 'self_voip', 'Unable', 'VOIP', 'voip', FALSE);
INSERT INTO `adkats_commands` VALUES(29, 'Active', 'self_rules', 'Log', 'Request Rules', 'rules', FALSE);
INSERT INTO `adkats_commands` VALUES(30, 'Active', 'round_restart', 'Log', 'Restart Current Round', 'restart', TRUE);
INSERT INTO `adkats_commands` VALUES(31, 'Active', 'round_next', 'Log', 'Run Next Round', 'nextlevel', TRUE);
INSERT INTO `adkats_commands` VALUES(32, 'Active', 'round_end', 'Log', 'End Current Round', 'endround', TRUE);
INSERT INTO `adkats_commands` VALUES(33, 'Active', 'server_nuke', 'Log', 'Server Nuke', 'nuke', TRUE);
INSERT INTO `adkats_commands` VALUES(34, 'Active', 'server_kickall', 'Log', 'Kick All Guests', 'kickall', TRUE);
INSERT INTO `adkats_commands` VALUES(35, 'Invisible', 'adkats_exception', 'Mandatory', 'Logged Exception', 'logexception', FALSE);
INSERT INTO `adkats_commands` VALUES(36, 'Invisible', 'banenforcer_enforce', 'Mandatory', 'Enforce Active Ban', 'enforceban', TRUE);
INSERT INTO `adkats_commands` VALUES(37, 'Active', 'player_unban', 'Log', 'Unban Player', 'unban', TRUE);

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

INSERT INTO `adkats_rolecommands` VALUES(1, 1);
INSERT INTO `adkats_rolecommands` VALUES(1, 2);
INSERT INTO `adkats_rolecommands` VALUES(1, 12);
INSERT INTO `adkats_rolecommands` VALUES(1, 17);
INSERT INTO `adkats_rolecommands` VALUES(1, 18);
INSERT INTO `adkats_rolecommands` VALUES(1, 20);
INSERT INTO `adkats_rolecommands` VALUES(1, 27);
INSERT INTO `adkats_rolecommands` VALUES(1, 28);
INSERT INTO `adkats_rolecommands` VALUES(1, 29);
INSERT INTO `adkats_rolecommands` VALUES(2, 1);
INSERT INTO `adkats_rolecommands` VALUES(2, 2);
INSERT INTO `adkats_rolecommands` VALUES(2, 3);
INSERT INTO `adkats_rolecommands` VALUES(2, 4);
INSERT INTO `adkats_rolecommands` VALUES(2, 5);
INSERT INTO `adkats_rolecommands` VALUES(2, 6);
INSERT INTO `adkats_rolecommands` VALUES(2, 7);
INSERT INTO `adkats_rolecommands` VALUES(2, 8);
INSERT INTO `adkats_rolecommands` VALUES(2, 9);
INSERT INTO `adkats_rolecommands` VALUES(2, 10);
INSERT INTO `adkats_rolecommands` VALUES(2, 11);
INSERT INTO `adkats_rolecommands` VALUES(2, 12);
INSERT INTO `adkats_rolecommands` VALUES(2, 13);
INSERT INTO `adkats_rolecommands` VALUES(2, 14);
INSERT INTO `adkats_rolecommands` VALUES(2, 15);
INSERT INTO `adkats_rolecommands` VALUES(2, 16);
INSERT INTO `adkats_rolecommands` VALUES(2, 17);
INSERT INTO `adkats_rolecommands` VALUES(2, 18);
INSERT INTO `adkats_rolecommands` VALUES(2, 19);
INSERT INTO `adkats_rolecommands` VALUES(2, 20);
INSERT INTO `adkats_rolecommands` VALUES(2, 21);
INSERT INTO `adkats_rolecommands` VALUES(2, 22);
INSERT INTO `adkats_rolecommands` VALUES(2, 23);
INSERT INTO `adkats_rolecommands` VALUES(2, 24);
INSERT INTO `adkats_rolecommands` VALUES(2, 25);
INSERT INTO `adkats_rolecommands` VALUES(2, 26);
INSERT INTO `adkats_rolecommands` VALUES(2, 27);
INSERT INTO `adkats_rolecommands` VALUES(2, 28);
INSERT INTO `adkats_rolecommands` VALUES(2, 29);
INSERT INTO `adkats_rolecommands` VALUES(2, 30);
INSERT INTO `adkats_rolecommands` VALUES(2, 31);
INSERT INTO `adkats_rolecommands` VALUES(2, 32);
INSERT INTO `adkats_rolecommands` VALUES(2, 33);
INSERT INTO `adkats_rolecommands` VALUES(2, 34);
INSERT INTO `adkats_rolecommands` VALUES(2, 35);
INSERT INTO `adkats_rolecommands` VALUES(2, 36);

DROP TABLE IF EXISTS `adkats_roles`;
CREATE TABLE IF NOT EXISTS `adkats_roles` (
  `role_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `role_key` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
  `role_name` varchar(255) COLLATE utf8_unicode_ci NOT NULL,
  PRIMARY KEY (`role_id`),
  UNIQUE KEY `role_key_UNIQUE` (`role_key`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Role List';

INSERT INTO `adkats_roles` VALUES(1, 'guest_default', 'Default Guest');
INSERT INTO `adkats_roles` VALUES(2, 'admin_full', 'Full Admin');

DROP TABLE IF EXISTS `adkats_settings`;
CREATE TABLE IF NOT EXISTS `adkats_settings` (
  `server_id` smallint(5) unsigned NOT NULL,
  `setting_name` varchar(200) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'SettingName',
  `setting_type` varchar(45) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'SettingType',
  `setting_value` varchar(1500) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'SettingValue',
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
