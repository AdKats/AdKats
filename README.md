[//]: # "<latest_stable_release>7.0.1.0</latest_stable_release>"
<p>
    <a name=adkats />
    <img src="https://i.imgur.com/r9pwH3A.png" alt="AdKats Advanced In-Game Admin Tools">
</p>
<p>
    <b>New Extension! Click below to enforce BF4 loadouts on-spawn!</b>
</p>
<p>
    <a href="https://forum.myrcon.com/showthread.php?9373-On-Spawn-Loadout-Enforcer-for-Infantry-Vehicles-AdKatsLRT-2-0-0-0" name=thread>
        <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Loadout.jpg" alt="AdKats Thread">
    </a>
</p>
<h2>Overview</h2>
<p>
    Admin Toolset with a plethora of features, over 100 available in-game commands, and many customization options.
    AdKats focuses on making in-game admins more efficient and accurate at their jobs, with flexibility for almost any setup.
    Includes a cross-server ban enforcer with advanced enforcement features, metabans support, global admin management, cross-server player messaging, and the BFAdminCP 2.0+ for web-based control has been released.
    Designed for groups with high-traffic servers and many admins, but will function just as well for small servers.
</p>
<ul>
    <li>
        <b>Extensive In-Game Commands.</b>
        Commands for player killing, kicking, punishing, banning, unbanning, moving, joining, whitelisting, messaging,
        etc, etc... 
        Over 100 available in-game commands. 
        Commands can be accessed from in-game, Procon's chat window, database, and from other plugins.
    </li>
    <li>
        <b>Customizable User Roles.</b>
        Custom user roles can be created for admins and players with each role given access to only the commands you want them to use. 
        Default guest role is given to all players and can be edited to your desired specs. 
        Roles and powers are automatically synced between servers so you only need to change user information once. 
        Soldiers assigned to users will also keep their powers if they change their in-game names.
    </li>
    <li>
        <b>Setting sync between servers.</b>
        Changes to plugin settings are stored in the database and can be automatically synced between your Procon layers. 
        Setting up new layers or switching layers is a breeze as the settings for existing servers are automatically imported on startup.
    </li>
    <li>
        <b>Infraction Tracking System.</b>
        Punish/forgive players for breaking rules on your servers. 
        Everything is tracked so the more infractions they commit the worse their punishment automatically gets. 
        Created so all players are treated equally based on their history, regardless of who is issuing punishments against them. 
        Heavily customizable.
    </li>
    <li>
        <b>Player Reputation System.</b>
        Based on issued commands from and against players they can form a numeric reputation on the server.
        Documentation below. 
        A local leaderboard for reputation is provided in the BFAdminCP.
    </li>
    <li>
        <b>Quick Player Report and Admin Call Handling with email support.</b>
        Notification system and quick handling features for all admin calls and player reports.
        Reports can be referenced by number for instant action. Automatic PBSS are triggered on reported players.
    </li>
    <li>
        <b>Orchestration and Server List Management.</b>
        Server reserved slots, spectator slots, autobalance whitelist through MULTIBalancer, ping kick whitelists, and several others can be automatically handled through the AdKats user list, role groups, and orchestration commands.
    </li>
    <li>
        <b>AdKats Ban Enforcer.</b>
        AdKats can enforce bans across all of your servers and can enforce on all identity metrics at the same time. 
        System will automatically import bans from your servers, consolidating them in one place, and can import existing bans from the BF3 Ban Manager plugin's tables. Full documentation below.
    </li>
    <li>
        <b>BF3/BF4 AntiCheat with Whitelist.</b>
        Battlelog stats can be polled for players in the server, issuing automatic bans for damage mods, aimbots, magic bullet, and several others. 
        The LIVE system can detect damage mods and magic bullet from a single round of play. 
        DPS checks are enabled by default, with others available after a few clicks.
    </li>
    <li>
        <b>Surrender Vote System.</b>
        When enabled, if players are stuck in their base with no options, they can vote to end the round with the current winning team as winner.
    </li>
    <li>
        <b>Auto-Surrender/Auto-Nuke System.</b>
        This uses ticket loss rates to detect where teams are on the map, specifically with how many flags are captured. 
        If a team is being base-camped, it can either automatically end the round with current winner, or nuke the team who is causing the base-camp. 
        Optimal values for Metro 2014 and Operation Locker are available, for both surrender and nuke options.
    </li>
    <li>
        <b>Automatic Updates.</b>
        AdKats automatically updates itself when stable releases are made, only requiring a Procon instance reboot to run updated versions. 
        This can be disabled if desired, but is required if running TEST versions.
    </li>
    <li>
        <b>Ping Enforcer.</b>
        Automated kick system based on ping, with moving average calculation, modifiers based on time of day and server population, customizable messages, logged kicks, and manual ping options.
    </li>
    <li>
        <b>AFK Manager.</b>
        Automated kick system based on player AFK time, with manual kick command. Customizable durations, and option to ignore chat messages counting toward active time.
    </li>
    <li>
        <b>SpamBot with Whitelist.</b>
        SpamBot with options for simultaneous say, yell, and tell. 
        Customizable intervals between each type of message, and ability to whitelist players/admins from seeing spambot messages.
        Rules can also be made map and mode specific.
    </li>
    <li>
        <b>Commander Manager.</b>
        Commanders can cause team imbalance when servers are in low population. 
        This manager can forbid commanders before a certain player count is active.
    </li>
    <li>
        <b>Cross-Server Player Messaging.</b>
        Private conversations between players can operate not only within the same server, but will work between any online server in the database, and even between any AdKats supported game.
    </li>
    <li>
        <b>Admin Assistants.</b>
        When fully used this can turn your regular player base into a human autoadmin. 
        Trusted players fill the gaps normal autoadmins don't see by utilizing the report system and keeping your server under control even when normal admins are offline.
    </li>
    <li>
        <b>Email Notification System.</b>
        Email addresses can be added to every user, and once enabled they will receive emails for player reports and admin calls.
    </li>
    <li>
        <b>TeamSpeak and Discord Integration.</b>
        AdKats can be connected to teamspeak or discord to apply perks and monitoring to players in VOIP.
    </li>
    <li>
        <b>Fuzzy Player Name Completion.</b>
        Fully completes partial or misspelled player names. 
        I've been consistently able to find almost any player only a few characters from their name. 
        Can also fetch players who have left the server, are in another server of yours on the same database, or have been in your servers at any point in time.
    </li>
    <li>
        <b>Player Muting.</b>
        Players can be muted if necessary, giving warnings and kicks if they talk. 
        Automatic mute in specific cases like language can be orchestrated by other plugins like Insane limits.
    </li>
    <li>
        <b>Player Joining.</b>
        Player's squads can be joined via command, and locked squads can be unlocked for admin entry.
    </li>
    <li>
        <b>Player Locking.</b>
        Players can be locked from admin commands for a specific timeout, the main purpose is if a certain admin is handling them (checking stats for cheat detection, records, etc.) they shouldn't be interrupted by another admin acting on the player.
    </li>
    <li>
        <b>Player Assist.</b>
        Player's want to play with their friends, but you don't want to imbalance the teams? 
        The assist command lets any player join the weak team to help them out and squad up with friends without hurting server balance.
    </li>
    <li>
        <b>Yell/Say Pre-Recording.</b>
        Use numbers to reference predefined messages. Avoid typing long reasons or messages. e.g. /kill player 3
    </li>
    <li>
        <b>Server Rule Management.</b>
        Server rules can be listed, requests for rules logged, rules targeted at other players, and rules can be distributed between servers automatically. 
        Rules can also be made map and mode specific.
    </li>
    <li>
        <b>External Controller API.</b>
        AdKats can be controlled from outside the game through systems like the BFAdminCP and through other plugins like Insane Limits. 
        For example, you can issue AdKats punish commands from Insane Limits or ProconRulz and have them logged against the player's profile like any other admin command.
    </li>
    <li>
        <b>TeamSwap.</b>
        Queued move system for servers that are consistently full, players can be queued to move to full teams once a slot opens.
    </li>
    <li>
        <b>Metabans Support.</b>
        When using ban enforcer all bans can be submitted to metabans and removed if the player is unbanned.
    </li>
    <li>
        <b>Editable In-Game Commands.</b>
        Command text, logging options, chat access types, and enable options can be edited to suit your needs.
    </li>
    <li>
        <b>Configurable automatic server restarts.</b>
        AdKats can automatically reboot your game server when it's empty.
    </li>
    <li>
        <b>Full Logging.</b>
        All admin activity is tracked via the database per your custom settings for every command, so holding your admins accountable for their actions is quick and painless.
        If you are using the BFAdminCP nobody but your highest admins will need manual Procon access.
    </li>
    <li>
        <b>Setting Lock.</b>
        The settings page in AdKats can be locked with a password. 
        This means even admins with access to plugin settings can be blocked from changes using the password.
    </li>
    <li>
        <b>Performance.</b>
        All actions, messaging, database communications, and command parsing take place on their own threads, minimizing performance impacts.
        However, AdKats is still almost the size of procon itself (logically anyway), and as such it's not suggested to run multiple instances of AdKats on a single procon layer.
    </li>
</ul>
<p>
    <a href="https://forum.myrcon.com/showthread.php?6045" name=thread>
        <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Thread.jpg" alt="AdKats Thread">
    </a>
</p>
<p>
    AdKats was inspired by the gaming community A Different Kind (=ADK=)<br/>
    Visit <a href="http://www.adkgamers.com/" target="_blank">ADKGamers.com</a> to say thanks!
</p>
<p>
    Development by Daniel J. Gradinjan (ColColonCleaner)
</p>
<p>
    If you find any bugs, please submit them <a href="https://github.com/ColColonCleaner/AdKats/issues?state=open" target="_blank">HERE</a> and they will be fixed ASAP.
</p>
<p>
    <a href="https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=danielgradinjan%40gmail%2ecom&lc=US&item_name=AdKats%20-%20Advanced%20In-Game%20Admin%20for%20Procon%20Frostbite%20-%20Donation&currency_code=USD&bn=PP%2dDonationsBF%3abtn_donate_LG%2egif%3aNonHosted" target="_blank"><img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Donate.jpg"></a>
</p>
<br/>
<HR>
<br/>
<p>
    <a name=manual />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_UserManual.jpg" alt="AdKats User Manual">
</p>
<p>
    <a name=dependencies />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Dependencies.jpg" alt="AdKats User Manual">
</p>
<h4><b>1. A MySQL Database</b></h4>
<p>
    A MySQL database accessible from your Procon layer is required. AdKats checks the database for needed tables on
    connect.<br/>
    <br/>
    <b>Getting a Database:</b>
    This plugin requires a MySQL database, and XpKiller's Stat logger plugin to operate. If you do not have an existing
    database and/or a Procon layer we suggest using Branzone's hosting services. Our group has been with them across
    BF3, BF4, and Hardline with no issues.<br/>
    <b>Web/Database Hosting Suggestion:</b><a href="https://www.branzone.com/aff.php?aff=226&pid=266" target="_blank">
    Branzone MySQL Databases</a><br/>
    <b>Procon Layer Hosting Suggestion:</b><a href="https://www.branzone.com/aff.php?aff=226&pid=192" target="_blank">
    Branzone Procon Layers</a>
</p>
<h4><b>2. XpKiller's "Procon Chat, GUID, Stats and Mapstats Logger" Plugin</b></h4>
<p>
    Stat logger was used for years before AdKats came on the scene so that database structure is used as a base for this plugin.
    AdKats will only run properly if pointed at the same database that your stat logger plugins are connected to, and also only when stat logger is running on all the servers you plan to install AdKats on.
    Running AdKats on the same layers as your stat logger instances for each server will ensure these conditions are met.
</p>
<p>
    The latest universal version of XpKiller's Stat Logger can be downloaded from here: <a
        href="https://forum.myrcon.com/showthread.php?6698" target="_blank">Procon Chat, GUID, Stats and Mapstats
    Logger</a>
</p>
<p>
    The BF3 only version of stat logger CAN be used with AdKats if you don't want to lose your old data, but is not advised.
</p>
<h4><b>3. Web Request Access</b></h4>
<p>
    AdKats uses web statistics and requests to manage players types, hack detection, user lists, and updates.
    The list of domains/sub-domains AdKats must be able to access for proper function are documented below in the
    "Web Requests" section. Whitelist these domains in your layer firewall if they cannot be accessed.
</p>
<HR>
<p>
    <a name=install />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Install.jpg" alt="AdKats User Manual">
</p>
<p>
    <b>WARNINGS. Read these before install.</b>
</p>
<p>
    <b>Running AdKats on a Linux environment is not advised or fully supported.</b>
    Testing on linux environments has been limited for AdKats. Some issues have been mitigated, namely issues with connecting over HTTPS for plugin documentation, updates, and startup, but this was necessary because the plugin wouldn't start otherwise. However, beyond that there has been very little full fledged testing on linux environments for this plugin, and there are so many facets of the plugin now I cannot guarantee functionality on that platform.
</p>
<p>
    <b>DO NOT run more than one instance of AdKats on a single procon layer at the same time.</b>
    Procon provides the ability to have a single layer monitor multiple servers at the same time, which is normally a good thing. However, over the years AdKats has grown larger than procon itself logic-wise and running multiple instances of AdKats on the same process can result in heavy performance issues and instability. Do not use procon's multiple-server-per-layer functionality unless only ONE of the servers on the layer has AdKats running. Each server should have its own dedicated layer when using this plugin. Splitting off AdKats into its own admin tool separate from procon has been brought up many times but the effort required at this point would not be worth the benefit.
</p>
<p>
    <b>INSTALL INSTRUCTIONS.</b>
</p>
<ol>
    <li>
        <b>Install XpKiller's Stat logger plugin.</b>
        Download and install the latest universal version of XpKiller's
        <a href="https://forum.myrcon.com/showthread.php?6698" target="_blank">Procon Chat, GUID, Stats and Mapstats Logger</a>.
        Make sure stat logger is running without error for a few minutes after its own installation BEFORE you attempt to do anything with AdKats installation. It needs to add an entry to the tbl_server table before AdKats can properly start up.
        If you are already running the BF3 only version of stat logger, that is fine, but the universal version is preferred for full functionality.
    </li>
    <li>
        <b style='color:#DF0101;'>GO BACK TO STEP 1 AND INSTALL STAT LOGGER.</b>
        Far too many people have posted issues because they refuse to follow instructions.
        DO NOT attempt to install AdKats until stat logger is running without issue.
    </li>
    <li>
        <b>Set up the database.</b>
        Run the contents of the <a href="https://raw.github.com/ColColonCleaner/AdKats/master/adkats.sql" target="_blank">AdKats Database Setup Script</a> on your database, on the same schema stat logger uses. You can do this via PHPMyAdmin or through a client such as MySQL Workbench, either will work just fine. You can use the same database/schema for all of your servers, you don't need a separate database for each server.
        <b><u>WARNING: If you already have AdKats installed and running this script will completely wipe your existing data for the plugin and all of your settings.</u></b>
        The script must be run by an account with permissions to create <b>tables, triggers, and stored procedures</b>.
    </li>
    <li>
        <b>Download the plugin.</b>
        Download the <a href="https://sourceforge.net/projects/adkats/files/latest/download" target="_blank">Latest
        Stable Release of AdKats</a>. If you want to try the latest features/fixes which aren't part of the stable release yet then just use the /pupdate command once your instance of AdKats is running, then reboot your procon layer.
    </li>
    <li>
        <b>Add the plugin to Procon.</b>
        Add the plugin file (AdKats.cs) to Procon as you would any other, in either the plugins/BF3 or plugins/BF4
        folder depending on which game your layer is running on. 
	No other files are required on the layer side, only the AdKats.cs file in the proper location according to your game.
    </li>
    <li>
        <b>Enter database credentials.</b>
        All database connection information must be entered in the settings tab before AdKats can run. 
	The plugin must be able to create/modify/use tables and their data. 
	Using the same credentials as your stat logger plugin should be sufficient.
    </li>
    <li>
        <b>Enable AdKats.</b>
        AdKats will confirm all dependencies and show startup information in the console.
        If startup completes and says the plugin is running then all is well.
        AdKats will automatically update itself with new stable patches and releases.
	If you want to update manually just use the /pupdate command.
        Enjoy your new admin tool!
    </li>
</ol>
<p>
    If you have any problems installing AdKats please let me know on the MyRCON forums or on Github as an issue and
    I'll respond promptly.
</p>
<HR>
<p>
    <a name=faq />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_FAQ.jpg" alt="AdKats User Manual">
</p>
<ul>
    <li>
        <b>'You have an error in your SQL syntax' error when running the setup SQL script.</b>
        This error is due to an issue with the latest version of PHPMyAdmin, not a database or script problem. 
        Please use another tool such as MySQL Workbench or a previous version of PHPMyAdmin to run this script and it will execute without issue.<br/><br/>
        The following error:<br/>
        #1064 - You have an error in your SQL syntax; check the manual that corresponds to your MySQL server version 
        for the right syntax to use near 'REPLACE INTO `adkats_commands` VALUES(75, 'Active', 'player_pm_reply', 
        'Unable',' at line 3
    </li>
    <li>
        <b>Other troubles running the setup SQL script.</b>
        If this happens it is most likely your database provider has restricted your access to create triggers,
        stored procedures, or both.
        These elements are required for AdKats to properly function, and thus it cannot be run without them.
        Please talk with your database provider and gain access to creation of stored procedures and triggers.
    </li>
    <li>
        <b>"Stat logger tables missing" on first run after setting up the plugin.</b>
        Confirm you have run the stat logger plugin on the same layer and it is functioning without error. 
        If it is, try rebooting your layer.
    </li>
    <li>
        <b>Suggest more for this list if you run across them.</b>
    </li>
</ul>
<HR>
<p>
    <a name=features />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Features.jpg" alt="AdKats User Manual">
</p>
<h3>User Ranks and Roles</h3>
<p>
    AdKats hands out powers based on roles you configure, these are completely separate from the setup you've done for
    Procon and are not affected by that system in any way.
    These users are distributed to all AdKats instances you run through your database.
    On first run of the plugin you will need to add a user before you can access a majority of the in-game commands.
    You can have as many users as you want.
    When a user is added you need to assign them a role.
    The default role is "Default Guest" and the allowed commands for that role are shown to you in the role section.
    The default guest role cannot be deleted but can be edited to allow any non-admin commands.
    You can add more roles by typing a new role name in the "add role" field.
    Any newly added roles duplicate the current allowed commands on the "Default Guest" role so you will need to edit the allowed commands for new roles.
    When you change a user's role and they are currently in-game they will be told that their role has changed, and what
    it was changed to.
</p>
<p>
    Once a user is added you need to assign their soldiers.
    If you add a user with the same name as their soldier(s) their soldier(s) will be added automatically if they are
    already in the database.
    Users can have multiple soldiers so if your admins have multiple accounts you can assign all of those soldiers
    under their user.
    <b>Soldiers need to be in your database before they can be added to a user, so make sure they have been in the server
    with AdKats/Stat Logger running before you try to add them to a user.</b>
    This system tracks user's soldiers so if they change their soldier names they will still have powers without
    needing to contact admins about the name change.
    Type a soldier's name in the "new soldier" field to add that soldier to a user.
    It will error out if it cannot find the soldier in the database.
    To add soldiers to the database quickly after installing stat logger for the first time, have them join any server
    you are running this version of AdKats on and their information will be immediately added.
</p>
<p>
    The user list is sorted by role power level and then by username. Power level is a metric for how much a role has
    access to.
    For any setting item that says "Delete?" you need to type the word delete in the line and press enter, this avoids
    accidental deletion of users/roles.
</p>
<h3>Full Logging</h3>
<p>
    All commands, their usage, who used them, who they were targeted on, why, when they were used, and where from, are
    logged in the database.
    All plugin actions are additionally stored in Procon's event log for review without connecting to the database.
    Player name/IP changes are logged and the records connected to their player ID, so tracking players is easier.
</p>
<h3>Infraction Tracking System</h3>
<p>
    Infraction Tracking commands take the load off admins remembering which players have broken server rules and how
    many times.
    These commands have been dubbed "punish" and "forgive". Each time a player is punished a log is made in the
    database;
    The more punishes they get, the more severe the action gets.
    Available punishments include: warn, kill, kick, temp-ban 60 minutes, temp-ban 2 hours, temp-ban 1 day, temp-ban 2 days,
    temp-ban 3 days, temp temp-ban 1 week, temp-ban 2 weeks, temp-ban 1 month, and permaban.
    The order and severity of punishments can be configured to your needs.
</p>
<p>
    Detailed Stuff: After a player is punished, their total infraction points are calculated using this very basic
    formula:
    <b>(Punishment Points - Forgiveness Points) = Total Points</b>
    Then an action is decided using Total Points from the punishment hierarchy.
    Punishments should get harsher as the player gets more points.
    A player cannot be punished more than once every 20 seconds; this prevents multiple admins
    from accidentally punishing a player multiple times for the same infraction.
</p>
<h4>TeamKilling Management</h4>
<p>
    The punish and forgive commands in AdKats are admin use only. 
    If you would like to integrate a teamkill punish/forgive system with the AdKats punish system, use the <a href="https://forum.myrcon.com/showthread.php?8690" target="_blank">Team Kill Tracker Plugin</a> and enable the AdKats integration settings.
</p>
<h4>IRO Punishments</h4>
<p>
    When a player is punished and has already been punished in the past 10 minutes the new punish counts
    for 2 infraction points instead of 1 since the player is immediately breaking server rules again.
    A punish worth 2 points is called an "IRO" punish by the plugin, standing for Immediate Repeat Offence.
    "[IRO]" will be prepended to the punish reason when this type of punish is activated.
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
        <td><b>1</b></td>
        <td>Warn</td>
        <td>warn</td>
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
        <td><b>5</b></td>
        <td>Temp-Ban 2 hours</td>
        <td>tban120</td>
    </tr>
    <tr>
        <td><b>6</b></td>
        <td>Temp-Ban 1 Day</td>
        <td>tbanday</td>
    </tr>
    <tr>
        <td><b>7</b></td>
        <td>Temp-Ban 2 Days</td>
        <td>tbanday</td>
    </tr>
    <tr>
        <td><b>8</b></td>
        <td>Temp-Ban 3 Days</td>
        <td>tbanday</td>
    </tr>
    <tr>
        <td><b>9</b></td>
        <td>Temp-Ban 1 Week</td>
        <td>tbanweek</td>
    </tr>
    <tr>
        <td><b>10</b></td>
        <td>Temp-Ban 2 Weeks</td>
        <td>tban2weeks</td>
    </tr>
    <tr>
        <td><b>11</b></td>
        <td>Temp-Ban 1 Month</td>
        <td>tbanmonth</td>
    </tr>
    <tr>
        <td><b>12</b></td>
        <td>Perma-Ban</td>
        <td>ban</td>
    </tr>
    <tr>
        <td><b>Greater Than 12</b></td>
        <td>Perma-Ban</td>
        <td>ban</td>
    </tr>
</table>
<p>
    Players may also be 'forgiven', which will reduce their infraction points by 1 each time, this is useful if you
    have a website where players can apologize for their actions in-game.
    Players cannot be forgiven into negative infraction point values.
</p>
<p>
    Since you can run multiple servers with this plugin on the same database, if you want punishments to increase on
    the current server when infractions are committed on others (or vice-versa) enable "Combine Server Punishments".
    Punishments from another server won't cause increase in infractions on the current server if
    "Combine Server Punishments" is disabled.
    This is available since many groups run different rule sets on each server they own, so players breaking rules on
    one may not know rules on another, so they get a clean slate.
</p>
<h4>Auto-Forgives</h4>
<p>
    Players can optionally be automatically forgiven infraction points after a specified duration of clean play and positive
    reputation.
    Settings are available to specify the minimum day count since last forgiven and last punished before an auto-forgive
    will be issued.
    The reputation command can be used by players to find out when their next auto-forgive will happen if the auto-forgive
    system is enabled.
    Minimum of 7 days for each auto-forgive duration, with suggested/default values of 14 and 30 days for forgive and punish
    durations respectively.
</p>
<p>
    <b>Suggestions:</b> When deciding to use the infraction system, 'punish' should be the only command used for player
    rule-breaking.
    Other commands like kill, or kick are not counted toward infractions since sometimes players ask to be killed,
    admins kill/kick themselves, or players get kicked for AFKing.
    Kill and kick should only be used for server management.
    Direct temp-ban and ban are of course still available for hacking/glitching situations, but that is the ONLY time
    they should be used.
</p>
<h3>Player Reputation System</h3>
<p>
    Reputation is a numeric for how helpful a player is to the server.
    The more they help admins by reporting rule breakers, moreso from spectator, assisting the weak team, or populating
    the server, the more their reputation increases.
    Committing infractions, breaking server rules, getting banned, etc, reduces their server reputation.
</p>
<p>
    Reputation starts at zero and moves toward -1000 or 1000 so it's easy to get/lose rep early on but harder
    near the top/bottom.
    Players will never reach -1000 or 1000 reputation, but can get close with a lot of effort.
    Each command a player issues and every command issued against them has a reputation amount; Some good, some bad.
    Every time a player's reputation changes you are notified of the change in chat.
</p>
<p>
    The following are ways reputation can be gained:
    <ul>
        <li>
            <b>Issuing good reports on players.</b> Just reporting someone gives rep but when an admin accepts the
            report or acts on it it's triple the rep bonus.
        </li>
        <li>
            <b>Reporting from spectator.</b> Reporting from spectator is worth much more than reporting in-game. Players
            are sacrificing their game time to help a server and should be rewarded.
        </li>
        <li>
            <b>Using !assist.</b> Sometimes teams really need help, and sometimes a player's friends are stuck on the
            weak team. Helping them and the server out by using this command to switch increases rep greatly.
        </li>
        <li>
            <b>Populating servers.</b> Worth twice that of an assist, populating a server helps more than almost anything
            else. Players are notified and thanked for populating servers along with the rep bonus.
        </li>
    </ul>
</p>
<p>
    If a player has infractions on their record that causes a reputation reduction, but the reduction infraction points
    cause reduces over time.
    So if they have infractions on their record, simply not committing them for a while reduces the rep loss caused.
    It does not reduce completely however, they will need to report some rule breakers to get it positive again.
</p>
<h3>Ban Enforcer</h3>
<p>
    AdKats can enforce bans across all of your servers.
    The Ban Enforcer will import and consolidate bans from every Procon instance you run.
    Bans can be made by name, GUID, IP, any combination, or all at once.
    The default ban is by EA GUID only; this default can be edited but doing so is not recommended.
    Banned players are told how long their ban will last, and when a banned player attempts to re-join they are told the
    remaining time on their ban.
    Using ban enforcer also gives access to the unban and future-permaban commands.
</p>
<p>
    The Enforcer works with existing auto-admins and any bans added manually through Procon will be
    automatically imported.
    A mini ban management section is added to the plugin settings when you enable this, however, for full fledged ban
    management it helps to run the BFAdminCP by Prophet731.
    Ban enforcer's options are simply too much for the plugin setting interface to house properly.
    Use of the ban enforcer is optional because of this slight dependency, and is disabled by default.
</p>
<p>
    Ban Enforcer can be enabled with the "Use Ban Enforcer" setting. On enable it will import all bans from your ban
    list, then clear it.
    Once you enable the enforcer you will be unable to manage any bans from Procon's banlist tab.
    Disabling ban enforcer will repopulate Procon's banlist with the imported bans, but you will lose any additional
    information ban enforcer gathered about the banned players.
</p>
<p>
    <b>Reasoning behind creation, for those interested:</b>
    We had tried many other ban management systems and they all appeared to have some significant downfalls.
    Developing this allowed for some nice features not previously available.
<ol>
    <li>I can bypass Procon's banlist completely, this way no data is lost on how/why/who created the ban or on who it's
        targeted.</li>
    <li>I can enforce bans by any parameter combination (Name, GUID, IP), not just one at a time.</li>
    <li>Players can now be told how much time is left on their ban dynamically, every time they attempt to join.</li>
    <li>Tracking of bans added through in-game commands or autoadmins on any server is a cakewalk now, so clan leaders
        don't need to go great lengths to look things up.</li>
</ol>
</p>
<h3>Report/CallAdmin System w/Email Support</h3>
<p>
    When a player puts in a proper !report or !admin all in-game admins are notified.
    Reports are logged in the database with full player names for reporter/target and the full reason for
    reporting.
    Uses of !report and !admin with this plugin require players to enter a reason, and will tell them if they
    haven't entered one.
    It will not send the report to admins unless reports are complete, cleaning up what admins end up seeing for
    reports.
</p>
<h4>Using Report IDs</h4>
<p>
    Reports and calls are issued a random three digit ID which expires either at the end of each round, or when it
    is used.
    These ID's can be used in any other action command, simply use that ID instead of a player-name and reason
    (e.g. waffleman73 baserapes, another player reports them and gets report ID 582, admins just use !punish 582 instead
    of !punish waffleman73 baserape).
    Confirmation of command with !yes is required before a report ID is acted on.
    Players are thanked for reporting when an admin uses their report ID.
    Other online admins are informed when an admin acts on a report by ID, either with action, deny, or accept.
</p>
<h4>Report Emails</h4>
<p>
    When email usage is enabled, all admins with emails defined will get an email containing the report information.
</p>
<h4>Report PBSS</h4>
<p>
    Automatic Punkbuster screenshots are triggered on reported players.
</p>
<h3>Admin Assistants</h3>
<p>
    We utilized this system on our no explosives server with great success, mainly catching things autoadmin cannot.
    Basically this system automatically tracks who the trusted players in your servers are,
    and who are reliable sources of reports.
</p>
<h4>Basic Functionality</h4>
<p>
    The system makes use of the report IDs assigned to each round report.
    When a player sends a report, and an admin uses the report by ID, the report is logged as confirmed.
    Once you enable Admin Assistants, AA status is given once the player has X confirmed reports in the past month or
    75+ total confirmed reports.
    A player with AA status is informed of their status on first spawn in the server after joining.
    If you enable the admin assistant perk, players with AA status are given access to the teamswap and online-admins
    commands for the duration they maintain AA status.
    These command perks are basically incentives to report rule-breakers.
    Whether a player has AA status is calculated when they join the server, and that status will remain for the duration
    they are in the server.
    When an admin assistant sends a report, to the admins their name is prefixed with [AA] to note it as a (most likely)
    reliable report.
    Likewise if an admin assistant is the target of a report, their name is prefixed with a clan-tag-like code.
    (e.g. Report [512]: [AA]ColColonCleaner reported [AA]SomeOtherAA for using explosives).
</p>
<h4>Advanced Usage (Auto-Handling)</h4>
<p>
    The advanced functionality of this system is now released to the public as testing is complete. This subsection uses your AAs as a collective human autoadmin.
</p>
<p>
    Players with AA status can conditionally have their reports acted on by the internal autoadmin. A list of trigger words or phrases of any length can be defined in AdKats settings. If an AA report reason contains any of those trigger words or phrases then autoadmin will act on their report with a punish on the target player, using the reason they entered. This was originally intended for cases when admins are offline and unable to answer reports, but has now been added for all cases.
</p>
<p>
    If admins are offline, and the report matches criteria, autoadmin will punish the target player after 5 seconds. If admins are online, a 45 second window is given for the admin to act on the report before automatic handling fires.
</p>
<p>
    Admins can use any action command just like normal (e.g. !kill ID, !punish ID, etc...), but can also use the new !accept, !ignore, and !deny commands. !accept will confirm the report but take no action against the target player. !ignore is used for bad or invalid reports, the report is removed from the list and no action is taken. !deny is for malicious reports and abuse of the report system, this command will destroy a reporter's reputation and hurt their AA status.
</p>
<p>
    Exceptions and Security Measures: Automatic handling will not be taken if the target of a report is an admin or another AA, a real admin must act on the report. Automatic action will also not be taken if the target player has already been acted on in some way in the past 60 seconds.
</p>
<h3>Extended Round Statistics</h3>
<p>
    Basic round statistics can be found through stat logger in the tbl_mapstats table, but these stats only give basic
    information at round end. AdKats adds a table tbl_extendedroundstats, which shows how matches progress while the
    round is still going, not just at the end. Every 30 seconds, the current round ID, round duration, team counts,
    ticket counts, ticket difference rates, team total scores, score rates, and a timestamp are logged in the table. A
    display of this information (in part) can be seen in the BFAdminCP server stats page. Logging starts at the
    beginning of each round, it will not start immediately for the current round when AdKats enables.
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
    NOTE: For cross-team joining, TeamSwap queues are not used, so if there is no room on the target team you will need
    to retry the command once room is available.
</p>
<h3>Pre-Messaging</h3>
<p>
    A list of editable pre-defined messages can be added in settings, then admins can use the message ID instead of
    typing the whole message in.
    Example: "!say 2" will send the second pre-defined message.
</p>
<p>
    Use !whatis [preMessageID] to find out what a particular ID links to before using it in commands.
</p>
<p>
    <b>Anywhere a reason or message is needed, a preMessage ID can be used instead.</b><br/>
    Example: 4th preMessage is "Baserape. Do not shoot uncap."<br/>
    "!punish muffinman 4" will punish them for the above reason.
    Even more useful is using report IDs with this, for example someone reports muffinman for "baseraping asshat" and
    gets report ID 283.
    You don't want "baseraping asshat" to be the actual reason entered, so you can just do "!punish 283 4", and he will
    get the proper punish message.
</p>
<h3>TeamSwap</h3>
<p>
    <b>TeamSwap is NOT an autobalancer</b>
    (look up other plugins for that functionality), it is for manual player moving only.
</p>
<p>
    TeamSwap is a server-smart player moving system which offers two major benefits over the default system.
    Normally when trying to move a player to a full team the command just fails at the server level, now the player is
    dropped on a queue until a slot opens on that side.
    They can keep playing on their side until that slot opens, since when it does they are immediately slain and moved
    over to fill it.
    Secondly it allows whitelisted (non-admin) players the ability to move themselves between teams as often as they
    want (within a ticket count window).
    This is currently not an available option in default battlefield aside from Procon commands since the game limits
    players to one switch per gaming session.
    Whitelisted players can type '!moveme' and TeamSwap will queue them.
    This is meant to be available to players outside the admin list, usually by paid usage to your community or to clan
    members only.
    Admins can also use '!moveme', and in their case it bypasses the ticket window restriction.
    When players are moved by admins, use the moveme command, or use assist, they are locked to that team for the
    current round.
    When a player is moved by admin, multibalancer unswitcher is disabled for a few seconds to remove the chance of
    autobalancer fighting admin moves.
</p>
<h3>Requiring Reasons</h3>
<p>
    Any command which might lead to actions against players are required to have a reason entered, and will cancel if
    no reason is given.
    Players (even the most atrocious in some cases) should know what they were acted on for, and it's also a good way to
    hold admins accountable for their actions.
    The minimum number of characters for reasons is editable in plugin settings.
    The editable setting only applies to admin commands, and the default value is 5 characters.
    Reports and Admin calls are hardcoded to 1 character minimum reason lengths.
</p>
<h3>Setting Sync</h3>
<p>
    Plugin settings are automatically synced to layers every 5 minutes from their particular server IDs.
    Settings for each plugin instance are stored in the database by server ID.
    Enter an existing server ID in the setting import field and all settings from that instance will be imported to this
    instance.
    Settings on the current instance will be overwritten by the synced settings.
    Whenever a setting is changed, that change is persisted to the database.
</p>
<h3>Special Player Lists</h3>
<p>
    Special player list table "adkats_specialplayers" has been added.
    In this table, players can be added to any desired group accepted by AdKats.
    Valid groups can be seen on github, in the adkatsspecialgroups.json file.
    Players can be added by ID, or by identifier (name, guid, or IP), and can be assigned a game and server to apply
    them to.
    If you use player IDs then you won't need to update player names if they change their names,
    the player names will automatically update when they join the server;
    This is especially good to use when whitelisting for the AntiCheat system.
    Leave fields blank to indicate wildcard, for example leaving the server column blank for player will mean it applies
    to all servers of their game.
    If you specify the server, the group you have them assigned to will only apply for that one server.
    Each special player list entry now has an effective and expiration date, stored in UTC time.
</p>
<h3>3rd Party Plugin Orchestration</h3>
<p>
    Orchestration table "adkats_orchestration" has been added. All entries in this table are polled every 5 minutes and
    sent to the appropriate plugins. Add entries to that table with a given server ID, plugin name, plugin setting name,
    and setting value. AdKats will send those settings to the target plugins every 5 minutes.
</p>
<h3>AntiCheat System with Whitelist</h3>
<p>
    The AntiCheat System is used for automatically catching and acting
    on players with suspicious or impossible statistics.
</p>
<p>
    The system uses battlelog for player stats, and is able to catch aimbots, damage mods, magic bullet, and other oddities.
</p>
<h4>DPS Checker [Code 4 Bans]</h4>
<p>
    The damage per shot each player gets with their weapons is calculated from their battlelog stats, bans being issued
    if a player attains impossible damage.
    This section is now completely automated and enabled by default without a means to turn it off, any doubt about bans
    it issues should be taken with extreme caution as this system when properly configured has not issued a false
    positive ban in the 2 years it has been active on our servers.
</p>
<p>
    The LIVE mods for this section enable it to detect damage mods from round to round, regardless of overall stats.
    Players using a damage mod during a round should be banned automatically the round after they are detected.
	The delay is due to battlelog request limits and needing to wait for the battlereport to post.
</p>
<h4>HSK Checker [Code 6 Bans]</h4>
<p>
    For this check only automatic weapons from specific categories are used in the calculation.
    This includes Sub Machine Guns, Assault Rifles, Carbines, and Machine Guns.
    Handguns, snipers, equipment, etc are not included since their HSK values can vary drastically.
    This limit is simple, if the headshot/kill percentage for any valid weapon is greater than your trigger level, the
    ban is issued.
    HSK percentage for even the top competitive players caps at 38%, so we set our value much higher than that.
    We started with 70% HS/K, and no false positives were found with that value, but lower as desired.
    The minimum acceptable value we allowed during testing was 50%, and that's where we have it now.
    100 kills with the weapon in question are required to trigger this check.
</p>
<p>
    The LIVE mods for this section are enabled when you turn on the 'LIVE system includes mass murder and aimbot checks' setting. Players using a very blatant aimbot for heads in a round should be caught very quickly with this system.
	It's very difficult to trigger this check normally, so don't worry about causing a large number of false positives
	by enabling it.
</p>
<h4>KPM Checker [Code 5 Bans]</h4>
<p>
    Be careful with this one, this is where a lot of legit competitive players reside.
    This check should only be used to request gameplay video of players to prove their play, then whitelist the player
    if everything checks out.
    For this check all weapons aside from melee weapons and equipment are included.
    This includes Sub Machine Guns, Assault Rifles, Carbines, Machine Guns, Handguns, and Sniper Rifles.
    This check uses weapon time and total kills, rather simplistic, just kills/total minutes.
    If that value is greater than your trigger level the ban is issued.
    After some research and testing the value used on our servers is the default, 5.0.
    200 kills with the weapon in question are required to trigger this check.
</p>
<p>
    The LIVE mods for this section are enabled when you turn on the 'LIVE system includes mass murder and aimbot checks' setting. Players who kill an inordinate amount of people in a short period of time will be caught quickly with this 
	system. It's very difficult to trigger this check normally, so don''tt worry about causing a large number of false 
	positives by enabling it.
</p>
<h4>Special Weapons Checker [Code 8 Bans]</h4>
<p>
    Special weapons bans are issued when a player starts killing with weapons which are normally non-lethal. 3 kills with the given non-lethal item are required in a round to trigger the script.
    <ul>
        <li><b>'BF4 [8-1]'</b> - 
            Portable Ammo Packs
        </li>
        <li><b>'BF4 [8-2]'</b> - 
            Radio Beacons
        </li>
        <li><b>'BF4 [8-3]'</b> - 
            SOFLAM Laser
        </li>
        <li><b>'BF4 [8-4]'</b> - 
            Motion Sensor Throwables
        </li>
        <li><b>'BF4 [8-5]'</b> - 
            Portable Med Packs
        </li>
        <li><b>'BF4 [8-6]'</b> - 
            Med Bags
        </li>
        <li><b>'BF4 [8-7]'</b> - 
            Ammo Bags
        </li>
        <li><b>'BF3 [8-1]'</b> - 
            Ammo Bags
        </li>
        <li><b>'BF3 [8-2]'</b> - 
            Radio Beacons
        </li>
        <li><b>'BF3 [8-3]'</b> - 
            SOFLAM Laser
        </li>
        <li><b>'BF3 [8-4]'</b> - 
            Med Bags
        </li>
    </ul>
</p>
<p>
    These special weapon checks are always enabled.
</p>
<h4>Magic Bullet</h4>
<p>
    The AntiCheat system is able to detect the magic bullet hack and will issue bans accordingly.
    If any ban it issues is questionable please post about it on MyRCON in the AdKats thread.
</p>
<h4>Posting Method</h4>
<p>
    The "most hacked" weapon (the one farthest above normal) is the one displayed in the ban reason using the
    following formats:
</p>
<p>
    <b>Damage Mod Bans:</b><br/>
    DPS Automatic Ban [4-WEAPONNAME-DPS-KILLS-HEADSHOTS-HITS]<br/>
    <b>Aimbot Bans:</b><br/>
    HSK Automatic Ban [6-WEAPONNAME-HSK-KILLS-HEADSHOTS-HITS]<br/>
    <b>KPM Bans:</b><br/>
    KPM Automatic Ban [5-WEAPONNAME-KPM-KILLS-HEADSHOTS-HITS]
</p>
<p>
    LIVE ban reasons have the following formats:
</p>
<p>
    <b>LIVE Damage Mod Bans:</b><br/>
    DPS Automatic Ban [LIVE][4-WEAPONNAME-DPS-KILLS-HEADSHOTS-HITS]<br/>
    <b>LIVE Aimbot Bans:</b><br/>
    HSK Automatic Ban [LIVE][6-L-KILLS-HSK]<br/>
    HSK Automatic Ban [LIVE][6-H-KILLS-HSK]<br/>
    <b>LIVE KPM Bans:</b><br/>
    KPM Automatic Ban [LIVE][5-L-KILLS]<br/>
    KPM Automatic Ban [LIVE][5-H-KILLS]<br/>
    <b>LIVE Magic Bullet:</b><br/>
    Magic Bullet [LIVE][7-KILLS-HITS]<br/>
    <b>LIVE Special Weapons:</b><br/>
    [LIVE][Code 8-" + actedCode + "]: Dispute Requested
</p>
<p>
<ul>
    <li>HSK bans take priority over DPS bans, and DPS over KPM.</li>
    <li>Whitelisting can either be done using the acwhitelist command, or by entering their player ID, name, guid,
        or IP in the adkats_specialplayers table using the group "whitelist_anticheat"</li>
</ul>
</p>
<h3>Automatic Updates</h3>
<p>
    AdKats is set up to automatically update itself when stable releases are made, so there is no need to upload newer
    versions to your layer once you are running it.
    Once the update is automatically downloaded and patched, all it requires is a Procon reboot to run the updated
    version.
    Admins will be informed of available updates when they are published, or if the plugin was able to automatically
    update, that the Procon instance needs to be rebooted.
    The automatic update process can be disabled for those who want to manually update, but it is enabled by default.
</p>
<h3>Ping Enforcer</h3>
<p>
    Ping enforcer allows control of players over a certain average ping, with several key options.
    A linearly interpolated moving average is used to monitor ping of all players in the server, kicking
    players after a minimum monitor time is reached, and a minimum number of players are in the server.
    The system can kick for missing ping, and attempt to manually ping players whose pings are not given by
    the server.
</p>
<p>
    Players who join the server and are over the limit are warned about it in chat.
    A player whose ping is normal, but spikes over your limit, is warned about the spike.
</p>
<p>
    Admins are automatically whitelisted, but the entire user list can be optionally whitelisted, or a given
    subset of role keys.
    Individual players can be whitelisted with the pwhitelist command.
</p>
<h3>AFK Manager</h3>
<p>
    AFK Manager allows control over players staying in the server without contributing. Kicking AFK players can be done
    automatically, or via the AFK command.
    When automatic kick is enabled it will monitor all players in the server, kicking them after the trigger
    AFK time is reached, but only if the number of players in the server is over a certain amount.
</p>
<p>
    Chat can be optionally ignored, so players just spamming chat without playing can be removed from the server.
</p>
<p>
    Admins are automatically whitelisted, but the entire user list can be optionally whitelisted, or a given
    subset of role keys.
    Spectators are immune to AFK kicks.
</p>
<h3>SpamBot with Whitelist</h3>
<p>
    The SpamBot is much akin to that found in other plugins, with a few added bells and whistles.
    Automatic messages can be set in separate lists for say, yell, and tell options.
    Each list has its own interval that can be customized, the defaults being 300, 600, and 900 seconds, respectively.
</p>
<p>
    The key difference is that admins and whitelisted players can be blocked from seeing SpamBot messages.
    This way your admins' chat are not cluttered with messages meant only for promotion or information they
    already know.
    Add [whitelistbypass] to the beginning of any spambot message and it will be sent to all players,
    ignoring this whitelist.
    Individual players can be whitelisted from seeing messages using the spamwhitelist command.
</p>
<h3>Commander Manager</h3>
<p>
    In cases of low population, commanders can mean the difference between a balanced server and baserape.
    This manager will not allow players to join as commander until a specified minimum number of players are in the
    server.
</p>
<p>
    We found a good value to be 40 players on 64p servers.
</p>
<h3>Surrender Vote System</h3>
<p>
    Surrender is used when one team is much stronger than another, to the point of it becoming nearly impossible
    for the losing team to move around the map. With this system players can vote to end the round with the current
    winning team as winner, having the running autobalancer scramble teams for a more balanced game next match.
</p>
<p>
    There are 3 commands used for surrender vote, surrender, votenext, and nosurrender. Access to these three
    commands, or a subset of them, must be given to your "Guest" role in the role settings section before this system
    can be used.
<ul>
    <li><b>surrender. </b> This command is usable by both teams, but will be translated to votenext if used by the
        winning team. Players on the losing team don't want to sit through a baserape, this command lets them vote toward
        round surrender. It adds one vote toward surrender.</li>
    <li><b>votenext. </b> This command is usable by both teams, but will be translated to surrender if used by the
        losing team. Players on the winning team will sometimes find it boring sitting at a baserape, this command
        lets them place their vote toward ending the round. It adds one vote toward surrender.</li>
    <li><b>nosurrender. </b> This command is usable only by the losing team. If someone doesn't think the situation
        is bad enough to warrant a surrender, they can use this command to vote against it. Removes one vote toward
        surrender.</li>
</ul>
</p>
<p>
    Minimum player counts, minimum ticket differences, and minimum ticket difference rates can be added as limits for
    starting a round surrender. These are important as they prevent players from starting a surrender vote when it's
    not warranted or necessary.
</p>
<p>
    A timeout can be added to the surrender vote. When enabled and the timeout expires after starting the vote, the
    vote is canceled and all votes removed.
</p>
<h3>Auto-Surrender/Auto-Nuke System</h3>
<h4>This system is based on ticket loss rates and only operates properly on servers running a single map/mode.</h4>
<p>
    Sometimes surrender vote is not enough to help the server. This system uses ticket loss/gain rates to automatically
    trigger either round surrender on the losing team, or auto-nuke on the winning team. This system should not be used
    on servers that run mixed mode, as different modes will have drastically different ticket rates.
</p>
<p>
    Do not enable this system until you have analyzed the ticket loss/gain rates that consistently happen during
    baserape on the particular server you wish to enable this on. Using the 'Display Ticket Rates in Procon Chat'
    setting in section A12 will display the rates in the Procon chat tab for analysis. Once you have the values you
    can set the windows for winning/losing team ticket rates, activating auto-surrender or auto-nuke.
</p>
<p>
    Other limits like minimum ticket difference and trigger count help make sure the ticket rates it sees are actually
    from a baserape and not from any other case.
</p>
<p>
    Once a round matches all of the parameters you set, auto-surrender or auto-nuke is triggered. Auto-surrender will
    cause the round to end, in favor of the current winning team. Auto-nuke will kill every player on the winning team
    that is currently alive; It will typically issue 1-3 times, making sure all players are dead.
</p>
<h3>Challenge System</h3>
<p>
	The challenge system in AdKats is designed to be a multi-server stats-driven way to give players new goals with rewards. You can set up almost any combination of damage types or weapons to be used by players, in multiple tiers.
</p>
<h4>Definitions (What the player must do.)</h4>
<p>
    Definitions are global, not server specific, the same definitions are visible across all of your servers. They define the base goals that a player must complete to finish a challenge. For example, get 5 kills with 5 different assault rifles. Each definition needs a name to be created. Once created you can define weapons or damage types on the definition, as many as you want. When adding a damage type to a definition the kill count associated with it is for EACH weapon, not the total kill count, the total count in this example would end up being 25. For weapon count in a damage type that means X different weapons of the player’s choosing, with Y kills for each weapon they decide to use. When adding a specific weapon code to a definition the kill count is for that specific weapon only. For a player to complete the resulting challenge they will need to perform all of the kills on the definition for that rule.
</p>
<h4>Rules (How fast the player must do it.)</h4>
<p>
    Rules are server specific. They define restrictions on how fast players must complete the challenge, what measuring system is used for that, whether the rule is enabled, and the rule's tier. Each rule is created by choosing a definition to base it on. When you select a definition and the rule is created an auto-generated name is created for it which you can edit as desired. THIS NAME, not the one on the definition, is the one shown to players. You can have multiple rules for the same definition if you want. For example, you could have the assault rifle definition above defined in two rules, one completing in 30 minutes, the other completing in 15 minutes for a harder challenge. You can choose between round based completion, minutes based completion, or deaths based completion. A player running this rule will need to complete the defined kills in the given constraint. The 'tier' of a rule is shorthand for how difficult it is.
</p>
<h4>Rewards (What the player gets for doing it.)</h4>
<p>
    Rewards are defined in tiers, 1 through 10. Supposedly tier 1 rules are the easiest and increase in difficulty through 10 being the hardest possible. You can define as many or few of these as you want. Personally I only have 3 tiers configured, 1 as easy, 2 as medium, and 3 as very hard. All rules of a certain tier will get the same reward. More rewards will be added as they are thought up but right now it's the following:
</p>
<ul>
    <li><b>Reserved Slots.</b> 
    	Self explanatory, adds a reserved slot for the person completing the challenge. If they already have a reserved slot, the reward time is added to their existing time.
    </li>
    <li><b>Spectator Slots.</b> 
    	Self explanatory, adds a spectator slot for the person completing the challenge. If they already have a spectator slot, the reward time is added to their existing time.
    </li>
    <li><b>Autobalance Whitelisting.</b> 
    	Players are added to the MULTIBalancer plugin's whitelist. If they already have an autobalance whitelist, the reward time is added to their existing time.
    </li>
    <li><b>Teamkill Whitelisting.</b> 
    	Players are added to the TeamKillTracker plugin's whitelist. If they already have a teamkill whitelist, the reward time is added to their existing time.
    </li>
    <li><b>Command Lock.</b> 
    	Issues the lock command on players for the specified duration. The lock command blocks admins or autoadmins from acting on a player if the commands are run through AdKats. This essentially lets a player break rules for a specified period of time without penalties. While a player is locked an announcement message is sent to the server every 30 seconds so they know what’s going on. There is a 24 hour timeout on this reward so players cannot grind for it more than once in a given day. The timeout is visible when running /ch rewards, or while playing a challenge with this reward in it. If you are running the loadout enforcer and are integrating with AdKats on that plugin, players who are currently locked will not be loadout enforced.
    </li>
</ul>
<h4>Example</h4>
<p>
    You want to set up a challenge to be run every round publicly, the challenge expires at the end of every round, and players need to get 10 kills with 3 different assault rifles (totalling 30 kills), and 5 kills with the magnum revolver. A player completing this should get 12 hours added to their reserved slot on the server.
</p>
<p>
    Make sure “server-wide round rules” is enabled.
</p>
<ul>
    <li>With this setting enabled there will be challenges announced at the start of every round if one is available. Without this setting enabled players would need to choose their own challenge manually.
    </li>
</ul>
<p>
    Make sure “auto-assign round rules” is enabled.
</p>
<ul>
    <li>With this setting enabled players will automatically be assigned the round challenge if one is running in two cases: They get a kill which fits the challenge, or they use the challenge command (by default !ch) to see the current challenge.
    </li>
</ul>
<p>
    Create a definition for the challenge.
</p>
<ul>
    <li>In the Add Definition setting type in whatever you want but make it match what you’re trying to make. For this one we’ll type in “30 AR 5 Magnum”.</li>
    <li>Once that is added you’ll see a definition entry added in the list. Now you need to add the damages/weapons.</li>
    <li>In the Add Damage Type setting inside this “30 AR 5 Magnum” definition, select “Assault_Rifle”.</li>
    <li>You’ll see a detail added to the list called “Damage - Assault Rifle” under this definition. It has a default of 1 weapon, and 1 kill.</li>
    <li>Change that to what we want, 10 kills, 3 weapons. Doing this will make players need to get 10 kills with 3 different assault rifles, totalling 30 kills.</li>
    <li>Now to add the magnum. In the Add Weapon setting inside this “Magnum ARs” definition, select “Handgun\44 Magnum”.</li>
    <li>You’ll see another detail added to the list called “Weapon - 44 Magnum” under this definition. It has a default of 1 kill.</li>
    <li>Change that to what we want, 5 kills.</li>
    <li>We now have the definition set up with 30 assault rifle kills (3 weapons, 10 kills each), and 5 magnum revolver kills.</li>
</ul>
<p>
    Create a rule based on this definition.
</p>
<ul>
    <li>Collapse the definition section so it’s not in the way.</li>
    <li>Under the rules section you’ll see an Add Rule setting. Select “30 AR 5 Magnum” in that setting.</li>
    <li>You’ll see a new rule added with the name “30 AR 5 Magnum Rule X”, X being the next index available for a rule.</li>
    <li>Rename that rule into something players would recognize, the name on this rule is what gets shown to players, not the definition name. Let’s call this one “Magnum ARs”. Just use the Name setting on the rule to change it.</li>
    <li>We want this to be a server-wide round rule. (If a rule doesn’t qualify as a server-wide round rule players can still play the challenge but they need to select it manually.) So, this rule needs to have several things set: Completion type of “Rounds”, Round Count of 1, Tier of 1, Enabled = true.</li>
    <li>Change the completion type on it from “None” to “Rounds”.</li>
    <li>You’ll see a Round Count setting appear, this needs to be kept at 1 for it to qualify as a server-wide round rule. </li>
    <li>You see a Tier setting, this needs to be kept at 1 for it to qualify as a server-wide round rule.</li>
    <li>You see an Enabled setting. Now that is is ready, change that setting to True.</li>
</ul>
<p>
    Create a reward for this tier.
</p>
<ul>
    <li>Collapse the rule section so it’s not in the way.</li>
    <li>Under the rewards section you’ll see an Add Reward setting. Enter 1 in that setting. This 1 means you are creating a reward for tier 1 challenges.</li>
    <li>You’ll see several settings appear for the new reward. Select the Reward Type setting and change it to ReservedSlot.</li>
    <li>Change Duration Minutes to 720.</li>
    <li>You see an Enabled setting. Now that is is ready, change that setting to True.</li>
    <li>You now have tier 1 challenges set up to add 12 hours to a player’s reserved slot whenever it is completed. You can add more than one reward to a tier if you want, but not duplicates of the same reward on the same tier.</li>
</ul>
<p>
    You are now set up with a new challenge players can use. On the next round it will be announced to the server. To get more info about the challenge system you can use !ch help
</p>
<h3>Automatic Database Disconnect/Malfunction Handling System</h3>
<p>
    If the connected database goes offline, or becomes over encumbered to the point of being unusable, AdKats will automatically handle that state. If that state is reached, AdKats will temporarily halt all interaction with the database, disable stat logger, and wait for the situation to be rectified. Checks for fixed connection are made every 30 seconds, and once restored stat logger and AdKats connections with the database are re-enabled.
</p>
<h3>Commanding AdKats from External Source</h3>
<h4><u>BFAdminCP can be used for this.</u></h4>
<p>
    There are currently two ways to interact with AdKats externally. (A third coming soon if possible).
</p>
<h4>Adding Database Records</h4>
<p>
    Have your external system add a row to the record table with a new record to be acted on.
    All information is needed in the row just like the ones sent from AdKats to the database.
    Review the ones already in your database before attempting this, and ask ColColonCleaner any questions you may have.
    The only exception is you need to make the 'adkats_read' column for that row = "N", this way AdKats will act on that
    record.
    Every 5-10 seconds the plugin checks for new input in the table, and will act on them if found.
</p>
<h4>Using external plugin API</h4>
<p>
    Two available MatchCommands have been added, one for issuing commands through AdKats, and the second for fetching
    admin lists.
    These can be called by other plugins to integrate their functionality with AdKats and its database.
<h5>FetchAuthorizedSoldiers</h5>
Plugin: AdKats<br/>
Method: FetchAuthorizedSoldiers<br/>
Parameters:
<ul>
    <li><b>caller_identity</b> String with ID unique to the plugin sending the request. No whitespace or special
        characters. e.g. "InsaneLimits"
    </li>
    <li><b>response_requested</b> true</li>
    <li><b>response_class</b> Class/plugin where the callback will be sent.</li>
    <li><b>response_method</b> Method within the target plugin that will accept the response</li>
    <li><b>user_subset</b> "admin", "elevated", or "all". Admin meaning they have access to player interaction commands,
        elevated meaning they do not. Returns all soldiers in that subset.
    </li>
    <li><b>user_role </b> Returns all soldiers belonging to users in a specific role.</li>
</ul>
(user_subset and user_role cannot be used at the same time, pick one or the other.)<br/><br/>
Response:
<ul>
    <li><b>caller_identity</b> AdKats</li>
    <li><b>response_requested</b> false</li>
    <li><b>response_type</b> FetchAuthorizedSoldiers</li>
    <li><b>response_value</b> List of soldiers that matched the given parameters. CPluginVariable.EncodeStringArray used
        to compact into one field. CPluginVariable.DecodeStringArray can be used to parse the field back into an array.
    </li>
</ul>
<h5>IssueCommand</h5>
Plugin: AdKats<br/>
Method: IssueCommand<br/>
Parameters:
<ul>
    <li><b>caller_identity</b> String with ID unique to the plugin sending the request. No whitespace or special
        characters. e.g. "InsaneLimits"
    </li>
    <li><b>response_requested</b> true/false. Whether the caller would like a response with the outcome of the command.
    </li>
    <li><b>response_class</b> Only if response_requested is true. Class/plugin where the callback will be sent.</li>
    <li><b>response_method</b> Only if response_requested is true. Method within the target plugin that will accept the
        response.
    </li>
    <li><b>command_type</b> Command key that references the desired command. Examples: player_kill, player_ban_perm,
        admin_say.
    </li>
    <li><b>command_numeric</b> Used for commands like player_ban_temp that require a numerical input. Currently
        player_ban_temp is the only command that requires a command numeric, and will throw errors if a numeric is not
        provided. In all other cases this field is optional.
    </li>
    <li><b>source_name</b> Name of the source you would like database logged. For example an admin name, plugin name, or
        a custom name like AutoAdmin.
    </li>
    <li><b>target_name</b> The exact name of the target you would like to issue the command against, usually a player
        name. For commands like admin_nuke which don't accept a player name, special syntax is used, documentation of
        such is provided in the readme.
    </li>
    <li><b>target_guid</b> Only required when binding to onJoin, onLeave, or other events where the player may not be
        loaded into AdKats' live player list yet. If the player cannot be found in the live player list by target_name
        then this guid is used to fetch their information from the database and perform the command.
    </li>
    <li><b>record_message</b> The message or reason that should be used with the command. e.g. Baserape. Message can be
        up to 500 characters.
    </li>
</ul>
Response:
<ul>
    <li><b>caller_identity</b> AdKats</li>
    <li><b>response_requested</b> false</li>
    <li><b>response_type</b> IssueCommand</li>
    <li><b>response_value</b> List of all messages sent for the command, comparable to what an admin would see in-game.
        CPluginVariable.EncodeStringArray used to compact into one field. CPluginVariable.DecodeStringArray can be used
        to parse the field back into an array. If the command succeeds without issue there should (generally) only be
        one message.
    </li>
</ul>
If all the required parameters are provided, the command will execute and log to the database. Response sent if it was requested.
<br/>
<br/>
<b>Plugin Example:</b><br/>
<br/>
var requestHashtable = new Hashtable{<br/>
&#160;&#160;&#160;&#160;&#160;{"caller_identity", "YourPlugin"},<br/>
&#160;&#160;&#160;&#160;&#160;{"response_requested", false},<br/>
&#160;&#160;&#160;&#160;&#160;{"command_type", "player_ban_perm"},<br/>
&#160;&#160;&#160;&#160;&#160;{"source_name", "AutoTest"},<br/>
&#160;&#160;&#160;&#160;&#160;{"target_name", "ColColonCleaner"},<br/>
&#160;&#160;&#160;&#160;&#160;{"target_guid", "EA_698E70AF4E420A99824EA9A438FE3CB1"},<br/>
&#160;&#160;&#160;&#160;&#160;{"record_message", "Testing"}<br/>
};<br/>
ExecuteCommand("procon.protected.plugins.call", "AdKats", "IssueCommand", "YourPlugin", JSON.JsonEncode(requestHashtable));<br/>
<br/>
<b>InsaneLimits Example (OnKill Activation):</b><br/>
<br/>
Hashtable command = new Hashtable();<br/>
command.Add("caller_identity", "InsaneLimits");<br/>
command.Add("response_requested", false);<br/>
command.Add("command_type", "player_punish");<br/>
command.Add("source_name", "AutoAdmin");<br/>
command.Add("target_name", player.Name);<br/>
command.Add("target_guid", player.EAGuid);<br/>
command.Add("record_message", "Using restricted weapon " + kill.Weapon);<br/>
plugin.CallOtherPlugin("AdKats", "IssueCommand", command);
</p>
<HR>
<p>
    <a name=commands />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Commands.jpg" alt="AdKats User Manual">
</p>
<p>
    <u><b>You can edit the text for each command to suit your needs in plugin settings.</b></u>
</p>
<p>
    Commands can be accessed with '!', '@', '.', '/!', '/@', '/.', or just '/'.
</p>
<p>
    Any action command issued with no parameters (e.g. '!kill') will target the speaker.
    If admins want to kill, kick, or even ban themselves, simply type the action command without any parameters.
    Any action command when given a player name (other than moving players) will require a reason.
</p>
<h3>Universal Commands</h3>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Default Text</b></td>
        <td><b>Params</b></td>
        <td><b>Description</b></td>
    </tr>
    <tr>
        <td><b>Confirm Command</b></td>
        <td>yes</td>
        <td>None</td>
        <td>
            The in-game command used for confirming other commands when needed. Must be active. Must be accessible under 'Any'. Must use 'yes' as command text. Cannot be denied for any role.
        </td>
    </tr>
    <tr>
        <td><b>Cancel Command</b></td>
        <td>no</td>
        <td>None</td>
        <td>
            The in-game command used to cancel other commands when needed. Must be active. Must be accessible under 'Any'. Must use 'no' as command text. Cannot be denied for any role
        </td>
    </tr>
</table>
<h3>Action Commands</h3>
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
        <td>The in-game command used for killing players. If the player is dead, they will be killed on spawn.</td>
    </tr>
    <tr>
        <td><b>Kill Player (Force)</b></td>
        <td>fkill</td>
        <td>
            None<br/>
            OR<br/>
            [player][reason]<br/>
            OR<br/>
            [reportID]<br/>
            OR<br/>
            [reportID][reason]
        </td>
        <td>Bypasses all extra functionality of the regular kill command, issuing admin kill on them immediately.</td>
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
            [duration]<br/>
            OR<br/>
            [duration][player][reason]<br/>
            OR<br/>
            [duration][reportID]<br/>
            OR<br/>
            [duration][reportID][reason]
        </td>
        <td>
            The in-game command used for temp-banning players.
            Default time is in minutes, but the number can have a letter after it designating the units. e.g. 2h for 2
            hours. Valid suffixes are m, h, d, w, and y.
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
        <td><b>Future Perma-Ban Player</b></td>
        <td>fban</td>
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
            The in-game command used for future-banning players.
            Default time is in minutes, but the number can have a letter after it designating the units. e.g. 2h for 2
            hours. Valid suffixes are m, h, d, w, and y.
            <br/><br/>
            Future ban is the exact opposite of a temp-ban.
            Enter the time the player has until they are permabanned.
            This is used for requesting action/videos/etc from players, giving them a time frame to do so.
            Ban is enforced on-join only, not during gameplay.
            This command can only be used when ban enforcer is enabled.
        </td>
    </tr>
    <tr>
        <td><b>Unban Player</b></td>
        <td>unban</td>
        <td>
            [player]
            OR<br/>
            [player][reason]<br/>
        </td>
        <td>
            The in-game command used for unbanning players. This command can only be used when ban enforcer is enabled.
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
            Will add a Punish record to the database,
            increasing a player's total points according to your settings,
            and issue the configured action for that point value.
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
        <td><b>Warn Player</b></td>
        <td>warn</td>
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
            The in-game command used for warning players. This will give them a verbal warning across their screen, and log that they were warned.
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
        <td><b>On-Death Move Player</b></td>
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
            If the player is already dead, this command automatically changes to force move.
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
        <td><b>Pull Player</b></td>
        <td>pull</td>
        <td>
            [player]
        </td>
        <td>
            Pulls a player to your current squad, killing them in the process.
        </td>
    </tr>
    <tr>
        <td><b>Mark Player</b></td>
        <td>mark</td>
        <td>
            [player]
        </td>
        <td>
            Marks a player for admin notification if they leave the server.
        </td>
    </tr>
    <tr>
        <td><b>Lock Player</b></td>
        <td>lock</td>
        <td>
            [player]
        </td>
        <td>
            Locks a player from admin commands for 10 minutes. Only the locking admin will be able to act on them.
        </td>
    </tr>
    <tr>
        <td><b>Unlock Player</b></td>
        <td>unlock</td>
        <td>
            [player]
        </td>
        <td>
            Allows the locking admin to unlock a currently locked player.
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
        <td><b>Lead Current Squad</b></td>
        <td>lead</td>
        <td>
            none<br/>
            OR<br/>
            [player]
        </td>
        <td>
            The in-game command used to the speaker to leader of their current squad. When targeted at a player, that player
            will be given leader of their current squad. Only available in BF4.
        </td>
    </tr>
    <tr>
        <td><b>Assist Losing Team</b></td>
        <td>assist</td>
        <td>
            none
        </td>
        <td>
            The in-game command used to join the weak/losing team.
            Blocked from usage until 2 minutes into any round.
            Blocked from usage when teams are within 60 tickets of each other.
        </td>
    </tr>
    <tr>
        <td><b>Kill Self</b></td>
        <td>killme</td>
        <td>None</td>
        <td>
            The in-game command used for killing the speaker.
            Specific timeout of 10 minutes to avoid abuse.
        </td>
    </tr>
    <tr>
        <td><b>Server Nuke</b></td>
        <td>nuke</td>
        <td>[US/RU/ALL]</td>
        <td>
            The in-game command used for killing all players in a subset. US, RU, or ALL will work.
        </td>
    </tr>
    <tr>
        <td><b>Server Nuke Winning Team</b></td>
        <td>wnuke</td>
        <td>
            none
        </td>
        <td>
            The in-game command used for killing all players on the winning team when they also have more map control.
        </td>
    </tr>
    <tr>
        <td><b>SwapNuke Server</b></td>
        <td>swapnuke</td>
        <td>none</td>
        <td>
            The in-game command used for team-switching all players in the server.
            THIS IS EXPERIMENTAL, AND SHOULD BE USED WITH CAUTION.
            MULTIBalancer unswitcher is automatically disabled when using this command, and re-enabled once complete.
        </td>
    </tr>
    <tr>
        <td><b>Kick All Guests</b></td>
        <td>kickall</td>
        <td>None</td>
        <td>
            The in-game command used for kicking all players except admins.
        </td>
    </tr>
    <tr>
        <td><b>Dequeue Player Action</b></td>
        <td>deq</td>
        <td>
            [player]
        </td>
        <td>
            Canceles all queued actions on the target player. Moves, kills, etc.
        </td>
    </tr>
    <tr>
        <td><b>Manage AFK Players</b></td>
        <td>afk</td>
        <td>
            None
        </td>
        <td>
            Calls the AFK Management functionality manually. Cannot be used if AFK payers are being managed automatically.
        </td>
    </tr>
    <tr>
        <td><b>Force Player Loadout</b></td>
        <td>floadout</td>
        <td>
            [player]
        </td>
        <td>
            If the loadout enforcer is installd and enabled then this command will force that player up to trigger level enforcement.
        </td>
    </tr>
    <tr>
        <td><b>Set Own Battlecry</b></td>
        <td>battlecry</td>
        <td>
            NONE
        </td>
        <td>
            Sets the current player's battlecry. This will be displayed to the server when they spawn for the first time during a session.
        </td>
    </tr>
    <tr>
        <td><b>Set Player Battlecry</b></td>
        <td>setbattlecry</td>
        <td>
            [player]
        </td>
        <td>
            Sets a players current battlecry. This will be displayed to the server when they spawn for the first time during a session. This command overrides the word filter for battle cries.
        </td>
    </tr>
    <tr>
        <td><b>Force Manual Player Ping</b></td>
        <td>fping</td>
        <td>
            [player]
        </td>
        <td>
            Forces AdKats to ignore what the server claims a player's ping is, instead manually fetching the ping using procon.
        </td>
    </tr>
    <tr>
        <td><b>Link Player to Discord Member</b></td>
        <td>fping</td>
        <td>
            [player][discordmember]
        </td>
        <td>
            This command links a player with an active Discord member by ID. Sometimes this is required if their name in game vs discord does not match.
        </td>
    </tr>
</table>
<h3>Special Player Group Commands</h3>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Default Text</b></td>
        <td><b>Params</b></td>
        <td><b>Description</b></td>
    </tr>
    <tr>
        <td><b>Reserved Slot Player</b></td>
        <td>reserved</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to reserved slots for the current server. The setting "Feed Server
            Reserved Slots" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Un-Reserved Slot Player</b></td>
        <td>unreserved</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from reserved slots for the current server. The setting "Feed Server
            Reserved Slots" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Spectator Slot Player</b></td>
        <td>spectator</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to spectator list for the current server. The setting "Feed Server
            Spectator List" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Un-Spectator Slot Player</b></td>
        <td>unspectator</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from spectator slots for the current server. The setting "Feed Server
            Spectator List" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Ping Whitelist Player</b></td>
        <td>pwhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the ping kick whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>Un-Ping Whitelist Player</b></td>
        <td>unpwhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from ping kick whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>AA Whitelist Player</b></td>
        <td>aawhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the Admin Assistant whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>Un-AA Whitelist Player</b></td>
        <td>unaawhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from Admin Assistant whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>SpamBot Whitelist Player</b></td>
        <td>spamwhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the SpamBot whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>Un-SpamBot Whitelist Player</b></td>
        <td>unspamwhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from SpamBot whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>Autobalance Whitelist Player</b></td>
        <td>mbwhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to MULTIBalancer whitelist for the current server. The setting
            "Feed MULTIBalancer Whitelist" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Remove Autobalance Whitelist</b></td>
        <td>unmbwhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from MULTIBalancer whitelist for the current server. "Feed MULTIBalancer Whitelist" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Autobalance Disperse Player</b></td>
        <td>disperse</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to MULTIBalancer even dispersion for the current server. The setting "Feed MULTIBalancer Even Dispersion List" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Remove Autobalance Dispersion</b></td>
        <td>undisperse</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from MULTIBalancer even dispersion for the current server. "Feed MULTIBalancer Even Dispersion List" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Spectator Blacklist Player</b></td>
        <td>specblacklist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the Spectator Blacklist for all servers. Players under this group will not be able to enter the server as a spectator.
        </td>
    </tr>
    <tr>
        <td><b>Un-Spectator Blacklist Player</b></td>
        <td>unspecblacklist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from Spectator Blacklist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>Report Whitelist Player</b></td>
        <td>rwhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the Report Whitelist for all servers. Players under this group cannot be reported.
        </td>
    </tr>
    <tr>
        <td><b>Un-Report Blacklist Player</b></td>
        <td>unrwhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from Report Whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>Populator Whitelist Player</b></td>
        <td>popwhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the Populator Whitelist for all servers. Used when only allowing approved populators to be considered for automatic populator perks. Setting section B27-2.
        </td>
    </tr>
    <tr>
        <td><b>Un-Populator Blacklist Player</b></td>
        <td>unpopwhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from Populator Whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>TeamKillTracker Whitelist Player</b></td>
        <td>tkwhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the TeamKillTracker Whitelist for all servers. "Feed TeamKillTracker Whitelist" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>Un-TeamKillTracker Whitelist Player</b></td>
        <td>untkwhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from TeamKillTracker Whitelist for all servers. "Feed TeamKillTracker Whitelist" must be enabled to use this command.
        </td>
    </tr>
    <tr>
        <td><b>AntiCheat Whitelist Player</b></td>
        <td>acwhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the AntiCheat Whitelist for all servers. A player under AntiCheat whitelist will not be acted on in any way by either the normal AntiCheat scripts or by the LIVE system.
        </td>
    </tr>
    <tr>
        <td><b>Remove AntiCheat Whitelist</b></td>
        <td>unacwhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from AntiCheat Whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>Command Target Whitelist Player</b></td>
        <td>cwhitelist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the Command Target Whitelist for all servers. A player under Command Target whitelist cannot be targeted by any commands configured in the 'Command Target Whitelist Commands' setting.
        </td>
    </tr>
    <tr>
        <td><b>Remove Command Target Whitelist</b></td>
        <td>uncwhitelist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from Command Target Whitelist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>Auto-Assist Blacklist Player</b></td>
        <td>auablacklist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the Auto-Assist Blacklist for all servers. A player under Auto-Assist Blacklist will always be checked if they are able to assist the weak team, and if they are they are automatically moved to that team.
        </td>
    </tr>
    <tr>
        <td><b>Remove Auto-Assist Blacklist</b></td>
        <td>unauablacklist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from Auto-Assist Blacklist for all servers.
        </td>
    </tr>
    <tr>
        <td><b>All-Caps Chat Blacklist Player</b></td>
        <td>allcapsblacklist</td>
        <td>
            [duration or 'perm']<br/>
            OR<br/>
            [duration or 'perm'][player]<br/>
            OR<br/>
            [duration or 'perm'][player][reason]<br/>
        </td>
        <td>
            The in-game command used for adding a player to the All-Caps Chat Blacklist for all servers. A player under All-Caps Chat Blacklist will be specifically enforced by the all-caps monitoring system.
        </td>
    </tr>
    <tr>
        <td><b>Remove All-Caps Chat Blacklist</b></td>
        <td>unallcapsblacklist</td>
        <td>
            [player]
        </td>
        <td>
            The in-game command used for removing a player from All-Caps Chat Blacklist for all servers.
        </td>
    </tr>
</table>
<h3>Reporting Commands</h3>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Default Text</b></td>
        <td><b>Params</b></td>
        <td><b>Description</b></td>
    </tr>
    <tr>
        <td><b>Report Player</b></td>
        <td>report</td>
        <td>[player][reason]</td>
        <td>
            The in-game command used for reporting players.
            Must have a reason, and will inform a player otherwise when used incorrectly.
            Will log a Report in the database (External GCP pulls from there for external admin notifications), and notify
            all in-game admins.
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
        <td><b>Accept Round Report</b></td>
        <td>accept</td>
        <td>
            [reportID]
        </td>
        <td>
            The in-game command used for accepting reports as confirmed.
        </td>
    </tr>
    <tr>
        <td><b>Deny Round Report</b></td>
        <td>deny</td>
        <td>
            [reportID]
        </td>
        <td>
            The in-game command used for denying reports.
        </td>
    </tr>
    <tr>
        <td><b>Ignore Round Report</b></td>
        <td>ignore</td>
        <td>
            [reportID]
        </td>
        <td>
            The in-game command used for ignoring reports.
        </td>
    </tr>
    <tr>
        <td><b>Contest Report</b></td>
        <td>contest</td>
        <td>
            None
        </td>
        <td>
            Usable by players to contest round reports before admins act on them.
        </td>
    </tr>
</table>
<h3>Communication Commands</h3>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Default Text</b></td>
        <td><b>Params</b></td>
        <td><b>Description</b></td>
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
            Chat access must be AnyHidden.
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
            Chat access must be AnyHidden.
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
            Chat access must be AnyHidden.
        </td>
    </tr>
    <tr>
        <td><b>Player Private Message</b></td>
        <td>msg</td>
        <td>
            [player][message]
        </td>
        <td>
            Opens a conversation with the given player. The player can either be in the current server, or any other
            BF3/BF4 server on your database.
        </td>
    </tr>
    <tr>
        <td><b>Player Private Reply</b></td>
        <td>r</td>
        <td>
            [message]
        </td>
        <td>
            Replies to a currently open conversation with the given message.
        </td>
    </tr>
    <tr>
        <td><b>Admin Private Message</b></td>
        <td>adminmsg</td>
        <td>
            [message]
        </td>
        <td>
            Sends a message to all online admins in the server. They can then open a private message with the sender to
            reply to the admin message.
        </td>
    </tr>
    <tr>
        <td><b>Server Countdown</b></td>
        <td>cdown</td>
        <td>[seconds] [squad/team/all] [reason]</td>
        <td>
            The in-game command used for issuing countdowns on a subset of players.
        </td>
    </tr>
</table>
<h3>Information Commands</h3>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Default Text</b></td>
        <td><b>Params</b></td>
        <td><b>Description</b></td>
    </tr>
    <tr>
        <td><b>What Is</b></td>
        <td>whatis</td>
        <td>
            [commandName]<br/>
            OR<br/>
            [preMessageID]
        </td>
        <td>
            The in-game command used for finding out what a particular preMessage ID, or command name, means.
        </td>
    </tr>
    <tr>
        <td><b>Request Rules</b></td>
        <td>rules</td>
        <td>
            none<br/>
            OR<br/>
            [player]
        </td>
        <td>
            The in-game command used to request the server rules. When targeted at a player, that player will be told the
            server rules. When targeted at a player, the command goes on timeout for that player.
        </td>
    </tr>
    <tr>
        <td><b>Request Online Admins</b></td>
        <td>admins</td>
        <td>
            none
        </td>
        <td>
            The in-game command used to get the list of current online admins.
        </td>
    </tr>
    <tr>
        <td><b>Request Uptimes</b></td>
        <td>uptime</td>
        <td>
            none
        </td>
        <td>
            The in-game command used to get the uptime of the server, procon/layer, AdKats, and several other things.
        </td>
    </tr>
    <tr>
        <td><b>List Round Reports</b></td>
        <td>reportlist</td>
        <td>
            none
        </td>
        <td>
            The in-game command used to get the latest 6 unanswered round reports.
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
        <td><b>Fetch Player Info</b></td>
        <td>pinfo</td>
        <td>
            [player]
        </td>
        <td>
            Fetches extended information about the player.
            Player name, ID, role, team name, team position, current score, time first seen, amount of time spent on current
            server, city location, IP change count, reports from/against during current round, infraction points, last
            punishment time/reason, reputation, and previous names.
        </td>
    </tr>
    <tr>
        <td><b>Fetch Player Chat</b></td>
        <td>pchat</td>
        <td>
            None<br/>
            OR<br/>
            [chatLines]<br/>
            OR<br/>
            [player]<br/>
            OR<br/>
            [chatLines][player]<br/>
            OR<br/>
            self [player]<br/>
            OR<br/>
            [chatLines] self [player]<br/>
            OR<br/>
            [player1][player2]<br/>
            OR<br/>
            [chatLines][player1][player2]
        </td>
        <td>
            Fetches chat history or conversation history between players.
        </td>
    </tr>
    <tr>
        <td><b>Request Reputation</b></td>
        <td>rep</td>
        <td>
            none<br/>
            OR<br/>
            [player]
        </td>
        <td>
            The in-game command used to request the server reputation. When targeted at a player, you will be told that
            player's reputation. Requesting a player's reputation other than your own is admin only.
        </td>
    </tr>
    <tr>
        <td><b>Request Server Commands</b></td>
        <td>help</td>
        <td>
            None
        </td>
        <td>
            Lists the server commands you can access.
        </td>
    </tr>
    <tr>
        <td><b>Find Player</b></td>
        <td>find</td>
        <td>
            [player]
        </td>
        <td>
            Returns the team, position, and score, of the targeted player.
            Chat access must be AnyHidden.
        </td>
    </tr>
    <tr>
        <td><b>Player Is Admin</b></td>
        <td>isadmin</td>
        <td>
            [player]
        </td>
        <td>
            Returns whether the given player is an admin, and states their role.
        </td>
    </tr>
    <tr>
        <td><b>Fetch Admin Status</b></td>
        <td>isadmin</td>
        <td>
            [player]
        </td>
        <td>
            Fetches the admin status and current role of the player.
        </td>
    </tr>
    <tr>
        <td><b>Fetch Player Loadout</b></td>
        <td>loadout</td>
        <td>
            [player]
        </td>
        <td>
            If the loadout enforcer is installed and enabled this command will return a player's current loadout.
        </td>
    </tr>
    <tr>
        <td><b>Fetch Player Perks</b></td>
        <td>perks</td>
        <td>
            None<br/>
            OR<br/>
            [player]
        </td>
        <td>
            Returns a player's current active perks and when they are set to expire.
        </td>
    </tr>
    <tr>
        <td><b>Fetch Player Ping</b></td>
        <td>ping</td>
        <td>
            None<br/>
            OR<br/>
            [player]
        </td>
        <td>
            Returns a player's current and average ping.
        </td>
    </tr>
    <tr>
        <td><b>Debug Assist Losing Team</b></td>
        <td>dassist</td>
        <td>
            [player]
        </td>
        <td>
            Returns whether a player is able to assist or not, and why.
        </td>
    </tr>
</table>
<h3>Logging Commands</h3>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Default Text</b></td>
        <td><b>Params</b></td>
        <td><b>Description</b></td>
    </tr>
    <tr>
        <td><b>Log Player Information</b></td>
        <td>log</td>
        <td>
            [player][message]
        </td>
        <td>
            The in-game command used for logging a message on a player's record. Does not affect their gameplay in any way.
        </td>
    </tr>
    <tr>
        <td><b>Give Server Feedback</b></td>
        <td>feedback</td>
        <td>
            message
        </td>
        <td>
            Logs the given message as feedback for the server.
        </td>
    </tr>
</table>
<h3>Voting Commands</h3>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Default Text</b></td>
        <td><b>Params</b></td>
        <td><b>Description</b></td>
    </tr>
    <tr>
        <td><b>Vote Surrender</b></td>
        <td>surrender</td>
        <td>
            none
        </td>
        <td>
            The in-game command used for starting/voting for a round surrender. Losing team specific, but either surrender
            or votenext can be used.
        </td>
    </tr>
    <tr>
        <td><b>Vote Next Round</b></td>
        <td>votenext</td>
        <td>
            none
        </td>
        <td>
            The in-game command used for starting/voting for a round surrender. Losing team specific, but either surrender
            or votenext can be used.
        </td>
    </tr>
    <tr>
        <td><b>Vote Against Surrender</b></td>
        <td>nosurrender</td>
        <td>
            none
        </td>
        <td>
            The in-game command used for voting AGAINST a currently active round surrender. This command may only be
            used by the losing team.
        </td>
    </tr>
</table>
<h3>Maintenance Commands</h3>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Default Text</b></td>
        <td><b>Params</b></td>
        <td><b>Description</b></td>
    </tr>
    <tr>
        <td><b>Restart Current Round</b></td>
        <td>restart</td>
        <td>None</td>
        <td>
            The in-game command used for restarting the round.
        </td>
    </tr>
    <tr>
        <td><b>Run Next Round</b></td>
        <td>nextlevel</td>
        <td>None</td>
        <td>
            The in-game command used for running the next map in current rotation, but keep all points and KDRs from this
            round.
        </td>
    </tr>
    <tr>
        <td><b>Restart AdKats</b></td>
        <td>prestart</td>
        <td>none</td>
        <td>
            The in-game command used for rebooting the AdKats instance. Requires confirmation.
        </td>
    </tr>
    <tr>
        <td><b>Shutdown Server</b></td>
        <td>shutdown</td>
        <td>none</td>
        <td>
            The in-game command used for shutting down/rebooting the Battlefield server. Requires confirmation.
        </td>
    </tr>
    <tr>
        <td><b>End Current Round</b></td>
        <td>endround</td>
        <td>[US/RU]</td>
        <td>
            The in-game command used for ending the current round with a winning team. Either US or RU.
        </td>
    </tr>
    <tr>
        <td><b>Plugin Update</b></td>
        <td>pupdate</td>
        <td>
            None
        </td>
        <td>
            Calls manual update of AdKats source and any connected extensions to their latest versions.
        </td>
    </tr>
</table>
<HR>
<p>
    <a name=webrequests />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Web.jpg" alt="AdKats User Manual">
</p>
<p>
    Some layer providers require whitelisting of connections through their firewall.
</p>
<p>
    <b>For AdKats to function properly, requests to the following domains/sub-domains must be allowed from your layer for http and https connections. If you do not understand what this means, please send this instruction and the list below to your layer host:</b>
</p>
<p>
<table>
    <tr>
        <td><b>Domain</b></td>
        <td><b>Usage</b></td>
    </tr>
    <tr>
        <td>battlelog.battlefield.com</td>
        <td>Player identity tracking and AntiCheat.</td>
    </tr>
    <tr>
        <td>raw.github.com</td>
        <td>Global configuration/documentation fetching, and database updates.</td>
    </tr>
    <tr>
        <td>raw.githubusercontent.com</td>
        <td>Global configuration/documentation fetching, and database updates.</td>
    </tr>
    <tr>
        <td>sourceforge.net</td>
        <td>Source updates.</td>
    </tr>
    <tr>
        <td>adkats.gamerethos.net</td>
        <td>Version management, and backup source for definition files.</td>
    </tr>
    <tr>
        <td>ip-api.com</td>
        <td>Player location tracking.</td>
    </tr>
    <tr>
        <td>metabans.com</td>
        <td>Ban enforcer posting.</td>
    </tr>
    <tr>
        <td>www.timeanddate.com</td>
        <td>Global UTC Timestamp</td>
    </tr>
</table>
</p>
<p>
    All are either simple GET or POST requests.
</p>
<p>
    <b>The following URLS are used for reputation stats, special player groups, database updates, and weapon stats for AntiCheat system:</b>
</p>
<p>
<table>
    <tr>
        <td><b>Link</b></td>
        <td><b>Usage</b></td>
    </tr>
    <tr>
        <td><a href="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/adkatsreputationstats.json" target="_blank">adkatsreputationstats.json</a></td>
        <td>Command Reputation Constants</td>
    </tr>
    <tr>
        <td><a href="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/adkatsspecialgroups.json" target="_blank">adkatsspecialgroups.json</a></td>
        <td>Special Player Group Definitions</td>
    </tr>
    <tr>
        <td><a href="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/adkatsupdates.json" target="_blank">adkatsupdates.json</a></td>
        <td>Database Updates</td>
    </tr>
    <tr>
        <td><a href="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/adkatsblweaponstats.json" target="_blank">adkatsblweaponstats.json</a></td>
        <td>Battlefield Weapon Stats (Damages)</td>
    </tr>
</table>
</p>
<HR>
<p>
    <a name=servercommands />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Server.jpg" alt="AdKats User Manual">
</p>
<p>
    AdKats issues specific server commands to execute its functions, and run properly. Below are their listings, usages, and intervals of usage.
</p>
<p>
<table>
    <tr>
        <td><b>Command</b></td>
        <td><b>Usage</b></td>
        <td><b>Interval</b></td>
    </tr>
    <tr>
        <td><b>serverInfo</b></td>
        <td>Fetching server info</td>
        <td>Plugin start, 10 second interval.</td>
    </tr>
    <tr>
        <td><b>vars.teamFactionOverride</b></td>
        <td>Fetching team definitions</td>
        <td>Plugin start, round start.</td>
    </tr>
    <tr>
        <td><b>punkBuster.pb_sv_command</b></td>
        <td>Triggering punkbuster screenshots</td>
        <td>Admin report and calladmin commands.</td>
    </tr>
    <tr>
        <td><b>squad.private</b></td>
        <td>Setting whether a squad should be private or not</td>
        <td>Admin join and pull commands.</td>
    </tr>
    <tr>
        <td><b>squad.leader</b></td>
        <td>Assigning squad leader</td>
        <td>Admin lead commands.</td>
    </tr>
    <tr>
        <td><b>player.isAlive</b></td>
        <td>(BF4 only) Checking if a player is alive</td>
        <td>Kill and move commands.</td>
    </tr>
    <tr>
        <td><b>admin.killPlayer</b></td>
        <td>Killing players</td>
        <td>Admin kill and nuke commands, either automatic or manual.</td>
    </tr>
    <tr>
        <td><b>admin.movePlayer</b></td>
        <td>Moving players between teams</td>
        <td>Admin move commands, and players attempting to move from locked teams.</td>
    </tr>
    <tr>
        <td><b>admin.kickPlayer</b></td>
        <td>Kicking players</td>
        <td>Admin kick commands, either automatic or manual.</td>
    </tr>
    <tr>
        <td><b>admin.say</b></td>
        <td>Sending say to either server or private player</td>
        <td>Admin say and tell commands, along with any automated functions that require notification to the user.</td>
    </tr>
    <tr>
        <td><b>admin.yell</b></td>
        <td>Sending yell to either server or private player</td>
        <td>Admin yell and tell commands, along with any automated functions that require unavoidable notification to the user.</td>
    </tr>
    <tr>
        <td><b>admin.listPlayers</b></td>
        <td>Listing current server players</td>
        <td>Plugin start, 10 second interval when TeamSwap queues are not empty.</td>
    </tr>
    <tr>
        <td><b>admin.shutDown</b></td>
        <td>Shutting down/rebooting the battlefield server</td>
        <td>Admin shutdown commands.</td>
    </tr>
    <tr>
        <td><b>banList.list</b></td>
        <td>Listing current server banlist</td>
        <td>If Ban Enforcer is enabled, when new ban(s) are added to the server, if not, after adding new bans to the server.</td>
    </tr>
    <tr>
        <td><b>banList.add</b></td>
        <td>Adding entries to the server ban list</td>
        <td>Admin ban commands, either automatic or manual, when ban enforcer is disabled, or when importing bans from ban enforcer back into the server.</td>
    </tr>
    <tr>
        <td><b>banList.save</b></td>
        <td>Saving the server ban list</td>
        <td>After adding new bans to the server, or, if ban enforcer is enabled, after clearing the server ban list.</td>
    </tr>
    <tr>
        <td><b>mapList.restartRound</b></td>
        <td>Restarting the current level, removing scores.</td>
        <td>Admin restartLevel commands.</td>
    </tr>
    <tr>
        <td><b>mapList.runNextRound</b></td>
        <td>Running the next map in the list, keeping scores.</td>
        <td>Admin nextLevel commands.</td>
    </tr>
    <tr>
        <td><b>mapList.endRound</b></td>
        <td>Ending the current round with a winner.</td>
        <td>Admin endLevel commands, round timer, surrender vote, and auto-surrender.</td>
    </tr>
    <tr>
        <td><b>reservedSlotsList.remove</b></td>
        <td>Removing entries from the reserved slot list.</td>
        <td>Reserved slot orchestration.</td>
    </tr>
    <tr>
        <td><b>reservedSlotsList.add</b></td>
        <td>Adding entries to the reserved slot list.</td>
        <td>Reserved slot orchestration.</td>
    </tr>
    <tr>
        <td><b>reservedSlotsList.save</b></td>
        <td>Saving the server reserved slot list</td>
        <td>Reserved slot orchestration.</td>
    </tr>
    <tr>
        <td><b>reservedSlotsList.list</b></td>
        <td>Fetching updated server reserved slot list</td>
        <td>Reserved slot orchestration.</td>
    </tr>
    <tr>
        <td><b>spectatorList.remove</b></td>
        <td>(BF4 only) Removing entries from the allowed spectator list.</td>
        <td>Allowed spectator orchestration.</td>
    </tr>
    <tr>
        <td><b>spectatorList.add</b></td>
        <td>(BF4 only) Adding entries to the allowed spectator list.</td>
        <td>Allowed spectator orchestration.</td>
    </tr>
    <tr>
        <td><b>spectatorList.save</b></td>
        <td>(BF4 only) Saving the server allowed spectator list</td>
        <td>Allowed spectator orchestration.</td>
    </tr>
    <tr>
        <td><b>spectatorList.list</b></td>
        <td>(BF4 only) Fetching updated server allowed spectator list</td>
        <td>Allowed spectator orchestration.</td>
    </tr>
</table>
</p>
<HR>
<p>
    <a name=settings />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Settings.jpg" alt="AdKats User Manual">
</p>
<h3>0. Instance Settings:</h3>
<ul>
    <li><b>'Auto-Enable/Keep-Alive'</b> - 
        When this is enabled, AdKats will auto-recover from shutdowns and auto-restart if disabled.
    </li>
    <li><b>'Lock Settings - Create Password'</b> - 
        Lock settings with a new created password. 
        Must be greater than 5 characters.
    </li>
    <li><b>'Lock Settings'</b> - 
        Lock settings with the existing settings password.
    </li>
    <li><b>'Unlock Settings'</b> - 
        Unlock settings with the existing settings password.
    </li>
</ul>
<h3>1. Server Settings:</h3>
<ul>
    <li><b>'Setting Import'</b> - 
        Enter an existing server ID here and all settings from that instance will be imported here. 
        Settings on this instance will be overwritten.
    </li>
    <li><b>'Server ID (Display)'</b> - 
        ID of this server. 
        Automatically set by the database.
    </li>
    <li><b>'Server IP (Display)'</b> - 
        IP address and port of this server. 
        Automatically set by Procon.
    </li>
    <li><b>'Server Round (Display)'</b> - 
        The current round number as determined by the extended round statistics log.
    </li>
    <li><b>'Server Game (Display)'</b> - 
        Game version of this server as recognized by AdKats.
        Automatically set by Procon.
    </li>
    <li><b>'Low Population Value'</b> - 
        Number of players at which the server is deemed 'Low Population'.
        Server is considered 'Medium Population' when the number of players is between the low and high population values.
    </li>
    <li><b>'High Population Value'</b> - 
        Number of players at which the server is deemed 'High Population'.
        Server is considered 'Medium Population' when the number of players is between the low and high population values.
    </li>
    <li><b>'Automatic Server Restart When Empty'</b> - 
        Enable/Disable automatically rebooting the server when it is empty. 
        Automatically accounts for seeder players.
    </li>
    <li><b>'Automatic Restart Minimum Uptime Hours'</b> - 
        When automatically rebooting the server, this is the minimum number of hours that the server must be online before the automatic reboot is triggered.
    </li>
    <li><b>'Automatic Procon Reboot When Server Reboots'</b> - 
        Only enable this setting if you have procon set up on a service which automatically reboots if it crashes. 
        This setting will cause procon to kill its own process shortly after the server is rebooted.
    </li>
</ul>
<h3>2. MySQL Settings:</h3>
<ul>
    <li><b>'MySQL Hostname'</b> to- 
        Hostname of the MySQL server AdKats should connect to.
    </li>
    <li><b>'MySQL Port'</b> - 
        Port of the MySQL server AdKats should connect to.
        Most of the time it's 3306.
    </li>
    <li><b>'MySQL Database'</b> - 
        Database name AdKats should use for storage. 
        Creation script given in database section.
    </li>
    <li><b>'MySQL Username'</b> - 
        Username of the MySQL server AdKats should connect to.
    </li>
    <li><b>'MySQL Password'</b> - 
        Password of the MySQL server AdKats should connect to.
    </li>
</ul>
<h3>3. User Settings:</h3>
<ul>
    <li><b>'Add User'</b> - 
        Add a user to the user list by entering their unique username here.
    </li>
    <li><b>*User Email*</b> - 
        Current stored email address of the listed user.
    </li>
    <li><b>*User Expiration*</b> - 
        Date that the user will revert to Default Guest role.
    </li>
    <li><b>*User Notes*</b> - 
        Any notes that are logged for the user.
    </li>
    <li><b>*User Role*</b> - 
        Current role of the listed user.
    </li>
    <li><b>*Delete User?*</b> - 
        Type delete in this line to delete the listed user.
    </li>
    <li><b>*Add Soldier?*</b> - 
        Type a logged soldier name in this line to connect it to the listed user. 
        Unique soldiers cannot be connected to more than one user at a time.
    </li>
    <li><b>*Delete Soldier?*</b> - 
        Type delete in this line to remove the listed soldier connection from the user.
    </li>
</ul>
<h3>3-2. Special Player Display:</h3>
<ul>
    <li><b>*Special Player Group Name* (Display)</b> - 
        Displays all players specifically assigned the given group for either all servers or this server specifically.
    </li>
</ul>
<h3>3-3. Verbose Special Player Display:</h3>
<ul>
    <li><b>*Verbose Special Player Group Name* (Display)</b> - 
        Displays all players assigned to a group, even if they aren't specifically assigned to it. 
        For example admins would be included in this list if you are automatically assigning reserved slot to admins.
        Many things can cause players to be assigned groups, and this section displays the final result.
    </li>
</ul>
<h3>4. Role Settings:</h3>
<ul>
    <li><b>'Add Role'</b> - 
        Type a new role name in this line to add a new role. Role names must be unique.
    </li>
</ul>
<p>
    Listed below "Add Role" are all the command assignments for each role.
    Change Allow/Deny for each command for the different user roles to control their access.
    Type delete in the "delete?" line to delete the user role.
    When a user role is deleted, all users on that role are changed to the Default Guest role.
    You cannot delete the Default Guest role.
    Commands that are classified as admin commands (commands that when enabled will make a role be considered admin), show up in the list with [ADMIN] next to them. Commands that are causing a role to be considered admin will have a '<---' next to the command, so it's easy to find those commands in the list.
</p>
<h3>4-2. Role Group Settings:</h3>
<p>
    Listed in this section is an entry for each possible special player group, on every role in your role list. 
    Setting 'Assign' to any entry will place all users/soldiers under that role on the selected special player group. 
    Some groups may be forced to 'Assign' by other options you've set around AdKats, for example if you've fed reserved slots for admins that group is force assigned for all admin roles.
</p>
<h3>5. Command Settings:</h3>
<ul>
    <li><b>'Minimum Required Reason Length'</b> - 
        The minimum length a reason must be for commands that require a reason to execute. 
        This only applies to admin commands.
    </li>
    <li><b>'Minimum Report Handle Seconds'</b> - 
        The minimum number of seconds before a report can be acted on by admins using report ID.
    </li>
    <li><b>'Minimum Minutes Into Round To Use Assist'</b> - 
        The number of minutes that a round needs to be active before players are allowed to use the !assist command.
    </li>
    <li><b>'Maximum Temp-Ban Duration Minutes'</b> - 
        The maximum number of minutes that a temp-ban can be issued for.
    </li>
    <li><b>'Countdown Duration before a Nuke is fired'</b> - 
        When an admin or the auto-nuke system issues a nuke on the server, this is the number of seconds warning that the server is given before the nuke is actually fired.
    </li>
    <li><b>'Allow Commands from Admin Say'</b> - 
        When this is enabled, all admins with procon access have unrestricted access to all enabled commands through procon's chat tab. 
        When issuing commands through procon's chat tab, the commands must be prefixed with a / for them to work.
    </li>
    <li><b>'Bypass all command confirmation -DO NOT USE-'</b> - 
        Disables all command confirmation. Do not use this setting unless you want to kick/ban the wrong people.
    </li>
    <li><b>'External plugin player commands'</b> - 
        List of commands from other plugins (with prefixes) that general players can access. Currently used for the help command.
    </li>
    <li><b>'External plugin admin commands'</b> - 
        List of commands from other plugins (with prefixes) that admins can access. Currently used for the help command.
    </li>
    <li><b>'Command Target Whitelist Commands'</b> - 
        List of commands that will be blocked when attempted to issue on players which have the command target whitelist group.
    </li>
</ul>
<h3>6. Command List:</h3>
<ul>
    <li><b>*Active*</b> - 
        Globally disable or enable the given command.
    </li>
    <li><b>*Logging*</b> - Set whether usage of this command is logged. All commands log by default.
    </li>
    <li><b>*Text*</b> - 
        Globally change the in-game text for this command. Command text must be unique.
    </li>
    <li><b>*Access Method*</b> - 
        The method that must be used to access this command from in-game. 
        Options are 'Any', 'AnyHidden', 'AnyVisible', 'GlobalVisible', 'TeamVisible', and 'SquadVisible'.
    </li>
</ul>
<h3>7. Punishment Settings:</h3>
<ul>
    <li><b>'Punishment Hierarchy'</b> - 
        List of punishments in the order you want them to be issued on players. Index in the list is the action taken at that number of infraction points. The default hierarchy is only an example list containing the available options, you should customize it for your server.
    </li>
    <li><b>'Combine Server Punishments'</b> - 
        Whether to make punishes from all servers on the connected database affect players on this server. Default is false.
    </li>
    <li><b>'Automatic Forgives'</b> - 
        Whether to enable automatic forgives on players who have infractions on the current server. These automatic forgives are given after they have clean play for a defined amount of time. By default auto-forgives start a month after last punishment, and every 2 weeks thereafter.
    </li>
    <li><b>'Automatic Forgive Days Since Punished'</b> - 
        The number of days since last punished required for an automatic forgive to be issued. Default is 30 days.
    </li>
    <li><b>'Automatic Forgive Days Since Forgiven'</b> - 
        The number of days since last forgiven required for an automatic forgive to be issued. Default is 14 days.
    </li>
    <li><b>'Only Kill Players when Server in low population'</b> - 
        When server population is below 'Low Population Value' only kill players so server does not empty. Player infraction points will be incremented normally.
    </li>
    <li><b>'Use IRO Punishment'</b> - 
        Whether the IRO punishment described in the infraction tracking docs will be used.
    </li>
    <li><b>'IRO Timeout Minutes'</b> - 
        Number of minutes after a punish that IRO status expires for the next punish.
    </li>
    <li><b>'IRO Punishment Overrides Low Pop'</b> - 
        When punishing players if a player gets an IRO punish it will ignore whether server is in low population or not.
    </li>
    <li><b>'IRO Punishment Infractions Required to Override'</b> - 
        Minimum number of infraction points a player must have for IRO punishments to override low population actions.
    </li>
</ul>
<h3>8. Email Settings:</h3>
<ul>
    <li><b>'Send Emails.'</b> - 
        Whether sending emails will be enabled.
    </li>
    <li><b>'Use SSL?'</b> - 
        Whether SSL will be used for connection to the SMTP server.
    </li>
    <li><b>'SMTP-Server address.'</b> - 
        Address of the SMTP server.
    </li>
    <li><b>'SMTP-Server port.'</b> - 
        Port to use for the SMTP server.
    </li>
    <li><b>'Sender address.'</b> - 
        The email address used to send all emails.
    </li>
    <li><b>'SMTP-Server username.'</b> - 
        The username used to authenticate into the SMTP server.
    </li>
    <li><b>'SMTP-Server password.'</b> - 
        The password used to authenticate into the SMTP server.
    </li>
    <li><b>'Custom HTML Addition.'</b> - 
        Custom HTML to add to the end of each email. 
        String replacements include: %player_id%, $player_name%, %player_guid%, %player_pbguid%, %player_ip%
    </li>
    <li><b>'Extra Recipient Email Addresses.'</b> - 
        List of all extra email addresses beside user email addresses that you would like to blast.
    </li>
    <li><b>'Only Send Report Emails When Admins Offline.'</b> - 
        Only send report notification emails when there are no admins in the server.
    </li>
</ul>
<h3>8-2. PushBullet Settings:</h3>
<ul>
    <li><b>'Send PushBullet Reports'</b> - 
        Whether to enable the PushBullet integration for AdKats reports against players.
    </li>
    <li><b>'PushBullet Access Token'</b> - 
        The access token required for sending notes through the PushBullet service.
    </li>
    <li><b>'PushBullet Note Target'</b> - 
        Either Private or Channel. 
        Private messages are to a specific person, channel messages need a separate channel tag.
    </li>
    <li><b>'PushBullet Channel Tag'</b> - 
        If Channel is chosen for the note target this setting takes the required tag to identify which channel to use.
    </li>
    <li><b>'Only Send PushBullet Reports When Admins Offline'</b> - 
        When this is enabled AdKats will only use the PushBullet system when there are no admins in the server.
    </li>
</ul>
<h3>9. TeamSwap Settings:</h3>
<ul>
    <li><b>'Ticket Window High'</b> - 
        When either team is above this ticket count, nobody (except admins) will be able to use TeamSwap.
    </li>
    <li><b>'Ticket Window Low'</b> - 
        When either team is below this ticket count, nobody (except admins) will be able to use TeamSwap.
    </li>
</ul>
<h3>A10. Admin Assistant Settings:</h3>
<ul>
    <li><b>'Enable Admin Assistants'</b> - 
        Whether admin assistant statuses can be assigned to players.
    </li>
    <li><b>'Minimum Confirmed Reports Per Month'</b> - 
        How many confirmed reports the player must have in the past month to be considered an admin assistant.
    </li>
    <li><b>'Enable Admin Assistant Perk'</b> - 
        Whether admin assistants will get the TeamSwap perk for their help.
    </li>
    <li><b>'Use AA Report Auto Handler'</b> - 
        Whether the internal auto-handling system for admin assistant reports is enabled.
    </li>
    <li><b>'Auto-Report-Handler Strings'</b> - 
        List of trigger words/phrases that the auto-handler will act on. One per line.
    </li>
</ul>
<h3>A11. Player Mute Settings:</h3>
<ul>
    <li><b>'On-Player-Muted Message'</b> - 
        The message given to players when they are muted by an admin.
    </li>
    <li><b>'On-Player-Killed Message'</b> - 
        The message given to players when they are killed for talking in chat after muting.
    </li>
    <li><b>'On-Player-Kicked Message'</b> - 
        The message given to players when they are kicked for talking more than X times in chat after muting.
    </li>
    <li><b>'# Chances to give player before kicking'</b> - 
        The number of chances players get to talk after being muted before they are kicked. 
        After testing, 4 appears to be the perfect number, but change as desired.
    </li>
    <li><b>'Ignore commands for mute enforcement'</b> - 
        Whether to ignore commands when enforcing mute status on a player.
    </li>
</ul>
<h3>A12. Messaging Settings:</h3>
<ul>
    <li><b>'Display Admin Name in Action Announcement'</b> -
        When players are kicked or banned from the server, the whole server is told.
        This changes whether the message includes the acting admin name.
    </li>
    <li><b>'Display New Player Announcement'</b> - 
        Whether to inform admins when a player joins the server for the first time. 
        This message is displayed when the player actually spawns.
    </li>
    <li><b>'Display Player Name Change Announcement'</b> - 
        Whether to inform admins when a player joins the server with a changed name, or a changed clan tag.
    </li>
    <li><b>'Display Targeted Player Left Notification'</b> - 
        Whether to inform admins when a player they acted on leaves the server.
    </li>
    <li><b>'Display Ticket Rates in Procon Chat'</b> - 
        Whether to display team ticket loss/gain rates in the Procon chat tab. 
        Useful for setting values in auto-surrender.
        This also includes flag estimates for conquest, and estimations of the winning team based on current ticket loss rates.
    </li>
    <li><b>'Inform players of reports against them'</b> - 
        Whether to inform players when someone reports them.
    </li>
    <li><b>'Player Inform Exclusion Strings'</b> - 
        List of words or phrases that will cancel informing reported players.
        For example, use if you don't want players to know if someone reports them with "hack", or "cheat" in the message.
    </li>
    <li><b>'Inform reputable players of admin joins'</b> - 
        Whether to tell reputable players that an admin joins the server.
    </li>
    <li><b>'Inform admins of admin joins'</b> - 
        Whether to tell admins that an admin joins the server.
    </li>
    <li><b>'Yell display time seconds'</b> - 
        The time in seconds that yell messages will be displayed.
    </li>
    <li><b>'Pre-Message List'</b> - 
        List of messages mapped to IDs that can be used in action commands. 
        e.g. !kill mustardman 23. 
        The !whatis command can be used to check what each ID means.
    </li>
    <li><b>'Require Use of Pre-Messages'</b> - 
        Whether using pre-messages in commands is required instead of custom messages.
    </li>
    <li><b>'Use first spawn message'</b> -
        Whether to use the first spawn message for players.
    </li>
    <li><b>'First spawn message text'</b> - 
        Message to send players when they first spawn in the server. Uses tell.
    </li>
    <li><b>'Use First Spawn Reputation and Infraction Message'</b> - 
        Whether to inform players of their current server reputation and infraction count after the first spawn message is shown.
    </li>
    <li><b>'Use Perk Expiration Notification'</b> - 
        Whether to tell players that they have perks which are expiring soon.
        This message is sent when they first spawn into the server.
    </li>
    <li><b>'Perk Expiration Notify Days Remaining'</b> - 
        The number of days before their perks expire that the notification messages start showing.
    </li>
</ul>
<h3>A12-2. SpamBot Settings:</h3>
<p>
    To bypass any whitelist (aside from the admin whitelist), simply add [whitelistbypass] to the start of any spambot message to make it always display that message.
    You can also add any map or mode combination to the start of a spambot message to make it specific to that map/mode combination. 
    The mode or map needs to be the exact name as displayed in the procon maplist tab.
    Examples are below:
    <ul>
        <li>
            Operation Metro 2014/Conquest Large/This is my first message which shows on conquest metro only.
        </li>
        <li>
            Operation Locker/Rush/[whitelistbypass]This is my second message which shows on locker rush only, and will bypass whitelist.
        </li>
        <li>
            CTF/This is my third message that shows on capture the flag, regardless of map.
        </li>
        <li>
            Zavod 311/[whitelistbypass]This is my fourth message which shows on zavod, regardless of game mode, and will bypass whitelist.
        </li>
        <li>
            This is my fifth message which shows on every map and mode.
        </li>
    </ul>
</p>
<p>
    Setting List
</p>
<ul>
    <li><b>'SpamBot Enable'</b> - 
        Whether to enable the SpamBot.
    </li>
    <li><b>'SpamBot Say List'</b> - 
        List of messages to send to the server as SAY.
    </li>
    <li><b>'SpamBot Say Delay Seconds'</b> - 
        The number of seconds between each SAY message is sent.
    </li>
    <li><b>'SpamBot Yell List'</b> - 
        List of messages to send to the server as YELL.
    </li>
    <li><b>'SpamBot Yell Delay Seconds'</b> - 
        The number of seconds between each YELL message is sent.
    </li>
    <li><b>'SpamBot Tell List'</b> - 
        List of messages to send to the server as TELL.
    </li>
    <li><b>'SpamBot Tell Delay Seconds'</b> - 
        The number of seconds between each TELL message is sent.
    </li>
    <li><b>'Exclude Admins and Whitelist from Spam'</b> - 
        Whether to exclude admins and whitelisted players from seeing any messages sent from the SpamBot.
    </li>
</ul>
<h3>A12-3. Battlecry Settings - Thanks WDF:</h3>
<ul>
    <li><b>'Player Battlecry Volume'</b> - 
        How 'loud' the player's battlecry will be. Options are Disabled, Say, Yell, and Tell.
    </li>
    <li><b>'Player Battlecry Max Length'</b> - 
        The maximum number of characters in a battlecry message. Cannot be less than 20 or more than 300.
    </li>
    <li><b>'Player Battlecry Denied Words'</b> - 
        List of case-insensitive words that are not allowed in battlecry messages. The !setbattlecry command available to admins overrides this list.
    </li>
</ul>
<h3>A12-4. All-Caps Chat Monitor:</h3>
<ul>
    <li><b>'Use All Caps Limiter'</b> - 
        Toggles the all caps limiter on or off.
    </li>
    <li><b>'All Caps Limiter Only Limit Specified Players'</b> - 
        When enabled this will make the all caps monitor not look at the entire server, instead only looking at a few choice players assigned the all caps blacklist special player group.
    </li>
    <li><b>'All Caps Limiter Character Percentage'</b> - 
        The percentage of characters in a given message that must be all caps before the message is considered all caps. 
        This ignores numbers and special characters, so #%28THIS99/$ would be considered 100% all caps.
    </li>
    <li><b>'All Caps Limiter Minimum Characters'</b> - 
        Minimum length a message needs to be before the all-caps system will examine it.
    </li>
    <li><b>'All Caps Limiter Warn Threshold'</b> - 
        The number of all caps messages before the player is warned in chat about the limitation.
        This value must be less than the kill threshold.
    </li>
    <li><b>'All Caps Limiter Kill Threshold'</b> - 
        The number of all caps messages before the player is killed.
    </li>
    <li><b>'All Caps Limiter Kick Threshold'</b> - 
        The number of all caps messages before the player is kicked.
        This value must be greater than the kill threshold.
    </li>
</ul>
<h3>A13. Banning Settings:</h3>
<ul>
    <li><b>'Use Additional Ban Message'</b> - 
        Whether to have an additional message append on each ban.
    </li>
    <li><b>'Additional Ban Message'</b> - 
        Additional ban message to append on each ban. e.g. "Dispute at www.yourclansite.com"
    </li>
    <li><b>'Procon Ban Admin Name'</b> - 
        Admin name that will be used for bans filed via procon.
    </li>
</ul>
<h3>A13-2. Ban Enforcer Settings:</h3>
<ul>
    <li><b>'Use Ban Enforcer'</b> - 
        Whether to use the AdKats Ban Enforcer.
    </li>
    <li><b>'Ban Enforcer BF4 Lenient Kick'</b> - 
        On BF4 a lot of players use DICE's new in-game server browser. The bad part of this is that nobody can see kick or ban reasons, they aren't displayed on the game UI. This setting allows the ban enforcer to spam a player with say and yell messages before they are ejected from the server so they know why they were banned. This comes with the side effect that people joining back after being banned may have a few seconds to use the chat window before they are ejected again. After 2 re-join attempts the lenient kick is automatically removed though, so people who take advantage of this should be handled regardless. Despite the handling, this setting is opt-in instead of opt-out.
    </li>
    <li><b>'Enforce New Bans by NAME'</b> - 
        Whether to use a player's name to ban them. (Insecure, players can change their names)
    </li>
    <li><b>'Enforce New Bans by GUID'</b> - 
        Whether to use a player's EA GUID to ban them. (Secure, players cannot change their GUIDs)
    </li>
    <li><b>'Enforce New Bans by IP'</b> - 
        Whether to use a player's IP Address to ban them. (Somewhat secure, experienced players can change their IP, and IP bans can hit multiple players.)
    </li>
    <li><b>'Use Metabans?'</b> - 
        Whether to use metabans functionality when banning/unbanning in ban enforcer.
    </li>
    <li><b>'Metabans Username'</b> - 
        Username for authentication to your metabans account.
    </li>
    <li><b>'Metabans API Key'</b> - 
        API Key for your metabans account.
    </li>
    <li><b>'Metabans Filter Strings'</b> - 
        List of case-insensitive words/strings that must be in a ban message for it to be posted to metabans.
    </li>
</ul>
<h3>A13-3. Mini Ban Management:</h3>
<ul>
    <li><b>'NAME Ban Count'</b> - 
        How many NAME bans are currently being enforced by AdKats Ban Enforcer.
    </li>
    <li><b>'GUID Ban Count'</b> - 
        How many EA GUID bans are currently being enforced by AdKats Ban Enforcer.
    </li>
    <li><b>'IP Ban Count'</b> - 
        How many IP bans are currently being enforced by AdKats Ban Enforcer.
    </li>
    <li><b>'Ban Search'</b> - 
        Enter a full or partial player name here and AdKats will display all ACTIVE matching bans.
    </li>
</ul>
<h3>A14. External Command Settings:</h3>
<ul>
    <li><b>'AdKatsLRT Extension Token'</b> - 
        Usable with AdKatsLRT - On Spawn Loadout Enforcer plugin. Once that plugin is purchased, the token can be placed here for automatic install/updates.
    </li>
</ul>
<h3>A15. VOIP Settings:</h3>
<ul>
    <li><b>'Server VOIP Address'</b> - 
        String that will be sent to players using the VOIP command.
    </li>
</ul>
<h3>A16. Orchestration Settings:</h3>
<ul>
    <li><b>'Feed MULTIBalancer Whitelist'</b> - 
        When enabled, the multibalancer whitelist special player group is used to feed MULTIBalancer's player whitelist.
    </li>
    <li><b>'Automatic MULTIBalancer Whitelist for Admins'</b> - 
        When enabled, all admins in your User List will be given whitelist from balance in MULTIBalancer.
    </li>
    <li><b>'Feed MULTIBalancer Even Dispersion List'</b> - 
        When enabled, the dispersion blacklist special player group is used to feed MULTIBalancer's even dispersion list.
    </li>
    <li><b>'Feed TeamKillTracker Whitelist'</b> - 
        When enabled, the TeamKillTracker whitelist special player group is used to feed TeamKillTracker's player whitelist.
    </li>
    <li><b>'Automatic TeamKillTracker Whitelist for Admins'</b> - 
        When enabled, all admins in your User List will be given an automatic TeamKillTracker whitelist.
    </li>
    <li><b>'Feed Server Reserved Slots'</b> - 
        When enabled, players in the reserved slot special player group will be given a reserved slot. 
        Any modifications of the reserved slot list outside of Adkats will be erased.
    </li>
    <li><b>'Automatic Reserved Slot for Admins'</b> - 
        When enabled, all admins in your User List will be given a reserved slot.
    </li>
    <li><b>'Automatic VIP Kick Whitelist for Admins'</b> - 
        When enabled, all admins in your User List will be given a VIP kick whitelist.
    </li>
    <li><b>'Feed Server Spectator List'</b> - 
        When enabled, the server's spectator list will include all players given the spectator whitelist special player group.
    </li>
    <li><b>'Automatic Spectator Slot for Admins'</b> - 
        When enabled, all admins in your User List will be given a spectator slot.
    </li>
    <li><b>'Feed Stat Logger Settings'</b> - 
        When enabled, stat logger is fed settings appropriate for AdKats, including correct database time offset, instant chat logging, etc.
        <p>
            The following settings are sent to stat logger when using the "Feed Stat Logger Settings" orchestration option:
            <ul>
                <li>"Servertime Offset" (TIME OFFSET CONVERSION TO UTC TIME)</li>
                <li>"Enable Chatlogging?" "Yes"</li>
                <li>"Instant Logging of Chat Messages?" "Yes"</li>
                <li>"Enable Statslogging?" "Yes"</li>
                <li>"Enable Weaponstats?" "Yes"</li>
                <li>"Enable KDR correction?" "Yes"</li>
                <li>"MapStats ON?" "Yes"</li>
                <li>"Session ON?" "Yes"</li>
                <li>"Save Sessiondata to DB?" "Yes"</li>
                <li>"Log playerdata only (no playerstats)?" "No"</li>
            </ul>
            "Enable Live Scoreboard in DB" is forced on at all times.
        </p>
    </li>
    <li><b>'Post Stat Logger Chat Manually'</b> - 
        Sometimes stat logger chat upload glitches and stops, this overrides that posting and uploads all chat to the database through the AdKats script instead.
    </li>
    <li><b>'Post Server Chat Spam'</b> - 
        Whether to include server spam messages when posting stat logger chat manually.
    </li>
    <li><b>'Exclude Commands from Chat Logs'</b> - 
        Whether to exclude messages containing commands from being stored in the database.
    </li>
    <li><b>'Banned Tags'</b> - 
        List of clan tags which will cause players to be banned from the server.
    </li>
</ul>
<h3>A17. Round Settings:</h3>
<ul>
    <li><b>'Round Timer: Enable'</b> - 
        When enabled, rounds will be limited to X minutes.
    </li>
    <li><b>'Round Timer: Round Duration Minutes'</b> - 
        Number of minutes that the round will last before the current winning team wins (Will only work correctly in conquest/domination at the moment).
    </li>
</ul>
<h3>A18. AntiCheat Settings:</h3>
<ul>
    <li><b>'Use LIVE Anti Cheat System'</b> - 
        The LIVE AntiCheat system looks at active statistics and between round statistics to bolster the abilities of the AntiCheat script. The only downside to this is that it issues more requests to battlelog.
    </li>
    <li><b>'LIVE System Includes Mass Murder and Aimbot Checks'</b> - 
        When this is enabled then the LIVe system will also act on players using active stats mid-round. Players are acted on if they kill a very large amount of people in a short period of time (standard mass-murder), or if they have an extremely high headshot/kill ratio with non-sniper weapons. The stats used here are leaps and bounds above normal, so they should only trigger for the most blatant hacks, however this is still disabled by default.
    </li>
    <li><b>'AntiCheat: DPS Checker: Ban Message'</b> - 
        Message prefix to use when banning for damage mod.
    </li>
    <li><b>'AntiCheat: HSK Checker: Enable'</b> - 
        Whether the Aimbot portion of the AntiCheat system is enabled.
    </li>
    <li><b>'AntiCheat: HSK Checker: Trigger Level'</b> -
        The headshot/kill ratio for automatic weapons that will trigger a ban. 100 kills minimum to trigger.
        After 3 months of testing, we suggest setting between 50 and 70 depending on the severity you want to enforce.
        You will get some false positives down near 50 but will catch many more aimbotters, setting near 70 will not result in false positives but also won't catch as many bots.
    </li>
    <li><b>'AntiCheat: HSK Checker: Ban Message'</b> - 
        Message prefix to use when banning for high HSK.
    </li>
    <li><b>'AntiCheat: KPM Checker: Enable'</b> - 
        Whether the KPM portion of the AntiCheat system is enabled.
    </li>
    <li><b>'AntiCheat: KPM Checker: Trigger Level'</b> - 
        Kills-per-minute with any included weapon that will trigger the ban.
    </li>
    <li><b>'AntiCheat: KPM Checker: Ban Message'</b> - 
        Message prefix to use when banning for high KPM.
    </li>
</ul>
<h3>A19. Server Rules Settings:</h3>
<p>
    You can add any map or mode combination to the start of a rule to make it specific to that map/mode combination. 
    The mode or map needs to be the exact name as displayed in the procon maplist tab.
    Examples are below:
    <ul>
        <li>
            Operation Metro 2014/Conquest Large/This is my first rule which shows on conquest metro only.
        </li>
        <li>
            Operation Locker/Rush/[whitelistbypass]This is my second rule which shows on locker rush only, and will bypass whitelist.
        </li>
        <li>
            CTF/This is my third rule that shows on capture the flag, regardless of map.
        </li>
        <li>
            Zavod 311/[whitelistbypass]This is my fourth rule which shows on zavod, regardless of game mode, and will bypass whitelist.
        </li>
        <li>
            This is my fifth rule which shows on every map and mode.
        </li>
    </ul>
</p>
<p>
    Setting List
</p>
<ul>
    <li><b>'Rule Print Delay'</b> - 
        Delay in seconds after rules are requested that they start being sent to the player.
    </li>
    <li><b>'Rule Print Interval'</b> - 
        Delay in seconds between each rule being sent to the player.
    </li>
    <li><b>'Server Rule List'</b> - 
        List of rules for the server. Raw text can be used here, or alternatively pre-message IDs.
    </li>
    <li><b>'Server Rule Numbers'</b> - 
        Whether to include rule numbers at the beginning of each line during rule printing.
    </li>
    <li><b>'Yell Server Rules'</b> - 
        Whether to send rules in both yell and say to players.
    </li>
</ul>
<h3>B20. AFK Settings:</h3>
<ul>
    <li><b>'AFK System Enable'</b> - 
        Whether to enable the AFK management system.
    </li>
    <li><b>'AFK Ignore Chat'</b> - 
        Events are used to cancel AFK timeout for players. 
        When this is enabled, players just sitting in the spawn screen chatting will be kicked. 
        They must play in order to stay in the server.
    </li>
    <li><b>'AFK Auto-Kick Enable'</b> - 
        Whether to automatically kick or not. When disabled the afk command must be used for kicking afk players.
    </li>
    <li><b>'AFK Trigger Minutes'</b> - 
        The number of minutes a player can do nothing before being considered AFK.
    </li>
    <li><b>'AFK Minimum Players'</b> - 
        Minimum number of players that must be in the server before the system will kick AFK players.
    </li>
    <li><b>'AFK Ignore User List'</b> - 
        Whether to ignore all users on the user list.
    </li>
    <li><b>'AFK Ignore Roles'</b> - 
        Visible when not ignoring all users on the user list. List the role keys that will be ignored.
    </li>
</ul>
<h3>B21. Ping Enforcer Settings:</h3>
<ul>
    <li><b>'Ping Enforcer Enable'</b> - 
        Whether to enable the Ping Enforcer.
    </li>
    <li><b>'Current Ping Limit (Display)'</b> - 
        The current ping limit based on all the settings below, showing the formula for how it was calculated.
    </li>
    <li><b>'Ping Moving Average Duration sec'</b> - 
        The amount of time that should be used to average the player pings.
        Default is a 3 minute window.
    </li>
    <li><b>'Ping Kick Low Population Trigger ms'</b> - 
        The minimum ping that will trigger a kick in low population.
    </li>
    <li><b>'Ping Kick Low Population Time Modifier'</b> - 
        24 lines, one for each hour of the day. 
        Positive numbers will add to the current ping limit at this population level, negative numbers will take away from it.
    </li>
    <li><b>'Ping Kick Medium Population Trigger ms'</b> - 
        The minimum ping that will trigger a kick in medium population.
    </li>
    <li><b>'Ping Kick Medium Population Time Modifier'</b> - 
        24 lines, one for each hour of the day. 
        Positive numbers will add to the current ping limit at this population level, negative numbers will take away from it.
    </li>
    <li><b>'Ping Kick High Population Trigger ms'</b> - 
        The minimum ping that will trigger a kick in high population.
    </li>
    <li><b>'Ping Kick High Population Time Modifier'</b> - 
        24 lines, one for each hour of the day. 
        Positive numbers will add to the current ping limit at this population level, negative numbers will take away from it.
    </li>
    <li><b>'Ping Kick Full Population Trigger ms'</b> - 
        The minimum ping that will trigger a kick in full population.
    </li>
    <li><b>'Ping Kick Full Population Time Modifier'</b> - 
        24 lines, one for each hour of the day. 
        Positive numbers will add to the current ping limit at this population level, negative numbers will take away from it.
    </li>
    <li><b>'Ping Kick Minimum Players'</b> - 
        The minimum number of players that must be in the server before ping kicks will happen.
    </li>
    <li><b>'Kick Missing Pings'</b> - 
        Whether to kick players for having missing ping.
    </li>
    <li><b>'Attempt Manual Ping when Missing'</b> - 
        If the server does not provide the player a ping, attempt to fetch their ping manually using the Procon instance.
    </li>
    <li><b>'Ping Kick Ignore User List'</b> - 
        Whether to ignore all users on the user list.
    </li>
    <li><b>'Ping Kick Ignore Roles'</b> - 
        List the role keys that will be ignored. e.g. full_admin, guest_default.
    </li>
    <li><b>'Ping Kick Message Prefix'</b> - 
        Custom message to be displayed in ping kicks.
    </li>
    <li><b>'Display Ping Enforcer Messages In Procon Chat'</b> - 
        Used to enable/disable the display of ping warning messages in the procon chat tab. 
        Kicks for ping are still displayed when this is disabled, but messages for ping spikes and ping being over the limit are not displayed.
    </li>
</ul>
<h3>B22. Commander Manager Settings:</h3>
<ul>
    <li><b>'Commander Manager Enable'</b> - 
        Whether to enable the Commander Manager.
    </li>
    <li><b>'Minimum Players to Allow Commanders'</b> - 
        Commanders will be automatically kicked when attempting to join with active player count less than this value. 
        Existing commanders will be kicked if player count drops below 75% of this value.
    </li>
</ul>
<h3>B23. Player Locking Settings:</h3>
<ul>
    <li><b>'Player Lock Manual Duration Minutes'</b> - 
        When locking players via command, they will be locked for the given duration in minutes.
    </li>
    <li><b>'Automatically Lock Players on Admin Action'</b> - 
        When a player is acted on by an admin, they will be automatically locked from action by other admins.
    </li>
    <li><b>'Player Lock Automatic Duration Minutes'</b> - 
        When automatically locking players, they will be locked for the given duration in minutes.
    </li>
</ul>
<h3>B24. Surrender Vote Settings:</h3>
<ul>
    <li><b>'Surrender Vote Enable'</b> - 
        Whether to enable the Surrender Vote System.
    </li>
    <li><b>'Percentage Votes Needed for Surrender'</b> - 
        Percentage of a team required for surrender vote to complete.
    </li>
    <li><b>'Minimum Player Count to Enable Surrender'</b> - 
        The minimum number of players that must be in the server for a surrender vote to be allowed.
    </li>
    <li><b>'Minimum Ticket Gap to Surrender'</b> - 
        The minimum difference in tickets between teams for a surrender vote to be allowed.
    </li>
    <li><b>'Enable Required Ticket Rate Gap to Surrender'</b> - 
        Whether to require a minimum ticket loss/gain rate between teams before a surrender vote is allowed.
        Use 'Display Ticket Rates in Procon Chat' in section A12 to monitor ticket loss/gain rates.
    </li>
    <li><b>'Minimum Ticket Rate Gap to Surrender'</b> - 
        The minimum difference in ticket rates between teams for a surrender vote to be allowed.
    </li>
    <li><b>'Surrender Vote Timeout Enable'</b> - 
        Whether to enable a timeout on the surrender vote. 
        After this timeout all votes will be removed, and the surrender vote will be stopped.
    </li>
    <li><b>'Surrender Vote Timeout Minutes'</b> - 
        The number of minutes after surrender vote start that it will time out and remove all votes.
    </li>
</ul>
<h3>B25. Auto-Surrender Settings:</h3>
<ul>
    <li><b>'Auto-Surrender Enable'</b> - 
        Whether to enable the Auto-Surrender System. When enabled, all below values must be contained in a round for it to trigger an automatic round surrender.
    </li>
    <li><b>'Auto-Surrender Use Optimal Values for Metro Conquest'</b> - 
        If you are running Metro 2014 on Conquest, use this setting, it will issue auto-surrender when a baserape happens and the weak team cannot recover.
    </li>
    <li><b>'Auto-Surrender Use Optimal Values for Locker Conquest'</b> - 
        If you are running Operation Locker on Conquest, use this setting, it will issue auto-surrender when a baserape happens and the weak team cannot recover.
    </li>
    <li><b>'Auto-Surrender Minimum Ticket Count'</b> - 
        If either team is below this ticket count, auto-surrender will not fire.
    </li>
    <li><b>'Auto-Surrender Maximum Ticket Count'</b> - 
        If either team is above this ticket count, auto-surrender will not fire.
    </li>
    <li><b>'Auto-Surrender Minimum Ticket Gap'</b> - 
        The minimum difference in ticket counts between teams for auto-surrender to fire.
    </li>
    <li><b>'Auto-Surrender Losing Team Rate Window Max'</b> - 
        The losing team's ticket rate must not be greater than this value for auto-surrender to fire.
    </li>
    <li><b>'Auto-Surrender Losing Team Rate Window Min'</b> - 
        The losing team's ticket rate must not be less than this value for auto-surrender to fire.
    </li>
    <li><b>'Auto-Surrender Winning Team Rate Window Max'</b> - 
        The winning team's ticket rate must not be greater than this value for auto-surrender to fire.
    </li>
    <li><b>'Auto-Surrender Winning Team Rate Window Min'</b> - 
        The winning team's ticket rate must not be less than this value for auto-surrender to fire.
    </li>
    <li><b>'Auto-Surrender Trigger Count to Surrender'</b> - 
        Triggers happen every 10 seconds. 
        The above values must be hit this number of times for auto-surrender to fire. Admins and players are optionally informed of triggers.
    </li>
    <li><b>'Auto-Surrender Message'</b> - 
        The message that will be sent to the server when an auto-surrender is fired.
        Place %WinnerName% in the string for the name of the winning team.
    </li>
    <li><b>'Nuke Winning Team Instead of Surrendering Losing Team'</b> - 
        When an auto-surrender would have been triggered on the losing team due to the settings above, instead, nuke the winning team.
    </li>
    <li><b>'Start Surrender Vote Instead of Surrendering Losing Team'</b> - 
        When an auto-surrender would have been triggered on the losing team due to the settings above, instead, simply start a surrender vote, with AutoAdmin giving 1 vote toward surrender.
    </li>
</ul>
<h3>B25-2. Auto Settings:</h3>
<ul>
    <li><b>'Maximum Auto-Nukes Each Round'</b> - 
        The maximum number of nukes that the auto-nuke system can fire before stopping.
    </li>
    <li><b>'Reset Auto-Nuke Trigger Count on Fire'</b> - 
        When this is enabled, regardless of how many triggers have been fired toward the nuke the number is reset when the actual nuke is fired. 
        Use this if you have many flags in your server which need to stabilize before getting good numbers for ticket loss again.
    </li>
    <li><b>'Switch to surrender after max nukes'</b> - 
        Instead of completely stopping the script when the max number of nukes is reached, instead revert to using the auto-surrender script.
    </li>
    <li><b>'Minimum Seconds Between Nukes'</b> - 
        The minimum number of seconds after a nuke is fired before another is able to fire.
    </li>
    <li><b>'Countdown Duration before a Nuke is fired'</b> - 
        When an admin or the auto-nuke system issues a nuke on the server, this is the number of seconds warning that the server is given before the nuke is actually fired.
    </li>
    <li><b>'Fire Nuke Triggers if Winning Team up by X Tickets'</b> - 
        An override to fire a nuke if there is too large of a ticket gap between the two teams. 
    </li>
    <li><b>'Announce Nuke Preparation to Players'</b> - 
        Nuke preparation percentages are calculated in the background.
        Use this setting to display the percentage progress toward firing the next nuke to all players in the server.
    </li>
    <li><b>'Allow Auto-Nuke to fire on losing teams'</b> - 
        Normally the script completely blocks issuing of nukes on the losing team, this is by design. 
        For the ambitious few however this setting is available to allow nukes on the losing team if they take over the map regardless.
    </li>
    <li><b>'Maximum Nuke Ticket Difference for Losing Team'</b> - 
        If we are not nuking losing teams, this setting is how many tickets a team must be losing by before a nuke is issued on them.
    </li>
    <li><b>'Auto-Nuke High Pop Duration Seconds'</b> - 
        The duration of a nuke when the server is in high population.
        During this time the target team is held dead as the server counts down the number of seconds you define here.
    </li>
    <li><b>'Auto-Nuke Medium Pop Duration Seconds'</b> - 
        Same as above but for when the server is in medium population.
    </li>
    <li><b>'Auto-Nuke Low Pop Duration Seconds'</b> - 
        Same as above but for when the server is in low population.
    </li>
    <li><b>'Auto-Nuke Consecutive Duration Increase'</b> - 
        If a team is clearly overpowered and keeps getting nuked, this setting will increase the number of seconds the nuke is fired for, with no upper limit.
        For example, if you have this setting at 10 seconds, and a high population nuke is fired for 15 seconds, but this is the 3rd nuke to be fired against this team, the resulting nuke is 35 seconds long.
    </li>
    <li><b>'Auto-Nuke Duration Increase Minimum Ticket Difference'</b> - 
        The minimum ticket difference between teams before the nuke duration is allowed to increase.
    </li>
    <li><b>'Auto-Nuke Message'</b> - 
        The message that will be sent to the server when an auto-nuke is fired.
        Place %WinnerName% in the message for the name of the winning team being nuked.
    </li>
</ul>
<h3>B26. Statistics Settings:</h3>
<ul>
    <li><b>'Post Map Benefit/Detriment Statistics'</b> - 
        Whether to post statistics on which maps are most beneficial/detrimental to the population of the server. Queries to extract meaning from this information can be acquired in the main AdKats forum thread.
    </li>
</ul>
<h3>B27. Populator Monitor Settings - Thanks CMWGaming:</h3>
<ul>
    <li><b>'Monitor Populator Players - Thanks CMWGaming'</b> - 
        When enabled, players who help populate servers can be automatically monitored and given perks.
    </li>
    <li><b>'Populator Players (Display)'</b> - 
        Current display of populator players using the below options.
    </li>
    <li><b>'Monitor Specified Populators Only'</b> - 
        When enabled, players must be placed under populator whitelist in order to be considered for populator status on this server.
    </li>
    <li><b>'Monitor Populators of This Server Only'</b> - 
        When enabled, only population counts of this server are used to count toward populator stats on this server.
    </li>
    <li><b>'Count to Consider Populator Past Week'</b> - 
        Players will be considered populator if they have this many populations in the past week.
    </li>
    <li><b>'Count to Consider Populator Past 2 Weeks'</b> - 
        Players will be considered populator if they have this many populations in the past 2 weeks.
    </li>
    <li><b>'Enable Populator Perks.'</b> - 
        When enabled, populator perk options are made visible.
    </li>
    <li><b>'Populator Perks - Reserved Slot.'</b> - 
        When enabled, populators are given reserved slots.
    </li>
    <li><b>'Populator Perks - Autobalance Whitelist.'</b> - 
        When enabled, populators are given MULTIBalancer whitelist.
    </li>
    <li><b>'Populator Perks - Ping Whitelist.'</b> - 
        When enabled, populators are given whitelist from ping kicks.
    </li>
    <li><b>'Populator Perks - TeamKillTracker Whitelist.'</b> - 
        When enabled, populators are given a whitelist in TeamKillTracker.
    </li>
</ul>
<h3>B28. Teamspeak Monitor Settings - Thanks CMWGaming:</h3>
<ul>
    <li><b>'Monitor Teamspeak Players - Thanks CMWGaming'</b> - 
        When enabled, the teamspeak player monitor settings will be displayed.
    </li>
    <li><b>'Teamspeak Players (Display)'</b> - 
        Current display of online players who are matched to teamspeak users.
    </li>
    <li><b>'Enable Teamspeak Player Monitor'</b> - 
        When enabled, the below settings will be used to monitor players in the targeted teamspeak server.
    </li>
    <li><b>'Teamspeak Server IP'</b> - 
        IP address of the teamspeak server.
    </li>
    <li><b>'Teamspeak Server Port'</b> - 
        Public port number of the teamspeak server.
    </li>
    <li><b>'Teamspeak Server Query Port'</b> - 
        Query port number of the teamspeak server.
    </li>
    <li><b>'Teamspeak Server Query Username'</b> - 
        Username to use for teamspeak connection.
    </li>
    <li><b>'Teamspeak Server Query Password'</b> - 
        Password to use for teamspeak connection.
    </li>
    <li><b>'Teamspeak Server Query Nickname'</b> - 
        Nickname to use for the teamspeak connection.
    </li>
    <li><b>'Teamspeak Main Channel Name'</b> - 
        Main channel to grab players from. 
        Must be set on connection start, cannot be modified afterwards.
    </li>
    <li><b>'Teamspeak Secondary Channel Names'</b> - 
        Any additional channels to pull players from.
    </li>
    <li><b>'Debug Display Teamspeak Clients'</b> - 
        Display console debug when relevant events happen.
    </li>
    <li><b>'TeamSpeak Player Join Announcement'</b> - 
        Whether to announce players who join in both teamspeak and the game.
    </li>
    <li><b>'TeamSpeak Player Join Message'</b> - 
        Message to announce joining teamspeak players with.
    </li>
    <li><b>'TeamSpeak Player Update Seconds'</b> - 
        How often the system will query teamspeak for client updates. Minimum 5 seconds.
    </li>
    <li><b>'Enable Teamspeak Player Perks'</b> - 
        Whether to give players in teamspeak any automatic perks.
    </li>
    <li><b>'Teamspeak Player Perks - VIP Kick Whitelist'</b> - 
        When enabled, teamspeak players are given VIP Kick Whitelist.
    </li>
    <li><b>'Teamspeak Player Perks - Autobalance Whitelist'</b> - 
        When enabled, teamspeak players are given MULTIBalancer whitelist.
    </li>
    <li><b>'Teamspeak Player Perks - Ping Whitelist'</b> - 
        When enabled, teamspeak players are given whitelist from ping kicks.
    </li>
    <li><b>'Teamspeak Player Perks - TeamKillTracker Whitelist'</b> - 
        When enabled, teamspeak players are given a whitelist in TeamKillTracker.
    </li>
</ul>
<h3>B29. Discord Player Monitor Settings:</h3>
<ul>
    <li><b>'Monitor Discord Players'</b> - 
        When enabled, the discord player monitor settings will be displayed.
    </li>
    <li><b>'[#] Discord Players (Display)'</b> - 
        Current display of online players who are matched to discord members.
    </li>
    <li><b>'[#] Discord Channel Members (Display)'</b> - 
        The full list of discord memebers in the list of monitored channels.
    </li>
    <li><b>'[#] Discord All Members (Display)'</b> - 
        List of all members in the discord server.
    </li>
    <li><b>'Enable Discord Player Monitor'</b> - 
        When enabled, the below settings will be used to monitor players in the targeted discord server.
    </li>
    <li><b>'Discord Server ID'</b> - 
        The ID of the discord server to monitor. This value can be found in the widget.json URL for the server.
    </li>
    <li><b>'Discord Channel Names'</b> - 
        The list of discord channels to monitor.
    </li>
    <li><b>'Require Voice in Discord to Issue Admin Commands'</b> - 
        When this is enabled, a player must be recognized as being online in the discord server in order to issue admin commands. 
        The current issue with this is the 5 minute update frequency from discord because procon with .net 3.5 doesn't have access to websockets.
    </li>
    <li><b>'Discord Player Join Announcement'</b> - 
        Whether to announce players who join in both discord and the game.
    </li>
    <li><b>'Discord Player Join Message'</b> - 
        Message to announce joining discord players with.
    </li>
    <li><b>'Enable Discord Player Perks'</b> - 
        Whether to give players in discord any automatic perks.
    </li>
    <li><b>'Discord Player Perks - Reserved Slot'</b> - 
        When enabled, discord players are given VIP Kick Whitelist.
    </li>
    <li><b>'Discord Player Perks - Autobalance Whitelist'</b> - 
        When enabled, discord players are given MULTIBalancer whitelist.
    </li>
    <li><b>'Discord Player Perks - Ping Whitelist'</b> - 
        When enabled, discord players are given whitelist from ping kicks.
    </li>
    <li><b>'Discord Player Perks - TeamKillTracker Whitelist'</b> - 
        When enabled, discord players are given a whitelist in TeamKillTracker.
    </li>
    <li><b>'Debug Display Discord Members'</b> - 
        Display console debug when relevant events happen.
    </li>
</ul>
<h3>C30. Team Power Monitor:</h3>
<ul>
    <li><b>'Team Power Active Influence'</b> - 
        How much players performance in the current round should be weighted against their overall statistics.
        I've found the best value for this to be 35.
    </li>
    <li><b>'Team Power (Display)'</b> - 
        The current power levels of each team and the percentage difference between them.
    </li>
    <li><b>'Player Power (Display)'</b> - 
        The current list of active players in the server with their team assignments and power levels. 
        A team key with a + after it means they are locked to that team.
        A team -> team means they are assigned to the other team and waiting to be moved.
        The first number following their name is the most important, the others are for debug.
        Player power ranges from 7-35.
    </li>
    <li><b>'Enable Team Power Scrambler'</b> - 
        Completely experimental system, use with caution.
        This system uses team power metrics to scramble teams based on their power levels, keeping squads together. 
        Players are then locked to their assigned teams for the duration of the round unless they use assist or are moved by an admin.
        I've noticed that this system does not work well for metro since the rounds are so volatile and people quit and join at such a high rate.
        Should be more useful for TDM or conquest vehicles where the player turnover rate is lower during a round.
    </li>
    <li><b>'Enable Team Power Join Reassignment'</b> - 
        Using team power metrics this system will reassign newly joining players to the weak team.
        If a 64 man server is nearly full and the weak team needs a lot of help it will allow up to a 4 player advantage for the weak team.
    </li>
    <li><b>'Team Power Join Reassignment Leniency'</b> - 
        Normally join reassignment only kicks in if a team is weaker, and they have less of the map controlled. This leniency kicks in as an override
        so that if a team is VERY behind in power it will still get players assigned to it even if they have more map control at the moment. The 
        configuration for this is power ratio percentage a team must be behind before this will activate. For example if it's set to 30, the other 
        team must be a full 30% more powerful before this override will kick in.
    </li>
    <li><b>'Team Power Join Reassignment Leniency Percent'</b> - 
        The percentage of power that a team must be behind in order to still get players assigned to it when they normally wouldn't be.
    </li>
    <li><b>'Enable Team Power Unswitcher'</b> - 
        Using team power metrics this system blocks players from moving themselves to the more powerful team.
        Still allows players to move themselves to the weaker team.
    </li>
    <li><b>'Enable Team Power Seeder Control'</b> - 
        A basic seeder balancer using team power metrics and placements on the map. 
        Seeders are evenly balanced between teams, and when there is an odd number of seeders the extra seeder is placed on the more powerful team.
    </li>
</ul>
<h3>C31. Weapon Limiter Settings:</h3>
<ul>
    <li><b>'Use NO EXPLOSIVES Limiter'</b> - 
        Whether to enable the internal no explosives limiter.
    </li>
    <li><b>'NO EXPLOSIVES Weapon String'</b> - 
        List of weapon codes or partial weapon codes that will cause punishment for a no explosive server.
    </li>
    <li><b>'NO EXPLOSIVES Exception String'</b> - 
        Any weapon code or partial weapon code previously matched by 'no explosive weapon string' but also matching this setting, will block action on that item.
        Useful for weapons like the ones ending in SHG, which in BF4 is the dart variant of the M26/M320.
    </li>
    <li><b>'Use Grenade Cook Catcher'</b> - 
        When the no explosives limiter is enabled, having this setting also enabled will cause AdKats to monitor for grenade cookers.
        In battlefield when you kill someone who is holding or throwing a grenade, the grenade becomes yours.
        Some players will take advantage of this to get other players acted on by autoadmins on no explosive servers.
        This script works to mitigate that problem.
    </li>
</ul>
<h3>C32. Challenge Settings</h3>
<ul>
    <li><b>'Use Challenge System'</b> - 
        Whether the challenge system is active. On disable any active server-wide round rules are cancelled.
    </li>
    <li><b>'Challenge System Minimum Players'</b> - 
        The minimum number of players which must be active in the server before challenges will accept any progress. Players can start challenges in low population but they will not gain any progress until the server has more players. Challenges which are death-based will not have deaths counted until this limit is reached either.
    </li>
    <li><b>'Use Server-Wide Round Rules'</b> - 
        Whether the system will choose a server-wide round rule that all players can join. Valid rules that can be chosen by this system must be enabled, round-based, be tier 1, and have a round count of 1. The current chosen rule will be assigned to any player who is currently not playing a challenge and queries the !ch info command. Round rules are chosen randomly at first until each is played once, then the least played rule is chosen.
    </li>
    <li><b>'Challenge System Auto-Assign Round Rules'</b> - 
        Whether the system will automatically assign the current round rule to a player when they get a kill that matches the rule.
    </li>
    <li><b>'Use Different Round Rule For Each Players'</b> - 
        Only visible if you have both above settings enabled. Will automatically assign a random round rule to each player instead of the whole server. Prevents issues where some people could get annoyed by the whole server being told, for example, to use shotguns.
    </li>
    <li><b>'[1] Displays - Active Entries (Display)'</b> - 
        The current challenge entries which are not completed, cancelled, or failed. They are sorted by rule, then by completion percentage. If there is a server-wide round rule, those entries are excluded from this list.
    </li>
    <li><b>'[1] Displays - Current Server-Wide Round Rule (Display)'</b> - 
        The name of the current server-wide round rule, if any.
    </li>
    <li><b>'[1] Displays - Active Round Rule Entries (Display)'</b> - 
        Same as the active entries display, except only for the server-wide round rule.
    </li>
    <li><b>'[2] Actions - Run Round Challenge ID'</b> - 
        Choose a server-wide round rule to run.
    </li>
    <li><b>'[3] Definitions - Add Definition?'</b> - 
        Enter a name for the new definition here to add it. Definitions are visible on all servers and form the basis of WHAT a player must do to complete a challenge.
    </li>
    <li><b>'[3] Definitions - Change Name?'</b> - 
        Change the name of a definition. Note this name is only internal, not shown to players. The rule name is shown to players.
    </li>
    <li><b>'[3] Definitions - Add Damage Type?'</b> - 
        Adds a damage type to the current definition.
    </li>
    <li><b>'[3] Definitions - Add Weapon?'</b> - 
        Adds a specific weapon to the current definition.
    </li>
    <li><b>'[3] Definitions - Delete Definition?'</b> - 
        Enter 'delete' in this setting to delete the definition. All rules and player completions of this definition will also be deleted if you delete this definition.
    </li>
    <li><b>'[3] Definitions - Weapon - Weapon Name'</b> - 
        Change the weapon for a definition detail with this setting. A definition can only have one of each particular weapon at a time.
    </li>
    <li><b>'[3] Definitions - Weapon - Kill Count'</b> - 
        The kill count required with this weapon to complete its part of the challenge.
    </li>
    <li><b>'[3] Definitions - Weapon - Delete Detail?'</b> - 
        Enter 'delete' in this setting to delete the weapon detail.
    </li>
    <li><b>'[3] Definitions - Damage - Damage Type'</b> - 
        Change the damage type for a definition detail with this setting. A definition can only have one of each damage type at a time.
    </li>
    <li><b>'[3] Definitions - Damage - Weapon Count'</b> - 
        A damage type requires a weapon count, specifying how many different weapons must be used to complete it.
    </li>
    <li><b>'[3] Definitions - Damage - Kill Count'</b> - 
        The kill count for each weapon in the damage type to complete the challenge. So if you have 5 weapons, and 5 kills, that means 25 total kills would be required to complete the challenge.
    </li>
    <li><b>'[3] Definitions - Damage - Delete Detail?'</b> - 
        Enter 'delete' in this setting to delete the damage type detail.
    </li>
    <li><b>'[4] Rules - Add Rule?'</b> - 
        Select a definition to create a server-specific rule based on it. Rules are how fast a player must complete a specific challenge, and are server specific.
    </li>
    <li><b>'[4] Rules - Definition'</b> - 
        Change the definition that this rule belongs to with this setting.
    </li>
    <li><b>'[4] Rules - Name'</b> - 
        The name of the rule. This is the name that is shown to players.
    </li>
    <li><b>'[4] Rules - Enabled'</b> - 
        Whether the rule is enabled. Disabled by default.
    </li>
    <li><b>'[4] Rules - Tier'</b> - 
        The tier of the rule. In general lower tier rules should be easier than higher tier rules.
    </li>
    <li><b>'[4] Rules - Completion'</b> - 
        What metric should be used to determine when the challenge ends. Either round count, minute count, or death count.
    </li>
    <li><b>'[4] Rules - Round Count'</b> - 
        The number of rounds that the challenge will run after starting.
    </li>
    <li><b>'[4] Rules - Duration Minutes'</b> - 
        The number of minutes that the challenge will run after starting.
    </li>
    <li><b>'[4] Rules - Death Count'</b> - 
        The number of deaths that the player is allowed after starting.
    </li>
    <li><b>'[4] Rules - Delete Rule?'</b> - 
        Enter 'delete' in this setting to delete the rule.
    </li>
    <li><b>'[5] Rewards - Challenge Command Lock Timeout Hours'</b> - 
        After a player recieves the command lock timeout reward, they cannot get that reward again until this many hours has passed. Any challenges containing the command lock reward can still be completed, and any other rewards on that challenge will still be applied.
    </li>
    <li><b>'[5] Rewards - Add Reward?'</b> - 
        Enter a tier to add a reward to that tier. Each tier can have one of each reward type.
    </li>
    <li><b>'[5] Rewards - Tier Level'</b> - 
        The tier level that this reward belongs to. When a reward of this tier is completed, this reward will be applied if it is enabled.
    </li>
    <li><b>'[5] Rewards - Reward Type'</b> - 
        The type of reward that will be given when this tier is completed.
    </li>
    <li><b>'[5] Rewards - Enabled'</b> - 
        Whether this reward is enabled. Defaults to disabled.
    </li>
    <li><b>'[5] Rewards - Duration Minutes'</b> - 
        The number of minutes length that the reward is applied for.
    </li>
    <li><b>'[5] Rewards - Delete Reward?'</b> - 
        Enter 'delete' in this setting to delete the reward.
    </li>
</ul>
<h3>D99. Debug Settings:</h3>
<ul>
    <li><b>'Debug level'</b> -
        Indicates how much debug-output is printed to the plugin-console.
        0 turns off debug messages (just shows important warnings/exceptions/success), 7 documents nearly every step.
        Don't edit unless you really want to be spammed with console logs, it will also slow down the plugin when turned up.
    </li>
    <li><b>'Debug Soldier Name'</b> -
        When this soldier issues commands in your server the duration for any command to complete is told in-game.
        Duration is from the time you entered the message until all aspects of the command have been completed.
    </li>
    <li><b>'Enforce Single Instance'</b> - 
        Enabled by default, and should remain enabled in almost all cases. 
        This makes sure that only one instance of AdKats is enabled at one time on a single server.
        This may be disabled if you notice issues with /AdKatsInstanceCheck messages being logged in your external tools like RCONNET.
    </li>
    <li><b>'Disable Automatic Updates'</b> - 
        Disables automatic updates for the plugin. Should only be disabled if you've modified the plugin code manually.
    </li>
    <li><b>'Command Entry'</b> -
        Enter commands here just like in game, mainly for debug purposes. 
        Don't let more than one person use this at any time.
    </li>
</ul>
