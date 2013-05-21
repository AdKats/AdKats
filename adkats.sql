--Was dropping tables, removed to remove risk of data loss if this gets run at the wrong time.
--DROP TABLE IF EXISTS `adkat_records`;
--DROP TABLE IF EXISTS `adkat_actionlist`;
--DROP TABLE IF EXISTS `adkat_teamswapwhitelist`;

CREATE TABLE `adkat_records` (
       `record_id` int(11) NOT NULL AUTO_INCREMENT, 
       `server_id` int(11) NOT NULL, 
       `command_type` varchar(45) NOT NULL, 
       `record_durationMinutes` int(11) NOT NULL, 
       `target_guid` varchar(100) NOT NULL, 
       `target_name` varchar(45) NOT NULL, 
       `source_name` varchar(45) NOT NULL, 
       `record_message` varchar(100) NOT NULL, 
       `record_time` datetime NOT NULL, 
       PRIMARY KEY (`record_id`));

CREATE TABLE `adkat_actionlist` (
       `action_id` int(11) NOT NULL AUTO_INCREMENT, 
       `record_id` int(11) NOT NULL,
       `plugin_read` ENUM('Y', 'N') NOT NULL, 
       PRIMARY KEY (`action_id`));

CREATE TABLE `adkat_teamswapwhitelist` (
       `player_name` varchar(45) NOT NULL, 
       `player_guid` varchar(100) NOT NULL DEFAULT 'WAITING ON USE FOR GUID', 
       PRIMARY KEY (`player_name`), 
       UNIQUE KEY `player_name_UNIQUE` (`player_name`));

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

CREATE OR REPLACE VIEW `adkat_weeklyplayerpoints` AS SELECT `adkat_playerlist`.`player_name` AS `playername`,
       `adkat_playerlist`.`player_guid` AS `playerguid`,
       `adkat_playerlist`.`server_id` AS `serverid`,
  (SELECT count(`adkat_records`.`target_guid`)
   FROM `adkat_records`
   WHERE (         (`adkat_records`.`command_type` = 'Punish')
          AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
          AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)
          AND (`adkat_records`.`record_time` between date_sub(now(),INTERVAL 7 DAY) and now()))
  ) AS `punishpoints`,
  (SELECT count(`adkat_records`.`target_guid`)
   FROM `adkat_records`
   WHERE (    (`adkat_records`.`command_type` = 'Forgive')
          AND (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
          AND (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)
          AND (`adkat_records`.`record_time` between date_sub(now(),INTERVAL 7 DAY) and now()))
   ) AS `forgivepoints`,
   (
         (SELECT count(`adkat_records`.`target_guid`)
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
   WHERE adkat_records.command_type = 'Move'
     OR adkat_records.command_type = 'ForceMove') AS 'Moves',
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
   WHERE adkat_records.command_type = 'TempBan') AS 'TempBans',
  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'PermaBan') AS 'PermaBans',
  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Punish') AS 'Punishes',
  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Forgive') AS 'Forgives',
  (SELECT COUNT(*)
   FROM adkat_records
   WHERE adkat_records.command_type = 'Report'
     OR adkat_records.command_type = 'CallAdmin') AS 'Reports',
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
   FROM adkat_records) AS 'TotalCommands';
