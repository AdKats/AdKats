AdKats
======
<p>
    Advanced Admin Tool Set for A-Different-Kind, with MySQL database back-end.
</p>
<h2>Description</h2>
<h3>Main</h3>
<p>
    This plugin should be used by groups with high traffic servers, with set rules, and many admins. It is a
    MySQL database reflected admin tool that includes editable in-game commands, database reflected punishment and
    forgiveness, proper player report and admin call handling, and internal implementation of Teamswap.
</p>
<h3>Reason Behind Development</h3>
<p>
    Players who break rules over time usually don't end up doing it in front of the same admin twice, so minimal or
    'incorrect' action is taken. 
    On very active servers with high player turn-around it's impossible for admins to track a player's history in their
    head, now the punish system tracks that instead and takes proper action based on a player's history.<br/><br/>

    In general this combines simple in-game admin commands, player name completion, admin punish/forgive,
    report/calladmin, and teamswap, into one plugin to reduce the load on your procon layer.
</p>
<h3>Punishment/Forgiveness System</h3>
<p>
    NOTE: This is NOT the player based punish/forgive system normally used for teamkilling, and is only usable by
    admins.<br/><br/>

    When a player is 'punished' by an admin a log is made in the database, their total points
    are calculated <b>(Punishment Count - Forgiveness Count = Total Points)</b> then an action decided from the punishment hierarchy. Punishments should get more harsh as the
    player gets more points. The punishment hierarchy is configurable to suit your needs. ADK is rather lenient with
    players, so there are 8 levels before a player is perma-banned in our version. Players may also be 'forgiven', which
    will reduce their total point value by 1 each time, this is useful if you have a website where players can apologize
    for their actions in-game.<br/><br/>

    Logs can be made server specific (The case of running multiple servers with this plugin on the same database),
    meaning rule breaking on one server won't cause increase in punishments on another server for that same player. This
    is available since many groups run different rule sets on each server they own. You can set all servers to the same
    ID for punishments to act across servers.<br/><br/>

    If you have a procon-external system (such as a web-based tool with direct rcon connection for server control), then
    use the ActionList table by turning 'act on punishments' off. The plugin will then send player Id's who need
    punishment (along with the server they are in) to that table for your external system to act on, instead of taking
    action here.<br/><br/>

    All commands which might lead to actions against players are required to have a reason entered, and will cancel if
    no reason is given. When deciding to use this system, 'punish' should be the only command used for player rule-breaking.
    Other commands like kill, or kick are not counted since sometimes players ask to be kill, and admins kill/kick themselves to leave games.
    Direct tban and ban are of course left in here for hacker/glitching situations, but that is the ONLY time they should be used.
    (currently working on adding direct tban counter into this system though, it would immediately escalate the player's hierarchy to tban level)
</p>
<h3>Report/CallAdmin System</h3>
<p>
    All uses of @report and @admin with this plugin require players to enter a reason, and will tell them if they
    haven't entered one. It will not send the report to admins unless it's in proper format. This cleans up what admins
    end up seeing for reports, useful since ADK admins get reports and admin calls whether they are in-game or not. When
    a player puts in a proper @report or @admin, all in-game admins are notified, then the report is logged in the
    database with full player names for reporter/target, and the full reason for reporting.
</p>
<h3>Teamswap</h3>
<p>
    This plugin implements Teamswap. Teamswap is a server-smart player moving system that offers two major benefits over the default system. It's available as a separate plugin, but in this instance I've merged it with
    move and forcemove commands. Normally if the team a player gets @move'd or @fmove'd to
    is full then the command just fails, now they are dropped on a queue until a slot opens on that side. They can keep
    playing on their side until that slot opens, when it does they are immediately slain and moved over to fill it.
    Secondly it allows whitelisted (non-admin) players the ability to move themselves between teams as often as they
    want (within a ticket count window). This is currently not an available option in default battlefield aside from
    procon commands, as the game itself limits players to one switch per gaming session. Whitelisted players can type
    '@moveme' and the plugin will queue them. This is meant to be available to players outside the admin list, usually
    by paid usage to your community or to clan members only. Admins can also use '@moveme', and in their case it
    bypasses the ticket window restriction.
</p>
<h3>Performance</h3>
<p>
    There still needs to be more testing done in this section. However, I've designed it to be rather heavy when 
    changing settings, but lighter when in use. All commands are stored in dictionaries so command meanings are parsed instantly when entered. 
    Also during record parsing there is a hierarchy of checks the command goes through (specific to each command), 
    if any of them fail the process ends immediately and informs the calling player of the error they made.
</p>
<h3>Available In-Game Commands</h3>
<p>
    <u><b>You can edit the text typed for each command to suit your needs in plugin settings.</b></u> Usage of all
    commands is database logged by default, but each command can be told whether to log or not. Logging all is useful
    though, especially when you have to hold 40+ admins accountable for their actions.<br/><br/>
    <table>
        <tr>
            <td><b>Command</b></td>
            <td><b>Params</b></td>
            <td><b>Access</b></td>
            <td><b>Description</b></td>
        </tr>
        <tr>
            <td><b>Kill</b></td>
            <td>[player][reason]</td>
            <td>Admin</td>
            <td>The in-game command used for killing players.</td>
        </tr>
        <tr>
            <td><b>Kick</b></td>
            <td>[player][reason]</td>
            <td>Admin</td>
            <td>The in-game command used for kicking players.</td>
        </tr>
        <tr>
            <td><b>TempBan</b></td>
            <td>[minutes] [player] [reason]</td>
            <td>Admin</td>
            <td>The in-game command used temp-banning players.</td>
        </tr>
        <tr>
            <td><b>PermaBan</b></td>
            <td>[player][reason]</td>
            <td>Admin</td>
            <td>The in-game command used for perma-banning players.</td>
        </tr>
        <tr>
            <td><b>Punish</b></td>
            <td>[player][reason]</td>
            <td>Admin</td>
            <td>The in-game command used for punishing players. Will add a Punish record to the database, increasing a player's total points by 1.</td>
        </tr>
        <tr>
            <td><b>Forgive</b></td>
            <td>[player][reason]</td>
            <td>Admin</td>
            <td>The in-game command used for forgiving players. Will add a Forgive record to the database, decreasing a player's total points by 1.</td>
        </tr>
        <tr>
            <td><b>Move</b></td>
            <td>[player]</td>
            <td>Admin</td>
            <td>The in-game command used for moving players between teams. Will add players to a death move list, when they die they will be sent to teamswap.</td>
        </tr>
        <tr>
            <td><b>ForceMove</b></td>
            <td>[player]</td>
            <td>Admin</td>
            <td>The in-game command used for force-moving players between teams. Will immediately send the given player to teamswap.</td>
        </tr>
        <tr>
            <td><b>TeamSwap</b></td>
            <td>None</td>
            <td>Admin and TeamSwap Whitelist</td>
            <td>The in-game command used for moving yourself between teams. Will immediately send the speaker to TeamSwap.</td>
        </tr>
        <tr>
            <td><b>Report</b></td>
            <td>[player][reason]</td>
            <td>All Players</td>
            <td>The in-game command used for reporting players. Must have a reason, and will inform a player otherwise when using. Will log a Report tuple in the database(External GCP polls from there for external admin notifications), and notify all in-game admins.</td>
        </tr>
        <tr>
            <td><b>CallAdmin</b></td>
            <td>[player][reason]</td>
            <td>All Players</td>
            <td>The in-game command used for calling admin attention to a player. Same deal as report, but used for a different reason.</td>
        </tr>
        <tr>
            <td><b>AdminSay</b></td>
            <td>[message]</td>
            <td>Admin</td>
            <td>The in-game command used to send a message through admin chat.</td>
        </tr>
        <tr>
            <td><b>AdminYell</b></td>
            <td>[message]</td>
            <td>Admin</td>
            <td>The in-game command used for to send a message through admin yell.</td>
        </tr>
        <tr>
            <td><b>PlayerSay</b></td>
            <td>[player][message]</td>
            <td>Admin</td>
            <td>The in-game command used for sending a message through admin chat to only a specific player.</td>
        </tr>
        <tr>
            <td><b>PlayerYell</b></td>
            <td>[player][message]</td>
            <td>Admin</td>
            <td>The in-game command used for sending a message through admin yell to only a specific player.</td>
        </tr>
        <tr>
            <td><b>RestartLevel</b></td>
            <td>None</td>
            <td>Admin</td>
            <td>The in-game command used for restarting the round.</td>
        </tr>
        <tr>
            <td><b>NextLevel</b></td>
            <td>None</td>
            <td>Admin</td>
            <td>The in-game command used for running the next map in current rotation, but keep all points and KDRs from this round.</td>
        </tr>
        <tr>
            <td><b>EndLevel</b></td>
            <td>[US/RU]</td>
            <td>Admin</td>
            <td>The in-game command used for ending the current round with a winning team. Either US or RU.</td>
        </tr>
        <tr>
            <td><b>Confirm Command</b></td>
            <td>None</td>
            <td>All Players</td>
            <td>The in-game command used for confirming other commands when needed.</td>
        </tr>
        <tr>
            <td><b>Cancel Command</b></td>
            <td>None</td>
            <td>All Players</td>
            <td>The in-game command used to cancel other commands when needed.</td>
        </tr>
    </table>
</p>
<h2>Settings</h2>
<p>
<h3>Debugging Settings:</h3>
* <b>'Debug
    level'</b> - Indicates how much debug-output is printed to the plugin-console. 0 turns off debug messages (just shows important warnings/exceptions), 6 documents nearly every step.
<h3>Admin Settings:</h3>
* <b>'Use Database Admin
    List'</b> - Whether to use list of admins from 'adminlist' table to cached admin list on plugin start. Plugin must be disabled and re-enabled (or db settings changed) to update admin list from database, admin names are cached in the plugin to save bandwidth.<br/>
* <b>'Static Admin List'</b> - List of admins input from plugin settings. Use if no admin database table.
<h3>MySQL Settings:</h3>
* <b>'MySQL Hostname'</b> - Hostname of the MySQL-Server Cross-Admin Enforcer should connect to. <br/>
* <b>'MySQL Port'</b> - Port of the MySQL-Server Cross-Admin Enforcer should connect to. <br/>
* <b>'MySQL
    Database'</b> - Database Cross-Admin Enforcer should use for storage. Hardcoded table names and creation scripts given below.<br/>
* <b>'MySQL Username'</b> - Username of the MySQL-Server Cross-Admin Enforcer should connect to. <br/>
* <b>'MySQL Password'</b> - Password of the MySQL-Server Cross-Admin Enforcer should connect to.
<h3>Command Settings:</h3>
Below are what the in-game commands format will be for all plugin functions. Command text is defined in each setting. All commands can be suffixed with '|log', this sets whether logs for that command will be sent to the database. Punish and Forgive commands require logging.
<br/><br/>
* <b>'Minimum Required Reason
    Length'</b> - The minimum length a reason must be for commands that require a reason to execute.<br/>
* <b>'Yell display time seconds'</b> - The integer time in seconds that yell messages will be displayed.<br/><br/>

<b>Specific command definitions given above.</b> All command text must be a single string with no whitespace. E.G. kill. All commands can be suffixed with '|log', which will set whether use of that command is logged in the database or not.
<h3>Punishment Settings:</h3>
* <b>'Act on
    Punishments'</b> - Whether the plugin should carry out punishments, or have an external source do it through adkat_actionlist.
<br/>
* <b>'Punishment
    Hierarchy'</b> - List of punishments in order from lightest to most severe. Index in list is the action taken at that number of points.
<br/>
* <b>'Minimum Reason
    Length'</b> - The minimum number of characters a reason must be to call punish or forgive on a player.<br/>
* <b>'Only Kill Players when Server in low
    population'</b> - When server population is below 'Low Server Pop Value', only kill players, so server does not empty. Player points will still be incremented normally.
<br/>
* <b>'Low Server Pop Value'</b> - Number of players at which the server is deemed 'Low Population'.<br/>
* <b>'Punishment
    Timeout'</b> - A player cannot be punished more than once every x.xx minutes. This prevents multiple admins from punishing a player multiple times for the same infraction.
<h3>Server Settings:</h3>
* <b>'Server
    ID'</b> - ID that will be used to identify this server in the database. This is not linked to any database attributes, instead it's used to differentiate between servers for infraction points.
<br/>
<h3>Teamswap Settings:</h3>
* <b>'Require Whitelist for
    Access'</b> - Whether the 'moveme' command will require whitelisting. Admins are always allowed to use it.<br/>
* <b>'Static Player Whitelist'</b> - Static list of players plugin-side that will be able to teamswap.<br/>
* <b>'Use Database
    Whitelist'</b> - Whether to use 'adkat_teamswapwhitelist' table in the database for player whitelisting. Plugin must be disabled and re-enabled (or db settings changed) to update whitelist from database, whitelisted names are cached in the plugin to save bandwidth.<br/>
* <b>'Ticket Window
    High'</b> - When either team is above this ticket count, then nobody (except admins) will be able to use teamswap.
<br/>
* <b>'Ticket Window
    Low'</b> - When either team is below this ticket count, then nobody (except admins) will be able to use teamswap.
</p>
<h2>Default Punishment Levels by Point</h2>
<p>
    Action decided after player is punished, and their points incremented.<br/><br/>
    * 1 point - Player Killed. <br/>
    * 2 points - Player Killed. <br/>
    * 3 points - Player Kicked. <br/>
    * 4 points - Player Kicked. <br/>
    * 5 points - Player Temp-Banned for 60 minutes. <br/>
    * 6 points - Player Temp-Banned for 60 minutes. <br/>
    * 7 points - Player Temp-Banned for 1 week. <br/>
    * 8 points - Player Temp-Banned for 1 week. <br/>
    * 9 points - Player Perma-Banned. <br/>
</p>
<h2>Database Tables and Views</h2>
<p>
    Main record table is the following:<br/>
    <br/>
    CREATE TABLE `adkat_records` (<br/>
    `record_id` int(11) NOT NULL AUTO_INCREMENT,<br/>
    `server_id` int(11) NOT NULL,<br/>
    `command_type`
    enum('Move','ForceMove','Teamswap','Kill','Kick','TempBan','Ban','Punish','Forgive','Report','CallAdmin') NOT
    NULL,<br/>
    `record_durationMinutes` int(11) NOT NULL,<br/>
    `target_guid` varchar(100) NOT NULL,<br/>
    `target_name` varchar(45) NOT NULL,<br/>
    `source_name` varchar(45) NOT NULL,<br/>
    `record_message` varchar(100) NOT NULL,<br/>
    `record_time` datetime NOT NULL,<br/>
    PRIMARY KEY (`record_id`)<br/>
    );<br/>
    <br/>
    Action List table is the following:<br/>
    <br/>
    CREATE TABLE `adkat_actionlist` (<br/>
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
    CREATE TABLE `adkat_teamswapwhitelist` (<br/>
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
    CREATE VIEW `adkat_playerlist` AS select `adkat_records`.`target_name` AS
    `player_name`,`adkat_records`.`target_guid` AS `player_guid`,`adkat_records`.`server_id` AS `server_id` from
    `adkat_records` group by `adkat_records`.`target_guid`,`adkat_records`.`server_id` order by
    `adkat_records`.`target_name`;<br/>
    <br/>
    Current Player Points View is the following:<br/>
    <br/>
    CREATE VIEW `adkat_playerpoints` AS select `adkat_playerlist`.`player_name` AS
    `playername`,`adkat_playerlist`.`player_guid` AS `playerguid`,`adkat_playerlist`.`server_id` AS `serverid`,(select
    count(`adkat_records`.`target_guid`) from `adkat_records` where ((`adkat_records`.`command_type` = 'Punish') and
    (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`) and (`adkat_records`.`server_id` =
    `adkat_playerlist`.`server_id`))) AS `punishpoints`,(select count(`adkat_records`.`target_guid`) from
    `adkat_records` where ((`adkat_records`.`command_type` = 'Forgive') and (`adkat_records`.`target_guid` =
    `adkat_playerlist`.`player_guid`) and (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) AS
    `forgivepoints`,((select count(`adkat_records`.`target_guid`) from `adkat_records` where
    ((`adkat_records`.`command_type` = 'Punish') and (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`)
    and (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) - (select count(`adkat_records`.`target_guid`)
    from `adkat_records` where ((`adkat_records`.`command_type` = 'Forgive') and (`adkat_records`.`target_guid` =
    `adkat_playerlist`.`player_guid`) and (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)))) AS
    `totalpoints` from `adkat_playerlist`;<br/>
    <br/>
    Current Player Reports and Admin calls is shown with the following:<br/>
    <br/>
    CREATE VIEW `adkat_reports` AS select `adkat_records`.`record_id` AS `record_id`,`adkat_records`.`server_id` AS
    `server_id`,`adkat_records`.`command_type` AS `command_type`,`adkat_records`.`record_durationMinutes` AS
    `record_durationMinutes`,`adkat_records`.`target_guid` AS `target_guid`,`adkat_records`.`target_name` AS
    `target_name`,`adkat_records`.`source_name` AS `source_name`,`adkat_records`.`record_message` AS
    `record_message`,`adkat_records`.`record_time` AS `record_time` from `adkat_records` where
    ((`adkat_records`.`command_type` = 'Report') or (`adkat_records`.`command_type` = 'CallAdmin'));<br/>
    <br/>
    ALL THE ABOVE NEED TO BE RUN IN THIS ORDER. Once the views are done a constant tally of player points can be seen
    from your external systems. <br/>
    <br/>
    <br/>
    Additional View if you want it:<br/>
    <br/>
    CREATE VIEW `adkat_naughtylist` AS select (select `tbl_server`.`ServerName` from `tbl_server` where
    (`tbl_server`.`ServerID` = `adkat_playerpoints`.`serverid`)) AS `server_name`,`adkat_playerpoints`.`playername` AS
    `player_name`,`adkat_playerpoints`.`totalpoints` AS `total_points` from `adkat_playerpoints` where
    (`adkat_playerpoints`.`totalpoints` > 0) order by `adkat_playerpoints`.`serverid`,`adkat_playerpoints`.`playername`;<br/>
</p>
<h2>Development</h2>
<p>
    Started by ColColonCleaner for ADK Gamers on Apr. 20, 2013
</p>
<h3>Changelog</h3>
<blockquote>
    <h4>0.0.1 (20-APR-2013)</h4>
    <b>Main: </b> <br/>
    Initial Version <br/>
    <h4>0.0.2 (25-APR-2013)</h4>
    <b>Changes</b> <br/>
    * Added plugin-side punishment. <br/>
    * Initial DB test, tables updated. <br/>
    <h4>0.0.3 (28-APR-2013)</h4>
    <b>Changes</b> <br/>
    * In-game commands no longer case sensitive. <br/>
    * External DB test, tables updated. <br/>
    * First in-game run during match, minor bugs fixed. <br/>
    <h4>0.0.4 (29-APR-2013)</h4>
    <b>Changes</b> <br/>
    * Added editable in-game commands for forgive/punish. <br/>
    <h4>0.0.5 (30-APR-2013)</h4>
    <b>Changes</b> <br/>
    * Removed global player access for production version. <br/>
    * Added admin list for access. <br/>
    * Added 'Low Server Pop' override system
    <h4>0.0.6 (1-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Added access to database admin list. <br/>
    * Fixed minor bugs during testing. <br/>
    <h4>0.0.7 (1-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Added view definitions to display current player point values. <br/>
    <h4>0.0.8 (2-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Updated player-say messages when punishment acted on. More informative message used.<br/>
    * Added editable minimum reason length.<br/>
    <h4>0.0.9 (3-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Added direct kill, kick, tban, and ban commands.<br/>
    * Made direct commands work with database.<br/>
    * Removed editable command list.<br/>
    <h4>0.1.0 (5-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Refactor record creation to increase speed.<br/>
    * Enumerate all database commands to parse in-game commands.<br/>
    * Implement xTeamSwap functions within this plugin.<br/>
    * Create database whitelist definitions for teamswap.<br/>
    * Add move, fmove, moveme as commands that use teamswap.<br/>
    * Refactor database logging to work with all commands.<br/>
    * Code cleanup and organize.<br/>
    * Player and admin messaging changes.<br/>
    <h4>0.1.1 (6-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Punish and forgive commands changed to 'pun' and 'for', for ease of use in high punish/minute instances.<br/>
    * Now a player may only be punished once every x minutes, this removes the case where two admins can punish a player
    for the same infraction.<br/>
    <h4>0.1.2 (8-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Re-added editable command list.<br/>
    <h4>0.1.3 (9-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Refactored settings and parsing to make the plugin more heavy while changing settings, but much lighter once in
    use.<br/>
    * Added setting for whether a command will be logged or not. Adding '|log' to the end of a setting name will make
    the plugin log uses of that command in the database. Default is logging for all action commands (which should be
    fine for performance). Right now only Punish and Forgive are required to be logged.<br/>
    * Fixed move command, now sends player to teamswap once they have died.<br/>
    <h4>0.1.4 (10-MAY-2013)</h4>
    <b>Changes</b> <br/>
    * Fixed bugs in command logging interface and command setting initialization.<br/>
    <br/>
    TODO 1: Add watchlist use.
</blockquote>
