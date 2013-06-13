<h2 style="color:#FF0000;">Version 0.2.5.1 released! Your version is listed above (Procon Only).</h2>
New in version 0.2.5.1 at the bottom of this document. Download link available.
<h1>AdKats</h1>
<p>
A MySQL reflected admin toolset that includes editable in-game commands, an out-of-game controller, database 
reflected punishment and forgiveness, proper player report and admin call handling, player name completion, 
player muting, yell/say pre-recording, and internal implementation of TeamSwap.<br/><br/>

Visit the tool's github page 
<a href="https://github.com/ColColonCleaner/AdKats/" target="_blank">here</a> 
to submit bugs or wanted features.<br/><br/>

Download the latest version here: 
<a href="http://sourceforge.net/projects/adkats/files/AdKats_v0.2.5.1.zip/download" target="_blank">Version 2.5.1</a>
</p>
<h2>Features</h2>
<h3>Main</h3>
<p>
This tool was designed for use by groups with high traffic servers and many admins, but will function just as well 
for small servers.
</p>
<h3>Punishment/Forgiveness System</h3>
<p>
<b>NOTE:</b> This is NOT the player-based punish/forgive system normally used for teamkilling, and is only usable by
admins.<br/>
<br/>
<B>TLDR:</b> Use of punish and forgive commands takes the load off admins remembering what players have broken 
server rules, and how many times. Each time a player is punished it's logged in the database, and the more punishes 
they get the more  severe the punishment. Available punishments include kill, kick, temp-ban 60 minutes, temp-ban 1 
day, temp-ban 1 week, temp-ban 2 weeks, temp-ban 1 month, and permaban. Order that the punishments are given can be 
configured to your needs.<br/>
<br/>
After a player is 'punished' (and the Punish log is made in the database), their total points are calculated using 
this basic formula:<br/>
<b>(Punishment Count - Forgiveness Count = Total Points)</b></center><br/>
Then an action is decided using total points from the punishment hierarchy. Punishments should get more harsh as the
player gets more points. A player cannot be punished more than once every 20 seconds, this prevents multiple admins from 
accidentally punishing a player multiple times for the same thing. When a player is punished, and has already been 
punished in the past 5 minutes, the new punish counts for 2 points instead of 1, as the player is immediately breaking 
server rules after being punished. The punishment hierarchy is configurable to suit your needs, but the default is 
below.<br/>

<table>
	<tr>
		<td><b>Total Points</b></td>
		<td><b>Punishment Outcome</b></td>
		<td><b>Hierarchy String</b></td>
	</tr>
	<tr>
		<td><b>Less than 1</b></td>
		<td>Kill</td>
		<td>kill</td>
	</tr>
	<tr>
		<td><b>1</b></td>
		<td>Kill</td>
		<td>kill</td>
	</tr>
	<tr>
		<td><b>2</b></td>
		<td>Kill</td>
		<td>kill</td>
	</tr>
	<tr>
		<td><b>3</b></td>
		<td>Kick</td>
		<td>kick</td>
	</tr>
	<tr>
		<td><b>4</b></td>
		<td>Temp-Ban 60 Minutes</td>
		<td>tban60</td>
	</tr>
	<tr>
		<td><b>5</b></td>
		<td>Temp-Ban 1 Day</td>
		<td>tbanday</td>
	</tr>
	<tr>
		<td><b>6</b></td>
		<td>Temp-Ban 1 Week</td>
		<td>tbanweek</td>
	</tr>
	<tr>
		<td><b>7</b></td>
		<td>Temp-Ban 2 Weeks</td>
		<td>tban2weeks</td>
	</tr>
	<tr>
		<td><b>8</b></td>
		<td>Temp-Ban 1 Month</td>
		<td>tbanmonth</td>
	</tr>
	<tr>
		<td><b>9</b></td>
		<td>Perma-Ban</td>
		<td>ban</td>
	</tr>
	<tr>
		<td><b>Greater Than 9</b></td>
		<td>Perma-Ban</td>
		<td>ban</td>
	</tr>
</table>

Players may also be 'forgiven', which will reduce their total point value by 1 each time, this is useful if you have a
website where players can apologize for their actions in-game. Players can be forgiven into negative total point values 
which is why a 'less than 1' clause is needed. <br/><br/>

You can run multiple servers with this plugin on the same database, just use different serverIDs for each 
one in plugin settings. If you want punishments to increase on this server when infractions are commited on others set 
"Combine Server Punishments" to true. Rule breaking on another server won't cause increase in punishments on the current 
server if "Combine Server Punishments" is false. This is available since many groups run different rule sets on each 
server they own, so players breaking rules on one server may not know rules on another, so they get a clean slate.
<br/><br/>

When deciding to use this system, 'punish' should be the only command used for player rule-breaking. Other commands 
like kill, or kick are not counted in the system since sometimes players ask to be killed, admins kill/kick themselves, 
or players get kicked for AFKing. Kill and kick should only be used for server management. Direct tban 
and ban are of course still available for hacker/glitching situations, but that is the ONLY time they should be used.
</p>
<h3>Report/CallAdmin System</h3>
<p>
When a player puts in a proper @report or @admin all in-game admins are notified, then the report is logged in the
database with full player names for reporter/target, and the full reason for reporting. All uses of @report and 
@admin with this plugin require players to enter a reason, and will tell them if they haven't entered one. It will 
not send the report to admins unless reports are complete. This cleans up what admins end up seeing for reports 
(useful if admins get reports and admin calls whether they are in-game or not).
<br/>
<h4>Using Report IDs</h4>
All reports are issued a random three digit ID which expires either at the end of each round, or when it is used. These ID's can be used in any 
other action command, simply use that ID instead of a player-name and reason (e.g. waffleman73 baserapes, another player 
reports them and gets report ID 582, admins just use @punish 582 instead of @punish waffleman73 baserape). Confirmation 
of command with @yes is required before a report ID is acted on. Players are thanked for reporting when an admin uses 
their report ID.
</p>
<h3>Admin Assistants</h3>
<p>
When a player sends a report, then an admin uses that report by ID, it is considered a "good" report. When a player 
has X good reports in the past week a small bonus is given, access to teamswap. When a player gets access it simply 
tells them "For your consistent player reporting you now have access to TeamSwap. Type @moveme to swap 
between teams as often as you want." They do not know they are considered an admin assistant, only that they have access to that. The 
assistant list is recalculated at the beginning of each round. They need to keep that report count up to keep access, 
if they have less than the required amount they are automatically removed.<br/><br/>

When an admin assistant sends a report, to the admins that report is prefixed with [AA] to note it as a (most likely) 
reliable report. Whether admin assistants get the teamswap perk can be disabled, but the prefixes admins see will remain.
</p>
<h3>Player Muting</h3>
<p>
Players can be muted using the mute command, muting lasts until the end of the round. Players who talk in chat after 
being muted will be killed each time they talk (up through 5 chat messages), on the 6th message they are kicked from 
the server. No action other than kill or kick is used by this system. There will be no way to un-mute players, there 
was a reason they were muted, and they can talk again next round. Admins cannot mute other admins.
</p>
<h3>Pre-Messaging</h3>
<p>
A list of editable pre-defined messages can be added in settings, then admins can use the message ID instead of typing 
the whole message in. Example: @say 2 will call the second pre-defined message.<br/><br/>

Use @whatis [preMessageID] to find out what a particular ID links to before using it in commands.<br/><br/>

<b>Anywhere a reason or message is needed, a preMessage ID can be used instead.</b>
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
This plugin has been tested on a 64 player Operation Metro server that is full nearly 24/7 and does not cause any 
noticable lag. AdKats is designed to be rather heavy when changing settings, but much lighter when in use. All commands 
are stored in dictionaries so command meanings are parsed instantly when entered. During command parsing there is a 
hierarchy of checks the command goes through (specific to each command), if any of them fail the process ends 
immediately and informs the calling player of the error they made. Database connections are fast and do not cause any 
noticable lag even when logging everything on a very active server.
</p>
<h3>Database Usage</h3>
<p>
You must use an online MySQL database accessible from your procon layer. If you have your own website you can make one 
there, or you can use an online service. My clan runs our own, but I found this online one to be ok, and has a free 
usage option. http://www.freesqldatabase.com/ But any online accessible MySql database will work. Be careful with that 
free option though, the size is limited, and these things can log A LOT of data if it's an active server. When I first 
activated it on a busy metro server we had over 1500 records in just a few days of use.

The plugin checks the database for needed tables on connect. If it doesn't find the proper tables/views it will run 
the script linked below. You can run the script beforehand if you dont want the plugin changing table structure in 
your database.<br/>
<br/>
<a href="https://github.com/ColColonCleaner/AdKats/blob/master/adkats.sql" target="_blank">SQL Code</a>
</p>
<h3>Available In-Game Commands</h3>
<p>
<u><b>You can edit the text typed for each command to suit your needs in plugin settings.</b></u> Usage of all
commands is database logged by default, but each command can be told whether to log or not. Logging all is useful 
especially when you have to hold 40+ admins accountable, and has not caused any noticable lag.<br/><br/>
<table>
	<tr>
		<td><b>Command</b></td>
		<td><b>Params</b></td>
		<td><b>Description</b></td>
	</tr>
	<tr>
		<td><b>Kill Player</b></td>
		<td>[player][reason]<br/>OR<br/>[reportID]<br/>OR<br/>[reportID][reason]</td>
		<td>The in-game command used for killing players.</td>
	</tr>
	<tr>
		<td><b>Kick Player</b></td>
		<td>[player][reason]<br/>OR<br/>[reportID]<br/>OR<br/>[reportID][reason]</td>
		<td>The in-game command used for kicking players.</td>
	</tr>
	<tr>
		<td><b>Temp-Ban Player</b></td>
		<td>[minutes][player][reason]<br/>OR<br/>[minutes][reportID]<br/>OR<br/>[minutes][reportID][reason]</td>
		<td>The in-game command used temp-banning players.</td>
	</tr>
	<tr>
		<td><b>Perma-Ban Player</b></td>
		<td>[player][reason]<br/>OR<br/>[reportID]<br/>OR<br/>[reportID][reason]</td>
		<td>The in-game command used for perma-banning players.</td>
	</tr>
	<tr>
		<td><b>Punish Player</b></td>
		<td>[player][reason]<br/>OR<br/>[reportID]<br/>OR<br/>[reportID][reason]</td>
		<td>The in-game command used for punishing players. Will add a Punish record to the database, increasing a player's total points by 1. When a reportID is used as input, details of the report are given and confirmation (@yes) needs to be given before the punish is sent.</td>
	</tr>
	<tr>
		<td><b>Forgive Player</b></td>
		<td>[player][reason]<br/>OR<br/>[reportID]<br/>OR<br/>[reportID][reason]</td>
		<td>The in-game command used for forgiving players. Will add a Forgive record to the database, decreasing a player's total points by 1.</td>
	</tr>
	<tr>
		<td><b>Mute Player</b></td>
		<td>[player][reason]<br/>OR<br/>[reportID]<br/>OR<br/>[reportID][reason]</td>
		<td>The in-game command used for muting players. Players will be muted till the end of the round, 5 kills then kick if they keep talking. Admins cannot be muted.</td>
	</tr>
	<tr>
		<td><b>Move Player</b></td>
		<td>[player]<br/>OR<br/>[reportID]</td>
		<td>The in-game command used for moving players between teams. Will add players to a death move list, when they die they will be sent to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>Force-Move Player</b></td>
		<td>[player]<br/>OR<br/>[reportID]</td>
		<td>The in-game command used for force-moving players between teams. Will immediately send the given player to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>TeamSwap Self</b></td>
		<td>None</td>
		<td>The in-game command used for moving yourself between teams. Will immediately send the speaker to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>Round Whitelist Player</b></td>
		<td>[player][reason]<br/>OR<br/>[reportID]<br/>OR<br/>[reportID][reason]</td>
		<td>The in-game command used for round-whitelisting players. 2 players may be whitelisted per round. Once whitelisted they can use teamswap.</td>
	</tr>
	<tr>
		<td><b>Report Player</b></td>
		<td>[player][reason]</td>
		<td>The in-game command used for reporting players. Must have a reason, and will inform a player otherwise when using. Will log a Report in the database(External GCP pulls from there for external admin notifications), and notify all in-game admins. Informs the reporter and admins of the report ID, which the punish system can use.</td>
	</tr>
	<tr>
		<td><b>Call Admin</b></td>
		<td>[player][reason]</td>
		<td>The in-game command used for calling admin attention to a player. Same deal as report, but used for a different reason. Informs the reporter and admins of the report ID, which the punish system can use.</td>
	</tr>
	<tr>
		<td><b>Admin Say</b></td>
		<td>[message]<br/>OR<br/>[preMessageID]</td>
		<td>The in-game command used to send a message through admin chat to the whole server.</td>
	</tr>
	<tr>
		<td><b>Admin Yell</b></td>
		<td>[message]<br/>OR<br/>[preMessageID]</td>
		<td>The in-game command used for to send a message through admin yell to the whole server.</td>
	</tr>
	<tr>
		<td><b>Player Say</b></td>
		<td>[player][message]<br/>OR<br/>[player][preMessageID]</td>
		<td>The in-game command used for sending a message through admin chat to only a specific player.</td>
	</tr>
	<tr>
		<td><b>Player Yell</b></td>
		<td>[player][message]<br/>OR<br/>[player][preMessageID]</td>
		<td>The in-game command used for sending a message through admin yell to only a specific player.</td>
	</tr>
	<tr>
		<td><b>What Is</b></td>
		<td>[preMessageID]</td>
		<td>The in-game command used for finding out what a particular preMessage ID links to.</td>
	</tr>
	<tr>
		<td><b>Restart Level</b></td>
		<td>None</td>
		<td>The in-game command used for restarting the round.</td>
	</tr>
	<tr>
		<td><b>Run Next Level</b></td>
		<td>None</td>
		<td>The in-game command used for running the next map in current rotation, but keep all points and KDRs from this round.</td>
	</tr>
	<tr>
		<td><b>End Level</b></td>
		<td>[US/RU]</td>
		<td>The in-game command used for ending the current round with a winning team. Either US or RU.</td>
	</tr>
	<tr>
		<td><b>Nuke Server</b></td>
		<td>[US/RU/ALL]</td>
		<td>The in-game command used for killing all players on a team. US, RU, or ALL will work.</td>
	</tr>
	<tr>
		<td><b>Kick All Players</b></td>
		<td>[none]</td>
		<td>The in-game command used for kicking all players except admins.</td>
	</tr>
	<tr>
		<td><b>Confirm Command</b></td>
		<td>None</td>
		<td>The in-game command used for confirming other commands when needed.</td>
	</tr>
	<tr>
		<td><b>Cancel Command</b></td>
		<td>None</td>
		<td>The in-game command used to cancel other commands when needed.</td>
	</tr>
</table>
</p>
<h3>Command Access Levels</h3>
<p>
Players need to be at or above certain access levels to perform commands. Players on the access list can have their 
powers disabled (without removing them from the access list) by lowering their access level. Players can be added to
or removed from the access from plugin settings using the "Add Access" and "Remove Access" text fields. The access 
level of a player can be changed once they are on the access list by modifying the number to the right of their name. 
All players are defaulted to level 6 in the system, and have no special access, level 0 is a full admin.
<br/>
<table>
	<tr>
		<td><b>Command</b></td>
		<td><b>Access Level</b></td>
	</tr>
	<tr>
		<td><b>Restart Level</b></td>
		<td>0</td>
	</tr>
	<tr>
		<td><b>Next Level</b></td>
		<td>0</td>
	</tr>
	<tr>
		<td><b>End Level</b></td>
		<td>0</td>
	</tr>
	<tr>
		<td><b>Nuke Server</b></td>
		<td>0</td>
	</tr>
	<tr>
		<td><b>Kick All</b></td>
		<td>0</td>
	</tr>
	<tr>
		<td><b>Permaban Player</b></td>
		<td>1</td>
	</tr>
	<tr>
		<td><b>Temp-Ban Player</b></td>
		<td>2</td>
	</tr>
	<tr>
		<td><b>Round Whitelist Player</b></td>
		<td>2</td>
	</tr>
	<tr>
		<td><b>Kill Player</b></td>
		<td>3</td>
	</tr>
	<tr>
		<td><b>Kick Player</b></td>
		<td>3</td>
	</tr>
	<tr>
		<td><b>Punish Player</b></td>
		<td>3</td>
	</tr>
	<tr>
		<td><b>Forgive Player</b></td>
		<td>3</td>
	</tr>
	<tr>
		<td><b>Mute Player</b></td>
		<td>3</td>
	</tr>
	<tr>
		<td><b>Move Player</b></td>
		<td>4</td>
	</tr>
	<tr>
		<td><b>Force-Move Player</b></td>
		<td>4</td>
	</tr>
	<tr>
		<td><b>Admin Say</b></td>
		<td>4</td>
	</tr>
	<tr>
		<td><b>Admin Yell</b></td>
		<td>4</td>
	</tr>
	<tr>
		<td><b>Player Say</b></td>
		<td>4</td>
	</tr>
	<tr>
		<td><b>Player Yell</b></td>
		<td>4</td>
	</tr>
	<tr>
		<td><b>TeamSwap</b></td>
		<td>5</td>
	</tr>
	<tr>
		<td><b>Report Player</b></td>
		<td>6</td>
	</tr>
	<tr>
		<td><b>Call Admin on Player</b></td>
		<td>6</td>
	</tr>
</table>
</p>
<h3>Commanding AdKats from Outside the Game</h3>
<h4>AdKats WebAdmin, a tool for controlling your servers, issuing commands, and viewing logs from the comfort of a 
web browser is currently in production. It should be released along with version 3.0</h4>
<p>
If you have an external system (such as a web-based tool with access to bf3 server information), then there are two 
ways to interact with AdKats externally.<br/>

<h4>1. Procon HTTP Server</h4>
Send commands to AdKats through procon's internal HTTP server, it just takes a couple setup steps. Turn on procon's 
HTTP server by going to Tools (Upper right) --> Options --> HTTP Tab and enable the server. The default port should be 
fine for most cases. Then in AdKats settings set the external access key to what you want for a password. Action taken 
is almost instant, and a helpful error message is given if incorrect params are entered. You can then query the plugin 
in following manner.<br/><br/>

<b>The BF3 server ip and port are shown in AdKats settings.</b><br/>
http://[procon_server_ip]:[server_port]/[bf3_server_ip]:[bf3_server_port]/plugins/AdKats/?[Parameters]<br/><br/>

<b>Required Parameters:</b><br/>
command_type=[Command from list Below]<br/>
target_name=[Full or Partial Player Name]<br/>
record_message=[reason for action]<br/>
access_key=[Your Access Key from AdKats Settings]<br/>
<b>Optional Parameters:</b><br/>
source_name=[Name of admin sending this command. This system has access level 1 (full admin) regardless of this entry.]<br/>
record_durationMinutes=[Used for Temp-Bans, duration of time in minutes]<br/><br/>

<b>Example of Command:</b><br/>
http://293.182.39.230:27360/173.199.91.187:25210/plugins/AdKats/?command_type=TempBan&source_name=ColonsEnemy&<br/>
target_name=ColColonCleaner&record_message=Testing&record_durationMinutes=60&access_key=MyPassword<br/><br/>

<b>SECURITY NOTE: As some will notice this works through GET commands, which are insecure, if you issue commands and 
someone outside gets the command URL you entered they will have full access to issue commands on your instance of this 
plugin. It is only insecure if you use this method though, as internally the key is randomized. I am working on securing 
this system, as it is ultimately better than option 2.</b></b>

<h4>2. Adding Database Records</h4>
Have your external system add a row to the record table with a new record to be acted on. All information is needed 
in the row just like the ones sent from AdKats to the database. Just make the 'adkats_read' column for that row = "N" 
and adkats will act on that record. Every ~20 seconds the plugin checks for new input in the table, and will 
act on them if found, this is a much slower acting system than the HTTP server option, but is much MUCH more secure.<br/>

Valid 'command_type's that can be acted on include the following:<br/>
<table>
	<tr>
		<td><b>Action To be Performed</b></td>
		<td><b>command_type</b></td>
	</tr>
	<tr>
		<td><b>Move Player</b></td>
		<td>Move</td>
	</tr>
	<tr>
		<td><b>Force-Move Player</b></td>
		<td>ForceMove</td>
	</tr>
	<tr>
		<td><b>Kill Player</b></td>
		<td>Kill</td>
	</tr>
	<tr>
		<td><b>Kick Player</b></td>
		<td>Kick</td>
	</tr>
	<tr>
		<td><b>Temp-Ban Player</b></td>
		<td>TempBan</td>
	</tr>
	<tr>
		<td><b>Permaban Player</b></td>
		<td>PermaBan</td>
	</tr>
	<tr>
		<td><b>Punish Player</b></td>
		<td>Punish</td>
	</tr>
	<tr>
		<td><b>Forgive Player</b></td>
		<td>Forgive</td>
	</tr>
	<tr>
		<td><b>Mute Player</b></td>
		<td>Mute</td>
	</tr>
	<tr>
		<td><b>Round Whitelist Player</b></td>
		<td>RoundWhitelist</td>
	</tr>
	<tr>
		<td><b>Admin Say</b></td>
		<td>AdminSay</td>
	</tr>
	<tr>
		<td><b>Player Say</b></td>
		<td>PlayerSay</td>
	</tr>
	<tr>
		<td><b>Admin Yell</b></td>
		<td>AdminYell</td>
	</tr>
	<tr>
		<td><b>Player Yell</b></td>
		<td>PlayerYell</td>
	</tr>
	<tr>
		<td><b>Restart Level</b></td>
		<td>RestartLevel</td>
	</tr>
	<tr>
		<td><b>Next Level</b></td>
		<td>NextLevel</td>
	</tr>
	<tr>
		<td><b>End Level</b></td>
		<td>EndLevel</td>
	</tr>
	<tr>
		<td><b>Nuke Server</b></td>
		<td>Nuke</td>
	</tr>
	<tr>
		<td><b>Kick All Players</b></td>
		<td>KickAll</td>
	</tr>
</table>
</p>
<h2>Settings</h2>
<h3>1. Server Settings:</h3>
<ul>
  <li><b>'Server ID'</b> - Value used to identify this server in the database. Must be different from all other servers you run.</li>
  <li><b>'Server IP'</b> - IP address of this server, just a display for your benefit.<br/></li>
</ul>
<h3>2. MySQL Settings:</h3>
<ul>
  <li><b>'MySQL Hostname'</b> - Hostname of the MySQL server AdKats should connect to.</li>
  <li><b>'MySQL Port'</b> - Port of the MySQL server AdKats should connect to, most of the time it's 3306.</li>
  <li><b>'MySQL Database'</b> - Database name AdKats should use for storage. Creation script given in database section.</li>
  <li><b>'MySQL Username'</b> - Username of the MySQL server AdKats should connect to.</li>
  <li><b>'MySQL Password'</b> - Password of the MySQL server AdKats should connect to.</li>
</ul>
<h3>3. Player Access Settings:</h3>
<ul>
  <li><b>'Add Access'</b> - Add a player to the access list by entering their exact IGN here.<br/></li>
  <li><b>'Remove Access'</b> - Remove a player already on the access list by typing their exact IGN here.<br/></li>
  <li><b>*PlayerName*</b> - Players in the current database access list are appeneded here with their access level.</li>
</ul> 
<h3>4. In-Game Command Settings:</h3>
<ul>
  <li><b>'Minimum Required Reason Length'</b> - The minimum length a reason must be for commands that require a reason to execute.</li>
</ul>
<b>Specific command definitions given in features section above.</b> All command text must be a single string with no whitespace. E.G. kill. All commands can be suffixed with '|log', which will set whether use of that command is logged in the database or not.
<h3>5. Punishment Settings:</h3>
<ul>
  <li><b>'Punishment Hierarchy'</b> - List of punishments in order from lightest to most severe. Index in list is the action taken at that number of points.</li>
  <li><b>'Combine Server Punishments'</b> - Whether to make punishes from all servers on this database affect players on this server. Default is false.</li>
  <li><b>'Only Kill Players when Server in low population'</b> - When server population is below 'Low Server Pop Value', only kill players, so server does not empty. Player points will be incremented normally.</li>
  <li><b>'Low Server Pop Value'</b> - Number of players at which the server is deemed 'Low Population'.</li>
</ul>
<h3>6. Email Settings:</h3>
<ul>
  <li><b>'Email and RSS handling coming soon.'</b></li>
</ul>
<h3>7. TeamSwap Settings:</h3>
<ul>
  <li><b>'Require Whitelist for Access'</b> - Whether the 'moveme' command will require whitelisting. Admins are always allowed to use it. Default False.</li>
  <li><b>'Auto-Whitelist Count'</b> - At the start of each round, X random players will be whitelisted for teamswap during that round. At the end of the round they lose their whitelisting. Use to get players interested in permanent whitelisting.</li>
  <li><b>'Ticket Window High'</b> - When either team is above this ticket count, nobody (except admins) will be able to use TeamSwap.</li>
  <li><b>'Ticket Window Low'</b> - When either team is below this ticket count, nobody (except admins) will be able to use TeamSwap.</li>
</ul>
<h3>8. Admin Assistant Settings:</h3>
<ul>
  <li><b>'Enable Admin Assistant Perk'</b> - Whether admin assistants will get a perk for their help.</li>
  <li><b>'Minimum Confirmed Reports Per Week'</b> - How many confirmed reports the player must have in the past week to be an admin assistant.</li>
</ul>
<h3>9. Player Mute Settings:</h3>
<ul>
  <li><b>'On-Player-Muted Message'</b> - The message given to players when they are muted by an admin.</li>
  <li><b>'On-Player-Killed Message'</b> - The message given to players when they are killed for talking in chat after muting.</li>
  <li><b>'On-Player-Kicked Message'</b> - The message given to players when they are kicked for talking more than X times in chat after muting.</li>
  <li><b>'# Chances to give player before kicking'</b> - The number of chances players get to talk after being muted before they are kicked. After testing, 5 appears to be the perfect number, but change as desired.</li>
</ul>
<h3>A10. Messaging Settings:</h3>
<ul>
  <li><b>'Yell display time seconds'</b> - The integer time in seconds that yell messages will be displayed.</li>
  <li><b>'Pre-Message List'</b> - List of messages for use in pre-say and pre-yell commands.</li>
</ul>
<h3>A11. Banning Settings:</h3>
<ul>
  <li><b>'Ban Type'</b> - The ban type that should be used when banning players, Frostbite - GUID is advised as that is the most reliable.</li>
  <li><b>'Use Additional Ban Message'</b> - Whether to have an additional message append on each ban.</li>
  <li><b>'Additional Ban Message'</b> - Additional ban message to append on each ban. e.g. "Dispute at www.yourclansite.com"</li>
</ul>
<h3>A12. HTTP Command Settings:</h3>
<ul>
  <li><b>'External Access Key'</b> - The access key required to use any HTTP commands, can be changed to whatever is desired, but the default is a random 64Bit hashcode generated when the plugin first runs.<br/></li>
</ul>
<h3>A13. Debug Settings:</h3>
<ul>
  <li><b>'Debug level'</b> - Indicates how much debug-output is printed to the plugin-console. 0 turns off debug messages (just shows important warnings/exceptions), 6 documents nearly every step. Don't edit unless you really wanna be spammed with console logs, it will also slow down the plugin when turned up.</li>
  <li><b>'Command Entry'</b> - Enter commands here just like in game, mainly for debug purposes. Don't let more than one person use this at any time.</li>
</ul>
<h2>New in Version 0.2.5.0</h3>
<p>
<ul>
  <li><b>Admin List GUI.</b> You can now modify the database reflected admin list through AdKats 
  settings. You can edit who the admins are, and what level of access they have, without needing to access the database 
  manually. All instances of the plugin on your database will reflect the admins you enter.</li>
  <li><b>Admins now have multiple levels of access.</b> They range from full admin (0) to normal player (6). List of 
  commands for each level is given below. Admins can issue commands at or below their level. All commands on an admin's 
  access level can be used on other admins of any level with the exception of muting.</li>
  <li><b>Commands now have levels of access.</b> Admins need to be at or above certain levels of access to use certain 
  commands.</li>
  <li><b>HTTP Server Online.</b> Commands can now be sent to AdKats using procon's internal HTTP server, or through the 
  database. Info given below on security of this system.</li>
  <li><b>Player name suggestion system improved.</b> System now considers player names starting with what was typed more 
  correct than those with it just somewhere in their name. System will also perform a "fuzzy" player-name search if the 
  text admins entered is not valid for any players.</li>
  <li><b>Ghost Commands Fixed.</b> Commands admins send but don't confirm will be auto-canceled if they move on to other 
  things. This stops unwanted commands from being acted on after the fact.</li>
  <li><b>TeamSwap can now auto-whitelist X random players in the server each round.</b> The random list is changed each 
  round. Use this to generate hype for players to get full access to teamswap. Players are told the first time they 
  spawn that they have access. Players who already have access are not added to the auto-whitelist.</li>
  <li><b>Player report logging improved.</b> Whether a report was used by an admin is now logged.</li>
  <li><b>"Admin Assistant" position added.</b> Players who consistently send useful player reports get a small bonus. 
  Details below. This can be disabled.</li>
  <li><b>Round Report Handling Improved.</b> Handling changed so admins can enter new reasons that override the report 
  reason. The new reason entered will be used instead, and must follow the requirements for a reason defined in 
  settings.</li>
  <li><b>Pre-defined messages usable in all commands.</b> All player interaction commands (not say or yell), will 
  accept preMessageIDs as input for reasons now. e.g. @kill charlietheunicorn 4 --> 
  charlietheunicorn killed for Baseraping Enemy Spawn Area.</li>
  <li><b>Server IDs can be different now, yet still have punishments increase across servers.</b> Now the origin of 
  reports wont show as coming from the same server, since same server ID was required before for global punishments.</li>
  <li><b>Added new commands.</b> Kick all Players, and Nuke Server.</li>
  <li><b>Commands can now operate in shortened hidden mode.</b> When commands are issued in hidden mode they normally 
  require an extra character. e.g. /@kill target reason. They now work with just the slash. e.g. /kill target reason. 
  </li>
  <li><b>Commands will target the speaker when entered with no parameters.</b> Most player interaction commands will 
  now target the speaker when entered with no parameters. So "@kill" == "@kill SourcePlayerName Self-Inflicted". Report 
  and call admin will not do this, in addition to commands meant for targeting multiple players.</li>
  <li><b>Additional ban message option added.</b> e.g. Optionally add "appeal at www.yoursite.com" to the end of bans.</li>
  <li><b>30 seconds now hardcoded as punishment timeout.</b> Setting was only editable for testing purposes.</li>
  <li><b>Optimizations in code, database, and settings handling.</b></li>
</ul>
</p>
