<h2>Development</h2>
<p>
    Started by Daniel J. Gradinjan (ColColonCleaner) for A Different Kind (ADK) on Apr. 20, 2013
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
    <li>Certain commands disabled on â€œOfficialâ€ BF4 servers.</li>
    <li>Both plugin description fetching and player list processing are now being processed async to avoid procon
        panic.</li>
    <li>The universal version of XpKillerâ€™s stat logger is now supported. Both versions of stat logger are supported.</li>
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
<h4>4.2.0.5 (21-MAR-2014)</h4>
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
    <li><b>Command Enhancement.</b> Rules command can now be targeted at a player.</li>
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
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
<h4>4.2.1.0 (30-MAR-2014)</h4>
<b>Enhancements</b><br/>
<ul>
    <li><b>Added Report Options.</b> Disabled by default, players can now be told they were reported, and a list of exclusion words can be added. EX: Inform on all reports except ones containing "hack", "aimbot", etc.</li>
    <li><b>Added contest command.</b> Disabled by default, players can now contest reports with the contest command. The contest command will block initial action by admins on the report ID, telling them the player contested the report and they need to investigate further.</li>
    <li><b>Enhanced assist command.</b> Some instances of abuse were found with the assist command. It has been changed to avoid this abuse. Ticket loss rate monitoring and a 30 second timeout has been added.</li>
</ul>
<b>Bugfixes</b><br/>
<ul>
    <li><b>Fixed the admin command report IDs.</b> Report IDs were fixed for the report command, not for the admin command.</li>
    <li><b>Fixed targeted player spam.</b> Fixed the spam from "targeted player X has left the server".</li>
</ul>
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
<h4>5.0.0.0 (17-JUL-2014)</h4>
<b>Changes</b><br/>
<ul>
    <li>Stat logger settings are now immediately fed on AdKats start, and at 1 hour intervals afterward, if the setting enabled.</li>
    <li>Low population setting now stored in section 1, server settings.</li>
    <li>Live scoreboard in DB is now force enabled.</li>
    <li>Player reputation algorithm modified to have a ceiling.</li>
    <li>Default name of balance whitelist command has been changed to blwhitelist instead of just whitelist.</li>
    <li>Move command now automatically changes to force move if the player is currently dead.</li>
</ul>
<b>Enhancements</b><br/>
<ul>
    <li>Added metabans support when using ban enforcer. Both bans and unbans supported.</li>
    <li>Commands, rules and assist for the time being, now have timeouts on them, specific to targeted players. </li>
    <li>Added option to remove rule numbers from printing.</li>
    <li>Low/high population is now tracked over the current running duration.</li>
    <li>Population statistics are now available using the uptime command.</li>
    <li>Player report messages completely revamped, now showing extended information about target and source.</li>
    <li>Additional precautions added to ensure admin powers are assigned properly when database connection goes offline.</li>
    <li>Added chat history/conversation to report emails.</li>
    <li>Added fkill command, it bypasses all other kill functionality and issues admin kill on the target immediately.</li>
    <li>Added remaining time to server-wide ban enforce messages.</li>
    <li>Reports on players recently acted on by admins are now blocked. 20 second timeout default.</li>
    <li>Players who have left the server can still be acted on using in-game commands now.</li>
    <li>Added help command, it lists all commands a player can access.</li>
    <li>Special player table now has expiration dates for all elements.</li>
    <li>Whatis command now supports command names, entering a command name will tell the source what that command does.</li>
    <li>Reporters are now informed when a player they reported leaves the server.</li>
    <li>Added an expiration date to users.</li>
    <li>All admins are informed when a kicked player rejoins the server.</li>
    <li>Added dequeue command. It cancels any queued actions for the player; moves, kills, etc.</li>
    <li>Added notes line for all users.</li>
    <li>Commands in the role allowed list now display if they cause a role to be considered "admin".</li>
    <li>Player reputation system, once private to ADK, is now public.</li>
    <li>Temp ban command now has a max duration, default 10 years.</li>
    <li>Added find command, return a player's current team, position, and score.</li>
    <li>Players targeted with reports (with report notification on), now ensure the player knows they were reported.</li>
    <li>Reports now have an optional timeout in seconds for admin action. They cannot be acted on by ID before this timeout expires.</li>
    <li>Online admins are now informed when a player requests server rules.</li>
    <li>Added automatic AFK kicker after X idle time, optional, with whitelists.</li>
    <li>Added afk command, activates AFK kicking functionality if automatic action is not enabled.</li>
    <li>Added secondary confirmation for punishment timeout, avoiding database calls if necessary.</li>
    <li>Added pull command, pulls a player to your squad, killing them in the process.</li>
    <li>Added ignore command, ignores round reports.</li>
    <li>Added mark command, marks a player for notification to admins if they leave the server.</li>
    <li>Commands can now be used with a period (.) prefix, in addition to all other prefixes.</li>
    <li>Added pchat command, returns recent chat and conversations from targeted players.</li>
    <li>Startup sequence notifications to admins have been moved and made more informative.</li>
    <li>Some commands can now have multiple targets, the first of such being the pchat command.</li>
    <li>Added pinfo command, gives extensive information about the targeted player.</li>
    <li>Unban command can now have a custom reason.</li>
    <li>Added hcwhitelist command, it adds a player to the hacker-checker whitelist, and unbans them if they are banned.</li>
    <li>Added more information to denied assist command attempts, and still more information given to admins.</li>
    <li>Added warn to punishment options.</li>
    <li>Self targeting the rules command as an admin now sends rules to the whole server.</li>
    <li>Commands are not included in mute enforcement anymore. Optional.</li>
    <li>Automatic new line has been added to the beginning of all yells in BF4, placing the [ADMIN] tag on a separate line.</li>
    <li>Added optional first spawn tell to players. Optional, disabled by default.</li>
    <li>Added logs to procon's event log for all records processed through AdKats.</li>
    <li>Now using BF4Stats API weapon damage for weapons not in AdKats weapon stat list.</li>
    <li>Added lock/unlock commands, blocking actions for 10 minutes on players who are locked, except by the locking admin.</li>
    <li>3rd party plugin settings can now be managed via the database.</li>
    <li>Greatly improved the performance of "Ban Search" in mini-ban-management section.</li>
</ul>
<b>Bugs Fixed</b><br/>
<ul>
    <li>Automatic unbans for changed names caused record spam.</li>
    <li>Assist command was unreliable due to ticket/rate logging errors.</li>
    <li>Chat log table trigger had a bug in the player ID assignment which would assign incorrect IDs for players with multiple games in the database.</li>
    <li>Rules command did not give feedback when targeted at a player.</li>
    <li>IP change logging caused spam and sometimes duplicated records.</li>
    <li>Unban command had several performance issues.</li>
    <li>Some settings were not included in the interval setting pull/push.</li>
    <li>Ticket rate and ticket count calculations were incorrect, and sometimes completely absent.</li>
    <li>Teamswap had multiple move errors when multiple people were queued for teamswap.</li>
</ul>
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
<h4>5.1.0.0 (17-SEP-2014)</h4>
<b>Changes</b><br/>
<ul>
    <li>Report IDs can only be acted on by admin role players now.</li>
    <li>All player orchestration in other plugins is done by player EAGUID now, instead of player name.</li>
</ul>
<b>Enhancements</b><br/>
<ul>
    <li>Released extended round statistics publicly. You can now track how rounds progress, not just how they end.</li>
    <li>Kicks by the AFK Manager are now hidden to avoid chat spam.</li>
    <li>Added command timeouts for kick, tban, ban, and fban, to avoid command spam.</li>
    <li>Multibalancer unswitcher is now disabled for a few seconds for each admin move, so the autobalancer will never fight admin moves.</li>
    <li>Players are now blocked from moving off a team they were moved to by admin. Lasts for the current round only.</li>
    <li>Player team/squad are now updated faster so incorrect location information is avoided.</li>
    <li>Startup notification for admins is now more prominent and readable.</li>
    <li>Player fetching is now cached layer-side for the current running instance, so after enabling and running for a while, operation will be less database heavy.</li>
    <li>Added useful information to the targeted player leaving messages.</li>
    <li>Added notification of reputation change for players.</li>
    <li>Added reputation command.</li>
    <li>Added confirmation message to the cancel command if a previous command was canceled.</li>
    <li>BF4 Case. Player type is now processed, i.e. Player, Spectator, CommanderPC, and CommanderMobile.</li>
    <li>Added more information to the player info command.</li>
    <li>Added log command.</li>
    <li>Added option that should never be used. Bypass setting for all command confirmation.</li>
    <li>Made the assist command's deny message more understandable.</li>
</ul>
<b>Bugs Fixed</b><br/>
<ul>
    <li>Fixed metabans credential request spam.</li>
    <li>Fixed internal team elements being overwritten when they shouldn't have been. This gave incorrect stats for player location and round storage stats.</li>
    <li>Fixed loophole where duplicate bans could be posted if the server lagged when clearing the ban list.</li>
    <li>Fixed edge case where players changing their name could cause specialplayer identifier mismatch.</li>
    <li>Fixed logic for plugin orchestration on player names, GUIDs were intended for use there but test player names remained in the release.</li>
    <li>Fixed command parsing from Insane Limits.</li>
    <li>Fixed issue where whitelisted players could be banned during startup as player listing happened in parallel with user listing.</li>
    <li>Fixed command rejection for existing specialplayer entries, no more duplicate postings allowed.</li>
    <li>Fixed command usage timeouts for assist, and all commands in general.</li>
    <li>Fixed reputation gain for the assist command.</li>
</ul>
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
<h4>5.2.0.0 (25-OCT-2014)</h4>
<b>Changes</b><br/>
<ul>
    <li>Plugin description and changelog are fetched at a 60 minute interval now, instead of only on startup.</li>
    <li>New line characters are removed from messages posted to Procon's chat window.</li>
    <li>Server info is now requested at a 10 second interval.</li>
    <li>Rule list can no longer be sent to offline players.</li>
    <li>MULTIBalancer unswitcher is now temporarily disabled when a SwapNuke is issued.</li>
    <li>All records are now posted in database UTC time instead of Procon layer UTC time, unless specific requirements are met.</li>
    <li>Assist command is nolonger allowed while a surrender vote is active, or if the round is not playing.</li>
    <li>Yell duration now limited between 0 and 10 seconds.</li>
</ul>
<b>Enhancements</b><br/>
<ul>
    <li>Added option to disable new player notification.</li>
    <li>Added option to disable player name change notification.</li>
    <li>Added messaging commands for private conversations between players, either in the same server, or between any of the servers on the database.</li>
    <li>Startup process messages are cleaned up in debug and procon chat.</li>
    <li>Startup progress now shown to players attempting commands during AdKats startup.</li>
    <li>Players attempting commands during AdKats startup are now be told once commands are fully online, not just admins.</li>
    <li>Greatly reduced the amount of database overhead required after first fetch of the user list.</li>
    <li>Added command to send private message to admins. /adminmsg</li>
    <li>Added player reputation to the admin report notification.</li>
    <li>Added special player group for Admin Assistants.</li>
    <li>Added command to add players to the Admin Assistant list.</li>
    <li>Released Ping Enforcer publicly.</li>
    <li>Added notification and startup block if the Procon layer and database have significantly different values for UTC time.</li>
    <li>Added 2 day and 3day ban options to the punishment hierarchy.</li>
    <li>Released Commander Manager publicly.</li>
    <li>Added optional automatic player lock on admin action.</li>
    <li>Added option to modify both manual and automatic player lock duration.</li>
    <li>Added Internal SpamBot with optional whitelisting for admins and players.</li>
    <li>Added SpamBot whitelist command.</li>
    <li>Added surrender/votenext/nosurrender commands, with a full fledged surrender system.</li>
    <li>Added reportlist command, to list the last 6 missed round reports.</li>
    <li>Added plugin restart command.</li>
    <li>Added server shutdown/reboot command.</li>
    <li>Reputable players are now informed when admins join the server. Disable option provided.</li>
    <li>Messages sent to procon admin are now bolded for visibility.</li>
    <li>Notification of glitched players in the server are now sent to admins. The battlefield server must be rebooted in these cases, they cannot be kicked/banned.</li>
    <li>Released Auto-Surrender/Auto-Nuke publicly.</li>
    <li>Added option to only send report emails when admins are in the server.</li>
    <li>Added option to tell players their reputation and infraction count on first-spawn, after the welcome message.</li>
    <li>Added option to override stat logger chat posting, and post manually.</li>
    <li>Added option to disable targeted player left notification.</li>
    <li>Added better response to report ID usage if the report was already acted on.</li>
    <li>Implemented linear interpolation on moving averages for ticket loss rates, score rates, and ping calculations.</li>
    <li>Tell messages are no longer shown in procon chat as both say and yell, simply one line as tell to reduce spam.</li>
    <li>Added commands for previous temp/perm bans, so old bans can be identified, and also will not affect player reputation.</li>
    <li>Added automatic database updater, so global fixes to database issues can be fixed without requiring plugin updates.</li>
    <li>Version status is now tracked for running instances, so issues with specific versions can be found and fixed.</li>
    <li>Added global definition for special player groups, so external tools can access all valid group information.</li>
</ul>
<b>Bugs Fixed</b><br/>
<ul>
    <li></li>
</ul>
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
<h4>6.0.0.0 (25-DEC-2014) Merry Christmas!</h4>
<b>Changes</b><br/>
<ul>
    <li>Manual round timer now activates on first spawn.</li>
    <li>Database actions are not fetched until startup is fully complete.</li>
    <li>Automatice spectator slot feed from user cache changed to admins only.</li>
    <li>New roles, when created, are given access to the same commands as Default Guest.</li>
    <li>'Display admin name in kick/ban announcement' now applies to punishments as well.</li>
    <li>Population monitoring has 3 sections instead of 2 now, Low/Medium/High, so "Only kill players when server in low population" has been changed to include both low/medium population. This is to keep the definition the same as before without modifying existing user's settings.</li>
</ul>
<b>Enhancements</b><br/>
<ul>
    <li>Player clan tags now processed and displayed in all available locations.</li>
    <li>Removed current score from player location in reports, and reduced the size of report prefixes to increase space for player names and vital information.</li>
    <li>Added countdown timer command (/cdown)</li>
    <li>Accepting Punish/Forgive for teamkilling through the Team Kill Tracker plugin.</li>
    <li>Detecting bad database connection vs normal, allowing disconnect if database becomes over encumbered for external reasons.</li>
    <li>Added manual plugin update command.</li>
    <li>Added options for low, medium, and high population to the ping enforcer.</li>
    <li>Added role power level, and disallowed certain commands from being issued from a lower power level player on a higher power level player. Commands such as kick/ban/mute/pull/mark/etc.</li>
    <li>Added manual warn command.</li>
    <li>Trimmed and cleaned targeted player leave message to be less intrusive on admin chat.</li>
    <li>Updated ban enforce message for temp bans to include both total and remaining ban time.</li>
    <li>Now updating stat logger information with player clan tags when available.</li>
    <li>Increased total allowed size of settings, allowing increased number of rules and spambot messages.</li>
    <li>Added option to exclude commands from manually posted chat logs.</li>
    <li>Added option to yell server rules.</li>
    <li>Added ability to add roles and their respective players to special player groups.</li>
    <li>Added option for auto-surrender to simply start a surrender vote instead of issuing end round.</li>
    <li>Added option for auto-surrender to have a minimum player count before triggering.</li>
    <li>Ban enforcer bans in the database are automatically expired once their time is up every few minutes, instead of being expired when the banned player rejoins the server.</li>
    <li>Added detection for duplicate instances of AdKats on each server. Only one instance of AdKats can be running on any given server.</li>
    <li>Changed timing of "user joined this server for the first time" to when the player first spawns, instead of when they first load in.</li>
    <li>Reduced chat spam from glitched player notification.</li>
    <li>Added admin notification when spectators leave the server.</li>
</ul>
<b>Bugs Fixed</b><br/>
<ul>
    <li>Manual chat log posting of command messages did not include the command prefix in the message.</li>
    <li>Massive I/O overload when database connection variables were not filled on startup, and procon plugin debug logging enabled.</li>
    <li>BF4 Phantom bow, categorized as carbine, could cause invalid HSK bans.</li>
    <li>Record posting bug could cause errors such as 'Unable to find source weight for type|action: 7|35'.</li>
    <li>Ban delay timer for hacker-checker could post bans twice.</li>
    <li>Automatic log on admin action was triggering on any action.</li>
    <li>Mark command did not work on report IDs.</li>
    <li>Automatic updater could complete with plugin file empty.</li>
    <li>Setting lock did not disable access to command entry setting.</li>
    <li>Required team for players once admin moved was not being cleared on round end.</li>
    <li>Admin WARN on punish did not give feedback to the punisher.</li>
    <li>Player identifiers were not stored when issuing new whitelist_multibalancer, slot_reserved, or slot_spectator special player entries.</li>
    <li>IP location fetching failed latitude/longitude parsing. Valid locations for players are displayed in the /pinfo command again.</li>
    <li>Ban enforcer could attempt to process server bans that did not have any identifiers.</li>
    <li>Automatic update for extensions could cause multiple dll files to be created.</li>
    <li>Automatic feed of reserved slots for admins was using all users in the user list on test versions.</li>
    <li>Debug message for record time to complete was incorrect.</li>
    <li>Round stat logger could attempt to post NaN entries to the database.</li>
    <li>Record finalization could fail if record had no command type or command action.</li>
    <li>Player reputation update could fail if commands were being fetched at the same time, could attempt to use invalid player IDs, and was not thread safe.</li>
    <li>Player dequeue command could throw race exceptions in some cases.</li>
    <li>Record upload could attempt to use invalid player IDs.</li>
    <li>Balance dispersion could attempt to use invalid player IDs.</li>
    <li>Targeted player leave message could fail if teams were being updated at the same time.</li>
    <li>MySQL queries could return in deadlock state under InnoDB, and the case was not handled.</li>
    <li>Player spawn processing could fail if commands were being fetched at the same time.</li>
    <li>FetchSQLUpdates printed stacktrace on exception for servers which were not under testing.</li>
</ul>
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
</blockquote>
