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
<h4>6.5.0.0 (9-FEB-2014)</h4>
<b>Changes</b><br/>
<ul>
    <li>Confirm command must be active and use 'yes' as command text.</li>
    <li>Cancel command must be active and use 'no' as command text.</li>
    <li>Confirm and cancel commands must be allowed on every role.</li>
    <li>Changed default auto-surrender message. Will not affect existing settings, just new installs.</li>
    <li>Changed auto-surrender message spam from 6 messages to 8 messages, ensuring the entire chat window is filled.</li>
    <li>SpamBot whitelist command now requires a duration as the first parameter. 'perm' for permanent, otherwise duration like standard temp-ban command duration.</li>
    <li>Reserved slot command now requires a duration as the first parameter. 'perm' for permanent, otherwise duration like standard temp-ban command duration.</li>
    <li>Spectator slot command now requires a duration as the first parameter. 'perm' for permanent, otherwise duration like standard temp-ban command duration.</li>
    <li>Admin assistant whitelist command now requires a duration as the first parameter. 'perm' for permanent, otherwise duration like standard temp-ban command duration.</li>
    <li>Ping whitelist command now requires a duration as the first parameter. 'perm' for permanent, otherwise duration like standard temp-ban command duration.</li>
    <li>Hacker-Checker whitelist command now requires a duration as the first parameter. 'perm' for permanent, otherwise duration like standard temp-ban command duration.</li>
    <li>Autobalance whitelist command now requires a duration as the first parameter. 'perm' for permanent, otherwise duration like standard temp-ban command duration.</li>
    <li>Autobalance dispersion command now requires a duration as the first parameter. 'perm' for permanent, otherwise duration like standard temp-ban command duration.</li>
    <li>Hacker checker HSK check now takes priority over DPS check because HSK based statistics were hitting DPS triggers incorrectly.</li>
</ul>
<b>New Commands</b><br/>
<ul>
    <li>Added specblacklist command to add players to the spectator blacklist.</li>
    <li>Added rwhitelist command to add players to report whitelist.</li>
    <li>Added popwhitelist command to add players to the populator whitelist specialplayer group.</li>
    <li>Added tkwhitelist command to add players to the TeamKillTracker whitelist specialplayer group.</li>
    <li>Added unspecblacklist command to remove players from the spectator blacklist.</li>
    <li>Added unrwhitelist command to remove players from Report Whitelist.</li>
    <li>Added unpopwhitelist command to remove players from the populator whitelist.</li>
    <li>Added untkwhitelist command to remove players from the TeamKillTracker whitelist specialplayer group.</li>
    <li>Added undisperse command to remove players from Autobalance Dispersion.</li>
    <li>Added unmbwhitelist command to remove players from Autobalance Whitelist.</li>
    <li>Added unreserved command to remove players from Reserved Slot.</li>
    <li>Added unspectator command to remove players from pectator Slot.</li>
    <li>Added unhcwhitelist command to remove players from Hacker-Checker.</li>
    <li>Added unpwhitelist command to remove players from Ping Whitelist.</li>
    <li>Added unaawhitelist command to remove players from Admin Assistant Whitelist.</li>
    <li>Added unspamwhitelist command to remove players from SpamBot Whitelist.</li>
</ul>
<b>Enhancements</b><br/>
<ul>
    <li>Added report whitelist specialplayer group.</li>
    <li>Added spectator blacklist specialplayer group.</li>
    <li>Added new auto-surrender option to disable cancelling/resetting the trigger count if a team begins making a comeback.</li>
    <li>Auto-surrender optimal values for metro/lockers greatly improved for all 3 modes of operation (surrender, nuke, and trigger vote).</li>
    <li>Auto-surrender preparation, pause, and resume messages streamlined.</li>
    <li>Database connection issue messages only sent when the issue is serious, reducing console spam.</li>
    <li>All specialplayer interaction commands now use offline/external player fetch over using fuzzy name match on online players.</li>
    <li>Added duration parameters to several specialplayer interaction commands, details in changes section.
All debug messages made more informative about what/where the message was issued.</li>
    <li>Added option to log win/loss/baserape statistics for players. Can only be enabled when using auto-surrender, as it relies on that system.</li>
    <li>Added option to monitor baserape causing players with automatic dispersal or automatic assist. This option relies on posted win/loss/baserape statistics, and includes options for duration and count to consider baserape causing. And, if feeding MULTIBalancer dispersion list, these players can automatically be placed under dispersion for the server. Players falling under automatic dispersion are notified on first join. Players under automatic dispersion cannot use the Assist command.</li>
    <li>Added option to monitor populator players. Option will generate a list of players who populate either all or the current server consistently, based on given parameters for the past week or 2 weeks.</li>
    <li>Added populator whitelist special player group for approved populator status. Players under this group can optionally be exclusively included in populator status.</li>
    <li>Added option to feed TeamKillTracker whitelist. Whitelist group will be forced to "Whitelist", and the given players pushed to that whitelist.</li>
    <li>Added TeamKillTracker whitelist specialplayer group. When feeding TeamKillTracker whitelist, these players will be included in that list.</li>
    <li>Added optional automatic perks for monitored populator players. Optional automatic perks can include reserved slot, MULTIBalancer whitelist, ping whitelist, and TeamKillTracker whitelist.</li>
    <li>Added setting group 3-2, Special Player Display. This setting group is a display of current specialplayer groups and their contents. Read only.</li>
    <li>Added access method option to all commands, for restriction of how commands can be executed. Applies only to commands issued by in-game players. Confirm and cancel commands cannot use settings other than 'Any'.</li>
    <li>Now automatically removing expired special player entries from the database.
Added small improvements to fuzzy player match response text.</li>
    <li>Decreased size of ping kick message to reduce chat spam.</li>
    <li>Improved integration with AdKatsLRT, the loadout enforcer plugin.</li>
    <li>Added more information to all error messages thrown during plugin auto-update.</li>
</ul>
<b>Bugs Fixed</b><br/>
<ul>
    <li>Major performance fix for users running orchestration settings.</li>
    <li>Fixed the 'Disable Automatic Updates' setting, which could not be re-enabled once disabled.</li>
    <li>Players could be double-checked for suspicious stats in some rare cases.</li>
    <li>Fixed display error thrown in some cases when editing user lists.</li>
    <li>Fixed some performance issues when sending AdKats generated player list to other plugins.</li>
    <li>Fixed integration of player_ban_temp commands from other plugins.</li>
    <li>'Use First Spawn Reputation and Infraction Message' was not the correct type. The setting type was a string instead of boolean, so you had to type "True" or "False" to get the value set.</li>
    <li>Automatic assign of specialplayer group to role did not re-fetch access list, so it appeared to be broken until next access fetch.</li>
    <li>MULTIBalancer whitelist feed was using player names instead of GUIDs.</li>
    <li>Database disconnects and thread exits were not handled properly in several places.</li>
    <li>Procon could freeze when editing the user list without users in the list.</li>
    <li>Fetched players and left players were kept indefinitely, causing memory overflow issues.</li>
    <li>Orchestration table and extended round stats table were added but not confirmed on startup.</li>
    <li>Fetching admin soldiers and online admin soldiers could throw errors in rare cases.</li>
    <li>Assist call via database did not confirm target on winning team before allowing action.</li>
    <li>Assist command gave reputation even if the player was not moved to the weak team.</li>
    <li>Multiple upload systems did not have complete error handling.</li>
</ul>
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
<h4>6.8.0.0 (16-AUG-2015)</h4>
<b>Enhancements</b><br/>
<ul>
	<li>Added a setting section selector for more easy traversal of the numerous plugin settings.</li>
	<li>Hacker-checker now takes soldier health of hardcore servers into account.</li>
	<li>With hacker-checker enabled at the end of each round all players are re-queued for stat checks, so if the round previously played caused any triggers they will be caught.</li>
	<li>Added LIVE system to the hacker checker, denoting actions taken based on a single round of play.</li>
	<li>Added catch for the 'Magic Bullet' hack to the LIVE system.</li>
	<li>Added catch for damage mods to the LIVE system, players using a damage mod in any round should be banned at round end.</li>
	<li>Hacker-checker no longer looks at only the top 15 weapons a player uses.</li>
	<li>Optimized all interaction and connections with Battlelog.</li>
	<li>Added option to the ping enforcer for modifying ping limit based on time of day.</li>
	<li>Added display of current ping limit based on all settings, showing the formula for how it was calculated.</li>
	<li>'Attempt manual ping when missing' is now disabled by default as this can cause performance issues for some layers.</li>
	<li>Added monitoring and dispersion option for top players in the server, a new take on server balance specifically focused on solving stacking issues before they start.</li>
	<li>Added option to ban players based on a set of banned clan tags.</li>
	<li>TeamSpeak player monitor now has an option to announce those joining both teamspeak and the game.</li>
	<li>To get around the issue where battlefield servers can only have 15 whitelisted spectators if the special player list 'spectator slot' has any players all others without a slot will be kicked from spectator; A manual way of getting the server spectator slot list to operate as it should.</li>
	<li>Added whitelist bypass option to the spambot. Add [whitelistbypass] to the beginning of any spambot message and it will be sent to all players, ignoring the whitelist.</li>
	<li>Spambot messages displayed in procon chat are now denoted with a bold 'SpamBot' prefix.</li>
	<li>Players are now thanked publicly when the assist command or auto-assist moves them to the weak team.</li>
	<li>Temp-ban command can now be issued on offline players.</li>
	<li>Perma-ban command can now be issued on offline players.</li>
	<li>Future perma-ban command can now be issued on offline players.</li>
	<li>Punish command can now be issued on offline players.</li>
	<li>Forgive command can now be issued on offline players.</li>
	<li>Player info command can now be issued on offline players.</li>
	<li>Player chat command can now be issued on offline players.</li>
	<li>Player log command can now be issued on offline players.</li>
	<li>Optimized communication between LRT and the main AdKats plugin.</li>
	<li>Optimized handling of database disconnection and critical state.</li>
	<li>Removed unnecessary calls to setting refresh to reduce procon client lag.</li>
	<li>Split fetching of player battlelog player info and battlelog player stats to speed up plugin start.</li>
	<li>Added warning message if the plugin is having issues connecting to battlelog.</li>
	<li>Updated reputation command to include infraction point information, and, if auto-forgives are enabled, when the player's next auto-forgive will fire.</li>
	<li>Populating servers from low pop status through high pop status now gives +10 base reputation.</li>
	<li>Added option to yell server rules.</li>
</ul>
<b>New Commands</b><br/>
<ul>
	<li>Added feedback command for players to give server feedback.</li>
	<li>Added isadmin command for players to check if a player is an admin.</li>
</ul>
<b>Changes</b><br/>
<ul>
	<li>Hacker-checker DPS section is now hardcode enabled and all settings aside from ban message are automated.</li>
	<li>Wait duration between battlelog requests increased from 1 second to 3 seconds.</li>
	<li>Increased hacker-checker KPM check's minimum kill count from 100 kills to 200 kills.</li>
	<li>Players can no-longer report themselves.</li>
	<li>Assist command is now blocked from usage until 2 minutes into any round.</li>
	<li>Assist command is now blocked from usage when teams are within 60 tickets of each other.</li>
	<li>Player say command chat access now required to be AnyHidden.</li>
	<li>Player yell command chat access now required to be AnyHidden.</li>
	<li>Player tell command chat access now required to be AnyHidden.</li>
	<li>Player find command chat access now required to be AnyHidden.</li>
	<li>Increased specific timeout of permaban and future permaban commands from 30 to 90 seconds.</li>
	<li>Plugin now automatically updates to the latest test version if user is running any outdated test version.</li>
	<li>Added 10 minute specific timeout to self kill command.</li>
	<li>Auto-surrender default resolve message changed from 'Auto-Resolving Baserape Round' to 'Auto-Resolving Round'.</li>
</ul>
<b>Bugs Fixed</b><br/>
<ul>
	<li>Assigning/unassigning role groups could cause a role to deny all of its commands.</li>
	<li>Invalid punishment hierarchy values could have been added, only to be rejected when the punish was issued.</li>
	<li>The metabans plugin could have been disabled while running ban enforcer with metabans integration enabled.</li>
	<li>Metabans API key/Username changes database side were not propagated to the metabans plugin.</li>
	<li>Blank spambot messages could have been added to the system.</li>
	<li>Round state sometimes failed to reset when disabling then enabling the plugin.</li>
	<li>Plugin version numbers with more than one digit per section caused update issues.</li>
	<li>Spambot messages and ticket rate messages were displayed when the server was empty.</li>
	<li>TeamSpeak users were sometimes incorrectly mapped to players in game due to name similarity.</li>
	<li>Players locked to a specific team could clear the lock by leaving then rejoining the server.</li>
	<li>Teamswap failed to move players properly from team 1 (US usually) to team 2 (RU usually) if team 1 was completely full.</li>
	<li>Round IDs for exteneded round stats were sometimes incorrectly calculated.</li>
	<li>Extended round stats could sometimes post during invalid round state, filling the log with countless duplicate records.</li>
	<li>Hacker-checker queue could call actions on players in the wrong order.</li>
	<li>Command locks on a player could block actions by the entity who locked them.</li>
	<li>Countdown command did not work correctly with some team sets.</li>
	<li>When banning a player with ban enforcer enabled the kick could sometimes fail.</li>
	<li>Modifying special player lists could cause excessive refreshing of the user list.</li>
	<li>Optimized plugin shutdown, some processes failing to finish could slow it down before.</li>
</ul>
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
<h4>7.0.0.0 (14-OCT-2017)</h4>
<b>Enhancements</b><br/>
<ul>
	<li>When a player is kicked for VIP, the plugin is sometimes able to tell admins who was kicked. DICE's event for this is unreliable, but when it works properly, the plugin will tell admins about the VIP kick.</li>
	<li>Added automatic server restart options. When the server is empty (aside from seeder accounts), and the server uptime is greater than a configured number of hours, the server is automatically rebooted. You also have the option to do an automatic procon shutdown when this happens. If you have procon configured to automatically reboot when a crash/shutdown occurs this will effectively reboot your procon layer and the server at the same time.</li>
	<li>Increased performance of the anti-cheat scripts. AdKats now stores player’s battlelog persona IDs in the database, so fetching their info from battlelog is faster and requires fewer requests.</li>
	<li>Decreased startup time of the loadout enforcer when paired with AdKats. Battlelog information needs to be fetched for every player before anti-cheat requests begin now.</li>
	<li>Commands that are classified as admin commands (commands that when enabled will make a role be considered admin), show up in the list with [ADMIN] next to them. Commands that are causing a role to be considered admin will have a '<---' next to the command, so it's easy to find those commands in the list.</li>
	<li>In addition to the "IRO Overrides Low Population" setting i've added a setting for minimum infractions before it overrides. With this setting a player will need at least that many infractions against them before IRO will override low population.</li>
	<li>Added a setting to disable the display of all ping enforcer messages in the procon chat tab. Messages for players kicked for ping are still displayed.</li>
	<li>The "Top Player Monitor" has been completely gutted and rebuilt. This section is now called the "Team Power Monitor" and shows the estimated power of each team in your server. You have options to use join reassignment based on team power and seeder balancing. The other sections of this system are either nonfunctional or experimental, and I would advise caution when using them. The scrambler might be good for your server, but it locks players to their assigned teams which means it's not good for volatile maps like metro.</li>
	<li>Added discord integration with the same options as the existing teamspeak integration. The only unfortunate side being that I can only update discord information (online players) every 5 minutes. This is because Procon is still on .NET 3.5 and doesn't have access to websockets.</li>
	<li>Admins can now bypass the spambot whitelist for certain messages by adding [whitelistbypass] to the start of the message. Useful for important/time-sensitive announcements.</li>
	<li>Added an all-caps chat limiter, with a bunch of settings for how a message is considered all-caps. Also added a new group so you can have only specific players targeted by this script.</li>
	<li>Completely redesigned the assist command for use with the team power monitor.</li>
	<li>Players that use the /assist command are now automatically queued for 5 minutes if their assist request fails. This is so players wanting to assist don't need to keep executing the command to see if they are allowed. After 5 minutes their assist request is automatically cancelled.</li>
	<li>A setting for the minimum number of minutes into a round before the /assist command becomes available has been added.</li>
	<li>Updated the assist messages to include a player's current calculated power.</li>
	<li>Added an option to disable the enforcement of single instances of AdKats on a server. This is for people who have issues with the /AdKatsInstanceCheck messages being stored in external systems. These messages are used by AdKats to make sure that one and only one instance is running on a server at a time, since it can cause undesirable results to have multiple instances running at the same time.</li>
	<li>Added a ban filter option for posting to metabans, so you can choose which bans are sent to the service.</li>
	<li>Add integration for PushBullet in the same manner that AdKats uses email integration.</li>
	<li>The nuke command can now accept team IDs in addition to team keys. This is mainly for people who run servers with the same faction as both teams and were previously unable to nuke team “2”.</li>
	<li>The server shutdown command now provides a 5 second countdown before rebooting the server.</li>
	<li>Moved the auto-nuke settings into its own setting section so it’s easier to configure.</li>
	<li>Added minimum and maximum ticket counts for the auto-surrender/auto-nuke so you can limit when it can activate during a round.</li>
	<li>The auto-nuke script is now able to issue timed nukes on players. This means a nuke is able to hold the target team's player dead for a specified amount of time, making the nuke more effective. I've also added options to increase the nuke duration as the number of times a team is nuked increases.</li>
	<li>Added options to reset the auto-nuke trigger counts when a nuke is fired, this way the script has time to reset after a nuked team loses a lot of flags.</li>
	<li>Added option for minimum duration between nukes to the auto-nuke script so you can configure how often it can fire.</li>
	<li>Added option to make nukes fire based on ticket difference instead of flags, so you can configure teams which have had the map for a long time to be pushed back regardless.</li>
	<li>Added option to configure how many nukes can fire during a round.</li>
	<li>Added option to switch back to auto-surrender after a certain number of nukes are fired and the team is still having trouble.</li>
	<li>Added an optional countdown before an auto-nuke is actually fired, giving players a chance to realize what's about to happen.</li>
	<li>Added option to notify players of perks that are expiring soon. Included the new /perks command so they can see what perks they have. Included a setting for how long before their perks expire to notify them.</li>
	<li>Added the "Battlecry" system. This section lets you allow players have a message sent to the server when they first spawn in, an announcement of their arrival. Options are available to change how "loud" the messages are and who is able to access their usage. You can also configure a list of banned words in the battlecry, and a max length on the battlecry.</li>
	<li>Added a BF4 faction randomizer so you can have random faction assignments in your server. There are many options for how the randomness should be handled.</li>
	<li>Split the 'Inform reputable players and admins of admin joins' into two separate settings, one for reputable players, and one for admins.</li>
	<li>Updated all the procon chat messages AdKats posts to be color coded for different operations. Added tags for some things like the spambot so its messages can be picked out of the tab easily.</li>
	<li>Messages in your rules and spambot can now be map/mode specific. Simply by adding a prefix to the messages and rules you can make them appear only on those maps or modes. Great for mixed mode servers.</li>
	<li>AdKats now monitors player clan tag changes, keeps a history of them, and notifies admins of changes. Tag history is also included in the player info command.</li>
	<li>Added domination to the list of modes where flag estimations are shown in the ticket rate messages.</li>
	<li>Added estimated winner and win time to the ticket rate messages.</li>
	<li>In BF4 since the in-game menu does not tell players why they were kicked or banned anymore, so AdKats now spams the kicked or banned player for a few seconds before they are booted to make sure they know why it happened.</li>
	<li>Improved some of the integrations with the loadout enforcer plugin. Specifically for report actions and manual loadout fetching.</li>
	<li>Added %map_name% and %mode_name% options to the email handler for reports.</li>
	<li>Improved the player info command to show actual hour count the player has in the server, in addition to the calculated weeks/days/hours string.</li>
	<li>Cleaned up quite a few messages which were unnecessarily long.</li>
	<li>Added more robust monitoring of player listing and server info triggers, making sure AdKats doesn’t oversaturate procon or the server with requests.</li>
	<li>Fortified exception handling in a lot of areas.</li>
	<li>Average AdKats startup durations are stored for the last 15 reboots, and displayed in the startup message when it fully completes.</li>
	<li>Estimated time until the plugin is finished starting up are now displayed to players attempting to issue commands during the startup phase. Once startup has completed they are notified and thanked for their patience.</li>
</ul>
<b>New Commands</b><br/>
<ul>
	<li>server_nuke_winning - Added a winning-nuke command, which is only able to nuke the currently winning and map dominant team. Helps to avoid human error when manually issuing nukes.</li>
	<li>player_ping - You can now fetch any player’s current ping and average ping with the fetch ping command.</li>
	<li>player_forceping - Admins can now force AdKats to issue manual pings on specific players using the force ping command. These players will be manually pinged by the layer instead of relying on the server provided info on their ping.</li>
	<li>player_debugassist - The debug assist command was added mainly for my own purposes, so i can see if a player would be allowed to assist without actually having them do it. But it could be useful to some people.</li>
	<li>player_perks - Players are now able to fetch their current list of perks with the player perks command. This command can also be targeted at a player to fetch that player’s perks.</li>
	<li>player_loadout - You can now fetch a player’s current loadout if you’re running the loadout enforcer, using the loadout command.</li>
	<li>player_loadout_force - Players loadouts can now be manually forced up to trigger level enforcement if you’re running the loadout enforcer, with the force loadout command.</li>
	<li>self_battlecry - Players can set their own battlecry using the battlecry command.</li>
	<li>player_battlecry - Admins can use the player battlecry command to set other player’s current battle cries.</li>
	<li>player_discordlink - Admins can link an active player in the server with a member in the discord server by ID using the discord link command.</li>
	<li>player_blacklistallcaps - Admins can make specific players fall under enforcement of the all caps chat limiter using the all caps blacklist command.</li>
	<li>player_blacklistallcaps_remove - Admins can remove players from the all caps blacklist specific enforcement using the remove all caps blacklist command.</li>
</ul>
<b>Changes</b><br/>
<ul>
	<li>Reserved slot feed for *online* admins has been renamed to match what it actually does, add a VIP kick whitelist.</li>
	<li>Weapon code posting to the centralized weapon code display has been removed since all weapon codes for the supported games are known now.</li>
	<li>Everything that was previously named "Hacker-Checker" is now named "Anti-Cheat". The command /hcwhitelist is also renamed to /acwhitelist to go along with this change.</li>
	<li>Players in teamspeak/discord are now automatically whitelisted from spambot messages.</li>
	<li>Automatic forgives for clean play no longer require positive reputation.</li>
	<li>Changed a lot of messages around the plugin which used a team’s name/key to also include the team ID in the message in the format ID/Key.</li>
	<li>The ‘time on server’ section of the player info command no longer says ‘+ current session’. The current session time is simply added to the base and displayed now.</li>
	<li>The mark command is no longer used to force player loadouts, there is a separate command for that now. The mark command is now only used to mark a player for leave notifications.</li>
	<li>Removed the concept of 'adjusted' ticket rates from the UI. The rates shown to you for flag based modes are now by default the adjusted rates, normal rates are shown for non-map modes.</li>
	<li>Added a block against negative values in the minimum surrender/auto-nuke ticket gap setting.</li>
	<li>Added block against people adding a minimum ticket rate window value greater than the maximum ticket rate window value, and vice-versa. The values will now automatically swap when the user attempts this.</li>
	<li>Records in the extended round stats table older than 60 days are now automatically purged.</li>
	<li>First join/first spawn messages are now blocked when the plugin is recently started. This is to make sure that players are not spammed when you reboot the plugin during an active round.</li>
	<li>Player leave notifications are no longer given based on private say/yell/tell messages through AdKats, all other action commands still result in a notification.</li>
	<li>Kicks against yourself are no longer announced to the server.</li>
	<li>Ticket rates in procon chat are now only displayed during an active round where ticket counts have changed, before they would still display during the pre-round phase.</li>
	<li>Forgiving players into negative infraction point values is now blocked.</li>
	<li>Replaced all mentions of @command in the plugin with !command.</li>
	<li>Issuing the kill command on a player will now announce that action to the server like other actions do.</li>
	<li>Removed messages about global timing fetch errors.</li>
	<li>Removed the restrictions on player names which were in place for BF3. Now most characters can be used in player names. The unfortunate side of this is those characters still cause issue fetching from battlelog.</li>
	<li>Changed population success notification to be a tell instead of a say.</li>
	<li>The players ColColonCleaner and PhirePhrey are automatically added to reserved slots for any server running AdKats.</li>
	<li>IP-API communication errors are now changed to debug messages instead of error messages, since they are not crucial to the function of AdKats.</li>
</ul>
<b>Bugs Fixed</b><br/>
<ul>
	<li>Fixed an issue where players could end up being infinitely queued for anti-cheat checks if their battlelog info was unavailable.</li>
	<li>Fixed an issue where players would not get updated battlelog information after the first fetch was complete, specifically on new rounds.</li>
	<li>Fixed an issue where players could end up being infinitely queued for IP info fetches if the server failed to respond, or if they left the server before it did.</li>
	<li>Fixed an issue where errors were thrown because some player’s battlelog information exceeded the size of 32 bit integers, increased the size of those variables.</li>
	<li>Fixed the DPS ban messages to contain the proper code 4 prefix.</li>
	<li>Fixed the KPM ban messages to contain the proper code 5 prefix.</li>
	<li>Fixed an issue where modifying a role's authorized commands would shoot you back to the top of the setting list. Basically now it waits a while before modifying the tags on those settings in the procon view.</li>
	<li>Fixed an issue where allowing commands from muted players would allow them to simply place a command prefix in front of the message and it wouldn’t act on them. Now it confirms that the command is valid and exists.</li>
	<li>Assist command timeout had incorrect calculations due to the duration of the command. This has been fixed.</li>
	<li>Fixed an issue where sometimes players could spawn while a nuke is happening and remain alive.</li>
	<li>Fixed some of the message formatting sent by the active round reports command.</li>
	<li>Fixed an issue where surrender voting included spectators/commanders in the required player count calculation to surrender.</li>
	<li>Fixed an issue where the populator monitor would not fetch updated populator players immediately when that section was enabled, it would have to wait for the next user list update (5 minute interval).</li>
	<li>Fixed chat spam during off hours caused by the spambot, making sure that those messages are only visible in the chat tab when there are at least 5 players in the server.</li>
	<li>Fixed issue where AFK player monitor would count all players (commanders/spectators) instead of just the active round players.</li>
	<li>Fixed an issue where AdKats and some auto-balancer plugins could fight over where a player should be if an admin moved them. AdKats would never give up the fight for control of the player and that resulted in an infinite loop of swapping the player between teams. AdKats now gives up the fight if a players has 8 or more moves in 5 seconds.</li>
	<li>Fixed an issue where a player who was previously assigned a team leaving the server would delete their assigned team, allowing them to join back and switch to their original team. AdKats now remembers their assigned team when the leave and rejoin during a single round.</li>
	<li>Fixed an issue where using section 4-2 to add command target whitelist to a role would have no effect.</li>
	<li>Fixed an issue where players were able to use the join command on themselves to get a free admin kill. They were also able to use this on a current squadmate for the same reason. These are now blocked.</li>
	<li>Blocked usage of the join/pull commands from spectator and commander player types.</li>
	<li>Fixed an issue where ‘previous’ bans could end up being imported into the plugin. The bans wouldn’t be issued again but it caused unnecessary processor work.</li>
	<li>Fixed an issue where a player could be added to multiple users at the same time.</li>
	<li>Fixed player fetch performance in several scenarios. Sometimes a player which was already loaded was still fetched from the database again.</li>
	<li>Fixed a bug where admin assistants who were considered ‘grandfathered’ would cause fetching issues.</li>
	<li>Fixed an issue where the spectator slot feed would attempt to add more than 15 players to the list. For some reason DICE added a 15 player limit to the spectator slot list. When you have more than 15 approved spectators now it just manually monitors the list and keeps the official spectator list empty.</li>
	<li>Fixed a few error possibilities coming from the ping enforcer.</li>
	<li>Fixed an error where comparing command input with player names could crash.</li>
	<li>Removed extra message spam caused by the private messaging system.</li>
	<li>Removed extra message spam caused by player say/yell/tell commands issued by external plugins.</li>
	<li>Fixed shutdown durations in some cases, making sure threads automatically exit when you shut down the plugin.</li>
	<li>Fixed an issue where battlelog going offline would cause a lot of errors to be thrown by the plugin. Now the plugin will wait at 30 second intervals and show warnings if battlelog is offline.</li>
	<li>Fixed an issue where the admin assistant whitelist could be given to admin roles in section 4-2. That whitelist is not supposed to be used for admin roles.</li>
</ul>
<b>Upgrade SQL from 4.0.0.0 - Current</b><br/>
<ul>
    <li><b>No upgrade SQL required.</b></li>
</ul>
</blockquote>