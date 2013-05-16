<h1>AdKats Proper Usage</h1>
<p>
Proper Usage Documentation for the ADK Admin Tool Set.
</p>
<h2>General Usage</h2>
<h3>Old Commands</h3>
<p>
All basic commands are still available for use. This includes Kill, Kick, TBan, Ban, Move, FMove, Moveme, Say, Yell, PSay, and PYell, These commands operate generally as they did before with other admin systems, but some have been changed slightly.
</p>
<h3>Old Command Changes</h3>
<p>
The player name completion system has been completely reconstructed to work better than before.<br/>
All Yell messages will be automatically changed to all caps, so don't worry about capitalizing them.<br/>
Move has been fixed, it will now swap players when they die instead of failing like the default in-game admin.<br/>
Move, Fmove, and Moveme all use teamswap now, their functions are the same, but internally they use teamswap.<br/>
Now all player interaction commands MUST have a reason, and will reject your command if no reason is given.<br/>
@report and @admin now require players to have a reason. Prophet's gcp is not set up to use this yet though and will still beep at you with incomplete names and reasons. However it will tell you when you're in-game the full player name and full reason for the report or admin call. 
</p>
<h3>Old Command Usage</h3>
<p>
Now you may ONLY use old player interaction commands (Kill, Kick, TBan, or Ban) in two cases. The first being extreme cases such as hackers or glitchers, where the player is directly TBanned or Banned. The second is server management, such as when a player asks to be killed, killing yourself as an admin, or kicking an AFK player for example. No actions should be taken using these commands for player infractions against server rules, no matter how severe the rule break (besides hacking/glitching), or what you know about that player's history.
</p>
<h3>New Commands and Usage</h3>
<p>
Punish Command - currently @punish. This command should be the ONLY command used for player infractions against server rules, besides hacking/glitching. It requires a reason as all other player interaction commands. It will tell you what action was taken against the player once their punishment is calculated. If the action was a TBan or Ban you must put in a ban submission just like before. There is currently a 30 second punishment timeout on players, this is meant to prevent the case where multiple admins see a player commit an infraction and thus punish them multiple times for the same infraction. It will tell you when a punishment timeout is active.
</p>
<p>
Forgive Command - currently @forgive. This command will forgive the targeted player 1 infraction point. You can use this command after a player's first infraction if they say sorry in-game or come to the website to say sorry. It is not required to forgive players, but it is a good gesture at the discretion of the admin.
</p>
<h2>Admin and TeamSwap Lists</h2>
<h3>General Info</h3>
<p>
All admins and teamswap whitelisted players will have logs in the database now with your exact player names. If you have your name changed you must inform an admin with database access to update your record.<br/>
Whenever a player tries to use a command and they don't have access the plugin will re-check the database to see if they are there yet. This means new admins and whitelisted players will auto update into the plugin once they are added to the database, no need for a plugin restart.
</p>
<h2>All Available In-Game Commands</h2>
<p>
<u><b>Usage of all commands, including who used them, their targets, and the reason for using them, is logged in the database.<br/><br/>
<table>
  <tr>
		<td><b>Command</b></td>
		<td><b>Params</b></td>
		<td><b>Access</b></td>
		<td><b>Description</b></td>
	</tr>
	<tr>
		<td><b>kill</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for killing players.</td>
	</tr>
	<tr>
		<td><b>kick</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for kicking players.</td>
	</tr>
	<tr>
		<td><b>tban</b></td>
		<td>[minutes] [player] [reason]</td>
		<td>Admin</td>
		<td>The in-game command used temp-banning players.</td>
	</tr>
	<tr>
		<td><b>ban</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for perma-banning players.</td>
	</tr>
	<tr>
		<td><b>punish</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for punishing players. Will add a Punish record to the database, increasing a player's total points by 1.</td>
	</tr>
	<tr>
		<td><b>forgive</b></td>
		<td>[player][reason]</td>
		<td>Admin</td>
		<td>The in-game command used for forgiving players. Will add a Forgive record to the database, decreasing a player's total points by 1.</td>
	</tr>
	<tr>
		<td><b>move</b></td>
		<td>[player]</td>
		<td>Admin</td>
		<td>The in-game command used for moving players between teams. Will add players to a death move list, when they die they will be sent to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>fmove</b></td>
		<td>[player]</td>
		<td>Admin</td>
		<td>The in-game command used for force-moving players between teams. Will immediately send the given player to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>moveme</b></td>
		<td>None</td>
		<td>Admin and TeamSwap Whitelist</td>
		<td>The in-game command used for moving yourself between teams. Will immediately send the speaker to TeamSwap.</td>
	</tr>
	<tr>
		<td><b>report</b></td>
		<td>[player][reason]</td>
		<td>All Players</td>
		<td>The in-game command used for reporting players. Must have a reason, and will inform a player otherwise when using. Will log a Report tuple in the database(External GCP polls from there for external admin notifications), and notify all in-game admins.</td>
	</tr>
	<tr>
		<td><b>admin</b></td>
		<td>[player][reason]</td>
		<td>All Players</td>
		<td>The in-game command used for calling admin attention to a player. Same deal as report, but used for a different reason.</td>
	</tr>
	<tr>
		<td><b>say</b></td>
		<td>[message]</td>
		<td>Admin</td>
		<td>The in-game command used to send a message through admin chat.</td>
	</tr>
	<tr>
		<td><b>yell</b></td>
		<td>[message]</td>
		<td>Admin</td>
		<td>The in-game command used for to send a message through admin yell.</td>
	</tr>
	<tr>
		<td><b>psay</b></td>
		<td>[player][message]</td>
		<td>Admin</td>
		<td>The in-game command used for sending a message through admin chat to only a specific player.</td>
	</tr>
	<tr>
		<td><b>pyell</b></td>
		<td>[player][message]</td>
		<td>Admin</td>
		<td>The in-game command used for sending a message through admin yell to only a specific player.</td>
	</tr>
	<tr>
		<td><b>restart</b></td>
		<td>None</td>
		<td>Admin</td>
		<td>The in-game command used for restarting the round.</td>
	</tr>
	<tr>
		<td><b>nextlevel</b></td>
		<td>None</td>
		<td>Admin</td>
		<td>The in-game command used for running the next map in current rotation, but keep all points and KDRs from this round.</td>
	</tr>
	<tr>
		<td><b>endround</b></td>
		<td>[US/RU]</td>
		<td>Admin</td>
		<td>The in-game command used for ending the current round with a winning team. Either US or RU.</td>
	</tr>
	<tr>
		<td><b>yes</b></td>
		<td>None</td>
		<td>All Players</td>
		<td>The in-game command used for confirming other commands when needed.</td>
	</tr>
	<tr>
		<td><b>no</b></td>
		<td>None</td>
		<td>All Players</td>
		<td>The in-game command used to cancel other commands when needed.</td>
	</tr>
</table>
</p>
