<h1>AdKats</h1>
<p>
A MySQL reflected admin toolset that includes editable in-game commands, database reflected punishment and
forgiveness, proper player report and admin call handling, player name completion, player muting, yell/say 
pre-recording, and internal implementation of TeamSwap.

Visit the tool's github page to submit bugs/enhancements, or to view more complete docs.
</p>
<h2>Description</h2>
<h3>Main</h3>
<p>
This tool was designed for use by groups with high traffic servers and many admins, but will function just as well 
for small servers.
</p>
<h3>Punishment/Forgiveness System</h3>
<p>
<b>NOTE:</b> This is NOT the player based punish/forgive system normally used for teamkilling, and is only usable by
admins.<br/><br/>

<b>BASIC VERSION:</b><br/>
Use of punish and forgive commands takes the load off admins remembering what players have broken server rules, and 
how many times. Each time a player is punished it's logged in the database, and the more punishes they get the more 
severe the punishment. Available punishments include kill, kick, temp-ban 60 minutes, temp-ban 1 week, and permaban. 
Order that the punishments are given can be configured to your needs. The default is kill, kill, kick, kick, tban60, 
tban60, tbanweek, tbanweek, ban.<br/><br/>

<b>DETAILED VERSION:</b><br/>
When a player is 'punished' by an admin a Punish log is made in the database, their total points are calculated using 
this basic formula:<br/><br/>

<b>(Punishment Count - Forgiveness Count = Total Points)</b><br/><br/>

Then an action is decided using total points from the punishment hierarchy. Punishments should get more harsh as the
player gets more points. The punishment hierarchy is configurable to suit your needs, but the default is below.<br/><br/>

<table>
	<tr>
		<td><b>Total Points</b></td>
		<td><b>Punishment Outcome</b></td>
	</tr>
	<tr>
		<td><b>Less than 1</b></td>
		<td>Kill</td>
	</tr>
	<tr>
		<td><b>1</b></td>
		<td>Kill</td>
	</tr>
	<tr>
		<td><b>2</b></td>
		<td>Kill</td>
	</tr>
	<tr>
		<td><b>3</b></td>
		<td>Kick</td>
	</tr>
	<tr>
		<td><b>4</b></td>
		<td>Kick</td>
	</tr>
	<tr>
		<td><b>5</b></td>
		<td>Temp-Ban 60 Minutes</td>
	</tr>
	<tr>
		<td><b>6</b></td>
		<td>Temp-Ban 60 Minutes</td>
	</tr>
	<tr>
		<td><b>7</b></td>
		<td>Temp-Ban 1 Week</td>
	</tr>
	<tr>
		<td><b>8</b></td>
		<td>Temp-Ban 1 Week</td>
	</tr>
	<tr>
		<td><b>9</b></td>
		<td>Perma-Ban</td>
	</tr>
	<tr>
		<td><b>Greater Than 9</b></td>
		<td>Perma-Ban</td>
	</tr>
</table>

ADK is rather lenient with
players, so there are 8 levels before a player is perma-banned in our version. Players may also be 'forgiven', which
will reduce their total point value by 1 each time, this is useful if you have a website where players can apologize
for their actions in-game.<br/><br/>

You can run multiple servers with this plugin on the same database, as long as you use different serverID's for each 
one in plugin settings. By setting each server to a different ID, rule breaking on one server won't cause increase in 
punishments on another server for that same player. If you want punishments to increase across all your servers, 
make the serverID the same for all. This is available since many groups run different rule sets on each server they 
own, so players breaking rules on one server may not know rules on another, they get a clean slate on each server.
<br/><br/>

If you have an external system (such as a web-based tool with access to bf3 server information), then have your 
external system update the record table with new commands to be acted on. Every 5-10 seconds the plugin checks for 
new input from external systems, and will act on them if found.<br/><br/>

When deciding to use this system, 'punish' should be the only command used for player rule-breaking. Other commands 
like kill, or kick are not counted since sometimes players ask to be killed, admins kill/kick themselves to leave 
games, or players get kicked for AFKing. Kill and kick should only be used for server management. Direct tban and ban 
are of course still available for hacker/glitching situations, but that is the ONLY time they should be used.<br/><br/>

When using the report system in tandem with this system, the report ID's that are generated can be used to reference 
players and reasons. Simply use that ID instead of a player-name and reason (e.g. waffleman73 baserapes, another player 
reports them and gets report ID 582, admins just use @punish 582 instead of @punish waffleman73 baserape). Confirmation 
of command with @yes is required before a report ID is acted on. Players are thanked for reporting when an admin uses 
their report ID.
</p>
<h3>Report/CallAdmin System</h3>
<p>
All uses of @report and @admin with this plugin require players to enter a reason, and will tell them if they haven't 
entered one. It will not send the report to admins unless it's done correctly. This cleans up what admins
end up seeing for reports (useful if admins get reports and admin calls whether they are in-game or not). When
a player puts in a proper @report or @admin all in-game admins are notified, then the report is logged in the
database with full player names for reporter/target, and the full reason for reporting.<br/><br/>

All reports are given a three digit ID which expires at the end of each round, these ID's can be used in the punish 
system to lighten the work admins do.
</p>
<h3>Player Muting</h3>
<p>
Players can be muted using the mute command, muting lasts until the end of the round. Players who talk in chat after 
being muted will be killed each time they talk (up through 5 chat messages), on the 6th message they are kicked from 
the server. No action other than kill or kick is used by this system. There will be no way to un-mute players, there 
was a reason they were muted, and they can talk again next round. Admins cannot mute other admins.
</p>
<h3>Pre-Yell and Pre-Say</h3>
<p>
A list of editable pre-defined messages can be added in settings, then admins can use the message ID instead of typing 
the whole message in. Example: @presay 2 will call the second pre-defined message, admin is asked to confirm the message 
with @yes to make sure it's the one they wanted.
</p>
<h3>TeamSwap</h3>
<p>
TeamSwap is a server-smart player moving system which offers two major benefits over the default system. Normally when 
trying to move a player to a full team the command just fails at the server level, now the player is dropped on a 
queue until a slot opens on that side. They can keep playing on their side until that slot opens, when it does they 
are immediately slain and moved over to fill it. Secondly it allows whitelisted (non-admin) players the ability to move 
themselves between teams as often as they want (within a ticket count window). This is currently not an available option 
in default battlefield aside from procon commands, the game limits players to one switch per gaming session. Whitelisted 
players can type '@moveme' and teamswap will queue them. This is meant to be available to players outside the admin 
list, usually by paid usage to your community or to clan members only. Admins can also use '@moveme', and in their 
case it bypasses the ticket window restriction.
</p>
<h3>Requiring Reasons</h3>
<p>
All commands which might lead to actions against players are required to have a reason entered, and will cancel if
no reason is given. Players (even the most atrocious in some cases) should know what they were acted on for.
</p>
<h3>Performance</h3>
<p>
AdKats is designed to be rather heavy when changing settings, but lighter when in use. All commands are stored in 
dictionaries so command meanings are parsed instantly when entered. During command parsing there is a hierarchy 
of checks the command goes through (specific to each command), if any of them fail the process ends immediately and 
informs the calling player of the error they made.
</p>
<h3>Available In-Game Commands</h3>
<p>
<u><b>You can edit the text typed for each command to suit your needs in plugin settings.</b></u> Usage of all
commands is database logged by default, but each command can be told whether to log or not. Logging all is useful 
especially when you have to hold 40+ admins accountable, and has not caused noticable lag.<br/><br/>
<table>
	<tr>
		<td><b>Command</b></td>
		<td><b>Params</b></td>
		<td><b>Access</b></td>
		<td><b>Description</b></td>
	</tr>
	<tr>
		<td><b>Kill Player</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for killing players.</td>
	</tr>
	<tr>
		<td><b>Kick Player</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for kicking players.</td>
	</tr>
	<tr>
		<td><b>Temp-Ban Player</b></td>
		<td>[minutes] [player] [reason]</td>
		<td>Admin</td>
		<td>The in-game command used temp-banning players.</td>
	</tr>
	<tr>
		<td><b>Perma-Ban Player</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for perma-banning players.</td>
	</tr>
	<tr>
		<td><b>Punish Player</b></td>
		<td>[player][reason]<br/>OR<br/>[reportID]</td>
		<td>Admin</td>
		<td>The in-game command used for punishing players. Will add a Punish record to the database, increasing a player's total points by 1. When a reportID is used as input, details of the report are given and confirmation (@yes) needs to be given before the punish is sent.</td>
	</tr>
	<tr>
		<td><b>Forgive Player</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for forgiving players. Will add a Forgive record to the database, decreasing a player's total points by 1.</td>
	</tr>
	<tr>
		<td><b>Mute Player</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for muting players. Players will be muted till the end of the round, 5 kills then kick if they keep talking. Admins cannot be muted.</td>
	</tr>
	<tr>
		<td><b>Move Player</b></td>
		<td>[player]</td>
		<td>Admin</td>
		<td>The in-game command used for moving players between teams. Will add players to a death move list, when they die they will be sent to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>Force-Move Player</b></td>
		<td>[player]</td>
		<td>Admin</td>
		<td>The in-game command used for force-moving players between teams. Will immediately send the given player to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>TeamSwap Player</b></td>
		<td>None</td>
		<td>Admin and TeamSwap Whitelist</td>
		<td>The in-game command used for moving yourself between teams. Will immediately send the speaker to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>Round Whitelist Player</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for round-whitelisting players. 2 players may be whitelisted per round. Once whitelisted they can use teamswap.</td>
	</tr>
	<tr>
		<td><b>Report Player</b></td>
		<td>[player][reason]</td>
		<td>All Players</td>
		<td>The in-game command used for reporting players. Must have a reason, and will inform a player otherwise when using. Will log a Report in the database(External GCP pulls from there for external admin notifications), and notify all in-game admins. Informs the reporter and admins of the report ID, which the punish system can use.</td>
	</tr>
	<tr>
		<td><b>Call Admin</b></td>
		<td>[player][reason]</td>
		<td>All Players</td>
		<td>The in-game command used for calling admin attention to a player. Same deal as report, but used for a different reason. Informs the reporter and admins of the report ID, which the punish system can use.</td>
	</tr>
	<tr>
		<td><b>Admin Say</b></td>
		<td>[message]</td>
		<td>Admin</td>
		<td>The in-game command used to send a message through admin chat.</td>
	</tr>
	<tr>
		<td><b>Admin Yell</b></td>
		<td>[message]</td>
		<td>Admin</td>
		<td>The in-game command used for to send a message through admin yell.</td>
	</tr>
	<tr>
		<td><b>Player Say</b></td>
		<td>[player][message]</td>
		<td>Admin</td>
		<td>The in-game command used for sending a message through admin chat to only a specific player.</td>
	</tr>
	<tr>
		<td><b>Player Yell</b></td>
		<td>[player][message]</td>
		<td>Admin</td>
		<td>The in-game command used for sending a message through admin yell to only a specific player.</td>
	</tr>
	<tr>
		<td><b>Pre-Say</b></td>
		<td>[message ID]</td>
		<td>Admin</td>
		<td>The in-game command used for sending a pre-defined message as an AdminSay.</td>
	</tr>
	<tr>
		<td><b>Pre-Yell</b></td>
		<td>[message ID]</td>
		<td>Admin</td>
		<td>The in-game command used for sending a pre-defined message as an AdminYell.</td>
	</tr>
	<tr>
		<td><b>Restart Level</b></td>
		<td>None</td>
		<td>Admin</td>
		<td>The in-game command used for restarting the round.</td>
	</tr>
	<tr>
		<td><b>Run Next Level</b></td>
		<td>None</td>
		<td>Admin</td>
		<td>The in-game command used for running the next map in current rotation, but keep all points and KDRs from this round.</td>
	</tr>
	<tr>
		<td><b>End Level</b></td>
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
* <b>'Debug level'</b> - Indicates how much debug-output is printed to the plugin-console. 0 turns off debug messages (just shows important warnings/exceptions), 6 documents nearly every step.
<h3>Admin Settings:</h3>
* <b>'Use Database Admin List'</b> - Whether to use list of admins from 'adminlist' table to cached admin list on plugin start. Admin names are cached in the plugin to save bandwidth. The list is updated when a non-admin is requesting access, to see if list has changed.<br/>
* <b>'Admin Table Name'</b> - Name of the database table that contains admin names. Default "tbl_adminlist". This table needs to be set up by groups manually right now, as they might already have an admin table.<br/>
* <b>'Column That Contains Admin Name'</b> - Name of the column in admin table that contains admin IGNs.<br/>
* <b>'Current Database Admin List'</b> - <b>(NOT EDITABLE)</b> When using the database admin list, this will show what players are currently admins.<br/>
* <b>'Static Admin List'</b> - List of admins input from plugin settings. Use if no admin database table.
<h3>Messaging Settings:</h3>
* <b>'Pre-Message List'</b> - List of messages to use for pre-say and pre-yell commands.
<h3>MySQL Settings:</h3>
* <b>'MySQL Hostname'</b> - Hostname of the MySQL server AdKats should connect to. <br/>
* <b>'MySQL Port'</b> - Port of the MySQL server AdKats should connect to, most of the time it's 3306. <br/>
* <b>'MySQL Database'</b> - Database name AdKats should use for storage. Hardcoded table names and creation scripts given below.<br/>
* <b>'MySQL Username'</b> - Username of the MySQL server AdKats should connect to. <br/>
* <b>'MySQL Password'</b> - Password of the MySQL server AdKats should connect to.
<h3>Command Settings:</h3>
* <b>'Minimum Required Reason Length'</b> - The minimum length a reason must be for commands that require a reason to execute.<br/>
* <b>'Yell display time seconds'</b> - The integer time in seconds that yell messages will be displayed.
  <br/><br/>
  <b>Specific command definitions given in description section above.</b> All command text must be a single string with no whitespace. E.G. kill. All commands can be suffixed with '|log', which will set whether use of that command is logged in the database or not.
<h3>Punishment Settings:</h3>
* <b>'Punishment Hierarchy'</b> - List of punishments in order from lightest to most severe. Index in list is the action taken at that number of points.<br/>
* <b>'Minimum Reason Length'</b> - The minimum number of characters a reason must be to call punish or forgive on a player.<br/>
* <b>'Only Kill Players when Server in low population'</b> - When server population is below 'Low Server Pop Value', only kill players, so server does not empty. Player points will be incremented normally.<br/>
* <b>'Low Server Pop Value'</b> - Number of players at which the server is deemed 'Low Population'.<br/>
* <b>'Punishment Timeout'</b> - A player cannot be punished more than once every x.xx minutes. This prevents multiple admins from punishing a player multiple times for the same infraction.
<h3>Server Settings:</h3>
* <b>'Server ID'</b> - ID that will be used to identify this server in the database. This is not linked to any specific database attributes, instead it's used to differentiate between servers for infraction points. Set all instances of this tool to the same server ID if you want points to work across servers.
<br/>
<h3>TeamSwap Settings:</h3>
* <b>'Require Whitelist for Access'</b> - Whether the 'moveme' command will require whitelisting. Admins are always allowed to use it.<br/>
* <b>'Static Player Whitelist'</b> - Static list of players plugin-side that will be able to TeamSwap.<br/>
* <b>'Use Database Whitelist'</b> - Whether to use 'adkat_teamswapwhitelist' table in the database for player whitelisting. Whitelisted names are cached in the plugin to save bandwidth. The list is updated when a non-whitelisted player is requesting access, to see if list has changed.<br/>
* <b>'Current Database Whitelist'</b> - <b>(NOT EDITABLE)</b> When using the database whitelist, this will show what players are currently whitelisted.<br/>
* <b>'Ticket Window High'</b> - When either team is above this ticket count, then nobody (except admins) will be able to use TeamSwap.<br/>
* <b>'Ticket Window Low'</b> - When either team is below this ticket count, then nobody (except admins) will be able to use TeamSwap.
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
The plugin checks the database for needed tables on connect. If it doesn't find the master record table it will run the script linked below. You can run the script beforehand if you dont want the plugin changing tables in your database.<br/>
<br/>
<a href="https://github.com/ColColonCleaner/AdKats/blob/master/adkats.sql" target="_blank">SQL Code</a>
</p>
