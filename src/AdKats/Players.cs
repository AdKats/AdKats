using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using MySqlConnector;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        public void PlayerListingThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Player Listing Thread", 1);
                Thread.CurrentThread.Name = "PlayerListing";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering Player Listing Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        Boolean playerListFetched = false;
                        APlayer pingPickedPlayer = null;

                        //Get all unparsed inbound lists
                        //Only allow player list fetching if the user list is already fetched
                        List<CPlayerInfo> inboundPlayerList = null;
                        lock (_PlayerListProcessingQueue)
                        {
                            if (_PlayerListProcessingQueue.Count > 0 && _firstUserListComplete)
                            {
                                Log.Debug(() => "Inbound player lists found. Grabbing.", 6);
                                while (_PlayerListProcessingQueue.Any())
                                {
                                    inboundPlayerList = _PlayerListProcessingQueue.Dequeue();
                                    playerListFetched = true;
                                    _firstPlayerListStarted = true;
                                }
                                //Clear the queue for next run
                                _PlayerListProcessingQueue.Clear();
                            }
                        }
                        if (inboundPlayerList == null)
                        {
                            inboundPlayerList = new List<CPlayerInfo>();
                        }

                        //Get all unparsed inbound player removals
                        Queue<CPlayerInfo> inboundPlayerRemoval = null;
                        lock (_PlayerRemovalProcessingQueue)
                        {
                            if (_PlayerRemovalProcessingQueue.Count > 0)
                            {
                                Log.Debug(() => "Inbound player removals found. Grabbing.", 6);
                                if (_PlayerRemovalProcessingQueue.Any())
                                {
                                    inboundPlayerRemoval = new Queue<CPlayerInfo>(_PlayerRemovalProcessingQueue.ToArray());
                                }
                                //Clear the queue for next run
                                _PlayerRemovalProcessingQueue.Clear();
                            }
                        }
                        if (inboundPlayerRemoval == null)
                        {
                            inboundPlayerRemoval = new Queue<CPlayerInfo>();
                        }

                        if (!inboundPlayerList.Any() &&
                            !inboundPlayerRemoval.Any() &&
                            !_PlayerRoleRefetch &&
                            !playerListFetched)
                        {
                            Log.Debug(() => "No inbound player listing actions. Waiting for Input.", 5);
                            //Wait for input
                            if (!_firstPlayerListStarted)
                            {
                                DoPlayerListTrigger();
                                Thread.Sleep(1000);
                            }
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. PlayerListing thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _PlayerProcessingWaitHandle.Reset();
                            _PlayerProcessingWaitHandle.WaitOne(TimeSpan.FromSeconds(60));
                            loopStart = UtcNow();
                            if (_firstPlayerListComplete)
                            {
                                //Case where all players are gone after first player list
                                _LastPlayerListProcessed = UtcNow();
                            }
                            continue;
                        }

                        List<string> removedPlayers = new List<string>();
                        lock (_PlayerDictionary)
                        {
                            //Firstly, go through removal queue, remove all names, and log them.
                            while (inboundPlayerRemoval.Any())
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                CPlayerInfo playerInfo = inboundPlayerRemoval.Dequeue();
                                APlayer aPlayer;
                                if (_PlayerDictionary.TryGetValue(playerInfo.SoldierName, out aPlayer))
                                {
                                    //Show leaving messages
                                    Boolean toldAdmins = false;
                                    if (!aPlayer.TargetedRecords.Any(aRecord => aRecord.command_action.command_key == "player_kick" || aRecord.command_action.command_key == "player_ban_temp" || aRecord.command_action.command_key == "player_ban_perm"))
                                    {
                                        List<ARecord> meaningfulRecords = aPlayer.TargetedRecords.Where(aRecord =>
                                        aRecord.command_action.command_key != "banenforcer_enforce" &&
                                        aRecord.command_action.command_key != "player_changeip" &&
                                        aRecord.command_action.command_key != "player_changename" &&
                                        aRecord.command_action.command_key != "player_changetag" &&
                                        aRecord.command_action.command_key != "player_repboost" &&
                                        aRecord.command_action.command_key != "player_pm_send" &&
                                        aRecord.command_action.command_key != "player_pm_reply" &&
                                        aRecord.command_action.command_key != "player_pm_start" &&
                                        aRecord.command_action.command_key != "player_pm_transmit" &&
                                        aRecord.command_action.command_key != "player_pm_cancel" &&
                                        !((aRecord.command_action.command_key == "player_say" ||
                                          aRecord.command_action.command_key == "player_yell" ||
                                          aRecord.command_action.command_key == "player_tell") &&
                                          aRecord.source_player == null) &&
                                        !aRecord.command_action.command_key.Contains("self_")).ToList();
                                        if (meaningfulRecords.Any())
                                        {
                                            List<String> types = (from record in meaningfulRecords select record.command_action.command_name).Distinct().ToList();
                                            String typeString = types.Aggregate("[", (current, type) => current + (type + ", "));
                                            typeString = typeString.Trim().TrimEnd(',') + "]";
                                            if (_ShowTargetedPlayerLeftNotification)
                                            {
                                                toldAdmins = true;
                                                OnlineAdminSayMessage(aPlayer.GetVerboseName() + " left from " + GetPlayerTeamKey(aPlayer) + " " + typeString, aPlayer.player_name);
                                            }
                                            var activeReports = aPlayer.TargetedRecords.Where(aRecord => aRecord.source_player != null &&
                                                                                                         IsActiveReport(aRecord)).ToList();
                                            // Update all the reports
                                            foreach (var report in activeReports)
                                            {
                                                FetchRecordUpdate(report);
                                            }
                                            activeReports = activeReports.Where(report => IsActiveReport(report)).ToList();
                                            foreach (ARecord report in activeReports)
                                            {
                                                // Expire all active reports for the player
                                                report.command_action = GetCommandByKey("player_report_expire");
                                                UpdateRecord(report);
                                            }
                                            foreach (APlayer player in activeReports.Where(report => report.source_player != null)
                                                                             .Select(report => report.source_player).Distinct())
                                            {
                                                player.Say("Player " + aPlayer.GetVerboseName() + " you reported has left.");
                                            }
                                            if (activeReports.Any() && _UseDiscordForReports && _DiscordReportsLeftWithoutAction)
                                            {
                                                _DiscordManager.PostReportToDiscord("Reported player " + aPlayer.GetVerboseName() + " left without being acted on.");
                                            }
                                        }
                                    }
                                    if (GetMatchingVerboseASPlayersOfGroup("watchlist", aPlayer).Any())
                                    {
                                        // Watched player left -> Announce leave.
                                        OnlineAdminSayMessage("Watched player " + aPlayer.GetVerboseName() + " has left the server.");
                                        if (_UseDiscordForWatchlist && _DiscordWatchlistLeftEnabled)
                                        {
                                            _DiscordManager.PostWatchListToDiscord(aPlayer, false, null);
                                        }
                                    }
                                    if (!toldAdmins && aPlayer.player_type == PlayerType.Spectator)
                                    {
                                        OnlineAdminSayMessage(((PlayerIsAdmin(aPlayer)) ? ("Admin ") : ("")) + aPlayer.GetVerboseName() + " stopped spectating.", aPlayer.player_name);
                                    }
                                    //Shut down any running conversations
                                    if (aPlayer.conversationPartner != null)
                                    {
                                        APlayer partner = aPlayer.conversationPartner;
                                        if (PlayerIsExternal(aPlayer.conversationPartner))
                                        {
                                            QueueRecordForProcessing(new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                server_id = partner.player_server.ServerID,
                                                record_orchestrate = true,
                                                command_type = GetCommandByKey("player_pm_cancel"),
                                                command_numeric = 0,
                                                target_name = partner.player_name,
                                                target_player = partner,
                                                source_name = aPlayer.player_name,
                                                source_player = aPlayer,
                                                record_message = aPlayer.GetVerboseName() + " has left their server. Private conversation closed.",
                                                record_time = UtcNow()
                                            });
                                        }
                                        else
                                        {
                                            partner.Say(aPlayer.GetVerboseName() + " has left. Private conversation closed.");
                                            partner.conversationPartner = null;
                                        }
                                        aPlayer.conversationPartner = null;
                                    }
                                    if ((_roundState == RoundState.Loaded || (_roundState == RoundState.Playing && _serverInfo.GetRoundElapsedTime().TotalMinutes < 2)) && !PlayerIsAdmin(aPlayer))
                                    {
                                        _mapDetrimentIndex++;
                                    }
                                    //Remove from populators
                                    _populationPopulatingPlayers.Remove(aPlayer.player_name);
                                    //Add player to the left dictionary
                                    aPlayer.player_online = false;
                                    aPlayer.player_new = false;
                                    aPlayer.player_server = null;
                                    aPlayer.player_spawnedOnce = false;
                                    aPlayer.player_chatOnce = false;
                                    aPlayer.LiveKills.Clear();
                                    aPlayer.ClearPingEntries();
                                    DequeuePlayer(aPlayer);
                                    //Remove all old values
                                    List<String> removeNames = _PlayerLeftDictionary.Where(pair => (UtcNow() - pair.Value.LastUsage).TotalMinutes > 120).Select(pair => pair.Key).ToList();
                                    foreach (String removeName in removeNames)
                                    {
                                        _PlayerLeftDictionary.Remove(removeName);
                                    }
                                    aPlayer.LastUsage = UtcNow();
                                    _PlayerLeftDictionary[aPlayer.player_name] = aPlayer;
                                }
                                RemovePlayerFromDictionary(playerInfo.SoldierName, false);
                                removedPlayers.Add(playerInfo.SoldierName);
                            }
                            List<string> validPlayers = new List<String>();
                            var fetchAccessAfterList = false;
                            if (inboundPlayerList.Count > 0)
                            {
                                Log.Debug(() => "Listing Players", 5);
                                //Loop over all players in the list

                                List<Double> durations = new List<Double>();
                                IEnumerable<CPlayerInfo> trimmedInboundPlayers = inboundPlayerList.Where(player => !removedPlayers.Contains(player.SoldierName));
                                Int32 index = 0;
                                foreach (CPlayerInfo playerInfo in trimmedInboundPlayers)
                                {
                                    index++;
                                    Stopwatch timer = new Stopwatch();
                                    timer.Start();
                                    if (!_pluginEnabled)
                                    {
                                        break;
                                    }
                                    //Check for glitched players
                                    if (String.IsNullOrEmpty(playerInfo.GUID))
                                    {
                                        if ((UtcNow() - _lastGlitchedPlayerNotification).TotalMinutes > 5)
                                        {
                                            OnlineAdminSayMessage(playerInfo.SoldierName + " is glitched, their player has no GUID.");
                                            Log.Warn(playerInfo.SoldierName + " is glitched, their player has no GUID.");
                                            _lastGlitchedPlayerNotification = UtcNow();
                                        }
                                        continue;
                                    }
                                    //Check for invalid player names
                                    if (!IsSoldierNameValid(playerInfo.SoldierName))
                                    {
                                        if ((UtcNow() - _lastInvalidPlayerNameNotification).TotalMinutes > 5)
                                        {
                                            OnlineAdminSayMessage(playerInfo.SoldierName + " had an invalid player name, unable to process.");
                                            Log.Warn(playerInfo.SoldierName + " has an invalid player name, unable to process.");
                                            KickPlayerMessage(playerInfo.SoldierName, "Your soldier name " + playerInfo.SoldierName + " is invalid.", 30);
                                            _lastInvalidPlayerNameNotification = UtcNow();
                                        }
                                        continue;
                                    }

                                    validPlayers.Add(playerInfo.SoldierName);
                                    //Check if the player is already in the player dictionary
                                    APlayer aPlayer = null;
                                    if (_PlayerDictionary.TryGetValue(playerInfo.SoldierName, out aPlayer))
                                    {
                                        //They are
                                        if (aPlayer.fbpInfo.Score != playerInfo.Score || aPlayer.fbpInfo.Kills != playerInfo.Kills || aPlayer.fbpInfo.Deaths != playerInfo.Deaths)
                                        {
                                            aPlayer.lastAction = UtcNow();
                                        }
                                        aPlayer.fbpInfo = playerInfo;
                                        if (_MissingPlayers.Contains(aPlayer.player_name))
                                        {
                                            Log.Success("Missing player " + aPlayer.GetVerboseName() + " finally loaded.");
                                            _MissingPlayers.Remove(aPlayer.player_name);
                                        }
                                        switch (aPlayer.fbpInfo.Type)
                                        {
                                            case 0:
                                                aPlayer.player_type = PlayerType.Player;
                                                break;
                                            case 1:
                                                aPlayer.player_type = PlayerType.Spectator;
                                                break;
                                            case 2:
                                                aPlayer.player_type = PlayerType.CommanderPC;
                                                break;
                                            case 3:
                                                aPlayer.player_type = PlayerType.CommanderMobile;
                                                break;
                                            default:
                                                Log.Error("Player type " + aPlayer.fbpInfo.Type + " is not valid.");
                                                break;
                                        }

                                        if (_roundState == RoundState.Playing)
                                        {
                                            Boolean proconFetched = false;
                                            Double ping = aPlayer.fbpInfo.Ping;
                                            if (((_pingEnforcerKickMissingPings && _attemptManualPingWhenMissing && ping < 0) || aPlayer.player_ping_manual) &&
                                                !String.IsNullOrEmpty(aPlayer.player_ip))
                                            {
                                                PingReply reply = null;
                                                try
                                                {
                                                    reply = _PingProcessor.Send(aPlayer.player_ip, 1000);
                                                }
                                                catch (Exception e)
                                                {
                                                    Log.HandleException(new AException("Error fetching manual player ping.", e));
                                                }
                                                if (reply != null && reply.Status == IPStatus.Success)
                                                {
                                                    ping = reply.RoundtripTime;
                                                    proconFetched = true;
                                                }
                                                else
                                                {
                                                    Log.Debug(() => "Ping status for " + aPlayer.GetVerboseName() + ": " + reply.Status, 5);
                                                    ping = -1;
                                                }
                                            }
                                            aPlayer.AddPingEntry(ping);

                                            //Automatic ping kick
                                            if (_pingEnforcerEnable &&
                                                aPlayer.player_type == PlayerType.Player &&
                                                !PlayerIsAdmin(aPlayer) &&
                                                !GetMatchingVerboseASPlayersOfGroup("whitelist_ping", aPlayer).Any() &&
                                                !_pingEnforcerIgnoreRoles.Contains(aPlayer.player_role.role_key) &&
                                                !(_pingEnforcerIgnoreUserList &&
                                                  FetchAllUserSoldiers().Any(sPlayer => sPlayer.player_guid == aPlayer.player_guid)) &&
                                                GetPlayerCount() > _pingEnforcerTriggerMinimumPlayers)
                                            {
                                                Double currentTriggerMS = GetPingLimit();
                                                //Warn players of limit and spikes
                                                if (ping > currentTriggerMS)
                                                {
                                                    if (aPlayer.player_pings_full && aPlayer.player_ping_avg < currentTriggerMS && ping > (aPlayer.player_ping_avg * 1.5))
                                                    {
                                                        aPlayer.Say("Warning, your ping is spiking. Current: [" + Math.Round(ping) + "ms] Avg: [" + Math.Round(aPlayer.player_ping_avg, 1) + "ms]" + ((proconFetched) ? ("[PR]") : ("")), _pingEnforcerDisplayProconChat, 1);
                                                    }
                                                    else
                                                    {
                                                        aPlayer.Say("Warning, your ping is over the limit. [" + Math.Round(aPlayer.player_ping, 1) + "ms]" + ((proconFetched) ? ("[PR]") : ("")), _pingEnforcerDisplayProconChat, 1);
                                                    }
                                                }
                                                //Are they over the limit, or missing
                                                if (((aPlayer.player_ping_avg > currentTriggerMS && aPlayer.player_ping > aPlayer.player_ping_avg) || (_pingEnforcerKickMissingPings && aPlayer.player_ping_avg < 0 && (UtcNow() - aPlayer.JoinTime).TotalSeconds > 60)) && aPlayer.player_pings_full)
                                                {
                                                    //Are they worse than the current picked player
                                                    if (pingPickedPlayer == null || (aPlayer.player_ping_avg > pingPickedPlayer.player_ping_avg && pingPickedPlayer.player_ping_avg > 0))
                                                    {
                                                        pingPickedPlayer = aPlayer;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            aPlayer.ClearPingEntries();
                                        }
                                        if (_CMDRManagerEnable &&
                                            _firstPlayerListComplete &&
                                            (aPlayer.player_type == PlayerType.CommanderPC || aPlayer.player_type == PlayerType.CommanderMobile) &&
                                            GetPlayerCount() < (0.75 * _CMDRMinimumPlayers))
                                        {
                                            ARecord record = new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                server_id = _serverInfo.ServerID,
                                                command_type = GetCommandByKey("player_kick"),
                                                command_numeric = 0,
                                                target_name = aPlayer.player_name,
                                                target_player = aPlayer,
                                                source_name = "CMDRManager",
                                                record_message = "Commanders not allowed until " + _CMDRMinimumPlayers + " active players",
                                                record_time = UtcNow()
                                            };
                                            QueueRecordForProcessing(record);
                                        }
                                    }
                                    else
                                    {
                                        //Player is not already online, handle fetching
                                        //First check if the player is rejoining current AdKats session
                                        aPlayer = _PlayerLeftDictionary.Values.FirstOrDefault(oPlayer => oPlayer.player_guid == playerInfo.GUID);
                                        if (aPlayer != null)
                                        {
                                            Log.Debug(() => "Player " + playerInfo.SoldierName + " re-joined.", 3);
                                            //Remove them from the left dictionary
                                            _PlayerLeftDictionary.Remove(playerInfo.SoldierName);
                                            //check for name changes
                                            if (!String.IsNullOrEmpty(playerInfo.SoldierName) && playerInfo.SoldierName != aPlayer.player_name)
                                            {
                                                aPlayer.player_name_previous = aPlayer.player_name;
                                                aPlayer.player_name = playerInfo.SoldierName;
                                                ARecord record = new ARecord
                                                {
                                                    record_source = ARecord.Sources.Automated,
                                                    server_id = _serverInfo.ServerID,
                                                    command_type = GetCommandByKey("player_changename"),
                                                    command_numeric = 0,
                                                    target_name = aPlayer.player_name,
                                                    target_player = aPlayer,
                                                    source_name = "AdKats",
                                                    record_message = aPlayer.player_name_previous,
                                                    record_time = UtcNow()
                                                };
                                                QueueRecordForProcessing(record);
                                                Log.Debug(() => aPlayer.player_name_previous + " changed their name to " + playerInfo.SoldierName + ". Updating the database.", 2);
                                                if (_ShowPlayerNameChangeAnnouncement)
                                                {
                                                    OnlineAdminSayMessage(aPlayer.player_name_previous + " changed their name to " + playerInfo.SoldierName);
                                                }
                                                UpdatePlayer(aPlayer);
                                            }
                                            if (aPlayer.TargetedRecords.Any(aRecord =>
                                                    aRecord.command_action.command_key == "player_kick" &&
                                                    (UtcNow() - aRecord.record_time).TotalMinutes < 30) &&
                                                    aPlayer.TargetedRecords.All(aRecord => aRecord.command_action.command_key != "banenforcer_enforce" &&
                                                    // Don't show the message if the person kicked themselves
                                                    aRecord.source_name != aPlayer.player_name))
                                            {
                                                OnlineAdminSayMessage("Kicked player " + aPlayer.GetVerboseName() + " re-joined.");
                                            }
                                            // Increment the player's active session
                                            // Helps us determine which session a record came from
                                            aPlayer.ActiveSession++;
                                        }
                                        else
                                        {
                                            //If they aren't in the list, fetch their information from the database
                                            aPlayer = FetchPlayer(true, false, false, null, -1, playerInfo.SoldierName, playerInfo.GUID, null, null);
                                            if (aPlayer == null)
                                            {
                                                //Do not handle the player if not returned
                                                continue;
                                            }
                                        }
                                        if (aPlayer.player_firstseen > _AutoKickNewPlayerDate)
                                        {
                                            // This player is newer to the server than the maximum first seen date, kick them
                                            KickPlayerMessage(aPlayer.player_name, "Please Contact The Server Admin", 0);
                                        }
                                        aPlayer.player_online = true;
                                        aPlayer.JoinTime = UtcNow();
                                        //Fetch their infraction points
                                        FetchPoints(aPlayer, false, true);
                                        //Team Power Information
                                        FetchPowerInformation(aPlayer);
                                        if (ChallengeManager != null)
                                        {
                                            if (ChallengeManager.Loading)
                                            {
                                                Threading.Wait(5000);
                                            }
                                            ChallengeManager.AssignActiveEntryForPlayer(aPlayer);
                                        }
                                        if (aPlayer.location == null || aPlayer.location.status != "success" || aPlayer.location.IP != aPlayer.player_ip)
                                        {
                                            //Update IP location
                                            QueuePlayerForIPInfoFetch(aPlayer);
                                        }

                                        //Last Punishment
                                        List<ARecord> punishments = FetchRecentRecords(aPlayer.player_id, GetCommandByKey("player_punish").command_id, 1000, 1, true, false);
                                        if (punishments.Any())
                                        {
                                            aPlayer.LastPunishment = punishments.FirstOrDefault();
                                        }
                                        //Last Forgive
                                        List<ARecord> forgives = FetchRecentRecords(aPlayer.player_id, GetCommandByKey("player_forgive").command_id, 1000, 1, true, false);
                                        if (forgives.Any())
                                        {
                                            aPlayer.LastForgive = forgives.FirstOrDefault();
                                        }
                                        aPlayer.player_server = _serverInfo;
                                        //Add the frostbite player info
                                        aPlayer.fbpInfo = playerInfo;
                                        if (_MissingPlayers.Contains(aPlayer.player_name))
                                        {
                                            Log.Success("Missing player " + aPlayer.GetVerboseName() + " finally loaded.");
                                            _MissingPlayers.Remove(aPlayer.player_name);
                                        }
                                        String joinLocation = String.Empty;
                                        ATeam playerTeam = null;
                                        if (aPlayer.fbpInfo != null)
                                        {
                                            _teamDictionary.TryGetValue(aPlayer.fbpInfo.TeamID, out playerTeam);
                                        }
                                        //Check for moving to teams aside from the one they are required to be on
                                        if (aPlayer.RequiredTeam != null &&
                                            playerTeam != null &&
                                            aPlayer.player_type == PlayerType.Player &&
                                            aPlayer.RequiredTeam.TeamKey != playerTeam.TeamKey &&
                                            (!PlayerIsAdmin(aPlayer) || !aPlayer.player_spawnedRound))
                                        {
                                            // Don't allow a player to be reassigned to the "neutral" team
                                            // Otherwise, run a mock assist command on them to see if they can reassign themselves
                                            if (playerTeam.TeamKey != "Neutral" &&
                                                RunAssist(aPlayer, null, null, true) &&
                                                _roundState == RoundState.Playing &&
                                                _serverInfo.GetRoundElapsedTime().TotalMinutes > _minimumAssistMinutes)
                                            {
                                                if (_serverInfo.GetRoundElapsedTime().TotalMinutes > 3)
                                                {
                                                    OnlineAdminSayMessage(aPlayer.GetVerboseName() + " (" + Math.Round(aPlayer.GetPower(true)) + ") REASSIGNED themselves from " + aPlayer.RequiredTeam.GetTeamIDKey() + " to " + playerTeam.GetTeamIDKey() + ".");
                                                }
                                                aPlayer.RequiredTeam = playerTeam;
                                            }
                                            else
                                            {
                                                if (_roundState == RoundState.Playing &&
                                                    NowDuration(aPlayer.lastSwitchMessage).TotalSeconds > 5)
                                                {
                                                    if (_UseExperimentalTools)
                                                    {
                                                        var message = Log.CViolet(aPlayer.GetVerboseName() + " (" + Math.Round(aPlayer.GetPower(true)) + ") re-joined, sending them back to " + aPlayer.RequiredTeam.GetTeamIDKey() + ".");
                                                        ProconChatWrite(Log.FBold(message));
                                                    }
                                                    PlayerTellMessage(aPlayer.player_name, "You were assigned to " + aPlayer.RequiredTeam.TeamKey + ". Try using " + GetChatCommandByKey("self_assist") + " to switch.");
                                                    aPlayer.lastSwitchMessage = UtcNow();
                                                }
                                                Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                                                ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                                                _MULTIBalancerUnswitcherDisabled = true;
                                                ExecuteCommand("procon.protected.send", "admin.movePlayer", aPlayer.player_name, aPlayer.RequiredTeam.TeamID.ToString(), "1", "false");
                                            }
                                        }
                                        switch (aPlayer.fbpInfo.Type)
                                        {
                                            case 0:
                                                aPlayer.player_type = PlayerType.Player;
                                                if (playerTeam == null || playerTeam.TeamID == 0)
                                                {
                                                    joinLocation += "player";
                                                }
                                                else
                                                {
                                                    joinLocation += playerTeam.GetTeamIDKey() + " player";
                                                }
                                                break;
                                            case 1:
                                                aPlayer.player_type = PlayerType.Spectator;
                                                joinLocation += "spectator";
                                                break;
                                            case 2:
                                                aPlayer.player_type = PlayerType.CommanderPC;
                                                joinLocation += "commander";
                                                break;
                                            case 3:
                                                aPlayer.player_type = PlayerType.CommanderMobile;
                                                if (playerTeam != null)
                                                {
                                                    joinLocation += playerTeam.GetTeamIDKey() + " ";
                                                }
                                                joinLocation += "tablet commander";
                                                break;
                                            default:
                                                Log.Error("Player type " + aPlayer.fbpInfo.Type + " is not valid.");
                                                break;
                                        }
                                        if (aPlayer.player_type == PlayerType.Spectator)
                                        {
                                            if (GetMatchingVerboseASPlayersOfGroup("blacklist_spectator", aPlayer).Any())
                                            {
                                                ARecord record = new ARecord
                                                {
                                                    record_source = ARecord.Sources.Automated,
                                                    server_id = _serverInfo.ServerID,
                                                    command_type = GetCommandByKey("player_kick"),
                                                    command_numeric = 0,
                                                    target_name = aPlayer.player_name,
                                                    target_player = aPlayer,
                                                    source_name = "SpectatorManager",
                                                    record_message = "You may not spectate at this time.",
                                                    record_time = UtcNow()
                                                };
                                                QueueRecordForProcessing(record);
                                            }
                                            if (GetVerboseASPlayersOfGroup("slot_spectator").Any() &&
                                                !GetMatchingVerboseASPlayersOfGroup("slot_spectator", aPlayer).Any())
                                            {
                                                ARecord record = new ARecord
                                                {
                                                    record_source = ARecord.Sources.Automated,
                                                    server_id = _serverInfo.ServerID,
                                                    command_type = GetCommandByKey("player_kick"),
                                                    command_numeric = 0,
                                                    target_name = aPlayer.player_name,
                                                    target_player = aPlayer,
                                                    source_name = "SpectatorManager",
                                                    record_message = "Whitelist required to spectate.",
                                                    record_time = UtcNow()
                                                };
                                                QueueRecordForProcessing(record);
                                            }
                                        }
                                        bool isAdmin = PlayerIsAdmin(aPlayer);
                                        if (_firstPlayerListComplete)
                                        {
                                            //Notify reputable players
                                            if (isAdmin || aPlayer.player_aa)
                                            {
                                                String message = ((isAdmin) ? ("Admin ") : ("Admin assistant ")) + aPlayer.GetVerboseName() + " joined as a " + joinLocation + ".";
                                                if (_InformReputablePlayersOfAdminJoins)
                                                {
                                                    List<APlayer> reputablePlayers = _PlayerDictionary.Values.Where(iPlayer => iPlayer.player_reputation >= _reputationThresholdGood && !PlayerIsAdmin(iPlayer)).ToList();
                                                    foreach (APlayer reputablePlayer in reputablePlayers)
                                                    {
                                                        reputablePlayer.Say(message);
                                                    }
                                                }
                                                if (_InformAdminsOfAdminJoins)
                                                {
                                                    OnlineAdminSayMessage(message);
                                                }
                                            }
                                            else if (aPlayer.player_type == PlayerType.Spectator)
                                            {
                                                OnlineAdminSayMessage(((PlayerIsAdmin(aPlayer)) ? ("Admin ") : ("")) + aPlayer.GetVerboseName() + " is now spectating.");
                                            }
                                            // Check Watchlist
                                            if (GetMatchingVerboseASPlayersOfGroup("watchlist", aPlayer).Any())
                                            {
                                                OnlineAdminSayMessage("Watched player " + aPlayer.GetVerboseName() + " has joined the server as a " + joinLocation + ".");
                                                if (_UseDiscordForWatchlist)
                                                {
                                                    _DiscordManager.PostWatchListToDiscord(aPlayer, true, joinLocation);
                                                }
                                            }
                                            //If populating, add player
                                            if (_populationPopulating && _populationStatus == PopulationState.Low && aPlayer.player_type == PlayerType.Player && _populationPopulatingPlayers.Count < _lowPopulationPlayerCount)
                                            {
                                                _populationPopulatingPlayers[aPlayer.player_name] = aPlayer;
                                            }
                                            //Increment benefit index
                                            if ((_roundState == RoundState.Playing || _roundState == RoundState.Loaded) && !PlayerIsAdmin(aPlayer))
                                            {
                                                _mapBenefitIndex++;
                                            }
                                        }
                                        //Set their last death/spawn times
                                        aPlayer.lastDeath = UtcNow();
                                        aPlayer.lastSpawn = UtcNow();
                                        aPlayer.lastAction = UtcNow();
                                        //Add them to the dictionary
                                        _PlayerDictionary.Add(playerInfo.SoldierName, aPlayer);

                                        //Get their battlelog information, or update the already fetched battlelog info
                                        QueuePlayerForBattlelogInfoFetch(aPlayer);

                                        //If they are an admin, and if we protect admins from VIP kicks, update the user list
                                        if (_firstPlayerListComplete && isAdmin && _FeedServerReservedSlots && _FeedServerReservedSlots_Admins_VIPKickWhitelist)
                                        {
                                            fetchAccessAfterList = true;
                                        }
                                        //Update rep
                                        UpdatePlayerReputation(aPlayer, false);
                                        //If using ban enforcer, check the player's ban status
                                        if (_UseBanEnforcer)
                                        {
                                            QueuePlayerForBanCheck(aPlayer);
                                        }
                                        else
                                        {
                                            //Queue the player for a AntiCheat check
                                            QueuePlayerForAntiCheatCheck(aPlayer);
                                        }
                                    }
                                    if (_CMDRManagerEnable &&
                                        _firstPlayerListComplete &&
                                        (aPlayer.player_type == PlayerType.CommanderPC || aPlayer.player_type == PlayerType.CommanderMobile) &&
                                        GetPlayerCount() < _CMDRMinimumPlayers)
                                    {
                                        ARecord record = new ARecord
                                        {
                                            record_source = ARecord.Sources.Automated,
                                            server_id = _serverInfo.ServerID,
                                            command_type = GetCommandByKey("player_kick"),
                                            command_numeric = 0,
                                            target_name = aPlayer.player_name,
                                            target_player = aPlayer,
                                            source_name = "CMDRManager",
                                            record_message = "Commanders not allowed until " + _CMDRMinimumPlayers + " active players",
                                            record_time = UtcNow()
                                        };
                                        QueueRecordForProcessing(record);
                                    }
                                    //Update them to round players
                                    HashSet<Int64> roundPlayers;
                                    if (!_RoundPlayerIDs.TryGetValue(_roundID, out roundPlayers))
                                    {
                                        roundPlayers = new HashSet<Int64>();
                                        _RoundPlayerIDs[_roundID] = roundPlayers;
                                    }
                                    roundPlayers.Add(aPlayer.player_id);
                                    timer.Stop();
                                    durations.Add(timer.Elapsed.TotalSeconds);
                                    if (!_firstPlayerListComplete)
                                    {
                                        Log.Write(index + "/" + trimmedInboundPlayers.Count() + " players loaded (" + aPlayer.player_name + "). " + Math.Round(durations.Sum() / durations.Count, 2) + "s per player.");
                                    }
                                    if (!aPlayer.RoundStats.ContainsKey(_roundID))
                                    {
                                        aPlayer.RoundStats[_roundID] = new APlayerStats(_roundID);
                                    }
                                    if (_roundState == RoundState.Playing)
                                    {
                                        aPlayer.RoundStats[_roundID].LiveStats = aPlayer.fbpInfo;
                                    }
                                }

                                ATeam team1, team2;
                                if (GetTeamByID(1, out team1))
                                {
                                    team1.UpdatePlayerCount(GetPlayerCount(true, true, true, 1));
                                }
                                if (GetTeamByID(2, out team2))
                                {
                                    team2.UpdatePlayerCount(GetPlayerCount(true, true, true, 2));
                                }
                                //Make sure the player dictionary is clean of any straglers
                                Int32 straglerCount = 0;
                                Int32 dicCount = _PlayerDictionary.Count;
                                foreach (string playerName in _PlayerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList())
                                {
                                    straglerCount++;
                                    Log.Debug(() => "Removing " + playerName + " from current player list (VIA CLEANUP).", 4);
                                    APlayer aPlayer;
                                    if (_PlayerDictionary.TryGetValue(playerName, out aPlayer))
                                    {
                                        //Shut down any running conversations
                                        if (aPlayer.conversationPartner != null)
                                        {
                                            APlayer partner = aPlayer.conversationPartner;
                                            if (PlayerIsExternal(aPlayer.conversationPartner))
                                            {
                                                QueueRecordForProcessing(new ARecord
                                                {
                                                    record_source = ARecord.Sources.Automated,
                                                    server_id = partner.player_server.ServerID,
                                                    record_orchestrate = true,
                                                    command_type = GetCommandByKey("player_pm_cancel"),
                                                    command_numeric = 0,
                                                    target_name = partner.player_name,
                                                    target_player = partner,
                                                    source_name = aPlayer.player_name,
                                                    source_player = aPlayer,
                                                    record_message = aPlayer.GetVerboseName() + " has left their server. Private conversation closed.",
                                                    record_time = UtcNow()
                                                });
                                            }
                                            else
                                            {
                                                partner.Say(aPlayer.GetVerboseName() + " has left. Private conversation closed.");
                                                partner.conversationPartner = null;
                                            }
                                            aPlayer.conversationPartner = null;
                                        }
                                        //Remove from populators
                                        _populationPopulatingPlayers.Remove(aPlayer.player_name);
                                        //Add player to the left dictionary
                                        aPlayer.player_online = false;
                                        aPlayer.player_new = false;
                                        aPlayer.player_server = null;
                                        aPlayer.player_spawnedOnce = false;
                                        aPlayer.player_chatOnce = false;
                                        aPlayer.ClearPingEntries();
                                        aPlayer.LiveKills.Clear();
                                        DequeuePlayer(aPlayer);
                                        //Remove all old values
                                        List<String> removeNames = _PlayerLeftDictionary.Where(pair => (UtcNow() - pair.Value.LastUsage).TotalMinutes > 120).Select(pair => pair.Key).ToList();
                                        foreach (String removeName in removeNames)
                                        {
                                            _PlayerLeftDictionary.Remove(removeName);
                                        }
                                        aPlayer.LastUsage = UtcNow();
                                        _PlayerLeftDictionary[aPlayer.player_name] = aPlayer;
                                    }
                                    _PlayerDictionary.Remove(playerName);
                                }
                                if (straglerCount > 1 && straglerCount > (dicCount / 2))
                                {
                                    ARecord record = new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        isDebug = true,
                                        server_id = _serverInfo.ServerID,
                                        command_type = GetCommandByKey("player_calladmin"),
                                        command_numeric = straglerCount,
                                        target_name = "Server",
                                        target_player = null,
                                        source_name = "AdKats",
                                        record_message = "Server Crashed (" + dicCount + " Players Lost)",
                                        record_time = UtcNow()
                                    };
                                    //Process the record
                                    QueueRecordForProcessing(record);
                                    Log.Error(record.record_message);
                                    //Set round ended
                                    _roundState = RoundState.Ended;
                                    //Clear populators
                                    _populationPopulatingPlayers.Clear();
                                }
                            }
                            if (fetchAccessAfterList)
                            {
                                FetchAllAccess(true);
                            }
                            if (_PlayerRoleRefetch || !_firstPlayerListComplete)
                            {
                                //Update roles for all fetched players
                                foreach (APlayer aPlayer in GetFetchedPlayers())
                                {
                                    AssignPlayerRole(aPlayer);
                                }
                                _PlayerRoleRefetch = false;
                            }

                            if (_firstPlayerListComplete)
                            {
                                Int32 playerCount = GetPlayerCount();
                                if (playerCount < _lowPopulationPlayerCount)
                                {
                                    switch (_populationStatus)
                                    {
                                        case PopulationState.Unknown:
                                            _populationTransitionTime = UtcNow();
                                            OnlineAdminSayMessage("Server in populating mode.");
                                            break;
                                        case PopulationState.Low:
                                            //Current state
                                            _populationDurations[PopulationState.Low] += (UtcNow() - _populationUpdateTime);
                                            break;
                                        case PopulationState.Medium:
                                            _populationTransitionTime = UtcNow();
                                            _populationDurations[PopulationState.Medium] += (UtcNow() - _populationTransitionTime);
                                            OnlineAdminSayMessage("Server now in populating mode, with " + playerCount + " populators.");
                                            break;
                                        case PopulationState.High:
                                            _populationTransitionTime = UtcNow();
                                            _populationDurations[PopulationState.High] += (UtcNow() - _populationTransitionTime);
                                            OnlineAdminSayMessage("Server now in populating mode, with " + playerCount + " populators.");
                                            break;
                                        default:
                                            break;
                                    }
                                    if (!_populationPopulating)
                                    {
                                        _populationPopulatingPlayers.Clear();
                                        _populationPopulating = true;
                                        foreach (APlayer popPlayer in _PlayerDictionary.Values.ToList().Where(player => player.player_type == PlayerType.Player &&
                                                                                                                        NowDuration(player.lastAction).TotalMinutes < 20).ToList())
                                        {
                                            _populationPopulatingPlayers[popPlayer.player_name] = popPlayer;
                                        }
                                    }
                                    _populationStatus = PopulationState.Low;
                                }
                                else if (playerCount < _highPopulationPlayerCount)
                                {
                                    switch (_populationStatus)
                                    {
                                        case PopulationState.Unknown:
                                            _populationTransitionTime = UtcNow();
                                            break;
                                        case PopulationState.Low:
                                            _populationTransitionTime = UtcNow();
                                            _populationDurations[PopulationState.Low] += (UtcNow() - _populationTransitionTime);
                                            break;
                                        case PopulationState.Medium:
                                            //Current state
                                            _populationDurations[PopulationState.Medium] += (UtcNow() - _populationUpdateTime);
                                            break;
                                        case PopulationState.High:
                                            _populationTransitionTime = UtcNow();
                                            _populationDurations[PopulationState.High] += (UtcNow() - _populationTransitionTime);
                                            break;
                                        default:
                                            break;
                                    }
                                    _populationStatus = PopulationState.Medium;
                                }
                                else
                                {
                                    switch (_populationStatus)
                                    {
                                        case PopulationState.Unknown:
                                            _populationTransitionTime = UtcNow();
                                            break;
                                        case PopulationState.Low:
                                            _populationTransitionTime = UtcNow();
                                            _populationDurations[PopulationState.Low] += (UtcNow() - _populationTransitionTime);
                                            break;
                                        case PopulationState.Medium:
                                            _populationTransitionTime = UtcNow();
                                            _populationDurations[PopulationState.Medium] += (UtcNow() - _populationTransitionTime);
                                            break;
                                        case PopulationState.High:
                                            //Current state
                                            _populationDurations[PopulationState.High] += (UtcNow() - _populationUpdateTime);
                                            break;
                                        default:
                                            break;
                                    }
                                    if (_populationPopulating)
                                    {
                                        foreach (APlayer popPlayer in _populationPopulatingPlayers.Values.Where(aPlayer => aPlayer.player_online && _PlayerDictionary.ContainsKey(aPlayer.player_name) && aPlayer.player_type == PlayerType.Player))
                                        {
                                            QueueRecordForProcessing(new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                server_id = _serverInfo.ServerID,
                                                command_type = GetCommandByKey("player_population_success"),
                                                command_numeric = 0,
                                                target_name = popPlayer.player_name,
                                                target_player = popPlayer,
                                                source_name = "PopulationManager",
                                                record_message = "Populated Server " + _serverInfo.ServerID,
                                                record_time = UtcNow()
                                            });
                                        }
                                        _populationPopulatingPlayers.Clear();
                                        _populationPopulating = false;
                                    }
                                    _populationStatus = PopulationState.High;
                                }
                                _populationUpdateTime = UtcNow();
                            }
                        }

                        if (pingPickedPlayer != null)
                        {
                            ARecord record = new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_kick"),
                                command_numeric = 0,
                                target_name = pingPickedPlayer.player_name,
                                target_player = pingPickedPlayer,
                                source_name = "PingEnforcer",
                                record_message = _pingEnforcerMessagePrefix + " " + ((pingPickedPlayer.player_ping_avg > 0) ? ("Cur:[" + Math.Round(pingPickedPlayer.player_ping) + "ms] Avg:[" + Math.Round(pingPickedPlayer.player_ping_avg) + "ms]") : ("[Missing]")),
                                record_time = UtcNow()
                            };
                            QueueRecordForProcessing(record);
                        }

                        //Update last successful player list time
                        DoPlayerListProcessed();
                        //Set required handles
                        _PlayerListUpdateWaitHandle.Set();
                        _TeamswapWaitHandle.Set();
                        //Push online player subscription
                        if (playerListFetched && _pluginEnabled)
                        {
                            SendOnlineSoldiers();
                        }
                        if (!_firstPlayerListComplete && playerListFetched && _pluginEnabled)
                        {
                            _AdKatsRunningTime = UtcNow();
                            _firstPlayerListComplete = true;
                            OnlineAdminSayMessage("Player listing complete [" + _PlayerDictionary.Count + " players]. Performing final startup.");
                            Log.Success("Player listing complete [" + _PlayerDictionary.Count + " players].");

                            Log.Info("Performing final startup.");

                            //Immediately request another player list to make sure we haven't missed anyone who just joined.
                            DoPlayerListTrigger();

                            //Do another access fetch to make sure server information is current
                            FetchAllAccess(true);

                            //Register external plugin commands
                            RegisterCommand(_issueCommandMatchCommand);
                            RegisterCommand(_fetchAuthorizedSoldiersMatchCommand);
                            Threading.Wait(500);

                            var startupDuration = NowDuration(_AdKatsStartTime);
                            _startupDurations.Enqueue(startupDuration);
                            while (_startupDurations.Count() > 10)
                            {
                                _startupDurations.Dequeue();
                            }
                            var averageStartupDuration = TimeSpan.FromSeconds(_startupDurations.Average(span => span.TotalSeconds));
                            var averageDurationString = "(" + FormatTimeString(averageStartupDuration, 3) + ":" + _startupDurations.Count() + ")";
                            OnlineAdminTellMessage("AdKats startup complete [" + FormatTimeString(startupDuration, 3) + "]" + averageDurationString + ". Commands are now online.");
                            foreach (String playerName in _PlayersRequestingCommands)
                            {
                                APlayer aPlayer;
                                if (_PlayerDictionary.TryGetValue(playerName, out aPlayer))
                                {
                                    if (!PlayerIsAdmin(aPlayer))
                                    {
                                        PlayerTellMessage(aPlayer.player_name, "AdKats commands now online. Thank you for your patience.");
                                    }
                                }
                            }
                            Log.Success("AdKats " + GetPluginVersion() + " startup complete [" + FormatTimeString(UtcNow() - _AdKatsStartTime, 3) + "]. Commands are now online.");

                            if (_TeamspeakPlayerMonitorEnable)
                            {
                                _TeamspeakManager.Enable();
                            }

                            if (_DiscordPlayerMonitorEnable && _DiscordPlayerMonitorView)
                            {
                                _DiscordManager.Enable();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.Warn("player listing thread was force aborted. Exiting.");
                            break;
                        }
                        Log.HandleException(new AException("Error occured in player listing thread. Skipping loop.", e));
                    }
                }
                Log.Debug(() => "Ending Player Listing Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in player listing thread.", e));
            }
        }

        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo cpbiPlayer)
        {
            try
            {
                Log.Debug(() => "OnPunkbusterPlayerInfo fired!", 7);
                APlayer aPlayer;
                if (_PlayerDictionary.TryGetValue(cpbiPlayer.SoldierName, out aPlayer))
                {
                    Boolean updatePlayer = false;
                    //Update the player with pb info
                    aPlayer.PBPlayerInfo = cpbiPlayer;
                    aPlayer.player_pbguid = cpbiPlayer.GUID;
                    aPlayer.player_slot = cpbiPlayer.SlotID;
                    String player_ip = cpbiPlayer.Ip.Split(':')[0];
                    if (player_ip != aPlayer.player_ip && !String.IsNullOrEmpty(player_ip))
                    {
                        updatePlayer = true;
                        if (!String.IsNullOrEmpty(aPlayer.player_ip))
                        {
                            Log.Debug(() => aPlayer.GetVerboseName() + " changed their IP from " + aPlayer.player_ip + " to " + player_ip + ". Updating the database.", 2);
                            ARecord record = new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_changeip"),
                                command_numeric = 0,
                                target_name = aPlayer.player_name,
                                target_player = aPlayer,
                                source_name = "AdKats",
                                record_message = aPlayer.player_ip,
                                record_time = UtcNow()
                            };
                            QueueRecordForProcessing(record);
                        }
                    }
                    aPlayer.SetIP(player_ip);

                    if (aPlayer.location == null || aPlayer.location.status != "success" || aPlayer.location.IP != aPlayer.player_ip)
                    {
                        //Update IP location
                        QueuePlayerForIPInfoFetch(aPlayer);
                    }

                    if (updatePlayer)
                    {
                        Log.Debug(() => "Queueing existing player " + aPlayer.GetVerboseName() + " for update.", 4);
                        UpdatePlayer(aPlayer);
                        //If using ban enforcer, queue player for update
                        if (_UseBanEnforcer)
                        {
                            QueuePlayerForBanCheck(aPlayer);
                        }
                    }
                }
                Log.Debug(() => "Player slot: " + cpbiPlayer.SlotID, 7);
                Log.Debug(() => "OnPunkbusterPlayerInfo finished!", 7);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured while processing punkbuster info.", e));
            }
        }

        private void FetchAllAccess(Boolean async)
        {
            try
            {
                if (async)
                {
                    _AccessFetchWaitHandle.Set();
                }
                else if (_threadsReady)
                {
                    lock (_userCache)
                    {
                        DateTime start = UtcNow();
                        FetchCommands();
                        Log.Debug(() => "Command fetch took " + (UtcNow() - start).TotalMilliseconds + "ms.", 4);
                        start = UtcNow();
                        FetchRoles();
                        Log.Debug(() => "Role fetch took " + (UtcNow() - start).TotalMilliseconds + "ms.", 4);
                        start = UtcNow();
                        FetchUserList();
                        Log.Debug(() => "User fetch took " + (UtcNow() - start).TotalMilliseconds + "ms.", 4);
                        start = UtcNow();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching all access.", e));
            }
        }

        private void AccessFetchingThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Access Fetching Thread", 1);
                Thread.CurrentThread.Name = "AccessFetching";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering Access Fetching Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        FetchAllAccess(false);

                        Log.Debug(() => "Access fetch waiting for Input.", 5);
                        if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                        {
                            Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                        }
                        _AccessFetchWaitHandle.Reset();
                        _AccessFetchWaitHandle.WaitOne(TimeSpan.FromSeconds(300));
                        loopStart = UtcNow();
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.Warn("Access Fetching thread was force aborted. Exiting.");
                            break;
                        }
                        Log.HandleException(new AException("Error occured in Access Fetching thread. Skipping loop.", e));
                    }
                }
                Log.Debug(() => "Ending Access Fetching Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in Access Fetching thread.", e));
            }
        }

        private void FetchRoundID(Boolean increment)
        {
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT
                                IFNULL(MAX(`round_id`), 0) AS `max_round_id`
                            FROM
                                `tbl_extendedroundstats`
                            WHERE
                                `server_id` = @server_id";
                        command.Parameters.AddWithValue("server_id", _serverInfo.ServerID);
                        using (MySqlDataReader reader = SafeExecuteReader(command))
                        {
                            if (reader.Read())
                            {
                                Int32 oldRoundID = reader.GetInt32("max_round_id");
                                if (increment)
                                {
                                    _roundID = oldRoundID + 1;
                                    Log.Debug(() => "New round. Round ID is " + String.Format("{0:n0}", _roundID), 2);
                                }
                                else
                                {
                                    _roundID = oldRoundID;
                                    Log.Debug(() => "Current round. Round ID is " + String.Format("{0:n0}", _roundID), 2);
                                }
                            }
                            else
                            {
                                _roundID = 1;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching round ID", e));
            }
        }

        private void StartRoundTicketLogger(Int32 startingSeconds)
        {
            try
            {
                if (!_pluginEnabled || !_threadsReady || !_firstPlayerListComplete || _roundID < 1)
                {
                    return;
                }
                Thread roundLoggerThread = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "RoundTicketLogger";
                        Int32 roundTimeSeconds = startingSeconds;
                        ProconChatWrite(Log.FBold("Ticket logging started on round " + String.Format("{0:n0}", _roundID)));

                        Stopwatch watch = new Stopwatch();
                        while (true)
                        {
                            if (!_pluginEnabled)
                            {
                                break;
                            }
                            watch.Reset();
                            watch.Start();
                            if (_roundState == RoundState.Loaded)
                            {
                                Threading.Wait(TimeSpan.FromSeconds(2));
                                continue;
                            }
                            ATeam team1 = _teamDictionary[1];
                            ATeam team2 = _teamDictionary[2];
                            if (_roundState == RoundState.Ended || !_pluginEnabled || GetPlayerCount() <= 1)
                            {
                                break;
                            }

                            using (MySqlConnection connection = GetDatabaseConnection())
                            {
                                using (MySqlCommand command = connection.CreateCommand())
                                {
                                    //Set the insert command structure
                                    command.CommandText = @"
                                    INSERT INTO
                                        `tbl_extendedroundstats`
                                    (
                                        `server_id`,
                                        `round_id`,
                                        `round_elapsedTimeSec`,
                                        `team1_count`,
                                        `team2_count`,
                                        `team1_score`,
                                        `team2_score`,
                                        `team1_spm`,
                                        `team2_spm`,
                                        `team1_tickets`,
                                        `team2_tickets`,
                                        `team1_tpm`,
                                        `team2_tpm`,
                                        `roundstat_time`,
                                        `map`
                                    ) 
                                    VALUES 
                                    (
                                        @server_id,
                                        @round_id,
                                        @round_elapsedTimeSec,
                                        @team1_count,
                                        @team2_count,
                                        @team1_score,
                                        @team2_score,
                                        @team1_spm,
                                        @team2_spm,
                                        @team1_tickets,
                                        @team2_tickets,
                                        @team1_tpm,
                                        @team2_tpm,
                                        UTC_TIMESTAMP(),
                                        @map
                                    )";
                                    command.Parameters.AddWithValue("@server_id", _serverInfo.ServerID);
                                    command.Parameters.AddWithValue("@round_id", _roundID);
                                    command.Parameters.AddWithValue("@round_elapsedTimeSec", roundTimeSeconds);
                                    command.Parameters.AddWithValue("@team1_count", team1.TeamPlayerCount);
                                    command.Parameters.AddWithValue("@team2_count", team2.TeamPlayerCount);
                                    command.Parameters.AddWithValue("@team1_score", Math.Round(team1.TeamTotalScore, 2));
                                    command.Parameters.AddWithValue("@team2_score", Math.Round(team2.TeamTotalScore, 2));
                                    command.Parameters.AddWithValue("@team1_spm", Math.Round(team1.TeamScoreDifferenceRate, 2));
                                    command.Parameters.AddWithValue("@team2_spm", Math.Round(team2.TeamScoreDifferenceRate, 2));
                                    command.Parameters.AddWithValue("@team1_tickets", team1.TeamTicketCount);
                                    command.Parameters.AddWithValue("@team2_tickets", team2.TeamTicketCount);
                                    command.Parameters.AddWithValue("@team1_tpm", Math.Round(team1.GetTicketDifferenceRate(), 2));
                                    command.Parameters.AddWithValue("@team2_tpm", Math.Round(team2.GetTicketDifferenceRate(), 2));
                                    command.Parameters.AddWithValue("@map", _serverInfo.InfoObject.Map);

                                    try
                                    {
                                        //Attempt to execute the query
                                        if (SafeExecuteNonQuery(command) > 0)
                                        {
                                            Log.Debug(() => "round stat pushed to database", 5);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Log.HandleException(new AException("Invalid round stats when posting. " + FormatTimeString(_serverInfo.GetRoundElapsedTime(), 2) + "|" + team1.TeamPlayerCount + "|" + team2.TeamPlayerCount + "|" + Math.Round(team1.TeamTotalScore, 2) + "|" + Math.Round(team2.TeamTotalScore, 2) + "|" + Math.Round(team1.TeamScoreDifferenceRate, 2) + "|" + team1.TeamScoreDifferenceRate + "|" + Math.Round(team2.TeamScoreDifferenceRate, 2) + "|" + team2.TeamScoreDifferenceRate + "|" + team1.TeamTicketCount + "|" + team2.TeamTicketCount + "|" + Math.Round(team1.GetTicketDifferenceRate(), 2) + "|" + team1.GetTicketDifferenceRate() + "|" + Math.Round(team2.GetTicketDifferenceRate(), 2) + "|" + team2.GetTicketDifferenceRate(), e));
                                    }
                                }
                            }

                            watch.Stop();
                            // dynamic wait time, Log every 30s if the server has players else every 10 minutes.
                            int logDelay = 30;
                            if (team1.TeamPlayerCount == 0 && team2.TeamPlayerCount == 0)
                            {
                                logDelay = 600;
                            }

                            if (watch.Elapsed.TotalSeconds < logDelay)
                            {
                                Threading.Wait(TimeSpan.FromSeconds(logDelay) - watch.Elapsed);
                            }
                            roundTimeSeconds += logDelay;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error in round stat logger thread", e));
                    }
                    Threading.StopWatchdog();
                }));

                if (!Threading.IsAlive("RoundTicketLogger"))
                {
                    //Start the thread
                    Threading.StartWatchdog(roundLoggerThread);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while starting round ticket logger", e));
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            Log.Debug(() => "Entering OnServerInfo", 7);
            try
            {
                if (_pluginEnabled)
                {
                    lock (_serverInfo)
                    {
                        if (serverInfo != null)
                        {
                            //Get the server info
                            if (NowDuration(_LastServerInfoReceive).TotalSeconds < 9.5)
                            {
                                return;
                            }
                            _LastServerInfoReceive = UtcNow();
                            _serverInfo.SetInfoObject(serverInfo);
                            if (serverInfo.TeamScores != null)
                            {
                                List<TeamScore> listCurrTeamScore = serverInfo.TeamScores;
                                //During round change, teams don't exist
                                if (listCurrTeamScore.Count > 0)
                                {
                                    foreach (TeamScore score in listCurrTeamScore)
                                    {
                                        ATeam currentTeam;
                                        if (!GetTeamByID(score.TeamID, out currentTeam))
                                        {
                                            if (_roundState == RoundState.Playing)
                                            {
                                                Log.Error("Teams not loaded when they should be.");
                                            }
                                            continue;
                                        }
                                        currentTeam.UpdateTicketCount(score.Score);
                                        currentTeam.UpdateTotalScore(_PlayerDictionary.Values.Where(aPlayer => aPlayer.fbpInfo.TeamID == score.TeamID).Aggregate<APlayer, double>(0, (current, aPlayer) => current + aPlayer.fbpInfo.Score));
                                    }
                                }
                                else
                                {
                                    Log.Debug(() => "Server info fired while changing rounds, no teams to parse.", 5);
                                }
                            }
                            ATeam team1, team2;
                            if (!GetTeamByID(1, out team1))
                            {
                                if (_roundState == RoundState.Playing)
                                {
                                    Log.Error("Teams not loaded when they should be.");
                                }
                                return;
                            }
                            if (!GetTeamByID(2, out team2))
                            {
                                if (_roundState == RoundState.Playing)
                                {
                                    Log.Error("Teams not loaded when they should be.");
                                }
                                return;
                            }
                            ATeam winningTeam = null;
                            ATeam losingTeam = null;
                            ATeam mapUpTeam = null;
                            ATeam mapDownTeam = null;
                            ATeam baserapingTeam = null;
                            ATeam baserapedTeam = null;
                            if (team1.TeamTicketCount > team2.TeamTicketCount)
                            {
                                winningTeam = team1;
                                losingTeam = team2;
                            }
                            else
                            {
                                winningTeam = team2;
                                losingTeam = team1;
                            }
                            if (team1.GetTicketDifferenceRate() > team2.GetTicketDifferenceRate())
                            {
                                //Team1 has more map than Team2
                                mapUpTeam = team1;
                                mapDownTeam = team2;
                            }
                            else
                            {
                                //Team2 has more map than Team1
                                mapUpTeam = team2;
                                mapDownTeam = team1;
                            }
                            if (_DisplayTicketRatesInProconChat &&
                                _roundState == RoundState.Playing &&
                                GetPlayerCount() >= 4 &&
                                team1.TeamTicketCount != _startingTicketCount &&
                                team2.TeamTicketCount != _startingTicketCount)
                            {
                                String flagMessage = "";
                                String winMessage = "";
                                if (_serverInfo.InfoObject.GameMode == "ConquestLarge0" ||
                                    _serverInfo.InfoObject.GameMode == "Chainlink0" ||
                                    _serverInfo.InfoObject.GameMode == "Domination0")
                                {
                                    Double winRate = mapUpTeam.GetTicketDifferenceRate();
                                    Double loseRate = mapDownTeam.GetTicketDifferenceRate();
                                    if (_serverInfo.InfoObject.GameMode == "ConquestLarge0" && GameVersion == GameVersionEnum.BF4)
                                    {
                                        Int32 maxFlags = Int32.MaxValue;
                                        switch (_serverInfo.InfoObject.Map)
                                        {
                                            case "XP0_Metro":
                                                maxFlags = 3;
                                                break;
                                            case "MP_Prison":
                                                maxFlags = 5;
                                                break;
                                        }
                                        if ((UtcNow() - _AdKatsRunningTime).TotalMinutes > 2.5 && _firstPlayerListComplete)
                                        {
                                            if (winRate > -20 && loseRate > -20)
                                            {
                                                flagMessage = " | Flags equal, ";
                                            }
                                            else if (loseRate <= -20 && loseRate > -34)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 1 flag, ";
                                            }
                                            else if (loseRate <= -34 && loseRate > -38)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 1-3 flags, ";
                                            }
                                            else if (loseRate <= -38 && loseRate > -44 || maxFlags == 3)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 3 flags, ";
                                            }
                                            else if (loseRate <= -44 && loseRate > -48)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 3-5 flags, ";
                                            }
                                            else if (loseRate <= -48 && loseRate > -54 || maxFlags == 5)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 5 flags, ";
                                            }
                                            else if (loseRate <= -54 && loseRate > -58)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 5-7 flags, ";
                                            }
                                            else if (loseRate <= -58 && loseRate > -64 || maxFlags == 7)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 7 flags, ";
                                            }
                                            else if (loseRate <= -64 && loseRate > -68)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 7-9 flags, ";
                                            }
                                            else if (loseRate <= -68 && loseRate > -74 || maxFlags == 9)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up 9 flags, ";
                                            }
                                            else if (loseRate < -74)
                                            {
                                                flagMessage = " | " + mapUpTeam.GetTeamIDKey() + " up many flags, ";
                                            }
                                            double t1t = team1.TeamAdjustedTicketAccellerationRate - team2.TeamAdjustedTicketAccellerationRate;
                                            double t2t = team2.TeamAdjustedTicketAccellerationRate - team1.TeamAdjustedTicketAccellerationRate;
                                            if (Math.Abs(t1t - t2t) < 10)
                                            {
                                                flagMessage += "not changing.";
                                            }
                                            else if (t1t > t2t)
                                            {
                                                flagMessage += team1.GetTeamIDKey() + " gaining ground.";
                                            }
                                            else
                                            {
                                                flagMessage += team2.GetTeamIDKey() + " gaining ground.";
                                            }
                                        }
                                        else
                                        {
                                            flagMessage = " | Calculating flag state.";
                                        }
                                    }
                                    else
                                    {
                                        flagMessage = " | " + _serverInfo.InfoObject.GameMode;
                                    }
                                    var t1RawRate = team1.GetRawTicketDifferenceRate();
                                    var t2RawRate = team2.GetRawTicketDifferenceRate();
                                    if (t1RawRate < 0 && t2RawRate < 0)
                                    {
                                        var t1Duration = TimeSpan.FromMinutes(team1.TeamTicketCount / Math.Abs(t1RawRate));
                                        var t2Duration = TimeSpan.FromMinutes(team2.TeamTicketCount / Math.Abs(t2RawRate));
                                        if (Math.Abs((t1Duration - t2Duration).TotalSeconds) < 60)
                                        {
                                            winMessage = " | Unsure of winning team.";
                                        }
                                        else if (t1Duration < t2Duration)
                                        {
                                            winMessage = " | " + team2.GetTeamIDKey() + " wins in " + FormatTimeString(t1Duration, 2) + ".";
                                        }
                                        else
                                        {
                                            winMessage = " | " + team1.GetTeamIDKey() + " wins in " + FormatTimeString(t2Duration, 2) + ".";
                                        }
                                    }
                                }
                                if ((UtcNow() - _LastTicketRateDisplay).TotalSeconds > 55 || _currentFlagMessage != flagMessage)
                                {
                                    _LastTicketRateDisplay = UtcNow();
                                    _currentFlagMessage = flagMessage;
                                    ProconChatWrite(Log.FBold(team1.TeamKey + " Rate: " + Math.Round(team1.GetTicketDifferenceRate(), 1) + " t/m | " + team2.TeamKey + " Rate: " + Math.Round(team2.GetTicketDifferenceRate(), 1) + " t/m" + flagMessage + winMessage));
                                }
                            }

                            if (team1.TeamTicketCount >= 0 && team2.TeamTicketCount >= 0)
                            {
                                _lowestTicketCount = Math.Min(team1.TeamTicketCount, team2.TeamTicketCount);
                                _highestTicketCount = Math.Max(team1.TeamTicketCount, team2.TeamTicketCount);
                            }

                            //Auto-Surrender System
                            if (_surrenderAutoEnable &&
                                _roundState == RoundState.Playing &&
                                !EventActive() &&
                                !_endingRound &&
                                (UtcNow() - _lastAutoSurrenderTriggerTime).TotalSeconds > 9.0 &&
                                _serverInfo.GetRoundElapsedTime().TotalSeconds > 60 &&
                                (UtcNow() - _AdKatsRunningTime).TotalMinutes > 2.5 &&
                                _firstPlayerListComplete &&
                                //Block system if all possible actions have already taken place this round
                                (getNukeCount(mapUpTeam.TeamID) < _surrenderAutoMaxNukesEachRound || _surrenderAutoNukeResolveAfterMax) &&
                                //Block system while a nuke is active
                                NowDuration(_lastNukeTime).TotalSeconds > _surrenderAutoNukeDurationHigh)
                            {
                                Boolean canFire = true;
                                Boolean fired = false;
                                Int32 denyReasonModulo = 1;
                                String denyReason = "Unknown reason";
                                String readyPercentage = "";

                                //Action
                                AutoSurrenderAction config_action = AutoSurrenderAction.None;
                                if (_surrenderAutoNukeInstead)
                                {
                                    if (getNukeCount(mapUpTeam.TeamID) < _surrenderAutoMaxNukesEachRound)
                                    {
                                        config_action = AutoSurrenderAction.Nuke;
                                    }
                                    else if (_surrenderAutoNukeResolveAfterMax)
                                    {
                                        config_action = AutoSurrenderAction.Surrender;
                                    }
                                    else
                                    {
                                        config_action = AutoSurrenderAction.None;
                                    }
                                }
                                else if (_surrenderAutoTriggerVote)
                                {
                                    config_action = AutoSurrenderAction.Vote;
                                }
                                else
                                {
                                    config_action = AutoSurrenderAction.Surrender;
                                }

                                if (config_action != AutoSurrenderAction.None)
                                {
                                    //State
                                    Boolean config_resumed = _surrenderAutoTriggerCountCurrent > 0 && _surrenderAutoTriggerCountCurrent == _surrenderAutoTriggerCountPause;

                                    //Tickets
                                    Int32 config_tickets_min = 0;
                                    Int32 config_tickets_max = 9999;
                                    Int32 config_tickets_gap_min = 0;

                                    //Rates
                                    Double config_mapUp_rate_max = 0;
                                    Double config_mapUp_rate_min = 0;
                                    Double config_mapDown_rate_max = 0;
                                    Double config_mapDown_rate_min = 0;

                                    //Triggers
                                    Int32 config_triggers_min = 0;

                                    //Set automatic values for metro 2014
                                    if (_surrenderAutoUseMetroValues)
                                    {
                                        //Tickets
                                        config_tickets_min = _surrenderAutoMinimumTicketCount;
                                        config_tickets_max = _surrenderAutoMaximumTicketCount;
                                        config_tickets_gap_min = _surrenderAutoMinimumTicketGap;

                                        //Rates
                                        config_mapDown_rate_max = -42;
                                        config_mapDown_rate_min = -1000;
                                        config_mapUp_rate_max = 1000;
                                        config_mapUp_rate_min = -5;

                                        //Triggers
                                        if (config_action == AutoSurrenderAction.Surrender)
                                        {
                                            config_triggers_min = 20;
                                            //Add modification based on ticket count
                                            if (losingTeam.TeamTicketCount <= 600)
                                            {
                                                config_triggers_min -= (600 - losingTeam.TeamTicketCount) / 30;
                                            }
                                            //Add modification based on automatic assist
                                            if (_PlayersAutoAssistedThisRound)
                                            {
                                                config_triggers_min *= 2;
                                            }
                                        }
                                        else
                                        {
                                            config_triggers_min = 4;
                                        }
                                    }
                                    //Set automatic values for operation locker
                                    else if (_surrenderAutoUseLockerValues)
                                    {
                                        //Tickets
                                        config_tickets_min = _surrenderAutoMinimumTicketCount;
                                        config_tickets_max = _surrenderAutoMaximumTicketCount;
                                        config_tickets_gap_min = _surrenderAutoMinimumTicketGap;

                                        //Rates
                                        config_mapDown_rate_max = -50;
                                        config_mapDown_rate_min = -1000;
                                        config_mapUp_rate_max = 1000;
                                        config_mapUp_rate_min = -5;

                                        //Triggers
                                        if (config_action == AutoSurrenderAction.Surrender)
                                        {
                                            config_triggers_min = 20;
                                            //Add modification based on ticket count
                                            if (losingTeam.TeamTicketCount <= 600)
                                            {
                                                config_triggers_min -= (600 - losingTeam.TeamTicketCount) / 30;
                                            }
                                            //Add modification based on automatic assist
                                            if (_PlayersAutoAssistedThisRound)
                                            {
                                                config_triggers_min *= 2;
                                            }
                                        }
                                        else
                                        {
                                            config_triggers_min = 4;
                                        }
                                    }
                                    //Set custom values based on the user
                                    else
                                    {
                                        //Tickets
                                        config_tickets_min = _surrenderAutoMinimumTicketCount;
                                        config_tickets_max = _surrenderAutoMaximumTicketCount;
                                        config_tickets_gap_min = _surrenderAutoMinimumTicketGap;

                                        //Rates
                                        config_mapDown_rate_max = _surrenderAutoLosingRateMax;
                                        config_mapDown_rate_min = _surrenderAutoLosingRateMin;
                                        config_mapUp_rate_max = _surrenderAutoWinningRateMax;
                                        config_mapUp_rate_min = _surrenderAutoWinningRateMin;

                                        //Triggers
                                        config_triggers_min = _surrenderAutoTriggerCountToSurrender;
                                    }

                                    //Add modification based on population
                                    if (config_action == AutoSurrenderAction.Nuke &&
                                        config_triggers_min < 5 &&
                                        (_populationStatus == PopulationState.Low || _populationStatus == PopulationState.Medium))
                                    {
                                        config_triggers_min = 5;
                                    }

                                    int playerCount = GetPlayerCount();
                                    int neededPlayers = Math.Max(_surrenderAutoMinimumPlayers - playerCount, 0);
                                    var ticketGap = Math.Abs(winningTeam.TeamTicketCount - losingTeam.TeamTicketCount);

                                    if (canFire &&
                                        neededPlayers > 0)
                                    {
                                        canFire = false;
                                        denyReason = neededPlayers + " more players needed.";
                                    }

                                    var downRate = mapDownTeam.GetTicketDifferenceRate();
                                    var upRate = mapUpTeam.GetTicketDifferenceRate();

                                    var validRateWindow = downRate <= config_mapDown_rate_max &&
                                                          downRate >= config_mapDown_rate_min &&
                                                          upRate <= config_mapUp_rate_max &&
                                                          upRate >= config_mapUp_rate_min;
                                    var validTicketBasedNuke = config_action == AutoSurrenderAction.Nuke &&
                                                               mapUpTeam == winningTeam &&
                                                               ticketGap > _NukeWinningTeamUpTicketCount &&
                                                               (!_NukeWinningTeamUpTicketHigh || _populationStatus == PopulationState.High) &&
                                                               getNukeCount(mapUpTeam.TeamID) < 1;
                                    var validTeams = config_action == AutoSurrenderAction.Nuke ||
                                                     winningTeam == mapUpTeam;
                                    if ((validRateWindow || validTicketBasedNuke) &&
                                        validTeams)
                                    {
                                        //Fire triggers
                                        _lastAutoSurrenderTriggerTime = UtcNow();
                                        _surrenderAutoTriggerCountCurrent++;

                                        readyPercentage = Math.Round(Math.Min((_surrenderAutoTriggerCountCurrent / (Double)config_triggers_min) * 100.0, 100)) + "%";

                                        if (canFire &&
                                            config_action == AutoSurrenderAction.Nuke &&
                                            getNukeCount(mapUpTeam.TeamID) > 0 &&
                                            NowDuration(_lastNukeTime).TotalSeconds < _surrenderAutoNukeMinBetween)
                                        {
                                            canFire = false;
                                            denyReason = "~" + FormatNowDuration(_lastNukeTime.AddSeconds(_surrenderAutoNukeMinBetween), 2) + " till it can fire again.";
                                        }

                                        if (canFire &&
                                            _surrenderAutoTriggerCountCurrent < config_triggers_min)
                                        {
                                            canFire = false;
                                            TimeSpan remaining = TimeSpan.FromSeconds((config_triggers_min - _surrenderAutoTriggerCountCurrent) * 10);
                                            denyReason = "~" + FormatTimeString(remaining, 2) + " till it can fire.";
                                        }
                                        if (canFire &&
                                            config_action == AutoSurrenderAction.Nuke &&
                                            mapUpTeam != winningTeam)
                                        {
                                            //Losing team is the one with all flags capped
                                            if (_surrenderAutoNukeLosingTeams)
                                            {
                                                if (ticketGap > _surrenderAutoNukeLosingMaxDiff)
                                                {
                                                    canFire = false;
                                                    denyReasonModulo = 3;
                                                    denyReason = mapUpTeam.TeamKey + " losing by more than " + _surrenderAutoNukeLosingMaxDiff + " tickets.";
                                                }
                                            }
                                            else
                                            {
                                                canFire = false;
                                                denyReasonModulo = 3;
                                                denyReason = mapUpTeam.TeamKey + " is losing.";
                                            }
                                        }

                                        if (canFire && winningTeam.TeamTicketCount > config_tickets_max)
                                        {
                                            canFire = false;
                                            denyReasonModulo = 2;
                                            denyReason = winningTeam.TeamKey + " has more than " + config_tickets_max + " tickets. (" + winningTeam.TeamTicketCount + ")";
                                        }

                                        if (canFire && losingTeam.TeamTicketCount < config_tickets_min)
                                        {
                                            canFire = false;
                                            denyReasonModulo = 2;
                                            denyReason = losingTeam.TeamKey + " has less than " + config_tickets_min + " tickets. (" + losingTeam.TeamTicketCount + ")";
                                        }

                                        if (canFire && ticketGap < config_tickets_gap_min)
                                        {
                                            canFire = false;
                                            denyReasonModulo = 2;
                                            denyReason = "Less than " + config_tickets_gap_min + " tickets between teams. (" + ticketGap + ")";
                                        }

                                        if (canFire)
                                        {
                                            fired = true;
                                            switch (config_action)
                                            {
                                                case AutoSurrenderAction.Surrender:
                                                    baserapingTeam = winningTeam;
                                                    baserapedTeam = losingTeam;
                                                    break;
                                                case AutoSurrenderAction.Nuke:
                                                    if (_surrenderAutoNukeLosingTeams)
                                                    {
                                                        baserapingTeam = mapUpTeam;
                                                        baserapedTeam = mapDownTeam;
                                                    }
                                                    else
                                                    {
                                                        baserapingTeam = winningTeam;
                                                        baserapedTeam = losingTeam;
                                                    }
                                                    break;
                                                case AutoSurrenderAction.Vote:
                                                    baserapingTeam = winningTeam;
                                                    baserapedTeam = losingTeam;
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            if (config_resumed)
                                            {
                                                if (config_action == AutoSurrenderAction.Nuke)
                                                {
                                                    if (_surrenderAutoAnnounceNukePrep)
                                                    {
                                                        AdminSayMessage("Auto-nuke countdown resumed at " + readyPercentage + ". " + denyReason);
                                                    }
                                                }
                                                else
                                                {
                                                    OnlineAdminSayMessage("Auto-surrender countdown resumed at " + readyPercentage + ". " + denyReason);
                                                }
                                            }
                                            //How often the message should be displayed
                                            else if ((_surrenderAutoTriggerCountCurrent == 1 ||
                                                     _surrenderAutoTriggerCountCurrent % 3 == 0 ||
                                                     (config_action == AutoSurrenderAction.Nuke && neededPlayers <= 10) ||
                                                     _surrenderAutoTriggerVote)
                                                     // Only show the nuke messages for rounds of 4 or more players
                                                     && playerCount >= 4)
                                            {
                                                if (_surrenderAutoTriggerCountCurrent < config_triggers_min)
                                                {
                                                    if (config_action == AutoSurrenderAction.Nuke)
                                                    {
                                                        if (_surrenderAutoAnnounceNukePrep && (mapUpTeam.TeamID == winningTeam.TeamID || _surrenderAutoNukeLosingTeams))
                                                        {
                                                            AdminSayMessage(mapUpTeam.TeamKey + " auto-nuke " + (getNukeCount(mapUpTeam.TeamID) + 1) + " " + readyPercentage + " ready. " + denyReason);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        OnlineAdminSayMessage("Auto-surrender " + readyPercentage + " ready. " + denyReason);
                                                    }
                                                }
                                                else
                                                {
                                                    if (config_action == AutoSurrenderAction.Nuke)
                                                    {
                                                        if (_surrenderAutoAnnounceNukePrep && _surrenderAutoTriggerCountCurrent % denyReasonModulo == 0)
                                                        {
                                                            AdminSayMessage(mapUpTeam.TeamKey + " auto-nuke " + (getNukeCount(mapUpTeam.TeamID) + 1) + " ready and waiting. " + denyReason);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        OnlineAdminSayMessage("Auto-surrender ready and waiting. " + denyReason);
                                                    }
                                                }
                                            }

                                            if (!_Team1MoveQueue.Any() &&
                                                !_Team2MoveQueue.Any() &&
                                                _serverInfo.GetRoundElapsedTime().TotalSeconds > 120)
                                            {
                                                Dictionary<String, APlayer> auaPlayers = new Dictionary<String, APlayer>();
                                                //Get players from the auto-assist blacklist
                                                foreach (APlayer aPlayer in GetOnlinePlayersOfGroup("blacklist_autoassist").Where(aPlayer =>
                                                    aPlayer.fbpInfo.TeamID == winningTeam.TeamID))
                                                {
                                                    if (!auaPlayers.ContainsKey(aPlayer.player_name))
                                                    {
                                                        auaPlayers[aPlayer.player_name] = aPlayer;
                                                    }
                                                }
                                                foreach (APlayer aPlayer in auaPlayers.Values)
                                                {
                                                    if (PlayerIsAdmin(aPlayer))
                                                    {
                                                        continue;
                                                    }
                                                    OnlineAdminSayMessage(aPlayer.GetVerboseName() + " being automatically assisted to weak team.");
                                                    PlayerTellMessage(aPlayer.player_name, Log.CViolet("You are being automatically assisted to the weak team."));
                                                    Thread.Sleep(2000);
                                                    _PlayersAutoAssistedThisRound = true;
                                                    QueueRecordForProcessing(new ARecord
                                                    {
                                                        record_source = ARecord.Sources.Automated,
                                                        server_id = _serverInfo.ServerID,
                                                        command_type = GetCommandByKey("self_assist"),
                                                        command_action = GetCommandByKey("self_assist_unconfirmed"),
                                                        target_name = aPlayer.player_name,
                                                        target_player = aPlayer,
                                                        source_name = "AUAManager",
                                                        record_message = "Assist Weak Team [" + winningTeam.TeamTicketCount + ":" + losingTeam.TeamTicketCount + "][" + FormatTimeString(_serverInfo.GetRoundElapsedTime(), 3) + "]",
                                                        record_time = UtcNow()
                                                    });
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //Server is outside of auto-surrender window, send update messages if needed
                                        readyPercentage = Math.Round((Math.Min(_surrenderAutoTriggerCountCurrent, config_triggers_min) / (Double)config_triggers_min) * 100.0) + "%";
                                        if (_surrenderAutoResetTriggerCountOnCancel)
                                        {
                                            if (_surrenderAutoTriggerCountCurrent > 0)
                                            {
                                                if (config_action == AutoSurrenderAction.Nuke)
                                                {
                                                    if (_surrenderAutoAnnounceNukePrep)
                                                    {
                                                        AdminSayMessage("Auto-nuke countdown cancelled.");
                                                    }
                                                }
                                                else
                                                {
                                                    OnlineAdminSayMessage("Auto-surrender countdown cancelled.");
                                                }
                                            }
                                            _surrenderAutoTriggerCountCurrent = 0;
                                        }
                                        else
                                        {
                                            if (_surrenderAutoTriggerCountCurrent > 0 && _surrenderAutoTriggerCountCurrent != _surrenderAutoTriggerCountPause)
                                            {
                                                _surrenderAutoTriggerCountPause = _surrenderAutoTriggerCountCurrent;
                                                if (_surrenderAutoTriggerCountCurrent < config_triggers_min)
                                                {
                                                    if (config_action == AutoSurrenderAction.Nuke)
                                                    {
                                                        if (_surrenderAutoAnnounceNukePrep)
                                                        {
                                                            AdminSayMessage("Auto-nuke countdown paused at " + readyPercentage + ".");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        OnlineAdminSayMessage("Auto-surrender countdown paused at " + readyPercentage + ".");
                                                    }
                                                }
                                                else
                                                {
                                                    if (config_action == AutoSurrenderAction.Nuke)
                                                    {
                                                        if (_surrenderAutoAnnounceNukePrep)
                                                        {
                                                            AdminSayMessage("Auto-nuke countdown paused.");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        OnlineAdminSayMessage("Auto-surrender countdown paused.");
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (fired)
                                    {
                                        if (_surrenderAutoResetTriggerCountOnFire)
                                        {
                                            _surrenderAutoTriggerCountCurrent = 0;
                                            _surrenderAutoTriggerCountPause = 0;
                                        }
                                        if (config_action == AutoSurrenderAction.Nuke)
                                        {
                                            string autoNukeMessage = _surrenderAutoNukeMessage.Replace("%WinnerName%", baserapingTeam.TeamName);
                                            QueueRecordForProcessing(new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                server_id = _serverInfo.ServerID,
                                                command_type = GetCommandByKey("server_nuke"),
                                                command_numeric = baserapingTeam.TeamID,
                                                target_name = baserapingTeam.TeamName,
                                                source_name = "RoundManager",
                                                record_message = autoNukeMessage,
                                                record_time = UtcNow()
                                            });
                                        }
                                        else if (_surrenderAutoTriggerVote)
                                        {
                                            QueueRecordForProcessing(new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                server_id = _serverInfo.ServerID,
                                                command_type = GetCommandByKey("self_votenext"),
                                                command_numeric = 0,
                                                target_name = "RoundManager",
                                                source_name = "RoundManager",
                                                record_message = "Auto-Starting Surrender Vote",
                                                record_time = UtcNow()
                                            });
                                        }
                                        else if (!_endingRound)
                                        {
                                            _endingRound = true;
                                            _surrenderAutoSucceeded = true;
                                            Thread roundEndDelayThread = new Thread(new ThreadStart(delegate
                                            {
                                                Log.Debug(() => "Starting a round end delay thread.", 5);
                                                try
                                                {
                                                    Thread.CurrentThread.Name = "RoundEndDelay";
                                                    string autoSurrenderMessage = _surrenderAutoMessage.Replace("%WinnerName%", baserapingTeam.TeamName);
                                                    for (int i = 0; i < 8; i++)
                                                    {
                                                        AdminTellMessage(autoSurrenderMessage);
                                                        Thread.Sleep(50);
                                                    }
                                                    Threading.Wait(1000 * _YellDuration);
                                                    ARecord repRecord = new ARecord
                                                    {
                                                        record_source = ARecord.Sources.Automated,
                                                        server_id = _serverInfo.ServerID,
                                                        command_type = GetCommandByKey("round_end"),
                                                        command_numeric = baserapingTeam.TeamID,
                                                        target_name = baserapingTeam.TeamName,
                                                        source_name = "RoundManager",
                                                        record_message = "Auto-Surrender (" + baserapingTeam.GetTeamIDKey() + " Win)(" + baserapingTeam.TeamTicketCount + ":" + baserapedTeam.TeamTicketCount + ")(" + FormatTimeString(_serverInfo.GetRoundElapsedTime(), 2) + ")",
                                                        record_time = UtcNow()
                                                    };
                                                    QueueRecordForProcessing(repRecord);
                                                }
                                                catch (Exception)
                                                {
                                                    Log.HandleException(new AException("Error while running round end delay."));
                                                }
                                                Log.Debug(() => "Exiting a round end delay thread.", 5);
                                                Threading.StopWatchdog();
                                            }));
                                            Threading.StartWatchdog(roundEndDelayThread);
                                        }
                                    }
                                }
                            }

                            _serverInfo.ServerName = serverInfo.ServerName;

                            if (!_pluginUpdateServerInfoChecked)
                            {
                                _pluginUpdateServerInfoChecked = true;
                                CheckForPluginUpdates(false);
                            }
                            FeedStatLoggerSettings();
                        }
                        else
                        {
                            Log.HandleException(new AException("Server info was null"));
                        }
                        _ServerInfoWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while processing server info.", e));
            }
            Log.Debug(() => "Exiting OnServerInfo", 7);
        }

        public override void OnSoldierHealth(Int32 limit)
        {
            _soldierHealth = limit;
        }

        public void PostAndResetMapBenefitStatistics()
        {
            Log.Debug(() => "Entering PostAndResetMapBenefitStatistics", 7);
            try
            {
                if (_PostMapBenefitStatistics && _serverInfo != null && _serverInfo.InfoObject != null)
                {
                    Int32 roundID = _roundID;
                    String mapName = _serverInfo.InfoObject.Map;
                    if (roundID > 0 && !String.IsNullOrEmpty(mapName))
                    {
                        QueueStatisticForProcessing(new AStatistic()
                        {
                            stat_type = AStatistic.StatisticType.map_detriment,
                            server_id = _serverInfo.ServerID,
                            round_id = _roundID,
                            target_name = mapName,
                            stat_value = _mapDetrimentIndex,
                            stat_comment = _mapDetrimentIndex + " players left because of " + mapName,
                            stat_time = UtcNow()
                        });
                        QueueStatisticForProcessing(new AStatistic()
                        {
                            stat_type = AStatistic.StatisticType.map_benefit,
                            server_id = _serverInfo.ServerID,
                            round_id = _roundID,
                            target_name = mapName,
                            stat_value = _mapBenefitIndex,
                            stat_comment = _mapBenefitIndex + " players joined because of " + mapName,
                            stat_time = UtcNow()
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while preparing map stats for upload", e));
            }
            _mapDetrimentIndex = 0;
            _mapBenefitIndex = 0;
            Log.Debug(() => "Exiting PostAndResetMapBenefitStatistics", 7);
        }

        public void PostRoundStatistics(ATeam winningTeam, ATeam losingTeam)
        {
            Log.Debug(() => "Entering PostRoundStatistics", 7);
            try
            {
                List<APlayer> OrderedPlayers = _PlayerDictionary.Values
                    .Where(aPlayer => aPlayer.player_type == PlayerType.Player).ToList();
                // Do not use their stored power, since that would skew the numbers
                OrderedPlayers = OrderedPlayers.OrderByDescending(aPlayer => aPlayer.GetPower(false, true, false)).ToList();
                List<APlayer> WinningPlayers = OrderedPlayers
                    .Where(aPlayer => aPlayer.fbpInfo.TeamID == winningTeam.TeamID).ToList();
                List<APlayer> LosingPlayers = OrderedPlayers
                    .Where(aPlayer => aPlayer.fbpInfo.TeamID == losingTeam.TeamID).ToList();
                foreach (APlayer aPlayer in WinningPlayers)
                {
                    QueueStatisticForProcessing(new AStatistic()
                    {
                        stat_type = AStatistic.StatisticType.player_win,
                        server_id = _serverInfo.ServerID,
                        round_id = _roundID,
                        target_name = aPlayer.player_name,
                        target_player = aPlayer,
                        stat_value = aPlayer.fbpInfo.SquadID,
                        stat_comment = aPlayer.player_name + " won",
                        stat_time = UtcNow()
                    });
                }
                foreach (APlayer aPlayer in LosingPlayers)
                {
                    QueueStatisticForProcessing(new AStatistic()
                    {
                        stat_type = AStatistic.StatisticType.player_loss,
                        server_id = _serverInfo.ServerID,
                        round_id = _roundID,
                        target_name = aPlayer.player_name,
                        target_player = aPlayer,
                        stat_value = aPlayer.fbpInfo.SquadID,
                        stat_comment = aPlayer.player_name + " lost",
                        stat_time = UtcNow()
                    });
                }
                var TopOrdered = OrderedPlayers.Take((Int32)(OrderedPlayers.Count / 3.75)).ToList();
                foreach (APlayer aPlayer in TopOrdered)
                {
                    QueueStatisticForProcessing(new AStatistic()
                    {
                        stat_type = AStatistic.StatisticType.player_top,
                        server_id = _serverInfo.ServerID,
                        round_id = _roundID,
                        target_name = aPlayer.player_name,
                        target_player = aPlayer,
                        stat_value = aPlayer.fbpInfo.SquadID,
                        stat_comment = aPlayer.player_name + " top player in position " + (WinningPlayers.IndexOf(aPlayer) + 1),
                        stat_time = UtcNow()
                    });
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while preparing round stats for upload", e));
            }
            Log.Debug(() => "Exiting PostRoundStatistics", 7);
        }

        public override void OnLevelLoaded(String strMapFileName, String strMapMode, Int32 roundsPlayed, Int32 roundsTotal)
        {
            Log.Debug(() => "Entering OnLevelLoaded", 7);
            try
            {
                if (_pluginEnabled)
                {
                    if (_LevelLoadShutdown)
                    {
                        Environment.Exit(2232);
                    }
                    //Upload map benefit/detriment statistics
                    PostAndResetMapBenefitStatistics();
                    //Change round state
                    _roundState = RoundState.Loaded;
                    //Request new server info
                    DoServerInfoTrigger();
                    //Completely clear all round-specific data
                    _endingRound = false;
                    _surrenderVoteList.Clear();
                    _nosurrenderVoteList.Clear();
                    _surrenderVoteActive = false;
                    _surrenderVoteSucceeded = false;
                    _surrenderAutoSucceeded = false;
                    _surrenderAutoTriggerCountCurrent = 0;
                    _surrenderAutoTriggerCountPause = 0;
                    _nukesThisRound.Clear();
                    _lastNukeTeam = null;
                    _roundAssists.Clear();
                    _PlayersAutoAssistedThisRound = false;
                    _RoundMutedPlayers.Clear();
                    _ActionConfirmDic.Clear();
                    _ActOnSpawnDictionary.Clear();
                    _ActOnIsAliveDictionary.Clear();
                    _TeamswapOnDeathMoveDic.Clear();
                    _Team1MoveQueue.Clear();
                    _Team2MoveQueue.Clear();
                    lock (_AssistAttemptQueue)
                    {
                        _AssistAttemptQueue.Clear();
                    }
                    _RoundCookers.Clear();
                    _unmatchedRoundDeathCounts.Clear();
                    _unmatchedRoundDeaths.Clear();
                    //Update the factions
                    UpdateFactions();
                    getMapInfo();
                    StartRoundTicketLogger(0);
                    if (ChallengeManager != null)
                    {
                        ChallengeManager.OnRoundLoaded(_roundID);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling level load.", e));
            }
            Log.Debug(() => "Exiting OnLevelLoaded", 7);
        }

        //Round ended stuff
        public override void OnRoundOverPlayers(List<CPlayerInfo> players)
        {
            _roundOverPlayers = players;
        }

        private void ProcessPresetHardcore()
        {
            var delayMS = 250;
            //ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.friendlyFire", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.killCam", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.miniMap", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.nameTag", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.regenerateHealth", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.hud", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.onlySquadLeaderSpawn", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.3dSpotting", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.3pCam", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.hud", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.soldierHealth", "60");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.hitIndicatorsEnabled", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.forceReloadWholeMags", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.preset", "HARDCORE", "false");
        }

        private void ProcessPresetNormal()
        {
            var delayMS = 250;
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.friendlyFire", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.killCam", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.miniMap", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.nameTag", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.regenerateHealth", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.hud", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.onlySquadLeaderSpawn", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.3dSpotting", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.3pCam", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.hud", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.soldierHealth", "100");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.hitIndicatorsEnabled", "true");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.forceReloadWholeMags", "false");
            ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.preset", "NORMAL", "false");
        }

        private void ProcessEventMapMode(Int32 eventRoundNumber)
        {
            ProcessEventMapMode(GetEventRoundMapCode(eventRoundNumber), GetEventRoundModeCode(eventRoundNumber));
        }

        private void ProcessEventMapMode(AEventOption.MapCode mapCode, AEventOption.ModeCode modeCode)
        {
            var delayMS = 250;
            Log.Debug(() => "Entering ProcessEventMapMode", 7);
            try
            {
                var mapFile = "XP0_Metro";
                switch (mapCode)
                {
                    case AEventOption.MapCode.MET:
                        mapFile = "XP0_Metro";
                        break;
                    case AEventOption.MapCode.LOC:
                        mapFile = "MP_Prison";
                        break;
                }
                switch (modeCode)
                {
                    case AEventOption.ModeCode.UNKNOWN:
                        Int32 GoalTickets = 0;
                        Double TicketRatio = 0;
                        Int32 GMC = 0;
                        break;
                    case AEventOption.ModeCode.T100:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "TeamDeathMatch0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "100");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.T200:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "TeamDeathMatch0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "200");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.T300:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "TeamDeathMatch0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "300");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.T400:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "TeamDeathMatch0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "400");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.R200:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "RushLarge0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "200");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.R300:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "RushLarge0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "300");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.R400:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "RushLarge0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "400");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.C500:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "ConquestLarge0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        GoalTickets = 500;
                        TicketRatio = 800 / 100.0;
                        GMC = (Int32)Math.Ceiling(GoalTickets / TicketRatio);
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", GMC.ToString());
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.C1000:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "ConquestLarge0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        GoalTickets = 1000;
                        TicketRatio = 800 / 100.0;
                        GMC = (Int32)Math.Ceiling(GoalTickets / TicketRatio);
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", GMC.ToString());
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.C2000:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "ConquestLarge0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        GoalTickets = 2000;
                        TicketRatio = 800 / 100.0;
                        GMC = (Int32)Math.Ceiling(GoalTickets / TicketRatio);
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", GMC.ToString());
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.F9:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "CaptureTheFlag0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "300");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.F6:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "CaptureTheFlag0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "200");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.F3:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "CaptureTheFlag0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", "100");
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.D500:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "Domination0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        GoalTickets = 500;
                        TicketRatio = 300 / 100.0;
                        GMC = (Int32)Math.Ceiling(GoalTickets / TicketRatio);
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", GMC.ToString());
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.HD500:
                        ProcessPresetHardcore();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "Domination0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        GoalTickets = 500;
                        TicketRatio = 300 / 100.0;
                        GMC = (Int32)Math.Ceiling(GoalTickets / TicketRatio);
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", GMC.ToString());
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.D750:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "Domination0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        GoalTickets = 750;
                        TicketRatio = 300 / 100.0;
                        GMC = (Int32)Math.Ceiling(GoalTickets / TicketRatio);
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", GMC.ToString());
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.D1000:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "100");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", mapFile, "Domination0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        GoalTickets = 1000;
                        TicketRatio = 300 / 100.0;
                        GMC = (Int32)Math.Ceiling(GoalTickets / TicketRatio);
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", GMC.ToString());
                        Log.Info("Event round setup complete!");
                        OnlineAdminSayMessage("Event round setup complete!");
                        break;
                    case AEventOption.ModeCode.RESET:
                        ProcessPresetNormal();
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.roundTimeLimit", "300");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.clear");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", "XP0_Metro", "ConquestLarge0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.add", "MP_Prison", "ConquestLarge0", "1");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.save");
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "mapList.setNextMapIndex", "0");
                        GoalTickets = 1600;
                        TicketRatio = 800 / 100.0;
                        GMC = (Int32)Math.Ceiling(GoalTickets / TicketRatio);
                        ExecuteCommandWithDelay(delayMS, "procon.protected.send", "vars.gameModeCounter", GMC.ToString());
                        Log.Info("Event RESET complete!");
                        break;
                    default:
                        Log.Error("Unknown mode type when processing event transition.");
                        break;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while processing event map mode.", e));
            }
            finally
            {
                Log.Debug(() => "Exiting ProcessEventMapMode", 7);
            }
        }

        public override void OnRoundOverTeamScores(List<TeamScore> teamScores)
        {
            try
            {
                //Set round duration
                _previousRoundDuration = _serverInfo.GetRoundElapsedTime();
                //Update the live team scores
                if (teamScores != null)
                {
                    foreach (var teamScore in teamScores)
                    {
                        ATeam aTeam;
                        if (GetTeamByID(teamScore.TeamID, out aTeam))
                        {
                            aTeam.UpdateTicketCount(teamScore.Score);
                        }
                    }
                }
                ATeam team1, team2;
                if (!GetTeamByID(1, out team1))
                {
                    if (_roundState == RoundState.Playing)
                    {
                        Log.Error("Teams not loaded when they should be.");
                    }
                    return;
                }
                if (!GetTeamByID(2, out team2))
                {
                    if (_roundState == RoundState.Playing)
                    {
                        Log.Error("Teams not loaded when they should be.");
                    }
                    return;
                }
                ATeam winningTeam, losingTeam;
                if (team1.TeamTicketCount > team2.TeamTicketCount)
                {
                    winningTeam = team1;
                    losingTeam = team2;
                }
                else
                {
                    winningTeam = team2;
                    losingTeam = team1;
                }

                lock (_AssistAttemptQueue)
                {
                    _AssistAttemptQueue.Clear();
                }

                // EVENT AUTOMATION
                if (_UseExperimentalTools &&
                    _EventRoundOptions.Any() &&
                    _EventDate.ToShortDateString() != GetLocalEpochTime().ToShortDateString())
                {
                    var nRound = _roundID + 1;
                    Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                    {
                        Thread.CurrentThread.Name = "EventAnnounce";
                        // If there is an active poll auto-complete it and any subsequent polls for 5 seconds
                        // This ensures that the next round is ready and configured
                        var startTime = UtcNow();
                        Threading.Wait(100);
                        while (NowDuration(startTime).TotalSeconds < 5)
                        {
                            try
                            {
                                if (_ActivePoll != null)
                                {
                                    _ActivePoll.Completed = true;
                                }
                            }
                            catch (Exception) { }
                            Threading.Wait(100);
                        }
                        // The new _roundID is fetched by now
                        if (EventActive(nRound + 1))
                        {
                            // The round before the event, make sure xVotemap is not active
                            // The map voting will be handled by the event script
                            ExecuteCommand("procon.protected.plugins.enable", "xVotemap", "False");
                        }
                        else if (EventActive(nRound))
                        {
                            var nextCode = GetEventRoundRuleCode(GetActiveEventRoundNumber(false));
                            if (nextCode == AEventOption.RuleCode.AO ||
                                nextCode == AEventOption.RuleCode.ARO ||
                                nextCode == AEventOption.RuleCode.LMGO ||
                                nextCode == AEventOption.RuleCode.BKO ||
                                nextCode == AEventOption.RuleCode.CAI ||
                                nextCode == AEventOption.RuleCode.PO)
                            {
                                ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "True");
                            }
                            else
                            {
                                ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "False");
                            }
                            SetExternalPluginSetting("AdKatsLRT", "Spawn Enforce Admins", "True");
                            SetExternalPluginSetting("AdKatsLRT", "Spawn Enforce Reputable Players", "True");
                            ExecuteCommand("procon.protected.plugins.enable", "xVotemap", "False");
                            //ACTIVE ROUND
                            for (int i = 0; i < 8; i++)
                            {
                                AdminTellMessage("PREPARING EVENT! " + GetEventMessage(false));
                                Thread.Sleep(2000);
                            }
                            ProcessEventMapMode(GetActiveEventRoundNumber(false));
                        }
                        else if (nRound == _EventTestRoundNumber)
                        {
                            //TEST ROUND
                            for (int i = 0; i < 8; i++)
                            {
                                AdminTellMessage("PREPARING EVENT! TESTING! TESTING!");
                                Thread.Sleep(2000);
                            }
                            ProcessEventMapMode(AEventOption.MapCode.MET, AEventOption.ModeCode.D500);
                        }
                        else if (nRound >= _CurrentEventRoundNumber + _EventRoundOptions.Count())
                        {
                            //NORMAL ROUND
                            // Reset the current event number, as the event has ended.
                            _CurrentEventRoundNumber = 999999;
                            QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                            ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "True");
                            ExecuteCommand("procon.protected.plugins.enable", "xVotemap", "True");
                            SetExternalPluginSetting("AdKatsLRT", "Spawn Enforce Admins", "False");
                            SetExternalPluginSetting("AdKatsLRT", "Spawn Enforce Reputable Players", "False");
                            for (int i = 0; i < 6; i++)
                            {
                                AdminTellMessage("EVENT IS OVER, THANK YOU FOR COMING!");
                                Thread.Sleep(2000);
                            }
                            ProcessEventMapMode(AEventOption.MapCode.MET, AEventOption.ModeCode.RESET);
                            for (int i = 0; i < 10; i++)
                            {
                                AdminTellMessage("EVENT IS OVER, THANK YOU FOR COMING!");
                                Thread.Sleep(2000);
                            }
                        }
                        UpdateSettingPage();
                        Threading.StopWatchdog();
                    })));
                }

                if (_serverInfo.ServerName.Contains("[FPSG] 24/7 Operation Lockers"))
                {
                    Int32 quality = 4;
                    if (winningTeam.TeamTicketCount >= 1500)
                    {
                        quality = 0;
                    }
                    else if (winningTeam.TeamTicketCount >= 1100)
                    {
                        quality = 1;
                    }
                    else if (winningTeam.TeamTicketCount >= 700)
                    {
                        quality = 2;
                    }
                    else if (winningTeam.TeamTicketCount >= 350)
                    {
                        quality = 3;
                    }
                    QueueStatisticForProcessing(new AStatistic()
                    {
                        stat_type = AStatistic.StatisticType.round_quality,
                        server_id = _serverInfo.ServerID,
                        round_id = _roundID,
                        target_name = _serverInfo.InfoObject.Map,
                        stat_value = quality,
                        stat_comment = "Quality level " + quality + " (" + winningTeam.TeamTicketCount + "|" + losingTeam.TeamTicketCount + ")",
                        stat_time = UtcNow()
                    });
                }
                else if (_serverInfo.ServerName.Contains("[FPSG] 24/7 Metro Madness"))
                {
                    Int32 quality = 4;
                    if (winningTeam.TeamTicketCount >= 1100)
                    {
                        quality = 0;
                    }
                    else if (winningTeam.TeamTicketCount >= 800)
                    {
                        quality = 1;
                    }
                    else if (winningTeam.TeamTicketCount >= 600)
                    {
                        quality = 2;
                    }
                    else if (winningTeam.TeamTicketCount >= 400)
                    {
                        quality = 3;
                    }
                    QueueStatisticForProcessing(new AStatistic()
                    {
                        stat_type = AStatistic.StatisticType.round_quality,
                        server_id = _serverInfo.ServerID,
                        round_id = _roundID,
                        target_name = _serverInfo.InfoObject.Map,
                        stat_value = quality,
                        stat_comment = "Quality level " + quality + " (" + winningTeam.TeamTicketCount + "|" + losingTeam.TeamTicketCount + ")",
                        stat_time = UtcNow()
                    });
                }

                //Post round stats
                PostRoundStatistics(winningTeam, losingTeam);

                //Wait for round over players to be fired, if not already
                var start = UtcNow();
                while (_roundOverPlayers == null && (UtcNow() - start).TotalSeconds < 10)
                {
                    Thread.Sleep(100);
                }
                if ((UtcNow() - start).TotalSeconds >= 10)
                {
                    Log.Error("Round over players waiting timed out!");
                }

                if (_roundOverPlayers != null)
                {
                    if (_UseTeamPowerMonitorScrambler)
                    {
                        //Clear out the round over squad list
                        _RoundPrepSquads.Clear();
                        //Update all players with their final stats
                        foreach (var roundPlayerData in _roundOverPlayers)
                        {
                            APlayer aPlayer;
                            if (_PlayerDictionary.TryGetValue(roundPlayerData.SoldierName, out aPlayer) &&
                                aPlayer.player_type == PlayerType.Player)
                            {
                                aPlayer.fbpInfo = roundPlayerData;
                                APlayerStats aStats;
                                if (aPlayer.RoundStats.TryGetValue(_roundID, out aStats))
                                {
                                    aStats.LiveStats = roundPlayerData;
                                }
                                var squadIdentifier = aPlayer.fbpInfo.TeamID.ToString() + aPlayer.fbpInfo.SquadID.ToString();
                                ASquad squad = _RoundPrepSquads.FirstOrDefault(iSquad => iSquad.TeamID == aPlayer.fbpInfo.TeamID &&
                                                                                              iSquad.SquadID == aPlayer.fbpInfo.SquadID);
                                // If the squad isn't loaded yet, load it
                                if (squad == null)
                                {
                                    squad = new ASquad(this)
                                    {
                                        TeamID = aPlayer.fbpInfo.TeamID,
                                        SquadID = aPlayer.fbpInfo.SquadID
                                    };
                                    _RoundPrepSquads.Add(squad);
                                }
                                // Store the player
                                squad.Players.Add(aPlayer);
                            }
                        }
                        foreach (var squad in _RoundPrepSquads.OrderBy(squad => squad.TeamID).ThenByDescending(squad => squad.Players.Sum(member => member.GetPower(true))))
                        {
                            Log.Info("Squad " + squad);
                        }
                        Log.Success(_RoundPrepSquads.Count() + " squads logged for next round.");
                    }

                    //Unassign round over players, wait for next round
                    _roundOverPlayers = null;
                }
                else
                {
                    Log.Error("Round over players not found/ready! Contact ColColonCleaner.");
                }

                if (ChallengeManager != null)
                {
                    ChallengeManager.OnRoundEnded(_roundID);
                }

                //Stat refresh
                List<APlayer> roundPlayerObjects;
                HashSet<Int64> roundPlayers;
                if (_roundID > 0 && _RoundPlayerIDs.TryGetValue(_roundID, out roundPlayers) && _useAntiCheatLIVESystem)
                {
                    //Get players who where online this round
                    roundPlayerObjects = GetFetchedPlayers().Where(dPlayer => roundPlayers.Contains(dPlayer.player_id)).ToList();

                    //TODO: Clear out the total score/kills/deaths

                    //Queue players for stats refresh
                    Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                    {
                        Thread.CurrentThread.Name = "StatRefetch";
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        foreach (var aPlayer in roundPlayerObjects)
                        {
                            if (_UseBanEnforcer)
                            {
                                QueuePlayerForBanCheck(aPlayer);
                            }
                            else
                            {
                                //Queue the player for a AntiCheat check
                                QueuePlayerForAntiCheatCheck(aPlayer);
                            }
                        }
                        Threading.StopWatchdog();
                    })));
                }

                RunFactionRandomizer();

                FetchRoundID(true);

                _roundState = RoundState.Ended;
                _EventRoundPolled = false;
                _pingKicksThisRound = 0;
                _ScrambleRequiredTeamsRemoved = false;
                foreach (APlayer aPlayer in GetFetchedPlayers().Where(aPlayer => aPlayer.RequiredTeam != null))
                {
                    aPlayer.RequiredTeam = null;
                    aPlayer.RequiredSquad = -1;
                    aPlayer.player_spawnedRound = false;
                }

                if (_UseExperimentalTools)
                {
                    // This might look a little odd, but since AdKats is first in the
                    // plugin loading list and the endround events are fired sync instead
                    // of async I can artificially delay other plugins from acting on the
                    // end round event for a few seconds. Obviously this is experimental.
                    Threading.Wait(5000);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running round over teamscores.", e));
            }
        }

        public void RunFactionRandomizer()
        {
            Log.Debug(() => "Entering RunFactionRandomizer", 6);
            try
            {
                //Faction Randomizer
                //Credit to LumPenPacK
                if (_factionRandomizerEnable && GameVersion == GameVersionEnum.BF4)
                {
                    var nextMap = _serverInfo.GetNextMap();
                    if (((_serverInfo.InfoObject.CurrentRound + 1) >= _serverInfo.InfoObject.TotalRounds) &&
                        (nextMap != null && (nextMap.MapFileName == "XP3_UrbanGdn" || nextMap.MapFileName == "X0_Oman")))
                    {
                        //Cannot change things on urban garden or oman
                        Log.Info("Cannot run faction randomizer on urban garden or gulf of oman.");
                        return;
                    }

                    var team1Selection = 0;
                    var team2Selection = 1;
                    var selectionValid = false;
                    var attempts = 0;
                    Random rnd = new Random();
                    Int32 US = 0;
                    Int32 RU = 1;
                    Int32 CN = 2;

                    while (!selectionValid && ++attempts < 1000)
                    {
                        switch (_factionRandomizerRestriction)
                        {
                            case FactionRandomizerRestriction.NoRestriction:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = rnd.Next(0, 3);
                                break;
                            case FactionRandomizerRestriction.NeverSameFaction:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = rnd.Next(0, 3);
                                if (team1Selection == team2Selection)
                                {
                                    continue;
                                }
                                break;
                            case FactionRandomizerRestriction.AlwaysSameFaction:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = team1Selection;
                                break;
                            case FactionRandomizerRestriction.AlwaysSwapUSvsRU:
                                if (_factionRandomizerCurrentTeam1 == US &&
                                    _factionRandomizerCurrentTeam2 == RU)
                                {
                                    team1Selection = RU;
                                    team2Selection = US;
                                }
                                else
                                {
                                    team1Selection = US;
                                    team2Selection = RU;
                                }
                                break;
                            case FactionRandomizerRestriction.AlwaysSwapUSvsCN:
                                if (_factionRandomizerCurrentTeam1 == US &&
                                    _factionRandomizerCurrentTeam2 == CN)
                                {
                                    team1Selection = CN;
                                    team2Selection = US;
                                }
                                else
                                {
                                    team1Selection = US;
                                    team2Selection = CN;
                                }
                                break;
                            case FactionRandomizerRestriction.AlwaysSwapRUvsCN:
                                if (_factionRandomizerCurrentTeam1 == RU &&
                                    _factionRandomizerCurrentTeam2 == CN)
                                {
                                    team1Selection = CN;
                                    team2Selection = RU;
                                }
                                else
                                {
                                    team1Selection = RU;
                                    team2Selection = CN;
                                }
                                break;
                            case FactionRandomizerRestriction.AlwaysBothUS:
                                team1Selection = US;
                                team2Selection = US;
                                break;
                            case FactionRandomizerRestriction.AlwaysBothRU:
                                team1Selection = RU;
                                team2Selection = RU;
                                break;
                            case FactionRandomizerRestriction.AlwaysBothCN:
                                team1Selection = CN;
                                team2Selection = CN;
                                break;
                            case FactionRandomizerRestriction.AlwaysUSvsX:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = rnd.Next(0, 3);
                                if (team1Selection != US && team2Selection != US)
                                {
                                    continue;
                                }
                                break;
                            case FactionRandomizerRestriction.AlwaysRUvsX:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = rnd.Next(0, 3);
                                if (team1Selection != RU && team2Selection != RU)
                                {
                                    continue;
                                }
                                break;
                            case FactionRandomizerRestriction.AlwaysCNvsX:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = rnd.Next(0, 3);
                                if (team1Selection != CN && team2Selection != CN)
                                {
                                    continue;
                                }
                                break;
                            case FactionRandomizerRestriction.NeverUSvsX:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = rnd.Next(0, 3);
                                if (team1Selection == US || team2Selection == US)
                                {
                                    continue;
                                }
                                break;
                            case FactionRandomizerRestriction.NeverRUvsX:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = rnd.Next(0, 3);
                                if (team1Selection == RU || team2Selection == RU)
                                {
                                    continue;
                                }
                                break;
                            case FactionRandomizerRestriction.NeverCNvsX:
                                team1Selection = rnd.Next(0, 3);
                                team2Selection = rnd.Next(0, 3);
                                if (team1Selection == CN || team2Selection == CN)
                                {
                                    continue;
                                }
                                break;
                            default:
                                break;
                        }

                        if (!_factionRandomizerAllowRepeatSelection)
                        {
                            //We cannot allow the same teams to be selected again
                            if (_factionRandomizerCurrentTeam1 == team1Selection &&
                                _factionRandomizerCurrentTeam2 == team2Selection)
                            {
                                continue;
                            }
                        }

                        selectionValid = true;
                    }

                    if (selectionValid)
                    {
                        _factionRandomizerCurrentTeam1 = team1Selection;
                        _factionRandomizerCurrentTeam2 = team2Selection;
                        ExecuteCommand("procon.protected.send", "vars.teamFactionOverride", "1", Convert.ToString(team1Selection));
                        ExecuteCommand("procon.protected.send", "vars.teamFactionOverride", "2", Convert.ToString(team2Selection));
                        ExecuteCommand("procon.protected.send", "vars.teamFactionOverride", "3", Convert.ToString(team1Selection));
                        ExecuteCommand("procon.protected.send", "vars.teamFactionOverride", "4", Convert.ToString(team2Selection));
                    }
                    else
                    {
                        Log.Error("Faction randomizer failed!");
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running faction randomizer.", e));
            }
            Log.Debug(() => "Exiting RunFactionRandomizer", 6);
        }

        public override void OnRunNextLevel()
        {
            try
            {
                if (_roundState != RoundState.Ended)
                {
                    getMapInfo();
                    _roundState = RoundState.Ended;
                    _pingKicksThisRound = 0;
                    foreach (APlayer aPlayer in GetFetchedPlayers().Where(aPlayer => aPlayer.RequiredTeam != null))
                    {
                        aPlayer.RequiredTeam = null;
                        aPlayer.RequiredSquad = -1;
                        aPlayer.player_spawnedRound = false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error handling next level.", e));
            }
        }

        //Move delayed players when they are killed
        public override void OnPlayerKilled(Kill kKillerVictimDetails)
        {
            Log.Debug(() => "Entering OnPlayerKilled", 7);
            try
            {
                //If the plugin is not enabled just return
                if (!_pluginEnabled || !_threadsReady || !_firstPlayerListComplete)
                {
                    return;
                }
                //Used for delayed player moving
                if (_TeamswapOnDeathMoveDic.Count > 0)
                {
                    lock (_TeamswapOnDeathCheckingQueue)
                    {
                        _TeamswapOnDeathCheckingQueue.Enqueue(kKillerVictimDetails.Victim);
                        _TeamswapWaitHandle.Set();
                    }
                }
                //Otherwise, queue the kill for processing
                QueueKillForProcessing(kKillerVictimDetails);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling onPlayerKilled.", e));
            }
            Log.Debug(() => "Exiting OnPlayerKilled", 7);
        }

        public override void OnPlayerIsAlive(string soldierName, bool isAlive)
        {
            Log.Debug(() => "Entering OnPlayerIsAlive", 7);
            try
            {
                if (!_pluginEnabled)
                {
                    return;
                }
                if (!_ActOnIsAliveDictionary.ContainsKey(soldierName))
                {
                    return;
                }
                ARecord aRecord;
                lock (_ActOnIsAliveDictionary)
                {
                    if (_ActOnIsAliveDictionary.TryGetValue(soldierName, out aRecord))
                    {
                        _ActOnIsAliveDictionary.Remove(aRecord.target_player.player_name);
                        aRecord.isAliveChecked = true;
                        switch (aRecord.command_action.command_key)
                        {
                            case "player_kill":
                            case "player_kill_lowpop":
                                if (isAlive)
                                {
                                    QueueRecordForActionHandling(aRecord);
                                }
                                else
                                {
                                    if (!_ActOnSpawnDictionary.ContainsKey(aRecord.target_player.player_name))
                                    {
                                        Log.Debug(() => aRecord.GetTargetNames() + " is dead. Queueing them for kill on-spawn.", 3);
                                        SendMessageToSource(aRecord, aRecord.GetTargetNames() + " is dead. Queueing them for kill on-spawn.");
                                        ExecuteCommand("procon.protected.send", "admin.killPlayer", aRecord.target_player.player_name);
                                        lock (_ActOnSpawnDictionary)
                                        {
                                            aRecord.command_action = GetCommandByKey("player_kill_repeat");
                                            _ActOnSpawnDictionary.Add(aRecord.target_player.player_name, aRecord);
                                        }
                                    }
                                }
                                break;
                            case "player_move":
                                //If player is not alive, change to force move
                                if (!isAlive)
                                {
                                    aRecord.command_type = GetCommandByKey("player_fmove");
                                    aRecord.command_action = GetCommandByKey("player_fmove");
                                }
                                QueueRecordForActionHandling(aRecord);
                                break;
                            default:
                                Log.Error("Command " + aRecord.command_action.command_key + " not useable in OnPlayerIsAlive");
                                break;
                        }
                    }
                    else
                    {
                        Log.Warn(soldierName + " not fetchable from the isalive dictionary.");
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling OnPlayerIsAlive.", e));
            }
            Log.Debug(() => "Exiting OnPlayerIsAlive", 7);
        }

        private void QueueKillForProcessing(Kill kKillerVictimDetails)
        {
            Log.Debug(() => "Entering queueKillForProcessing", 7);
            try
            {
                if (_pluginEnabled && _threadsReady && _firstPlayerListComplete)
                {
                    Log.Debug(() => "Preparing to queue kill for processing", 6);
                    lock (_KillProcessingQueue)
                    {
                        _KillProcessingQueue.Enqueue(kKillerVictimDetails);
                        Log.Debug(() => "Kill queued for processing", 6);
                        _KillProcessingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing kill for processing.", e));
            }
            Log.Debug(() => "Exiting queueKillForProcessing", 7);
        }


        public void TeamswapThreadLoop()
        {
            //assume the max player count per team is 32 if no server info has been provided
            Int32 maxTeamPlayerCount = 32;
            try
            {
                Log.Debug(() => "Starting TeamSwap Thread", 1);
                Thread.CurrentThread.Name = "TeamSwap";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering TeamSwap Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        ATeam team1;
                        ATeam team2;
                        if (!_teamDictionary.TryGetValue(1, out team1))
                        {
                            if (_roundState == RoundState.Playing)
                            {
                                Log.Debug(() => "Team 1 was not found. Unable to continue.", 1);
                            }
                            Threading.Wait(5000);
                            continue;
                        }
                        if (!_teamDictionary.TryGetValue(2, out team2))
                        {
                            if (_roundState == RoundState.Playing)
                            {
                                Log.Debug(() => "Team 2 was not found. Unable to continue.", 1);
                            }
                            Threading.Wait(5000);
                            continue;
                        }

                        //Refresh Max Player Count, needed for responsive server size
                        if (_serverInfo.InfoObject != null && _serverInfo.InfoObject.MaxPlayerCount != maxTeamPlayerCount)
                        {
                            maxTeamPlayerCount = _serverInfo.InfoObject.MaxPlayerCount / 2;
                        }

                        //Get players who died that need moving
                        if ((_TeamswapOnDeathMoveDic.Count > 0 && _TeamswapOnDeathCheckingQueue.Count > 0) || _TeamswapForceMoveQueue.Count > 0)
                        {
                            Log.Debug(() => "Preparing to lock TeamSwap queues", 4);

                            _PlayerListUpdateWaitHandle.Reset();
                            //Wait for listPlayers to finish, max 10 seconds
                            if (!_PlayerListUpdateWaitHandle.WaitOne(TimeSpan.FromSeconds(10)))
                            {
                                Log.Debug(() => "ListPlayers ran out of time for TeamSwap. 10 sec.", 4);
                            }

                            Queue<CPlayerInfo> movingQueue;
                            Queue<CPlayerInfo> checkingQueue;
                            lock (_TeamswapForceMoveQueue)
                            {
                                movingQueue = new Queue<CPlayerInfo>(_TeamswapForceMoveQueue.ToArray());
                                _TeamswapForceMoveQueue.Clear();
                            }
                            lock (_TeamswapOnDeathCheckingQueue)
                            {
                                checkingQueue = new Queue<CPlayerInfo>(_TeamswapOnDeathCheckingQueue.ToArray());
                                _TeamswapOnDeathCheckingQueue.Clear();
                            }

                            //Check for "on-death" move players
                            while (_TeamswapOnDeathMoveDic.Count > 0 && checkingQueue.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                //Dequeue the first/next player
                                String playerName = checkingQueue.Dequeue().SoldierName;
                                CPlayerInfo player;
                                //If they are
                                if (_TeamswapOnDeathMoveDic.TryGetValue(playerName, out player))
                                {
                                    //Player has died, remove from the dictionary
                                    _TeamswapOnDeathMoveDic.Remove(playerName);
                                    //Add to move queue
                                    movingQueue.Enqueue(player);
                                }
                            }

                            while (movingQueue.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                CPlayerInfo player = movingQueue.Dequeue();
                                switch (player.TeamID)
                                {
                                    case 1:
                                        if (!ContainsCPlayerInfo(_Team1MoveQueue, player.SoldierName))
                                        {
                                            _Team1MoveQueue.Enqueue(player);
                                            PlayerSayMessage(player.SoldierName, Log.CViolet("Added to (" + team1.TeamKey + " -> " + team2.TeamKey + ") queue in position " + (IndexOfCPlayerInfo(_Team1MoveQueue, player.SoldierName) + 1) + "."));
                                        }
                                        else
                                        {
                                            PlayerSayMessage(player.SoldierName, Log.CViolet(team2.TeamKey + " Team Full (" + team2.TeamPlayerCount + "/" + maxTeamPlayerCount + "). You are in queue position " + (IndexOfCPlayerInfo(_Team1MoveQueue, player.SoldierName) + 1)));
                                        }
                                        break;
                                    case 2:
                                        if (!ContainsCPlayerInfo(_Team2MoveQueue, player.SoldierName))
                                        {
                                            _Team2MoveQueue.Enqueue(player);
                                            PlayerSayMessage(player.SoldierName, Log.CViolet("Added to (" + team2.TeamKey + " -> " + team1.TeamKey + ") queue in position " + (IndexOfCPlayerInfo(_Team2MoveQueue, player.SoldierName) + 1) + "."));
                                        }
                                        else
                                        {
                                            PlayerSayMessage(player.SoldierName, Log.CViolet(team1.TeamKey + " Team Full (" + team1.TeamPlayerCount + "/" + maxTeamPlayerCount + "). You are in queue position " + (IndexOfCPlayerInfo(_Team2MoveQueue, player.SoldierName) + 1)));
                                        }
                                        break;
                                }
                            }
                        }
                        Log.Debug(() => "Team Info: " + team1.TeamKey + ": " + team1.TeamPlayerCount + "/" + maxTeamPlayerCount + " " + team2.TeamKey + ": " + team2.TeamPlayerCount + "/" + maxTeamPlayerCount, 5);
                        if (_Team2MoveQueue.Count > 0 || _Team1MoveQueue.Count > 0)
                        {
                            //Perform player moving
                            do
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                if (_Team2MoveQueue.Count > 0)
                                {
                                    if (team1.TeamPlayerCount < maxTeamPlayerCount)
                                    {
                                        CPlayerInfo player = _Team2MoveQueue.Dequeue();
                                        APlayer dicPlayer;
                                        if (_PlayerDictionary.TryGetValue(player.SoldierName, out dicPlayer))
                                        {
                                            if (dicPlayer.fbpInfo.TeamID == 1)
                                            {
                                                //Skip the kill/swap if they are already on the goal team by some other means
                                                continue;
                                            }
                                        }
                                        if (String.IsNullOrEmpty(player.SoldierName))
                                        {
                                            Log.Error("soldiername null in team 2 -> 1 teamswap");
                                        }
                                        else
                                        {
                                            Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                                            ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                                            _MULTIBalancerUnswitcherDisabled = true;
                                            var told = false;
                                            if (dicPlayer != null)
                                            {
                                                dicPlayer.RequiredTeam = team1;
                                                ARecord assistRecord = dicPlayer.TargetedRecords.FirstOrDefault(record => record.command_type.command_key == "self_assist" && record.command_action.command_key == "self_assist_unconfirmed");
                                                if (assistRecord != null)
                                                {
                                                    AdminSayMessage(Log.CViolet(assistRecord.target_player.GetVerboseName() + " (" + Math.Round(assistRecord.target_player.GetPower(true)) + "), thank you for assisting " + team1.TeamKey + "!"));
                                                    assistRecord.command_action = GetCommandByKey("self_assist");
                                                    QueueRecordForProcessing(assistRecord);
                                                    told = true;
                                                }
                                            }
                                            if (!told)
                                            {
                                                PlayerSayMessage(player.SoldierName, Log.CViolet("Swapping you from team " + team2.TeamKey + " to team " + team1.TeamKey));
                                            }
                                            ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, "1", "1", "true");
                                            _LastPlayerMoveIssued = UtcNow();
                                            team1.TeamPlayerCount++;
                                            team2.TeamPlayerCount--;
                                        }
                                        Threading.Wait(100);
                                    }
                                }
                                if (_Team1MoveQueue.Count > 0)
                                {
                                    if (team2.TeamPlayerCount < maxTeamPlayerCount)
                                    {
                                        CPlayerInfo player = _Team1MoveQueue.Dequeue();
                                        APlayer dicPlayer;
                                        if (_PlayerDictionary.TryGetValue(player.SoldierName, out dicPlayer))
                                        {
                                            if (dicPlayer.fbpInfo.TeamID == 2)
                                            {
                                                //Skip the kill/swap if they are already on the goal team by some other means
                                                continue;
                                            }
                                        }
                                        if (String.IsNullOrEmpty(player.SoldierName))
                                        {
                                            Log.Error("soldiername null in team 1 -> 2 teamswap");
                                        }
                                        else
                                        {
                                            Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                                            ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                                            _MULTIBalancerUnswitcherDisabled = true;
                                            PlayerSayMessage(player.SoldierName, Log.CViolet("Swapping you from team " + team1.TeamKey + " to team " + team2.TeamKey));
                                            if (dicPlayer != null)
                                            {
                                                dicPlayer.RequiredTeam = team2;
                                                ARecord assistRecord = dicPlayer.TargetedRecords.FirstOrDefault(record => record.command_type.command_key == "self_assist" && record.command_action.command_key == "self_assist_unconfirmed");
                                                if (assistRecord != null)
                                                {
                                                    AdminSayMessage(assistRecord.target_player.GetVerboseName() + " (" + Math.Round(assistRecord.target_player.GetPower(true)) + "), thank you for assisting " + team2.TeamKey + "!");
                                                    assistRecord.command_action = GetCommandByKey("self_assist");
                                                    QueueRecordForProcessing(assistRecord);
                                                }
                                            }
                                            ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, "2", "1", "true");
                                            _LastPlayerMoveIssued = UtcNow();
                                            team2.TeamPlayerCount++;
                                            team1.TeamPlayerCount--;
                                        }
                                    }
                                }
                            } while (false);
                        }
                        else
                        {
                            Log.Debug(() => "No players to swap. Waiting for Input.", 6);
                            //There are no players to swap, wait.
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _TeamswapWaitHandle.Reset();
                            _TeamswapWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                            loopStart = UtcNow();
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("TeamSwap thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in TeamSwap thread. Skipping current loop.", e));
                    }
                    _TeamswapWaitHandle.Reset();
                    _TeamswapWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                }
                Log.Debug(() => "Ending TeamSwap Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in teamswap thread.", e));
            }
        }

        //Whether a move queue contains a given player
        private bool ContainsCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            Log.Debug(() => "Entering containsCPlayerInfo", 7);
            try
            {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 index = 0; index < queueList.Count; index++)
                {
                    if (playerArray[index].SoldierName == player)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while checking for player in teamswap queue.", e));
            }
            Log.Debug(() => "Exiting containsCPlayerInfo", 7);
            return false;
        }

        //The index of a player in the move queue
        private Int32 IndexOfCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            Log.Debug(() => "Entering getCPlayerInfo", 7);
            try
            {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 i = 0; i < queueList.Count; i++)
                {
                    if (playerArray[i].SoldierName == player)
                    {
                        return i;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting index of player in teamswap queue.", e));
            }
            Log.Debug(() => "Exiting getCPlayerInfo", 7);
            return -1;
        }

        private void QueueRecordForProcessing(ARecord record)
        {
            Log.Debug(() => "Entering queueRecordForProcessing", 7);
            try
            {
                if (record.command_action == null)
                {
                    if (record.command_type == null)
                    {
                        record.record_exception = Log.HandleException(new AException("Attempted to create a record with no command. " + ((String.IsNullOrEmpty(record.source_name)) ? ("NOSOURCE") : (record.source_name)) + "|" + ((String.IsNullOrEmpty(record.record_message)) ? ("NOMESSAGE") : (record.record_message))));
                        FinalizeRecord(record);
                        return;
                    }
                    record.command_action = record.command_type;
                }
                if (!record.record_action_executed)
                {
                    //Check for command lock
                    if (record.target_player != null &&
                        record.target_player.IsLocked() &&
                        record.target_player.GetLockSource() != record.source_name &&
                        (!_UseExperimentalTools || record.source_name != "ProconAdmin"))
                    {
                        SendMessageToSource(record, record.GetTargetNames() + " is command locked by " + record.target_player.GetLockSource() + ". Please wait for unlock [" + FormatTimeString(record.target_player.GetLockRemaining(), 3) + "].");
                        FinalizeRecord(record);
                        return;
                    }
                    //Power level exclusion
                    if (record.source_player != null && record.target_player != null && record.source_player.player_role.role_powerLevel < record.target_player.player_role.role_powerLevel &&
                        (record.command_type.command_key == "player_kill" ||
                         record.command_type.command_key == "player_kill_force" ||
                         record.command_type.command_key == "player_kick" ||
                         record.command_type.command_key == "player_ban_temp" ||
                         record.command_type.command_key == "player_ban_perm" ||
                         record.command_type.command_key == "player_ban_perm_future" ||
                         record.command_type.command_key == "player_punish" ||
                         record.command_type.command_key == "player_forgive" ||
                         record.command_type.command_key == "player_mute" ||
                         record.command_type.command_key == "player_move" ||
                         record.command_type.command_key == "player_fmove" ||
                         record.command_type.command_key == "self_lead" ||
                         record.command_type.command_key == "player_pull" ||
                         record.command_type.command_key == "player_lock"))
                    {
                        SendMessageToSource(record, "You cannot issue " + record.command_type.command_name + " on " + record.target_player.GetVerboseName() + " their power level (" + record.target_player.player_role.role_powerLevel + ") is higher than yours (" + record.source_player.player_role.role_powerLevel + ")");
                        FinalizeRecord(record);
                        return;
                    }
                    if (record.target_player != null && _CommandTargetWhitelistCommands.Contains(record.command_type.command_text) && GetMatchingVerboseASPlayersOfGroup("whitelist_commandtarget", record.target_player).Any())
                    {
                        SendMessageToSource(record, record.command_type.command_name + " cannot be issued on " + record.target_player.GetVerboseName());
                        FinalizeRecord(record);
                        return;
                    }
                    // Move protection handling
                    if (record.source_player != null && record.target_player != null && (record.command_type.command_key == "player_move" || record.command_type.command_key == "player_fmove" || record.command_type.command_key == "player_pull") && GetMatchingVerboseASPlayersOfGroup("whitelist_move_protection", record.target_player).Any())
                    {
                        SendMessageToSource(record, record.target_player.GetVerboseName() + " is protected from being moved.");
                        FinalizeRecord(record);
                        return;
                    }
                    //Command timeouts
                    if (record.command_action != null &&
                        _commandTimeoutDictionary.ContainsKey(record.command_action.command_key) &&
                        !record.record_action_executed)
                    {
                        if (record.target_player != null && !record.TargetPlayersLocal.Any())
                        {
                            //Cancel call if record is on timeout for single player
                            if (record.target_player.TargetedRecords.Any(aRecord => aRecord.command_action.command_key == record.command_action.command_key && aRecord.record_time.AddSeconds(Math.Abs(_commandTimeoutDictionary[record.command_action.command_key](this))) > UtcNow()))
                            {
                                SendMessageToSource(record, record.command_type.command_name + " on timeout for " + record.GetTargetNames());
                                FinalizeRecord(record);
                                return;
                            }
                        }
                        else if (record.TargetPlayersLocal.Any())
                        {
                            //Cancel call if record is on timeout for any targeted players
                            foreach (APlayer aPlayer in record.TargetPlayersLocal)
                            {
                                if (aPlayer.TargetedRecords.Any(aRecord => aRecord.command_action.command_key == record.command_action.command_key && aRecord.record_time.AddSeconds(Math.Abs(_commandTimeoutDictionary[record.command_action.command_key](this))) > UtcNow()))
                                {
                                    SendMessageToSource(record, record.command_type.command_name + " on timeout for " + aPlayer.GetVerboseName());
                                    FinalizeRecord(record);
                                    return;
                                }
                            }
                        }
                    }
                    if (record.target_player != null && (record.command_type.command_key == "player_report" || record.command_type.command_key == "player_calladmin") && record.target_player.TargetedRecords.Any(targetedRecord => (targetedRecord.command_action.command_key == "player_kill" || targetedRecord.command_action.command_key == "player_kill_lowpop" || targetedRecord.command_action.command_key == "player_kill_repeat" || targetedRecord.command_action.command_key == "player_kill_force" || targetedRecord.command_action.command_key == "player_kick" || targetedRecord.command_action.command_key == "player_ban_temp" || targetedRecord.command_action.command_key == "player_ban_perm" || targetedRecord.command_action.command_key == "player_ban_perm_future" || targetedRecord.command_action.command_key == "player_punish" || targetedRecord.command_action.command_key == "player_mute" || targetedRecord.command_action.command_key == "player_say" || targetedRecord.command_action.command_key == "player_yell" || targetedRecord.command_action.command_key == "player_tell") && (UtcNow() - targetedRecord.record_time).TotalSeconds < 60))
                    {
                        OnlineAdminSayMessage("Report on " + record.GetTargetNames() + " blocked. Player already acted on.");
                        SendMessageToSource(record, "Report on " + record.GetTargetNames() + " blocked. Player already acted on.");
                        FinalizeRecord(record);
                        return;
                    }
                    //Special command case
                    Log.Debug(() => "Preparing to check " + record.command_type.command_key + " record for pre-upload processing.", 5);
                    switch (record.command_type.command_key)
                    {
                        case "self_rules":
                            {
                                if (record.source_name != record.target_name &&
                                    record.target_player != null &&
                                    record.source_player != null &&
                                    !PlayerIsAdmin(record.source_player))
                                {
                                    if (PlayerIsAdmin(record.target_player))
                                    {
                                        SendMessageToSource(record, record.GetTargetNames() + " is an admin, they already know the rules.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    if (record.target_player.player_reputation > _reputationThresholdGood)
                                    {
                                        SendMessageToSource(record, record.GetTargetNames() + " is reputable, they know the rules.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                            }
                            break;
                        case "player_forgive":
                            {
                                if (record.target_player != null && FetchPoints(record.target_player, _CombineServerPunishments, true) <= 0)
                                {
                                    SendMessageToSource(record, record.GetTargetNames() + " does not have any infractions to forgive.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (record.source_name == record.target_name)
                                {
                                    SendMessageToSource(record, "You may not issue forgives against yourself, contant another administrator.");
                                    FinalizeRecord(record);
                                    return;
                                }
                            }
                            break;
                        case "player_report":
                        case "player_calladmin":
                            {
                                if (record.target_player != null && !record.target_player.player_online && record.target_player.TargetedRecords.Any(aRecord => (aRecord.command_action.command_key == "player_kick" || aRecord.command_action.command_key == "player_ban_temp" || aRecord.command_action.command_key == "player_ban_perm") && (UtcNow() - aRecord.record_time).TotalSeconds < 300))
                                {
                                    SendMessageToSource(record, record.GetTargetNames() + " has already been removed by an admin.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (record.target_player != null && GetMatchingVerboseASPlayersOfGroup("whitelist_report", record.target_player).Any())
                                {
                                    SendMessageToSource(record, record.GetTargetNames() + " is whitelisted from reports. Please contact an admin directly if this is urgent.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (record.source_player != null && GetMatchingVerboseASPlayersOfGroup("blacklist_report", record.source_player).Any())
                                {
                                    SendMessageToSource(record, "You may not report players at this time.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (record.source_name == record.target_name)
                                {
                                    SendMessageToSource(record, "You may not report yourself.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (EventActive())
                                {
                                    SendMessageToSource(record, "REPORTING IS DISABLED DURING EVENTS.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                string lowerM = " " + record.record_message.ToLower() + " ";
                                if (_UseExperimentalTools)
                                {
                                    if (lowerM.Contains("headgl") || lowerM.Contains("head gl"))
                                    {
                                        SendMessageToSource(record, "'Head Glitching' related actions are not bannable.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    //Block reports for false reports
                                    if (lowerM.Contains(" false r"))
                                    {
                                        SendMessageToSource(record, "Do not report for false reports, use !contest.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                //Block reports for ping if the ping enforcer is enabled
                                if ((lowerM.Contains(" ping") || lowerM.Contains(" pings")) && _pingEnforcerEnable)
                                {
                                    SendMessageToSource(record, "Automatic system handles ping, do not report for it.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                //Block report wars
                                if (record.target_player != null &&
                                    record.target_player.TargetedRecords.Count(aRecord =>
                                        aRecord.source_name == record.source_name &&
                                        (aRecord.command_type.command_key == "player_report" ||
                                            aRecord.command_type.command_key == "player_calladmin") &&
                                        NowDuration(aRecord.record_time).TotalMinutes < 5 &&
                                        aRecord.command_action.command_key != "player_report_confirm") >= 1 &&
                                    record.source_player != null &&
                                    record.source_player.TargetedRecords.Count(aRecord =>
                                        aRecord.source_name == record.target_name &&
                                        (aRecord.command_type.command_key == "player_report" ||
                                            aRecord.command_type.command_key == "player_calladmin") &&
                                        NowDuration(aRecord.record_time).TotalMinutes < 5 &&
                                        aRecord.command_action.command_key != "player_report_confirm") >= 1)
                                {
                                    SendMessageToSource(record, "Do not have report wars. If this is urgent please contact an admin in Discord; " + GetChatCommandByKey("self_voip") + " for the address.");
                                    QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _serverInfo.ServerID,
                                        command_type = GetCommandByKey("player_log"),
                                        command_numeric = 0,
                                        target_name = record.source_name,
                                        target_player = record.source_player,
                                        source_name = "AutoAdmin",
                                        record_message = "Report war blocked between " + record.GetSourceName() + " and " + record.GetTargetNames(),
                                        record_time = UtcNow()
                                    });
                                    FinalizeRecord(record);
                                    return;
                                }
                                //Block multiple reports of the same player from one source
                                if (record.target_player != null &&
                                    record.target_player.TargetedRecords.Count(aRecord =>
                                        aRecord.source_name == record.source_name &&
                                        (aRecord.command_type.command_key == "player_report" ||
                                            aRecord.command_type.command_key == "player_calladmin") &&
                                        NowDuration(aRecord.record_time).TotalMinutes < 5 &&
                                        aRecord.command_action.command_key != "player_report_confirm") >= 2)
                                {
                                    SendMessageToSource(record, "You already reported " + record.target_player.GetVerboseName() + ". If this is urgent please contact an admin in teamspeak; @ts for the address.");
                                    QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _serverInfo.ServerID,
                                        command_type = GetCommandByKey("player_log"),
                                        command_numeric = 0,
                                        target_name = record.source_name,
                                        target_player = record.source_player,
                                        source_name = "AutoAdmin",
                                        record_message = "Report spam blocked on " + record.GetTargetNames(),
                                        record_time = UtcNow()
                                    });
                                    FinalizeRecord(record);
                                    return;
                                }
                                //Block multiple reports of the same player from multiple sources
                                if (record.target_player != null &&
                                    record.target_player.TargetedRecords.Count(aRecord =>
                                        (aRecord.command_type.command_key == "player_report" ||
                                            aRecord.command_type.command_key == "player_calladmin") &&
                                        NowDuration(aRecord.record_time).TotalMinutes < 5 &&
                                        aRecord.command_action.command_key != "player_report_confirm") >= 3)
                                {
                                    SendMessageToSource(record, record.target_player.GetVerboseName() + " has already been reported. If this is urgent please contact an admin in teamspeak; @ts for the address.");
                                    QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _serverInfo.ServerID,
                                        command_type = GetCommandByKey("player_log"),
                                        command_numeric = 0,
                                        target_name = record.source_name,
                                        target_player = record.source_player,
                                        source_name = "AutoAdmin",
                                        record_message = "Report spam blocked on " + record.GetTargetNames(),
                                        record_time = UtcNow()
                                    });
                                    FinalizeRecord(record);
                                    return;
                                }
                            }
                            break;
                        case "player_pm_send":
                            {
                                if (record.target_player != null && record.source_player != null)
                                {
                                    if (record.target_player.player_guid == record.source_player.player_guid)
                                    {
                                        SendMessageToSource(record, "foreveralone.jpg (You can't start a conversation with yourself)");
                                    }
                                }
                                else
                                {
                                    SendMessageToSource(record, "Invalid players when trying to start conversation.");
                                    FinalizeRecord(record);
                                    return;
                                }
                            }
                            break;
                        case "player_lock":
                            {
                                //Check if already locked
                                if (record.target_player != null && record.target_player.IsLocked())
                                {
                                    SendMessageToSource(record, record.GetTargetNames() + " is already locked by " + record.target_player.GetLockSource() + " for " + FormatTimeString(record.target_player.GetLockRemaining(), 3) + ".");
                                    FinalizeRecord(record);
                                    return;
                                }
                            }
                            break;
                        case "player_unlock":
                            {
                                //Check if already locked
                                if (record.target_player != null && record.target_player.IsLocked() && record.target_player.GetLockSource() != record.source_name)
                                {
                                    SendMessageToSource(record, record.GetTargetNames() + " is locked by " + record.target_player.GetLockSource() + ", either they can unlock them, or after " + FormatTimeString(record.target_player.GetLockRemaining(), 3) + " the player will be automatically unlocked.");
                                    FinalizeRecord(record);
                                    return;
                                }
                            }
                            break;
                        case "self_surrender":
                        case "self_votenext":
                            {
                                if (EventActive())
                                {
                                    SendMessageToSource(record, "Surrender Vote is not available during events.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (!_surrenderVoteEnable)
                                {
                                    SendMessageToSource(record, "Surrender Vote must be enabled in AdKats settings to use this command.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (_roundState != RoundState.Playing)
                                {
                                    SendMessageToSource(record, "Round state must be playing to use surrender. Current: " + _roundState);
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (record.source_player != null && record.source_player.player_type == PlayerType.Spectator && !PlayerIsAdmin(record.source_player))
                                {
                                    SendMessageToSource(record, "You cannot use " + GetChatCommandByKey("self_surrender") + " or " + GetChatCommandByKey("self_votenext") + " as a spectator.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (_surrenderVoteSucceeded)
                                {
                                    SendMessageToSource(record, "Surrender already succeeded.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (_surrenderVoteList.Contains(record.source_name))
                                {
                                    SendMessageToSource(record, "You already voted! You can cancel your vote with " + GetChatCommandByKey("command_cancel"));
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (!_surrenderVoteActive)
                                {
                                    Int32 playerCount = GetPlayerCount();
                                    if (playerCount < _surrenderVoteMinimumPlayerCount)
                                    {
                                        SendMessageToSource(record, _surrenderVoteMinimumPlayerCount + " players needed to start surrender vote. Current: " + playerCount);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    ATeam team1, team2;
                                    if (!GetTeamByID(1, out team1))
                                    {
                                        if (_roundState == RoundState.Playing)
                                        {
                                            Log.Error("Teams not loaded when they should be.");
                                        }
                                        return;
                                    }
                                    if (!GetTeamByID(2, out team2))
                                    {
                                        if (_roundState == RoundState.Playing)
                                        {
                                            Log.Error("Teams not loaded when they should be.");
                                        }
                                        return;
                                    }
                                    Int32 ticketGap = Math.Abs(team1.TeamTicketCount - team2.TeamTicketCount);
                                    if (ticketGap < _surrenderVoteMinimumTicketGap)
                                    {
                                        SendMessageToSource(record, _surrenderVoteMinimumTicketGap + " ticket gap needed to start Surrender Vote. Current: " + ticketGap);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Double ticketRateGap = Math.Abs(team1.GetTicketDifferenceRate() - team2.GetTicketDifferenceRate());
                                    if (_surrenderVoteTicketRateGapEnable && ticketRateGap < _surrenderVoteMinimumTicketRateGap)
                                    {
                                        SendMessageToSource(record, _surrenderVoteMinimumTicketRateGap + " ticket rate gap needed to start Surrender Vote. Current: " + Math.Round(ticketRateGap, 2));
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                //Replace type if needed
                                ACommand surrenderCommand = GetCommandByKey("self_surrender");
                                ACommand votenextCommand = GetCommandByKey("self_votenext");
                                if (record.source_player == null)
                                {
                                    //Record is external, votenext must me used
                                    if (record.command_type.command_key == surrenderCommand.command_key)
                                    {
                                        record.command_type = votenextCommand;
                                        record.command_action = votenextCommand;
                                        record.record_message = "Player Voted for Next Round";
                                    }
                                }
                                else if (PlayerIsWinning(record.source_player))
                                {
                                    //Player is winning, votenext must me used
                                    if (record.command_type.command_key == surrenderCommand.command_key)
                                    {
                                        record.command_type = votenextCommand;
                                        record.command_action = votenextCommand;
                                        record.record_message = "Player Voted for Next Round";
                                    }
                                }
                                else
                                {
                                    //Player is losing, surrender must me used
                                    if (record.command_type.command_key == votenextCommand.command_key)
                                    {
                                        record.command_type = surrenderCommand;
                                        record.command_action = surrenderCommand;
                                        record.record_message = "Player Voted for Surrender";
                                    }
                                }
                            }
                            break;
                        case "self_nosurrender":
                            {
                                if (EventActive())
                                {
                                    SendMessageToSource(record, "Surrender Vote is not available during events.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (!_surrenderVoteEnable)
                                {
                                    SendMessageToSource(record, "Surrender Vote must be enabled in AdKats settings to use this command.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (_roundState != RoundState.Playing)
                                {
                                    SendMessageToSource(record, "Round state must be playing to vote against surrender. Current: " + _roundState);
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (_surrenderVoteSucceeded)
                                {
                                    SendMessageToSource(record, "Surrender already succeeded.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (_nosurrenderVoteList.Contains(record.source_name))
                                {
                                    SendMessageToSource(record, "You already voted against surrender!");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (!_surrenderVoteActive)
                                {
                                    SendMessageToSource(record, "A surrender vote must be active to vote against it.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (record.source_player != null && PlayerIsWinning(record.source_player))
                                {
                                    AdminSayMessage("You cannot use " + GetChatCommandByKey("self_nosurrender") + " from the winning team.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (record.source_player != null && record.source_player.player_type == PlayerType.Spectator && !PlayerIsAdmin(record.source_player))
                                {
                                    SendMessageToSource(record, "You cannot use " + GetChatCommandByKey("self_nosurrender") + " as a spectator.");
                                    FinalizeRecord(record);
                                    return;
                                }
                            }
                            break;
                        case "player_join":
                            if (record.target_name == record.source_name)
                            {
                                SendMessageToSource(record, "You are already in squad with yourself.");
                                FinalizeRecord(record);
                                return;
                            }
                            if (record.target_player != null &&
                                record.source_player != null &&
                                record.target_player.fbpInfo.TeamID == record.source_player.fbpInfo.TeamID &&
                                record.target_player.fbpInfo.SquadID == record.source_player.fbpInfo.SquadID)
                            {
                                SendMessageToSource(record, "You are already in squad with " + record.target_player.GetVerboseName() + ".");
                                FinalizeRecord(record);
                                return;
                            }
                            break;
                        case "self_battlecry":
                        case "player_battlecry":
                            if (String.IsNullOrEmpty(record.record_message) && String.IsNullOrEmpty(record.target_player.player_battlecry))
                            {
                                SendMessageToSource(record, "You do not have a battlecry to remove.");
                                FinalizeRecord(record);
                                return;
                            }
                            break;
                        case "player_whitelistreport_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_report", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the Report whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistspambot_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_spambot", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the SpamBot whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistaa_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_adminassistant", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the Admin Assistant whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistping_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_ping", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the Ping whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistanticheat_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_anticheat", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the AntiCheat whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_slotspectator_remove":
                            if (!GetMatchingASPlayersOfGroup("slot_spectator", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in spectator slot list.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_slotreserved_remove":
                            if (!GetMatchingASPlayersOfGroup("slot_reserved", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in reserved slot list.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistbalance_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_multibalancer", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the autobalance whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_blacklistdisperse_remove":
                            if (!GetMatchingASPlayersOfGroup("blacklist_dispersion", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not under autobalance dispersion.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistbf4db_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_bf4db", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the BF4DB whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistba_remove":
                            // again redudant... ahhh this needs a redesign. NOW. Hedius.
                            if (!GetMatchingASPlayersOfGroup("whitelist_ba", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the BattlefieldAgency whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_persistentmute_remove":
                            if (!GetMatchingASPlayersOfGroup("persistent_mute", record.target_player).Any() && !GetMatchingASPlayersOfGroup("persistent_mute_force", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not perma/temp muted.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistpopulator_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_populator", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not under populator whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistteamkill_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_teamkill", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not under TeamKillTracker whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_blacklistspectator_remove":
                            if (!GetMatchingASPlayersOfGroup("blacklist_spectator", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the spectator blacklist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_blacklistreport_remove":
                            if (!GetMatchingASPlayersOfGroup("blacklist_report", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the report source blacklist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistcommand_remove":
                            if (!GetMatchingASPlayersOfGroup("whitelist_commandtarget", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the command target whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_blacklistautoassist_remove":
                            if (!GetMatchingASPlayersOfGroup("blacklist_autoassist", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the auto-assist blacklist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_blacklistallcaps_remove":
                            if (!GetMatchingASPlayersOfGroup("blacklist_allcaps", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not under all-caps chat blacklist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_challenge_play_remove":
                            if (!GetMatchingASPlayersOfGroup("challenge_play", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in challenge playing status.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_challenge_ignore_remove":
                            if (!GetMatchingASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in challenge ignoring status.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_challenge_autokill_remove":
                            if (!GetMatchingASPlayersOfGroup("challenge_autokill", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in challenge autokill status.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_watchlist_remove":
                            if (!GetMatchingASPlayersOfGroup("watchlist", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the watchlist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                        case "player_whitelistmoveprotection_remove":
                            // Also redundant code... :) i love adkats :)
                            if (!GetMatchingASPlayersOfGroup("whitelist_move_protection", record.target_player).Any())
                            {
                                SendMessageToSource(record, "Matching player not in the Move Protection whitelist.");
                                FinalizeRecord(record);
                                return;
                            }
                            Log.Debug(() => record.command_type.command_key + " record allowed to continue processing.", 5);
                            break;
                    }
                    if (record.target_player != null)
                    {
                        //Add the record to the player's targeted records
                        record.target_player.TargetedRecords.Add(record);
                    }
                }
                if (_pluginVersionStatus == VersionStatus.OutdatedBuild && !record.record_action_executed && (record.source_player == null || PlayerIsAdmin(record.source_player)))
                {
                    if (_pluginUpdatePatched)
                    {
                        SendMessageToSource(record, "AdKats has been updated to version " + _latestPluginVersion + "! Reboot PRoCon to activate this patch.");
                    }
                    else
                    {
                        SendMessageToSource(record, "You are running an outdated version of AdKats. Update " + _latestPluginVersion + " is released.");
                    }
                }
                if (record.SourceSession == 0 && record.source_player != null)
                {
                    record.SourceSession = record.source_player.ActiveSession;
                }
                if (record.TargetSession == 0 && record.target_player != null)
                {
                    record.TargetSession = record.target_player.ActiveSession;
                }
                Log.Debug(() => "Preparing to queue " + record.command_type.command_key + " record for processing", 6);
                //Set the record update time
                record.record_time_update = UtcNow();
                lock (_UnprocessedRecordQueue)
                {
                    //Queue the record for processing
                    _UnprocessedRecordQueue.Enqueue(record);
                    Log.Debug(() => "Record queued for processing", 6);
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while queueing record for processing.", e);
                Log.HandleException(record.record_exception);
            }
            Log.Debug(() => "Exiting queueRecordForProcessing", 7);
        }

        private void QueueStatisticForProcessing(AStatistic aStat)
        {
            Log.Debug(() => "Entering QueueStatisticForProcessing", 6);
            try
            {
                Log.Debug(() => "Preparing to queue statistic for processing", 6);
                lock (_UnprocessedStatisticQueue)
                {
                    //Queue the statistic for processing
                    _UnprocessedStatisticQueue.Enqueue(aStat);
                    Log.Debug(() => "Statistic queued for processing", 6);
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queuing statistic for processing.", e));
            }
            Log.Debug(() => "Exiting QueueStatisticForProcessing", 6);
        }

    }
}
