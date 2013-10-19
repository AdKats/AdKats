<h2>Development</h2>
<p>
Started by ColColonCleaner for A Different Kind (ADK) on Apr. 20, 2013
</p>
<h3>Changelog</h3>
<blockquote>
<h4>0.0.1 (20-APR-2013)</h4>
<b>Main: </b> <br/>
    * Initial Version <br/>
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
    * Create database whitelist definitions for TeamSwap.<br/>
    * Add move, fmove, moveme as commands that use TeamSwap.<br/>
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
    * Fixed move command, now sends player to TeamSwap once they have died.<br/>
<h4>0.1.4 (10-MAY-2013)</h4>
<b>Changes</b> <br/>
    * Fixed bugs in command logging interface and command setting initialization.<br/>
<h4>0.1.5 (12-MAY-2013)</h4>
<b>Changes</b> <br/>
    * Cleaned up messaging. Small bug fixes.<br/>
<h4>0.1.6 (14-MAY-2013)</h4>
<b>Changes</b> <br/>
    * Optimized calling of listPlayers to a maximum of only once every 5 seconds or on call from a move command.<br/>
    * Fixed console spam at start of plugin.<br/>
    * Added update of admin list/teamswap list if a player isn't on it and trying a command.<br/>
    * Gave plugin control over table creation if not setup beforehand.<br/>
<h4>0.1.7 (15-MAY-2013)</h4>
<b>Changes</b> <br/>
    * Reconfigured Database connection handling and connection testing to follow best practices seen elsewhere.<br/>
    * Fixed bugs in the database structure confirmation and table setup sequence.<br/>
    * All yell messages will now be changed to uppercase before sending.<br/>
    * Added confirm action to all round targeted commands.<br/>
<h4>0.1.7.2 (16-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Console Errors displayed when players enter invalid settings made more descriptive.<br/>
   * Added presay and preyell commands.<br/>
<h4>0.1.7.3 (17-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Made plugin description download github stored README and CHANGELOG instead of storing it plugin-side.<br/>
<h4>0.1.8.0 (18-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Improve the player name prediction system.<br/>
<h4>0.1.9.0 (19-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Add a report ID to reports, so admins can act on reports directly.<br/>
   * Add a thank you for reporting when a report gets acted on.<br/>
<h4>0.1.9.1 (20-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Added banning admin name to all kick/ban logs.<br/>
   * Fixed bug in logging when commands were given in uppercase.<br/>
<h4>0.1.9.2 (20-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Added player mute system.<br/>
   * Added global adminsay when a player gets kicked or banned by an admin.<br/>
<h4>0.1.9.4 (20-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Added round-whitelisting for players.<br/>
<h4>0.1.9.9 (21-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Removed actionlist, made entire record table two-way accessible.<br/>
   * Added report ID usage to all player interaction commands.<br/>
   * Added display of current database admins and whitelisted players in plugin settings.<br/>
   * Added pyell to dev for plugin information in certain cases.<br/>
   * All action commands can be called via the database now.<br/>
<h4>0.2.0.0 (21-MAY-2013)</h4>
<b>Changes</b> <br/>
   * Minor bug fixes for version 2 release.<br/>
<h4>0.2.5.0 (4-JUNE-2013)</h4>
<b>Changes</b> <br/>
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
<h4>0.2.5.1 (6-JUNE-2013)</h4>
<b>Changes</b> <br/>
<ul>
  <li><b>Bug-fixes.</b> Some documented bugs in the issues section (milestone 0.2.5.1) are fixed.</li>
  <li><b>Punishment Enhancements.</b> Punishment timout has been reduced to 20 seconds.</li>
  <li><b>IRO Punishments Added.</b> Immediate Repeat Offence. If a player gets punished more than once in a 5 minute 
  time span, the subsequent punishes were be worth 2 infraction points instead of just 1.</li>
  <li><b>Messaging Enhancements.</b> Pre-say and pre-yell commands have been removed, and now the preMessage IDs can 
  be used in regular say, yell, and any other commands that need a reason or message.</li>
  <li><b>Pre-Message Enhancements.</b> Use of pre-defined messages can be required now.</li>
</ul>
<h4>0.3.0.0 (13-JULY-2013)</h4>
<b>Changes</b> <br/>
<ul>
  <li><b>Performance.</b> All actions, parsing, and database communications take place on their own threads now, 
  increasing performance greatly.</li>
  <li><b>Setting Sync.</b> All plugin settings are now stored in the database, specific to each procon instance. 
  Usage shown in readme.</li>
  <li><b>Dependencies.</b> XPKiller's Stat logger is now REQUIRED for AdKats to function. It provides much useful 
  information regarding both player and server statistics, which the new Ban Enforcer and AdKats itself use to improve 
  your admin abilities.</li>
  <li><b>AdKats WebAdmin API.</b> A website from which you can manage all aspects of your server and playerbase. 
  Direct control of players within the server, command feeds, all logs, ban management, and server statistics are all 
  included in this site.</li>
  <li><b>AdKats Ban Enforcer.</b> Due to lacking functionality and/or bugs in other ban managers, an internal Ban 
  Enforcer is now coded into AdKats. AdKats can now enforce bans accross all of your servers. The Ban Enforcer will 
  import and consolidate all bans from every procon instance it's enabled on. Once enabled, bans made on one of your 
  servers will be enforced on all others within seconds of issuing the ban. Bans can be made by name, GUID, IP, any 
  combination, or all at once. The enforcer works with all existing auto-admins, and requires AdKats WebAdmin for 
  ban management. You can use it without WebAdmin, but you will be unable to manage any bans, lift them early, or modify 
  them in any way once submitted. Use of the ban enforcer is optional because of this dependency, and is disabled by 
  default.</li>
  <li><b>Punishment Enhancements.</b> IRO punishments can now override the low population count and act normally.</li>
  <li><b>Kick/Ban Messages Improved.</b> Frostbite has a 80 character limit for ban/kick messages, and the new ban/kick 
  messages comply with that. Also, all kick/ban messages are more descriptive than before, and ban messages in 
  particular will tell the player how long their ban is. </li>
  <li><b>Small bug-fixes and enhancements.</b> Messages for errors, player information, and commands, are more 
  informative to the users now. Small bugs fixed.</li>
  <li><b>Debug Soldier Added.</b> Set the debug soldier name in settings to get the speed of commands on your server 
  sent to you in-game. Time is in milliseconds, from the time you entered it until all actions resulting from that 
  command have finished.</li>
</ul>
<h4>0.3.0.1 (3-AUG-2013)</h4>
<b>Changes</b> <br/>
<ul>
  <li><b>Bug-fixes.</b> Documented bugs in the issues section (milestone 0.3.0.1) are fixed.</li>
  <li><b>Added delayed kill.</b> When a player dies and is then admin killed, kill will be performed when they spawn.</li>
  <li><b>Ban-Sync process revamped.</b> All ban enforcer sync methods have been reworked to be more reliable and 
  efficient.</li>
  <li><b>Admin list sorted by level then name.</b> Simple visual fix.</li>
</ul>
<h4>0.3.0.2(6-SEP-2013)</h4>
<b>Changes</b> <br/>
<ul>
  <li><b>Bug-fixes/Enhancements</b> Documented bugs in the issues section (milestone 0.3.0.2) are fixed.</li>
  <li><b>Teamswap Queues Removed Temporarily.</b> Until the queuing problem can be fixed, queues for teamswap have been 
  removed. All moves, force-moves, and self-moves will be called directly.</li>
  <li><b>Import from BF3 Ban Manager Added.</b> When AdKats Ban Enforcer is enabled, a check for BF3 Ban Manager tables 
  is performed. All bans managed through BF3 Ban Manager will be automatically imported into AdKats.</li>
</ul>
<h4>0.3.0.3(8-SEP-2013)</h4>
<b>Changes</b> <br/>
<ul>
  <li><b>Bug-fixes/Enhancements</b> Small but extremely important bug fixed. Necessary for WebAdmin.</li>
</ul>
<h4>0.3.1.1(16-SEP-2013)</h4>
<b>Changes</b> <br/>
<ul>
  <li><b>Bug-fixes/Enhancements</b> Small bugs fixed. Some commands simplified. Necessary for WebAdmin.</li>
  <li><b>Performance Fix</b> IP checking was causing lag in the previous verion.</li>
</ul>
<h4>0.3.1.6(24-SEP-2013)</h4>
<b>Changes</b> <br/>
<ul>
  <li><b>Bug-fixes/Enhancements</b> Teamswap queues have been brought back after being fixed. Listplayers calls 
  shortened and only handled every 5 seconds, and ban enforcer enforcement should now cause less chat spam.</li>
  <li><b>Added join command</b> @|!|/ join playername will join on that player, with certain access levels.</li>
  <li><b>Punishment logging improved</b> # of player points is now stored in the record message for each punish.</li>
  <li><b>Permaban punish overrides low population</b>Used to not happen.</li>
  <li><b>Remove text to disable command</b>Command name will autofill with command disabled text when removed.</li>
</ul>
<h4>3.5.0.0(19-OCT-2013)</h4>
<b>Changes</b> <br/>
<ul>
  <li><b>Bug-fixes/Enhancements.</b> Record upload for certain commands optimized, kicks/bans on yourself no-longer 
  tell the whole server, auto-whitelisted players are logged, MutePlayer doesn't cause momentary disconnect, etc, etc, 
  full list on github.</li>
  <li><b>Beta Version of Stat Logger Required.</b> Version 1.1.0.2 of stat logger is required for this version of AdKats. 
  That version of stat logger has been included with this release.</li>
  <li><b>MULTIBalancer Orchestration.</b> All players access level 0-5 can be automatically whitelisted.</li>
  <li><b>Reserved Slot Orchestration.</b> All players on the access list can be automatically given reserved slots.</li>
  <li><b>Added VOIP Command.</b> Voip command can be used by all players to get teamspeak or other voip server info.</li>
  <li><b>User based kill command added.</b> Players can kill themselves with @killme. Disable by removing command text.</li>
  <li><b>Push errors to database.</b> All errors and exceptions are now pushed to the database for logging.</li>
  <li><b>Server Crash Reporter Added.</b> Server crash/Blaze DC reporter added (Only meaningful to those using webadmin).</li>
  <li><b>Admin Assistant Logic Changed.</b> Admin Assistants are now on a 30 day report calculation, instead of 7 days.</li>
  <li><b>Database connection backup added.</b> Commands will now still carry out their actions if the database 
  connection is temporarily lost. It will spam console errors like there's no tomorrow but will still act on commands.</li>
</ul>
</blockquote>
