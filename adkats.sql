-- AdKats Database Setup Script by ColColonCleaner

-- This is run automatically if AdKats senses the database is not set up properly.
-- If you don't want the plugin changing tables/views in your database, you must run this beforehand.

CREATE TABLE IF NOT EXISTS `adkats_accesslist` ( 
       `player_name` VARCHAR(20) NOT NULL, 
	`member_id` INT(11) UNSIGNED NOT NULL DEFAULT 0, 
	`player_email` VARCHAR(254) NOT NULL DEFAULT "test@gmail.com", 
	`access_level` INT(11) NOT NULL DEFAULT 6, 
	PRIMARY KEY (`player_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Access List';

CREATE TABLE IF NOT EXISTS `adkats_records` (
	`record_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`command_type` VARCHAR(45) NOT NULL DEFAULT "DefaultCommand", 
	`command_action` VARCHAR(45) NOT NULL DEFAULT "DefaultAction", 
	`command_numeric` INT(11) NOT NULL DEFAULT 0, 
	`target_name` VARCHAR(45) NOT NULL DEFAULT "NoTarget", 
	`target_id` INT(11) UNSIGNED DEFAULT NULL, 
	`source_name` VARCHAR(45) NOT NULL DEFAULT "NoNameAdmin", 
	`record_message` VARCHAR(500) NOT NULL DEFAULT "NoMessage", 
	`record_time` TIMESTAMP DEFAULT CURRENT_TIMESTAMP, 
	`adkats_read` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`adkats_web` BOOL NOT NULL DEFAULT 0,
	PRIMARY KEY (`record_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Records';
-- ALTER TABLE `adkats_records` ADD 
-- 	CONSTRAINT `adkats_records_fk_server_id` 
-- 		FOREIGN KEY (`server_id`) 
-- 		REFERENCES tbl_server(ServerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;
-- ALTER TABLE `adkats_records` ADD 
-- 	CONSTRAINT `adkats_records_fk_target_id` 
-- 		FOREIGN KEY (`target_id`) 
-- 		REFERENCES tbl_playerdata(PlayerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

CREATE TABLE IF NOT EXISTS `adkats_serverPlayerPoints` (
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`punish_points` INT(11) NOT NULL, 
	`forgive_points` INT(11) NOT NULL, 
	`total_points` INT(11) NOT NULL, 
	PRIMARY KEY (`player_id`, `server_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Server Specific Player Points';
-- ALTER TABLE `adkats_serverPlayerPoints` ADD 
-- 	CONSTRAINT `adkats_serverPlayerPoints_fk_player_id` 
-- 		FOREIGN KEY (`player_id`) 
-- 		REFERENCES tbl_playerdata(PlayerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;
-- ALTER TABLE `adkats_serverPlayerPoints` ADD 
-- 	CONSTRAINT `adkats_serverPlayerPoints_fk_server_id` 
-- 		FOREIGN KEY (`server_id`) 
-- 		REFERENCES tbl_server(ServerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

CREATE TABLE IF NOT EXISTS `adkats_globalPlayerPoints` (
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`punish_points` INT(11) NOT NULL, 
	`forgive_points` INT(11) NOT NULL, 
	`total_points` INT(11) NOT NULL, 
	PRIMARY KEY (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Global Player Points';
-- ALTER TABLE `adkats_globalPlayerPoints` ADD 
-- 	CONSTRAINT `adkats_globalPlayerPoints_fk_player_id` 
-- 		FOREIGN KEY (`player_id`) 
-- 		REFERENCES tbl_playerdata(PlayerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

CREATE TABLE IF NOT EXISTS `adkats_banlist` ( 
	`ban_id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT, 
	`player_id` INT(11) UNSIGNED NOT NULL, 
	`latest_record_id` INT(11) UNSIGNED NOT NULL, 
	`ban_notes` VARCHAR(150) NOT NULL DEFAULT 'NoNotes', 
	`ban_status` enum('Active', 'Expired', 'Disabled') NOT NULL DEFAULT 'Active',
	`ban_startTime` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP, 
	`ban_endTime` DATETIME NOT NULL, 
	`ban_enforceName` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`ban_enforceGUID` ENUM('Y', 'N') NOT NULL DEFAULT 'Y', 
	`ban_enforceIP` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
	`ban_sync` VARCHAR(100) NOT NULL DEFAULT "-sync-", 
	PRIMARY KEY (`ban_id`), 
	UNIQUE KEY `player_id_UNIQUE` (`player_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Ban Enforcer List';
-- ALTER TABLE `adkats_banlist` ADD 
-- 	CONSTRAINT `adkats_banlist_fk_player_id` 
-- 		FOREIGN KEY (`player_id`) 
-- 		REFERENCES tbl_playerdata(PlayerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;
-- ALTER TABLE `adkats_banlist` ADD 
-- 	CONSTRAINT `adkats_banlist_fk_latest_record_id` 
-- 		FOREIGN KEY (`latest_record_id`) 
-- 		REFERENCES adkats_records(record_id) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

CREATE TABLE IF NOT EXISTS `adkats_settings` ( 
	`server_id` SMALLINT(5) UNSIGNED NOT NULL, 
	`setting_name` VARCHAR(200) NOT NULL DEFAULT "SettingName", 
	`setting_type` VARCHAR(45) NOT NULL DEFAULT "SettingType", 
	`setting_value` VARCHAR(1500) NOT NULL DEFAULT "SettingValue", 
	PRIMARY KEY (`server_id`, `setting_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AdKats Setting Sync';
-- ALTER TABLE `adkats_settings` ADD 
-- 	CONSTRAINT `adkats_settings_fk_server_id` 
-- 		FOREIGN KEY (`server_id`) 
-- 		REFERENCES tbl_server(ServerID) 
-- 		ON DELETE CASCADE 
-- 		ON UPDATE NO ACTION;

DROP TRIGGER IF EXISTS adkats_update_point_insert;
DROP TRIGGER IF EXISTS adkats_update_point_delete;

DELIMITER |

CREATE TRIGGER adkats_update_point_insert BEFORE INSERT ON `adkats_records`
	FOR EACH ROW 
	BEGIN 
		DECLARE command_type VARCHAR(45);
		DECLARE server_id INT(11);
		DECLARE player_id INT(11);
		SET command_type = NEW.command_type;
		SET server_id = NEW.server_id;
		SET player_id = NEW.target_id;

		IF(command_type = 'Punish') THEN
			INSERT INTO `adkats_serverPlayerPoints` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 1, 0, 1) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` + 1, 
				`total_points` = `total_points` + 1;
			INSERT INTO `adkats_globalPlayerPoints` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 1, 0, 1) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` + 1, 
				`total_points` = `total_points` + 1;
		ELSEIF (command_type = 'Forgive') THEN
			INSERT INTO `adkats_serverPlayerPoints` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 1, -1) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` + 1, 
				`total_points` = `total_points` - 1;
			INSERT INTO `adkats_globalPlayerPoints` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 1, -1) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` + 1, 
				`total_points` = `total_points` - 1;
		END IF;
	END;

|

CREATE TRIGGER adkats_update_point_delete AFTER DELETE ON `adkats_records`
	FOR EACH ROW 
	BEGIN 
		DECLARE command_type VARCHAR(45);
		DECLARE server_id INT(11);
		DECLARE player_id INT(11);
		SET command_type = OLD.command_type;
		SET server_id = OLD.server_id;
		SET player_id = OLD.target_id;

		IF(command_type = 'Punish') THEN
			INSERT INTO `adkats_serverPlayerPoints` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` - 1, 
				`total_points` = `total_points` - 1;
			INSERT INTO `adkats_globalPlayerPoints` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`punish_points` = `punish_points` - 1, 
				`total_points` = `total_points` - 1;
		ELSEIF (command_type = 'Forgive') THEN
			INSERT INTO `adkats_serverPlayerPoints` 
				(`player_id`, `server_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, server_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` - 1, 
				`total_points` = `total_points` + 1;
			INSERT INTO `adkats_globalPlayerPoints` 
				(`player_id`, `punish_points`, `forgive_points`, `total_points`) 
			VALUES 
				(player_id, 0, 0, 0) 
			ON DUPLICATE KEY UPDATE 
				`forgive_points` = `forgive_points` - 1, 
				`total_points` = `total_points` + 1;
		END IF;
	END;

|

DELIMITER ;

CREATE OR REPLACE VIEW `adkats_totalcmdissued` AS
SELECT
  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Move' OR adkats_records.command_type = 'ForceMove') AS 'Moves',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Teamswap') AS 'TeamSwaps',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Kill') AS 'Kills',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Kick') AS 'Kicks',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'TempBan' OR adkats_records.command_action = 'TempBan') AS 'TempBans',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'PermaBan' OR adkats_records.command_action = 'PermaBan') AS 'PermaBans',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Punish') AS 'Punishes',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Forgive') AS 'Forgives',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Report' OR adkats_records.command_type = 'CallAdmin') AS 'Reports',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'ConfirmReport') AS 'UsedReports',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'AdminSay') AS 'AdminSays',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'PlayerSay') AS 'PlayerSays',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'AdminYell') AS 'AdminYells',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'PlayerYell') AS 'PlayerYells',

  (SELECT COUNT(*)
   FROM adkats_records
   WHERE adkats_records.command_type = 'Mute') AS 'PlayerMutes',

  (SELECT COUNT(*)
   FROM adkats_records) AS 'TotalCommands';
