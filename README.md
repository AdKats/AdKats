<script>
    //<latest_stable_release>4.1.0.0</latest_stable_release>
</script>
<h1>AdKats</h1>
<p>
    Admin Toolset with a plethora of features, basically the default "In-Game Admin" with 1500+ hours of enhancements and features added.
    AdKats focuses on making in-game admins more efficient and accurate at their jobs, with flexibility for almost any setup.
    Includes a cross-server ban enforcer more reliable and complete than others available, and the AdKats WebAdmin for out-of-game control has been released.
    Designed for groups with high-traffic servers and many admins, but will function just as well for small servers.
</p>
<ul>
    <li>
        <b>Basic Action Commands.</b>
        Standard commands for player killing, kicking, punishing, banning, unbanning, moving, etc...Over 30 available in-game commands.
        Commands can be accessed from almost anywhere: In-game, Procon's chat window, database, HTTP server, other plugins, etc, etc...
    </li>
    <li>
        <b>Editable Ranks and Roles.</b>
        Custom ranks and roles can be created for users, with each role given access to only the commands you want them to access.
        Default guest role is given to all players, and can be edited to your desired specs.
        All roles and powers are automatically synced between servers, so you only need to change user information once.
        Soldiers assigned to users will also keep their powers even if they change their in-game names.
    </li>
    <li>
        <b>Admin and setting sync between servers.</b>
        All changes to plugin settings are stored in the database, and can be automatically synced between your Procon layers.
    </li>
    <li><b>Infraction Tracking System.</b>
        Punish/Forgive players for infractions against your server. Everything is tracked, so the more infractions they
        commit, the worse their punishment gets. Made so all players are treated equally. Heavily customizable.
    </li>
    <li>
        <b>Quick Player Report and Admin Call Handling, with email support.</b>
        Notification system and quick handling features for all admin calls and player reports.
        Reports can be referenced by number for instant action.
    </li>
    <li>
        <b>Orchestration and Server List Management.</b>
        Server reserved slots, server spectator slots, and autobalancer whitelising through MULTIBalancer can all be automatically done through the AdKats user list.
    </li>
    <li>
        <b>Admin Assistants.</b>
        You can choose to give a small perk to players who consistently provide you with accurate player reports.
        Documentation linked below.
    </li>
    <li>
        <b>BF3/BF4 "Hacker-Checker" with Whitelist.</b>
        BF3Stats and BF4Stats are internally used to pull player information, and can be enabled for hacker-checking with a couple clicks.
        Please read documentation before enabling.
    </li>
    <li>
        <b>Email Notification System.</b>
        Email addresses can be added to every user, and once enabled they will be sent emails for player reports and admin calls.
        I am currently working on adding command parsing on reply to emails, and possibly text message command support in the future.
    </li>
    <li>
        <b>Fuzzy Player Name Completion.</b>
        Fully completes partial or misspelled player names. I've been consistently able to find almost any player with
        3-4 characters from their name.
    </li>
    <li>
        <b>Player Muting.</b>
        Players can be muted if necessary.
    </li>
    <li>
        <b>Player Joining.</b>
        Player's squads can be joined via command, and locked squads can be unlocked for admin entry.
    </li>
    <li>
        <b>Yell/Say Pre-Recording.</b>
        Use numbers to reference predefined messages. Avoid typing long reasons or messages. e.g. @kill player 3
    </li>
    <li>
        <b>Server rule printing.</b>
        Just like "Server rules on request" except multiple prefixes can be used for rule commands at the same time, and requests for rules are logged.
    </li>
    <li>
        <b>External Controller API.</b>
        AdKats can be controlled from outside the game through systems like AdKats WebAdmin, and soon through other plugins like Insane Limits.
    </li>
    <li>
        <b>Internal Implementation of TeamSwap.</b>
        Queued move system for servers that are consistently full, players can be queued to move to full teams once a slot opens.
        Greatly improved over the default version.
        Documentation linked below.
    </li>
    <li>
        <b>AdKats Ban Enforcer.</b>
        AdKats can enforce bans across all of your servers, and can enforce on all metrics at the same time.
        The internal system has been built to be more complete and reliable than others available, including metabans, and is further enhanced by using AdKats WebAdmin.
        It will automatically import all Procon bans from all your servers and consolidate them.
        It will also import any existing bans from the BF3 Ban Manager plugin's tables.
        Full documentation linked below.
    </li>
    <li>
        <b>Editable In-Game Commands.</b>
        All command text, logging options, and enable options can be edited to suit your needs.
    </li>
    <li><b>Full Logging.</b>
        All admin activity is trackable via the database per your custom settings for every command;
        So holding your admins accountable for their actions is quick and painless.
        And, if you are using AdKats WebAdmin nobody but your highest admins will need direct Procon access.
    </li>
    <li>
        <b>Performance.</b>
        All actions, messaging, database communications, and command parsing take place on their own threads, minimizing performance impacts.
    </li>
</ul>
<p>
    If you find any bugs, please submit them <a href="https://github.com/ColColonCleaner/AdKats/issues?state=open" target="_blank">HERE</a> and they will be fixed ASAP.
</p>
<p>
    AdKats was inspired by the gaming community A Different Kind (ADK).
    Visit <a href="http://www.adkgamers.com/" target="_blank">http://www.adkgamers.com/</a> to say thanks!
</p>
<h2>Upgrade Installation</h2>
<p>
For update instructions from your current version please review the MyRCON forum thread.<br/><br/>

<a href="https://forum.myrcon.com/showthread.php?6045-AdKats-Advanced-In-Game-Admin-and-Ban-Enforcer-4-1-0-0&p=96108&viewfull=1#post96108" target="_blank">Update 4.1.0.0</a><br/><br/>

Once update ready, download updated AdKats source from here: 
<a href="http://sourceforge.net/projects/adkats/files/AdKats_v4.1.0.0.zip/download" target="_blank">Version 4.1.0.0</a>
</p>
<h2>Fresh Installation</h2>
<ol>
    <li>
        <b>Install XpKiller's Stat logger plugin.</b>
        Download and install the latest universal version of XpKiller's
        <a href="https://forum.myrcon.com/showthread.php?6698" target="_blank">Procon Chat, GUID, Stats and Mapstats Logger</a>.
        Make sure stat logger is installed and running! Do NOT attempt to install AdKats until that plugin is running without issue.
    </li>
    <li>
        <b>Set up the database.</b>
        Run the contents of this sql script on your database. It must be run on the same database that Stat Logger is running on.
        (You can copy/paste the entire page as its shown): https://raw.github.com/ColColonCleaner/AdKats/master/adkats.sql
        <br/>
        (I would run this automatically if I could, but i'm limited until Procon updates their MySQL connector to allow delimiters)
    </li>
    <li>
        <b>Download AdKats Source.</b>
        Download the latest version of AdKats from here:
        <a href="http://sourceforge.net/projects/adkats/files/AdKats_v4.1.0.0.zip/download" target="_blank">Version 4.1.0.0</a>
    </li>
    <li>
        <b>Add plugin file to Procon.</b>
        Add the plugin file to Procon as you would any other, in either the plugins/BF3 or plugins/BF4 folder depending on which game your layer is running on.
    </li>
    <li>
        <b>Enter database credentials.</b>
        All database connection information must be entered in the settings tab before AdKats can run.
    </li>
    <li>
        <b>Enable AdKats.</b>
        AdKats will confirm all dependencies and show confirmation in the console. If it gives your server an ID then all is well.
    </li>
    <li>
        <b>Disable the default "In-Game Admin".</b>
        Disable any other plugins that use commands like kill, kick, etc. The commands would be run by both, causing unwanted functionality. Enjoy AdKats!
    </li>
</ol>
<p>
    If you have any problems installing AdKats please let me know on the MyRCON forums, or here as an issue and I'll respond promptly.
</p>
<h2>Dependencies</h2>
<h4>1. A MySQL Database</h4>
<p>
    A MySQL database accessible from your Procon layer is required. AdKats checks the database for needed tables on connect.<br/>
    <br/>
    <b>Getting a Database:</b>
    Usually the hosting company for your layers can provide you a database, and using that is advisable as the latency between Procon and the DB will be the lowest possible.
    Or even better if you're hosting layers on a VPS just create a local database by downloading the appropriate installer from MySQL's website.
    We use our webserver for database hosting and that works great.
    Be cautious of free database options and services, those paths usually have restrictions on database size and are hosted on unreliable servers, which can lead to many problems down the road.
</p>
<h4>2. XpKiller's "Procon Chat, GUID, Stats and Mapstats Logger" Plugin</h4>
<p>
    Version 1.1.0.1+ of the BF3 version, or any universal version is required.
    AdKats will only run if one of these plugins is (1) using the same database AdKats uses, and (2) running on every battlefield Server you plan to install AdKats on.
    Running it along-side AdKats on each Procon layer will ensure these conditions are met.
</p>
<p>
    The latest universal version of XpKiller's Stat Logger can be downloaded from here: <a href="https://forum.myrcon.com/showthread.php?6698" target="_blank">Procon Chat, GUID, Stats and Mapstats Logger</a>
</p>
<h2>Features</h2>
<h3>User Ranks and Roles</h3>
<p>
    On first enable you will need to add a user, you can have as many users as you want.
    When a user is added you need to assign them a role.
    The default role is "Default Guest" and the allowed commands for that role are shown to you in the role section.
    The default guest role cannot be deleted, but can be edited to your heart's content.
    You can add more roles by typing a new role name in the "add role" field.
    All roles that are added default to allow all commands, so you will need to edit the allowed commands for new roles.
    When you change a user's role and they are currently in-game they will be told that their role has changed, and what it was changed to.
</p>
<p>
    Once a user is added you need to assign their soldiers.
    Users can have multiple soldiers, so if your admins have multiple accounts you can assign all of those soldiers under their user.
    All soldiers added need to be in your database before they can be added to a user.
    This system tracks user's soldiers, so if they change their soldier names they will still have powers without needing to contact admins about the change.
    Type their soldier's name in the "new soldier" field to add them.
    It will error out if it cannot find the soldier in the database.
    To add soldiers to the database quickly, have them join any server you are running this version of AdKats on and their information will be immediately added.
</p>
<p>
    The user list is sorted by role ID, then by user name.
    Any item that says "Delete?" you need to type the word delete in the line and hit enter.
</p>
<h3>Infraction Tracking System</h3>
<p>
    Infraction Tracking commands take the load off admins remembering which players have broken server rules, and how
    many times. These commands have been dubbed "punish" and "forgive". Each time a player is punished a log is made in the
    database; The more punishes they get, the more severe the action gets. Available punishments include: kill, kick,
    temp-ban 60 minutes, temp-ban 1 day, temp-ban 1 week, temp-ban 2 weeks, temp-ban 1 month, and permaban. The order
    and severity of punishments can be configured to your needs.
</p>
<p>
    Detailed Stuff: After a player is punished, their total points are calculated using this very basic formula:
    <b>(Punishment Points - Forgiveness Points) = Total Points</b>
    Then an action is decided using Total Points from the punishment hierarchy. Punishments should get harsher as the
    player gets more points. A player cannot be punished more than once every 20 seconds; this prevents multiple admins
    from accidentally punishing a player multiple times for the same infraction.
</p>
<h4>IRO Punishments</h4>
<p>
    When a player is punished, and has already been punished in the past 10 minutes, the new punish counts
    for 2 points instead of 1 since the player is immediately breaking server rules again. A punish worth 2
    points is called an "IRO" punish by the plugin, standing for Immediate Repeat Offence. "[IRO]" will be appended to the
    punish reason when this type of punish is activated.
</p>
<h4>Punishment Hierarchy</h4>
<p>
    The punishment hierarchy is configurable to suit your needs, but the default is below.
</p>
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
        <td>Temp-Ban 1 hour</td>
        <td>tban60</td>
    </tr>
    <tr>
        <td><b>4</b></td>
        <td>Temp-Ban 2 hours</td>
        <td>tban120</td>
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
<p>
    Players may also be 'forgiven', which will reduce their Total Points value by 1 each time, this is useful if you have a
    website where players can apologize for their actions in-game. Players can be forgiven into negative total point values
    which is why a 'less than 1' clause is needed.
</p>
<p>
    You can run multiple servers with this plugin on the same database; A different ID is automatically assigned to each
    server. If you want punishments to increase on this server when infractions are committed on others set
    "Combine Server Punishments" to true. Rule breaking on another server won't cause increase in punishments on the current
    server if "Combine Server Punishments" is false. This is available since many groups run different rule sets on each
    server they own, so players breaking rules on one server may not know rules on another, so they get a clean slate.
</p>
<p>
    <b>Suggestions:</b> When deciding to use this system, 'punish' should be the only command used for player rule-breaking.
    Other commands like kill, or kick are not counted in the system since sometimes players ask to be killed, admins kill/kick themselves,
    or players get kicked for AFKing. Kill and kick should only be used for server management. Direct tban
    and ban are of course still available for hacking/glitching situations, but that is the ONLY time they should be used.
</p>
<h3>Ban Enforcer</h3>
<p>
    AdKats can enforce bans across all of your servers.
    The Ban Enforcer will import and consolidate all bans from every Procon instance you run.
    Bans can be made by name, GUID, IP, any combination, or all at once.
    The default ban is by EA GUID only, this default can be edited but is not recommended.
    Banned players are told how long their ban will last, and when a banned player attempts to re-join they are told the remaining time on their ban.
    Using ban enforcer also gives access to the unban in-game command.
</p>
<p>
    The Enforcer works properly with all existing auto-admins, and any bans added manually through Procon will be automatically imported by the system.
    A mini-ban-management section is added to the plugin settings when you enable this, however, for full ban management it requires AdKats WebAdmin.
    Ban enforcer's options are simply too much for the plugin setting interface to house properly.
    Use of the ban enforcer is optional because of this slight dependency, and is disabled by default.
</p>
<p>
    Ban Enforcer can be enabled with the "Use Ban Enforcer" setting. On enable it will import all bans from your ban list then clear it.
    Once you enable enforcer you will be unable to manage any bans from Procon's banlist tab.
    Disabling ban enforcer will repopulate Procon's banlist with the imported bans, but you will lose any additional information ban enforcer was able to gather about the banned players.
</p>
<p>
    <b>Reasoning behind creation, for those interested:</b>
    We had tried many other ban management systems and they all appeared to have some significant downfalls.
    Developing this allowed for some nice features not previously available.
    I can bypass Procon's banlist completely, this way no data is lost on how/why/who created the ban or on who it's targeted.
    I can enforce bans by any parameter combination (Name, GUID, IP), not just one at a time.
    Players can now be told how much time is left on their ban dynamically, every time they attempt to join.
    And tracking of bans added through in-game commands or autoadmins on any server is a cakewalk now, so clan leaders don't need to go great lengths to look things up.
    Several other reasons as well, but overall it was a fantastic move, and thankfully we had the devs available to make it. </shamelessSelfPromotion>.
</p>
<h3>Report/CallAdmin System w/Email Support</h3>
<p>
    When a player puts in a proper @report or @admin all in-game admins are notified.
    All reports are logged in the database with full player names for reporter/target, and the full reason for reporting.
    All uses of @report and @admin with this plugin require players to enter a reason, and will tell them if they haven't entered one.
    It will not send the report to admins unless reports are complete, which cleans up what admins end up seeing for reports.
</p>
<h4>Using Report IDs</h4>
<p>
    All reports and calls are issued a random three digit ID which expires either at the end of each round, or when it is used.
    These ID's can be used in any other action command, simply use that ID instead of a player-name and reason
    (e.g. waffleman73 baserapes, another player reports them and gets report ID 582, admins just use @punish 582 instead of @punish waffleman73 baserape).
    Confirmation of command with @yes is required before a report ID is acted on.
    Players are thanked for reporting when an admin uses their report ID.
</p>
<h4>Report Emails</h4>
<p>
    When email usage is enabled, all users with access to player interaction commands will get an email containing the report information.
</p>
<h3>Admin Assistants</h3>
<p>
    This system has been completely revamped in 4.1.0.0, and several hidden features have now been released to the public.
    We utilized the full system on our no explosives server with great success, mainly catching things autoadmin cannot.
    Basically this system automatically tracks who the trusted players in your servers are, and who are reliable sources of reports.
</p>
<h4>Basic Functionality</h4>
<p>
    The system makes use of the report IDs assigned to each round report.
    When a player sends a report, and an admin uses the report by ID, the report is logged as confirmed.
    Once you enable Admin Assistants, AA status is given once the player has X confirmed reports in the past month or 75+ total confirmed reports.
    A player with AA status is informed of their status on first spawn in the server after joining.
    If you enable the admin assistant perk, players with AA status are given access to the teamswap and admins commands for the duration they maintain AA status.
    These command perks are basically incentives to report rule-breakers.
    Whether a player has AA status is calculated when they join the server, and that status will remain for the duration they are in the server.
    When an admin assistant sends a report, to the admins their name is prefixed with [AA] to note it as a (most likely) reliable report.
    Likewise if an admin assistant is the target of a report, their name is prefixed with a clan-tag-like code.
    (e.g. Report [512]: [AA]ColColonCleaner reported [AA]SomeOtherAA for using explosives).
</p>
<h4>Advanced Usage (Auto-Handling)</h4>
<p>
    The advanced functionality of this system is now released to the public as testing is complete.
    This subsection uses your AAs as a collective human autoadmin.<br/><br/>

    Players with AA status can conditionally have their reports acted on by the internal autoadmin.
    A list of trigger words or phrases of any length can be defined in AdKats settings.
    If an AA report reason contains any of those trigger words or phrases then autoadmin will act on their report with a punish on the target player, using the reason they entered.
    This was originally intended for cases when admins are offline and unable to answer reports, but has now been added for all cases.
    If admins are offline, and the report matches criteria, autoadmin will punish the target player after 5 seconds.
    If admins are online, a 45 second window is given for the admin to act on the report before automatic handling fires.
    Admins can use any action command just like normal (e.g. @kill ID, @punish ID, etc...), but can also use the new @accept or @deny commands.
    @accept will confirm the report but take no action against the target player.
    @deny is used for bad or invalid reports, and will hurt the reporter's AA status.<br/><br/>

    Exceptions and Security Measures:
    Admins and admin assistants are protected from automatic actions by this system.
    Automatic handling will not be taken if the target of a report is an admin or another AA, a real admin must act on the report.
    Automatic action will also not be taken if the target player has already been acted on in some way in the past 60 seconds.
</p>
<h3>Player Muting</h3>
<p>
    Players can be muted using the mute command.
    Muting lasts until the end of the round.
    Players who talk in chat after being muted will be killed each time they talk (up through X chat messages).
    On the (X+1)th chat message they are kicked from the server.
    A player coming back during the same round is kicked again on their first chat message.
    No action other than kill or kick is used by this system.
    There will be no way to un-mute players, there was a reason they were muted, and they can talk again next round.
    Admins cannot mute other admins.
</p>
<h3>Player Joining</h3>
<p>
    Players can be joined using the join command.
    Joining either works off of player name or report ID.
    Issuing the command will place you in the targeted player's squad if there is room available.
    The command is available to all players, but for the general public will only operate for the same team.
    If user has TeamSwap or higher access, this will work across teams.
    If user has admin access, the target squad will be unlocked for their entry.
    NOTE: For cross-team joining, TeamSwap queues are not used, so if there is no room on the target team you will need to retry the command once room is available.
</p>
<h3>Pre-Messaging</h3>
<p>
    A list of editable pre-defined messages can be added in settings, then admins can use the message ID instead of typing the whole message in.
    Example: "@say 2" will send the second pre-defined message.
</p>
<p>
    Use @whatis [preMessageID] to find out what a particular ID links to before using it in commands.
</p>
<p>
    <b>Anywhere a reason or message is needed, a preMessage ID can be used instead.</b><br/>
    Example: 4th preMessage is "Baserape. Do not shoot uncap."<br/>
    "@punish muffinman 4" will punish them for the above reason.
    Even more useful is using report IDs with this, for example someone reports muffinman for "baseraping asshat" and gets report ID 283.
    You don't want "baseraping asshat" to be the actual reason entered, so you can just do "@punish 283 4", and he will get the proper punish message.
</p>
<h3>TeamSwap</h3>
<p>
    <b>TeamSwap is NOT an autobalancer</b>
    (look up other plugins for that functionality), it is for manual player moving only.
</p>
<p>
    TeamSwap is a server-smart player moving system which offers two major benefits over the default system.
    Normally when trying to move a player to a full team the command just fails at the server level, now the player is dropped on a queue until a slot opens on that side.
    They can keep playing on their side until that slot opens, since when it does they are immediately slain and moved over to fill it.
    Secondly it allows whitelisted (non-admin) players the ability to move themselves between teams as often as they want (within a ticket count window).
    This is currently not an available option in default battlefield aside from Procon commands since the game limits players to one switch per gaming session.
    Whitelisted players can type '@moveme' and TeamSwap will queue them.
    This is meant to be available to players outside the admin list, usually by paid usage to your community or to clan members only.
    Admins can also use '@moveme', and in their case it bypasses the ticket window restriction.
</p>
<h3>Requiring Reasons</h3>
<p>
    All commands which might lead to actions against players are required to have a reason entered, and will cancel if no reason is given.
    Players (even the most atrocious in some cases) should know what they were acted on for, and it's also a good way to hold admins accountable for their actions.
    The minimum number of characters for reasons is editable in plugin settings.
    The editable setting only applies to admin commands, and the default value is 5 characters.
    Reports and Admin calls are hardcoded to 1 character minimum reason lengths.
</p>
<h3>Setting Sync</h3>
<p>
    Plugin settings are automatically synced to layers every 5 minutes from their particular server IDs.
    All settings for each plugin instance are stored in the database by server ID.
    Enter an existing server ID in the setting import field and all settings from that instance will be imported to this instance.
    All settings on the current instance will be overwritten by the synced settings.
    Whenever a setting is changed, that change is persisted to the database.
</p>
<h3>Special Player Lists</h3>
<p>
    Special player list table "adkats_specialplayers" has been added for the 4.1.0.0 release.
    In this table, players can be added to any desired group accepted by AdKats.
    Valid groups are currently slot_reserved, slot_spectator, whitelist_multibalancer, blacklist_dispersion, and whitelist_hackerchecker.
    Players can be added by ID, or by identifier (name, guid, or IP), and can be assigned a game and server to apply them to.
    If you use player IDs then you wont need to update player names if they change their names,
    the player names will automatically update when they join the server;
    This is especially good to use when whitelisting for the hacker-checker.
    Leave fields blank to indicate wildcard, for example leaving the server column blank for player will mean it applies to all servers of their game.
    If you specify the server, the group you have them assigned to will only apply for that one server.
</p>
<h3>Internal Hacker-Checker with Whitelist</h3>
<p>
    Ever since we started running servers we never banned off of "cheat-o-meter" results, since there were too many false positives, so we built our own.
    This code has been dormant in AdKats for several months now, only activating on =ADK= servers while we tested it.
    We are releasing the fully tested BF3 and BF4 versions now.
</p>
<p>
    The hacker-checker uses BF3Stats.com and BF4Stats.com for player stats, and is able to catch both aimbots and damage mods.
    To avoid false positives, only weapons that fire bullets (no crossbow, 320, etc), and deal less than 50% damage per shot are included in the calculations.
    This removes all equipment, sniper rifles, shotguns, and heavy-hitting pistols like the magnum/rex from calculations.
    For the remaining weapons there are two checks each one goes through, customizable to your desired trigger levels.

    Info posts:
    https://forum.myrcon.com/showthread.php?6045-AdKats-Advanced-In-Game-Admin-and-Ban-Enforcer-4-0-0-0&p=90700&viewfull=1#post90700
    https://forum.myrcon.com/showthread.php?6045-AdKats-Advanced-In-Game-Admin-and-Ban-Enforcer-4-0-0-0&p=92106&viewfull=1#post92106
</p>
<h4>Damage Mod Checker</h4>
<p>
    Damage per shot for all known weapons is held on the AdKats repository, and is downloaded when the plugin enables.
    The damage per shot each player gets with that weapon is calculated from BF3Stats/BF4Stats.
    The threshold you set for this check is the percentage above normal required to trigger the ban.
    We have ours set at 50% above normal damage (just 50 in the setting).
    e.g. A weapon is dealing at least 150% of the damage it normally should. (A 25 DPS assault rifle is dealing 37.5+ DPS)
    Every ban on trigger level 50 on our servers has been examined personally, and this check has never triggered a false positive.
    50 kills with the weapon in question are required to trigger a ban using this check.
</p>
<h4>Aimbot Checker</h4>
<p>
    For this check only automatic weapons from specific categories are used in the calculation.
    This includes Sub Machine Guns, Assault Rifles, Carbines, and Machine Guns.
    Handguns, snipers, equipment, etc are not included since their HSK values can vary drastically.
    This limit is simple, if the headshot/kill percentage for any valid weapon is greater than your trigger level, the ban is issued.
    HS/K percentage for even the top competitive players caps at 38%, so we set our value much higher than that.
    We started with 70% HS/K, and no false positives were found with that value, but lower as desired.
    The minimum we allowed during testing was 50%.
    100 kills with the weapon in question are required to trigger this check.
</p>
<h4>KPM Checker</h4>
<p>
    Be careful with this one, this is where a lot of legit competitive players reside.
    This check should only be used to request video gameplay of players to prove their play, then whitelist the player.
    For this check all weapons aside from melee weapons and equipment are included.
    This includes Sub Machine Guns, Assault Rifles, Carbines, Machine Guns, Handguns, and Sniper Rifles.
    This check uses weapon time and total kills, rather simple, just kills/total minutes.
    If that value is greater than your trigger level the ban is issued.
    After some research and testing the value used on our servers is the default, 4.5.
    100 kills with the weapon in question are required to trigger this check.
</p>
<h4>Posting Method</h4>
<p>
    The heaviest hacked weapon (the one farthest above normal) is the one displayed in the ban reason using the following formats:<br/>
    Damage Mod Bans:<br/>
    Hacking/Cheating DPS Automatic Ban [WEAPONNAME-DPS-KILLS-HEADSHOTS]<br/>
    Aimbot Bans:<br/>
    Hacking/Cheating HSK Automatic Ban [WEAPONNAME-HSK-KILLS-HEADSHOTS]<br/>
    KPM Bans:<br/>
    Hacking/Cheating KPM Automatic Ban [WEAPONNAME-KPM-KILLS-HEADSHOTS]
</p>
<p>
    Damage mod bans take priority over aimbot bans.
    If you want to whitelist a player from a server, enter their player name, guid, or IP in the whitelist array for each server.
    We will add database support for whitelisting in a later version.
    If a player is not found on BF3Stats or BF4Stats, AdKats will keep checking for stats every couple minutes while they are in the server, stopping if they leave.
</p>
<h3>Commanding AdKats from External Source</h3>
<h4>AdKats WebAdmin can be used for this.</h4>
<p>
    If you have an external system (such as a web-based tool with access to bf3 server information), then there is currently one way to interact with AdKats externally (A second coming soon if possible).
</p>
<h4>Adding Database Records</h4>
<p>
    Have your external system add a row to the record table with a new record to be acted on.
    All information is needed in the row just like the ones sent from AdKats to the database.
    Review the ones already in your database before attempting this, and ask ColColonCleaner any questions you may have.
    The only exception is you need to make the 'adkats_read' column for that row = "N", this way AdKats will act on that record.
    Every 5-10 seconds the plugin checks for new input in the table, and will act on them if found.
</p>
<h4>Using external plugin API</h4>
<p>
    Two available MatchCommands have been added, one for issuing commands through AdKats, and the second for fetching admin lists.
    These can be called by other plugins to integrate their functionality with AdKats and its database.
<h5>FetchAuthorizedSoldiers</h5>
Plugin: AdKats<br/>
Method: FetchAuthorizedSoldiers<br/>
Parameters:
<ul>
    <li><b>caller_identity</b> String with ID unique to the plugin sending the request. No whitespace or special characters. e.g. "InsaneLimits"</li>
    <li><b>response_requested</b> true</li>
    <li><b>response_class</b> Class/plugin where the callback will be sent.</li>
    <li><b>response_method</b> Method within the target plugin that will accept the response</li>
    <li><b>user_subset</b> "admin", "elevated", or "all". Admin meaning they have access to player interaction commands, elevated meaning they do not. Returns all soldiers in that subset.</li>
    <li><b>user_role </b> Returns all soldiers belonging to users in a specific role.</li>
</ul>
(user_subset and user_role cannot be used at the same time, pick one or the other.)<br/><br/>
Response:
<ul>
    <li><b>caller_identity</b> AdKats</li>
    <li><b>response_requested</b> false</li>
    <li><b>response_type</b> FetchAuthorizedSoldiers</li>
    <li><b>response_value</b> List of soldiers that matched the given parameters. CPluginVariable.EncodeStringArray used to compact into one field. CPluginVariable.DecodeStringArray can be used to parse the field back into an array.</li>
</ul>
<h5>IssueCommand</h5>
Plugin: AdKats<br/>
Method: IssueCommand<br/>
Parameters:
<ul>
    <li><b>caller_identity</b> String with ID unique to the plugin sending the request. No whitespace or special characters. e.g. "InsaneLimits"</li>
    <li><b>response_requested</b> true/false. Whether the caller would like a response with the outcome of the command.</li>
    <li><b>response_class</b> Only if response_requested is true. Class/plugin where the callback will be sent.</li>
    <li><b>response_method</b> Only if response_requested is true. Method within the target plugin that will accept the response.</li>
    <li><b>command_type</b> Command key that references the desired command. Examples: player_kill, player_ban_perm, admin_say.</li>
    <li><b>command_numeric</b> Used for commands like player_ban_temp that require a numerical input. Currently player_ban_temp is the only command that requires a command numeric, and will throw errors if a numerica is not provided. In all other cases this field is optional.</li>
    <li><b>source_name</b> Name of the source you would like database logged. For example an admin name, plugin name, or a custom name like AutoAdmin.</li>
    <li><b>target_name</b> The exact name of the target you would like to issue the command against, usually a player name. For commands like admin_nuke which don't accept a player name, special syntax is used, documentation of such is provided in the readme.</li>
    <li><b>target_guid</b> Only required when binding to onJoin, onLeave, or other events where the player may not be loaded into AdKats' live player list yet. If the player cannot be found in the live player list by target_name then this guid is used to fetch their information from the database and perform the command.</li>
    <li><b>record_message</b> The message or reason that should be used with the command. e.g. Baserape. Message can be up to 500 characters.</li>
</ul>
Response:
<ul>
    <li><b>caller_identity</b> AdKats</li>
    <li><b>response_requested</b> false</li>
    <li><b>response_type</b> IssueCommand</li>
    <li><b>response_value</b> List of all messages sent for the command, comparable to what an admin would see in-game. CPluginVariable.EncodeStringArray used to compact into one field. CPluginVariable.DecodeStringArray can be used to parse the field back into an array. If the command succeeds withouth issue there should (generally) only be one message.</li>
</ul>
If all the required parameters are provided, the command will execute and log to the database. Response sent if it was requested.<br/>
<br/>
Example:<br/>
var requestHashtable = new Hashtable{<br/>
{"caller_identity", "SomePlugin"},<br/>
{"response_requested", false},<br/>
{"command_type", "player_ban_perm"},<br/>
{"source_name", "AutoTest"},<br/>
{"target_name", "ColColonCleaner"},<br/>
{"target_guid", "EA_698E70AF4E420A99824EA9A438FE3CB1"},<br/>
{"record_message", "Testing"}<br/>
};<br/>
ExecuteCommand("procon.protected.plugins.call", "AdKats", "IssueCommand", JSON.JsonEncode(requestHashtable));
</p>
<h3>Available In-Game Commands</h3>
<p>
    <u><b>You can edit the text for each command to suit your needs in plugin settings.</b></u>
</p>
<p>
    Commands can be accessed with '@', '!', '/!', '/@', or just '/'.
</p>
<p>
    Any action command given with no parameters (e.g. '@kill') will target the speaker. If admins want to kill, kick, or
    even ban themselves, simply type the command without any parameters.
    Any action command when given a player name (other than moving players) will require a reason.
</p>
<table>
<tr>
    <td><b>Command</b></td>
    <td><b>Default Text</b></td>
    <td><b>Params</b></td>
    <td><b>Description</b></td>
</tr>
<tr>
    <td><b>Kill Player</b></td>
    <td>kill</td>
    <td>
        None<br/>
        OR<br/>
        [player][reason]<br/>
        OR<br/>
        [reportID]<br/>
        OR<br/>
        [reportID][reason]
    </td>
    <td>The in-game command used for killing players.</td>
</tr>
<tr>
    <td><b>Kick Player</b></td>
    <td>kick</td>
    <td>
        None<br/>
        OR<br/>
        [player][reason]<br/>
        OR<br/>
        [reportID]<br/>
        OR<br/>
        [reportID][reason]
    </td>
    <td>The in-game command used for kicking players.</td>
</tr>
<tr>
    <td><b>Temp-Ban Player</b></td>
    <td>tban</td>
    <td>
        [time]<br/>
        OR<br/>
        [time][player][reason]<br/>
        OR<br/>
        [time][reportID]<br/>
        OR<br/>
        [time][reportID][reason]
    </td>
    <td>
        The in-game command used for temp-banning players.
        Default time is in minutes, but the number can have a letter after it designating the units. e.g. 2h for 2 hours. Valid suffixes are m, h, d, w, and y.
    </td>
</tr>
<tr>
    <td><b>Perma-Ban Player</b></td>
    <td>ban</td>
    <td>
        None<br/>
        OR<br/>
        [player][reason]<br/>
        OR<br/>
        [reportID]<br/>
        OR<br/>
        [reportID][reason]
    </td>
    <td>The in-game command used for perma-banning players.</td>
</tr>
<tr>
    <td><b>Unban Player</b></td>
    <td>unban</td>
    <td>
        [player]
    </td>
    <td>
        The in-game command used for unbanning players. Only usable when ban enforcer is enabled.
    </td>
</tr>
<tr>
    <td><b>Punish Player</b></td>
    <td>punish</td>
    <td>
        None<br/>
        OR<br/>
        [player][reason]<br/>
        OR<br/>
        [reportID]<br/>
        OR<br/>
        [reportID][reason]
    </td>
    <td>
        The in-game command used for punishing players.
        Will add a Punish record to the database, increasing a player's total points by 1.
    </td>
</tr>
<tr>
    <td><b>Forgive Player</b></td>
    <td>forgive</td>
    <td>
        None<br/>
        OR<br/>
        [player][reason]<br/>
        OR<br/>
        [reportID]<br/>
        OR<br/>
        [reportID][reason]
    </td>
    <td>
        The in-game command used for forgiving players.
        Will add a Forgive record to the database, decreasing a player's total points by 1.
    </td>
</tr>
<tr>
    <td><b>Mute Player</b></td>
    <td>mute</td>
    <td>
        None<br/>
        OR<br/>
        [player][reason]<br/>
        OR<br/>
        [reportID]<br/>
        OR<br/>
        [reportID][reason]
    </td>
    <td>
        The in-game command used for muting players.
        Players will be muted till the end of the round, X kills then kick if they keep talking.
        Admins cannot be muted.
    </td>
</tr>
<tr>
    <td><b>Move Player</b></td>
    <td>move</td>
    <td>
        None<br/>
        OR<br/>
        [player]<br/>
        OR<br/>
        [reportID]
    </td>
    <td>
        The in-game command used for moving players between teams.
        Will add players to the "on-death" move list, when they die they will be sent to TeamSwap.
    </td>
</tr>
<tr>
    <td><b>Force-Move Player</b></td>
    <td>fmove</td>
    <td>
        None<br/>
        OR<br/>
        [player]<br/>
        OR<br/>
        [reportID]
    </td>
    <td>
        The in-game command used for force-moving players between teams.
        Will immediately send the given player to TeamSwap.
    </td>
</tr>
<tr>
    <td><b>Join Player</b></td>
    <td>join</td>
    <td>
        [player]<br/>
        OR<br/>
        [reportID]
    </td>
    <td>
        The in-game command used for joining player's squads.
        Will immediately send the speaker to the target if possible, within access limitations.
    </td>
</tr>
<tr>
    <td><b>TeamSwap Self</b></td>
    <td>moveme</td>
    <td>None</td>
    <td>
        The in-game command used for moving yourself between teams.
        Will immediately send the speaker to TeamSwap.
    </td>
</tr>
<tr>
    <td><b>Report Player</b></td>
    <td>report</td>
    <td>[player][reason]</td>
    <td>
        The in-game command used for reporting players.
        Must have a reason, and will inform a player otherwise when used incorrectly.
        Will log a Report in the database (External GCP pulls from there for external admin notifications), and notify all in-game admins.
        Informs the reporter and admins of the report ID, which the punish system can use.
    </td>
</tr>
<tr>
    <td><b>Call Admin</b></td>
    <td>admin</td>
    <td>[player][reason]</td>
    <td>
        The in-game command used for calling admin attention to a player.
        Same deal as report, but used for a different reason.
        Informs the reporter and admins of the report ID, which the punish system can use.
    </td>
</tr>
<tr>
    <td><b>Admin Accept</b></td>
    <td>accept</td>
    <td>
        [reportID]
    </td>
    <td>
        The in-game command used for accepting reports as confirmed.
    </td>
</tr>
<tr>
    <td><b>Admin Deny</b></td>
    <td>deny</td>
    <td>
        [reportID]
    </td>
    <td>
        The in-game command used for denying reports.
    </td>
</tr>
<tr>
    <td><b>Admin Say</b></td>
    <td>say</td>
    <td>
        [message]<br/>
        OR<br/>
        [preMessageID]
    </td>
    <td>
        The in-game command used to send a message through admin chat to the whole server.
    </td>
</tr>
<tr>
    <td><b>Player Say</b></td>
    <td>psay</td>
    <td>
        [player][message]<br/>
        OR<br/>
        [player][preMessageID]
    </td>
    <td>
        The in-game command used for sending a message through admin chat to only a specific player.
    </td>
</tr>
<tr>
    <td><b>Admin Yell</b></td>
    <td>yell</td>
    <td>
        [message]<br/>
        OR<br/>
        [preMessageID]
    </td>
    <td>
        The in-game command used for to send a message through admin yell to the whole server.
    </td>
</tr>
<tr>
    <td><b>Player Yell</b></td>
    <td>pyell</td>
    <td>
        [player][message]<br/>
        OR<br/>
        [player][preMessageID]
    </td>
    <td>
        The in-game command used for sending a message through admin yell to only a specific player.
    </td>
</tr>
<tr>
    <td><b>Admin Tell</b></td>
    <td>tell</td>
    <td>
        [message]<br/>
        OR<br/>
        [preMessageID]
    </td>
    <td>
        The in-game command used for to send a message through both admin say and admin yell to the whole server.
    </td>
</tr>
<tr>
    <td><b>Player Tell</b></td>
    <td>ptell</td>
    <td>
        [player][message]<br/>
        OR<br/>
        [player][preMessageID]
    </td>
    <td>
        The in-game command used for sending a message through both admin say and admin yell to only a specific player.
    </td>
</tr>
<tr>
    <td><b>What Is</b></td>
    <td>whatis</td>
    <td>[preMessageID]</td>
    <td>
        The in-game command used for finding out what a particular preMessage ID links to.
    </td>
</tr>
<tr>
    <td><b>Lead</b></td>
    <td>lead</td>
    <td>
        none
    </td>
    <td>
        The in-game command used to the speaker to leader of their current squad. Only available in BF4.
    </td>
</tr>
<tr>
    <td><b>Admins</b></td>
    <td>admins</td>
    <td>
        none
    </td>
    <td>
        The in-game command used to get the list of current online admins.
    </td>
</tr>
<tr>
    <td><b>VOIP</b></td>
    <td>voip</td>
    <td>None</td>
    <td>
        The in-game command used for sending VOIP server info to the speaker.
    </td>
</tr>
<tr>
    <td><b>Kill Self</b></td>
    <td>killme</td>
    <td>None</td>
    <td>
        The in-game command used for killing the speaker.
    </td>
</tr>
<tr>
    <td><b>Restart Level</b></td>
    <td>restart</td>
    <td>None</td>
    <td>
        The in-game command used for restarting the round.
    </td>
</tr>
<tr>
    <td><b>Run Next Level</b></td>
    <td>nextlevel</td>
    <td>None</td>
    <td>
        The in-game command used for running the next map in current rotation, but keep all points and KDRs from this round.
    </td>
</tr>
<tr>
    <td><b>End Round</b></td>
    <td>endround</td>
    <td>[US/RU]</td>
    <td>
        The in-game command used for ending the current round with a winning team. Either US or RU.
    </td>
</tr>
<tr>
    <td><b>Nuke Server</b></td>
    <td>nuke</td>
    <td>[US/RU/ALL]</td>
    <td>
        The in-game command used for killing all players on a team. US, RU, or ALL will work.
    </td>
</tr>
<tr>
    <td><b>Kick All Players</b></td>
    <td>kickall</td>
    <td>[none]</td>
    <td>
        The in-game command used for kicking all players except admins.
    </td>
</tr>
<tr>
    <td><b>Confirm Command</b></td>
    <td>yes</td>
    <td>None</td>
    <td>
        The in-game command used for confirming other commands when needed.
    </td>
</tr>
<tr>
    <td><b>Cancel Command</b></td>
    <td>no</td>
    <td>None</td>
    <td>
        The in-game command used to cancel other commands when needed.
    </td>
</tr>
</table>
<h2>Settings</h2>
<h3>0. Instance Settings:</h3>
<ul>
    <li><b>'Auto-Enable/Keep-Alive'</b> - When this is enabled, AdKats will auto-recover from shutdowns and auto-restart if disabled.</li>
</ul>
<h3>1. Server Settings:</h3>
<ul>
    <li><b>'Server ID (Display)'</b> - ID of this server. Automatically set via the database.</li>
    <li><b>'Server IP (Display)'</b> - IP address and port of this server. Automatically set via Procon.<br/></li>
    <li><b>'Setting Import'</b> - Enter an existing server ID here and all settings from that instance will be imported here. All settings on this instance will be overwritten.<br/></li>
</ul>
<h3>2. MySQL Settings:</h3>
<ul>
    <li><b>'MySQL Hostname'</b> - Hostname of the MySQL server AdKats should connect to.</li>
    <li><b>'MySQL Port'</b> - Port of the MySQL server AdKats should connect to, most of the time it's 3306.</li>
    <li><b>'MySQL Database'</b> - Database name AdKats should use for storage. Creation script given in database section.</li>
    <li><b>'MySQL Username'</b> - Username of the MySQL server AdKats should connect to.</li>
    <li><b>'MySQL Password'</b> - Password of the MySQL server AdKats should connect to.</li>
</ul>
<h3>3. User Settings:</h3>
<ul>
    <li><b>'Add User'</b> - Add a user to the user list by entering their unique username here.<br/></li>
    <li><b>*User Email*</b> - Current stored email of the listed user.<br/></li>
    <li><b>*User Role*</b> - Current role of the listed user.<br/></li>
    <li><b>*Delete User?*</b> - Type delete in this line to delete the listed user.<br/></li>
    <li><b>*Add Soldier?*</b> - Type a logged soldier name in this line to connect it to the listed user. Unique soldiers cannot be connected to more than one user at a time.<br/></li>
    <li><b>*Delete Soldier?*</b> - Type delete in this line to remove the listed soldier connection from the user.<br/></li>
    </li>
</ul>
<h3>4. Role Settings:</h3>
<ul>
    <li><b>'Add Role'</b> - Type a new role name in this line to add a new role. Role names must be unique.</li>
</ul>
<p>
    Listed below "Add Role" are all the command assignments for each role.
    Change Allow/Deny for each command for the different user roles to control their access.
    Type delete in the "delete?" line to delete the user role.
    When a user role is deleted, all users on that role are changed to the Default Guest role.
</p>
<h3>5. Command Settings:</h3>
<ul>
    <li><b>'Minimum Required Reason Length'</b> - The minimum length a reason must be for commands that require a reason to execute. This only applies to admin commands.</li>
    <li><b>'Allow Commands from Admin Say'</b> - When this is enabled, all admins with procon access have unrestricted access to all enabled commands through procon's chat tab. When issuing commands through procon's chat tab, the commands must be prefixed with a / for them to work.</li>
</ul>
<h3>6. Command List:</h3>
<ul>
    <li><b>*Active*</b> - Globally disable or enable the given command.</li>
    <li><b>*Logging*</b> - Set whether usage of this command is logged. All commands log by default except roundwhitelist.</li>
    <li><b>*Text*</b> - Globally change the in-game text for this command. Command text must be unique.</li>
</ul>
<h3>7. Punishment Settings:</h3>
<ul>
    <li><b>'Punishment Hierarchy'</b> - List of punishments in order from lightest to most severe. Index in list is the action taken at that number of points.</li>
    <li><b>'Combine Server Punishments'</b> - Whether to make punishes from all servers on this database affect players on this server. Default is false.</li>
    <li><b>'Only Kill Players when Server in low population'</b> - When server population is below 'Low Server Pop Value', only kill players, so server does not empty. Player points will be incremented normally.</li>
    <li><b>'Low Server Pop Value'</b> - Number of players at which the server is deemed 'Low Population'.</li>
    <li><b>'Use IRO Punishment'</b> - Whether the IRO punishment described in the infraction tracking docs will be used.</li>
    <li><b>'IRO Timeout Minutes'</b> - Number of minutes after a punish that IRO status expires for the next punish.</li>
    <li><b>'IRO Punishment Overrides Low Pop'</b> - When punishing players, if a player gets an IRO punish, it will ignore whether server is in low population or not.</li>
</ul>
<h3>8. Email Settings:</h3>
<ul>
    <li><b>'Send Emails.'</b> - Whether sending emails will be enabled. By default the adkatsbattlefield gmail account will be used to send emails. When this is true, all reports and admin calls will be send to the supplied email addresses on users and the extra email list.</li>
    <li><b>'Use SSL?' - Whether SSL will be used in connection to given SMTP server.</b></li>
    <li><b>'SMTP-Server address.' - Address of the SMTP server.</b></li>
    <li><b>'SMTP-Server port.'</b> - Port to use for the SMTP server.</li>
    <li><b>'Sender address.'</b> - The email address used to send all emails.</li>
    <li><b>'SMTP-Server username.'</b> - The username used to authenticate into the SMTP server.</li>
    <li><b>'SMTP-Server password.'</b> - The password used to authenticate into the SMTP server.</li>
    <li><b>'Custom HTML Addition.'</b> - Custom HTML to add to the end of each email. String replacements include. %player_id%, $player_name%, %player_guid%, %player_pbguid%, and %player_ip%.</li>
    <li><b>'Extra Recipient Email Addresses.'</b> - List of all extra email addresses beside user email addresses that you would like to blast.</li>
</ul>
<h3>9. TeamSwap Settings:</h3>
<ul>
    <li><b>'Ticket Window High'</b> - When either team is above this ticket count, nobody (except admins) will be able to use TeamSwap.</li>
    <li><b>'Ticket Window Low'</b> - When either team is below this ticket count, nobody (except admins) will be able to use TeamSwap.</li>
</ul>
<h3>A10. Admin Assistant Settings:</h3>
<ul>
    <li><b>'Enable Admin Assistants'</b> - Whether admin assistant statuses can be assigned to players.</li>
    <li><b>'Minimum Confirmed Reports Per Month'</b> - How many confirmed reports the player must have in the past month to be considered an admin assistant.</li>
    <li><b>'Enable Admin Assistant Perk'</b> - Whether admin assistants will get the TeamSwap perk for their help.</li>
    <li><b>'Use AA Report Auto Handler'</b> - Whether the internal auto-handling system for admin assistant reports is enabled.</li>
    <li><b>'Auto-Report-Handler Strings'</b> - List of trigger words/phrases that the auto-handler will act on. One per line.</li>
</ul>
<h3>A11. Player Mute Settings:</h3>
<ul>
    <li><b>'On-Player-Muted Message'</b> - The message given to players when they are muted by an admin.</li>
    <li><b>'On-Player-Killed Message'</b> - The message given to players when they are killed for talking in chat after muting.</li>
    <li><b>'On-Player-Kicked Message'</b> - The message given to players when they are kicked for talking more than X times in chat after muting.</li>
    <li><b>'# Chances to give player before kicking'</b> - The number of chances players get to talk after being muted before they are kicked. After testing, 5 appears to be the perfect number, but change as desired. </li>
</ul>
<h3>A12. Messaging Settings:</h3>
<ul>
    <li>
        <b>'Display Admin Name in Kick and Ban Announcement'</b>
        When players are kicked or banned from the server, the whole server is told.
        This changes whether the message includes the kicking or banning admin name, instead of just "admin".
    </li>
    <li><b>'Yell display time seconds'</b> - The integer time in seconds that yell messages will be displayed.</li>
    <li><b>'Pre-Message List'</b> - List of messages for use in pre-say and pre-yell commands.</li>
    <li><b>'Require Use of Pre-Messages'</b> - Whether using pre-messages in commands is required instead of custom messages.</li>
</ul>
<h3>A13. Banning Settings:</h3>
<ul>
    <li><b>'Use Additional Ban Message'</b> - Whether to have an additional message append on each ban.</li>
    <li><b>'Additional Ban Message'</b> - Additional ban message to append on each ban. e.g. "Dispute at www.yourclansite.com"</li>
    <li><b>'Procon Ban Admin Name'</b> - Admin name that will be used for bans filed via procon.</li>
</ul>
<h3>A13-2. Ban Enforcer Settings:</h3>
<ul>
    <li><b>'Use Ban Enforcer'</b> - Whether to use the internal AdKats Ban Enforcer.</li>
    <li><b>'Enforce New Bans by NAME'</b> - Whether to use a player's name to ban them. (Insecure, players can change their names)</li>
    <li><b>'Enforce New Bans by GUID'</b> - Whether to use a player's EA GUID to ban them. (Secure, players cannot change their GUIDs)</li>
    <li><b>'Enforce New Bans by IP'</b> - Whether to use a player's IP Address to ban them. (Somewhat secure, experienced players can change their IP, and IP bans can hit multiple players.)</li>
</ul>
<h3>A13-3. Mini Ban Management:</h3>
<ul>
    <li><b>'NAME Ban Count'</b> - How many NAME bans are currently being enforced by AdKats Ban Enforcer.</li>
    <li><b>'GUID Ban Count'</b> - How many EA GUID bans are currently being enforced by AdKats Ban Enforcer.</li>
    <li><b>'IP Ban Count'</b> - How many IP bans are currently being enforced by AdKats Ban Enforcer.</li>
    <li><b>'Ban Search'</b> - Enter a full or partial player name here and AdKats will display all ACTIVE matching bans.</li>
</ul>
<h3>A14. External Command Settings:</h3>
<ul>
    <li><b>'External Access Key'</b> - The access key required to use any HTTP commands, can be changed to whatever is desired, but the default is a random 64Bit hashcode generated when the plugin first runs. </li>
    <li><b>'Fetch Actions from Database'</b> - Whether to use the database as a source for new commands.</li>
</ul>
<h3>A15. VOIP Settings:</h3>
<ul>
    <li><b>'Server VOIP Address'</b> - String that will be sent to players using the VOIP command.</li>
</ul>
<h3>A16. Orchestration Settings:</h3>
<ul>
    <li><b>'Feed MULTIBalancer Whitelist'</b> - When enabled, the adkats_specialplayers table (group: whitelist_multibalancer) is used to feed MULTIBalancer's player whitelist.</li>
    <li><b>'Feed MULTIBalancer Even Dispersion List'</b> - When enabled, the adkats_specialplayers table (group: blacklist_dispersion) is used to feed MULTIBalancer's even dispersion list.</li>
    <li><b>'Feed Server Reserved Slots'</b> - When enabled, the servers reserved slots will include all AdKats user's soldiers.</li>
    <li><b>'Feed Server Spectator List'</b> - When enabled, the servers spectator list will include all AdKats user's soldiers.</li>
    <li><b>'Feed Stat Logger Settings'</b> - When enabled, stat logger is fed settings appropriate for AdKats, including correct database time offset, instant chat logging, etc.
        <p>
            The following settings are sent to stat logger when using the "Feed Stat Logger Settings" orchestration option:
            <ul>
                <li>"Servertime Offset" (TIME OFFSET CONVERSION TO UTC TIME)</li>
                <li>"Enable Chatlogging?" "Yes"</li>
                <li>"Log ServerSPAM?" "Yes"</li>
                <li>"Instant Logging of Chat Messages?" "Yes"</li>
                <li>"Enable Statslogging?" "Yes"</li>
                <li>"Enable Weaponstats?" "Yes"</li>
                <li>"Enable Livescoreboard in DB?" "Yes"</li>
                <li>"Enable KDR correction?" "Yes"</li>
                <li>"MapStats ON?" "Yes"</li>
                <li>"Session ON?" "Yes"</li>
                <li>"Save Sessiondata to DB?" "Yes"</li>
                <li>"Log playerdata only (no playerstats)?" "No"</li>
            </ul>
        </p>
    </li>
</ul>
<h3>A17. Round Settings:</h3>
<ul>
    <li><b>'Round Timer: Enable'</b> - When enabled, rounds will be limited to X minutes.</li>
    <li><b>'Round Timer: Round Duration Minutes'</b> - Number of minutes that the round will last before the current winning team wins (Will only work correctly in conquest at the moment).</li>
</ul>
<h3>A18. Internal Hacker-Checker Settings:</h3>
<ul>
    <li><b>'HackerChecker: Enable'</b> - Whether the internal BF3Stats hacker-checker is enabled.</li>
    <li><b>'HackerChecker: DPS Checker: Enable'</b> - Whether the Damage Mod portion of the hacker-checker is enabled.</li>
    <li>
        <b>'HackerChecker: DPS Checker: Trigger Level'</b> -
        The percentage over normal weapon damage that will cause a ban.
        50 kills minimum to trigger.
        After 3 months of testing, 50 is the best value, and has not issued a single false positive in that time.
    </li>
    <li><b>'HackerChecker: DPS Checker: Ban Message'</b> - Message prefix to use when banning for damage mod.</li>
    <li><b>'HackerChecker: HSK Checker: Enable'</b> - Whether the Aimbot portion of the hacker-checker is enabled.</li>
    <li><b>'HackerChecker: HSK Checker: Trigger Level'</b> -
        The headshot/kill ratio for automatic weapons that will trigger a ban.
        100 kills minimum to trigger.
        After 3 months of testing, we suggest setting between 50 and 70 depending on the severity you want to enforce.
        You will get some false positives down near 50 but will catch many more aimbotters, setting near 70 will not result in any false positives but also wont catch as many aimbotters.
    </li>
    <li><b>'HackerChecker: HSK Checker: Ban Message'</b> - Message prefix to use when banning for high KPM.</li>
    <li><b>'HackerChecker: KPM Checker: Enable'</b> - Whether the KPM portion of the hacker-checker is enabled.</li>
    <li><b>'HackerChecker: KPM Checker: Trigger Level'</b> - Kills-per-minute with any included weapon that will trigger the ban.
    </li>
    <li><b>'HackerChecker: KPM Checker: Ban Message'</b> - Message prefix to use when banning for high KPM.</li>
</ul>
<h3>A19. Server Rules Settings:</h3>
<ul>
    <li><b>'Rule Print Delay'</b> - Delay in seconds after the command is issued that commands start being sent to the player.</li>
    <li><b>'Rule Print Interval'</b> - Number of seconds between each rule being sent to the player.</li>
    <li><b>'Server Rule List'</b> - List of rules for the server. Raw messages can be used here, or alternatively pre-message IDs.</li>
</ul>
<h3>D99. Debug Settings:</h3>
<ul>
    <li><b>'Debug level'</b> -
        Indicates how much debug-output is printed to the plugin-console.
        0 turns off debug messages (just shows important warnings/exceptions/success), 7 documents nearly every step.
        Don't edit unless you really want to be spammed with console logs, it will also slow down the plugin when turned up.
    </li>
    <li><b>'Debug Soldier Name'</b> -
        When this soldier issues commands in your server, the time for any command to complete is told in-game.
        Duration is from the time you entered the message, until all aspects of the command have been completed.
    </li>
    <li><b>'Command Entry'</b> -
        Enter commands here just like in game, mainly for debug purposes. Don't let more than one person use this at any time.
    </li>
</ul>
