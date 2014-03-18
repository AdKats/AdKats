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
<h4>0.3.0.2 (6-SEP-2013)</h4>
<b>Changes</b> <br/>
<ul>
    <li><b>Bug-fixes/Enhancements</b> Documented bugs in the issues section (milestone 0.3.0.2) are fixed.</li>
    <li><b>Teamswap Queues Removed Temporarily.</b> Until the queuing problem can be fixed, queues for teamswap have been
        removed. All moves, force-moves, and self-moves will be called directly.</li>
    <li><b>Import from BF3 Ban Manager Added.</b> When AdKats Ban Enforcer is enabled, a check for BF3 Ban Manager tables
        is performed. All bans managed through BF3 Ban Manager will be automatically imported into AdKats.</li>
</ul>
<h4>0.3.0.3 (8-SEP-2013)</h4>
<b>Changes</b> <br/>
<ul>
    <li><b>Bug-fixes/Enhancements</b> Small but extremely important bug fixed. Necessary for WebAdmin.</li>
</ul>
<h4>0.3.1.1 (16-SEP-2013)</h4>
<b>Changes</b> <br/>
<ul>
    <li><b>Bug-fixes/Enhancements</b> Small bugs fixed. Some commands simplified. Necessary for WebAdmin.</li>
    <li><b>Performance Fix</b> IP checking was causing lag in the previous verion.</li>
</ul>
<h4>0.3.1.6 (24-SEP-2013)</h4>
<b>Changes</b> <br/>
<ul>
    <li><b>Bug-fixes/Enhancements</b> Teamswap queues have been brought back after being fixed. Listplayers calls
        shortened and only handled every 5 seconds, and ban enforcer enforcement should now cause less chat spam.</li>
    <li><b>Added join command</b> @|!|/ join playername will join on that player, with certain access levels.</li>
    <li><b>Punishment logging improved</b> # of player points is now stored in the record message for each punish.</li>
    <li><b>Permaban punish overrides low population</b>Used to not happen.</li>
    <li><b>Remove text to disable command</b>Command name will autofill with command disabled text when removed.</li>
</ul>
<h4>3.5.0.0 (19-OCT-2013)</h4>
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
<h4>3.7.0.0 (15-NOV-2013)</h4>
<b>Changes</b> <br/>
<ul>
    <li>Compatibility with BF4 and BF3 in the same version.</li>
    <li>Certain commands disabled on “Official” BF4 servers.</li>
    <li>Both plugin description fetching and player list processing are now being processed async to avoid procon
        panic.</li>
    <li>The universal version of XpKiller’s stat logger is now supported. Both versions of stat logger are supported.</li>
    <li>Source ID has been added to the records table. Records will remain connected to a player even if their name
        changes.</li>
    <li>Player ID is now added to all stored chatlogs. XpKillers chat log table is automatically modified to include
        player ID; Thus chat logging functions are taken over by AdKats and force disabled on stat logger.</li>
    <li>AdKats will enter backup mode if your database becomes unavailable temporarily, your connections max out, or it
        becomes overloaded. Stat logger will automatically be disabled if a disconnect is detected, and re-enabled once the
        connection issue has been resolved. This is to prevent procon from entering panic mode and ejecting either plugin
        from the runtime, requiring a layer restart to get them back.</li>
    <li>Experimental tools updated to work on BF3 and BF4</li>
    <li>Round timer added. Rounds can now be automatically ended after X minutes. Countdown to round end is given, and
        the current winning team will win.</li>
    <li>Internal Hacker-Checker released publicly for BF3. This code has been in AdKats for months now, but was set to
        only activate on =ADK= servers. Uses BF3Stats for stat information. Do not use unless you read the documentation on
        github <a href="https://github.com/ColColonCleaner/AdKats/blob/master/README.md#internal-hacker-checker-with-whitelist" target="_blank">(HERE)</a>.
        BF4 version coming soon. </li>
    <li>Round whitelisting of players is no-longer logged in the database</li>
</ul>
<h4>4.0.0.0 (21-DEC-2013)</h4>
<b>Changes</b> <br/>
<ul>
    <li>User/role based access system added, old access level concept removed.</li>
    <li>Added rule printing like "Server rules on request", with multiple prefixes and database log support.</li>
    <li>Email functionality added.</li>
    <li>IRO punishments now editable for both activation and timeout.</li>
    <li>Admin name can now optionally be inclided in global kick/ban admin.say messages.</li>
    <li>Admin name for procon bans now editable.</li>
    <li>Tell and Player-Tell commands added.</li>
    <li>Unban command added for use with ban enforcer.</li>
    <li>Added mini ban management section for use with ban enforcer.</li>
    <li>All AdKats functions now operation on UTC time.</li>
    <li>Optional Auto-Enable/Keep-Alive added to make sure the plugin is always running.</li>
    <li>Commands can now be accepted from procon's chat tab, and thus from other plugins.</li>
    <li>Internal hacker-checker updated to work with BF4.</li>
    <li>Ban reasons updated to look better with battlelog's new display.</li>
    <li>Added spectator list management for BF4.</li>
    <li>Database update script from 3.7 to 4.0 included. Will automatically run.</li>
    <li>Bug fixes from previous releases.</li>
</ul>
<h4>4.1.0.0 (14-FEB-2014)</h4>
<b>Changes</b><br/>
<ul>
    <li><b>TeamSwap</b> Auto-Whitelisting per round has been removed as an option.</li>
</ul>
<b>Enhancements</b><br/>
<ul>
    <li><b>Added @accept and @deny, for acting on round reports.</b> No actions against players will be taken when using these commands, they are for helping or hindering AA status for a player. A system coming currently under testing on our servers for automatic report actions will make use of this.</li>
    <li><b>Added @admins command</b> Accessible to to admins and admin assistants by default. Returns the list of online admins in the server.</li>
    <li><b>Added @lead command</b> Lead command will give the speaker leader of their current squad.</li>
    <li><b>Team Enhancements</b> Team names are now detected every match, so factions US, RU, and CN for nuke and kickall now hit the correct team.</li>
    <li><b>Command Enhancements</b> Target player names entered with 1 character will no longer be automatically acted on. Will require a confirm.</li>
    <li><b>Email Enhancements</b> All emails sent through AdKats are now sent as blind carbon copy. Emails are now added to a queue when multiple need to be sent in succession.</li>
    <li><b>List Enhancements</b> Added adkats_specialplayers database table. This table can be used for assigning special whitelists, access, and blacklists. Valid groups are currently slot_reserved, slot_spectator, whitelist_multibalancer, blacklist_dispersion, and whitelist_hackerchecker. Players can be added by ID, or by identifier (name, guid, or IP), and can be assigned, a game and server to apply the list to. If you use player IDs then you wont need to update player names if they change their names, the player names will automatically update when they join the server.</li>
    <li><b>Orchestration Enhancements</b> Default In-Game Admin is automatically disabled when AdKats is running. Had some issues where people accidentally had both plugins running.</li>
    <li><b>User Enhancements</b> Game type is now displayed for players connected to users. Add Soldier now checks across all games and fetches up to 10 matching player names. When adding a new user an automatic check for players of the same name is performed.</li>
    <li><b>Player Info Enhancements</b> All custom messages in AdKats, when targeted at a player, player information string replacements will be parsed. e.g. %player_name%, or %player_id%</li>
    <li><b>Admin Assistant Enhancements</b> Completely reconstructed system with previous private features released to the public. Also added grandfathering for Admin Assistants; >75 confirmed reports overall will also grant admin assistant status.</li>
    <li><b>MatchCommand Enhancements</b> AdKats is now callable from other plugins like InsaneLimits for issuing commands and fetching current synced admin list.</li>
    <li><b>Ban Enforcer Enhancements</b> All bans are now enforced on a server group basis. If a ban is issued on one server group, it will only be enforced on other servers in that server group. Added ban reason and banning admin name to mini-ban-management.</li>
    <li><b>Hacker-Checker Enhancements</b> Added KPM check to hacker checker.</li>
    <li><b>Procon Ban Enhancements</b> When using procon bans, the banning admin name is now added to the front of the reason.</li>
</ul>
<b>Bugfixes</b><br/>
<ul>
    <li><b>Stability</b> Some issues with thread deadlock injected in previous versions have been resolved.</li>
    <li><b>Commands</b> All in-game commands are rejected with message during AdKats startup/reboot until full player list has been processed, this avoids killer/victim null case. Punishment timeout now blocks action, not just upload, matching spec. Kill-on-spawn for players now happens at the appropriate time, avoiding a previous double-kill issue. Players spamming @rules will no-longer spawn multiple rule printers. Rules command now shows the current rule # and total, e.g. (1/10)...(2/10)...etc...</li>
    <li><b>Ban Enforcer</b> Linked accounts were causing ID issues with ban enforcer, this is now fixed. Name bans through Procon sometimes failed propagation, this has been fixed. Unsupported round bans caused permabans in ban enforcer since type was not recognized, this has been changed to 1 hour temp-ban.</li>
    <li><b>Records</b> Logic hole where debug/exception record messages could overwrite main record messages of the same ID has been closed.</li>
    <li><b>Hacker Checker</b> Bug where hacker-checker mesh with BF3Stats was failing has been fixed.</li>
    <li><b>Chat</b> All chat messages are now trimmed before handling, both before and after command prefix parsing.</li>
</ul>
<h4>4.2.0.0 (18-MAR-2014)</h4>
<b>Changes</b><br/>
<ul>
    <li><b>Settings.</b> AdKats settings are now stored database side almost exclusively. They have always been stored there, but now have been removed from procon's plugin setting list. The only things remaining plugin side are DB connection settings, setting lock information, and debug level.</li>
    <li><b>Command Access during Startup.</b> Commands cannot be accessed until the first player listing is complete, all commands issued before this is complete are rejected. Looking at our current systems this usually takes about 1 minute after initial startup. Admins are informed in-game when the startup sequence has completed, and how long it took to complete.
</li>
</ul>
<b>Enhancements</b><br/>
<ul>
    <li><b>Added setting lock.</b> Settings in AdKats can now be locked with a password. This is so you can give lower level admins access to mini-ban-management (and some other basic functions), without endangering your other settings.</li>
    <li><b>Orchestration Enhancement.</b> Automatic MULTIBalancer whitelisting for admins is now an optional selection.</li>
    <li><b>Orchestration Enhancement.</b> Automatic reserved slot for the AdKats user list is now an optional selection.</li>
    <li><b>Orchestration Enhancement.</b> Automatic spectator slot for the AdKats user list is now an optional selection.</li>
    <li><b>Command Enhancement.</b> Lead command can now be targeted at a player.</li>
    <li><b>Added uptime command.</b> Debug and information purposes; Gives info on server, procon, and AdKats uptime, along with a couple other things.</li>
    <li><b>Added assist command.</b> Will move the requesting player to the weak/losing team and thank them for doing so.</li>
    <li><b>Added spectator slot command.</b> Will add the player to spectator slots for the current server.</li>
    <li><b>Added reserved slot command.</b> Will add the player to reserved slots for the current server.</li>
    <li><b>Added disperse command.</b> Will add the player to MULTIBalancer even dispersion for the current server.</li>
    <li><b>Added whitelist command.</b> Will add the player to MULTIBalancer whitelist for the current server.</li>
    <li><b>Added logging of player name/IP changes.</b> Now all changes of IP and name are logged for record/tracking purposes.</li>
    <li><b>Automatic PBSS.</b> An automatic PBSS (Punkbuster Screenshot) is triggered on any player who has the @report or @admin command issued against them.</li>
    <li><b>Automatic command DB addition.</b> Any new commands I add in new versions of AdKats will not require a special database query from you to add access to them, they will be added automatically.</li>
    <li><b>Leave Messages for Acted Players</b> Any player who leaves the server after having a command issued on them will trigger a chat message to admins in-game.</li>
    <li><b>Procon Chat Enhancement</b> Messages intended for online admins are now bold in procon chat window when no admins are in-game.</li>
    <li><b>Added swapnuke command.</b> This command will move everyone on the server to the opposite team. This is still experimental, and I would suggest taking caution in using it.</li>
</ul>
<b>Bugfixes</b><br/>
<ul>
    <li><b>Fixed the killme command.</b> Fixed the killme command which was broken by 4.1.0.0.</li>
    <li><b>Name Ban Fix.</b> Name bans were still enforced if a player changed their name.</li>
    <li><b>Report IDs.</b> Report IDs when handling round reports were displayed as 0.</li>
    <li><b>Time Delays.</b> Fixed time delay for rules command before allowing another call.</li>
    <li><b>Conditional command access.</b> Conditional command access contained loopholes. The only commands affected were @moveme, and @admins.</li>
    <li><b>Setting feed to stat logger.</b> Setting feed to stat logger has been changed considerably. Settings are now only fed on the hour, and reductions have been made to only required settings.</li>
    <li><b>Setting Fetch.</b> Settings were not fetched initially on AdKats startup, causing any commands changed in the first 5 minutes of operation to be overwritten by database stored settings. This has been fixed.</li>
    <li><b>Player listing.</b> Player listing was not called automatically on startup, this caused up to 30 additional seconds where commands were inaccessible. This has been fixed. </li>
    <li><b>Threading.</b> Major issues with threading have been resolved. One thread was not exiting, so more were being spawned to compensate, causing eventual layer lockup. This has been fixed, and the process is now being monitored.</li>
</ul>
<b>Upgrad SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
</blockquote>
