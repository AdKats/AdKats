/* 
 * xADKAdminTools.cs
 */

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.Reflection;

using MySql.Data.MySqlClient;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;


namespace PRoConEvents
{
    //Aliases
    using EventType = PRoCon.Core.Events.EventType;
    using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

    public class xADKAdminTools : PRoConPluginAPI, IPRoConPluginInterface
    {
        #region Variables

        // Enumerations
        public enum MessageTypeEnum { Warning, Error, Exception, Normal };

        // General settings
        private bool isEnabled;
        private int debugLevel;
        private int USTeamId = 1;
        private int RussianTeam = 2;
        private Boolean updating = false;
        private CServerInfo serverInfo = null;

        // Player Lists
        Dictionary<string, CPlayerInfo> currentPlayers = new Dictionary<string, CPlayerInfo>();
        List<CPlayerInfo> playerList = new List<CPlayerInfo>();
        //The list of players on RU wishing to move to US (This list takes first priority)
        private Queue<CPlayerInfo> USMoveList = new Queue<CPlayerInfo>();
        //the list of players on US wishing to move to RU (This list takes secondary)
        private Queue<CPlayerInfo> RUMoveList = new Queue<CPlayerInfo>();
        //player counts per team
        private int USPlayerCount = 0;
        private int RUPlayerCount = 0;

        // Admin List
        private Boolean useDatabaseAdminList = true;
        private List<string> databaseAdminCache = new List<string>();
        private List<string> staticAdminCache = new List<string>();

        // MySQL Settings
        public MySqlConnection databaseConnection = null;
        public string mySqlHostname;
        public string mySqlPort;
        public string mySqlDatabase;
        public string mySqlUsername;
        public string mySqlPassword;

        // Battlefield Server ID
        private int serverID = -1;

        //Static Enum of Possible Admin Commands
        public enum CAE_CommandType { Default, Yes, No, MovePlayer, ForceMovePlayer, MoveSelf, KillPlayer, KickPlayer, TempBanPlayer, BanPlayer, PunishPlayer, ForgivePlayer, ReportPlayer, CallAdmin };
        //Default command names
        public Dictionary<string, CAE_CommandType> CAE_CommandStrings = new Dictionary<string, CAE_CommandType>();
        //Create the inverse dictionary
        public Dictionary<CAE_CommandType, string> CAE_CommandStringsInverse = new Dictionary<CAE_CommandType, string>();
        //Database record types
        public Dictionary<CAE_CommandType, string> CAE_RecordTypes = new Dictionary<CAE_CommandType, string>();

        //When a player partially completes a name, this dictionary holds those actions until player confirms action
        private Dictionary<string, CAE_Record> actionAttemptList = new Dictionary<string, CAE_Record>();
        //Whether to act on punishments within the plugin
        private Boolean actOnPunishments = false;
        //Default hierarchy of punishments
        private string[] punishmentHierarchy = 
        {
            "kill",
            "kill",
            "kick",
            "kick",
            "tban60",
            "tban60",
            "tbanweek",
            "tbanweek",
            "ban"
        };
        //When punishing, only kill players when server is in low population
        private Boolean onlyKillOnLowPop = false;
        //Default for low populations
        private int lowPopPlayerCount = 24;
        //Default required reason length
        private int requiredReasonLength = 5;

        //TeamSwap Settings
        //whether to allow all players, or just players in the whitelist
        private Boolean requireWhitelist = false;
        //Static whitelist for plugin only use
        private List<string> staticWhitelistCache = new List<string>();
        //Whether to use the database whitelist for teamswap
        private Boolean useDatabaseWhitelist = true;
        //Database whitelist cache
        private List<string> databaseWhitelistCache = new List<string>();
        //the lowest ticket count of either team
        private int lowestTicketCount = 500000;
        //the highest ticket count of either team
        private int highestTicketCount = 0;
        //the highest ticket count of either team to allow self move
        private int teamSwapTicketWindowHigh = 500000;
        //the lowest ticket count of either team to allow self move
        private int teamSwapTicketWindowLow = 0;
        //denied access text
        private string deniedAccessText = "TeamSwap Access Denied. Please purchase access on ADKGamers.com";

        #endregion

        public xADKAdminTools()
        {
            //Populate Command Strings
            this.CAE_CommandStrings.Add("yes", CAE_CommandType.Yes);
            this.CAE_CommandStrings.Add("no", CAE_CommandType.No);
            this.CAE_CommandStrings.Add("move", CAE_CommandType.MovePlayer);
            this.CAE_CommandStrings.Add("fmove", CAE_CommandType.ForceMovePlayer);
            this.CAE_CommandStrings.Add("moveme", CAE_CommandType.MoveSelf);
            this.CAE_CommandStrings.Add("kill", CAE_CommandType.KillPlayer);
            this.CAE_CommandStrings.Add("kick", CAE_CommandType.KickPlayer);
            this.CAE_CommandStrings.Add("tban", CAE_CommandType.TempBanPlayer);
            this.CAE_CommandStrings.Add("ban", CAE_CommandType.BanPlayer);
            this.CAE_CommandStrings.Add("punish", CAE_CommandType.PunishPlayer);
            this.CAE_CommandStrings.Add("forgive", CAE_CommandType.ForgivePlayer);
            this.CAE_CommandStrings.Add("report", CAE_CommandType.ReportPlayer);
            this.CAE_CommandStrings.Add("admin", CAE_CommandType.CallAdmin);
            //Populate Inverse Command Strings
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.Yes, "yes");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.No, "no");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.MovePlayer, "move");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.ForceMovePlayer, "fmove");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.MoveSelf, "moveme");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.KillPlayer, "kill");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.KickPlayer, "kick");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.TempBanPlayer, "tban");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.BanPlayer, "ban");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.PunishPlayer, "punish");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.ForgivePlayer, "forgive");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.ReportPlayer, "report");
            this.CAE_CommandStringsInverse.Add(CAE_CommandType.CallAdmin, "admin");
            //Fill DB record types
            this.CAE_RecordTypes.Add(CAE_CommandType.MovePlayer, "Move");
            this.CAE_RecordTypes.Add(CAE_CommandType.ForceMovePlayer, "ForceMove");
            this.CAE_RecordTypes.Add(CAE_CommandType.MoveSelf, "Teamswap");
            this.CAE_RecordTypes.Add(CAE_CommandType.KillPlayer, "Kill");
            this.CAE_RecordTypes.Add(CAE_CommandType.KickPlayer, "Kick");
            this.CAE_RecordTypes.Add(CAE_CommandType.TempBanPlayer, "TempBan");
            this.CAE_RecordTypes.Add(CAE_CommandType.BanPlayer, "Ban");
            this.CAE_RecordTypes.Add(CAE_CommandType.PunishPlayer, "Punish");
            this.CAE_RecordTypes.Add(CAE_CommandType.ForgivePlayer, "Forgive");
            this.CAE_RecordTypes.Add(CAE_CommandType.ReportPlayer, "Report");
            this.CAE_RecordTypes.Add(CAE_CommandType.CallAdmin, "CallAdmin");
            isEnabled = false;
            debugLevel = 2;
        }

        #region Plugin details

        public string GetPluginName()
        {
            return "ADK Admin Tools";
        }

        public string GetPluginVersion()
        {
            return "0.1.0";
        }

        public string GetPluginAuthor()
        {
            return "ColColonCleaner";
        }

        public string GetPluginWebsite()
        {
            return "http://www.adkgamers.com/";
        }

        public string GetPluginDescription()
        {
            return @"
            <h1>xADKAdminTools</h1>
            <h2>Description</h2>
            <h3>Main</h3>
            <p>
                This plugin should be used by groups who have high traffic servers, with set rules, and many admins. Players who break rules over time usually don't end up doing it in front of the same admin twice, so minimal, or incorrect action is taken. This fixes that issue, along with keeping all functionality of other admin tools.<br/><br/>

                All actions are logged in a database on a single connection. This allows the punish command to take appropriate action on every player, every time.<br/><br/>

                Logs can be made server specific, meaning rule breaking on one server won't cause increase in punishments on another server for that same player. This is available since many groups run different rule sets on each server.<br/><br/>

                If you have a procon-external system, such as a web-based tool for server control, then use the ActionList table by turning 'act on punishments' off. This will simply send player Id's who need punishment to that table for your external system to act on instead.<br/><br/>

                Players may also be forgiven, which will reduce their total point value by 1.<br/><br/>

                All punishments are required to have a reason, and will cancel if no reason is given.<br/><br/>

                This plugin also implements teamswap. Teamswap allows whitelisted players the ability to move themselves between teams as often as they want. This is currently not an available option in default battlefield aside from procon commands, as the game itself limits players to one switch per gaming session. Players simply type '@moveme' and the plugin will slay them, then move them to the other team. Meant to be available to people outside the admin list, usually by paid usage.<br/><br/>
            </p>
            <h2>Settings</h2>
            <p>
                Debugging Settings:<br/><br/>
                * 'Debug level' - Indicates how much debug-output is printed to the plugin-console. 0 turns off debug messages (just shows important warnings/exceptions), 6 documents nearly every step.</br></br>
                Admin Settings:<br/><br/>
                * 'Use Database Admin List' - Whether to use list of admins from 'adminlist' table to cached admin list on plugin start. Plugin must be restarted to update admin list from database.<br/></br>
                * 'Static Admin List' - List of admins input from plugin settings. Use if no admin database table. <br/></br>
                MySQL Settings:<br/><br/>
                * 'MySQL Hostname' - Hostname of the MySQL-Server Cross-Admin Enforcer should connect to. <br/></br>
                * 'MySQL Port' - Port of the MySQL-Server Cross-Admin Enforcer should connect to. <br/></br>
                * 'MySQL Database' - Database Cross-Admin Enforcer should use for storage. Hardcoded table names and creation scripts given below. <br/></br>
                * 'MySQL Username' - Username of the MySQL-Server Cross-Admin Enforcer should connect to. <br/></br>
                * 'MySQL Password' - Password of the MySQL-Server Cross-Admin Enforcer should connect to. <br/></br>
                Punishment Settings:<br/><br/>
                * 'Act on Punishments' - Whether the plugin should carry out punishments, or have an external source do it through cae_actionlist.<br/></br>
                * 'Punishment Hierarchy' - List of punishments in order from lightest to most severe. Index in list is the action taken at that number of points.<br/></br>
                * 'Minimum Reason Length' - The minimum number of characters a reason must be to call punish or forgive on a player.<br/></br>
                * 'Only Kill Players when Server in low population' - When server population is below 'Low Server Pop Value', only kill players, so server does not empty. Player points will still be incremented normally.<br/></br>
                * 'Low Server Pop Value' - Number of players at which the server is deemed 'Low Population'.<br/></br>
                Server Settings:<br/><br/>
                * 'Server ID' - ID that will be used to identify this server in the database.<br/></br>
                Teamswap Settings:<br/><br/>
                * 'Require Whitelist for Access' - Whether the 'moveme' command will require whitelisting. Admins are always allowed to use it.<br/></br>
                * 'Static Player Whitelist' - Static list of players plugin-side that will be able to teamswap.<br/></br>
                * 'Use Database Whitelist' - Whether to use 'cae_teamswapwhitelist' table in the database for player whitelisting.<br/></br>
                * 'Ticket Window High' - When either team is above this ticket count, then nobody (except admins) will be able to teamswap.<br/></br>
                * 'Ticket Window Low' - When either team is below this ticket count, then nobody (except admins) will be able to teamswap.<br/></br>
            </p>
            <h2>Default Punishment Levels by Point</h2>
            <p>
                Action decided after player is punished, and their points incremented.</br>

                </br>
                * 1 point  - Player Killed. </br>
                * 2 points - Player Killed. </br>
                * 3 points - Player Kicked. </br>
                * 4 points - Player Kicked. </br>
                * 5 points - Player Temp Banned for 60 minutes. </br>
                * 6 points - Player Temp Banned for 60 minutes. </br>
                * 7 points - Player Temp Banned for 1 week. </br>
                * 8 points - Player Temp Banned for 1 week. </br>
                * 9 points - Player Perma-Banned. </br>
            </p>
            <h2>Current In-Game player Commands</h2>
            <p>
                * '@move playername' : This command not implemented yet, use force move.<br/>
                * '@fmove playername' : This command will log a force move for the given player, then add them to the teamswap queue.<br/>
                * '@moveme' : This command will log a teamswap for the sayer (if they have access), then add them to the teamswap queue.<br/>
                * '@kill playername reason' : This command will log a kill for the given player, then kill them.<br/>
                * '@kick playername reason' : This command will log a kick for the given player, then kick them.<br/>
                * '@tban minutes playername reason' : This command will log a temp ban for the given player, then temp ban them for the given number of minutes.<br/>
                * '@ban playername reason' : This command will log a perma-ban for the given player, then perma-ban them.<br/>
                * '@punish playername reason' : This command will log a punish for the given player, increasing their total points by 1. Reason must be 5 chars or more.<br/>
                * '@forgive playername reason' : This command will log a forgive for the given player, decreasing their total points by 1. Reason must be 5 chars or more.<br/>
                * '@yes' : Confirm a partial player name selection.</br>
                * '@no' : Cancel partial player name selection.</br>
            </p>
            <h2>Development</h2>
            <p>
                Started by ColColonCleaner for ADK Gamers on Apr. 20, 2013</br>
            </p>
            <h2>Database Tables and Views</h2>
            <p>
                Main record table is the following:<br/>
                <br/>
                CREATE TABLE `cae_records` (<br/>
                  `record_id` int(11) NOT NULL AUTO_INCREMENT,<br/>
                  `server_id` int(11) NOT NULL,<br/>
                  `record_type` enum('Move','ForceMove','Teamswap','Kill','Kick','TempBan','Ban','Punish','Forgive','Report','CallAdmin') NOT NULL,<br/>
                  `record_durationMinutes` int(11) NOT NULL,<br/>
                  `target_guid` varchar(100) NOT NULL,<br/>
                  `target_name` varchar(45) NOT NULL,<br/>
                  `source_name` varchar(45) NOT NULL,<br/>
                  `record_reason` varchar(100) NOT NULL,<br/>
                  `record_time` datetime NOT NULL,<br/>
                  PRIMARY KEY (`record_id`)<br/>
                );<br/>
                <br/>
                Action List table is the following:<br/>
                <br/>
                CREATE TABLE `cae_actionlist` (<br/>
                  `action_id` int(11) NOT NULL AUTO_INCREMENT,<br/>
                  `server_id` int(11) NOT NULL,<br/>
                  `player_guid` varchar(100) NOT NULL,<br/>
                  `player_name` varchar(45) NOT NULL,<br/>
                  PRIMARY KEY (`action_id`)<br/>
                );<br/>
                <br/>
                Admin table is the following:<br/>
                <br/>
                CREATE TABLE `adminlist` (<br/>
                  `name` varchar(255) NOT NULL,<br/>
                  `uniqueid` varchar(32) NOT NULL COMMENT 'GUID',<br/>
                  PRIMARY KEY (`uniqueid`)<br/>
                );<br/>
                <br/>
                Teamswap whitelist is the following:<br/>
                <br/>
                CREATE TABLE `cae_teamswapwhitelist` (<br/>
                  `player_name` varchar(45) NOT NULL DEFAULT 'NOTSET',<br/>
                  PRIMARY KEY (`player_name`),<br/>
                  UNIQUE KEY `player_name_UNIQUE` (`player_name`)<br/>
                );<br/>
                <br/>
                Server table is the following:<br/>
                <br/>
                CREATE TABLE `tbl_server` (<br/>
                  `ServerID` smallint(5) unsigned NOT NULL AUTO_INCREMENT,<br/>
                  `ServerGroup` tinyint(3) unsigned NOT NULL DEFAULT '0',<br/>
                  `IP_Address` varchar(45) DEFAULT NULL,<br/>
                  `ServerName` varchar(200) DEFAULT NULL,<br/>
                  `usedSlots` smallint(5) unsigned DEFAULT '0',<br/>
                  `maxSlots` smallint(5) unsigned DEFAULT '0',<br/>
                  `mapName` varchar(45) DEFAULT NULL,<br/>
                  `fullMapName` text,<br/>
                  `Gamemode` varchar(45) DEFAULT NULL,<br/>
                  `GameMod` varchar(45) DEFAULT NULL,<br/>
                  `PBversion` varchar(45) DEFAULT NULL,<br/>
                  `ConnectionState` varchar(45) DEFAULT NULL,<br/>
                  PRIMARY KEY (`ServerID`),<br/>
                  UNIQUE KEY `IP_Address_UNIQUE` (`IP_Address`),<br/>
                  KEY `INDEX_SERVERGROUP` (`ServerGroup`),<br/>
                  KEY `IP_Address` (`IP_Address`)<br/>
                );<br/>
                <br/>
                Player List View is the following:<br/>
                <br/>
                CREATE VIEW `cae_playerlist` AS select `cae_records`.`target_name` AS `player_name`,`cae_records`.`target_guid` AS `player_guid`,`cae_records`.`server_id` AS `server_id` from `cae_records` group by `cae_records`.`target_guid`,`cae_records`.`server_id` order by `cae_records`.`target_name`;<br/>
                <br/>
                Current Player Points View is the following:<br/>
                <br/>
                CREATE VIEW `cae_playerpoints` AS select `cae_playerlist`.`player_name` AS `playername`,`cae_playerlist`.`player_guid` AS `playerguid`,`cae_playerlist`.`server_id` AS `serverid`,(select count(`cae_records`.`target_guid`) from `cae_records` where ((`cae_records`.`record_type` = 'Punish') and (`cae_records`.`target_guid` = `cae_playerlist`.`player_guid`) and (`cae_records`.`server_id` = `cae_playerlist`.`server_id`))) AS `punishpoints`,(select count(`cae_records`.`target_guid`) from `cae_records` where ((`cae_records`.`record_type` = 'Forgive') and (`cae_records`.`target_guid` = `cae_playerlist`.`player_guid`) and (`cae_records`.`server_id` = `cae_playerlist`.`server_id`))) AS `forgivepoints`,((select count(`cae_records`.`target_guid`) from `cae_records` where ((`cae_records`.`record_type` = 'Punish') and (`cae_records`.`target_guid` = `cae_playerlist`.`player_guid`) and (`cae_records`.`server_id` = `cae_playerlist`.`server_id`))) - (select count(`cae_records`.`target_guid`) from `cae_records` where ((`cae_records`.`record_type` = 'Forgive') and (`cae_records`.`target_guid` = `cae_playerlist`.`player_guid`) and (`cae_records`.`server_id` = `cae_playerlist`.`server_id`)))) AS `totalpoints` from `cae_playerlist`;<br/>
                <br/>
                ALL THE ABOVE NEED TO BE RUN IN THIS ORDER. Once the views are done a constant tally of player points can be seen from your external systems. <br/>
                <br/>
                <br/>
                Additional View if you want it:<br/>
                <br/>
                CREATE VIEW `cae_naughtylist` AS select (select `tbl_server`.`ServerName` from `tbl_server` where (`tbl_server`.`ServerID` = `cae_playerpoints`.`serverid`)) AS `server_name`,`cae_playerpoints`.`playername` AS `player_name`,`cae_playerpoints`.`totalpoints` AS `total_points` from `cae_playerpoints` where (`cae_playerpoints`.`totalpoints` > 0) order by `cae_playerpoints`.`serverid`,`cae_playerpoints`.`playername`;<br/>
            </p>
            <h3>Changelog</h3>
            <blockquote>
                <h4>0.0.1 (20-APR-2013)</h4>
	                <b>Main: </b>          <br/>
                        Initial Version          <br/>
                <h4>0.0.2 (25-APR-2013)</h4>
	                <b>Changes</b>          <br/>
                        * Added plugin-side punishment. <br/>
                        * Initial DB test, tables updated. <br/>
                <h4>0.0.3 (28-APR-2013)</h4>
	                <b>Changes</b>          <br/>
                        * In-game commands no longer case sensitive. <br/>
                        * External DB test, tables updated. <br/>
                        * First in-game run during match, minor bugs fixed. <br/>
                <h4>0.0.4 (29-APR-2013)</h4>
	                <b>Changes</b>          <br/>
                        * Added editable in-game commands for forgive/punish. <br/>
                <h4>0.0.5 (30-APR-2013)</h4>
	                <b>Changes</b>          <br/>
                        * Removed global player access for production version. <br/>
                        * Added admin list for access. <br/>
                        * Added 'Low Server Pop' override system
                <h4>0.0.6 (1-MAY-2013)</h4>
	                <b>Changes</b>          <br/>
                        * Added access to database admin list. <br/>
                        * Fixed minor bugs during testing. <br/>
                <h4>0.0.7 (1-MAY-2013)</h4>
	                <b>Changes</b>          <br/>
                        * Added view definitions to display current player point values. <br/>
                <h4>0.0.8 (2-MAY-2013)</h4>
	                <b>Changes</b>          <br/>
                        * Updated player-say messages when punishment acted on. More informative message used.<br/>
                        * Added editable minimum reason length.<br/>
                <h4>0.0.9 (3-MAY-2013)</h4>
	                <b>Changes</b>          <br/>
                        * Added direct kill, kick, tban, and ban commands.<br/>
                        * Made direct commands work with database.<br/>
                        * Removed editable command list.<br/>
                <h4>0.1.0 (5-MAY-2013)</h4>
	                <b>Changes</b>          <br/>
                        * Refactor record creation to increase speed.<br/>
                        * Enumerate all database commands to parse in-game commands.<br/>
                        * Implement xTeamSwap functions within this plugin.<br/>
                        * Create database whitelist definitions for teamswap.<br/>
                        * Add move, fmove, moveme as commands that use teamswap.<br/>
                        * Refactor database logging to work with all commands.<br/>
             </blockquote>
            ";
        }

        #endregion

        #region Plugin settings

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            string temp = "";

            //Debug settings
            lstReturn.Add(new CPluginVariable("Debugging|Debug level", typeof(int), this.debugLevel));

            //Server Settings
            lstReturn.Add(new CPluginVariable("Server Settings|Server ID", typeof(int), this.serverID));

            //SQL Settings
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Hostname", typeof(string), mySqlHostname));
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Port", typeof(string), mySqlPort));
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Database", typeof(string), mySqlDatabase));
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Username", typeof(string), mySqlUsername));
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Password", typeof(string), mySqlPassword));

            //Punishment Settings
            lstReturn.Add(new CPluginVariable("Punishment Settings|Act on Punishments", typeof(Boolean), this.actOnPunishments));
            lstReturn.Add(new CPluginVariable("Punishment Settings|Punishment Hierarchy", typeof(string[]), this.punishmentHierarchy));
            lstReturn.Add(new CPluginVariable("Punishment Settings|Minimum Required Reason Length", typeof(int), this.requiredReasonLength));
            lstReturn.Add(new CPluginVariable("Punishment Settings|Only Kill Players when Server in low population", typeof(Boolean), this.onlyKillOnLowPop));
            lstReturn.Add(new CPluginVariable("Punishment Settings|Low Population Value", typeof(int), this.lowPopPlayerCount));

            //Admin Settings
            lstReturn.Add(new CPluginVariable("Admin Settings|Use Database Admin List", typeof(Boolean), this.useDatabaseAdminList));
            lstReturn.Add(new CPluginVariable("Admin Settings|Static Admin List", typeof(string[]), this.staticAdminCache.ToArray()));

            //TeamSwap Settings
            lstReturn.Add(new CPluginVariable("TeamSwap Settings|Require Whitelist for Access", typeof(Boolean), this.requireWhitelist));
            lstReturn.Add(new CPluginVariable("TeamSwap Settings|Static Player Whitelist", typeof(string[]), this.staticWhitelistCache.ToArray()));
            lstReturn.Add(new CPluginVariable("TeamSwap Settings|Use Database Whitelist", typeof(Boolean), this.useDatabaseWhitelist));
            lstReturn.Add(new CPluginVariable("TeamSwap Settings|Ticket Window High", typeof(int), this.teamSwapTicketWindowHigh));
            lstReturn.Add(new CPluginVariable("TeamSwap Settings|Ticket Window Low", typeof(int), this.teamSwapTicketWindowLow));
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            if (Regex.Match(strVariable, @"Debug level").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                debugLevel = tmp;
            }
            else if (Regex.Match(strVariable, @"Server ID").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.serverID = tmp;
            }
            else if (Regex.Match(strVariable, @"Act on Punishments").Success)
            {
                this.actOnPunishments = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Punishment Hierarchy").Success)
            {
                this.punishmentHierarchy = CPluginVariable.DecodeStringArray(strValue);
            }
            else if (Regex.Match(strVariable, @"Only Kill Players when Server in low population").Success)
            {
                this.onlyKillOnLowPop = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Low Population Value").Success)
            {
                this.lowPopPlayerCount = Int32.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Minimum Required Reason Length").Success)
            {
                this.requiredReasonLength = Int32.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Use Database Admin List").Success)
            {
                this.useDatabaseAdminList = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Static Admin List").Success)
            {
                this.staticAdminCache = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (Regex.Match(strVariable, @"MySQL Hostname").Success)
            {
                mySqlHostname = strValue;
            }
            else if (Regex.Match(strVariable, @"MySQL Port").Success)
            {
                int tmp = 3306;
                int.TryParse(strValue, out tmp);
                if (tmp > 0 && tmp < 65536)
                {
                    mySqlPort = strValue;
                }
                else
                {
                    ConsoleException("Invalid value for MySQL Port: '" + strValue + "'. Must be number between 1 and 65535!");
                }
            }
            else if (Regex.Match(strVariable, @"MySQL Database").Success)
            {
                mySqlDatabase = strValue;
            }
            else if (Regex.Match(strVariable, @"MySQL Username").Success)
            {
                mySqlUsername = strValue;
            }
            else if (Regex.Match(strVariable, @"MySQL Password").Success)
            {
                mySqlPassword = strValue;
            }
            else if (Regex.Match(strVariable, @"Server ID").Success)
            {
                serverID = Int32.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Require Whitelist for Access").Success)
            {
                this.requireWhitelist = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Static Player Whitelist").Success)
            {
                this.staticWhitelistCache = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (Regex.Match(strVariable, @"Use Database Whitelist").Success)
            {
                this.useDatabaseWhitelist = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Ticket Window High").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.teamSwapTicketWindowHigh = tmp;
            }
            else if (Regex.Match(strVariable, @"Ticket Window Low").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.teamSwapTicketWindowLow = tmp;
            }
            
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name, "OnVersion", "OnServerInfo", "OnResponseError", "OnListPlayers", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerKilled", "OnPlayerSpawned", "OnPlayerTeamChange", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOverPlayers", "OnRoundOver", "OnRoundOverTeamScores", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded");
            //Get admin list if needed
            if ((this.mySqlDatabase != null) && (this.mySqlHostname != null) && (this.mySqlPassword != null) && (this.mySqlPort != null) && (this.mySqlUsername != null))
            {
                if (this.useDatabaseAdminList)
                {
                    this.fetchAdminList();
                }
                if (this.useDatabaseWhitelist)
                {
                    this.fetchTeamswapWhitelist();
                }
            }
        }

        #endregion

        #region Procon Events

        public void OnPluginEnable()
        {
            //Get admin list if needed
            if ((this.mySqlDatabase != null) && (this.mySqlHostname != null) && (this.mySqlPassword != null) && (this.mySqlPort != null) && (this.mySqlUsername != null))
            {
                if (this.useDatabaseAdminList)
                {
                    this.fetchAdminList();
                }
                if (this.useDatabaseWhitelist)
                {
                    this.fetchTeamswapWhitelist();
                }
            }
            isEnabled = true;
            ConsoleWrite("^b^2Enabled!^n^0 Version: " + GetPluginVersion());
        }

        public void OnPluginDisable()
        {
            isEnabled = false;

            ConsoleWrite("^b^1Disabled! =(^n^0");
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            if (this.updating)
            {
                return;
            }
            else
            {
                this.updating = true;
            }
            Dictionary<String, CPlayerInfo> playerList = new Dictionary<String, CPlayerInfo>();
            //Reset the player counts of both sides and recount everything
            this.USPlayerCount = 0;
            this.RUPlayerCount = 0;
            foreach (CPlayerInfo player in players)
            {
                if (player.TeamID == this.USTeamId)
                {
                    this.USPlayerCount++;
                }
                else
                {
                    this.RUPlayerCount++;
                }
                playerList.Add(player.SoldierName, player);
            }
            this.currentPlayers = playerList;
            this.playerList = players;
            //perform the player switching
            this.runTeamSwap();
            this.updating = false;
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            //Get the team scores
            this.serverInfo = serverInfo;
            List<TeamScore> listCurrTeamScore = serverInfo.TeamScores;
            int iTeam0Score = listCurrTeamScore[0].Score;
            int iTeam1Score = listCurrTeamScore[1].Score;
            this.lowestTicketCount = (iTeam0Score < iTeam1Score) ? (iTeam0Score) : (iTeam1Score);
            this.highestTicketCount = (iTeam0Score > iTeam1Score) ? (iTeam0Score) : (iTeam1Score);
        }

        //execute the swap code on player leaving
        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            //When any player leaves, the list of players needs to be updated.
            this.ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
        }
        //execute the swap code on player teamchange
        public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId)
        {
            //When any player leaves, the list of players needs to be updated.
            this.ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
        }

        //runs through both team swap queues and performs the swapping
        public void runTeamSwap()
        {
            //assume the max player count per team is 32 if no server info has been provided
            int maxPlayerCount = (this.serverInfo != null) ? (this.serverInfo.MaxPlayerCount / 2) : (32);
            Boolean movedPlayer;
            do
            {
                movedPlayer = false;
                if (this.RUMoveList.Count > 0)
                {
                    if (this.USPlayerCount < maxPlayerCount)
                    {
                        CPlayerInfo player = this.RUMoveList.Dequeue();
                        ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, this.USTeamId.ToString(), "1", "true");
                        this.playerSayMessage(player.SoldierName, "Swapping you from team RU to team US");
                        movedPlayer = true;
                        USPlayerCount++;
                    }
                }
                if (this.USMoveList.Count > 0)
                {
                    if (this.RUPlayerCount < maxPlayerCount)
                    {
                        CPlayerInfo player = this.USMoveList.Dequeue();
                        ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, this.RussianTeam.ToString(), "1", "true");
                        this.playerSayMessage(player.SoldierName, "Swapping you from team US to team RU");
                        movedPlayer = true;
                        RUPlayerCount++;
                    }
                }
            } while (movedPlayer);
        }

        public void teamSwapPlayer(CPlayerInfo player)
        {
            if (player.TeamID == this.USTeamId)
            {
                if (!this.containsCPlayerInfo(this.USMoveList, player.SoldierName))
                {
                    this.USMoveList.Enqueue(player);
                    this.playerSayMessage(player.SoldierName, "You have been added to the (US -> RU) TeamSwap queue in position " + (this.indexOfCPlayerInfo(this.USMoveList, player.SoldierName) + 1) + ".");
                }
                else
                {
                    this.playerSayMessage(player.SoldierName, "(US -> RU) queue: Position " + (this.indexOfCPlayerInfo(this.USMoveList, player.SoldierName) + 1));
                }
            }
            else
            {
                if (!this.containsCPlayerInfo(this.RUMoveList, player.SoldierName))
                {
                    this.RUMoveList.Enqueue(player);
                    this.playerSayMessage(player.SoldierName, "You have been added to the (RU -> US) TeamSwap queue in position " + (this.indexOfCPlayerInfo(this.RUMoveList, player.SoldierName) + 1) + ".");
                }
                else
                {
                    this.playerSayMessage(player.SoldierName, "(RU -> US) queue: Position " + (this.indexOfCPlayerInfo(this.RUMoveList, player.SoldierName) + 1));
                }
            }
            //call an update of the player list, this will move players when possible
            this.ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
        }

        private bool containsCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            CPlayerInfo[] playerArray = queueList.ToArray();
            for (Int32 index = 0; index < queueList.Count; index++)
            {
                if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                {
                    return true;
                }
            }
            return false;
        }

        private CPlayerInfo getCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            CPlayerInfo[] playerArray = queueList.ToArray();
            for (Int32 index = 0; index < queueList.Count; index++)
            {
                if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                {
                    return ((CPlayerInfo)playerArray[index]);
                }
            }
            return null;
        }

        private Int32 indexOfCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            CPlayerInfo[] playerArray = queueList.ToArray();
            for (Int32 index = 0; index < queueList.Count; index++)
            {
                if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                {
                    return index;
                }
            }
            return -1;
        }

        public void createRecord(String speaker, String[] splitCommand)
        {
            //Create record for return
            CAE_Record record = new CAE_Record();
            //Add server
            record.server_id = this.serverID;

            //Add Command
            string commandString = splitCommand[0].ToLower();
            DebugWrite("Raw Command: " + commandString, 6);
            CAE_CommandType commandType = this.getCommand(commandString);
            //If command not parsable, return without creating
            if (commandType == CAE_CommandType.Default)
            {
                DebugWrite("Command not parsable", 6);
                this.playerSayMessage(speaker, "CAE: Invalid command format.");
                return;
            }
            record.record_type = commandType;

            //Add source
            //Check if player has the right to perform what he's asking
            if (!this.hasAccess(speaker, record.record_type))
            {
                DebugWrite("No rights to call command", 6);
                this.playerSayMessage(speaker, "CAE: No rights to use " + commandString + " command. Inquire about access on ADKGamers.com");
                //Return without creating if player doesn't have rights to do it
                return;
            }
            record.source_name = speaker;

            //Add other general data
            record.source_name = speaker;
            record.server_id = this.serverID;
            record.record_time = DateTime.Now;

            //Add specific data based on type
            switch (record.record_type)
            {
                case CAE_CommandType.MovePlayer:
                    this.playerSayMessage(speaker, "CAE: Use force move.");
                    return;
                    break;
                case CAE_CommandType.ForceMovePlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = "FORCE MOVE";
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.MoveSelf:
                    record.target_name = speaker;
                    record.record_reason = "TEAMSWAP";
                    break;
                case CAE_CommandType.KillPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = splitCommand[2];
                        DebugWrite("reason: " + record.record_reason, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_reason.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "CAE: Reason too short, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.KickPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = splitCommand[2];
                        DebugWrite("reason: " + record.record_reason, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_reason.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "CAE: Reason too short, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.TempBanPlayer:
                    try
                    {
                        int record_duration = 0;
                        DebugWrite("Raw Duration: " + splitCommand[1], 6);
                        Boolean valid = Int32.TryParse(splitCommand[1], out record_duration);
                        record.record_durationMinutes = record_duration;
                        record.target_name = splitCommand[2];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = splitCommand[3];
                        DebugWrite("reason: " + record.record_reason, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_reason.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "CAE: Reason too short, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.BanPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = splitCommand[2];
                        DebugWrite("reason: " + record.record_reason, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_reason.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "CAE: Reason too short, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.PunishPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = splitCommand[2];
                        DebugWrite("reason: " + record.record_reason, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_reason.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "CAE: Reason too short, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.ForgivePlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = splitCommand[2];
                        DebugWrite("reason: " + record.record_reason, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_reason.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "CAE: Reason too short, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.ReportPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = splitCommand[2];
                        DebugWrite("reason: " + record.record_reason, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_reason.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "CAE: Reason too short, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.CallAdmin:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_reason = splitCommand[2];
                        DebugWrite("reason: " + record.record_reason, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "CAE: Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_reason.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "CAE: Reason too short, unable to submit.");
                        return;
                    }
                    break;
                case CAE_CommandType.Yes:
                    CAE_Record recordAttempt = null;
                    this.actionAttemptList.TryGetValue(speaker, out recordAttempt);
                    if (recordAttempt != null)
                    {
                        this.actionAttemptList.Remove(speaker);
                        this.processRecord(recordAttempt);
                    }
                    return;
                case CAE_CommandType.No:
                    this.actionAttemptList.Remove(speaker);
                    return;
                default:
                    return;
            }

            //Check if player exists in the game, or suggest a player
            foreach (CPlayerInfo playerInfo in this.playerList)
            {
                if (playerInfo.SoldierName.ToLower().Equals(record.target_name.ToLower()))
                {
                    //Player found, grab guid and name
                    record.target_guid = playerInfo.GUID;
                    record.target_name = playerInfo.SoldierName;
                    record.targetPlayerInfo = playerInfo;
                    //Process record right away
                    this.processRecord(record);
                    return;
                }
                else if (playerInfo.SoldierName.ToLower().Contains(record.target_name.ToLower()))
                {
                    //Possible player found, grab guid
                    record.target_guid = playerInfo.GUID;
                    record.target_name = playerInfo.SoldierName;
                    record.targetPlayerInfo = playerInfo;
                    //Send record to attempt list
                    this.playerSayMessage(speaker, "CAE: Did you mean: " + playerInfo.SoldierName + "?");
                    this.actionAttemptList.Remove(speaker);
                    this.actionAttemptList.Add(speaker, record);
                    return;
                }
            }
            //No player found
            DebugWrite("player not found", 6);
            this.playerSayMessage(speaker, "CAE: Player not found.");
            return;
        }

        private CAE_CommandType getCommand(string commandString)
        {
            CAE_CommandType command = CAE_CommandType.Default;
            this.CAE_CommandStrings.TryGetValue(commandString, out command);
            return command;
        }

        private void processRecord(CAE_Record record)
        {
            //Upload Record
            this.uploadRecord(record);
            //Perform Actions
            switch (record.record_type)
            {
                case CAE_CommandType.MovePlayer:
                    this.moveTarget(record);
                    break;
                case CAE_CommandType.ForceMovePlayer:
                    this.forceMoveTarget(record);
                    break;
                case CAE_CommandType.MoveSelf:
                    this.forceMoveTarget(record);
                    break;
                case CAE_CommandType.KillPlayer:
                    this.killTarget(record, "");
                    break;
                case CAE_CommandType.KickPlayer:
                    this.kickTarget(record, "");
                    break;
                case CAE_CommandType.TempBanPlayer:
                    this.tempBanTarget(record, "");
                    break;
                case CAE_CommandType.BanPlayer:
                    this.permaBanTarget(record, "");
                    break;
                case CAE_CommandType.PunishPlayer:
                    this.punishTarget(record);
                    break;
                case CAE_CommandType.ForgivePlayer:
                    this.forgiveTarget(record);
                    break;
                case CAE_CommandType.ReportPlayer:
                    this.reportTarget(record);
                    break;
                case CAE_CommandType.CallAdmin:
                    this.callAdminOnTarget(record);
                    break;
                default:
                    break;
            }
        }

        private string PrepareMySqlConnectionString()
        {
            return "Server=" + mySqlHostname + ";Port=" + mySqlPort + ";Database=" + mySqlDatabase + ";Uid=" + mySqlUsername + ";Pwd=" + mySqlPassword + ";";
        }

        private void uploadRecord(CAE_Record record)
        {
            DebugWrite("postRecord starting!", 6);

            if (this.databaseConnection == null)
            {
                this.databaseConnection = new MySqlConnection(PrepareMySqlConnectionString());
            }

            Boolean success = false;
            try
            {
                using (this.databaseConnection)
                {
                    using (MySqlCommand command = this.databaseConnection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "INSERT INTO `" + mySqlDatabase + "`.`cae_records` (`server_id`, `record_type`, `record_durationMinutes`,`target_guid`, `target_name`, `source_name`, `record_reason`, `record_time`) VALUES (@server_id, @record_type, @record_durationMinutes, @target_guid, @target_name, @source_name, @record_reason, @record_time)";
                        //Fill the command
                        DebugWrite(record.target_guid, 6);
                        command.Parameters.AddWithValue("@server_id", record.server_id);
                        string type = "";
                        //Convert enum to DB string
                        this.CAE_RecordTypes.TryGetValue(record.record_type, out type);
                        command.Parameters.AddWithValue("@record_type", type);
                        command.Parameters.AddWithValue("@record_durationMinutes", record.record_durationMinutes);
                        command.Parameters.AddWithValue("@target_guid", record.target_guid);
                        command.Parameters.AddWithValue("@target_name", record.target_name);
                        command.Parameters.AddWithValue("@source_name", record.source_name);
                        command.Parameters.AddWithValue("@record_reason", record.record_reason);
                        command.Parameters.AddWithValue("@record_time", record.record_time);
                        //Open the connection if needed
                        if(!this.databaseConnection.Ping())
                        {
                            this.databaseConnection.Open();
                        }
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0)
                        {
                            success = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(FormatMessage(e.ToString(), MessageTypeEnum.Exception), 3);
            }

            string temp = "";
            this.CAE_RecordTypes.TryGetValue(CAE_CommandType.PunishPlayer, out temp);

            if (success)
            {
                DebugWrite(temp + " log for player '" + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 5);
            }
            else
            {
                DebugWrite(temp + " log for player '" + record.target_name + " by " + record.source_name + " FAILED!", 0);
            }

            DebugWrite("postRecord finished!", 6);
        }

        private void uploadAction(CAE_Record record)
        {
            if (this.databaseConnection == null)
            {
                this.databaseConnection = new MySqlConnection(PrepareMySqlConnectionString());
            }

            Boolean success = false;
            try
            {
                using (this.databaseConnection)
                {
                    using (MySqlCommand command = this.databaseConnection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "INSERT INTO `" + mySqlDatabase + "`.`cae_actionlist` (`server_id`, `player_guid`, `player_name`) VALUES (@server_id, @player_guid, @player_name)";
                        //Fill the command
                        command.Parameters.AddWithValue("@server_id", record.server_id);
                        command.Parameters.AddWithValue("@player_guid", record.target_guid);
                        command.Parameters.AddWithValue("@player_name", record.target_name);
                        //Open the connection if needed
                        if (!this.databaseConnection.Ping())
                        {
                            this.databaseConnection.Open();
                        }
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0)
                        {
                            success = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(FormatMessage(e.ToString(), MessageTypeEnum.Exception), 3);
            }


            if (success)
            {
                DebugWrite("Action log for player '" + record.target_name + " by admin " + record.source_name + " SUCCESSFUL!", 5);
            }
            else
            {
                DebugWrite("Action log for player '" + record.target_name + " by admin " + record.source_name + " FAILED!", 0);
            }
        }

        private int fetchPoints(string player_guid, int server_id)
        {
            DebugWrite("fetchPoints starting!", 6);

            if (this.databaseConnection == null)
            {
                this.databaseConnection = new MySqlConnection(PrepareMySqlConnectionString());
            }

            int returnVal = -1;

            try
            {
                using (this.databaseConnection)
                {
                    using (MySqlCommand command = this.databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT playername, playerguid, serverid, totalpoints FROM `" + this.mySqlDatabase + "`.`cae_playerpoints` WHERE `playerguid` = '" + player_guid + "' AND `serverid` = " + server_id;
                        //Open the connection if needed
                        if (!this.databaseConnection.Ping())
                        {
                            this.databaseConnection.Open();
                        }
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                DebugWrite("getPoints found records for player " + reader.GetString("playername") + "!", 5);
                                returnVal = reader.GetInt32("totalpoints");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }

            DebugWrite("fetchPoints finished!", 6);

            return returnVal;
        }

        private void fetchAdminList()
        {
            DebugWrite("fetchAdminList starting!", 6);

            if (this.databaseConnection == null)
            {
                this.databaseConnection = new MySqlConnection(PrepareMySqlConnectionString());
            }

            List<string> tempAdminList = new List<string>();

            Boolean success = false;
            try
            {
                using (this.databaseConnection)
                {
                    using (MySqlCommand command = this.databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT name AS admin_name, uniqueid AS admin_guid FROM `" + this.mySqlDatabase + "`.`adminlist`";
                        //Open the connection if needed
                        if (!this.databaseConnection.Ping())
                        {
                            this.databaseConnection.Open();
                        }
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                success = true;
                                string admin_name = reader.GetString("admin_name").ToLower();
                                string admin_guid = reader.GetString("admin_guid");
                                DebugWrite("Admin found: " + admin_name, 6);
                                //only use admin names not guids for now
                                tempAdminList.Add(admin_name);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }

            if (success)
            {
                this.databaseAdminCache = tempAdminList;
                DebugWrite("Admin List Fetched from Database. Length:" + this.databaseAdminCache.Count, 0);
            }
            else
            {
                DebugWrite("Failed to fetch admin list. Restart plugin to re-call fetch.", 0);
            }

            DebugWrite("fetchAdminList finished!", 6);
        }

        private void fetchTeamswapWhitelist()
        {
            DebugWrite("fetchTeamswapWhitelist starting!", 6);

            if (this.databaseConnection == null)
            {
                this.databaseConnection = new MySqlConnection(PrepareMySqlConnectionString());
            }

            List<string> tempWhitelist = new List<string>();

            Boolean success = false;
            try
            {
                using (this.databaseConnection)
                {
                    using (MySqlCommand command = this.databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT player_name AS player_name FROM `" + this.mySqlDatabase + "`.`cae_teamswapwhitelist`";
                        //Open the connection if needed
                        if (!this.databaseConnection.Ping())
                        {
                            this.databaseConnection.Open();
                        }
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                success = true;
                                string player_name = reader.GetString("player_name").ToLower();
                                DebugWrite("Player found: " + player_name, 6);
                                //only use admin names not guids for now
                                tempWhitelist.Add(player_name);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }

            if (success)
            {
                this.databaseWhitelistCache = tempWhitelist;
                DebugWrite("Teamswap Whitelist Fetched from Database. Length:" + this.databaseWhitelistCache.Count, 0);
            }
            else
            {
                DebugWrite("Failed to fetch teamswap whitelist. Restart plugin to re-call fetch.", 0);
            }

            DebugWrite("fetchTeamswapWhitelist finished!", 6);
        }

        private Boolean hasAccess(String player_name, CAE_CommandType command)
        {
            switch (command)
            {
                case CAE_CommandType.MovePlayer:
                    return this.isAdmin(player_name);
                case CAE_CommandType.ForceMovePlayer:
                    return this.isAdmin(player_name);
                case CAE_CommandType.MoveSelf:
                    if (this.isAdmin(player_name))
                    {
                        return true;
                    }
                    else if (this.requireWhitelist)
                    {
                        return this.isWhitelisted(player_name);
                    }
                    else
                    {
                        return true;
                    }
                case CAE_CommandType.KillPlayer:
                    return this.isAdmin(player_name);
                case CAE_CommandType.KickPlayer:
                    return this.isAdmin(player_name);
                case CAE_CommandType.TempBanPlayer:
                    return this.isAdmin(player_name);
                case CAE_CommandType.BanPlayer:
                    return this.isAdmin(player_name);
                case CAE_CommandType.PunishPlayer:
                    return this.isAdmin(player_name);
                case CAE_CommandType.ForgivePlayer:
                    return this.isAdmin(player_name);
                case CAE_CommandType.ReportPlayer:
                    return true;
                case CAE_CommandType.CallAdmin:
                    return true;
                case CAE_CommandType.Yes:
                    return true;
                case CAE_CommandType.No:
                    return true;
                default:
                    this.DebugWrite("Command not recognized in hasAccess", 6);
                    return false;
            }
        }

        private Boolean isAdmin(string player_name)
        {
            if (this.useDatabaseAdminList)
            {
                return (this.databaseAdminCache.Contains(player_name.ToLower()) || this.staticAdminCache.Contains(player_name.ToLower()));
            }
            else
            {
                return this.staticAdminCache.Contains(player_name.ToLower());
            }
        }

        private Boolean isWhitelisted(string player_name)
        {
            if (this.useDatabaseWhitelist)
            {
                return (this.databaseWhitelistCache.Contains(player_name.ToLower()) || this.staticWhitelistCache.Contains(player_name.ToLower()));
            }
            else
            {
                return this.staticWhitelistCache.Contains(player_name.ToLower());
            }
        }

        #endregion

        #region Server Messaging

        //all messaging is redirected to global chat for analysis
        public override void OnGlobalChat(string speaker, string message)
        {
            if (message.StartsWith("@") || message.StartsWith("!"))
            {
                message = message.Substring(1);
            }
            else if (message.StartsWith("/@") || message.StartsWith("/!"))
            {
                message = message.Substring(2);
            }
            else
            {
                //If the message does not cause either of the above clauses, then ignore it.
                return;
            }

            //Split the command into sections by whitespace
            string[] splitCommand = message.Trim().Split(' ');
            //Create the record
            this.createRecord(speaker, splitCommand);
            //Processing complete. End.
            return;
        }
        public override void OnTeamChat(string speaker, string message, int teamId) { this.OnGlobalChat(speaker, message); }
        public override void OnSquadChat(string speaker, string message, int teamId, int squadId) { this.OnGlobalChat(speaker, message); }


        public void playerSayMessage(string target, string message)
        {
            ExecuteCommand("procon.protected.send", "admin.say", message, "player", target);
            ExecuteCommand("procon.protected.chat.write", string.Format("(PlayerSay {0}) ", target) + message);
        }

        public void moveTarget(CAE_Record record)
        {
            forceMoveTarget(record);
        }

        public void forceMoveTarget(CAE_Record record)
        {
            this.DebugWrite("Entering forceMoveTarget", 6);

            if (record.record_type == CAE_CommandType.MoveSelf)
            {
                if (this.isAdmin(record.source_name) || ((this.teamSwapTicketWindowHigh >= this.highestTicketCount) && (this.teamSwapTicketWindowLow <= this.lowestTicketCount)) )
                {
                    this.DebugWrite("Calling Teamswap on self", 6);
                    teamSwapPlayer(record.targetPlayerInfo);
                }
                else
                {
                    this.DebugWrite("Player unable to teamswap", 6);
                    this.playerSayMessage(record.source_name, "You cannot TeamSwap at this time. Game outside ticket window [" + this.teamSwapTicketWindowLow + ", " + this.teamSwapTicketWindowHigh + "].");
                }
            }
            else
            {
                this.DebugWrite("Calling Teamswap on target", 6);
                this.playerSayMessage(record.source_name, "CAE: " + record.target_name + " sent to teamswap.");
                teamSwapPlayer(record.targetPlayerInfo);
            }

            this.DebugWrite("Exiting forceMoveTarget", 6);
        }

        public void killTarget(CAE_Record record, string additionalMessage)
        {
            //Perform actions
            ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_name);
            this.playerSayMessage(record.target_name, "Killed by admin for: " + record.record_reason + ". " + additionalMessage);
            this.playerSayMessage(record.source_name, "CAE: You KILLED " + record.target_name + " for " + record.record_reason + ". " + additionalMessage);
        }

        public void kickTarget(CAE_Record record, string additionalMessage)
        {
            //Perform Actions
            ExecuteCommand("procon.protected.send", "admin.kickPlayer", record.target_name, record.record_reason + ". " + additionalMessage);
            this.playerSayMessage(record.target_name, "Killed by admin for: " + record.record_reason + "." + additionalMessage);
            this.playerSayMessage(record.source_name, "CAE: You KICKED " + record.target_name + " for " + record.record_reason + ". ");
        }

        public void tempBanTarget(CAE_Record record, string additionalMessage)
        {
            //Perform Actions
            Int32 seconds = record.record_durationMinutes * 60;
            ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_guid, "seconds", seconds + "", record.record_reason + ". " + additionalMessage);
            ExecuteCommand("procon.protected.send", "banList.save");
            ExecuteCommand("procon.protected.send", "banList.list");
            this.playerSayMessage(record.source_name, "CAE: You TEMP BANNED " + record.target_name + " for " + record.record_durationMinutes + " minutes. " + additionalMessage);
        }

        public void permaBanTarget(CAE_Record record, string additionalMessage)
        {
            //Perform Actions
            ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_guid, "perm", record.record_reason + ". " + additionalMessage);
            ExecuteCommand("procon.protected.send", "banList.save");
            ExecuteCommand("procon.protected.send", "banList.list");
            this.playerSayMessage(record.source_name, "CAE: You PERMA BANNED " + record.target_name + "! Get a vet admin NOW!" + additionalMessage);
        }

        public void punishTarget(CAE_Record record)
        {
            if (this.actOnPunishments)
            {
                //Get number of points the player from server
                int points = this.fetchPoints(record.target_guid, this.serverID);
                //Get the proper action to take for player punishment
                string action = "noaction";
                if (points > (this.punishmentHierarchy.Length - 1))
                {
                    action = this.punishmentHierarchy[this.punishmentHierarchy.Length - 1];
                }
                else if (points > 0)
                {
                    action = this.punishmentHierarchy[points - 1];
                }
                //Set additional message
                string additionalMessage = "(" + points + " infraction points)";

                //Call correct action
                if (action.Equals("kill") || (this.onlyKillOnLowPop && this.playerList.Count < this.lowPopPlayerCount))
                {
                    this.killTarget(record, additionalMessage);
                }
                else if (action.Equals("kick"))
                {
                    this.kickTarget(record, additionalMessage);
                }
                else if (action.Equals("tban60"))
                {
                    record.record_durationMinutes = 60;
                    this.tempBanTarget(record, additionalMessage);
                }
                else if (action.Equals("tbanweek"))
                {
                    record.record_durationMinutes = 10080;
                    this.tempBanTarget(record, additionalMessage);
                }
                else if (action.Equals("ban"))
                {
                    this.permaBanTarget(record, additionalMessage);
                }
            }
            else
            {
                this.playerSayMessage(record.source_name, "CAE: Punish Logged for " + record.target_name);
                this.uploadAction(record);
            }
        }

        public void forgiveTarget(CAE_Record record)
        {
            this.playerSayMessage(record.source_name, "CAE: Forgive Logged for " + record.target_name);
            this.playerSayMessage(record.target_name, "CAE: Forgiven 1 infraction point. You now have " + this.fetchPoints(record.target_guid, record.server_id) + " point(s) against you.");
        }

        public void reportTarget(CAE_Record record)
        {
            foreach (String admin_name in this.databaseAdminCache)
            {
                this.playerSayMessage(admin_name, "REPORT: " + record.source_name + " reported " + record.target_name + " for " + record.record_reason);
            }
            foreach (String admin_name in this.staticAdminCache)
            {
                this.playerSayMessage(admin_name, "REPORT: " + record.source_name + " reported " + record.target_name + " for " + record.record_reason);
            }
            this.playerSayMessage(record.source_name, "Report sent to admins on " + record.target_name + " for " + record.record_reason);
        }
        
        public void callAdminOnTarget(CAE_Record record)
        {
            foreach (String admin_name in this.databaseAdminCache)
            {
                this.playerSayMessage(admin_name, "ADMIN CALL: " + record.source_name + " called admin on " + record.target_name + " for " + record.record_reason);
            }
            foreach (String admin_name in this.staticAdminCache)
            {
                this.playerSayMessage(admin_name, "ADMIN CALL: " + record.source_name + " called admin on " + record.target_name + " for " + record.record_reason);
            }
            this.playerSayMessage(record.source_name, "Admin call sent on " + record.target_name + " for " + record.record_reason);
        }

        #endregion

        #region Helper Classes

        public class CAE_Record
        {
            public int server_id = -1;
            public string target_guid = null;
            public string target_name = null;
            public string source_name = null;
            public CAE_CommandType record_type = CAE_CommandType.Default;
            public string record_reason = null;
            public DateTime record_time = DateTime.Now;
            public Int32 record_durationMinutes = 0;

            //Sup Attributes
            public CPlayerInfo targetPlayerInfo;

            public CAE_Record(int server_id, CAE_CommandType record_type, Int32 record_durationMinutes, string target_guid, string target_name, string source_name, string record_reason, DateTime record_time)
            {
                this.server_id = server_id;
                this.record_type = record_type;
                this.target_guid = target_guid;
                this.target_name = target_name;
                this.source_name = source_name;
                this.record_reason = record_reason;
                this.record_time = record_time;
                this.record_durationMinutes = record_durationMinutes;
            }

            public CAE_Record()
            {
            }
        }

        public string FormatMessage(string msg, MessageTypeEnum type)
        {
            string prefix = "[^bCross-Admin Enforcer^n] ";

            if (type.Equals(MessageTypeEnum.Warning))
            {
                prefix += "^1^bWARNING^0^n: ";
            }
            else if (type.Equals(MessageTypeEnum.Error))
            {
                prefix += "^1^bERROR^0^n: ";
            }
            else if (type.Equals(MessageTypeEnum.Exception))
            {
                prefix += "^1^bEXCEPTION^0^n: ";
            }

            return prefix + msg;
        }

        public void LogWrite(string msg)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
        }

        public void ConsoleWrite(string msg, MessageTypeEnum type)
        {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Normal);
        }

        public void ConsoleWarn(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Warning);
        }

        public void ConsoleError(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Error);
        }

        public void ConsoleException(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Exception);
        }

        public void DebugWrite(string msg, int level)
        {
            if (debugLevel >= level)
            {
                ConsoleWrite(msg, MessageTypeEnum.Normal);
            }
        }

        #endregion

        #region Helper methods

        public void ServerCommand(params string[] args)
        {
            List<string> list = new List<string>();
            list.Add("procon.protected.send");
            list.AddRange(args);
            this.ExecuteCommand(list.ToArray());
        }

        #endregion

    } // end xADKAdminTools
} // end namespace PRoConEvents

