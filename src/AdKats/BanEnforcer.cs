using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using MySqlConnector;

using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        private void BanEnforcerThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Ban Enforcer Thread", 1);
                Thread.CurrentThread.Name = "BanEnforcer";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering Ban Enforcer Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        SendNonQuery("Updating Active Bans", "UPDATE `adkats_bans` SET `ban_status` = 'Expired' WHERE `ban_endTime` <= UTC_TIMESTAMP() AND `ban_status` = 'Active'", false);

                        //Get all unchecked players
                        Queue<APlayer> playerCheckingQueue;
                        if (_BanEnforcerCheckingQueue.Count > 0 && _UseBanEnforcer)
                        {
                            Log.Debug(() => "Preparing to lock banEnforcerMutex to retrive new players", 6);
                            lock (_BanEnforcerCheckingQueue)
                            {
                                Log.Debug(() => "Inbound ban enforcer players found. Grabbing.", 5);
                                //Grab all players in the queue
                                playerCheckingQueue = new Queue<APlayer>(_BanEnforcerCheckingQueue.ToArray());
                                //Clear the queue for next run
                                _BanEnforcerCheckingQueue.Clear();
                                if (_databaseConnectionCriticalState)
                                {
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            Log.Debug(() => "No inbound ban checks. Waiting for Input.", 6);
                            //Wait for input
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _BanEnforcerWaitHandle.Reset();
                            _BanEnforcerWaitHandle.WaitOne(TimeSpan.FromSeconds(60));
                            loopStart = UtcNow();
                            continue;
                        }

                        //Get all checks in order that they came in
                        while (playerCheckingQueue.Count > 0)
                        {
                            if (!_pluginEnabled)
                            {
                                break;
                            }
                            //Grab first/next player
                            APlayer aPlayer = playerCheckingQueue.Dequeue();
                            Log.Debug(() => "begin ban enforcer reading player " + aPlayer.GetVerboseName(), 5);
                            if (_PlayerDictionary.ContainsKey(aPlayer.player_name))
                            {
                                List<ABan> aBanList = FetchPlayerBans(aPlayer);
                                if (aBanList.Count > 0)
                                {
                                    //Check for specific ban on this player
                                    ABan playerBan = aBanList.Where(aBan => aBan.player_id == aPlayer.player_id || (aBan.ban_record.target_player != null && aBan.ban_record.target_player.player_id == aPlayer.player_id)).FirstOrDefault();
                                    if (playerBan != null)
                                    {
                                        //Ensure the ban record has updated player information
                                        playerBan.ban_record.target_player = aPlayer;
                                        //Found specific ban
                                        QueueRecordForProcessing(new ARecord
                                        {
                                            record_source = ARecord.Sources.Automated,
                                            source_name = "BanEnforcer",
                                            isIRO = false,
                                            server_id = _serverInfo.ServerID,
                                            target_name = aPlayer.player_name,
                                            target_player = aPlayer,
                                            command_type = GetCommandByKey("banenforcer_enforce"),
                                            command_numeric = (int)playerBan.ban_id,
                                            record_message = playerBan.ban_record.record_message,
                                            record_time = UtcNow()
                                        });
                                    }
                                    else
                                    {
                                        //No specific ban, use linked bans
                                        List<String> linkedIDs = (from aBan in aBanList where aBan != null && aBan.ban_record != null && aBan.ban_record.target_player != null select aBan.ban_record.target_player.player_id.ToString()).ToList();
                                        String strIDs = String.Join(", ", linkedIDs.ToArray());
                                        //Use the first ban found
                                        playerBan = aBanList.FirstOrDefault();
                                        if (playerBan != null)
                                        {
                                            //Ensure the ban record has updated player information
                                            playerBan.ban_record.target_player = aPlayer;
                                            //Queue record for upload
                                            QueueRecordForProcessing(new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                source_name = "BanEnforcer",
                                                isIRO = false,
                                                server_id = _serverInfo.ServerID,
                                                target_name = aPlayer.player_name,
                                                target_player = aPlayer,
                                                command_type = GetCommandByKey("banenforcer_enforce"),
                                                command_numeric = (int)playerBan.ban_id,
                                                record_message = playerBan.ban_record.record_message + " [LINKED ACCOUNT " + strIDs + "]",
                                                record_time = UtcNow()
                                            });
                                        }
                                        else
                                        {
                                            Log.Error("Error fetching ban details to enforce.");
                                            continue;
                                        }
                                    }
                                    Log.Debug(() => "BAN ENFORCED on " + aPlayer.GetVerboseName(), 3);
                                    //Enforce the ban
                                    EnforceBan(playerBan, true);
                                }
                                else
                                {
                                    Log.Debug(() => "No ban found for player", 5);
                                    if (_serverInfo.ServerType != "OFFICIAL")
                                    {
                                        //Only call a hack check if the player does not already have a ban
                                        QueuePlayerForAntiCheatCheck(aPlayer);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("ban enforcer thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in ban enforcer thread. Skipping current loop.", e));
                    }
                }
                Log.Debug(() => "Ending Ban Enforcer Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in ban enforcer thread.", e));
            }
        }

        public override void OnBanAdded(CBanInfo ban)
        {
            if (!_pluginEnabled || !_UseBanEnforcer)
            {
                return;
            }
            //Log.Debug(() => "OnBanAdded fired", 6);
            ExecuteCommand("procon.protected.send", "banList.list");
        }

        public override void OnBanList(List<CBanInfo> banList)
        {
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                //Return if small duration (0.5 seconds) since last ban list, or if there is already a ban list going on
                if ((UtcNow() - _lastSuccessfulBanList) < TimeSpan.FromSeconds(0.5))
                {
                    Log.Debug(() => "Banlist being called quickly.", 4);
                    return;
                }
                if (_BansQueuing)
                {
                    Log.Error("Attempted banlist call rejected. Processing already in progress.");
                    return;
                }
                DateTime startTime = UtcNow();
                _lastSuccessfulBanList = startTime;
                if (!_pluginEnabled)
                {
                    return;
                }
                Log.Debug(() => "OnBanList fired", 5);
                if (_UseBanEnforcer)
                {
                    if (banList.Count > 0)
                    {
                        Log.Debug(() => "Bans found", 3);
                        lock (_CBanProcessingQueue)
                        {
                            //Only allow queueing of new bans if the processing queue is currently empty
                            if (_CBanProcessingQueue.Count == 0)
                            {
                                foreach (CBanInfo cBan in banList)
                                {
                                    Log.Debug(() => "Queuing Ban.", 7);
                                    _CBanProcessingQueue.Enqueue(cBan);
                                    _BansQueuing = true;
                                    if (UtcNow() - startTime > TimeSpan.FromSeconds(50))
                                    {
                                        Log.HandleException(new AException("OnBanList took longer than 50 seconds, exiting so procon doesn't panic."));
                                        _BansQueuing = false;
                                        return;
                                    }
                                }
                                _BansQueuing = false;
                            }
                        }
                    }
                }
                _DbCommunicationWaitHandle.Set();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured while listing procon bans.", e));
                _BansQueuing = false;
            }
        }

        public override void OnBanListClear()
        {
            Log.Debug(() => "Ban list cleared", 5);
        }

        public override void OnBanListSave()
        {
            Log.Debug(() => "Ban list saved", 5);
        }

        public override void OnBanListLoad()
        {
            Log.Debug(() => "Ban list loaded", 5);
        }

        private void QueuePlayerForAntiCheatCheck(APlayer aPlayer)
        {
            Log.Debug(() => "Entering queuePlayerForAntiCheatCheck", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue " + aPlayer.player_name + " for AntiCheat check", 6);
                    _AntiCheatCheckedPlayersStats.Remove(aPlayer.player_guid);
                    lock (_AntiCheatQueue)
                    {
                        if (_AntiCheatQueue.All(qPlayer => qPlayer.player_guid != aPlayer.player_guid))
                        {
                            _AntiCheatQueue.Enqueue(aPlayer);
                            Log.Debug(() => aPlayer.player_name + " queued for AntiCheat check", 6);
                            _AntiCheatWaitHandle.Set();
                        }
                        else
                        {
                            Log.Debug(() => aPlayer.player_name + " AntiCheat check cancelled; player already in queue.", 6);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing player for AntiCheat check.", e));
            }
            Log.Debug(() => "Exiting queuePlayerForAntiCheatCheck", 7);
        }

        public List<ASpecialPlayer> GetASPlayersOfGroup(String specialPlayerGroup)
        {
            Log.Debug(() => "Entering GetAsPlayersOfGroup", 8);
            try
            {
                lock (_baseSpecialPlayerCache)
                {
                    List<ASpecialPlayer> matchingSpecialPlayers = new List<ASpecialPlayer>();
                    matchingSpecialPlayers.AddRange(_baseSpecialPlayerCache.Values.Where(asPlayer =>
                        asPlayer.player_group != null &&
                        asPlayer.player_group.group_key == specialPlayerGroup));
                    return matchingSpecialPlayers;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching special players of group.", e));
            }
            Log.Debug(() => "Exiting GetAsPlayersOfGroup", 8);
            return null;
        }

        public List<ASpecialPlayer> GetVerboseASPlayersOfGroup(String specialPlayerGroup)
        {
            Log.Debug(() => "Entering GetVerboseASPlayersOfGroup", 8);
            try
            {
                lock (_baseSpecialPlayerCache)
                {
                    List<ASpecialPlayer> matchingSpecialPlayers = new List<ASpecialPlayer>();
                    matchingSpecialPlayers.AddRange(_verboseSpecialPlayerCache.Values.Where(asPlayer =>
                        asPlayer.player_group != null &&
                        asPlayer.player_group.group_key == specialPlayerGroup));
                    return matchingSpecialPlayers;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching verbose special players of group.", e));
            }
            Log.Debug(() => "Exiting GetVerboseASPlayersOfGroup", 8);
            return null;
        }

        public List<ASpecialPlayer> GetMatchingASPlayers(APlayer aPlayer)
        {
            Log.Debug(() => "Entering GetMatchingASPlayers", 8);
            try
            {
                lock (_baseSpecialPlayerCache)
                {
                    List<ASpecialPlayer> matchingSpecialPlayers = new List<ASpecialPlayer>();
                    matchingSpecialPlayers.AddRange(_baseSpecialPlayerCache.Values.Where(asPlayer =>
                        asPlayer != null &&
                        asPlayer.IsMatchingPlayer(aPlayer)));
                    return matchingSpecialPlayers;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching matching special players.", e));
            }
            Log.Debug(() => "Exiting GetMatchingASPlayers", 8);
            return null;
        }

        public List<ASpecialPlayer> GetMatchingVerboseASPlayers(APlayer aPlayer)
        {
            Log.Debug(() => "Entering GetMatchingVerboseASPlayers", 8);
            try
            {
                lock (_baseSpecialPlayerCache)
                {
                    List<ASpecialPlayer> matchingSpecialPlayers = new List<ASpecialPlayer>();
                    matchingSpecialPlayers.AddRange(_verboseSpecialPlayerCache.Values.Where(asPlayer =>
                        asPlayer != null &&
                        asPlayer.IsMatchingPlayer(aPlayer)));
                    return matchingSpecialPlayers;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching matching verbose special players.", e));
            }
            Log.Debug(() => "Exiting GetMatchingVerboseASPlayers", 8);
            return null;
        }

        public List<ASpecialPlayer> GetMatchingASPlayersOfGroup(String specialPlayerGroup, APlayer aPlayer)
        {
            Log.Debug(() => "Entering GetMatchingASPlayersOfGroup", 8);
            try
            {
                lock (_baseSpecialPlayerCache)
                {
                    List<ASpecialPlayer> matchingSpecialPlayers = new List<ASpecialPlayer>();
                    matchingSpecialPlayers.AddRange(_baseSpecialPlayerCache.Values.Where(asPlayer =>
                        asPlayer != null &&
                        asPlayer.IsMatchingPlayerOfGroup(aPlayer, specialPlayerGroup)));
                    return matchingSpecialPlayers;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching matching special players of group.", e));
            }
            Log.Debug(() => "Exiting GetMatchingASPlayersOfGroup", 8);
            return null;
        }

        public List<ASpecialPlayer> GetMatchingVerboseASPlayersOfGroup(String specialPlayerGroup, APlayer aPlayer)
        {
            Log.Debug(() => "Entering GetMatchingVerboseASPlayersOfGroup", 8);
            try
            {
                // Locking on the base cache is used for both base and verbose caches
                lock (_baseSpecialPlayerCache)
                {
                    List<ASpecialPlayer> matchingSpecialPlayers = new List<ASpecialPlayer>();
                    matchingSpecialPlayers.AddRange(_verboseSpecialPlayerCache.Values.Where(asPlayer =>
                        asPlayer != null &&
                        asPlayer.IsMatchingPlayerOfGroup(aPlayer, specialPlayerGroup)));
                    return matchingSpecialPlayers;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching matching verbose special players.", e));
            }
            Log.Debug(() => "Exiting GetMatchingVerboseASPlayersOfGroup", 8);
            return null;
        }

        public Dictionary<String, APlayer> GetOnlinePlayerDictionaryOfGroup(String specialPlayerGroup)
        {
            Dictionary<String, APlayer> onlinePlayersOfGroup = new Dictionary<String, APlayer>();
            Log.Debug(() => "Entering GetOnlinePlayerDictionaryOfGroup", 6);
            try
            {
                List<APlayer> onlinePlayerObjects = _PlayerDictionary.Values.ToList();
                List<ASpecialPlayer> asPlayerObjects = GetVerboseASPlayersOfGroup(specialPlayerGroup);
                foreach (ASpecialPlayer asPlayer in asPlayerObjects)
                {
                    foreach (APlayer aPlayer in onlinePlayerObjects)
                    {
                        if (asPlayer.player_object != null && asPlayer.player_object.player_id == aPlayer.player_id)
                        {
                            onlinePlayersOfGroup[aPlayer.player_name] = aPlayer;
                        }
                        else if (asPlayer.player_identifier == aPlayer.player_name || asPlayer.player_identifier == aPlayer.player_guid || asPlayer.player_identifier == aPlayer.player_ip)
                        {
                            onlinePlayersOfGroup[aPlayer.player_name] = aPlayer;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching matching special players.", e));
            }
            Log.Debug(() => "Exiting GetOnlinePlayerDictionaryOfGroup", 6);
            return onlinePlayersOfGroup;
        }

        public List<APlayer> GetOnlinePlayersOfGroup(String specialPlayerGroup)
        {
            Log.Debug(() => "Entering GetOnlinePlayersOfGroup", 6);
            try
            {
                return GetOnlinePlayerDictionaryOfGroup(specialPlayerGroup).Values.ToList();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching matching special players.", e));
            }
            Log.Debug(() => "Exiting GetOnlinePlayersOfGroup", 6);
            return null;
        }

        public Dictionary<String, APlayer> GetOnlinePlayerDictionaryWithoutGroup(String specialPlayerGroup)
        {
            Dictionary<String, APlayer> onlinePlayersWithoutGroup = new Dictionary<String, APlayer>();
            Log.Debug(() => "Entering GetOnlinePlayerDictionaryWithoutGroup", 6);
            try
            {
                List<APlayer> onlinePlayerObjects = _PlayerDictionary.Values.ToList();
                foreach (APlayer aPlayer in onlinePlayerObjects)
                {
                    if (!GetMatchingVerboseASPlayersOfGroup(specialPlayerGroup, aPlayer).Any())
                    {
                        onlinePlayersWithoutGroup[aPlayer.player_name] = aPlayer;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching matching special players.", e));
            }
            Log.Debug(() => "Exiting GetOnlinePlayerDictionaryWithoutGroup", 6);
            return onlinePlayersWithoutGroup;
        }

        public List<APlayer> GetOnlinePlayersWithoutGroup(String specialPlayerGroup)
        {
            Log.Debug(() => "Entering GetOnlinePlayersWithoutGroup", 6);
            try
            {
                return GetOnlinePlayerDictionaryWithoutGroup(specialPlayerGroup).Values.ToList();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching online players without group.", e));
            }
            Log.Debug(() => "Exiting GetOnlinePlayersWithoutGroup", 6);
            return null;
        }

        public Boolean PlayerProtected(APlayer aPlayer)
        {
            try
            {
                //Pull players from special player cache
                if (GetMatchingASPlayersOfGroup("whitelist_anticheat", aPlayer).Any())
                {
                    return true;
                }
                List<ASpecialPlayer> protectedList = GetVerboseASPlayersOfGroup("whitelist_anticheat");
                if (protectedList.Any())
                {
                    foreach (ASpecialPlayer asPlayer in protectedList)
                    {
                        if (asPlayer.player_object != null && asPlayer.player_object.player_id == aPlayer.player_id)
                        {
                            Log.Debug(() => aPlayer.GetVerboseName() + " protected from AntiCheat by database ID.", 2);
                            return true;
                        }
                        if (!String.IsNullOrEmpty(asPlayer.player_identifier))
                        {
                            if (aPlayer.player_name == asPlayer.player_identifier)
                            {
                                Log.Debug(() => aPlayer.GetVerboseName() + " protected from AntiCheat by NAME.", 2);
                                return true;
                            }
                            if (aPlayer.player_guid == asPlayer.player_identifier)
                            {
                                Log.Debug(() => aPlayer.GetVerboseName() + " protected from AntiCheat by GUID.", 2);
                                return true;
                            }
                            if (aPlayer.player_ip == asPlayer.player_identifier)
                            {
                                Log.Debug(() => aPlayer.GetVerboseName() + " protected from AntiCheat by IP.", 2);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching player protected status.", e));
            }
            return false;
        }
    }
}
