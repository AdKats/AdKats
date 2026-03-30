using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using PRoCon.Core;
using PRoCon.Core.Maps;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        public override void OnVersion(String serverType, String version)
        {
            _serverInfo.GamePatchVersion = version;
        }

        public override void OnTeamFactionOverride(Int32 targetTeamID, Int32 overrideTeamId)
        {
            if (!_acceptingTeamUpdates)
            {
                return;
            }
            try
            {
                switch (overrideTeamId)
                {
                    case -1:
                        //Check for already existing Neutral team
                        if (_serverInfo.GetRoundElapsedTime().TotalSeconds > 20 && _teamDictionary.ContainsKey(targetTeamID) && _teamDictionary[targetTeamID].TeamKey == "Neutral")
                        {
                            Log.Debug(() => "Neutral Team already set for team " + targetTeamID + ", cancelling override.", 4);
                            break;
                        }
                        _teamDictionary[targetTeamID] = new ATeam(this, targetTeamID, "Neutral", "Neutral Team", "Neutral Team");
                        Log.Debug(() => "Assigning team ID " + targetTeamID + " to Neutral ", 4);
                        break;
                    case 0:
                        switch (GameVersion)
                        {
                            case GameVersionEnum.BF3:
                            case GameVersionEnum.BF4:
                                //Check for already existing US team
                                if (_serverInfo.GetRoundElapsedTime().TotalSeconds > 20 && _teamDictionary.ContainsKey(targetTeamID) && _teamDictionary[targetTeamID].TeamKey == "US")
                                {
                                    Log.Debug(() => "Team US already set for team " + targetTeamID + ", cancelling override.", 4);
                                    break;
                                }
                                _teamDictionary[targetTeamID] = new ATeam(this, targetTeamID, "US", "US Army", "United States Army");
                                Log.Debug(() => "Assigning team ID " + targetTeamID + " to US ", 4);
                                break;
                            case GameVersionEnum.BFHL:
                                //Check for already existing US team
                                if (_serverInfo.GetRoundElapsedTime().TotalSeconds > 20 && _teamDictionary.ContainsKey(targetTeamID) && _teamDictionary[targetTeamID].TeamKey == "Cops")
                                {
                                    Log.Debug(() => "Team Cops already set for team " + targetTeamID + ", cancelling override.", 4);
                                    break;
                                }
                                _teamDictionary[targetTeamID] = new ATeam(this, targetTeamID, "LE", "Cops", "Law Enforcement");
                                Log.Debug(() => "Assigning team ID " + targetTeamID + " to Cops ", 4);
                                break;
                        }
                        break;
                    case 1:
                        switch (GameVersion)
                        {
                            case GameVersionEnum.BF3:
                            case GameVersionEnum.BF4:
                                //Check for already existing RU team
                                if (_serverInfo.GetRoundElapsedTime().TotalSeconds > 20 && _teamDictionary.ContainsKey(targetTeamID) && _teamDictionary[targetTeamID].TeamKey == "RU")
                                {
                                    Log.Debug(() => "Team RU already set for team " + targetTeamID + ", cancelling override.", 4);
                                    break;
                                }
                                _teamDictionary[targetTeamID] = new ATeam(this, targetTeamID, "RU", "Russian Army", "Russian Federation Army");
                                Log.Debug(() => "Assigning team ID " + targetTeamID + " to RU", 4);
                                break;
                            case GameVersionEnum.BFHL:
                                //Check for already existing RU team
                                if (_serverInfo.GetRoundElapsedTime().TotalSeconds > 20 && _teamDictionary.ContainsKey(targetTeamID) && _teamDictionary[targetTeamID].TeamKey == "Crims")
                                {
                                    Log.Debug(() => "Team Crims already set for team " + targetTeamID + ", cancelling override.", 4);
                                    break;
                                }
                                _teamDictionary[targetTeamID] = new ATeam(this, targetTeamID, "CR", "Crims", "Criminals");
                                Log.Debug(() => "Assigning team ID " + targetTeamID + " to Crims", 4);
                                break;
                        }
                        break;
                    case 2:
                        switch (GameVersion)
                        {
                            case GameVersionEnum.BF3:
                            case GameVersionEnum.BF4:
                                //Check for already existing CN team
                                if (_serverInfo.GetRoundElapsedTime().TotalSeconds > 20 && _teamDictionary.ContainsKey(targetTeamID) && _teamDictionary[targetTeamID].TeamKey == "CN")
                                {
                                    Log.Debug(() => "Team CN already set for team " + targetTeamID + ", cancelling override.", 4);
                                    break;
                                }
                                _teamDictionary[targetTeamID] = new ATeam(this, targetTeamID, "CN", "Chinese Army", "Chinese People's Liberation Army");
                                Log.Debug(() => "Assigning team ID " + targetTeamID + " to CN", 4);
                                break;
                            default:
                                Log.Error("Attempted to use team key 2 on non-BF3/BF4 server.");
                                break;
                        }
                        break;
                    default:
                        Log.Error("Team ID " + overrideTeamId + " was not understood.");
                        break;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while processing team faction override.", e));
            }
        }

        public override void OnFairFight(bool isEnabled)
        {
            _serverInfo.FairFightEnabled = isEnabled;
        }

        public override void OnIsHitIndicator(bool isEnabled)
        {
            _serverInfo.HitIndicatorEnabled = isEnabled;
        }

        public override void OnCommander(bool isEnabled)
        {
            _serverInfo.CommanderEnabled = isEnabled;
        }

        public override void OnForceReloadWholeMags(bool isEnabled)
        {
            _serverInfo.ForceReloadWholeMags = isEnabled;
        }

        public override void OnServerType(String serverType)
        {
            _serverInfo.ServerType = serverType;
        }

        public override void OnGameAdminLoad()
        {
            Log.Info("OnGameAdminLoad");
        }

        public override void OnGameAdminSave()
        {
            Log.Info("OnGameAdminSave");
        }

        public override void OnGameAdminPlayerAdded(String soldierName)
        {
            Log.Info("OnGameAdminPlayerAdded " + soldierName);
        }

        public override void OnGameAdminPlayerRemoved(String soldierName)
        {
            Log.Info("OnGameAdminPlayerRemoved " + soldierName);
        }

        public override void OnGameAdminCleared()
        {
            Log.Info("OnGameAdminCleared");
        }

        public override void OnGameAdminList(List<String> soldierNames)
        {
            foreach (string soldierName in soldierNames)
            {
                Log.Info("OnGameAdminList " + soldierName);
            }
        }

        public void UpdateFactions()
        {
            try
            {
                _acceptingTeamUpdates = true;
                _teamDictionary.Clear();
                _teamDictionary[0] = new ATeam(this, 0, "Neutral", "Neutrals", "Neutral Players");
                if (GameVersion == GameVersionEnum.BF3)
                {
                    OnTeamFactionOverride(1, 0);
                    OnTeamFactionOverride(2, 1);
                    OnTeamFactionOverride(3, 0);
                    OnTeamFactionOverride(4, 1);
                    _acceptingTeamUpdates = false;
                }
                else if (GameVersion == GameVersionEnum.BF4)
                {
                    Log.Debug(() => "Assigning team ID " + 0 + " to Spectator", 4);
                    Thread.Sleep(500);
                    ExecuteCommand("procon.protected.send", "vars.teamFactionOverride");
                    //Wait for proper team overrides to complete

                    if (!Threading.IsAlive("TeamAssignmentConfirmation"))
                    {
                        Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                        {
                            Thread.CurrentThread.Name = "TeamAssignmentConfirmation";
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                            DateTime starting = UtcNow();
                            while (true)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                if ((UtcNow() - starting).TotalSeconds > 30)
                                {
                                    Log.Warn("TeamAssignmentConfirmation took too long.");
                                    break;
                                }
                                if (!_teamDictionary.ContainsKey(1) ||
                                    !_teamDictionary.ContainsKey(2) ||
                                    !_teamDictionary.ContainsKey(3) ||
                                    !_teamDictionary.ContainsKey(4))
                                {
                                    Thread.Sleep(TimeSpan.FromSeconds(0.5));
                                    continue;
                                }
                                _acceptingTeamUpdates = false;
                                break;
                            }
                            Threading.StopWatchdog();
                        })));
                    }
                }
                else if (GameVersion == GameVersionEnum.BFHL)
                {
                    Log.Debug(() => "Assigning team ID " + 0 + " to Spectator", 4);
                    OnTeamFactionOverride(1, 0);
                    OnTeamFactionOverride(2, 1);
                    _acceptingTeamUpdates = false;
                }

                //Team power monitor assignment code
                if (_UseTeamPowerMonitorScrambler &&
                    _firstPlayerListComplete &&
                    _populationStatus != PopulationState.Low &&
                    !Threading.IsAlive("TeamPowerMonitorAssignment"))
                {
                    Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                    {
                        Thread.CurrentThread.Name = "TeamPowerMonitorAssignment";
                        Thread.Sleep(TimeSpan.FromSeconds(0.1));
                        DateTime starting = UtcNow();
                        List<APlayer> playerList;
                        while (true)
                        {
                            if (!_pluginEnabled)
                            {
                                break;
                            }
                            if ((UtcNow() - starting).TotalSeconds > 30)
                            {
                                Log.Warn("TeamPowerMonitorAssignment took too long.");
                                break;
                            }
                            if (_acceptingTeamUpdates)
                            {
                                Thread.Sleep(TimeSpan.FromSeconds(0.5));
                                continue;
                            }
                            ATeam team1, team2;
                            if (!GetTeamByID(1, out team1))
                            {
                                Log.Info("Team 1 was not found, waiting.");
                                Thread.Sleep(TimeSpan.FromSeconds(0.5));
                                continue;
                            }
                            if (!GetTeamByID(2, out team2))
                            {
                                Log.Info("Team 2 was not found, waiting.");
                                Thread.Sleep(TimeSpan.FromSeconds(0.5));
                                continue;
                            }

                            if (_RoundPrepSquads.Count() < 1)
                            {
                                Log.Warn("No squads were stored from the previous round!");
                            }
                            // Remove players from stored squads who have left the server
                            foreach (var squad in _RoundPrepSquads.ToList())
                            {
                                foreach (var aPlayer in squad.Players.ToList())
                                {
                                    if (!_PlayerDictionary.ContainsKey(aPlayer.player_name))
                                    {
                                        Log.Info("Removing " + aPlayer.player_name + " from stored squads, as they have left the server.");
                                        squad.Players.Remove(aPlayer);
                                    }
                                }
                                // If the squad contains 1 or fewer players, disband the squad so the player is available for other squads
                                if (squad.Players.Count() <= 1)
                                {
                                    _RoundPrepSquads.Remove(squad);
                                }
                            }
                            Log.Info(_RoundPrepSquads.Count() + " squads ready for dispersion.");

                            // Print the squad list
                            // Remove team IDs from the squads
                            foreach (var squad in _RoundPrepSquads.OrderBy(squad => squad.TeamID).ThenByDescending(squad => squad.Players.Sum(member => member.GetPower(true))))
                            {
                                Log.Info("Squad " + squad);
                                squad.TeamID = 0;
                                squad.SquadID = 0;
                            }

                            // Alternate between team 1 and 2 for dispersion every round
                            // This decides where the first (most powerful) squad is sent during dispersion
                            var requiredTeam = true;

                            //Decide which teams the squads should be on
                            foreach (var aSquad in _RoundPrepSquads.OrderByDescending(squad => squad.Players.Sum(member => member.GetPower(true))))
                            {

                                var team1Squads = _RoundPrepSquads.Where(dSquad => dSquad.TeamID == team1.TeamID).ToList();
                                var team1Count = team1Squads.Sum(dSquad => dSquad.Players.Count());
                                var team1Power = team1Squads.Sum(dSquad => dSquad.Players.Sum(member => member.GetPower(true)));
                                var team2Squads = _RoundPrepSquads.Where(dSquad => dSquad.TeamID == team2.TeamID).ToList();
                                var team2Count = team2Squads.Sum(dSquad => dSquad.Players.Count());
                                var team2Power = team2Squads.Sum(dSquad => dSquad.Players.Sum(member => member.GetPower(true)));

                                // Assume max team size of 32 unless otherwise provided
                                var maxTeamPlayerCount = 32;
                                if (_serverInfo.InfoObject != null && _serverInfo.InfoObject.MaxPlayerCount != maxTeamPlayerCount)
                                {
                                    maxTeamPlayerCount = _serverInfo.InfoObject.MaxPlayerCount / 2;
                                }

                                var team1Available = true;
                                if (team1Count + aSquad.Players.Count() > maxTeamPlayerCount)
                                {
                                    Log.Info("Cannot assign " + aSquad + " to team 1, max player count would be exceeded.");
                                    team1Available = false;
                                }
                                var team2Available = true;
                                if (team2Count + aSquad.Players.Count() > maxTeamPlayerCount)
                                {
                                    Log.Info("Cannot assign " + aSquad + " to team 2, max player count would be exceeded.");
                                    team2Available = false;
                                }

                                if (!team1Available && !team2Available)
                                {
                                    Log.Error("Major failure, both teams would be over capacity when assigning " + aSquad);
                                }
                                else
                                {
                                    Log.Info("Power: 1:" + team1Count + ":" + Math.Round(team1Power) + " | 2:" + team2Count + ":" + Math.Round(team2Power));
                                    if (requiredTeam)
                                    {
                                        if (currentStartingTeam1)
                                        {
                                            Log.Success("First squad required to be team 1.");
                                            AssignTeam(aSquad, team1, team1Squads);
                                        }
                                        else
                                        {
                                            Log.Success("First squad required to be team 2.");
                                            AssignTeam(aSquad, team2, team2Squads);
                                        }
                                    }
                                    else
                                    {
                                        if (team1Power + aSquad.GetPower() <= team2Power || !team2Available)
                                        {
                                            AssignTeam(aSquad, team1, team1Squads);
                                        }
                                        else
                                        {
                                            AssignTeam(aSquad, team2, team2Squads);
                                        }
                                    }
                                }
                                requiredTeam = false;
                            }
                            // Toggle the starting team value
                            currentStartingTeam1 = !currentStartingTeam1;

                            // Merge anyone currently not in a squad into an existing squad
                            // If more squads are needed, create them
                            Log.Info("Merging remaining players into " + _RoundPrepSquads.Count() + " available squads.");
                            // Only grab players who have a valid team and are of the player type
                            foreach (var aPlayer in _PlayerDictionary.Values.ToList().Where(dPlayer => (dPlayer.fbpInfo.TeamID == team1.TeamID || dPlayer.fbpInfo.TeamID == team2.TeamID) &&
                                                                                                       dPlayer.player_type == PlayerType.Player))
                            {
                                // See if this player is in an existing squad
                                Boolean found = false;
                                foreach (var aSquad in _RoundPrepSquads)
                                {
                                    if (aSquad.Players.Contains(aPlayer))
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    Boolean added = false;
                                    // Only add players to squads on the weak team
                                    var team1Squads = _RoundPrepSquads.Where(dSquad => dSquad.TeamID == team1.TeamID).ToList();
                                    var team1Count = team1Squads.Sum(dSquad => dSquad.Players.Count());
                                    var team1Power = team1Squads.Sum(dSquad => dSquad.Players.Sum(member => member.GetPower(true)));
                                    var team2Squads = _RoundPrepSquads.Where(dSquad => dSquad.TeamID == team2.TeamID).ToList();
                                    var team2Count = team2Squads.Sum(dSquad => dSquad.Players.Count());
                                    var team2Power = team2Squads.Sum(dSquad => dSquad.Players.Sum(member => member.GetPower(true)));

                                    // Assume max team size of 32 unless otherwise provided
                                    var maxTeamPlayerCount = 32;
                                    if (_serverInfo.InfoObject != null && _serverInfo.InfoObject.MaxPlayerCount != maxTeamPlayerCount)
                                    {
                                        maxTeamPlayerCount = _serverInfo.InfoObject.MaxPlayerCount / 2;
                                    }

                                    var team1Available = true;
                                    if (team1Count + 1 > maxTeamPlayerCount)
                                    {
                                        Log.Info("Cannot assign " + aPlayer.player_name + " to team 1, max player count would be exceeded.");
                                        team1Available = false;
                                    }
                                    var team2Available = true;
                                    if (team2Count + 1 > maxTeamPlayerCount)
                                    {
                                        Log.Info("Cannot assign " + aPlayer.player_name + " to team 2, max player count would be exceeded.");
                                        team2Available = false;
                                    }

                                    var chosenTeam = 0;
                                    if (!team1Available && !team2Available)
                                    {
                                        Log.Error("Major failure, both teams would be over capacity when assigning " + aPlayer.player_name);
                                    }
                                    else
                                    {
                                        Log.Info("Power: 1:" + team1Count + ":" + Math.Round(team1Power) + " | 2:" + team2Count + ":" + Math.Round(team2Power));
                                        if (team1Power + aPlayer.GetPower(true) <= team2Power || !team2Available)
                                        {
                                            chosenTeam = team1.TeamID;
                                        }
                                        else
                                        {
                                            chosenTeam = team2.TeamID;
                                        }

                                        // Add to the weakest squads first
                                        foreach (var aSquad in _RoundPrepSquads
                                                                .Where(aSquad => aSquad.TeamID == chosenTeam)
                                                                .OrderBy(dSquad => dSquad.Players.Sum(member => member.GetPower(true))))
                                        {
                                            if (aSquad.Players.Count() < (GameVersion == GameVersionEnum.BF3 ? 4 : 5))
                                            {
                                                Log.Info("Adding " + aPlayer.player_name + " to squad " + aSquad);
                                                aSquad.Players.Add(aPlayer);
                                                added = true;
                                                break;
                                            }
                                        }
                                        if (!added)
                                        {
                                            Log.Info("No squads available for " + aPlayer.player_name + ", creating new squad.");
                                            var newSquad = new ASquad(this)
                                            {
                                                TeamID = chosenTeam
                                                // Squad ID defaults to 0
                                            };
                                            newSquad.Players.Add(aPlayer);
                                            _RoundPrepSquads.Add(newSquad);
                                        }
                                    }
                                }
                            }

                            var t1Squads = _RoundPrepSquads.Where(dSquad => dSquad.TeamID == team1.TeamID).ToList();
                            var t1Count = t1Squads.Sum(dSquad => dSquad.Players.Count());
                            var t1Power = t1Squads.Sum(dSquad => dSquad.Players.Sum(member => member.GetPower(true)));
                            var t2Squads = _RoundPrepSquads.Where(dSquad => dSquad.TeamID == team2.TeamID).ToList();
                            var t2Count = t2Squads.Sum(dSquad => dSquad.Players.Count());
                            var t2Power = t2Squads.Sum(dSquad => dSquad.Players.Sum(member => member.GetPower(true)));

                            // Fix the team distribution counts if needed
                            if (Math.Abs(t1Count - t2Count) > 2)
                            {
                                if (t1Count > t2Count)
                                {
                                    // Team 1 needs a bad squad moved to team 2
                                    var worstSquad = t1Squads.OrderBy(dSquad => dSquad.Players.Sum(member => member.GetPower(true))).FirstOrDefault();
                                    if (worstSquad != null)
                                    {
                                        worstSquad.TeamID = team2.TeamID;
                                        Log.Info("REASSIGNED SQUAD TO TEAM 2: " + worstSquad);
                                    }
                                }
                                else
                                {
                                    // Team 2 needs a bad squad moved to team 1
                                    var worstSquad = t2Squads.OrderBy(dSquad => dSquad.Players.Sum(member => member.GetPower(true))).FirstOrDefault();
                                    if (worstSquad != null)
                                    {
                                        worstSquad.TeamID = team1.TeamID;
                                        Log.Info("REASSIGNED SQUAD TO TEAM 1: " + worstSquad);
                                    }
                                }
                            }

                            t1Squads = _RoundPrepSquads.Where(dSquad => dSquad.TeamID == team1.TeamID).ToList();
                            t1Count = t1Squads.Sum(dSquad => dSquad.Players.Count());
                            t1Power = t1Squads.Sum(dSquad => dSquad.Players.Sum(member => member.GetPower(true)));
                            t2Squads = _RoundPrepSquads.Where(dSquad => dSquad.TeamID == team2.TeamID).ToList();
                            t2Count = t2Squads.Sum(dSquad => dSquad.Players.Count());
                            t2Power = t2Squads.Sum(dSquad => dSquad.Players.Sum(member => member.GetPower(true)));

                            Double percDiff = Math.Abs(t1Power - t2Power) / ((t1Power + t2Power) / 2.0) * 100.0;
                            String message = "";
                            if (t1Power > t2Power)
                            {
                                message += "Team 1 up " + Math.Round(((t1Power - t2Power) / t2Power) * 100) + "% ";
                            }
                            else
                            {
                                message += "Team 2 up " + Math.Round(((t2Power - t1Power) / t1Power) * 100) + "% ";
                            }
                            message += "(1:" + t1Count + ":" + Math.Round(t1Power, 2) + " / 2:" + t2Count + ":" + Math.Round(t2Power, 2) + ")";
                            Log.Info("Team Power Dispersion: " + message);

                            // Print the final team squad lists
                            foreach (var squad in _RoundPrepSquads.OrderBy(squad => squad.TeamID).ThenByDescending(squad => squad.Players.Sum(member => member.GetPower(true))))
                            {
                                Log.Info("Squad " + squad);
                            }

                            var movesToTeam1 = new Queue<AMove>();
                            var movesToTeam2 = new Queue<AMove>();
                            // Build the list of player moves to satisfy the decisions
                            foreach (var squad in _RoundPrepSquads.ToList())
                            {
                                foreach (var aPlayer in squad.Players.ToList())
                                {
                                    var move = new AMove()
                                    {
                                        Player = aPlayer,
                                        Squad = squad
                                    };
                                    if (squad.TeamID == team1.TeamID)
                                    {
                                        movesToTeam1.Enqueue(move);
                                    }
                                    else if (squad.TeamID == team2.TeamID)
                                    {
                                        movesToTeam2.Enqueue(move);
                                    }
                                    else
                                    {
                                        Log.Error("Invalid team ID when building move list.");
                                    }
                                }
                            }

                            // Build the move queue such that we don't try to move to a full team
                            var currentTeam1Count = GetPlayerCount(true, true, true, 1);
                            var currentTeam2Count = GetPlayerCount(true, true, true, 2);
                            var moveList = new Queue<AMove>();
                            while (movesToTeam1.Any() || movesToTeam2.Any())
                            {
                                if (movesToTeam1.Any() && movesToTeam2.Any())
                                {
                                    // Both teams have available moves
                                    if (currentTeam1Count <= currentTeam2Count)
                                    {
                                        moveList.Enqueue(movesToTeam1.Dequeue());
                                        currentTeam1Count++;
                                    }
                                    else
                                    {
                                        moveList.Enqueue(movesToTeam2.Dequeue());
                                        currentTeam2Count++;
                                    }
                                }
                                else if (movesToTeam1.Any())
                                {
                                    // Only moves to team 1 are left
                                    moveList.Enqueue(movesToTeam1.Dequeue());
                                    currentTeam1Count++;
                                }
                                else
                                {
                                    // Only moves to team 2 are left
                                    moveList.Enqueue(movesToTeam2.Dequeue());
                                    currentTeam2Count++;
                                }
                            }

                            Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                            ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                            _MULTIBalancerUnswitcherDisabled = true;

                            playerList = _PlayerDictionary.Values.ToList();
                            Log.Success("Built move queue.");
                            Log.Info("Clearing squads.");
                            foreach (var aPlayer in playerList.Where(dPlayer => dPlayer.player_type == PlayerType.Player))
                            {
                                ExecuteCommand("procon.protected.send", "admin.movePlayer", aPlayer.player_name, aPlayer.fbpInfo.TeamID.ToString(), "0", "true");
                                Thread.Sleep(20);
                            }
                            Log.Success("Squads cleared.");
                            Log.Info("Moving teams.");
                            foreach (var aMove in moveList.ToList())
                            {
                                ExecuteCommand("procon.protected.send", "admin.movePlayer", aMove.Player.player_name, aMove.Squad.TeamID.ToString(), "0", "true");
                                if (aMove.Squad.TeamID == team1.TeamID)
                                {
                                    aMove.Player.RequiredTeam = team1;
                                }
                                else if (aMove.Squad.TeamID == team2.TeamID)
                                {
                                    aMove.Player.RequiredTeam = team2;
                                }
                                else
                                {
                                    Log.Error("Unable to assign required team for " + aMove.Player.player_name + ".");
                                }
                                Thread.Sleep(20);
                            }
                            Log.Success("Teams moved.");
                            Log.Info("Assigning squads.");
                            foreach (var aMove in moveList.ToList())
                            {
                                ExecuteCommand("procon.protected.send", "admin.movePlayer", aMove.Player.player_name, aMove.Squad.TeamID.ToString(), aMove.Squad.SquadID.ToString(), "true");
                                aMove.Player.RequiredSquad = aMove.Squad.SquadID;
                                Thread.Sleep(20);
                            }
                            Log.Success("Squads assigned.");

                            // Update the cached player list just in case
                            playerList = _PlayerDictionary.Values.ToList();
                            // Attempt to make sure every player stays on their assigned team/squad, despite the DICE balancer
                            while (playerList.Count() > 10 &&
                                   _roundState != RoundState.Playing)
                            {
                                foreach (var aPlayer in playerList.Where(dPlayer => !dPlayer.player_spawnedRound))
                                {
                                    if (_roundState == RoundState.Playing)
                                    {
                                        break;
                                    }
                                    if (!aPlayer.player_spawnedRound)
                                    {
                                        if (aPlayer.RequiredTeam != null)
                                        {
                                            if (aPlayer.fbpInfo.TeamID != aPlayer.RequiredTeam.TeamID || aPlayer.fbpInfo.SquadID != aPlayer.RequiredSquad)
                                            {
                                                ExecuteCommand("procon.protected.send", "admin.movePlayer", aPlayer.player_name, aPlayer.RequiredTeam.TeamID.ToString(), aPlayer.RequiredSquad.ToString(), "false");
                                                Thread.Sleep(50);
                                            }
                                        }
                                        else
                                        {
                                            // Choose a squad for the player

                                        }
                                    }
                                }
                                playerList = _PlayerDictionary.Values.ToList();
                            }

                            // Print the team squad lists
                            foreach (var squad in _RoundPrepSquads.OrderBy(squad => squad.TeamID).ThenByDescending(squad => squad.Players.Sum(member => member.GetPower(true))))
                            {
                                Log.Info("Squad " + squad);
                            }

                            _RoundPrepSquads.Clear();

                            break;
                        }
                        Log.Success("Team dispersion complete!");
                        Threading.Wait(TimeSpan.FromSeconds(15));
                        Log.Info("Checking players 1.");
                        playerList = _PlayerDictionary.Values.ToList();
                        foreach (var aPlayer in playerList)
                        {
                            if (aPlayer.RequiredTeam != null)
                            {
                                if (aPlayer.RequiredTeam.TeamID != aPlayer.fbpInfo.TeamID)
                                {
                                    Log.Warn("Dispersion: " + aPlayer.player_name + " assigned to " + aPlayer.RequiredTeam.TeamID + " but on " + aPlayer.fbpInfo.TeamID);
                                }
                            }
                            else
                            {
                                Log.Warn("Dispersion: " + aPlayer.player_name + " not assigned to a team.");
                            }
                        }
                        Log.Success("Team check 1 complete!");
                        Threading.Wait(TimeSpan.FromSeconds(30));
                        Log.Info("Checking players 2.");
                        playerList = _PlayerDictionary.Values.ToList();
                        foreach (var aPlayer in playerList)
                        {
                            if (aPlayer.RequiredTeam != null)
                            {
                                if (aPlayer.RequiredTeam.TeamID != aPlayer.fbpInfo.TeamID)
                                {
                                    Log.Warn("Dispersion: " + aPlayer.player_name + " assigned to " + aPlayer.RequiredTeam.TeamID + " but on " + aPlayer.fbpInfo.TeamID);
                                }
                            }
                            else
                            {
                                Log.Warn("Dispersion: " + aPlayer.player_name + " not assigned to a team.");
                            }
                        }
                        Log.Success("Team check 2 complete!");

                        Threading.StopWatchdog();
                    })));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while running faction updates.", e));
            }
        }

        private void AssignTeam(ASquad aSquad, ATeam aTeam, List<ASquad> squadList)
        {
            try
            {
                // Assign this squad to team
                aSquad.TeamID = aTeam.TeamID;
                Log.Info("Assigned " + aSquad + " to team " + aTeam.TeamID + ".");
                // Find the first available squad in team 2
                // Do not include the "None" squad
                var named = false;
                foreach (var squadID in ASquad.Names.Keys.ToList().Where(sqaudKey => sqaudKey != 0).Reverse())
                {
                    if (!squadList.Any(dSquad => dSquad.SquadID == squadID))
                    {
                        aSquad.SquadID = squadID;
                        named = true;
                        Log.Info("Named " + aSquad + ".");
                        break;
                    }
                }
                if (!named)
                {
                    Log.Error("Unable to name squad " + aSquad);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error assigning teams.", e));
            }
        }

        public override void OnMaplistLoad()
        {
            getMapInfo();
        }

        public override void OnMaplistSave()
        {
            getMapInfo();
        }

        public override void OnMaplistCleared()
        {
            getMapInfo();
        }

        public override void OnMaplistMapAppended(string mapFileName)
        {
            getMapInfo();
        }

        public override void OnMaplistNextLevelIndex(int mapIndex)
        {
            getMapInfo();
        }

        public override void OnMaplistMapRemoved(int mapIndex)
        {
            getMapInfo();
        }

        public override void OnMaplistMapInserted(int mapIndex, string mapFileName)
        {
            getMapInfo();
        }

        public void getMapInfo()
        {
            getMapList();
            getMapIndices();
        }

        public void getMapList()
        {
            ExecuteCommand("procon.protected.send", "mapList.list");
        }

        public void getMapIndices()
        {
            ExecuteCommand("procon.protected.send", "mapList.getMapIndices");
        }

        public override void OnMaplistList(List<MaplistEntry> lstMaplist)
        {
            Log.Debug(() => "Entering OnMaplistList", 5);
            if (!_pluginEnabled || _serverInfo == null)
            {
                return;
            }

            _serverInfo.SetMapList(lstMaplist);

            Log.Debug(() => "Exiting OnMaplistList", 7);
        }

        public override void OnMaplistGetMapIndices(int mapIndex, int nextIndex)
        {
            Log.Debug(() => "Entering OnMaplistGetMapIndices", 5);
            if (!_pluginEnabled || _serverInfo == null)
            {
                return;
            }

            _serverInfo.SetMapListIndicies(mapIndex, nextIndex);

            Log.Debug(() => "Exiting OnMaplistGetMapIndices", 7);
        }

        public override void OnPlayerTeamChange(String soldierName, Int32 teamId, Int32 squadId)
        {
            Log.Debug(() => "Entering OnPlayerTeamChange", 7);
            try
            {
                if (!_firstPlayerListComplete)
                {
                    return;
                }
                if (_PlayerDictionary.ContainsKey(soldierName))
                {
                    APlayer aPlayer = _PlayerDictionary[soldierName];
                    // Add to the move list
                    Boolean moveAccepted = true;
                    Boolean moveLoop = false;
                    if (_roundState == RoundState.Playing)
                    {
                        aPlayer.TeamMoves.Add(UtcNow());
                        // Check if there were 8 or more moves in the last 5 seconds
                        var movesLast5 = aPlayer.TeamMoves.Count(time => time > UtcNow().AddSeconds(-5));
                        if (movesLast5 >= 8 && NowDuration(aPlayer.JoinTime).TotalSeconds > 20)
                        {
                            // The player is stuck in a move loop, remove their required team and bow to whatever script/plugin is causing this
                            moveLoop = true;
                            var message = aPlayer.GetVerboseName() + " was stuck in a move loop.";
                            if (aPlayer.RequiredTeam != null)
                            {
                                aPlayer.RequiredTeam = null;
                                message += " Removing their required team.";
                            }
                            Log.Warn(message);
                            OnlineAdminSayMessage(message);
                        }
                    }
                    ATeam newTeam;
                    if (!GetTeamByID(teamId, out newTeam))
                    {
                        if (_roundState == RoundState.Playing)
                        {
                            Log.Error("Error fetching new team on team change.");
                        }
                        aPlayer.fbpInfo.TeamID = teamId;
                        aPlayer.fbpInfo.SquadID = squadId;
                        return;
                    }
                    ATeam oldTeam;
                    if (!GetTeamByID(aPlayer.fbpInfo.TeamID, out oldTeam))
                    {
                        if (_roundState == RoundState.Playing)
                        {
                            Log.Error("Error fetching old team on team change.");
                        }
                        aPlayer.fbpInfo.TeamID = teamId;
                        aPlayer.fbpInfo.SquadID = squadId;
                        return;
                    }
                    if (aPlayer.RequiredTeam != null &&
                        aPlayer.RequiredTeam.TeamKey != newTeam.TeamKey &&
                        (!PlayerIsAdmin(aPlayer) || !aPlayer.player_spawnedRound))
                    {
                        if (aPlayer.fbpInfo.TeamID == 0)
                        {
                            // They aren't officially on a team yet, just force the required team until that happens.
                            ExecuteCommand("procon.protected.send", "admin.movePlayer", soldierName, aPlayer.RequiredTeam.TeamID.ToString(), aPlayer.RequiredSquad > 0 ? aPlayer.RequiredSquad.ToString() : "1", "true");
                            moveAccepted = false;
                        }
                        else if (RunAssist(aPlayer, null, null, true) &&
                            _roundState == RoundState.Playing &&
                            _serverInfo.GetRoundElapsedTime().TotalMinutes > _minimumAssistMinutes)
                        {
                            if (_serverInfo.GetRoundElapsedTime().TotalMinutes > 3)
                            {
                                OnlineAdminSayMessage(Log.CViolet(aPlayer.GetVerboseName() + " (" + Math.Round(aPlayer.GetPower(true)) + ") REASSIGNED themselves from " + aPlayer.RequiredTeam.GetTeamIDKey() + " to " + newTeam.GetTeamIDKey() + "."));
                            }
                            aPlayer.RequiredTeam = newTeam;
                        }
                        else
                        {
                            if (_roundState == RoundState.Playing &&
                                NowDuration(aPlayer.lastSwitchMessage).TotalSeconds > 5)
                            {
                                if (_UseExperimentalTools)
                                {
                                    var message = aPlayer.GetVerboseName() + " (" + Math.Round(aPlayer.GetPower(true)) + ") attempted to switch teams after being assigned to " + aPlayer.RequiredTeam.GetTeamIDKey() + ".";
                                    ProconChatWrite(Log.CViolet(message));
                                }
                                PlayerTellMessage(aPlayer.player_name, Log.CViolet("You were assigned to " + aPlayer.RequiredTeam.TeamKey + ". Try using " + GetChatCommandByKey("self_assist") + " to switch."));
                                aPlayer.lastSwitchMessage = UtcNow();
                            }
                            moveAccepted = false;
                            var squadName = aPlayer.RequiredSquad > 0 ? ASquad.Names[aPlayer.RequiredSquad] : ASquad.Names[1];
                            Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                            ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                            _MULTIBalancerUnswitcherDisabled = true;
                            ExecuteCommand("procon.protected.send", "admin.movePlayer", soldierName, aPlayer.RequiredTeam.TeamID.ToString(), aPlayer.RequiredSquad > 0 ? aPlayer.RequiredSquad.ToString() : "1", "true");
                        }
                    }
                    ATeam team1, team2, winningTeam, losingTeam, powerTeam, weakTeam, mapUpTeam, mapDownTeam;
                    if (_firstPlayerListComplete &&
                        _roundState == RoundState.Playing &&
                        aPlayer.RequiredTeam == null &&
                        GetPlayerCount() > 15 &&
                        GetTeamByID(1, out team1) &&
                        GetTeamByID(2, out team2) &&
                        moveAccepted &&
                        !moveLoop)
                    {
                        // Wait for top stats
                        var startTime = UtcNow();
                        while (_pluginEnabled &&
                               !aPlayer.TopStats.Fetched &&
                               NowDuration(startTime).TotalSeconds < 10)
                        {
                            Threading.Wait(200);
                        }

                        // set up the team variables
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
                            mapUpTeam = team1;
                            mapDownTeam = team2;
                        }
                        else
                        {
                            mapUpTeam = team2;
                            mapDownTeam = team1;
                        }
                        var t1Power = team1.GetTeamPower(null, aPlayer);
                        var t2Power = team2.GetTeamPower(null, aPlayer);

                        var debugT1Power = t1Power;
                        if (_serverInfo.InfoObject.Map == "XP0_Metro" &&
                            _serverInfo.InfoObject.GameMode == "ConquestLarge0")
                        {
                            // If this is metro, overstate the power of the lower team slightly
                            // The upper team needs a slight stat boost over normal
                            var roundMinutes = _serverInfo.GetRoundElapsedTime().TotalMinutes;
                            if (team1 == mapUpTeam)
                            {
                                // If the lower team has the map, overstate its power even more
                                if ((team2.TeamTicketCount + 500 < team1.TeamTicketCount || roundMinutes < 10) &&
                                    _populationStatus == PopulationState.High)
                                {
                                    t1Power *= 1.35;
                                }
                                else
                                {
                                    t1Power *= 1.22;
                                }
                            }
                            else if (team1.TeamTicketCount + 500 > team2.TeamTicketCount)
                            {
                                if (_serverInfo.GetRoundElapsedTime().TotalMinutes <= 10)
                                {
                                    t1Power *= 1.12;
                                }
                                else if (_populationStatus == PopulationState.High)
                                {
                                    t1Power *= 1.08;
                                }
                            }
                        }
                        if (t1Power > t2Power)
                        {
                            powerTeam = team1;
                            weakTeam = team2;
                        }
                        else
                        {
                            powerTeam = team2;
                            weakTeam = team1;
                        }
                        var powerGap = Math.Abs(((t1Power - t2Power) / t2Power) * 100);
                        var players = _PlayerDictionary.Values.ToList();
                        var weakCount = players.Count(dPlayer => dPlayer.player_type == PlayerType.Player &&
                                                                 (dPlayer.fbpInfo.TeamID == weakTeam.TeamID || (dPlayer.RequiredTeam != null && dPlayer.RequiredTeam.TeamID == weakTeam.TeamID)));
                        var powerCount = players.Count(dPlayer => dPlayer.player_type == PlayerType.Player &&
                                                                  dPlayer.fbpInfo.TeamID == powerTeam.TeamID || (dPlayer.RequiredTeam != null && dPlayer.RequiredTeam.TeamID == powerTeam.TeamID));
                        var teamCountLeniency = 1;
                        // If it's not the early game, the server is populated, and the weak team is also losing, increase leniency to 2 players
                        if (_serverInfo.GetRoundElapsedTime().TotalMinutes >= 10 &&
                            weakTeam == losingTeam &&
                            // Require high population state
                            _populationStatus == PopulationState.High)
                        {
                            teamCountLeniency = 2;
                        }
                        // Assume max team size of 32 unless otherwise provided
                        var maxTeamPlayerCount = 32;
                        if (_serverInfo.InfoObject != null &&
                            _serverInfo.InfoObject.MaxPlayerCount != maxTeamPlayerCount)
                        {
                            maxTeamPlayerCount = _serverInfo.InfoObject.MaxPlayerCount / 2;
                        }

                        if (oldTeam.TeamKey == "Neutral")
                        {
                            // Do reassignment
                            if ((aPlayer.GetPower(true) > 8 || aPlayer.fbpInfo.Rank > 15) &&
                                _UseTeamPowerMonitorReassign)
                            {
                                if (!aPlayer.TopStats.Fetched)
                                {
                                    Log.Error(aPlayer.player_name + " assigned without top stats fetched.");
                                }

                                // If the current weak team is not map dominant, or is down by more than 30% power
                                // and it doesn't have too many players, assign the player to that team
                                var accepted = false;
                                var acceptReason = "None";
                                if (weakTeam == mapDownTeam)
                                {
                                    accepted = true;
                                    acceptReason = "Map";
                                }
                                else if (_UseTeamPowerMonitorReassignLenient &&
                                         powerGap > _TeamPowerMonitorReassignLenientPercent)
                                {
                                    accepted = true;
                                    acceptReason = "P-" + Math.Round(powerGap);
                                }
                                if (accepted &&
                                    weakCount - teamCountLeniency < powerCount &&
                                    weakCount < maxTeamPlayerCount)
                                {
                                    var message = Log.CViolet(aPlayer.GetVerboseName() + " (" + Math.Round(aPlayer.GetPower(true)) + ") join-assigned to " + weakTeam.GetTeamIDKey() + " [" + acceptReason + "].");
                                    if (_UseExperimentalTools)
                                    {
                                        ProconChatWrite(message);
                                    }
                                    moveAccepted = false;
                                    // Assign the team
                                    aPlayer.RequiredTeam = weakTeam;
                                    Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                                    ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                                    _MULTIBalancerUnswitcherDisabled = true;
                                    ExecuteCommand("procon.protected.send", "admin.movePlayer", aPlayer.player_name, aPlayer.RequiredTeam.TeamID.ToString(), "0", "true");
                                }
                            }
                        }
                        else if (_UseTeamPowerMonitorUnswitcher)
                        {
                            // Do unswitching
                            if (newTeam == powerTeam)
                            {
                                var message = Log.CViolet(aPlayer.GetVerboseName() + " (" + Math.Round(aPlayer.GetPower(true)) + ") unswitched back to " + weakTeam.GetTeamIDKey() + ".");
                                if (_UseExperimentalTools)
                                {
                                    ProconChatWrite(message);
                                }
                                aPlayer.Say(Log.CViolet("Unswitched back to " + weakTeam.GetTeamIDKey() + ". Try using " + GetChatCommandByKey("self_assist") + " to switch."));
                                moveAccepted = false;
                                Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                                ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                                _MULTIBalancerUnswitcherDisabled = true;
                                ExecuteCommand("procon.protected.send", "admin.movePlayer", aPlayer.player_name, weakTeam.TeamID.ToString(), "0", "true");
                            }
                        }
                    }
                    if (moveAccepted)
                    {
                        // Update their player object's team ID
                        aPlayer.fbpInfo.TeamID = teamId;

                        //If the player is queued for automatic assist, remove them from the queue
                        if (_AssistAttemptQueue.Any())
                        {
                            lock (_AssistAttemptQueue)
                            {
                                var matchingRecord = _AssistAttemptQueue.FirstOrDefault(assistRecord => assistRecord.target_player.player_id == aPlayer.player_id);
                                if (matchingRecord != null)
                                {
                                    //The player is queued, rebuild the queue without them in it
                                    SendMessageToSource(matchingRecord, Log.CViolet("You moved teams manually. Automatic assist cancelled."));
                                    _AssistAttemptQueue = new Queue<ARecord>(_AssistAttemptQueue.Where(assistRecord => assistRecord != matchingRecord));
                                }
                            }
                        }
                    }
                }
                else
                {
                    Log.Warn(soldierName + " switched to team " + teamId + " without being in player list.");
                    if (!_MissingPlayers.Contains(soldierName))
                    {
                        _MissingPlayers.Add(soldierName);
                    }
                }
                //When a player changes team, tell teamswap to recheck queues
                _TeamswapWaitHandle.Set();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player team change.", e));
            }
            Log.Debug(() => "Exiting OnPlayerTeamChange", 7);
        }

        public List<APlayer> GetSquadPlayers(APlayer aPlayer)
        {
            return GetSquadPlayers(aPlayer.fbpInfo.SquadID);
        }

        public List<APlayer> GetSquadPlayers(Int32 squadID)
        {
            return _PlayerDictionary.Values.ToList().Where(
                aPlayer => aPlayer.fbpInfo.SquadID == squadID).ToList();
        }

        public override void OnPlayerSquadChange(string soldierName, int teamId, int squadId)
        {
            Log.Debug(() => "Entering OnPlayerSquadChange", 7);
            try
            {
                if (!_firstPlayerListComplete)
                {
                    return;
                }
                if (_PlayerDictionary.ContainsKey(soldierName))
                {
                    APlayer aPlayer = _PlayerDictionary[soldierName];
                    ATeam newTeam;
                    if (!GetTeamByID(teamId, out newTeam))
                    {
                        if (_roundState == RoundState.Playing)
                        {
                            Log.Error("Error fetching new team on squad change.");
                        }
                        aPlayer.fbpInfo.TeamID = teamId;
                        aPlayer.fbpInfo.SquadID = squadId;
                        return;
                    }
                    ATeam oldTeam;
                    if (!GetTeamByID(aPlayer.fbpInfo.TeamID, out oldTeam))
                    {
                        if (_roundState == RoundState.Playing)
                        {
                            Log.Error("Error fetching old team on squad change.");
                        }
                        aPlayer.fbpInfo.TeamID = teamId;
                        aPlayer.fbpInfo.SquadID = squadId;
                        return;
                    }
                    Int32 oldSquad = aPlayer.fbpInfo.SquadID;
                    aPlayer.fbpInfo.SquadID = squadId;
                    if (aPlayer.RequiredTeam != null &&
                        aPlayer.RequiredSquad > 0 &&
                        aPlayer.RequiredSquad != squadId &&
                        // If they are being moved to the 'None' squad, don't try to move them back just yet
                        squadId != 0 &&
                        _roundState != RoundState.Playing)
                    {
                        Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                        ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                        _MULTIBalancerUnswitcherDisabled = true;
                        ExecuteCommand("procon.protected.send", "admin.movePlayer", soldierName, aPlayer.RequiredTeam.TeamID.ToString(), aPlayer.RequiredSquad.ToString(), "false");
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player squad change.", e));
            }
            Log.Debug(() => "Exiting OnPlayerSquadChange", 7);
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset cpsSubset)
        {
            Log.Debug(() => "Entering OnListPlayers", 7);
            try
            {
                //Only handle the list if it is an "All players" list
                if (cpsSubset.Subset == CPlayerSubset.PlayerSubsetType.All)
                {
                    DoPlayerListReceive();
                    //Return if small duration (1 second) since last accepted player list
                    //But only if the plugin hasn't just started up
                    if (NowDuration(_LastPlayerListAccept).TotalSeconds < 1.0 &&
                        NowDuration(_AdKatsRunningTime).TotalSeconds > 30)
                    {
                        return;
                    }
                    //Only perform the following if all threads are ready
                    if (_threadsReady)
                    {
                        QueuePlayerListForProcessing(players);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured while listing players.", e));
            }
            Log.Debug(() => "Exiting OnListPlayers", 7);
        }

        private void QueuePlayerListForProcessing(List<CPlayerInfo> players)
        {
            Log.Debug(() => "Entering QueuePlayerListForProcessing", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue player list for processing", 6);
                    lock (_PlayerListProcessingQueue)
                    {
                        _PlayerListProcessingQueue.Enqueue(players);
                        Log.Debug(() => "Player list queued for processing", 6);
                        _PlayerProcessingWaitHandle.Set();
                    }
                    DoPlayerListAccept();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing player list for processing.", e));
            }
            Log.Debug(() => "Exiting QueuePlayerListForProcessing", 7);
        }

        private void QueuePlayerForRemoval(CPlayerInfo player)
        {
            Log.Debug(() => "Entering QueuePlayerForRemoval", 7);
            try
            {
                if (_pluginEnabled && _firstPlayerListComplete)
                {
                    Log.Debug(() => "Preparing to queue player list for processing", 6);
                    lock (_PlayerRemovalProcessingQueue)
                    {
                        _PlayerRemovalProcessingQueue.Enqueue(player);
                        Log.Debug(() => "Player removal queued for processing", 6);
                        _PlayerProcessingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing player for removal.", e));
            }
            Log.Debug(() => "Exiting QueuePlayerForRemoval", 7);
        }
    }
}
