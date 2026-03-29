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
        public void OnPluginLoadingEnv(List<String> lstPluginEnv)
        {
            foreach (String env in lstPluginEnv)
            {
                Log.Debug(() => "^9OnPluginLoadingEnv: " + env, 7);
            }
            switch (lstPluginEnv[1])
            {
                case "BF3":
                    GameVersion = GameVersionEnum.BF3;
                    break;
                case "BF4":
                    GameVersion = GameVersionEnum.BF4;
                    break;
                case "BFHL":
                    GameVersion = GameVersionEnum.BFHL;
                    break;
            }
            Log.Success("^1Game Version: " + GameVersion);

            //Initialize the Email Handler
            _EmailHandler = new EmailHandler(this);

            //Initialize PushBullet Handler
            _PushBulletHandler = new PushBulletHandler(this);
        }

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
                                    worstSquad.TeamID = team2.TeamID;
                                    Log.Info("REASSIGNED SQUAD TO TEAM 2: " + worstSquad);
                                }
                                else
                                {
                                    // Team 2 needs a bad squad moved to team 1
                                    var worstSquad = t2Squads.OrderBy(dSquad => dSquad.Players.Sum(member => member.GetPower(true))).FirstOrDefault();
                                    worstSquad.TeamID = team1.TeamID;
                                    Log.Info("REASSIGNED SQUAD TO TEAM 1: " + worstSquad);
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
                                    if (_PlayerDictionary.ContainsKey(_debugSoldierName))
                                    {
                                        PlayerSayMessage(_debugSoldierName, message);
                                    }
                                    else
                                    {
                                        ProconChatWrite(Log.CViolet(message));
                                    }
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
                                    if (_PlayerDictionary.ContainsKey(_debugSoldierName))
                                    {
                                        PlayerSayMessage(_debugSoldierName, message);
                                    }
                                    else if (_UseExperimentalTools)
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
                                if (_PlayerDictionary.ContainsKey(_debugSoldierName))
                                {
                                    PlayerSayMessage(_debugSoldierName, message);
                                }
                                else if (_UseExperimentalTools)
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

        public override void OnRoundOverPlayers(List<CPlayerInfo> players)
        {
            _roundOverPlayers = players;
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

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            Log.Debug(() => "Entering OnPlayerSpawned", 7);
            try
            {
                APlayer aPlayer = null;
                if (_pluginEnabled && _threadsReady && _firstPlayerListComplete)
                {
                    //Fetch the player
                    if (!_PlayerDictionary.TryGetValue(soldierName, out aPlayer))
                    {
                        Log.Warn(soldierName + " spawned without being in player list.");
                        if (!_MissingPlayers.Contains(soldierName))
                        {
                            _MissingPlayers.Add(soldierName);
                        }
                        return;
                    }
                    aPlayer.player_spawnedRound = true;

                    //Ensure frostbite player info
                    if (aPlayer.fbpInfo == null)
                    {
                        return;
                    }

                    //Fetch teams
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
                    ATeam friendlyTeam, enemyTeam;
                    if (aPlayer.fbpInfo.TeamID == team1.TeamID)
                    {
                        friendlyTeam = team1;
                        enemyTeam = team2;
                    }
                    else
                    {
                        friendlyTeam = team2;
                        enemyTeam = team1;
                    }

                    if (_roundState == RoundState.Loaded)
                    {
                        _playingStartTime = UtcNow();
                        _roundState = RoundState.Playing;

                        //Take minimum ticket count between teams (accounts for rush), but not less than 0
                        _startingTicketCount = Math.Max(0, Math.Min(team1.TeamTicketCount, team2.TeamTicketCount));

                        if (EventActive())
                        {
                            Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                            {
                                Thread.CurrentThread.Name = "RoundWelcome";
                                Thread.Sleep(TimeSpan.FromSeconds(10));
                                AdminTellMessage("WELCOME TO ROUND EVENT " + String.Format("{0:n0}", _roundID) + "! " + GetEventMessage(false));
                                Int32 messages = 0;
                                while (messages++ < 10)
                                {
                                    Threading.Wait(TimeSpan.FromSeconds(3));
                                    AdminSayMessage(GetEventMessage(false) + " Use !rules for details.");
                                }
                                Threading.StopWatchdog();
                            })));
                        }
                        else if (_UseExperimentalTools && GameVersion == GameVersionEnum.BF4 && _serverInfo != null && _serverInfo.GetRoundElapsedTime().TotalSeconds < 30)
                        {
                            if (_serverInfo.ServerName.ToLower().Contains("metro") && _serverInfo.ServerName.ToLower().Contains("no explosives"))
                            {
                                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "RoundWelcome";
                                    Thread.Sleep(TimeSpan.FromSeconds(17));
                                    AdminTellMessage("Welcome to round " + String.Format("{0:n0}", _roundID) + " of No Explosives Metro!");
                                    Threading.StopWatchdog();
                                })));
                            }
                            else if (_serverInfo.ServerName.ToLower().Contains("locker") && _serverInfo.ServerName.ToLower().Contains("pistol"))
                            {
                                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "RoundWelcome";
                                    Thread.Sleep(TimeSpan.FromSeconds(17));
                                    AdminTellMessage("Welcome to round " + String.Format("{0:n0}", _roundID) + " of Pistols Only Locker!");
                                    Threading.StopWatchdog();
                                })));
                            }
                        }

                        if (_useRoundTimer)
                        {
                            StartRoundTimer();
                        }

                        if (ChallengeManager != null)
                        {
                            ChallengeManager.OnRoundPlaying(_roundID);
                        }
                    }

                    if (_CommandNameDictionary.Count > 0)
                    {
                        //Handle TeamSwap notifications
                        String command = GetChatCommandByKey("self_teamswap");
                        aPlayer.lastSpawn = UtcNow();
                        aPlayer.lastAction = UtcNow();

                        //Add matched spawn count
                        if (_unmatchedRoundDeaths.Contains(aPlayer.player_name))
                        {
                            friendlyTeam.IncrementTeamTicketAdjustment();
                        }
                        //Removed unmatched death if applicable
                        _unmatchedRoundDeaths.Remove(aPlayer.player_name);
                        //Decrement unmatched death count if applicable
                        if (_unmatchedRoundDeathCounts.ContainsKey(aPlayer.player_name))
                        {
                            _unmatchedRoundDeathCounts[aPlayer.player_name] = _unmatchedRoundDeathCounts[aPlayer.player_name] - 1;
                        }

                        if (aPlayer.player_aa && !aPlayer.player_aa_told)
                        {
                            String adminAssistantMessage = "You are an Admin Assistant. ";
                            if (!_UseAAReportAutoHandler && !_EnableAdminAssistantPerk)
                            {
                                adminAssistantMessage += "Thank you for your consistent reporting.";
                            }
                            else
                            {
                                adminAssistantMessage += "Perks: ";
                                if (_UseAAReportAutoHandler)
                                {
                                    adminAssistantMessage += "AutoAdmin can handle some of your reports. ";
                                }
                                if (_EnableAdminAssistantPerk)
                                {
                                    adminAssistantMessage += "You can use the " + command + " command.";
                                }
                            }
                            PlayerSayMessage(soldierName, adminAssistantMessage);
                            aPlayer.player_aa_told = true;
                        }
                    }

                    //Handle Dev Notifications
                    if (soldierName == "ColColonCleaner" && !_toldCol)
                    {
                        PlayerTellMessage("ColColonCleaner", "AdKats " + PluginVersion + " running!");
                        _toldCol = true;
                    }

                    var startDuration = NowDuration(_AdKatsStartTime).TotalSeconds;
                    var startupDuration = TimeSpan.FromSeconds(_startupDurations.Average(span => span.TotalSeconds)).TotalSeconds;
                    if (!aPlayer.player_spawnedOnce &&
                        ChallengeManager != null)
                    {
                        // Make sure that they have their challenge entry assigned if applicable
                        ChallengeManager.AssignActiveEntryForPlayer(aPlayer);
                    }
                    if (!aPlayer.player_spawnedOnce && startDuration - startupDuration > 120)
                    {
                        if (_ShowNewPlayerAnnouncement && aPlayer.player_new)
                        {
                            OnlineAdminSayMessage(aPlayer.GetVerboseName() + " just joined for the first time!");
                        }

                        if (_UseFirstSpawnMessage ||
                            (_battlecryVolume != BattlecryVolume.Disabled && !String.IsNullOrEmpty(aPlayer.player_battlecry)) ||
                            _UsePerkExpirationNotify)
                        {
                            Thread spawnPrinter = new Thread(new ThreadStart(delegate
                            {
                                Log.Debug(() => "Starting a spawn printer thread.", 5);
                                try
                                {
                                    Thread.CurrentThread.Name = "SpawnPrinter";

                                    //Wait 2 seconds
                                    Threading.Wait(2000);

                                    //Send perk expiration notification
                                    if (_UsePerkExpirationNotify)
                                    {
                                        var groups = GetMatchingVerboseASPlayers(aPlayer);
                                        var expiringGroups = groups.Where(group => NowDuration(group.player_expiration).TotalDays < _PerkExpirationNotifyDays);
                                        if (expiringGroups.Any())
                                        {
                                            PlayerTellMessage(aPlayer.player_name, "You have perks expiring soon. Use " + GetChatCommandByKey("player_perks") + " to see your perks.");
                                            Threading.Wait(TimeSpan.FromSeconds(_YellDuration));
                                        }
                                    }

                                    if (_battlecryVolume != BattlecryVolume.Disabled &&
                                        !String.IsNullOrEmpty(aPlayer.player_battlecry))
                                    {
                                        switch (_battlecryVolume)
                                        {
                                            case BattlecryVolume.Say:
                                                AdminSayMessage(aPlayer.player_battlecry);
                                                break;
                                            case BattlecryVolume.Yell:
                                                AdminYellMessage(aPlayer.player_battlecry);
                                                break;
                                            case BattlecryVolume.Tell:
                                                AdminTellMessage(aPlayer.player_battlecry);
                                                break;
                                        }
                                        Threading.Wait(TimeSpan.FromSeconds(_YellDuration));
                                    }
                                    else if (_UseFirstSpawnMessage)
                                    {
                                        PlayerTellMessage(aPlayer.player_name, _FirstSpawnMessage);
                                        Threading.Wait(TimeSpan.FromSeconds(_YellDuration));
                                    }

                                    int points = FetchPoints(aPlayer, false, true);
                                    if (_useFirstSpawnRepMessage)
                                    {
                                        Boolean isAdmin = PlayerIsAdmin(aPlayer);
                                        String repMessage = "Your reputation is " + Math.Round(aPlayer.player_reputation, 2) + ", with ";
                                        if (points > 0)
                                        {
                                            repMessage += points + " infraction point(s). ";
                                        }
                                        else
                                        {
                                            repMessage += "a clean infraction record. ";
                                        }
                                        PlayerTellMessage(aPlayer.player_name, repMessage);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.HandleException(new AException("Error while printing spawn messages", e));
                                }
                                Log.Debug(() => "Exiting a spawn printer.", 5);
                                Threading.StopWatchdog();
                            }));

                            //Start the thread
                            Threading.StartWatchdog(spawnPrinter);
                        }
                    }
                    aPlayer.player_spawnedOnce = true;

                    if (_ActOnSpawnDictionary.Count > 0)
                    {
                        lock (_ActOnSpawnDictionary)
                        {
                            ARecord record;
                            if (_ActOnSpawnDictionary.TryGetValue(soldierName, out record))
                            {
                                //Remove it from the dic
                                _ActOnSpawnDictionary.Remove(soldierName);
                                //Wait 1.5 seconds to take action (no "killed by admin" message in BF3 without this wait)
                                Threading.Wait(1500);
                                //Queue the action
                                QueueRecordForActionHandling(record);
                            }
                        }
                    }

                    if (_AutomaticForgives &&
                        aPlayer.player_infractionPoints > 0 &&
                        aPlayer.LastPunishment != null &&
                        (UtcNow() - aPlayer.LastPunishment.record_time).TotalDays > _AutomaticForgiveLastPunishDays &&
                        (aPlayer.LastForgive == null || (UtcNow() - aPlayer.LastForgive.record_time).TotalDays > _AutomaticForgiveLastForgiveDays))
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_forgive"),
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "InfractionManager",
                            record_message = "Auto-Forgiven for Clean Play",
                            record_time = UtcNow()
                        });
                    }

                    //Auto-Nuke Slay Duration
                    var duration = NowDuration(_lastNukeTime);
                    if (duration.TotalSeconds < _nukeAutoSlayActiveDuration &&
                        _lastNukeTeam != null &&
                        aPlayer.fbpInfo.TeamID == _lastNukeTeam.TeamID)
                    {
                        var endDuration = NowDuration(_lastNukeTime.AddSeconds(_nukeAutoSlayActiveDuration));
                        var durationRounded = Math.Round(endDuration.TotalSeconds, 1);
                        if (durationRounded > 0)
                        {
                            PlayerTellMessage(aPlayer.player_name, _lastNukeTeam.TeamKey + " nuke active for " + Math.Round(endDuration.TotalSeconds, 1) + " seconds!");
                            ExecuteCommand("procon.protected.send", "admin.killPlayer", aPlayer.player_name);
                        }
                    }

                    if (aPlayer.ActiveChallenge != null)
                    {
                        aPlayer.ActiveChallenge.AddSpawn(aPlayer);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player spawn.", e));
            }
            Log.Debug(() => "Exiting OnPlayerSpawned", 7);
        }

        public override void OnPlayerJoin(string soldierName)
        {
            Log.Debug(() => "Entering OnPlayerJoin", 7);
            try
            {
                if (_pluginEnabled &&
                    _firstPlayerListComplete &&
                    GameVersion == GameVersionEnum.BF4 &&
                    !String.IsNullOrEmpty(_vipKickedPlayerName))
                {
                    var matchingPlayer = GetFetchedPlayers().FirstOrDefault(aPlayer => aPlayer.player_name == soldierName);
                    if (matchingPlayer != null)
                    {
                        OnlineAdminSayMessage(_vipKickedPlayerName + " kicked for VIP " + matchingPlayer.GetVerboseName() + " to join.");
                    }
                    else
                    {
                        OnlineAdminSayMessage(_vipKickedPlayerName + " kicked for VIP " + soldierName + " to join.");
                    }
                    _vipKickedPlayerName = null;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player join.", e));
            }
            Log.Debug(() => "Exiting OnPlayerJoin", 7);
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            Log.Debug(() => "Entering OnPlayerLeft", 7);
            try
            {
                QueuePlayerForRemoval(playerInfo);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player left.", e));
            }
            Log.Debug(() => "Exiting OnPlayerLeft", 7);
        }

        public override void OnPlayerDisconnected(string soldierName, string reason)
        {
            Log.Debug(() => "Entering OnPlayerDisconnected", 7);
            try
            {
                if (_pluginEnabled &&
                    _firstPlayerListComplete &&
                    GameVersion == GameVersionEnum.BF4 &&
                    reason == "PLAYER_KICKED")
                {
                    var matchingPlayer = GetFetchedPlayers().FirstOrDefault(aPlayer => aPlayer.player_name == soldierName);
                    if (matchingPlayer != null)
                    {
                        _vipKickedPlayerName = matchingPlayer.GetVerboseName();
                    }
                    else
                    {
                        _vipKickedPlayerName = soldierName;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player disconnected.", e));
            }
            Log.Debug(() => "Exiting OnPlayerDisconnected", 7);
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

        public override void OnGlobalChat(String speaker, String message)
        {
            try
            {
                message = message.Trim();
                AChatMessage chatMessage = new AChatMessage()
                {
                    Speaker = speaker,
                    Message = message,
                    OriginalMessage = message,
                    Subset = AChatMessage.ChatSubset.Global,
                    Hidden = message.Trim().StartsWith("/"),
                    SubsetTeamID = -1,
                    SubsetSquadID = -1,
                    Timestamp = UtcNow()
                };
                APlayer aPlayer;
                if (_PlayerDictionary.TryGetValue(speaker, out aPlayer))
                {
                    if (aPlayer.fbpInfo != null)
                    {
                        chatMessage.SubsetTeamID = aPlayer.fbpInfo.TeamID;
                        chatMessage.SubsetSquadID = aPlayer.fbpInfo.SquadID;
                    }
                    aPlayer.player_chatOnce = true;
                }
                HandleChat(chatMessage);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error when handling OnGlobalChat", e));
            }
        }

        public override void OnTeamChat(String speaker, String message, Int32 teamId)
        {
            try
            {
                message = message.Trim();
                AChatMessage chatMessage = new AChatMessage()
                {
                    Speaker = speaker,
                    Message = message,
                    OriginalMessage = message,
                    Subset = AChatMessage.ChatSubset.Team,
                    Hidden = message.Trim().StartsWith("/"),
                    SubsetTeamID = teamId,
                    SubsetSquadID = -1,
                    Timestamp = UtcNow()
                };
                APlayer aPlayer;
                if (_PlayerDictionary.TryGetValue(speaker, out aPlayer))
                {
                    if (aPlayer.fbpInfo != null)
                    {
                        chatMessage.SubsetSquadID = aPlayer.fbpInfo.SquadID;
                    }
                }
                HandleChat(chatMessage);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error when handling OnTeamChat", e));
            }
        }

        public override void OnSquadChat(String speaker, String message, Int32 teamId, Int32 squadId)
        {
            try
            {
                message = message.Trim();
                AChatMessage chatMessage = new AChatMessage()
                {
                    Speaker = speaker,
                    Message = message,
                    OriginalMessage = message,
                    Subset = AChatMessage.ChatSubset.Squad,
                    Hidden = message.Trim().StartsWith("/"),
                    SubsetTeamID = teamId,
                    SubsetSquadID = squadId,
                    Timestamp = UtcNow()
                };
                HandleChat(chatMessage);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error when handling OnSquadChat", e));
            }
        }

        public override void OnReservedSlotsList(List<String> soldierNames)
        {
            try
            {
                Log.Debug(() => "Reserved slots listed.", 5);
                _CurrentReservedSlotPlayers = soldierNames;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling reserved slot list.", e));
            }
        }

        public override void OnSpectatorListList(List<String> soldierNames)
        {
            try
            {
                Log.Debug(() => "Spectators listed.", 5);
                _CurrentSpectatorListPlayers = soldierNames;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling spectator list.", e));
            }
        }

        public override void OnMaxSpectators(Int32 spectatorLimit)
        {
            _serverInfo.MaxSpectators = spectatorLimit;
        }

        public override void OnSpectatorListLoad()
        {
        }

        public override void OnSpectatorListSave()
        {
        }

        public override void OnSpectatorListPlayerAdded(String soldierName)
        {
        }

        public override void OnSpectatorListPlayerRemoved(String soldierName)
        {
        }

        public override void OnSpectatorListCleared()
        {
        }

    }
}
