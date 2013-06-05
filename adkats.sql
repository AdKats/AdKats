-- AdKats Database Setup Script by ColColonCleaner

-- This is run automatically if AdKats senses the database is not set up properly.
-- If you don't want the plugin changing tables/views in your database, you must run this beforehand.

-- If the tables needed are not in the database yet, the below two queries will be success
CREATE TABLE IF NOT EXISTS `adkat_records` ( 
`record_id` int(11) NOT NULL AUTO_INCREMENT, 
`server_id` int(11) NOT NULL DEFAULT -1, 
`server_ip` varchar(45) NOT NULL DEFAULT "0.0.0.0:0000", 
`command_type` varchar(45) NOT NULL DEFAULT "DefaultCommand", 
`command_action` varchar(45) NOT NULL DEFAULT "DefaultAction", 
`record_durationMinutes` int(11) NOT NULL DEFAULT 0, 
`target_guid` varchar(100) NOT NULL DEFAULT "EA_NoGUID", 
`target_name` varchar(45) NOT NULL DEFAULT "NoTarget", 
`source_name` varchar(45) NOT NULL DEFAULT "NoNameAdmin", 
`record_message` varchar(100) NOT NULL DEFAULT "NoMessage", 
`record_time` TIMESTAMP DEFAULT CURRENT_TIMESTAMP, 
`adkats_read` ENUM('Y', 'N') NOT NULL DEFAULT 'N', 
PRIMARY KEY (`record_id`)
);
CREATE TABLE IF NOT EXISTS `adkat_accesslist` ( 
`player_name` varchar(45) NOT NULL DEFAULT "NoPlayer", 
`player_guid` varchar(100) NOT NULL DEFAULT 'WAITING ON USE FOR GUID', 
`access_level` int(11) NOT NULL DEFAULT 6, 
PRIMARY KEY (`player_name`), UNIQUE KEY `player_name_UNIQUE` (`player_name`));

CREATE OR REPLACE VIEW `adkat_playerlist` AS
SELECT `adkat_records`.`target_name` AS `player_name`,
       `adkat_records`.`target_guid` AS `player_guid`,
       `adkat_records`.`server_id` AS `server_id`
FROM `adkat_records`
GROUP BY `adkat_records`.`target_guid`,
         `adkat_records`.`server_id`
ORDER BY `adkat_records`.`target_name`;


CREATE OR REPLACE VIEW `adkat_playerpoints` AS
SELECT `adkat_playerlist`.`player_name` AS `playername`,
       `adkat_playerlist`.`player_guid` AS `playerguid`,
       `adkat_playerlist`.`server_id` AS `serverid`,

  (SELECT count(`adkat_records`.`target_guid`)
   FROM `adkat_records`
   WHERE ((`adkat_records`.`command_type` = 'Punish')
          AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
          AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) AS `punishpoints`,

  (SELECT count(`adkat_records`.`target_guid`)
   FROM `adkat_records`
   WHERE ((`adkat_records`.`command_type` = 'Forgive')
          AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
          AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) AS `forgivepoints`,
       (
          (SELECT count(`adkat_records`.`target_guid`)
           FROM `adkat_records`
           WHERE ((`adkat_records`.`command_type` = 'Punish')
                  AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
                  AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) -
          (SELECT count(`adkat_records`.`target_guid`)
           FROM `adkat_records`
           WHERE ((`adkat_records`.`command_type` = 'Forgive')
                  AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
                  AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)))) AS `totalpoints`
FROM `adkat_playerlist`;


CREATE OR REPLACE VIEW `adkat_weeklyplayerpoints` AS
SELECT `adkat_playerlist`.`player_name` AS `playername`, `adkat_playerlist`.`player_guid` AS `playerguid`, `adkat_playerlist`.`server_id` AS `serverid`,
  (SELECT count(`adkat_records`.`target_guid`)
   FROM `adkat_records`
   WHERE ((`adkat_records`.`command_type` = 'Punish')
          AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
          AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)
          AND (`adkat_records`.`record_time` BETWEEN date_sub(now(),INTERVAL 7 DAY) AND now())) ) AS `punishpoints`,
  (SELECT count(`adkat_records`.`target_guid`)
   FROM `adkat_records`
   WHERE ((`adkat_records`.`command_type` = 'Forgive')
          AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
          AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)
          AND (`adkat_records`.`record_time` BETWEEN date_sub(now(),INTERVAL 7 DAY) AND now())) ) AS `forgivepoints`, ( (
SELECT count(`adkat_records`.`target_guid`)
          FROM `adkat_records`
          WHERE (    (`adkat_records`.`command_type` = 'Punish')
                       AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
                       AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)
              AND (`adkat_records`.`record_time` between date_sub(now(),INTERVAL 7 DAY) and now()))) -
         (SELECT count(`adkat_records`.`target_guid`)
          FROM `adkat_records`
          WHERE (    (`adkat_records`.`command_type` = 'Forgive')
                       AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
       		  AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)
              AND (`adkat_records`.`record_time` between date_sub(now(),INTERVAL 7 DAY) and now())))
       ) AS `totalpoints`
FROM `adkat_playerlist`;

CREATE OR REPLACE VIEW `adkat_reports` AS
SELECT `adkat_records`.`record_id` AS `record_id`,
       `adkat_records`.`server_id` AS `server_id`,
       `adkat_records`.`command_type` AS `command_type`,
       `adkat_records`.`record_durationMinutes` AS `record_durationMinutes`,
       `adkat_records`.`target_guid` AS `target_guid`,
       `adkat_records`.`target_name` AS `target_name`,
       `adkat_records`.`source_name` AS `source_name`,
       `adkat_records`.`record_message` AS `record_message`,
       `adkat_records`.`record_time` AS `record_time`
FROM `adkat_records`
WHERE ((`adkat_records`.`command_type` = 'Report')
       OR (`adkat_records`.`command_type` = 'CallAdmin'));

CREATE OR REPLACE VIEW `adkat_naughtylist` AS
SELECT `adkat_playerpoints`.`serverid` AS `server_id`,
       `adkat_playerpoints`.`playername` AS `player_name`,
       `adkat_playerpoints`.`totalpoints` AS `total_points`
FROM `adkat_playerpoints`
WHERE (`adkat_playerpoints`.`totalpoints` > 0)
ORDER BY  `adkat_playerpoints`.`totalpoints` DESC,
          `adkat_playerpoints`.`serverid`,
          `adkat_playerpoints`.`playername`;

CREATE OR REPLACE VIEW `adkat_weeklynaughtylist` AS
SELECT `adkat_weeklyplayerpoints`.`serverid` AS `server_id`,
       `adkat_weeklyplayerpoints`.`playername` AS `player_name`,
       `adkat_weeklyplayerpoints`.`totalpoints` AS `total_points`
FROM `adkat_weeklyplayerpoints`
WHERE (`adkat_weeklyplayerpoints`.`totalpoints` > 0)
ORDER BY `adkat_weeklyplayerpoints`.`totalpoints` DESC,
         `adkat_weeklyplayerpoints`.`serverid`,
         `adkat_weeklyplayerpoints`.`playername`;

CREATE OR REPLACE VIEW `adkat_totalcmdissued` AS
SELECT
  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Move' OR adkat_records.command_type = 'ForceMove') AS 'Moves',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Teamswap') AS 'TeamSwaps',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Kill') AS 'Kills',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Kick') AS 'Kicks',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'TempBan' OR adkat_records.command_action = 'TempBan') AS 'TempBans',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'PermaBan' OR adkat_records.command_action = 'PermaBan') AS 'PermaBans',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Punish') AS 'Punishes',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Forgive') AS 'Forgives',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Report' OR adkat_records.command_type = 'CallAdmin') AS 'Reports',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'ConfirmReport') AS 'UsedReports',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'AdminSay') AS 'AdminSays',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'PlayerSay') AS 'PlayerSays',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'AdminYell') AS 'AdminYells',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'PlayerYell') AS 'PlayerYells',

  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Mute') AS 'PlayerMutes',

  (SELECT COUNT(*)
   FROM adkat_records) AS 'TotalCommands';

-- Run these last, if they fail it means it was a new DB, if it succeeds their previous data was saved.
ALTER TABLE `adkat_records` MODIFY `record_time` TIMESTAMP DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `adkat_records` MODIFY `server_id` int(11) NOT NULL DEFAULT -1;
ALTER TABLE `adkat_records` MODIFY `command_type` varchar(45) NOT NULL DEFAULT "DefaultCommand"; 
ALTER TABLE `adkat_records` MODIFY `record_durationMinutes` int(11) NOT NULL DEFAULT 0; 
ALTER TABLE `adkat_records` MODIFY `target_guid` varchar(100) NOT NULL DEFAULT "EA_NoGUID"; 
ALTER TABLE `adkat_records` MODIFY `target_name` varchar(45) NOT NULL DEFAULT "NoTarget"; 
ALTER TABLE `adkat_records` MODIFY `source_name` varchar(45) NOT NULL DEFAULT "NoNameAdmin"; 
ALTER TABLE `adkat_records` MODIFY `record_message` varchar(100) NOT NULL DEFAULT "NoMessage"; 
ALTER TABLE `adkat_records` MODIFY `adkats_read` ENUM('Y', 'N') NOT NULL DEFAULT 'N';

DELIMITER $$

DROP PROCEDURE IF EXISTS upgrade_database_2_0_to_2_5 $$
CREATE PROCEDURE upgrade_database_2_0_to_2_5()
BEGIN
-- add server_ip and command_action columns safely
IF NOT EXISTS( (SELECT * FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
	AND COLUMN_NAME='server_ip' AND TABLE_NAME='adkat_records') ) THEN
		ALTER TABLE `adkat_records` ADD `server_ip` varchar(45) NOT NULL DEFAULT "0.0.0.0:0000"; 
END IF; 
-- add server_ip and command_action columns safely
IF NOT EXISTS( (SELECT * FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
	AND COLUMN_NAME='command_action' AND TABLE_NAME='adkat_records') ) THEN 
		ALTER TABLE `adkat_records` ADD `command_action` varchar(45) NOT NULL DEFAULT "DefaultAction"; 
END IF; 

END $$

CALL upgrade_database_2_0_to_2_5() $$

DELIMITER ;

DROP TABLE IF EXISTS `adkat_teamswapwhitelist`;
