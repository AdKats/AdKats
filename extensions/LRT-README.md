<script>
    //<latest_stable_release>1.0.4.1</latest_stable_release>
</script>
<p>
    <a href="https://forum.myrcon.com/showthread.php?9180" name=thread>
        <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Loadout.jpg" alt="AdKatsLRT - On-Spawn Loadout Enforcer">
    </a>
</p>
<h2>Overview</h2>
<p>
    All 3500 infantry items in the game (primaries, sidearms, attachments for either, gadgets, grenades, etc) can be denied on spawn using this plugin, with customizable messages for each item. Settings allow for only enforcing loadouts on problem players, or for all players, with per-item gradation of that enforcement.
</p>
<h4>Basic</h4>
<ul>
    <li>
        Enforce every infantry item (any primary, secondary, attachments for either, gadgets, knifes, and grenades) in the game on-spawn.
    </li>
    <li>
        Any update made to the game's weapons are automatically imported and made available, so if DICE changes or adds weapons, they are immediately enforceable.
    </li>
    <li>
        Players notified and thanked when they fix their loadouts after being killed.
    </li>
    <li>
        Customizable kill messages for each denied item, with combined messages and details if more than one is spawned in the same loadout.
    </li>
    <li>
        Statistics on enforcement, including percent of players enforced, percent killed for enforcement, percent who fixed their loadouts after kill, and percent who quit the server without fixing their loadouts after kill.
    </li>
</ul>
<h4>With AdKats</h4>
<ul>
    <li>
        Two levels of enforcement, allowing multiple levels of severity for each item.
    </li>
    <li>
        In-game commands to call more strict loadout enforcement on specific players.
    </li>
    <li>
        Using the reputation system, reputable players are optionally not forced to change their loadouts, as we know they are not going to use them.
    </li>
    <li>
        Admins and are optionally whitelisted from spawn enforcement, but still fall under trigger enforcement if marked or punished.
    </li>
    <li>
        Other plugins can call loadout checks and enforcement, so it can enhance your current autoadmin.
    </li>
</ul>
<p>
    Development by Daniel J. Gradinjan (ColColonCleaner)
</p>
<p>
    If you find any bugs, please inform me about them on the MyRCON forums and they will be fixed ASAP. Purchase of this plugin carries no guarantee to continue functioning properly if DICE changes their configuration to disallow this plugin's method of parsing loadouts.
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
<ul>
    <li>
        <b>Procon, and optionally AdKats.</b> Basic functions only require Procon, however, for advanced functions, it requires AdKats 5.3.1.4 or later to be installed and running.
    </li>
    <li>
        <b>Separated Layer IPs.</b> This is a battlelog intensive plugin, do not run it on more than one battlefield server from the same Procon layer IP address. If you have multiple servers to enforce, you must run the plugin from different Procon layer IP addresses. If you do not heed this warning, your layers run the risk of being temporarily IP banned from battlelog.
    </li>
</ul>
<HR>
<p>
    <a name=install />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Install.jpg" alt="AdKats User Manual">
</p>
<ol>
    <li>
        <b>Optionally Install AdKats.</b>
        AdKatsLRT can run independently from the AdKats base plugin, but certain advanced functions are disabled as a result.         To install the latest version of AdKats, view the 
        <a href="https://github.com/AdKats/AdKats#install" target="_blank">AdKats Install Instructions.</a>
        After install make sure you are running version 5.3.1.4 or later. If you are not, download the <a href="http://api.gamerethos.net/adkats/fetch/test?force=1" target="_blank">Latest Test Version</a>.
    </li>
    <li>
        <b>Purchase AdKatsLRT, and aquire an extension token.</b>
        <a href="https://forum.myrcon.com/private.php?do=newpm&u=15312" target="_blank">Private message ColColonCleaner</a> on the MyRCON forums about getting the plugin. Include names of the servers you run, and the names of the servers you will be running the loadout enforcer on. AdKatsLRT is $25, one-time fee, with free updates for the duration of the project. Payments accepted through <a href="https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=danielgradinjan%40gmail%2ecom&lc=US&item_name=AdKatsLRT%20%2d%20On%2dSpawn%20Loadout%20Enforcer%20for%20Procon%20Frostbite%20%2d%20Purchase&amount=25%2e00&currency_code=USD&no_note=0&bn=PP%2dDonationsBF%3abtn_donateCC_LG%2egif%3aNonHostedGuest" target="_blank">PayPal Donation</a> or <a href="https://wallet.google.com" target="_blank">Google Wallet</a> (colcoloncleaner@gmail.com).
    </li>
    <li>
        <b>Install the plugin. (With AdKats Installed)</b>
        Simple. You will be given an extension token on purchase, simply paste the token in AdKats setting "A14. External Command Settings | AdKatsLRT Extension Token". The plugin will be automatically downloaded and installed onto your procon instance. Restart procon to see the plugin.
    </li>
    <li>
        <b>Install the plugin. (WITHOUT AdKats Installed)</b>
        Once purchased, you will be given access to the private repository where AdKatsLRT is hosted. If you want future access for manual updates, supply a Github user name and you will be added that way. If you do not have a Github account, you will be given the source code manually.
    </li>
    <li>
        <b>Enable AdKatsLRT.</b>
        AdKatsLRT will start and fetch required information, then wait for the first player list response from either AdKats or the server. Once that comes through, it will complete startup and loadout enforcement will be online. Enjoy your new admin tool!
    </li>
</ol>
<p>
    If you have any problems installing AdKatsLRT please let me know on the MyRCON forums and I'll respond promptly. All instances of AdKatsLRT are tracked once installed.
</p>
<HR>
<p>
    <a name=features />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Features.jpg" alt="AdKats User Manual">
</p>
<h3>Item Library</h3>
<p>
    Every infantry item in the game (about 3500 items), can be enforced here. The settings are split into 3 sections; Weapons, Gadgets, and Weapon Accessories, in that order.
</p>
<p>
    <img src="http://i.gyazo.com/7991413c903cd88f268a18eb2e95bd38.png" alt="AdKatsLRT Setting List">
</p>
<h3>Loadout Processing</h3>
<h4>Deciding Enforcement Type</h4>
<p>
    <b>The gist of it: </b>Deny on trigger denies that weapon/accessory for problem players and players you've manually marked, deny on spawn denies that weapon for everyone except admins and repuatable players.
</p>
<p>
    <b>Details: </b>Loadouts have several reasons for being checked; A player spawns, gets reported, gets punished, gets marked, or has more than 5 infraction points. Any of these instances will call a loadout check, and the reason for checking them changes the way the enforcement works.
</p>
<p>
    When running loadout enforcement for a specific player, the reason, and action if invalid, is first decided. The following are results for specific reasons, in order of priority:
    <ul>
        <li>If a player was marked, they are set under trigger enforcement, and will be slain for invalid loadout of any kind.</li>
        <li>If a player is punished, they are set under trigger enforcement, and will be slain for invalid loadout of any kind.</li>
        <li>If a player was reported, their reputation is negative, and no admins are online, they are set under trigger enforcement and will be slain for invalid loadout of any kind. If they are slain after report, the report is automatically accepted.</li>
        <li>If a player has more than 5 infraction points, was punished in the last 2 months, their reputation is negative, and no admins are online, they are set under trigger enforcement and will be slain for invalid loadout.</li>
        <li>If a player joins, or spawns, has less than 15 reputation, and is not an admin, they are set under spawn enforcement, and will be slain for spawn denied items in their loadout.</li>
    </ul>
</p>
<h4>Informing and Acting</h4>
<p>
    If a player is about to be slain for loadout enforcement, regardless of enforcement type, they are shown two messages. The first is a generic message containing all denied weapons they have in their loadout "playername please remove [denied weapons] from your loadout". This messages is sent using player SAY. After that, the specific messages written by the admin for each denied item is displayed. These customizable messages are found in setting sections 5A and 5B, once denied items are selected. These messages are sent using player TELL.
</p>
<p>
    Immediately after informing the player of denied items in their loadout, they are admin killed. If they are under trigger enforcement, admins are notified of their demise, all other messages are private.
</p>
<p>
    Thank you messages are given to players who fix their loadouts. If a player is under trigger enforcement, admins are notified that they fixed their loadout.
</p>
<h3>Debug Messages</h3>
<p>
    Setting your debug level to at least 2 will display statistics in the console when stats change. The stats available are percent under loadout enforcement (should be nearly 100%), percent killed for loadout enforcement, percent who fixed their loadouts after kill, and percent who quit the server without fixing after being killed.
</p>
<HR>
<p>
    <a name=commands />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Commands.jpg" alt="AdKats User Manual">
</p>
<p>
    Certain commands in AdKats are modified by this plugin. The changes to those commands are listed below.
</p>
<table>
<tr>
    <td><b>Command</b></td>
    <td><b>Default Text</b></td>
    <td><b>Changes</b></td>
</tr>
<tr>
    <td><b>Punish Player</b></td>
    <td>punish</td>
    <td>
        Punish works as normal, but also initiates trigger level enforcement on the target player for the duration the plugin is online.
    </td>
</tr>
<tr>
    <td><b>Mark Player</b></td>
    <td>mark</td>
    <td>
        Instead of marking a player for leave notification only, it also initiates trigger level enforcement on the target player for the duration the plugin is online.
    </td>
</tr>
<tr>
    <td><b>Report Player</b></td>
    <td>report</td>
    <td>
        Reports initiate trigger level enforcement on targeted players. If the reported player has invalid items in their loadout, the report is automatically accepted, and admins notified of such.
    </td>
</tr>
<tr>
    <td><b>Call Admin</b></td>
    <td>admin</td>
    <td>
        Same changes as Report Player.
    </td>
</tr>
</table>
<HR>
<p>
    <a name=settings />
    <img src="https://raw.githubusercontent.com/ColColonCleaner/AdKats/master/images/AdKats_Docs_Settings.jpg" alt="AdKats User Manual">
</p>
<h3>0. Instance Settings:</h3>
<ul>
    <li><b>'Integrate with AdKats'</b> - Whether to integrate functions with AdKats. When enabled this unlocks more setting options.
    </li>
    <li><b>'Spawn Enforce Admins'</b> - Viewable when integrating with AdKats. Adds admins to spawn enforcement.
    </li>
    <li><b>'Spawn Enforce Reputable Players'</b> - Viewable when integrating with AdKats. Adds reputable players (> 15 rep) to spawn enforcement.
    </li>
    <li><b>'Trigger Enforce Minimum Infraction Points'</b> - Viewable when integrating with AdKats. Sets the minimum infraction point count to automatically add problem players to trigger level enforcement.
    </li>
</ul>
<h3>1. Preset Settings:</h3>
<ul>
    <li><b>'Coming Soon'</b> - This setting block will soon contain settings for presets, like 'No Frag Rounds', or 'No Explosives'.
    </li>
</ul>
<h3>2. Weapons:</h3>
<ul>
    <li><b>'*WeaponIdentifier Allow on trigger?'</b> - Viewable when integrating with AdKats. Whether this item should be allowed/denied when a player is under trigger level enforcement.</li>
    <li><b>'*WeaponIdentifier Allow on spawn?'</b> - Appears when a weapon is denied under trigger enforcement. Whether this item should be allowed/denied when a player is under spawn level enforcement.</li>
</ul>
<h3>3. Gadgets:</h3>
<ul>
    <li><b>'*GadgetIdentifier Allow on trigger?'</b> - Viewable when integrating with AdKats. Whether this item should be allowed/denied when a player is under trigger level enforcement.</li>
    <li><b>'*GadgetIdentifier Allow on spawn?'</b> - Appears when a weapon is denied under trigger enforcement. Whether this item should be allowed/denied when a player is under spawn level enforcement.</li>
</ul>
<h3>4. Weapon Accessories:</h3>
<ul>
    <li><b>'*AccessoryIdentifier Allow on trigger?'</b> - Viewable when integrating with AdKats. Whether this item should be allowed/denied when a player is under trigger level enforcement.</li>
    <li><b>'*AccessoryIdentifier Allow on spawn?'</b> - Appears when a weapon is denied under trigger enforcement. Whether this item should be allowed/denied when a player is under spawn level enforcement.</li>
</ul>
<h3>5A. Denied Item Kill Messages:</h3>
<ul>
    <li><b>'*ItemIdentifier Kill Message'</b> - The specific message sent to players when they are slain for having this item in their loadout.</li>
</ul>
<h3>5B. Denied Item Accessory Kill Messages:</h3>
<ul>
    <li><b>'*AccessoryIdentifier Kill Message'</b> - The specific message sent to players when they are slain for having this accessory in their current loadout.</li>
</ul>
<h3>D99. Debug Settings:</h3>
<ul>
    <li><b>'Debug level'</b> -
        Indicates how much debug-output is printed to the plugin-console.
        0 turns off debug messages (just shows important warnings/exceptions/success), 1 includes kill notifications, 2 includes stats, 3 includes queue information, 4 includes each player's full loadout, and 5 is overly detailed.
    </li>
</ul>
