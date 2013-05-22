<h2>Development</h2>
<p>
Started by ColColonCleaner for ADK Gamers on Apr. 20, 2013
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
</blockquote>
