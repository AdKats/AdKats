using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using MySqlConnector;
using Newtonsoft.Json;

using PRoCon.Core.Players.Items;

using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        // ===========================================================================================
        // Challenge Manager (lines 55567-61788 from original AdKats.cs)
        // ===========================================================================================

        public class AChallengeManager
        {
            private AdKats _plugin;

            // Settings
            public Boolean Enabled;
            public Boolean AutoPlay = true;
            public Boolean EnableServerRoundRules;
            public Boolean RandomPlayerRoundRules;
            public Int32 MinimumPlayers = 0;
            public Int32 CommandLockTimeoutHours = 24;

            public enum ChallengeState
            {
                Init,
                Loaded,
                Playing,
                Ended
            }

            // Runtime
            public Boolean Loading
            {
                get; private set;
            }
            public Boolean Loaded
            {
                get; private set;
            }
            private Boolean TriggerLoad;
            public List<CDefinition> Definitions;
            public List<CRule> Rules;
            public List<CReward> Rewards;
            public List<CEntry> Entries;
            private Int32 LoadedRoundID;
            // Round Rule
            public CRule RoundRule;
            private ChallengeState ChallengeRoundState = ChallengeState.Init;
            public List<CEntry> CompletedRoundEntries;

            // Timings
            private DateTime _LastDBReadAll = DateTime.UtcNow - TimeSpan.FromMinutes(30);

            public AChallengeManager(AdKats plugin)
            {
                _plugin = plugin;
                try
                {
                    Definitions = new List<CDefinition>();
                    Rules = new List<CRule>();
                    Entries = new List<CEntry>();
                    CompletedRoundEntries = new List<CEntry>();
                    Rewards = new List<CReward>();
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while creating challenge manager.", e));
                }
            }

            public void SetEnabled(Boolean enable)
            {
                try
                {
                    if (Enabled && !enable)
                    {
                        _plugin.Log.Success("Disabling challenge manager.");
                        CancelActiveRoundRule();
                        ChallengeRoundState = ChallengeState.Init;
                        Enabled = enable;
                    }
                    else if (!Enabled && enable)
                    {
                        _plugin.Log.Success("Enabling challenge manager.");
                        if (Loaded)
                        {
                            Enabled = enable;
                            if (_plugin._roundID <= 1)
                            {
                                _plugin.Log.Error("Round ID was invalid when starting challenge manager.");
                            }
                            else
                            {
                                OnRoundLoaded(_plugin._roundID);
                                if (_plugin._roundState == RoundState.Playing)
                                {
                                    // We're already playing. Trigger playing state.
                                    OnRoundPlaying(_plugin._roundID);
                                }
                            }
                        }
                        else
                        {
                            Enabled = enable;
                            TriggerLoad = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while reading all challenge manager DB info.", e));
                }
            }

            public String GetDefinitionEnum(Boolean includeNone)
            {
                try
                {
                    if (!Definitions.Any())
                    {
                        _plugin.Log.Error("Attempted to get definition enum with no definitions added.");
                        return null;
                    }
                    var rng = new Random(Environment.TickCount);
                    var enumString = String.Empty;
                    foreach (var defName in Definitions.OrderBy(def => def.Name)
                                                       .Select(def => def.Name))
                    {
                        if (String.IsNullOrEmpty(enumString))
                        {
                            enumString += "enum.ChallengeDefinitionEnum" + (includeNone ? "None" : "") + "_" + rng.Next(100000, 999999) + "(" + (includeNone ? "None|" : "");
                        }
                        else
                        {
                            enumString += "|";
                        }
                        enumString += defName;
                    }
                    enumString += ")";
                    return enumString;
                }
                catch (Exception e)
                {
                    Loading = false;
                    _plugin.Log.HandleException(new AException("Error while generating definition enum.", e));
                }
                return null;
            }

            public void HandleRead(MySqlConnection con, Boolean bypass)
            {
                try
                {
                    var localConnection = con;
                    if (localConnection == null)
                    {
                        localConnection = _plugin.GetDatabaseConnection();
                    }
                    try
                    {
                        if (_plugin.NowDuration(_LastDBReadAll).TotalMinutes > 5.0 || bypass)
                        {
                            if (Loading)
                            {
                                return;
                            }
                            Loading = true;
                            // -- LOAD --
                            DBReadDefinitions(con);
                            DBReadRules(con);
                            DBReadEntries(con);
                            DBReadRewards(con);
                            if (TriggerLoad)
                            {
                                if (_plugin._roundID <= 1)
                                {
                                    _plugin.Log.Error("Round ID was invalid when starting challenge manager.");
                                }
                                else
                                {
                                    OnRoundLoaded(_plugin._roundID);
                                    if (_plugin._roundState == RoundState.Playing)
                                    {
                                        // We're already playing. Trigger playing state.
                                        OnRoundPlaying(_plugin._roundID);
                                    }
                                }
                                TriggerLoad = false;
                            }
                            // -- END LOAD --
                            Loading = false;
                            Loaded = true;
                            _LastDBReadAll = _plugin.UtcNow();
                            _plugin.UpdateSettingPage();
                        }
                    }
                    finally
                    {
                        if (con == null &&
                            localConnection != null)
                        {
                            localConnection.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while reading all challenge manager DB info.", e));
                    Loading = false;
                }
            }

            public void DBReadDefinitions(MySqlConnection con)
            {
                try
                {
                    var localConnection = con;
                    if (localConnection == null)
                    {
                        localConnection = _plugin.GetDatabaseConnection();
                    }
                    try
                    {
                        using (MySqlCommand command = localConnection.CreateCommand())
                        {
                            command.CommandText = @"
                              SELECT `ID`,
                                     `Name`,
                                     `CreateTime`,
                                     `ModifyTime`
                                FROM `adkats_challenge_definition`
                            ORDER BY `ID` ASC";
                            var defReads = new List<CDefinition>();
                            var defPushes = new List<CDefinition>();
                            using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                            {
                                lock (Definitions)
                                {
                                    var validIDs = new List<Int64>();
                                    while (reader.Read())
                                    {
                                        var readID = reader.GetInt32("ID");
                                        var def = Definitions.FirstOrDefault(dDef => dDef.ID == readID);
                                        if (def == null)
                                        {
                                            def = new CDefinition(_plugin, this, false);
                                            def.ID = readID;
                                            Definitions.Add(def);
                                        }
                                        def.Name = reader.GetString("Name");
                                        var sanitizedName = def.Name.Replace("|", "");
                                        if (String.IsNullOrEmpty(sanitizedName))
                                        {
                                            _plugin.Log.Error("Challenge definition " + readID + " contained invalid name characters.");
                                            var rng = new Random(Environment.TickCount);
                                            // Assume we won't hit duplicates with 1 million (welp)
                                            def.Name = "CHG-" + def.ID + "-" + rng.Next(1000000);
                                            defPushes.Add(def);
                                        }
                                        else if (def.Name != sanitizedName)
                                        {
                                            _plugin.Log.Error("Challenge definition " + readID + " contained invalid name characters.");
                                            def.Name = sanitizedName;
                                            defPushes.Add(def);
                                        }
                                        def.CreateTime = reader.GetDateTime("CreateTime");
                                        def.ModifyTime = reader.GetDateTime("ModifyTime");
                                        defReads.Add(def);
                                        validIDs.Add(readID);
                                    }
                                    // Remove definitions as necessary
                                    foreach (var def in Definitions.Where(dDef => !validIDs.Contains(dDef.ID)).ToList())
                                    {
                                        _plugin.Log.Info("Removing definition " + def.ID + " from challenge manager. Definition was deleted from database.");
                                        Definitions.Remove(def);
                                    }
                                }
                            }
                            // These must be executed afterward, otherwise it would cause overlapping readers on the same connection
                            foreach (var def in defPushes)
                            {
                                def.DBPush(localConnection);
                            }
                            foreach (var def in defReads)
                            {
                                def.DBReadDetails(localConnection);
                            }
                        }
                    }
                    finally
                    {
                        if (con == null &&
                            localConnection != null)
                        {
                            localConnection.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error reading in definitions for challenge manager.", e));
                }
            }

            public List<CDefinition> GetDefinitions()
            {
                var defs = new List<CDefinition>();
                try
                {
                    lock (Definitions)
                    {
                        defs.AddRange(Definitions.OrderBy(def => def.ID).ToList());
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting list of definitions.", e));
                }
                return defs;
            }

            public CDefinition GetDefinition(Int64 defID)
            {
                try
                {
                    lock (Definitions)
                    {
                        if (!Definitions.Any())
                        {
                            return null;
                        }
                        return Definitions.FirstOrDefault(def => def.ID == defID);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting definition from manager.", e));
                }
                return null;
            }

            protected void DeleteDefinition(Int64 defID)
            {
                try
                {
                    lock (Definitions)
                    {
                        CDefinition def = Definitions.FirstOrDefault(dDef => dDef.ID == defID);
                        if (def == null)
                        {
                            _plugin.Log.Error("No definition exists with ID " + defID + ".");
                            return;
                        }
                        Definitions.Remove(def);
                        // Reload everything. Deleting a definition is huge.
                        HandleRead(null, true);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while deleting definition from manager.", e));
                }
            }

            public void CreateDefinition(String defName)
            {
                try
                {
                    lock (Definitions)
                    {
                        var newDef = new CDefinition(_plugin, this, true)
                        {
                            Name = defName
                        };
                        // Check if a definition exists with this ID
                        if (Definitions.Any(dDef => dDef.ID == newDef.ID))
                        {
                            _plugin.Log.Error("Definition with ID " + newDef.ID + " already exists.");
                            return;
                        }
                        // Check if a definition exists with this name
                        if (Definitions.Any(dDef => dDef.Name == newDef.Name))
                        {
                            _plugin.Log.Error("Definition called " + newDef.Name + " already exists.");
                            return;
                        }
                        if (newDef.Phantom)
                        {
                            // Try to push it to the database
                            newDef.DBPush(null);
                        }
                        if (newDef.ID <= 0)
                        {
                            _plugin.Log.Error("Defintion had invalid ID when adding to the challenge manager.");
                            return;
                        }
                        Definitions.Add(newDef);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while adding definition to manager.", e));
                }
            }

            public void DBReadRules(MySqlConnection con)
            {
                try
                {
                    if (_plugin._serverInfo == null ||
                        _plugin._serverInfo.ServerID <= 0)
                    {
                        _plugin.Log.Error("Unable to read challenge rules. Server info invalid.");
                        return;
                    }
                    var localConnection = con;
                    if (localConnection == null)
                    {
                        localConnection = _plugin.GetDatabaseConnection();
                    }
                    try
                    {
                        using (MySqlCommand command = localConnection.CreateCommand())
                        {
                            command.CommandText = @"
                              SELECT `ID`,
                                     `ServerID`,
                                     `DefID`,
                                     `Enabled`,
                                     `Name`,
                                     `Tier`,
                                     `CompletionType`,
                                     `RoundCount`,
                                     `DurationMinutes`,
                                     `DeathCount`,
                                     `CreateTime`,
                                     `ModifyTime`,
                                     `RoundLastUsedTime`,
                                     `PersonalLastUsedTime`
                                FROM `adkats_challenge_rule`
                               WHERE `ServerID` = @ServerID
                            ORDER BY `DefID` ASC, `ID` ASC";
                            command.Parameters.AddWithValue("@ServerID", _plugin._serverInfo.ServerID);
                            var ruleDeletes = new List<CRule>();
                            var rulePushes = new List<CRule>();
                            using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                            {
                                lock (Rules)
                                {
                                    var validIDs = new List<Int64>();
                                    while (reader.Read())
                                    {
                                        var add = false;
                                        var readID = reader.GetInt32("ID");
                                        var rule = Rules.FirstOrDefault(dRule => dRule.ID == readID);
                                        if (rule == null)
                                        {
                                            rule = new CRule(_plugin, this, false);
                                            rule.ID = readID;
                                            add = true;
                                        }
                                        var serverID = reader.GetInt64("ServerID");
                                        if (_plugin._serverInfo.ServerID != serverID)
                                        {
                                            _plugin.Log.Error("Invalid server ID when reading challenge rule " + rule.ID + ".");
                                            ruleDeletes.Add(rule);
                                            continue;
                                        }
                                        rule.ServerID = serverID;
                                        var definition = GetDefinition(reader.GetInt64("DefID"));
                                        if (definition == null)
                                        {
                                            _plugin.Log.Error("Invalid definition value when reading challenge rule " + rule.ID + ".");
                                            ruleDeletes.Add(rule);
                                            continue;
                                        }
                                        rule.Definition = definition;
                                        rule.Enabled = reader.GetBoolean("Enabled");
                                        rule.Name = reader.GetString("Name");
                                        var sanitizedName = rule.Name.Replace("|", "");
                                        if (String.IsNullOrEmpty(sanitizedName))
                                        {
                                            _plugin.Log.Error("Challenge rule " + rule.ID + " contained invalid name characters.");
                                            var rng = new Random(Environment.TickCount);
                                            // Assume we won't hit duplicates with 1 million (welp)
                                            rule.Name = "CHG-" + rule.ID + "-" + rng.Next(1000000);
                                            rulePushes.Add(rule);
                                        }
                                        else if (rule.Name != sanitizedName)
                                        {
                                            _plugin.Log.Error("Challenge rule " + rule.ID + " contained invalid name characters.");
                                            rule.Name = sanitizedName;
                                            rulePushes.Add(rule);
                                        }
                                        rule.Tier = reader.GetInt32("Tier");
                                        rule.Completion = (CRule.CompletionType)Enum.Parse(typeof(CRule.CompletionType), reader.GetString("CompletionType"));
                                        rule.RoundCount = reader.GetInt32("RoundCount");
                                        rule.DurationMinutes = reader.GetInt32("DurationMinutes");
                                        rule.DeathCount = reader.GetInt32("DeathCount");
                                        rule.CreateTime = reader.GetDateTime("CreateTime");
                                        rule.ModifyTime = reader.GetDateTime("ModifyTime");
                                        rule.RoundLastUsedTime = reader.GetDateTime("RoundLastUsedTime");
                                        rule.PersonalLastUsedTime = reader.GetDateTime("PersonalLastUsedTime");
                                        validIDs.Add(readID);
                                        if (add)
                                        {
                                            Rules.Add(rule);
                                        }
                                    }
                                    // Remove rules as necessary
                                    foreach (var rule in Rules.Where(dRule => !validIDs.Contains(dRule.ID)).ToList())
                                    {
                                        _plugin.Log.Info("Removing rule " + rule.ID + " from challenge manager. Rule was deleted from database.");
                                        Rules.Remove(rule);
                                    }
                                }
                            }
                            // These must be executed afterward, otherwise it would cause overlapping readers on the same connection
                            foreach (var rule in ruleDeletes)
                            {
                                rule.DBDelete(localConnection);
                            }
                            foreach (var rule in rulePushes)
                            {
                                rule.DBPush(localConnection);
                            }
                        }
                    }
                    finally
                    {
                        if (con == null &&
                            localConnection != null)
                        {
                            localConnection.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error reading in rules for challenge manager.", e));
                }
            }

            public List<CRule> GetRules()
            {
                var rules = new List<CRule>();
                try
                {
                    lock (Rules)
                    {
                        rules.AddRange(Rules.OrderBy(rule => rule.ID).ToList());
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting list of rules.", e));
                }
                return rules;
            }

            public CRule GetRule(Int64 ruleID)
            {
                try
                {
                    lock (Rules)
                    {
                        if (!Rules.Any())
                        {
                            return null;
                        }
                        return Rules.FirstOrDefault(rule => rule.ID == ruleID);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting rule from manager.", e));
                }
                return null;
            }

            protected void DeleteRule(Int64 ruleID)
            {
                try
                {
                    lock (Rules)
                    {
                        CRule rule = Rules.FirstOrDefault(dRule => dRule.ID == ruleID);
                        if (rule == null)
                        {
                            _plugin.Log.Error("No rule exists with ID " + ruleID + ".");
                            return;
                        }
                        Rules.Remove(rule);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while deleting rule from manager.", e));
                }
            }

            public void CreateRule(String definitionName)
            {
                try
                {
                    lock (Rules)
                    {
                        var rule = new CRule(_plugin, this, true);
                        if (String.IsNullOrEmpty(definitionName))
                        {
                            _plugin.Log.Error("Definition name was empty when creating challenge rule.");
                            return;
                        }
                        var definitions = GetDefinitions();
                        if (!definitions.Any())
                        {
                            _plugin.Log.Error("No definitions available when creating challenge rule.");
                            return;
                        }
                        var matchingDefinition = definitions.FirstOrDefault(def => def.Name == definitionName);
                        if (matchingDefinition == null)
                        {
                            _plugin.Log.Error("No matching definition when creating challenge rule.");
                            return;
                        }
                        rule.Definition = matchingDefinition;
                        var rules = GetRules();
                        Int64 oneLouder = 1;
                        if (rules.Any())
                        {
                            oneLouder += rules.Select(dRule => dRule.ID).Max();
                        }
                        rule.Name = rule.Definition.Name + " Rule " + oneLouder;
                        Rules.Add(rule);
                        rule.DBPush(null);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while creating definition detail in manager.", e));
                }
            }

            protected void DeleteReward(Int64 rewardID)
            {
                try
                {
                    lock (Rewards)
                    {
                        CReward reward = Rewards.FirstOrDefault(dReward => dReward.ID == rewardID);
                        if (reward == null)
                        {
                            _plugin.Log.Error("No reward exists with ID " + rewardID + ".");
                            return;
                        }
                        Rewards.Remove(reward);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while deleting reward from manager.", e));
                }
            }

            public void DBReadEntries(MySqlConnection con)
            {
                try
                {
                    var localConnection = con;
                    if (localConnection == null)
                    {
                        localConnection = _plugin.GetDatabaseConnection();
                    }
                    try
                    {
                        var startTime = _plugin.UtcNow();
                        using (MySqlCommand command = localConnection.CreateCommand())
                        {
                            command.CommandText = @"
                              SELECT `ace`.`ID`,
                                     `ace`.`PlayerID`,
                                     `ace`.`RuleID`,
                                     `ace`.`Completed`,
                                     `ace`.`Failed`,
                                     `ace`.`Canceled`,
                                     `ace`.`StartRound`,
                                     `ace`.`StartTime`,
                                     `ace`.`CompleteTime`
                                FROM `adkats_challenge_entry` `ace`
                                JOIN `adkats_challenge_rule` `acr`
                                  ON `ace`.`RuleID` = `acr`.`ID`
                               WHERE `acr`.`ServerID` = @ServerID
                                 AND `ace`.`Completed` = 0
                                 AND `ace`.`Canceled` = 0
                                 AND `ace`.`Failed` = 0
                            ORDER BY `PlayerID` ASC, `RuleID` ASC, `ID` ASC";
                            var entryChecks = new List<CEntry>();
                            command.Parameters.AddWithValue("@ServerID", _plugin._serverInfo.ServerID);
                            using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                            {
                                lock (Entries)
                                {
                                    while (reader.Read())
                                    {
                                        var readID = reader.GetInt64("ID");
                                        if (Entries.Any(dEntry => dEntry.ID == readID))
                                        {
                                            // This entry is already loaded. Ignore it.
                                            continue;
                                        }
                                        var playerID = reader.GetInt64("PlayerID");
                                        var ruleID = reader.GetInt64("RuleID");
                                        var startRound = reader.GetInt32("StartRound");
                                        var entry = new CEntry(_plugin, this, _plugin.FetchPlayer(false, false, false, null, playerID, null, null, null, null), GetRule(ruleID), startRound, false);
                                        entry.ID = readID;
                                        if (entry.Player == null)
                                        {
                                            _plugin.Log.Error("Unable to fetch player for Entry " + entry.ID + " and player " + playerID + ". Unable to read.");
                                            continue;
                                        }
                                        if (entry.Rule == null)
                                        {
                                            _plugin.Log.Error("Unable to fetch rule for Entry " + entry.ID + " and rule " + ruleID + ". Unable to read.");
                                            continue;
                                        }
                                        entry.Completed = reader.GetBoolean("Completed");
                                        entry.Failed = reader.GetBoolean("Failed");
                                        entry.Canceled = reader.GetBoolean("Canceled");
                                        entry.StartTime = reader.GetDateTime("StartTime");
                                        entry.CompleteTime = reader.GetDateTime("CompleteTime");
                                        entryChecks.Add(entry);
                                        Entries.Add(entry);
                                    }
                                }
                            }
                            // These must be executed afterward, otherwise it would cause overlapping readers on the same connection
                            foreach (var entry in entryChecks.Where(ent => !ent.Completed && !ent.Canceled && !ent.Failed))
                            {
                                // Check to see if the entry has failed
                                entry.CheckFailure();
                                // Read in the details and refresh progress
                                entry.DBReadDetails(localConnection);
                                if (entry.Progress == null)
                                {
                                    entry.RefreshProgress(null);
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (con == null &&
                            localConnection != null)
                        {
                            localConnection.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error reading in entries for challenge manager.", e));
                }
            }

            public List<CEntry> GetEntries()
            {
                var entries = new List<CEntry>();
                try
                {
                    lock (Entries)
                    {
                        entries.AddRange(Entries.OrderBy(entry => entry.ID).ToList());
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting list of entries.", e));
                }
                return entries;
            }

            public List<CEntry> GetEntriesForPlayer(APlayer player)
            {
                var entries = new List<CEntry>();
                if (player.player_id <= 0)
                {
                    _plugin.Log.Error("Unable to get entries for player. Player did not have a valid ID.");
                    return entries;
                }
                try
                {
                    lock (Entries)
                    {
                        entries.AddRange(Entries.Where(entry => entry.Player.player_id == player.player_id)
                                                .OrderBy(entry => entry.ID)
                                                .ToList());
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting list of entries for player.", e));
                }
                return entries;
            }

            public void AssignActiveEntryForPlayer(APlayer player)
            {
                try
                {
                    if (player.ActiveChallenge != null ||
                        _plugin.EventActive())
                    {
                        return;
                    }
                    // Order by ID so we can cancel older entries as needed
                    var activeEntries = GetEntriesForPlayer(player).Where(entry => !entry.Canceled &&
                                                                                   !entry.Completed &&
                                                                                   !entry.Failed)
                                                                   .OrderBy(entry => entry.ID)
                                                                   .ToList();
                    var cancelling = false;
                    while (activeEntries.Count() > 1)
                    {
                        var first = activeEntries.First();
                        // There is more than one active entry. Cancel this oldest one.
                        first.DoCancel();
                        // Remove the entry.
                        activeEntries.Remove(first);
                        // Repeat as needed.
                        cancelling = true;
                    }
                    if (cancelling && !activeEntries.Any())
                    {
                        _plugin.Log.Error("We cancelled too many things!!!!");
                    }
                    // Check for ignoring status
                    if (_plugin.GetMatchingVerboseASPlayersOfGroup("challenge_ignore", player).Any())
                    {
                        // They are ignoring challenges but have an active challenge. Cancel it.
                        var matching = activeEntries.FirstOrDefault();
                        if (matching != null)
                        {
                            matching.DoCancel();
                        }
                        return;
                    }
                    player.ActiveChallenge = activeEntries.FirstOrDefault();
                    // Secondary check in case we're doing random assignment of challenges
                    if (player.ActiveChallenge == null &&
                        EnableServerRoundRules &&
                        RandomPlayerRoundRules &&
                        AutoPlay &&
                        RoundRule == null &&
                        ChallengeRoundState == ChallengeState.Playing)
                    {
                        // Need to choose a random round rule for the player
                        CreateAndAssignRandomRoundEntry(player, true);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while assigning active challenge entry for player.", e));
                }
            }

            public void AddCompletedEntryForRound(CEntry entry)
            {
                try
                {
                    if (entry == null ||
                        entry.ID <= 0 ||
                        !entry.Details.Any())
                    {
                        _plugin.Log.Error("Tried to add completed entry when it was invalid.");
                        return;
                    }
                    lock (CompletedRoundEntries)
                    {
                        if (CompletedRoundEntries.Contains(entry))
                        {
                            _plugin.Log.Error("Tried to add completed entry " + entry.ID + " for round, but it was already added.");
                            return;
                        }
                        if (!entry.Completed)
                        {
                            _plugin.Log.Error("Tried to add completed entry " + entry.ID + " for round, but it wasn't completed.");
                            return;
                        }
                        CompletedRoundEntries.Add(entry);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while adding completed round entry.", e));
                }
            }

            public List<CEntry> GetCompletedRoundEntries()
            {
                try
                {
                    lock (CompletedRoundEntries)
                    {
                        return CompletedRoundEntries.ToList();
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting completed round entries.", e));
                }
                return new List<CEntry>();
            }

            public CEntry GetCompletedRoundEntryForPlayer(APlayer player)
            {
                try
                {
                    if (RoundRule == null)
                    {
                        return null;
                    }
                    lock (CompletedRoundEntries)
                    {
                        return CompletedRoundEntries.FirstOrDefault(entry => entry.Player.player_id == player.player_id &&
                                                                             entry.Rule == RoundRule);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting completed round entries for player.", e));
                }
                return null;
            }

            public void CreateAndAssignRandomRoundEntry(APlayer player, Boolean verbose)
            {
                try
                {
                    var roundRules = GetRules().Where(rule => rule.Enabled &&
                                                              rule.Tier == 1 &&
                                                              rule.Completion == CRule.CompletionType.Rounds &&
                                                              rule.RoundCount == 1 &&
                                                              rule.Definition.GetDetails().Any());
                    if (!roundRules.Any())
                    {
                        if (verbose)
                        {
                            player.Say("No active round challenges found.");
                        }
                        return;
                    }
                    // Assign a random rule from the available list
                    var rng = new Random(Environment.TickCount);
                    CreateAndAssignEntry(player, roundRules.OrderBy(rule => rng.Next()).First(), verbose);
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error creating and assigning random round entry.", e));
                }
            }

            public void CreateAndAssignRandomEntry(APlayer player, Int32 tier, Boolean verbose)
            {
                try
                {
                    if (tier < 0)
                    {
                        tier = 0;
                    }
                    if (tier > 10)
                    {
                        tier = 10;
                    }
                    var rules = GetRules().Where(rule => rule.Enabled &&
                                                         rule.Definition.GetDetails().Any())
                                          .OrderBy(rule => rule.Tier)
                                          .ThenBy(rule => rule.Name).ToList();
                    if (tier != 0)
                    {
                        rules = rules.Where(rule => rule.Tier == tier).ToList();
                    }
                    if (!rules.Any())
                    {
                        if (verbose)
                        {
                            player.Say("No matching challenges found.");
                        }
                        return;
                    }
                    // Assign a random rule from the available list
                    var rng = new Random(Environment.TickCount);
                    CreateAndAssignEntry(player, rules.OrderBy(rule => rng.Next()).First(), verbose);
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error creating and assigning random entry.", e));
                }
            }

            public void CreateAndAssignEntry(APlayer player, CRule rule, Boolean verbose)
            {
                try
                {
                    if (player == null ||
                        player.player_id <= 0)
                    {
                        _plugin.Log.Error("Player was invalid when creating entry.");
                        return;
                    }
                    // Check for ignoring status
                    if (_plugin.GetMatchingVerboseASPlayersOfGroup("challenge_ignore", player).Any())
                    {
                        _plugin.Log.Error("Player was ignoring challenges when trying to assign entry.");
                        return;
                    }
                    if (rule == null ||
                        rule.ID <= 0 ||
                        rule.ServerID != _plugin._serverInfo.ServerID ||
                        rule.Phantom)
                    {
                        _plugin.Log.Error("Rule was invalid when creating entry.");
                        return;
                    }
                    if (!rule.Enabled)
                    {
                        _plugin.Log.Error("Rule " + rule.ToString() + " was not enabled when creating entry.");
                        return;
                    }
                    if (ChallengeRoundState != ChallengeState.Playing)
                    {
                        if (ChallengeRoundState == ChallengeState.Init)
                        {
                            _plugin.Log.Error("Challenge system not initialized when assigning entry.");
                            return;
                        }
                        player.Say("Round not started. Please spawn to start the round before starting challenges.");
                        return;
                    }
                    if (LoadedRoundID <= 1)
                    {
                        _plugin.Log.Error("Loaded round ID was invalid when assigning entry.");
                        return;
                    }
                    lock (Entries)
                    {
                        // Cancel any existing entries for the player
                        if (player.ActiveChallenge != null)
                        {
                            player.ActiveChallenge.DoCancel();
                        }
                        // Create the new entry
                        var newEntry = new CEntry(_plugin, this, player, rule, LoadedRoundID, true);
                        newEntry.DBPush(null);
                        if (newEntry.Phantom)
                        {
                            _plugin.Log.Error("Unable to create challenge entry for " + player.GetVerboseName() + ", could not upload to database.");
                            return;
                        }
                        if (verbose)
                        {
                            var commandText = _plugin.GetChatCommandByKey("self_challenge");
                            player.Say(_plugin.Log.CPink("Now playing " + rule.Name + " challenge. For more info use " + commandText));
                            if (_plugin.GetPlayerCount() < MinimumPlayers)
                            {
                                player.Say("Challenges do not gain progress until " + MinimumPlayers + " active players.");
                            }
                        }
                        newEntry.RefreshProgress(null);
                        Entries.Add(newEntry);
                        player.ActiveChallenge = newEntry;
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while creating and assigning challenge entry in manager.", e));
                }
            }

            public void AssignRoundChallengeIfKillValid(AKill kill)
            {
                try
                {
                    if (RoundRule == null)
                    {
                        // We don't have a round rule. Nothing to do here.
                        return;
                    }
                    if (kill == null ||
                        kill.killer == null ||
                        kill.killer.player_id <= 0)
                    {
                        _plugin.Log.Error("Kill was invalid when assigning round challenge.");
                        return;
                    }
                    var player = kill.killer;
                    // Check ignoring case
                    if (_plugin.GetMatchingVerboseASPlayersOfGroup("challenge_ignore", player).Any())
                    {
                        return;
                    }
                    // Check whitelisting case
                    if (!AutoPlay && !_plugin.GetMatchingVerboseASPlayersOfGroup("challenge_play", player).Any())
                    {
                        return;
                    }
                    // Check to see if they should have an active challenge already assigned
                    if (player.ActiveChallenge == null)
                    {
                        AssignActiveEntryForPlayer(player);
                    }
                    if (player.ActiveChallenge == null)
                    {
                        // Only create the entry if the current kill is valid for the round rule
                        if (RoundRule.KillValid(kill) &&
                            // Make sure they haven't completed the round challenge already
                            GetCompletedRoundEntryForPlayer(player) == null)
                        {
                            CreateAndAssignEntry(player, RoundRule, true);
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while assigning challenge if kill valid.", e));
                }
            }

            public List<CReward> GetRewards()
            {
                var rewards = new List<CReward>();
                try
                {
                    lock (Rewards)
                    {
                        rewards.AddRange(Rewards.OrderBy(reward => reward.ID).ToList());
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting list of rewards.", e));
                }
                return rewards;
            }

            public CReward GetReward(Int64 rewardID)
            {
                try
                {
                    lock (Rewards)
                    {
                        if (!Rewards.Any())
                        {
                            return null;
                        }
                        return Rewards.FirstOrDefault(dReward => dReward.ID == rewardID);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting reward from manager.", e));
                }
                return null;
            }

            public void CreateReward(Int32 tier)
            {
                try
                {
                    lock (Rewards)
                    {
                        var newReward = new CReward(_plugin, this, true)
                        {
                            Tier = tier
                        };
                        if (newReward == null)
                        {
                            _plugin.Log.Error("Reward was null when adding to the challenge manager.");
                            return;
                        }
                        // Check if a reward exists with this tier and reward type
                        if (Rewards.Any(dReward => dReward.Tier == newReward.Tier && dReward.Reward == newReward.Reward))
                        {
                            _plugin.Log.Error("Reward with tier " + newReward.Tier + " and reward " + newReward.Reward.ToString() + " already exists.");
                            return;
                        }
                        if (newReward.Phantom)
                        {
                            // Try to push it to the database
                            newReward.DBPush(null);
                        }
                        if (newReward.ID <= 0)
                        {
                            _plugin.Log.Error("Reward had invalid ID when adding to the challenge manager.");
                            return;
                        }
                        Rewards.Add(newReward);
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while adding reward to manager.", e));
                }
            }

            public void DBReadRewards(MySqlConnection con)
            {
                try
                {
                    var localConnection = con;
                    if (localConnection == null)
                    {
                        localConnection = _plugin.GetDatabaseConnection();
                    }
                    try
                    {
                        using (MySqlCommand command = localConnection.CreateCommand())
                        {
                            command.CommandText = @"
                            SELECT `ID`,
                                   `ServerID`,
                                   `Tier`,
                                   `Reward`,
                                   `Enabled`,
                                   `DurationMinutes`,
                                   `CreateTime`,
                                   `ModifyTime`
                            FROM `adkats_challenge_reward`
                            WHERE `ServerID` = @ServerID
                            ORDER BY `ID` ASC";
                            command.Parameters.AddWithValue("@ServerID", _plugin._serverInfo.ServerID);
                            using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                            {
                                lock (Rewards)
                                {
                                    var validIDs = new List<Int64>();
                                    while (reader.Read())
                                    {
                                        var readID = reader.GetInt32("ID");
                                        var reward = Rewards.FirstOrDefault(dReward => dReward.ID == readID);
                                        if (reward == null)
                                        {
                                            reward = new CReward(_plugin, this, false);
                                            reward.ID = readID;
                                            Rewards.Add(reward);
                                        }
                                        reward.ServerID = reader.GetInt64("ServerID");
                                        if (reward.ServerID != _plugin._serverInfo.ServerID)
                                        {
                                            _plugin.Log.Error("CReward " + this.ToString() + " was loaded, but belongs to another server.");
                                        }
                                        reward.Tier = reader.GetInt32("Tier");
                                        reward.Reward = (CReward.RewardType)Enum.Parse(typeof(CReward.RewardType), reader.GetString("Reward"));
                                        reward.Enabled = reader.GetBoolean("Enabled");
                                        reward.DurationMinutes = reader.GetInt32("DurationMinutes");
                                        reward.CreateTime = reader.GetDateTime("CreateTime");
                                        reward.ModifyTime = reader.GetDateTime("ModifyTime");
                                        validIDs.Add(readID);
                                    }
                                    // Remove definitions as necessary
                                    foreach (var reward in Rewards.Where(dReward => !validIDs.Contains(dReward.ID)).ToList())
                                    {
                                        _plugin.Log.Info("Removing reward " + reward.ID + " from challenge manager. Reward was deleted from database.");
                                        Rewards.Remove(reward);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (con == null &&
                            localConnection != null)
                        {
                            localConnection.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error reading in rewards for challenge manager.", e));
                }
            }

            public void CancelActiveRoundRule()
            {
                try
                {
                    if (RoundRule != null)
                    {
                        // Remove the current rule
                        RoundRule = null;
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while cancelling active round rule entries.", e));
                }
            }

            public void FailActiveRoundRule()
            {
                try
                {
                    if (RoundRule != null)
                    {
                        // Get the active challenges being played for the current round rule.
                        foreach (var activeEntry in _plugin._PlayerDictionary.Values.ToList()
                                                            .Where(dPlayer => dPlayer.ActiveChallenge != null &&
                                                                                dPlayer.ActiveChallenge.Rule == RoundRule)
                                                            .Select(dPlayer => dPlayer.ActiveChallenge))
                        {
                            // Cancel all active entries for this rule, since it's being changed.
                            activeEntry.DoFail();
                        }
                        // Remove the current rule
                        RoundRule = null;
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while failing active round rule entries.", e));
                }
            }

            public String GetChallengeInfo(APlayer aPlayer, Boolean description)
            {
                try
                {
                    if (!Enabled)
                    {
                        return "Challenge manager not enabled.";
                    }
                    if (_plugin.EventActive())
                    {
                        return "Challenges are not enabled during server events.";
                    }
                    if (aPlayer == null ||
                        aPlayer.player_id <= 0)
                    {
                        _plugin.Log.Error("Tried to get challenge info for an invalid player.");
                        return "ERROR758";
                    }
                    // Get active entries for the current player.
                    var commandText = _plugin.GetChatCommandByKey("self_challenge");
                    AssignActiveEntryForPlayer(aPlayer);
                    var completedRoundEntry = GetCompletedRoundEntryForPlayer(aPlayer);
                    if (aPlayer.ActiveChallenge == null)
                    {
                        // They aren't playing a challenge yet.
                        // Add them to the current round challenge automatically if one is running.
                        if (RoundRule == null)
                        {
                            // No round challenge. Tell them how they can join a challenge.
                            return "No round challenge active. Choose a new challenge with " + commandText + " list";
                        }
                        // We have a round rule, good, but make sure they haven't completed the round challenge already
                        if (completedRoundEntry == null)
                        {
                            CreateAndAssignEntry(aPlayer, RoundRule, false);
                        }
                    }
                    var completedRoundEntryString = "";
                    if (completedRoundEntry != null)
                    {
                        completedRoundEntryString += "COMPLETED " + completedRoundEntry.Rule.Name.ToUpper() + " ROUND CHALLENGE" + Environment.NewLine;
                    }
                    if (aPlayer.ActiveChallenge == null)
                    {
                        return completedRoundEntryString + "Choose a new challenge with " + commandText + " list";
                    }
                    // The player is available and they have entries.
                    var challenge = aPlayer.ActiveChallenge;
                    var completionTimeString = "Challenge Ends: ";
                    if (challenge.Rule.Completion == CRule.CompletionType.Rounds)
                    {
                        // Completion round is the same round if RoundCount = 1
                        var completionRoundID = challenge.StartRound + challenge.Rule.RoundCount;
                        var remainingRounds = completionRoundID - LoadedRoundID;
                        if (remainingRounds == 1)
                        {
                            completionTimeString += "End of this round.";
                        }
                        else if (remainingRounds == 2)
                        {
                            completionTimeString += "End of next round.";
                        }
                        else
                        {
                            completionTimeString += remainingRounds + "rounds from now.";
                        }
                    }
                    else if (challenge.Rule.Completion == CRule.CompletionType.Duration)
                    {
                        var completionTime = challenge.StartTime + TimeSpan.FromMinutes(challenge.Rule.DurationMinutes);
                        var completionDuration = _plugin.NowDuration(completionTime);

                        if (_plugin.UtcNow() > completionTime)
                        {
                            completionTimeString += "In a few seconds.";
                        }
                        else
                        {
                            completionTimeString += "In " + Math.Round(completionDuration.TotalMinutes, 1) + "m (" + _plugin.FormatTimeString(completionDuration, 3) + ")";
                        }
                    }
                    else if (challenge.Rule.Completion == CRule.CompletionType.Deaths)
                    {
                        var deaths = challenge.Progress.Deaths.Count();
                        var deathsRemaining = challenge.Rule.DeathCount - deaths;

                        if (deathsRemaining <= 0)
                        {
                            completionTimeString += "ERROR17592";
                        }
                        else
                        {
                            completionTimeString += "If you get " + deathsRemaining + " " + (deaths > 0 ? "more " : "") + "deaths.";
                        }
                    }
                    else
                    {
                        completionTimeString += "ERROR28217";
                    }
                    completionTimeString += Environment.NewLine;
                    var rewardString = "";
                    var matchingRewards = GetRewards().Where(dReward => dReward.Tier == challenge.Rule.Tier &&
                                                                        dReward.Enabled &&
                                                                        dReward.Reward != CReward.RewardType.None);
                    if (matchingRewards.Any())
                    {
                        var rewardStrings = matchingRewards.OrderBy(dReward => dReward.Reward.ToString())
                                                           .Select(dReward => dReward.getDescriptionString(aPlayer))
                                                           .Distinct();
                        rewardString = String.Join(", ", rewardStrings.ToArray());
                    }
                    var info = "";
                    if (description)
                    {
                        info += aPlayer.GetVerboseName() + " " + challenge.Rule.Name.ToUpper() + " CHALLENGE" + Environment.NewLine;
                        info += completionTimeString;
                        if (!String.IsNullOrEmpty(rewardString))
                        {
                            info += "Rewards: " + rewardString + Environment.NewLine;
                        }
                        info += challenge.Rule.RuleInfo() + Environment.NewLine;
                        info += "To see your progress type: " + commandText + " p" + Environment.NewLine;
                    }
                    else
                    {
                        info += "Status: " + Math.Round(challenge.Progress.CompletionPercentage) + "% | " + challenge.Progress.TotalCompletedKills + " Kills | " + challenge.Progress.TotalRequiredKills + " Required" + Environment.NewLine;
                        info += completionTimeString;
                        if (!String.IsNullOrEmpty(rewardString))
                        {
                            info += "Rewards: " + rewardString + Environment.NewLine;
                        }
                        info += challenge.Progress.ToString();
                    }
                    if (_plugin.GetPlayerCount() < MinimumPlayers)
                    {
                        info += "Challenges do not gain progress until " + MinimumPlayers + " active players.";
                    }
                    return info;
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting challenge info.", e));
                }
                return "ERROR264";
            }

            public void OnRoundEnded(Int32 roundID)
            {
                try
                {
                    if (!Enabled)
                    {
                        return;
                    }
                    // Confirm we are in valid state to end.
                    if (ChallengeRoundState != ChallengeState.Playing)
                    {
                        // We're not, get out of here.
                        _plugin.Log.Warn("Ended challenge round when not in playing state. State was " + ChallengeRoundState.ToString() + ".");
                    }

                    // This needs to be done async to prevent AdKats from blocking the main procon event thread
                    _plugin.Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                    {
                        Thread.CurrentThread.Name = "ChallengeRoundEnd";
                        // Wait for any round-end messages to fire
                        _plugin.Threading.Wait(TimeSpan.FromSeconds(15));
                        SyncOnRoundEnded(roundID);
                        _plugin.Threading.StopWatchdog();
                    })));
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while running round end logic.", e));
                }
            }

            private void SyncOnRoundEnded(Int32 roundID)
            {
                try
                {
                    if (!Enabled)
                    {
                        return;
                    }
                    List<CEntry> completedPersonalChallenges;
                    List<CEntry> completedRoundRuleChallenges;
                    var notIgnoringPlayers = _plugin.GetOnlinePlayersWithoutGroup("challenge_ignore");
                    if (RoundRule != null)
                    {
                        completedRoundRuleChallenges = GetCompletedRoundEntries().Where(entry => entry.Rule == RoundRule).ToList();
                        completedPersonalChallenges = GetCompletedRoundEntries().Where(entry => entry.Rule != RoundRule).ToList();
                        // Congrats to the winners
                        var completedRoundRuleEntries = GetCompletedRoundEntries().Where(entry => entry.Rule == RoundRule);
                        var playerS = completedRoundRuleEntries.Count() != 1 ? "s" : "";
                        var endedMessage = RoundRule.Name + " Round Challenge Ended! " + completedRoundRuleEntries.Count() + " player" + playerS + " completed it!";
                        _plugin.ProconChatWrite(_plugin.Log.CPink(endedMessage));
                        var roundMessage = "Round Winners: " + String.Join(", ", completedRoundRuleEntries.Select(entry => entry.Player.GetVerboseName()).ToArray());
                        if (completedRoundRuleEntries.Any())
                        {
                            _plugin.ProconChatWrite(_plugin.Log.CPink(roundMessage));
                        }
                        foreach (var aPlayer in notIgnoringPlayers)
                        {
                            _plugin.PlayerSayMessage(aPlayer.player_name, endedMessage, false, 1);
                            if (completedRoundRuleEntries.Any())
                            {
                                _plugin.PlayerSayMessage(aPlayer.player_name, roundMessage, false, 1);
                            }
                        }
                    }
                    else
                    {
                        completedPersonalChallenges = GetCompletedRoundEntries();
                    }
                    if (completedPersonalChallenges.Any())
                    {
                        _plugin.Threading.Wait(TimeSpan.FromSeconds(3));
                        var personalMessage = "Personal Winners: " + String.Join(Environment.NewLine, completedPersonalChallenges.Select(entry => entry.Player.GetVerboseName() + " [" + entry.Rule.Name + "]").ToArray());
                        _plugin.ProconChatWrite(_plugin.Log.CPink(personalMessage));
                        foreach (var aPlayer in notIgnoringPlayers)
                        {
                            _plugin.PlayerSayMessage(aPlayer.player_name, personalMessage, false, 1);
                        }
                    }
                    // Trigger the state change
                    ChallengeRoundState = ChallengeState.Ended;
                    // Fail the active round rule if applicable
                    FailActiveRoundRule();
                    // Fail challenges as necessary
                    var roundEntries = GetEntries().Where(entry => !entry.Completed &&
                                                                   !entry.Failed &&
                                                                   !entry.Canceled);
                    foreach (var entry in roundEntries)
                    {
                        entry.CheckFailure();
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while running round end sync logic.", e));
                }
            }

            public void OnRoundLoaded(Int32 roundID)
            {
                try
                {
                    if (!Enabled)
                    {
                        return;
                    }
                    if (ChallengeRoundState == ChallengeState.Playing)
                    {
                        // We are still in playing state, this is likely due to a server crash or other oddity. Run the end trigger to change the state.
                        _plugin.Log.Warn("Challenge manager was in Playing state when loading round. Was expecting Ended. Fixing this state.");
                        SyncOnRoundEnded(roundID);
                    }
                    // Confirm we are in valid state to load.
                    if (ChallengeRoundState != ChallengeState.Ended &&
                        ChallengeRoundState != ChallengeState.Init)
                    {
                        // We're not, get out of here.
                        _plugin.Log.Warn("Attempted to load challenge when not in ended or initializing state.");
                        return;
                    }
                    lock (CompletedRoundEntries)
                    {
                        // Clear all of the completed round entries. We're starting a new list.
                        CompletedRoundEntries.Clear();
                    }
                    // Do not start a server-wide round rule during an event
                    if (EnableServerRoundRules && !RandomPlayerRoundRules && !_plugin.EventActive())
                    {
                        // Make sure we don't have an active rule at this time.
                        if (RoundRule == null)
                        {
                            // We want to have server-wide round rules. Grab ones which are valid for that.
                            var roundRules = GetRules().Where(rule => rule.Enabled &&
                                                                      rule.Tier == 1 &&
                                                                      rule.Completion == CRule.CompletionType.Rounds &&
                                                                      rule.RoundCount == 1 &&
                                                                      rule.Definition.GetDetails().Any());
                            if (roundRules.Any())
                            {
                                // Randomize the list and pick the first unused one
                                var rng = new Random(Environment.TickCount);
                                var chosenRule = roundRules.Where(rule => rule.RoundLastUsedTime.Equals(AdKats.GetEpochTime())).OrderBy(rule => rng.Next()).FirstOrDefault();
                                if (chosenRule == null)
                                {
                                    // None are unused, pick the one which has the longest duration since used
                                    chosenRule = roundRules.OrderBy(rule => rule.RoundLastUsedTime).FirstOrDefault();
                                }
                                if (chosenRule != null)
                                {
                                    chosenRule.RoundLastUsedTime = _plugin.UtcNow();
                                    chosenRule.DBPush(null);
                                    RoundRule = chosenRule;
                                    if (ChallengeRoundState == ChallengeState.Ended)
                                    {
                                        // Delay announcing the new challenge for a few seconds
                                        _plugin.Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                                        {
                                            Thread.CurrentThread.Name = "ChallengeRoundRuleAnnounce";
                                            Thread.Sleep(TimeSpan.FromSeconds(25));

                                            var startMessage = RoundRule.Name + " Challenge Starting! Type " + _plugin.GetChatCommandByKey("self_challenge") + " for more info.";
                                            _plugin.ProconChatWrite(_plugin.Log.FBold(_plugin.Log.CPink(startMessage)));
                                            // Only tell players about the new challenge if they don't already have a challenge assigned
                                            foreach (var player in _plugin.GetOnlinePlayersWithoutGroup("challenge_ignore").Where(player => player.ActiveChallenge == null))
                                            {
                                                _plugin.PlayerTellMessage(player.player_name, startMessage, false, 1);
                                            }
                                            _plugin.Threading.StopWatchdog();
                                        })));
                                    }
                                }
                                else
                                {
                                    _plugin.Log.Error("Unable to select server-wide round rule.");
                                }
                            }
                            else
                            {
                                _plugin.Log.Warn("Server-wide round rules enabled but none available. They must be enabled, tier 1, completion type Rounds, and have duration of 1 round.");
                            }
                        }
                        else
                        {
                            _plugin.Log.Error("Unable to start new round rule. Rule " + RoundRule.ToString() + " is already running.");
                        }
                    }
                    LoadedRoundID = roundID;
                    ChallengeRoundState = ChallengeState.Loaded;
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while running round load logic.", e));
                }
            }

            public void OnRoundPlaying(Int32 roundID)
            {
                try
                {
                    if (!Enabled)
                    {
                        return;
                    }
                    // Confirm we are in valid state to play.
                    if (ChallengeRoundState != ChallengeState.Loaded)
                    {
                        // We're not, get out of here.
                        _plugin.Log.Warn("Attempted to start playing challenge round when not in loaded state.");
                        return;
                    }
                    if (LoadedRoundID != roundID)
                    {
                        _plugin.Log.Error("Attempted to start challenge playing with invalid round ID " + roundID + ", original round loaded was " + LoadedRoundID);
                        return;
                    }
                    ChallengeRoundState = ChallengeState.Playing;
                    if (EnableServerRoundRules)
                    {
                        var playerList = _plugin.GetOnlinePlayersWithoutGroup("challenge_ignore").Where(player => player.ActiveChallenge == null);
                        if (RoundRule != null)
                        {
                            var startMessage = RoundRule.Name + " Challenge Starting! Type " + _plugin.GetChatCommandByKey("self_challenge") + " for more info.";
                            _plugin.ProconChatWrite(_plugin.Log.FBold(_plugin.Log.CPink(startMessage)));
                            // Only tell players about the new challenge if they don't already have a challenge assigned
                            foreach (var player in playerList)
                            {
                                _plugin.PlayerTellMessage(player.player_name, startMessage, false, 1);
                            }
                        }
                        else if (RandomPlayerRoundRules)
                        {
                            foreach (var player in playerList)
                            {
                                AssignActiveEntryForPlayer(player);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while running round play logic.", e));
                }
            }

            public void RunRoundChallenge(Int32 RuleID)
            {
                try
                {
                    if (Enabled)
                    {
                        if (Loading)
                        {
                            _plugin.Threading.Wait(5000);
                        }
                        if (!Loaded)
                        {
                            HandleRead(null, true);
                            if (!Loaded)
                            {
                                _plugin.Log.Error("Unable to run next challenge. Manager could not complete loading.");
                                return;
                            }
                        }
                        if (!EnableServerRoundRules)
                        {
                            _plugin.Log.Error("Server-wide round rules not enabled. Unable to start rule.");
                            return;
                        }
                        if (RandomPlayerRoundRules)
                        {
                            _plugin.Log.Error("Random player round rules are being used instead of one rule for everyone.");
                            return;
                        }
                        var chosenRule = GetRules().FirstOrDefault(rule => rule.ID == RuleID);
                        if (chosenRule == null)
                        {
                            // They entered an invalid value
                            _plugin.Log.Error("Invalid rule ID entered. Unable to start rule.");
                            return;
                        }
                        if (!chosenRule.Enabled)
                        {
                            // They entered an invalid value
                            _plugin.Log.Error("Rule " + chosenRule.ToString() + " is not enabled. Unable to start rule.");
                            return;
                        }
                        if (chosenRule.Tier != 1)
                        {
                            // They entered an invalid value
                            _plugin.Log.Error("Rule " + chosenRule.ToString() + " is not a tier 1 rule. Unable to start rule.");
                            return;
                        }
                        if (chosenRule.Completion != CRule.CompletionType.Rounds || chosenRule.RoundCount != 1)
                        {
                            // Only 1 round long, round based completion rules can be used.
                            _plugin.Log.Error("Rule " + chosenRule.ToString() + " isn't round completion or doesn't have a round count of 1, unable to start rule.");
                            return;
                        }
                        if (_plugin.EventActive())
                        {
                            // Challenges are not allowed during events
                            _plugin.Log.Error("Server-wide challenges cannot activate during events.");
                            return;
                        }
                        CancelActiveRoundRule();
                        chosenRule.RoundLastUsedTime = _plugin.UtcNow();
                        chosenRule.DBPush(null);
                        RoundRule = chosenRule;
                        var startMessage = RoundRule.Name + " Challenge Starting! Type " + _plugin.GetChatCommandByKey("self_challenge") + " for more info.";
                        _plugin.ProconChatWrite(_plugin.Log.FBold(_plugin.Log.CPink(startMessage)));
                        // Only tell players about the new challenge if they don't already have a challenge assigned
                        foreach (var player in _plugin.GetOnlinePlayersWithoutGroup("challenge_ignore").Where(player => player.ActiveChallenge == null))
                        {
                            _plugin.PlayerTellMessage(player.player_name, startMessage, false, 1);
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while running next challenge.", e));
                }
            }

            public class CDefinition
            {
                private AdKats _plugin;

                public static String DetailTypeEnumString = "enum.ChallengeTemplateDetailType(None|Weapon|Damage)";
                public static String DetailDamageEnumString = String.Empty;

                public Boolean Phantom;

                private AChallengeManager Manager;
                public Int64 ID;
                public String Name;
                public DateTime CreateTime;
                public DateTime ModifyTime;
                private List<CDefinitionDetail> Details;

                public CDefinition(AdKats plugin, AChallengeManager manager, Boolean phantom)
                {
                    _plugin = plugin;
                    Manager = manager;
                    Phantom = phantom;
                    CreateTime = _plugin.UtcNow();
                    ModifyTime = _plugin.UtcNow();
                    Details = new List<CDefinitionDetail>();

                    //Fill the damage type setting enum string, if it's not already
                    if (DetailDamageEnumString == String.Empty)
                    {
                        Random random = new Random(Environment.TickCount);
                        foreach (CDefinitionDetail.DetailDamage damageType in Enum.GetValues(typeof(CDefinitionDetail.DetailDamage))
                                                                .Cast<CDefinitionDetail.DetailDamage>()
                                                                .OrderBy(type => type.ToString()))
                        {
                            if (String.IsNullOrEmpty(DetailDamageEnumString))
                            {
                                DetailDamageEnumString += "enum.DetailDamageTypeEnum_" + random.Next(100000, 999999) + "(";
                            }
                            else
                            {
                                DetailDamageEnumString += "|";
                            }
                            DetailDamageEnumString += damageType;
                        }
                        DetailDamageEnumString += ")";
                    }
                }

                public void SetNameByString(String newName)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(newName))
                        {
                            _plugin.Log.Error("Definition name was empty when setting by string.");
                            return;
                        }
                        var sanitizedName = _plugin.MakeAlphanumeric(newName);
                        if (String.IsNullOrEmpty(sanitizedName))
                        {
                            _plugin.Log.Error("Definition name was empty when setting by string.");
                            return;
                        }
                        if (Name == sanitizedName)
                        {
                            _plugin.Log.Error("Definition name was the same when setting by string.");
                            return;
                        }
                        // Check to see if the name can be parsed as an int32
                        // This is actually an issue with the procon setting display framework
                        // For some reason if the string is numeric, it tries to parse it as a number
                        if (Regex.IsMatch(sanitizedName, @"^\d+$"))
                        {
                            // String is numeric, try to parse it.
                            Int32 parsed;
                            if (!Int32.TryParse(sanitizedName, out parsed))
                            {
                                // Can't parse it. Make it not numeric.
                                sanitizedName += "X";
                            }
                        }
                        // Check if a definition exists with this name
                        if (Manager.GetDefinitions().Any(dDef => dDef.Name == sanitizedName))
                        {
                            _plugin.Log.Error("Definition called " + sanitizedName + " already exists.");
                            return;
                        }
                        Name = sanitizedName;
                        ModifyTime = _plugin.UtcNow();
                        // Push to the database.
                        DBPush(null);
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while setting definition name by string.", e));
                    }
                }

                public void SortDetails(MySqlConnection connection)
                {
                    try
                    {
                        if (ID <= 0 || Phantom)
                        {
                            _plugin.Log.Error("CDefinition was invalid when sorting.");
                            return;
                        }
                        var localConnection = connection;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            lock (Details)
                            {
                                var currentID = 1;
                                foreach (var detail in Details.OrderBy(dDetail => dDetail.DetailID))
                                {
                                    // Take the current ordering by detail ID and create a sequential list
                                    detail.SetDetailID(localConnection, currentID++);
                                    if (detail.Phantom)
                                    {
                                        detail.DBPush(connection);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (connection == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while sorting definition details.", e));
                    }
                }

                public List<CDefinitionDetail> GetDetails()
                {
                    var details = new List<CDefinitionDetail>();
                    try
                    {
                        lock (Details)
                        {
                            details.AddRange(Details.OrderBy(det => det.DetailID).ToList());
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while getting list of details.", e));
                    }
                    return details;
                }

                public CDefinitionDetail GetDetail(Int64 detailID)
                {
                    try
                    {
                        lock (Details)
                        {
                            if (!Details.Any())
                            {
                                return null;
                            }
                            return Details.FirstOrDefault(detail => detail.DetailID == detailID);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while getting detail by detail ID.", e));
                    }
                    return null;
                }

                protected void DeleteDetail(Int64 detailID)
                {
                    try
                    {
                        lock (Details)
                        {
                            var matchingDetail = Details.FirstOrDefault(detail => detail.DetailID == detailID);
                            if (matchingDetail != null)
                            {
                                Details.Remove(matchingDetail);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while deleting detail from definition.", e));
                    }
                }

                public void CreateDetail(String type, String value)
                {
                    try
                    {
                        lock (Details)
                        {
                            // Set the ID to 99999. This will be changed during the detail sort.
                            var detail = new CDefinitionDetail(_plugin, this, 99999, true)
                            {
                                KillCount = 1
                            };
                            // Find the detail type
                            if (type == CDefinitionDetail.DetailType.Damage.ToString())
                            {
                                detail.Type = CDefinitionDetail.DetailType.Damage;
                                detail.WeaponCount = 1;
                                try
                                {
                                    detail.Damage = (CDefinitionDetail.DetailDamage)Enum.Parse(typeof(CDefinitionDetail.DetailDamage), value);
                                }
                                catch (Exception e)
                                {
                                    _plugin.Log.Error("Unable to create Damage detail with damage type " + value + ".");
                                    return;
                                }
                                if (Details.Any(dDetail => dDetail.Type == CDefinitionDetail.DetailType.Damage &&
                                                           dDetail.Damage == detail.Damage))
                                {
                                    _plugin.Log.Error("Detail with damage " + detail.Damage.ToString() + " already exists.");
                                    return;
                                }
                                // Check to see if any of the internal damage types are already added
                                var existingDamageTypes = new List<DamageTypes>();
                                foreach (var damageTypeList in Details.Select(dDetail => dDetail.GetDamageTypes()))
                                {
                                    existingDamageTypes.AddRange(damageTypeList);
                                }
                                foreach (var damageType in existingDamageTypes)
                                {
                                    if (detail.GetDamageTypes().Contains(damageType))
                                    {
                                        _plugin.Log.Error("Detail with internal damage " + damageType + " already exists.");
                                        return;
                                    }
                                }
                            }
                            else if (type == CDefinitionDetail.DetailType.Weapon.ToString())
                            {
                                detail.Type = CDefinitionDetail.DetailType.Weapon;
                                var weaponCode = _plugin.WeaponDictionary.GetWeaponCodeByShortName(value.Split('\\')[1]);
                                // Confirm the weapon is valid
                                if (String.IsNullOrEmpty(weaponCode))
                                {
                                    _plugin.Log.Error("Unable to create Weapon detail with weapon name " + value + ". No matching weapon code exists.");
                                    return;
                                }
                                if (_plugin.WeaponDictionary.GetDamageTypeByWeaponCode(weaponCode) == DamageTypes.None)
                                {
                                    _plugin.Log.Error("Unable to create Weapon detail with weapon code " + weaponCode + ". No valid matching damage type exists.");
                                    return;
                                }
                                if (Details.Any(dDetail => dDetail.Type == CDefinitionDetail.DetailType.Weapon &&
                                                           dDetail.Weapon == weaponCode))
                                {
                                    _plugin.Log.Error("Detail with weapon " + value + " already exists.");
                                    return;
                                }
                                detail.Weapon = weaponCode;
                            }
                            else
                            {
                                _plugin.Log.Error("Invalid detail type " + type + " when creating definition detail.");
                                return;
                            }
                            Details.Add(detail);
                            // Sorting details will automatically push newly created details
                            SortDetails(null);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while creating definition detail in manager.", e));
                    }
                }

                public void DBPush(MySqlConnection connection)
                {
                    try
                    {
                        if (connection == null)
                        {
                            connection = _plugin.GetDatabaseConnection();
                        }
                        if (Phantom)
                        {
                            DBCreate(connection, true);
                        }
                        else
                        {
                            DBUpdate(connection, true);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBPush for CDefinition.", e));
                    }
                }

                private void DBCreate(MySqlConnection con, Boolean includeDetails)
                {
                    try
                    {
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                INSERT INTO
                                    `adkats_challenge_definition`
                                (
                                    `Name`,
                                    `CreateTime`,
                                    `ModifyTime`
                                )
                                VALUES
                                (
                                    @Name,
                                    @CreateTime,
                                    @ModifyTime
                                )";
                                command.Parameters.AddWithValue("@Name", Name);
                                command.Parameters.AddWithValue("@CreateTime", CreateTime);
                                command.Parameters.AddWithValue("@ModifyTime", ModifyTime);
                                if (_plugin.SafeExecuteNonQuery(command) > 0)
                                {
                                    ID = command.LastInsertedId;
                                    // This record is no longer phantom
                                    Phantom = false;
                                    if (includeDetails)
                                    {
                                        DBPushDetails(localConnection);
                                    };
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBCreate for CDefinition.", e));
                    }
                }

                public void DBPushDetails(MySqlConnection con)
                {
                    try
                    {
                        if (ID <= 0)
                        {
                            _plugin.Log.Error("ID " + ID + " was invalid when updating details for CDefinition.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            foreach (var detail in Details)
                            {
                                detail.DBPush(localConnection);
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBUpdateDetails for CDefinition.", e));
                    }
                }

                private void DBUpdate(MySqlConnection con, Boolean includeDetails)
                {
                    try
                    {
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                UPDATE
                                    `adkats_challenge_definition`
                                SET
                                    `Name` = @Name,
                                    `ModifyTime` = @ModifyTime
                                WHERE
                                    `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                command.Parameters.AddWithValue("@Name", Name);
                                command.Parameters.AddWithValue("@ModifyTime", ModifyTime);
                                if (_plugin.SafeExecuteNonQuery(command) > 0)
                                {
                                    if (includeDetails)
                                    {
                                        DBPushDetails(con);
                                    }
                                }
                                else
                                {
                                    _plugin.Log.Error("Failed to update CDefinition " + ID + " in database.");
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBUpdate for CDefinition.", e));
                    }
                }

                public void DBRead(MySqlConnection con)
                {
                    try
                    {
                        if (ID <= 0)
                        {
                            _plugin.Log.Error("ID " + ID + " was invalid when reading CDefinition.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                  SELECT `ID`,
                                         `Name`,
                                         `CreateTime`,
                                         `ModifyTime`
                                    FROM `adkats_challenge_definition`
                                   WHERE `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                var push = false;
                                using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                                {
                                    if (reader.Read())
                                    {
                                        Name = reader.GetString("Name");
                                        var sanitizedName = Name.Replace("|", "");
                                        if (String.IsNullOrEmpty(sanitizedName))
                                        {
                                            _plugin.Log.Error("Challenge definition " + ID + " contained invalid name characters.");
                                            var rng = new Random(Environment.TickCount);
                                            // Assume we won't hit duplicates with 1 million (welp)
                                            Name = "CHG-" + ID + "-" + rng.Next(1000000);
                                            push = true;
                                        }
                                        else if (Name != sanitizedName)
                                        {
                                            _plugin.Log.Error("Challenge definition " + ID + " contained invalid name characters.");
                                            Name = sanitizedName;
                                            push = true;
                                        }
                                        CreateTime = reader.GetDateTime("CreateTime");
                                        ModifyTime = reader.GetDateTime("ModifyTime");
                                    }
                                    else
                                    {
                                        _plugin.Log.Error("Unable to find matching CDefinition for ID " + ID);
                                    }
                                }
                                if (push)
                                {
                                    DBPush(localConnection);
                                }
                            }
                            DBReadDetails(localConnection);
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBRead for CDefinition.", e));
                    }
                }

                public void DBReadDetails(MySqlConnection con)
                {
                    try
                    {
                        if (ID <= 0)
                        {
                            _plugin.Log.Error("ID " + ID + " was invalid when reading CDefinition Details.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                  SELECT `DefID`,
                                         `DetailID`,
                                         `Type`,
                                         `Damage`,
                                         `WeaponCount`,
                                         `Weapon`,
                                         `KillCount`,
                                         `CreateTime`,
                                         `ModifyTime`
                                    FROM `adkats_challenge_definition_detail`
                                   WHERE `DefID` = @DefID";
                                command.Parameters.AddWithValue("@DefID", ID);
                                var deleteDetails = new List<CDefinitionDetail>();
                                using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                                {
                                    lock (Details)
                                    {
                                        // Clear all existing details, we are fetching them all from the DB
                                        Details.Clear();
                                        while (reader.Read())
                                        {
                                            var add = false;
                                            var upload = false;
                                            var detailID = reader.GetInt32("DetailID");
                                            CDefinitionDetail detail = Details.FirstOrDefault(dDetail => dDetail.DetailID == detailID);
                                            if (detail == null)
                                            {
                                                detail = new CDefinitionDetail(_plugin, this, detailID, false);
                                                add = true;
                                            }
                                            detail.Type = (CDefinitionDetail.DetailType)Enum.Parse(typeof(CDefinitionDetail.DetailType), reader.GetString("Type"));
                                            // Damage section
                                            if (!reader.IsDBNull(3))
                                            {
                                                detail.Damage = (CDefinitionDetail.DetailDamage)Enum.Parse(typeof(CDefinitionDetail.DetailDamage), reader.GetString("Damage"));
                                                // Make sure we aren't loading in duplicate damage types
                                                if (detail.Type == CDefinitionDetail.DetailType.Damage &&
                                                    Details.Any(dDetail => dDetail.Type == CDefinitionDetail.DetailType.Damage &&
                                                                           dDetail.Damage == detail.Damage))
                                                {
                                                    _plugin.Log.Error("Detail with damage " + detail.Damage.ToString() + " already exists.");
                                                    deleteDetails.Add(detail);
                                                    continue;
                                                }
                                                detail.WeaponCount = reader.GetInt32("WeaponCount");
                                                if (detail.Type == CDefinitionDetail.DetailType.Damage &&
                                                    detail.WeaponCount < 1)
                                                {
                                                    _plugin.Log.Error("Challenge detail " + this.ID + ":" + detail.DetailID + " had an invalid weapon count. Changing to 1.");
                                                    detail.WeaponCount = 1;
                                                    upload = true;
                                                }
                                            }
                                            // Weapon section
                                            if (!reader.IsDBNull(5))
                                            {
                                                detail.Weapon = reader.GetString("Weapon");
                                                // Make sure we aren't loading in duplicate weapon codes
                                                if (detail.Type == CDefinitionDetail.DetailType.Weapon &&
                                                    Details.Any(dDetail => dDetail.Type == CDefinitionDetail.DetailType.Weapon &&
                                                                           dDetail.Weapon == detail.Weapon))
                                                {
                                                    _plugin.Log.Error("Detail with weapon " + detail.Weapon + " already exists.");
                                                    deleteDetails.Add(detail);
                                                    continue;
                                                }
                                            }
                                            detail.KillCount = reader.GetInt32("KillCount");
                                            if (detail.KillCount < 1)
                                            {
                                                _plugin.Log.Error("Challenge detail " + this.ID + ":" + detail.DetailID + " had an invalid kill count. Changing to 1.");
                                                detail.KillCount = 1;
                                                upload = true;
                                            }
                                            detail.CreateTime = reader.GetDateTime("CreateTime");
                                            detail.ModifyTime = reader.GetDateTime("ModifyTime");
                                            if (upload)
                                            {
                                                detail.DBPush(localConnection);
                                            }
                                            if (add)
                                            {
                                                Details.Add(detail);
                                            }
                                        }
                                        // No need to clean up details, they are purged during every read.
                                    }
                                }
                                if (deleteDetails.Any())
                                {
                                    foreach (var detail in deleteDetails)
                                    {
                                        detail.DBDelete(localConnection);
                                    }
                                    SortDetails(localConnection);
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBReadDetails for CDefinition.", e));
                    }
                }

                public void DBDelete(MySqlConnection con)
                {
                    try
                    {
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                DELETE FROM
                                    `adkats_challenge_definition`
                                WHERE
                                    `ID` = @ID;";
                                command.Parameters.AddWithValue("@ID", ID);
                                if (_plugin.SafeExecuteNonQuery(command) > 0)
                                {
                                    // SUCCESS
                                    // Clear all the details, they are removed automatically from the database
                                    _plugin.Log.Info("Deleted challenge definition " + ID + " with " + Details.Count() + " details.");
                                    Details.Clear();
                                    Manager.DeleteDefinition(ID);
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBDelete for CDefinition.", e));
                    }
                }

                public class CDefinitionDetail
                {
                    public enum DetailType
                    {
                        None,
                        Weapon,
                        Damage
                    }
                    public enum DetailDamage
                    {
                        None,
                        Melee,
                        Handgun,
                        Assault_Rifle,
                        Carbine,
                        LMG,
                        SMG,
                        DMR,
                        DMR_And_Sniper,
                        Sniper_Rifle,
                        Shotgun,
                        Explosive
                    }

                    private AdKats _plugin;

                    public Boolean Phantom;

                    public CDefinition Definition
                    {
                        get; private set;
                    }
                    public Int64 DetailID
                    {
                        get; private set;
                    }
                    public DetailType Type = DetailType.None;
                    public DetailDamage Damage = DetailDamage.None;
                    public Int32 WeaponCount;
                    public String Weapon;
                    public Int32 KillCount;
                    public DateTime CreateTime;
                    public DateTime ModifyTime;

                    public CDefinitionDetail(AdKats plugin, CDefinition definition, Int64 startingID, Boolean phantom)
                    {
                        _plugin = plugin;
                        Definition = definition;
                        DetailID = startingID;
                        Phantom = phantom;
                        CreateTime = _plugin.UtcNow();
                        ModifyTime = _plugin.UtcNow();
                    }

                    public void DBPush(MySqlConnection con)
                    {
                        try
                        {
                            if (con == null)
                            {
                                con = _plugin.GetDatabaseConnection();
                            }
                            if (Phantom)
                            {
                                DBCreate(con);
                            }
                            else
                            {
                                DBUpdate(con);
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error performing DBPush for CDefinitionDetail.", e));
                        }
                    }

                    private void DBCreate(MySqlConnection con)
                    {
                        try
                        {
                            if (Definition == null || Definition.ID <= 0 || DetailID <= 0)
                            {
                                _plugin.Log.Error("CDefinitionDetail was invalid when creating.");
                                return;
                            }
                            var localConnection = con;
                            if (localConnection == null)
                            {
                                localConnection = _plugin.GetDatabaseConnection();
                            }
                            try
                            {
                                using (MySqlCommand command = localConnection.CreateCommand())
                                {
                                    command.CommandText = @"
                                    INSERT INTO
                                        `adkats_challenge_definition_detail`
                                    (
                                        `DefID`,
                                        `DetailID`,
                                        `Type`,
                                        `Damage`,
                                        `WeaponCount`,
                                        `Weapon`,
                                        `KillCount`,
                                        `CreateTime`,
                                        `ModifyTime`
                                    )
                                    VALUES
                                    (
                                        @DefID,
                                        @DetailID,
                                        @Type,
                                        @Damage,
                                        @WeaponCount,
                                        @Weapon,
                                        @KillCount,
                                        @CreateTime,
                                        @ModifyTime
                                    )
                                    ON DUPLICATE KEY UPDATE
                                        `Type` = @Type
                                    AND `Damage` = @Damage
                                    AND `WeaponCount` = @WeaponCount
                                    AND `Weapon` = @Weapon
                                    AND `KillCount` = @KillCount
                                    AND `CreateTime` = @CreateTime
                                    AND `ModifyTime` = @ModifyTime";
                                    command.Parameters.AddWithValue("@DefID", Definition.ID);
                                    command.Parameters.AddWithValue("@DetailID", DetailID);
                                    command.Parameters.AddWithValue("@Type", Type.ToString());
                                    command.Parameters.AddWithValue("@Damage", Damage.ToString());
                                    command.Parameters.AddWithValue("@WeaponCount", WeaponCount);
                                    command.Parameters.AddWithValue("@Weapon", Weapon);
                                    command.Parameters.AddWithValue("@KillCount", KillCount);
                                    command.Parameters.AddWithValue("@CreateTime", CreateTime);
                                    command.Parameters.AddWithValue("@ModifyTime", ModifyTime);
                                    if (_plugin.SafeExecuteNonQuery(command) > 0)
                                    {
                                        // This record is no longer phantom
                                        Phantom = false;
                                    }
                                }
                            }
                            finally
                            {
                                if (con == null &&
                                    localConnection != null)
                                {
                                    localConnection.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error performing DBCreate for CDefinitionDetail.", e));
                        }
                    }

                    private void DBUpdate(MySqlConnection con)
                    {
                        try
                        {
                            if (Definition == null || Definition.ID <= 0 || DetailID <= 0 || Phantom)
                            {
                                _plugin.Log.Error("CDefinitionDetail was invalid when updating.");
                                return;
                            }
                            var localConnection = con;
                            if (localConnection == null)
                            {
                                localConnection = _plugin.GetDatabaseConnection();
                            }
                            try
                            {
                                using (MySqlCommand command = localConnection.CreateCommand())
                                {
                                    command.CommandText = @"
                                    UPDATE
                                        `adkats_challenge_definition_detail`
                                    SET
                                        `Type` = @Type,
                                        `Damage` = @Damage,
                                        `WeaponCount` = @WeaponCount,
                                        `Weapon` = @Weapon,
                                        `KillCount` = @KillCount,
                                        `CreateTime` = @CreateTime,
                                        `ModifyTime` = @ModifyTime
                                    WHERE
                                        `DefID` = @DefID
                                    AND `DetailID` = @DetailID";
                                    command.Parameters.AddWithValue("@DefID", Definition.ID);
                                    command.Parameters.AddWithValue("@DetailID", DetailID);
                                    command.Parameters.AddWithValue("@Type", Type.ToString());
                                    command.Parameters.AddWithValue("@Damage", Damage.ToString());
                                    command.Parameters.AddWithValue("@WeaponCount", WeaponCount);
                                    command.Parameters.AddWithValue("@Weapon", Weapon);
                                    command.Parameters.AddWithValue("@KillCount", KillCount);
                                    command.Parameters.AddWithValue("@CreateTime", CreateTime);
                                    command.Parameters.AddWithValue("@ModifyTime", ModifyTime);
                                    if (_plugin.SafeExecuteNonQuery(command) <= 0)
                                    {
                                        _plugin.Log.Error("Failed to update CDefinitionDetail " + Definition.ID + ":" + DetailID + " in database.");
                                    }
                                }
                            }
                            finally
                            {
                                if (con == null &&
                                    localConnection != null)
                                {
                                    localConnection.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error performing DBUpdate for CDefinitionDetail.", e));
                        }
                    }

                    public void DBRead(MySqlConnection con)
                    {
                        try
                        {
                            if (Definition == null || Definition.ID <= 0 || DetailID <= 0 || Phantom)
                            {
                                _plugin.Log.Error("CDefinitionDetail was invalid when reading.");
                                return;
                            }
                            var localConnection = con;
                            if (localConnection == null)
                            {
                                localConnection = _plugin.GetDatabaseConnection();
                            }
                            try
                            {
                                using (MySqlCommand command = localConnection.CreateCommand())
                                {
                                    command.CommandText = @"
                                      SELECT `Type`,
                                             `Damage`,
                                             `WeaponCount`,
                                             `Weapon`,
                                             `KillCount`,
                                             `CreateTime`,
                                             `ModifyTime`
                                        FROM `adkats_challenge_definition_detail`
                                       WHERE `DefID` = @DefID
                                         AND `DetailID` = @DetailID";
                                    command.Parameters.AddWithValue("@DefID", Definition.ID);
                                    command.Parameters.AddWithValue("@DetailID", DetailID);
                                    var push = false;
                                    var delete = false;
                                    using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                                    {
                                        if (reader.Read())
                                        {
                                            Type = (DetailType)Enum.Parse(typeof(DetailType), reader.GetString("Type"));
                                            // Damage section
                                            if (!reader.IsDBNull(1))
                                            {
                                                Damage = (DetailDamage)Enum.Parse(typeof(DetailDamage), reader.GetString("Damage"));
                                                // Make sure we aren't loading in duplicate damage types
                                                if (Type == DetailType.Damage &&
                                                    Definition.GetDetails().Any(dDetail => dDetail.Type == DetailType.Damage &&
                                                                                           dDetail.Damage == Damage))
                                                {
                                                    _plugin.Log.Error("Detail with damage " + Damage.ToString() + " already exists.");
                                                    delete = true;
                                                }
                                                WeaponCount = reader.GetInt32("WeaponCount");
                                                if (Type == DetailType.Damage &&
                                                    WeaponCount < 1)
                                                {
                                                    _plugin.Log.Error("Challenge detail " + Definition.ID + ":" + DetailID + " had an invalid weapon count. Changing to 1.");
                                                    WeaponCount = 1;
                                                    push = true;
                                                }
                                            }
                                            // Weapon section
                                            if (!reader.IsDBNull(3))
                                            {
                                                Weapon = reader.GetString("Weapon");
                                                // Make sure we aren't loading in duplicate weapon codes
                                                if (Type == DetailType.Weapon &&
                                                    Definition.GetDetails().Any(dDetail => dDetail.Type == DetailType.Weapon &&
                                                                                           dDetail.Weapon == Weapon))
                                                {
                                                    _plugin.Log.Error("Detail with weapon " + Weapon + " already exists.");
                                                    delete = true;
                                                }
                                            }
                                            KillCount = reader.GetInt32("KillCount");
                                            if (KillCount < 1)
                                            {
                                                _plugin.Log.Error("Challenge detail " + Definition.ID + ":" + DetailID + " had an invalid kill count. Changing to 1.");
                                                KillCount = 1;
                                                push = true;
                                            }
                                            CreateTime = reader.GetDateTime("CreateTime");
                                            ModifyTime = reader.GetDateTime("ModifyTime");
                                        }
                                        else
                                        {
                                            _plugin.Log.Error("Unable to find matching CDefinitionDetail for " + Definition.ID + ":" + DetailID + ".");
                                        }
                                    }
                                    if (delete)
                                    {
                                        DBDelete(localConnection);
                                        Definition.DeleteDetail(DetailID);
                                    }
                                    else if (push)
                                    {
                                        DBPush(localConnection);
                                    }
                                }
                            }
                            finally
                            {
                                if (con == null &&
                                    localConnection != null)
                                {
                                    localConnection.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error performing DBRead for CDefinitionDetail.", e));
                        }
                    }

                    public void DBDelete(MySqlConnection con)
                    {
                        try
                        {
                            if (Definition == null || Definition.ID <= 0 || DetailID <= 0 || Phantom)
                            {
                                _plugin.Log.Error("CDefinitionDetail was invalid when deleting.");
                                return;
                            }
                            var localConnection = con;
                            if (localConnection == null)
                            {
                                localConnection = _plugin.GetDatabaseConnection();
                            }
                            try
                            {

                                using (MySqlCommand command = localConnection.CreateCommand())
                                {
                                    command.CommandText = @"
                                    DELETE FROM
                                        `adkats_challenge_definition_detail`
                                    WHERE `DefID` = @DefID
                                      AND `DetailID` = @DetailID";
                                    command.Parameters.AddWithValue("@DefID", Definition.ID);
                                    command.Parameters.AddWithValue("@DetailID", DetailID);
                                    if (_plugin.SafeExecuteNonQuery(command) > 0)
                                    {
                                        // SUCCESS
                                        _plugin.Log.Info("Deleted CDefinitionDetail " + Definition.ID + ":" + DetailID + ".");
                                        Definition.DeleteDetail(DetailID);
                                    }
                                }
                            }
                            finally
                            {
                                if (con == null &&
                                    localConnection != null)
                                {
                                    localConnection.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error performing DBDelete for CDefinitionDetail.", e));
                        }
                    }

                    public void SetDetailID(MySqlConnection con, Int64 newDetailID)
                    {
                        try
                        {
                            if (newDetailID <= 0)
                            {
                                _plugin.Log.Error("newDetailID was invalid when changing ID.");
                                return;
                            }
                            if (DetailID == newDetailID)
                            {
                                _plugin.Log.Info("Detail IDs were the same when changing ID.");
                                return;
                            }
                            if (Phantom)
                            {
                                // No database link to update, simply set the new ID
                                DetailID = newDetailID;
                                return;
                            }
                            if (Definition == null || Definition.ID <= 0 || DetailID <= 0)
                            {
                                _plugin.Log.Error("CDefinitionDetail was invalid when changing ID.");
                                return;
                            }
                            var localConnection = con;
                            if (localConnection == null)
                            {
                                localConnection = _plugin.GetDatabaseConnection();
                            }
                            try
                            {
                                using (MySqlCommand command = localConnection.CreateCommand())
                                {
                                    command.CommandText = @"
                                    UPDATE IGNORE
                                        `adkats_challenge_definition_detail`
                                    SET
                                        `DetailID` = @NewDetailID
                                    WHERE
                                        `DefID` = @DefID
                                    AND `DetailID` = @OldDetailID";
                                    command.Parameters.AddWithValue("@DefID", Definition.ID);
                                    command.Parameters.AddWithValue("@OldDetailID", DetailID);
                                    command.Parameters.AddWithValue("@NewDetailID", newDetailID);
                                    if (_plugin.SafeExecuteNonQuery(command) > 0)
                                    {
                                        DetailID = newDetailID;
                                    }
                                    else
                                    {
                                        _plugin.Log.Error("Error changing CDefinitionDetail " + Definition.ID + ":" + DetailID + " to ID " + newDetailID + ".");
                                    }
                                }
                            }
                            finally
                            {
                                if (con == null &&
                                    localConnection != null)
                                {
                                    localConnection.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error updating ID for CDefinitionDetail.", e));
                        }
                    }

                    public void SetDamageTypeByString(String damageType)
                    {
                        try
                        {
                            // Confirm this is a damage type detail
                            if (Type != DetailType.Damage)
                            {
                                _plugin.Log.Error("Tried to change damage type of detail when it wasn't a damage detail.");
                                return;
                            }
                            if (String.IsNullOrEmpty(damageType))
                            {
                                _plugin.Log.Error("Damage type was empty when setting damage type by string.");
                                return;
                            }
                            var newDamage = (CDefinitionDetail.DetailDamage)Enum.Parse(typeof(CDefinitionDetail.DetailDamage), damageType);
                            if (newDamage == Damage)
                            {
                                _plugin.Log.Info("Old detail damage type and new damage type were the same when setting damage type by string.");
                                return;
                            }
                            Damage = newDamage;
                            ModifyTime = _plugin.UtcNow();
                            DBPush(null);
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error updating damage type by string for CDefinitionDetail.", e));
                        }
                    }

                    public void SetWeaponCountByString(String weaponCount)
                    {
                        try
                        {
                            // Confirm this is a damage type detail
                            if (Type != DetailType.Damage)
                            {
                                _plugin.Log.Error("Tried to change weapon count of detail when it wasn't a damage detail.");
                                return;
                            }
                            if (String.IsNullOrEmpty(weaponCount))
                            {
                                _plugin.Log.Error("Weapon count was empty when setting weapon count by string.");
                                return;
                            }
                            var newWeaponCount = Int32.Parse(weaponCount);
                            if (newWeaponCount < 1)
                            {
                                _plugin.Log.Error("Weapon count cannot be less than 1.");
                                newWeaponCount = 1;
                            }
                            if (newWeaponCount != WeaponCount)
                            {
                                WeaponCount = newWeaponCount;
                                ModifyTime = _plugin.UtcNow();
                                DBPush(null);
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error updating weapon count by string for CDefinitionDetail.", e));
                        }
                    }

                    public void SetWeaponNameByString(String weaponName)
                    {
                        try
                        {
                            // Confirm this is a weapon type detail
                            if (Type != DetailType.Weapon)
                            {
                                _plugin.Log.Error("Tried to change weapon name of detail when it wasn't a weapon detail.");
                                return;
                            }
                            if (String.IsNullOrEmpty(weaponName))
                            {
                                _plugin.Log.Error("Weapon name was empty when setting weapon name by string.");
                                return;
                            }
                            // Get the weapon code for this name
                            var weaponSplit = weaponName.Split('\\');
                            if (weaponSplit.Count() != 2)
                            {
                                _plugin.Log.Error("Challenge weapon name '" + weaponName + "' was invalid. Unable to assign.");
                                return;
                            }
                            var newWeapon = _plugin.WeaponDictionary.GetWeaponCodeByShortName(weaponSplit[1]);
                            if (String.IsNullOrEmpty(newWeapon))
                            {
                                _plugin.Log.Error("Error getting weapon code when setting weapon name by string.");
                                return;
                            }
                            if (newWeapon != Weapon)
                            {
                                Weapon = newWeapon;
                                ModifyTime = _plugin.UtcNow();
                                DBPush(null);
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error updating weapon name by string for CDefinitionDetail.", e));
                        }
                    }

                    public void SetKillCountByString(String killCount)
                    {
                        try
                        {
                            if (String.IsNullOrEmpty(killCount))
                            {
                                _plugin.Log.Error("Kill count was empty when setting kill count by string.");
                                return;
                            }
                            var newKillCount = Int32.Parse(killCount);
                            if (newKillCount < 1)
                            {
                                _plugin.Log.Error("Kill count cannot be less than 1.");
                                newKillCount = 1;
                            }
                            if (newKillCount != KillCount)
                            {
                                KillCount = newKillCount;
                                ModifyTime = _plugin.UtcNow();
                                DBPush(null);
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error updating kill count by string for CDefinitionDetail.", e));
                        }
                    }

                    public List<DamageTypes> GetDamageTypes()
                    {
                        try
                        {
                            // Maps detail damage to procon damage types
                            List<DamageTypes> damageTypes = new List<DamageTypes>();
                            if (Type == DetailType.Damage)
                            {
                                switch (Damage)
                                {
                                    case DetailDamage.None:
                                        damageTypes.Add(DamageTypes.None);
                                        break;
                                    case DetailDamage.Melee:
                                        damageTypes.Add(DamageTypes.Melee);
                                        break;
                                    case DetailDamage.Handgun:
                                        damageTypes.Add(DamageTypes.Handgun);
                                        break;
                                    case DetailDamage.Assault_Rifle:
                                        damageTypes.Add(DamageTypes.AssaultRifle);
                                        break;
                                    case DetailDamage.Carbine:
                                        damageTypes.Add(DamageTypes.Carbine);
                                        break;
                                    case DetailDamage.LMG:
                                        damageTypes.Add(DamageTypes.LMG);
                                        break;
                                    case DetailDamage.SMG:
                                        damageTypes.Add(DamageTypes.SMG);
                                        break;
                                    case DetailDamage.DMR:
                                        damageTypes.Add(DamageTypes.DMR);
                                        break;
                                    case DetailDamage.DMR_And_Sniper:
                                        damageTypes.Add(DamageTypes.DMR);
                                        damageTypes.Add(DamageTypes.SniperRifle);
                                        break;
                                    case DetailDamage.Sniper_Rifle:
                                        damageTypes.Add(DamageTypes.SniperRifle);
                                        break;
                                    case DetailDamage.Shotgun:
                                        damageTypes.Add(DamageTypes.Shotgun);
                                        break;
                                    case DetailDamage.Explosive:
                                        damageTypes.Add(DamageTypes.Explosive);
                                        damageTypes.Add(DamageTypes.ProjectileExplosive);
                                        break;
                                    default:
                                        _plugin.Log.Info("Invalid detail damage when getting damage types.");
                                        break;
                                }
                            }
                            if (!damageTypes.Any())
                            {
                                damageTypes.Add(DamageTypes.None);
                            }
                            return damageTypes;
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error getting damage types for CDefinitionDetail.", e));
                        }
                        return null;
                    }

                    public override string ToString()
                    {
                        try
                        {
                            if (_plugin == null ||
                                Type == DetailType.None)
                            {
                                return "INVALID";
                            }
                            var detailName = Type.ToString() + " - ";
                            if (Type == DetailType.Damage)
                            {
                                detailName += Damage.ToString().Replace("_", " ");
                            }
                            else if (Type == DetailType.Weapon)
                            {
                                detailName += _plugin.WeaponDictionary.GetShortWeaponNameByCode(Weapon);
                            }
                            return detailName;
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error stringifying definition detail.", e));
                        }
                        return "ERROR";
                    }
                }
            }

            public class CRule
            {
                public enum CompletionType
                {
                    None,
                    Rounds,
                    Duration,
                    Deaths
                }
                public static String CompletionTypeEnumString = "enum.ChallengeRuleCompletionType(None|Rounds|Duration|Deaths)";

                private AdKats _plugin;

                public Boolean Phantom;

                private AChallengeManager Manager;
                public Int64 ID;
                public Int64 ServerID;
                public CDefinition Definition;
                public Boolean Enabled;
                public String Name;
                public Int32 Tier;
                public CompletionType Completion = CompletionType.None;
                public Int32 RoundCount;
                public Int32 DurationMinutes;
                public Int32 DeathCount;
                public DateTime CreateTime;
                public DateTime ModifyTime;
                public DateTime RoundLastUsedTime;
                public DateTime PersonalLastUsedTime;

                public CRule(AdKats plugin, AChallengeManager manager, Boolean phantom)
                {
                    _plugin = plugin;
                    Manager = manager;
                    Phantom = phantom;
                    CreateTime = _plugin.UtcNow();
                    ModifyTime = _plugin.UtcNow();
                    RoundLastUsedTime = AdKats.GetEpochTime();
                    PersonalLastUsedTime = AdKats.GetEpochTime();
                    Tier = 1;
                    RoundCount = 1;
                    DurationMinutes = 60;
                    DeathCount = 1;
                }

                public void DBPush(MySqlConnection con)
                {
                    try
                    {
                        if (con == null)
                        {
                            con = _plugin.GetDatabaseConnection();
                        }
                        if (Phantom)
                        {
                            DBCreate(con);
                        }
                        else
                        {
                            DBUpdate(con);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBPush for CDefinitionDetail.", e));
                    }
                }

                private void DBCreate(MySqlConnection con)
                {
                    try
                    {
                        if (Definition == null ||
                            Definition.ID <= 0)
                        {
                            _plugin.Log.Error("CRule was invalid when creating.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                INSERT INTO
                                    `adkats_challenge_rule`
                                (
                                    `ServerID`,
                                    `DefID`,
                                    `Enabled`,
                                    `Name`,
                                    `Tier`,
                                    `CompletionType`,
                                    `RoundCount`,
                                    `DurationMinutes`,
                                    `DeathCount`,
                                    `CreateTime`,
                                    `ModifyTime`,
                                    `RoundLastUsedTime`,
                                    `PersonalLastUsedTime`
                                )
                                VALUES
                                (
                                    @ServerID,
                                    @DefID,
                                    @Enabled,
                                    @Name,
                                    @Tier,
                                    @CompletionType,
                                    @RoundCount,
                                    @DurationMinutes,
                                    @DeathCount,
                                    @CreateTime,
                                    @ModifyTime,
                                    @RoundLastUsedTime,
                                    @PersonalLastUsedTime
                                )";
                                if (ServerID <= 0)
                                {
                                    // This rule doesn't have an associated server ID, and we're creating it.
                                    // Assign this server's ID
                                    ServerID = _plugin._serverInfo.ServerID;
                                }
                                command.Parameters.AddWithValue("@ServerID", ServerID);
                                command.Parameters.AddWithValue("@DefID", Definition.ID);
                                command.Parameters.AddWithValue("@Enabled", Enabled);
                                command.Parameters.AddWithValue("@Name", Name);
                                command.Parameters.AddWithValue("@Tier", Tier);
                                command.Parameters.AddWithValue("@CompletionType", Completion.ToString());
                                command.Parameters.AddWithValue("@RoundCount", RoundCount);
                                command.Parameters.AddWithValue("@DurationMinutes", DurationMinutes);
                                command.Parameters.AddWithValue("@DeathCount", DeathCount);
                                command.Parameters.AddWithValue("@CreateTime", CreateTime);
                                command.Parameters.AddWithValue("@ModifyTime", ModifyTime);
                                command.Parameters.AddWithValue("@RoundLastUsedTime", RoundLastUsedTime);
                                command.Parameters.AddWithValue("@PersonalLastUsedTime", PersonalLastUsedTime);
                                if (_plugin.SafeExecuteNonQuery(command) > 0)
                                {
                                    ID = command.LastInsertedId;
                                    // This record is no longer phantom
                                    Phantom = false;
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBCreate for CRule.", e));
                    }
                }

                private void DBUpdate(MySqlConnection con)
                {
                    try
                    {
                        if (Definition == null ||
                            Definition.ID <= 0 ||
                            Phantom ||
                            ID <= 0)
                        {
                            _plugin.Log.Error("CRule was invalid when updating.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                UPDATE
                                    `adkats_challenge_rule`
                                SET
                                    `DefID` = @DefID,
                                    `Enabled` = @Enabled,
                                    `Name` = @Name,
                                    `Tier` = @Tier,
                                    `CompletionType` = @CompletionType,
                                    `RoundCount` = @RoundCount,
                                    `DurationMinutes` = @DurationMinutes,
                                    `DeathCount` = @DeathCount,
                                    `ModifyTime` = @ModifyTime,
                                    `RoundLastUsedTime` = @RoundLastUsedTime,
                                    `PersonalLastUsedTime` = @PersonalLastUsedTime
                                WHERE
                                    `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                command.Parameters.AddWithValue("@DefID", Definition.ID);
                                command.Parameters.AddWithValue("@Enabled", Enabled);
                                command.Parameters.AddWithValue("@Name", Name);
                                command.Parameters.AddWithValue("@Tier", Tier);
                                command.Parameters.AddWithValue("@CompletionType", Completion.ToString());
                                command.Parameters.AddWithValue("@RoundCount", RoundCount);
                                command.Parameters.AddWithValue("@DurationMinutes", DurationMinutes);
                                command.Parameters.AddWithValue("@DeathCount", DeathCount);
                                command.Parameters.AddWithValue("@ModifyTime", ModifyTime);
                                command.Parameters.AddWithValue("@RoundLastUsedTime", RoundLastUsedTime);
                                command.Parameters.AddWithValue("@PersonalLastUsedTime", PersonalLastUsedTime);
                                if (_plugin.SafeExecuteNonQuery(command) <= 0)
                                {
                                    _plugin.Log.Error("Failed to update CRule " + ID + " in database.");
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBUpdate for CRule.", e));
                    }
                }

                public void DBRead(MySqlConnection con)
                {
                    try
                    {
                        if (Definition == null ||
                            Definition.ID <= 0 ||
                            Phantom ||
                            ID <= 0)
                        {
                            _plugin.Log.Error("CRule was invalid when reading.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                  SELECT `ID`,
                                         `ServerID`,
                                         `DefID`,
                                         `Enabled`,
                                         `Name`,
                                         `Tier`,
                                         `CompletionType`,
                                         `RoundCount`,
                                         `DurationMinutes`,
                                         `DeathCount`,
                                         `CreateTime`,
                                         `ModifyTime`,
                                         `RoundLastUsedTime`,
                                         `PersonalLastUsedTime`
                                    FROM `adkats_challenge_rule`
                                   WHERE `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                var push = false;
                                var delete = false;
                                using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                                {
                                    if (reader.Read())
                                    {
                                        var serverID = reader.GetInt64("ServerID");
                                        if (_plugin._serverInfo.ServerID != serverID)
                                        {
                                            _plugin.Log.Error("Invalid server ID when reading challenge rule " + ID + ".");
                                            delete = true;
                                        }
                                        ServerID = serverID;
                                        var definition = Manager.GetDefinition(reader.GetInt64("DefID"));
                                        if (definition == null)
                                        {
                                            _plugin.Log.Error("Invalid definition value when reading challenge rule " + ID + ".");
                                            delete = true;
                                        }
                                        Definition = definition;
                                        Enabled = reader.GetBoolean("Enabled");
                                        Name = reader.GetString("Name");
                                        var sanitizedName = Name.Replace("|", "");
                                        if (String.IsNullOrEmpty(sanitizedName))
                                        {
                                            _plugin.Log.Error("Challenge rule " + ID + " contained invalid name characters.");
                                            var rng = new Random(Environment.TickCount);
                                            // Assume we won't hit duplicates with 1 million (welp)
                                            Name = "CHG-" + ID + "-" + rng.Next(1000000);
                                            push = true;
                                        }
                                        else if (Name != sanitizedName)
                                        {
                                            _plugin.Log.Error("Challenge rule " + ID + " contained invalid name characters.");
                                            Name = sanitizedName;
                                            push = true;
                                        }
                                        Tier = reader.GetInt32("Tier");
                                        if (Tier < 1)
                                        {
                                            _plugin.Log.Error("Rule tier cannot be less than 1.");
                                            Tier = 1;
                                            push = true;
                                        }
                                        if (Tier > 10)
                                        {
                                            _plugin.Log.Error("Rule tier cannot be greter than 10.");
                                            Tier = 10;
                                            push = true;
                                        }
                                        Completion = (CompletionType)Enum.Parse(typeof(CompletionType), reader.GetString("CompletionType"));
                                        RoundCount = reader.GetInt32("RoundCount");
                                        DurationMinutes = reader.GetInt32("DurationMinutes");
                                        DeathCount = reader.GetInt32("DeathCount");
                                        CreateTime = reader.GetDateTime("CreateTime");
                                        ModifyTime = reader.GetDateTime("ModifyTime");
                                        RoundLastUsedTime = reader.GetDateTime("RoundLastUsedTime");
                                        PersonalLastUsedTime = reader.GetDateTime("PersonalLastUsedTime");
                                    }
                                    else
                                    {
                                        _plugin.Log.Error("Unable to find matching CRule for " + ID + ".");
                                        return;
                                    }
                                }
                                if (delete)
                                {
                                    DBDelete(localConnection);
                                }
                                else if (push)
                                {
                                    DBPush(localConnection);
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBRead for CRule.", e));
                    }
                }

                public void DBDelete(MySqlConnection con)
                {
                    try
                    {
                        if (Definition == null ||
                            Definition.ID <= 0 ||
                            Phantom ||
                            ID <= 0)
                        {
                            _plugin.Log.Error("CRule was invalid when reading.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {

                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                DELETE FROM
                                    `adkats_challenge_rule`
                                WHERE
                                    `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                if (_plugin.SafeExecuteNonQuery(command) > 0)
                                {
                                    // SUCCESS
                                    _plugin.Log.Info("Deleted CRule " + ID + ".");
                                    Manager.DeleteRule(ID);
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBDelete for CRule.", e));
                    }
                }

                public void SetEnabledByString(String enabled)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(enabled))
                        {
                            _plugin.Log.Error("Enabled was empty when setting by string.");
                            return;
                        }
                        var newEnabled = Boolean.Parse(enabled);
                        if (newEnabled != Enabled)
                        {
                            if (newEnabled && !Definition.GetDetails().Any())
                            {
                                _plugin.Log.Error("Cannot enable rule " + Name + ", associated definition " + Definition.Name + " has no damages/weapons added.");
                                return;
                            }
                            Enabled = newEnabled;
                            ModifyTime = _plugin.UtcNow();
                            DBPush(null);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating enabled state by string for CRule.", e));
                    }
                }

                public void SetNameByString(String newName)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(newName))
                        {
                            _plugin.Log.Error("Rule name was empty when setting by string.");
                            return;
                        }
                        var sanitizedName = newName.Replace("|", "");
                        if (String.IsNullOrEmpty(sanitizedName))
                        {
                            _plugin.Log.Error("Rule name was empty when setting by string.");
                            return;
                        }
                        if (Name == sanitizedName)
                        {
                            _plugin.Log.Error("Rule name was the same when setting by string.");
                            return;
                        }
                        // Check to see if the name can be parsed as an int32
                        // This is actually an issue with the procon setting display framework
                        // For some reason if the string is numeric, it tries to parse it as a number
                        if (Regex.IsMatch(sanitizedName, @"^\d+$"))
                        {
                            // String is numeric, try to parse it.
                            Int32 parsed;
                            if (!Int32.TryParse(sanitizedName, out parsed))
                            {
                                // Can't parse it. Make it not numeric.
                                sanitizedName += "X";
                            }
                        }
                        // Check if a definition exists with this name
                        if (Manager.GetRules().Any(dRule => dRule.Name == sanitizedName))
                        {
                            _plugin.Log.Error("Rule called " + sanitizedName + " already exists.");
                            return;
                        }
                        Name = sanitizedName;
                        ModifyTime = _plugin.UtcNow();
                        // Push to the database.
                        DBPush(null);
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while setting rule name by string.", e));
                    }
                }

                public void SetTierByString(String tier)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(tier))
                        {
                            _plugin.Log.Error("Rule tier was empty when setting by string.");
                            return;
                        }
                        var newTier = Int32.Parse(tier);
                        if (newTier < 1)
                        {
                            _plugin.Log.Error("Rule tier cannot be less than 1.");
                            newTier = 1;
                        }
                        if (newTier > 10)
                        {
                            _plugin.Log.Error("Rule tier cannot be greter than 10.");
                            newTier = 10;
                        }
                        if (newTier == Tier)
                        {
                            _plugin.Log.Info("Old tier and new tier were the same when setting by string.");
                            return;
                        }
                        Tier = newTier;
                        ModifyTime = _plugin.UtcNow();
                        DBPush(null);
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating tier by string for CRule.", e));
                    }
                }

                public void SetDefinitionByString(String definitionName)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(definitionName))
                        {
                            _plugin.Log.Error("Definition name was empty when setting by string.");
                            return;
                        }
                        var definitions = Manager.GetDefinitions();
                        if (!definitions.Any())
                        {
                            _plugin.Log.Error("No definitions available when setting by string.");
                            return;
                        }
                        var matchingDefinition = definitions.FirstOrDefault(def => def.Name == definitionName);
                        if (matchingDefinition == null)
                        {
                            _plugin.Log.Error("No matching definition when setting by string.");
                            return;
                        }
                        if (matchingDefinition.ID == Definition.ID)
                        {
                            _plugin.Log.Info("Old definition and new definition were the same when setting by string.");
                            return;
                        }
                        Definition = matchingDefinition;
                        ModifyTime = _plugin.UtcNow();
                        DBPush(null);
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating definition by string for CRule.", e));
                    }
                }

                public void SetCompletionTypeByString(String completionType)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(completionType))
                        {
                            _plugin.Log.Error("Completion type was empty when setting by string.");
                            return;
                        }
                        var newCompletionType = (CompletionType)Enum.Parse(typeof(CompletionType), completionType);
                        if (newCompletionType == Completion)
                        {
                            _plugin.Log.Info("Old completion type and new completion type were the same when setting by string.");
                            return;
                        }
                        Completion = newCompletionType;
                        ModifyTime = _plugin.UtcNow();
                        DBPush(null);
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating completion type by string for CRule.", e));
                    }
                }

                public void SetRoundCountByString(String roundCount)
                {
                    try
                    {
                        if (Completion != CompletionType.Rounds)
                        {
                            _plugin.Log.Error("Rule type was not ROUNDS when trying to set rounds duration by string.");
                            return;
                        }
                        if (String.IsNullOrEmpty(roundCount))
                        {
                            _plugin.Log.Error("Round count was empty when setting by string.");
                            return;
                        }
                        var newRoundCount = Int32.Parse(roundCount);
                        if (newRoundCount < 1)
                        {
                            _plugin.Log.Error("Round based rule duration cannot be less than 1 round.");
                            newRoundCount = 1;
                        }
                        if (newRoundCount != RoundCount)
                        {
                            RoundCount = newRoundCount;
                            ModifyTime = _plugin.UtcNow();
                            DBPush(null);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating round duration by string for CRule.", e));
                    }
                }

                public void SetDurationMinutesByString(String durationMinutes)
                {
                    try
                    {
                        if (Completion != CompletionType.Duration)
                        {
                            _plugin.Log.Error("Rule type was not DURATION when trying to set minute duration by string.");
                            return;
                        }
                        if (String.IsNullOrEmpty(durationMinutes))
                        {
                            _plugin.Log.Error("Duration minutes was empty when setting by string.");
                            return;
                        }
                        var newDurationMinutes = Int32.Parse(durationMinutes);
                        if (newDurationMinutes < 1)
                        {
                            _plugin.Log.Error("Minute based rule duration cannot be less than 1 minute.");
                            newDurationMinutes = 1;
                        }
                        if (newDurationMinutes != DurationMinutes)
                        {
                            DurationMinutes = newDurationMinutes;
                            ModifyTime = _plugin.UtcNow();
                            DBPush(null);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating minute duration by string for CRule.", e));
                    }
                }

                public void SetDeathCountByString(String deathCount)
                {
                    try
                    {
                        if (Completion != CompletionType.Deaths)
                        {
                            _plugin.Log.Error("Rule type was not DEATHS when trying to set rounds duration by string.");
                            return;
                        }
                        if (String.IsNullOrEmpty(deathCount))
                        {
                            _plugin.Log.Error("Death count was empty when setting by string.");
                            return;
                        }
                        var newDeathCount = Int32.Parse(deathCount);
                        if (newDeathCount < 1)
                        {
                            _plugin.Log.Error("Death based rule duration cannot be less than 1 death.");
                            newDeathCount = 1;
                        }
                        if (newDeathCount != DeathCount)
                        {
                            DeathCount = newDeathCount;
                            ModifyTime = _plugin.UtcNow();
                            DBPush(null);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating death duration by string for CRule.", e));
                    }
                }

                public String RuleInfo()
                {
                    String info = "";
                    var details = Definition.GetDetails();
                    var damages = details.Where(det => det.Type == CDefinition.CDefinitionDetail.DetailType.Damage);
                    if (damages.Any())
                    {
                        info += "Weapon Types: ";
                        foreach (var detail in damages)
                        {
                            var weaponS = detail.WeaponCount != 1 ? "s" : "";
                            var killS = detail.KillCount != 1 ? "s" : "";
                            info += "[" + detail.Damage.ToString().Replace("_", " ") + "/" + detail.WeaponCount + " Weapon" + weaponS + "/" + detail.KillCount + " Kill" + killS + "]" + Environment.NewLine;
                        }
                        info += Environment.NewLine;
                    }
                    var weapons = details.Where(det => det.Type == CDefinition.CDefinitionDetail.DetailType.Weapon);
                    if (weapons.Any())
                    {
                        info += "Weapons: ";
                        foreach (var detail in weapons)
                        {
                            var killS = detail.KillCount != 1 ? "s" : "";
                            info += "[" + _plugin.WeaponDictionary.GetShortWeaponNameByCode(detail.Weapon) + "/" + detail.KillCount + " Kill" + killS + "] " + Environment.NewLine;
                        }
                    }
                    return info;
                }

                public Boolean KillValid(AKill aKill)
                {
                    try
                    {
                        if (ID <= 0 ||
                            Definition == null ||
                            Definition.ID <= 0)
                        {
                            _plugin.Log.Error("Rule was invalid when checking for valid kill.");
                            return false;
                        }
                        // Check for invalid kill
                        if (aKill == null ||
                            aKill.killer == null ||
                            String.IsNullOrEmpty(aKill.weaponCode) ||
                            aKill.victim == null)
                        {
                            _plugin.Log.Error("Kill was invalid when checking for valid kill.");
                            return false;
                        }
                        // Silently cancel on teamkills
                        if (aKill.IsTeamkill)
                        {
                            return false;
                        }
                        // Default to the kill being invalid
                        var details = Definition.GetDetails();
                        if (!details.Any())
                        {
                            _plugin.Log.Error("Tried to add kill to rule " + ID + ", definition " + Definition.ID + ", when that definition had no damages/weapons.");
                            return false;
                        }
                        foreach (var detail in details)
                        {
                            if (detail.KillCount <= 0)
                            {
                                _plugin.Log.Error("Challenge definition detail " + detail.Definition.ID + "|" + detail.DetailID + " had a non-positive kill count.");
                                continue;
                            }
                            switch (detail.Type)
                            {
                                case CDefinition.CDefinitionDetail.DetailType.None:
                                    _plugin.Log.Error("Challenge definition damage detail " + detail.Definition.ID + "|" + detail.DetailID + " had a NONE rule type.");
                                    break;
                                case CDefinition.CDefinitionDetail.DetailType.Damage:
                                    // Check for matching damage
                                    if (detail.WeaponCount <= 0)
                                    {
                                        _plugin.Log.Error("Challenge definition damage detail " + detail.Definition.ID + "|" + detail.DetailID + "|" + detail.Damage.ToString() + " had non-positive weapon count.");
                                        break;
                                    }
                                    if (!detail.GetDamageTypes().Contains(aKill.weaponDamage))
                                    {
                                        break;
                                    }
                                    return true;
                                case CDefinition.CDefinitionDetail.DetailType.Weapon:
                                    // Check for matching weapon
                                    if (detail.Weapon != aKill.weaponCode)
                                    {
                                        break;
                                    }
                                    return true;
                            }
                        }
                        return false;
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while checking whether challenge kill was valid.", e));
                    }
                    return false;
                }

                public override string ToString()
                {
                    return ID + " (Tier " + Tier + " / " + Name + ")";
                }
            }

            public class CEntry
            {
                private AdKats _plugin;

                public Boolean Phantom;

                private AChallengeManager Manager;
                public Int64 ID;
                public APlayer Player;
                public CRule Rule;
                public Boolean Completed;
                public Boolean Failed;
                public Boolean Canceled;
                public Int32 StartRound;
                public DateTime StartTime;
                public DateTime CompleteTime;
                public List<CEntryDetail> Details;
                public EntryProgress Progress;

                public String LastCompletedWeapon;
                public Boolean AutoKillTold;
                public Boolean Died;
                public Boolean kAllowed;

                public CEntry(AdKats plugin, AChallengeManager manager, APlayer player, CRule rule, Int32 startingRound, Boolean phantom)
                {
                    _plugin = plugin;
                    Manager = manager;
                    Player = player;
                    Rule = rule;
                    Phantom = phantom;
                    StartRound = startingRound;
                    if (StartRound <= 1)
                    {
                        throw new ArgumentException("Error creating CEntry. Starting round number was invalid.");
                    }
                    StartTime = _plugin.UtcNow();
                    CompleteTime = AdKats.GetEpochTime();
                    Details = new List<CEntryDetail>();
                }

                public List<CEntryDetail> GetDetails()
                {
                    var details = new List<CEntryDetail>();
                    try
                    {
                        lock (Details)
                        {
                            details.AddRange(Details.OrderBy(det => det.DetailID).ToList());
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while getting list of details.", e));
                    }
                    return details;
                }

                public CEntryDetail GetDetail(Int64 detailID)
                {
                    try
                    {
                        lock (Details)
                        {
                            if (!Details.Any())
                            {
                                return null;
                            }
                            return Details.FirstOrDefault(detail => detail.DetailID == detailID);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while getting detail by detail ID.", e));
                    }
                    return null;
                }

                public void CheckFailure()
                {
                    try
                    {
                        // If this entry is already closed, don't process it
                        if (!Completed && !Failed && !Canceled)
                        {
                            // The entry is active
                            // Check for round duration failure
                            if (Rule.Completion == CRule.CompletionType.Rounds)
                            {
                                // Completion round is the same round if RoundCount = 1
                                var completionRound = StartRound + Rule.RoundCount - 1;
                                if (Manager.LoadedRoundID > completionRound || (Manager.ChallengeRoundState == ChallengeState.Ended &&
                                                                                Manager.LoadedRoundID == completionRound))
                                {
                                    // This entry is overtime. Fail it.
                                    DoFail();
                                    return;
                                }
                            }
                            else if (Rule.Completion == CRule.CompletionType.Duration)
                            {
                                var completionTime = StartTime + TimeSpan.FromMinutes(Rule.DurationMinutes);
                                // If the current round is
                                if (_plugin.UtcNow() > completionTime)
                                {
                                    // This entry is overtime. Fail it.
                                    DoFail();
                                    return;
                                }
                            }
                            else if (Rule.Completion == CRule.CompletionType.Deaths)
                            {
                                // Ignore death count based checks, those are handled on the death event itself
                            }
                            else
                            {
                                _plugin.Log.Error("Unable to do validation on entry " + ID + ", completion type is invalid.");
                            }

                            // Automatically cancel challenges that haven't been updated in 10 days.
                            if (Details.Any())
                            {
                                DateTime lastModifyTime = Details.Max(detail => detail.Kill.TimeStamp);
                                TimeSpan durationSinceModified = _plugin.NowDuration(lastModifyTime);
                                if (durationSinceModified > TimeSpan.FromDays(10))
                                {
                                    DoCancel();
                                }
                            }
                            else
                            {
                                TimeSpan durationSinceStarted = _plugin.NowDuration(StartTime);
                                if (durationSinceStarted > TimeSpan.FromDays(10))
                                {
                                    DoCancel();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing Validate for CEntry.", e));
                    }
                }

                public void DoFail()
                {
                    try
                    {
                        if (ID <= 0 ||
                            Canceled ||
                            Completed ||
                            Failed ||
                            Player == null ||
                            Player.player_id <= 0)
                        {
                            _plugin.Log.Error("Attempted to fail a challenge entry when it was invalid.");
                            return;
                        }
                        // We're valid to fail. Continue.
                        // Get progress.
                        var percentage = "0%";
                        // Only load the progress if details exist, otherwise the percentage is always 0
                        if (Details.Any())
                        {
                            if (Progress == null)
                            {
                                RefreshProgress(null);
                            }
                            percentage = Math.Round(Progress.CompletionPercentage) + "%";
                        }
                        _plugin.PlayerSayMessage(Player.player_name, _plugin.Log.CPink(Rule.Name + " challenge FAILED at " + percentage + " complete."), Manager.ChallengeRoundState == ChallengeState.Playing, 1);
                        Failed = true;
                        CompleteTime = _plugin.UtcNow();
                        DBPush(null);
                        Player.ActiveChallenge = null;
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing Fail for CEntry.", e));
                    }
                }

                public void DoComplete()
                {
                    try
                    {
                        if (ID <= 0 ||
                            Canceled ||
                            Completed ||
                            Failed ||
                            Player == null ||
                            Player.player_id <= 0)
                        {
                            _plugin.Log.Error("Attempted to fail a challenge entry when it was invalid.");
                            return;
                        }
                        // We're valid to fail. Continue.
                        // Get progress.
                        var percentage = "0%";
                        // Only load the progress if details exist, otherwise the percentage is always 0
                        if (Details.Any())
                        {
                            if (Progress == null)
                            {
                                RefreshProgress(null);
                            }
                            percentage = Math.Round(Progress.CompletionPercentage) + "%";
                        }
                        // Validate that they actually completed it.
                        if (Progress.CompletionPercentage < 99.999)
                        {
                            _plugin.Log.Error("Attempted to complete challenge without 100% completion.");
                            return;
                        }
                        // Process rewards.
                        var rewardMessage = "";
                        var matchingRewards = Manager.GetRewards().Where(dReward => dReward.Tier == Rule.Tier &&
                                                                                    dReward.Enabled &&
                                                                                    dReward.Reward != CReward.RewardType.None);
                        var givenRewards = new List<String>();
                        foreach (var reward in matchingRewards)
                        {
                            Int32 existingMinutes = 0;
                            List<ASpecialPlayer> existingPlayers = new List<ASpecialPlayer>();
                            var descriptionString = reward.getDescriptionString(Player);
                            switch (reward.Reward)
                            {
                                case CReward.RewardType.ReservedSlot:
                                    existingPlayers = _plugin.GetMatchingASPlayersOfGroup("slot_reserved", Player);
                                    if (existingPlayers.Any())
                                    {
                                        existingMinutes = (Int32)_plugin.NowDuration(existingPlayers.First().player_expiration).TotalMinutes;
                                    }
                                    _plugin.QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _plugin._serverInfo.ServerID,
                                        command_type = _plugin.GetCommandByKey("player_slotreserved"),
                                        command_numeric = existingMinutes + reward.DurationMinutes,
                                        target_name = Player.player_name,
                                        target_player = Player,
                                        source_name = "ChallengeManager",
                                        record_message = Player.GetVerboseName() + " completed tier " + reward.Tier + " challenge, assigning " + descriptionString + ".",
                                        record_time = _plugin.UtcNow()
                                    });
                                    givenRewards.Add(descriptionString);
                                    break;
                                case CReward.RewardType.SpectatorSlot:
                                    existingPlayers = _plugin.GetMatchingASPlayersOfGroup("slot_spectator", Player);
                                    if (existingPlayers.Any())
                                    {
                                        existingMinutes = (Int32)_plugin.NowDuration(existingPlayers.First().player_expiration).TotalMinutes;
                                    }
                                    _plugin.QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _plugin._serverInfo.ServerID,
                                        command_type = _plugin.GetCommandByKey("player_slotspectator"),
                                        command_numeric = existingMinutes + reward.DurationMinutes,
                                        target_name = Player.player_name,
                                        target_player = Player,
                                        source_name = "ChallengeManager",
                                        record_message = Player.GetVerboseName() + " completed tier " + reward.Tier + " challenge, assigning " + descriptionString + ".",
                                        record_time = _plugin.UtcNow()
                                    });
                                    givenRewards.Add(descriptionString);
                                    break;
                                case CReward.RewardType.BalanceWhitelist:
                                    existingPlayers = _plugin.GetMatchingASPlayersOfGroup("whitelist_multibalancer", Player);
                                    if (existingPlayers.Any())
                                    {
                                        existingMinutes = (Int32)_plugin.NowDuration(existingPlayers.First().player_expiration).TotalMinutes;
                                    }
                                    _plugin.QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _plugin._serverInfo.ServerID,
                                        command_type = _plugin.GetCommandByKey("player_whitelistbalance"),
                                        command_numeric = existingMinutes + reward.DurationMinutes,
                                        target_name = Player.player_name,
                                        target_player = Player,
                                        source_name = "ChallengeManager",
                                        record_message = Player.GetVerboseName() + " completed tier " + reward.Tier + " challenge, assigning " + descriptionString + ".",
                                        record_time = _plugin.UtcNow()
                                    });
                                    givenRewards.Add(descriptionString);
                                    break;
                                case CReward.RewardType.PingWhitelist:
                                    existingPlayers = _plugin.GetMatchingASPlayersOfGroup("whitelist_ping", Player);
                                    if (existingPlayers.Any())
                                    {
                                        existingMinutes = (Int32)_plugin.NowDuration(existingPlayers.First().player_expiration).TotalMinutes;
                                    }
                                    _plugin.QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _plugin._serverInfo.ServerID,
                                        command_type = _plugin.GetCommandByKey("player_whitelistping"),
                                        command_numeric = existingMinutes + reward.DurationMinutes,
                                        target_name = Player.player_name,
                                        target_player = Player,
                                        source_name = "ChallengeManager",
                                        record_message = Player.GetVerboseName() + " completed tier " + reward.Tier + " challenge, assigning " + descriptionString + ".",
                                        record_time = _plugin.UtcNow()
                                    });
                                    givenRewards.Add(descriptionString);
                                    break;
                                case CReward.RewardType.TeamKillTrackerWhitelist:
                                    existingPlayers = _plugin.GetMatchingASPlayersOfGroup("whitelist_teamkill", Player);
                                    if (existingPlayers.Any())
                                    {
                                        existingMinutes = (Int32)_plugin.NowDuration(existingPlayers.First().player_expiration).TotalMinutes;
                                    }
                                    _plugin.QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _plugin._serverInfo.ServerID,
                                        command_type = _plugin.GetCommandByKey("player_whitelistteamkill"),
                                        command_numeric = existingMinutes + reward.DurationMinutes,
                                        target_name = Player.player_name,
                                        target_player = Player,
                                        source_name = "ChallengeManager",
                                        record_message = Player.GetVerboseName() + " completed tier " + reward.Tier + " challenge, assigning " + descriptionString + ".",
                                        record_time = _plugin.UtcNow()
                                    });
                                    givenRewards.Add(descriptionString);
                                    break;
                                case CReward.RewardType.CommandLock:
                                    var lockString = "command lock";
                                    if (_plugin._UseExperimentalTools)
                                    {
                                        lockString = "rule breaking allowed";
                                    }
                                    var time = _plugin.UtcNow();
                                    var lockCommand = _plugin.GetCommandByKey("player_lock");
                                    var recentLock = _plugin.FetchRecentRecords(Player.player_id, lockCommand.command_id, 1000, 1, true, false).FirstOrDefault();
                                    if (recentLock != null && recentLock.source_name == "ChallengeManager")
                                    {
                                        var durationSinceLast = _plugin.NowDuration(recentLock.record_time);
                                        if (durationSinceLast.TotalHours < Manager.CommandLockTimeoutHours)
                                        {
                                            Player.Say("Unable to award '" + lockString + "' until it's active again.");
                                            continue;
                                        }
                                    }
                                    _plugin.QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _plugin._serverInfo.ServerID,
                                        command_type = lockCommand,
                                        command_numeric = reward.DurationMinutes,
                                        target_name = Player.player_name,
                                        target_player = Player,
                                        source_name = "ChallengeManager",
                                        record_message = Player.GetVerboseName() + " completed tier " + reward.Tier + " challenge, assigning " + descriptionString + ".",
                                        record_time = time
                                    });
                                    var end = time.AddMinutes(reward.DurationMinutes);
                                    _plugin.Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                                    {
                                        Thread.CurrentThread.Name = "CommandLockMonitor";
                                        Thread.Sleep(TimeSpan.FromSeconds(3));
                                        _plugin.AdminSayMessage(Player.GetVerboseName() + " " + lockString + " ACTIVE!");
                                        while (true)
                                        {
                                            if (!_plugin._pluginEnabled)
                                            {
                                                break;
                                            }
                                            if (_plugin.UtcNow() > end)
                                            {
                                                _plugin.AdminSayMessage(Player.GetVerboseName() + " " + lockString + " ENDED!");
                                                break;
                                            }
                                            var duration = _plugin.NowDuration(end);
                                            var durationSec = (Int32)duration.TotalSeconds;
                                            // Send the message every 30 seconds
                                            if (durationSec % 30 == 0)
                                            {
                                                _plugin.AdminSayMessage(Player.GetVerboseName() + " " + lockString + ", " + _plugin.FormatTimeString(duration, 2) + " remaining!");
                                            }
                                            _plugin.Threading.Wait(1000);
                                        }
                                        _plugin.Threading.StopWatchdog();
                                    })));
                                    givenRewards.Add(descriptionString);
                                    break;
                            }
                        }
                        if (givenRewards.Any())
                        {
                            rewardMessage = String.Join(", ", givenRewards.ToArray());
                        }
                        var spacer = "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -";
                        var finishing = Player.GetVerboseName() + " finished tier " + Rule.Tier + " challenge";
                        var gratz = Rule.Name + "! Congrats!";
                        if (givenRewards.Any())
                        {
                            rewardMessage = "Reward: " + rewardMessage;
                        }
                        _plugin.ProconChatWrite(_plugin.Log.FBold(_plugin.Log.CPink(finishing + " " + gratz)));
                        foreach (var player in _plugin.GetOnlinePlayersWithoutGroup("challenge_ignore"))
                        {
                            _plugin.PlayerSayMessage(player.player_name, spacer, false, 1);
                            _plugin.PlayerSayMessage(player.player_name, finishing, false, 1);
                            _plugin.PlayerSayMessage(player.player_name, gratz, false, 1);
                            if (givenRewards.Any())
                            {
                                _plugin.PlayerSayMessage(player.player_name, rewardMessage, false, 1);
                            }
                            _plugin.PlayerSayMessage(player.player_name, spacer, false, 1);
                        }
                        if (givenRewards.Any())
                        {
                            Player.Say("Use " + _plugin.GetChatCommandByKey("player_perks") + " to see your updated perks.");
                        }
                        Completed = true;
                        CompleteTime = _plugin.UtcNow();
                        DBPush(null);
                        Manager.AddCompletedEntryForRound(this);
                        _plugin.QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _plugin._serverInfo.ServerID,
                            command_type = _plugin.GetCommandByKey("player_challenge_complete"),
                            command_numeric = Rule.Tier,
                            target_name = Player.player_name,
                            target_player = Player,
                            source_name = "ChallengeManager",
                            record_message = "Completed Tier " + Rule.Tier + " Challenge [" + Rule.Name + "]",
                            record_time = _plugin.UtcNow()
                        });
                        Player.ActiveChallenge = null;
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing Fail for CEntry.", e));
                    }
                }

                public void DoCancel()
                {
                    try
                    {
                        if (ID <= 0 ||
                            Canceled ||
                            Completed ||
                            Failed ||
                            Player == null ||
                            Player.player_id <= 0)
                        {
                            _plugin.Log.Error("Attempted to cancel a challenge entry when it was invalid.");
                            return;
                        }
                        // We're valid to cancel. Continue.
                        // Get progress.
                        var percentage = "0%";
                        // Only load the progress if details exist, otherwise the percentage is always 0
                        if (Details.Any())
                        {
                            if (Progress == null)
                            {
                                RefreshProgress(null);
                            }
                            percentage = Math.Round(Progress.CompletionPercentage) + "%";
                        }
                        Player.Say(Rule.Name + " challenge CANCELLED at " + percentage + " complete.");
                        Canceled = true;
                        CompleteTime = _plugin.UtcNow();
                        DBPush(null);
                        Player.ActiveChallenge = null;
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing Cancel for CEntry.", e));
                    }
                }

                public void DBPush(MySqlConnection connection)
                {
                    try
                    {
                        if (connection == null)
                        {
                            connection = _plugin.GetDatabaseConnection();
                        }
                        if (Phantom)
                        {
                            DBCreate(connection, true);
                        }
                        else
                        {
                            DBUpdate(connection, true);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBPush for CEntry.", e));
                    }
                }

                private void DBCreate(MySqlConnection con, Boolean includeDetails)
                {
                    try
                    {
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                INSERT INTO
                                    `adkats_challenge_entry`
                                (
                                    `PlayerID`,
                                    `RuleID`,
                                    `Completed`,
                                    `Failed`,
                                    `Canceled`,
                                    `StartRound`,
                                    `StartTime`,
                                    `CompleteTime`
                                )
                                VALUES
                                (
                                    @PlayerID,
                                    @RuleID,
                                    @Completed,
                                    @Failed,
                                    @Canceled,
                                    @StartRound,
                                    @StartTime,
                                    @CompleteTime
                                )";
                                command.Parameters.AddWithValue("@PlayerID", Player.player_id);
                                command.Parameters.AddWithValue("@RuleID", Rule.ID);
                                command.Parameters.AddWithValue("@Completed", Completed);
                                command.Parameters.AddWithValue("@Failed", Failed);
                                command.Parameters.AddWithValue("@Canceled", Canceled);
                                command.Parameters.AddWithValue("@StartRound", StartRound);
                                command.Parameters.AddWithValue("@StartTime", StartTime);
                                command.Parameters.AddWithValue("@CompleteTime", CompleteTime);
                                if (_plugin.SafeExecuteNonQuery(command) > 0)
                                {
                                    ID = command.LastInsertedId;
                                    // This record is no longer phantom
                                    Phantom = false;
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBCreate for CEntry.", e));
                    }
                }

                private void DBUpdate(MySqlConnection con, Boolean includeDetails)
                {
                    try
                    {
                        if (ID <= 0)
                        {
                            _plugin.Log.Error("ID " + ID + " was invalid when updating CEntry.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                UPDATE
                                    `adkats_challenge_entry`
                                SET
                                    `Completed` = @Completed,
                                    `Failed` = @Failed,
                                    `Canceled` = @Canceled,
                                    `CompleteTime` = @CompleteTime
                                WHERE
                                    `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                command.Parameters.AddWithValue("@Completed", Completed);
                                command.Parameters.AddWithValue("@Failed", Failed);
                                command.Parameters.AddWithValue("@Canceled", Canceled);
                                command.Parameters.AddWithValue("@CompleteTime", CompleteTime);
                                if (_plugin.SafeExecuteNonQuery(command) <= 0)
                                {
                                    _plugin.Log.Error("Failed to update CEntry " + ID + " in database.");
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBUpdate for CEntry.", e));
                    }
                }

                public void DBRead(MySqlConnection con)
                {
                    try
                    {
                        if (ID <= 0)
                        {
                            _plugin.Log.Error("ID " + ID + " was invalid when reading CEntry.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                  SELECT `ID`,
                                         `PlayerID`,
                                         `RuleID`,
                                         `Completed`,
                                         `Failed`,
                                         `Canceled`,
                                         `StartRound`,
                                         `StartTime`,
                                         `CompleteTime`
                                    FROM `adkats_challenge_entry`
                                   WHERE `ID` = @`ID`";
                                command.Parameters.AddWithValue("@ID", ID);
                                using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                                {
                                    if (reader.Read())
                                    {
                                        var playerID = reader.GetInt64("PlayerID");
                                        if (Player != null)
                                        {
                                            // We have a player already
                                            if (Player.player_id != playerID)
                                            {
                                                _plugin.Log.Error("WHAT. Entry " + ID + " changed player on the database. This should never happen.");
                                            }
                                        }
                                        else
                                        {
                                            Player = _plugin.FetchPlayer(false, false, false, null, playerID, null, null, null, null);
                                        }
                                        if (Player == null)
                                        {
                                            _plugin.Log.Error("Unable to fetch player for Entry " + ID + " and player " + playerID + ". Unable to read.");
                                            return;
                                        }
                                        var ruleID = reader.GetInt64("RuleID");
                                        if (Rule != null)
                                        {
                                            // We have a rule already
                                            if (Rule.ID != ruleID)
                                            {
                                                _plugin.Log.Error("NOOOO. Entry changed rule on the database. This should never happen.");
                                            }
                                        }
                                        else
                                        {
                                            Rule = Manager.GetRule(ruleID);
                                        }
                                        if (Rule == null)
                                        {
                                            _plugin.Log.Error("Unable to fetch rule for Entry " + ID + " and rule " + ruleID + ". Unable to read.");
                                            return;
                                        }
                                        Completed = reader.GetBoolean("Completed");
                                        Failed = reader.GetBoolean("Failed");
                                        Canceled = reader.GetBoolean("Canceled");
                                        StartRound = reader.GetInt32("StartRound");
                                        StartTime = reader.GetDateTime("StartTime");
                                        CompleteTime = reader.GetDateTime("CompleteTime");
                                        RefreshProgress(null);
                                    }
                                    else
                                    {
                                        _plugin.Log.Error("Unable to find matching CEntry for ID " + ID);
                                    }
                                }
                            }
                            DBReadDetails(localConnection);
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBRead for CEntry.", e));
                    }
                }

                public void DBReadDetails(MySqlConnection con)
                {
                    try
                    {
                        if (ID <= 0)
                        {
                            _plugin.Log.Error("ID " + ID + " was invalid when reading CEntry details.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                  SELECT `DetailID`,
                                         `VictimID`,
                                         `Weapon`,
                                         `RoundID`,
                                         `DetailTime`
                                    FROM `adkats_challenge_entry_detail`
                                   WHERE `EntryID` = @EntryID
                                ORDER BY `DetailID` ASC";
                                command.Parameters.AddWithValue("@EntryID", ID);
                                using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                                {
                                    lock (Details)
                                    {
                                        var changed = false;
                                        // Clear all existing details, we are fetching them all from the DB
                                        while (reader.Read())
                                        {
                                            var detailID = reader.GetInt32("DetailID");
                                            if (detailID <= 0)
                                            {
                                                _plugin.Log.Error("Entry detail " + ID + ":" + detailID + " was invalid, unable to load.");
                                                continue;
                                            }
                                            if (GetDetails().Any(dDetail => dDetail.DetailID == detailID))
                                            {
                                                // This single message will be VERY spammy once the manager starts being used. Remove after testing.
                                                _plugin.Log.Error("Entry detail " + ID + ":" + detailID + " already loaded. Skipping.");
                                                continue;
                                            }
                                            var detailKiller = Player;
                                            if (detailKiller == null)
                                            {
                                                _plugin.Log.Error("Killer was invalid when loading entry detail " + ID + ":" + detailID + ", unable to load.");
                                                continue;
                                            }
                                            var victimID = reader.GetInt32("VictimID");
                                            var detailVictim = _plugin.FetchPlayer(false, false, false, null, victimID, null, null, null, null);
                                            if (detailVictim == null)
                                            {
                                                _plugin.Log.Error("Victim " + victimID + " was invalid when loading entry detail " + ID + ":" + detailID + ", unable to load.");
                                                continue;
                                            }
                                            var detailWeaponCode = reader.GetString("Weapon");
                                            var detailWeaponDamage = _plugin.WeaponDictionary.GetDamageTypeByWeaponCode(detailWeaponCode);
                                            // Use damage validation to make sure the weapon is real
                                            if (detailWeaponDamage == DamageTypes.None && detailWeaponCode != "Death")
                                            {
                                                _plugin.Log.Error("Weapon " + detailWeaponCode + " was invalid when loading entry detail " + ID + ":" + detailID + ", unable to load.");
                                                continue;
                                            }
                                            var detailRoundID = reader.GetInt32("RoundID");
                                            if (detailRoundID <= 0)
                                            {
                                                _plugin.Log.Error("Round ID " + detailRoundID + " was invalid when loading entry detail " + ID + ":" + detailID + ", unable to load.");
                                                continue;
                                            }
                                            var detailTime = reader.GetDateTime("DetailTime");
                                            // Build the kill object
                                            var kill = new AKill(_plugin)
                                            {
                                                killer = detailKiller,
                                                weaponCode = detailWeaponCode,
                                                weaponDamage = detailWeaponDamage,
                                                victim = detailVictim,
                                                RoundID = detailRoundID,
                                                UTCTimeStamp = detailTime,
                                                TimeStamp = detailTime.ToLocalTime()
                                            };
                                            Details.Add(new CEntryDetail(_plugin, this, detailID, kill, false));
                                            changed = true;
                                        }
                                        if (changed)
                                        {
                                            RefreshProgress(null);
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBReadDetails for CEntry.", e));
                    }
                }

                public Boolean AddKill(AKill aKill)
                {
                    try
                    {
                        if (!Manager.Enabled ||
                            !Rule.Enabled ||
                            Completed ||
                            Failed ||
                            Canceled)
                        {
                            _plugin.Log.Info("Entry " + ID + " was invalid when adding kill.");
                            return false;
                        }
                        // Check for invalid entry
                        if (aKill.killer.player_id != Player.player_id)
                        {
                            _plugin.Log.Info("Kill player " + aKill.killer.GetVerboseName() + " did not match entry player " + Player.GetVerboseName() + ".");
                            return false;
                        }
                        // Check for invalid rule
                        if (Rule == null)
                        {
                            _plugin.Log.Warn("Entry " + ID + " rule was null when trying to add kill: " + aKill.ToString());
                            return false;
                        }
                        // Check for invalid kill
                        if (!Rule.KillValid(aKill))
                        {
                            return false;
                        }
                        lock (Details)
                        {
                            // Create the new detail ID as one more than the current max.
                            var detailID = Details.Select(dDetail => dDetail.DetailID).DefaultIfEmpty(0).Max() + 1;
                            if (detailID <= 0)
                            {
                                _plugin.Log.Error("Entry detail " + ID + ":" + detailID + " was invalid. Generated detail ID was not valid. Unable to add kill.");
                                return false;
                            }
                            if (GetDetails().Any(dDetail => dDetail.DetailID == detailID))
                            {
                                _plugin.Log.Error("Entry detail " + ID + ":" + detailID + " already existed when adding kill. Unable to add kill.");
                                return false;
                            }
                            if (aKill.killer == null || aKill.killer.player_id <= 0)
                            {
                                _plugin.Log.Error("Killer was invalid when adding kill to entry detail " + ID + ":" + detailID + ". Unable to add kill.");
                                return false;
                            }
                            if (aKill.victim == null || aKill.victim.player_id <= 0)
                            {
                                _plugin.Log.Error("Victim was invalid when adding kill to entry detail " + ID + ":" + detailID + ". Unable to add kill.");
                                return false;
                            }
                            // Use damage validation to make sure the weapon is real
                            if (aKill.weaponDamage == DamageTypes.None)
                            {
                                _plugin.Log.Error("Weapon " + aKill.weaponCode + " was invalid when adding kill to entry detail " + ID + ":" + detailID + ". Unable to add kill.");
                                return false;
                            }
                            if (aKill.RoundID <= 0)
                            {
                                _plugin.Log.Error("Round ID " + aKill.RoundID + " was invalid when adding kill to entry detail " + ID + ":" + detailID + ". Unable to add kill.");
                                return false;
                            }

                            // We're good so far. Now make sure the kill is meaningful.
                            // Meaningful being adding it to an available damage/weapon bucket
                            if (!RefreshProgress(aKill))
                            {
                                // Kill was not meaningful
                                return false;
                            }

                            var newDetail = new CEntryDetail(_plugin, this, detailID, aKill, true);
                            newDetail.DBPush(null);
                            // Make sure the upload was successful before adding
                            if (newDetail.Phantom)
                            {
                                _plugin.Log.Error("Challenge entry detail " + ID + ":" + detailID + " could not upload. Unable to add kill.");
                                return false;
                            }

                            // Everything is validated. Add the kill.
                            Details.Add(newDetail);

                            if (Progress.CompletionPercentage >= 99.999)
                            {
                                DoComplete();
                                return true;
                            }

                            Progress.SendStatusForKill(aKill);
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while adding kill to challenge entry.", e));
                    }
                    return false;
                }

                public void AddDeath(AKill inputKill)
                {
                    try
                    {
                        if (!Manager.Enabled ||
                            !Rule.Enabled ||
                            Completed ||
                            Failed ||
                            Canceled)
                        {
                            _plugin.Log.Info("Entry " + ID + " was invalid when adding death.");
                            return;
                        }
                        // Check for invalid entry
                        if (inputKill.victim == null || inputKill.victim.player_id != Player.player_id)
                        {
                            _plugin.Log.Info("Victim player " + inputKill.victim.GetVerboseName() + " did not match entry player " + Player.GetVerboseName() + ".");
                            return;
                        }
                        // Check for invalid rule
                        if (Rule == null)
                        {
                            _plugin.Log.Warn("Entry " + ID + " rule was null when trying to add death: " + inputKill.ToString());
                            return;
                        }
                        if (Died)
                        {
                            return;
                        }
                        lock (Details)
                        {
                            // Create the new detail ID as one more than the current max.
                            var detailID = Details.Select(dDetail => dDetail.DetailID).DefaultIfEmpty(0).Max() + 1;
                            if (detailID <= 0)
                            {
                                _plugin.Log.Error("Entry detail " + ID + ":" + detailID + " was invalid. Generated detail ID was not valid. Unable to add death.");
                                return;
                            }
                            if (GetDetails().Any(dDetail => dDetail.DetailID == detailID))
                            {
                                _plugin.Log.Error("Entry detail " + ID + ":" + detailID + " already existed when adding death. Unable to add death.");
                                return;
                            }
                            if (inputKill.killer == null)
                            {
                                // The killer player was null. This is likely an admin kill.
                                return;
                            }
                            if (inputKill.killer == inputKill.victim || inputKill.IsSuicide)
                            {
                                // They suicided.
                                return;
                            }
                            if (inputKill.RoundID <= 0)
                            {
                                _plugin.Log.Error("Round ID " + inputKill.RoundID + " was invalid when adding death to entry detail " + ID + ":" + detailID + ". Unable to add death.");
                                return;
                            }

                            // Need to make a new kill object
                            // Change the kill code to Death
                            var death = new AKill(_plugin)
                            {
                                killer = inputKill.killer,
                                weaponCode = "Death",
                                weaponDamage = DamageTypes.None,
                                victim = inputKill.victim,
                                IsHeadshot = inputKill.IsHeadshot,
                                IsSuicide = inputKill.IsSuicide,
                                IsTeamkill = inputKill.IsTeamkill,
                                RoundID = inputKill.RoundID,
                                TimeStamp = inputKill.TimeStamp,
                                UTCTimeStamp = inputKill.UTCTimeStamp
                            };

                            var newDetail = new CEntryDetail(_plugin, this, detailID, death, true);
                            newDetail.DBPush(null);
                            // Make sure the upload was successful before adding
                            if (newDetail.Phantom)
                            {
                                _plugin.Log.Error("Challenge entry detail " + ID + ":" + detailID + " could not upload. Unable to add death.");
                                return;
                            }

                            // Everything is validated. Add the death.
                            Details.Add(newDetail);

                            RefreshProgress(null);

                            // We only care about responding to the death if this is a death or time based challenge
                            var commandText = _plugin.GetChatCommandByKey("self_challenge");
                            if (Rule.Completion == CRule.CompletionType.Deaths)
                            {
                                var deaths = Progress.Deaths.Count();
                                var deathsRemaining = Rule.DeathCount - deaths;
                                if (Progress.Deaths.Count() >= Rule.DeathCount)
                                {
                                    DoFail();
                                    return;
                                }
                                var deathS = deathsRemaining != 1 ? "s" : "";
                                _plugin.PlayerSayMessage(Player.player_name, commandText + " You have " + deathsRemaining + " death" + deathS + " remaining.", false, 1);
                                Died = true;
                            }
                            else if (Rule.Completion == CRule.CompletionType.Duration)
                            {
                                var completionTime = StartTime + TimeSpan.FromMinutes(Rule.DurationMinutes);
                                var completionDuration = _plugin.NowDuration(completionTime);
                                _plugin.PlayerSayMessage(Player.player_name, commandText + " You have " + _plugin.FormatTimeString(completionDuration, 3) + " remaining.", false, 1);
                                Died = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while adding death to challenge entry.", e));
                    }
                }

                public void AddSpawn(APlayer player)
                {
                    try
                    {
                        if (!Manager.Enabled ||
                            !Rule.Enabled ||
                            Completed ||
                            Failed ||
                            Canceled)
                        {
                            _plugin.Log.Info("Entry " + ID + " was invalid when adding spawn.");
                            return;
                        }
                        // Check for invalid entry
                        if (player == null || player.player_id != Player.player_id)
                        {
                            _plugin.Log.Info("Spawn player " + player.GetVerboseName() + " did not match entry player " + Player.GetVerboseName() + ".");
                            return;
                        }
                        // Check for invalid rule
                        if (Rule == null)
                        {
                            _plugin.Log.Warn("Entry " + ID + " rule was null when trying to add spawn.");
                            return;
                        }
                        // Reset the flag so they are allowed to accept deaths again.
                        Died = false;
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while adding spawn to challenge entry.", e));
                    }
                }

                public Boolean RefreshProgress(AKill includeKill)
                {
                    try
                    {
                        var killMeaningful = true;

                        if (ID <= 0 ||
                            Rule == null ||
                            Rule.ID <= 0 ||
                            Rule.Definition == null ||
                            Rule.Definition.ID <= 0)
                        {
                            _plugin.Log.Error("Entry was invalid when refreshing progress.");
                            return false;
                        }
                        var details = Rule.Definition.GetDetails();
                        if (!details.Any())
                        {
                            _plugin.Log.Error("Cannot refresh progress, no damage types added to definition " + Rule.Definition.ID + ".");
                            return false;
                        }

                        // Build the necessary buckets
                        // Damage buckets
                        var damageDetails = details.Where(detail => detail.Type == CDefinition.CDefinitionDetail.DetailType.Damage &&
                                                                    detail.Damage != CDefinition.CDefinitionDetail.DetailDamage.None &&
                                                                    detail.KillCount > 0).ToList();
                        var damageBuckets = new List<DamageBucket>();
                        foreach (var detail in damageDetails)
                        {
                            var damageBucket = new DamageBucket(_plugin)
                            {
                                Damage = detail.Damage,
                                WeaponCount = detail.WeaponCount,
                                MaxKillsPerWeapon = detail.KillCount
                            };
                            var weaponCodes = new List<String>();
                            foreach (var damageType in detail.GetDamageTypes())
                            {
                                weaponCodes.AddRange(_plugin.WeaponDictionary.GetWeaponCodesOfDamageType(damageType));
                            }
                            foreach (var weaponCode in weaponCodes)
                            {
                                damageBucket.Weapons[weaponCode] = new WeaponBucket(_plugin)
                                {
                                    WeaponCode = weaponCode,
                                    MaxKills = detail.KillCount
                                };
                            }
                            if (!damageBuckets.Contains(damageBucket))
                            {
                                damageBuckets.Add(damageBucket);
                            }
                        }
                        // Weapon buckets
                        var weaponDetails = details.Where(detail => detail.Type == CDefinition.CDefinitionDetail.DetailType.Weapon &&
                                                                    !String.IsNullOrEmpty(detail.Weapon) &&
                                                                    detail.KillCount > 0).ToList();
                        var weaponBuckets = new List<WeaponBucket>();
                        foreach (var detail in weaponDetails)
                        {
                            var weaponBucket = new WeaponBucket(_plugin)
                            {
                                WeaponCode = detail.Weapon,
                                MaxKills = detail.KillCount
                            };
                            if (!weaponBuckets.Contains(weaponBucket))
                            {
                                weaponBuckets.Add(weaponBucket);
                            }
                        }
                        var deathBucket = new List<AKill>();

                        var detailKills = GetDetails().Select(dDetail => dDetail.Kill).ToList();
                        if (includeKill != null)
                        {
                            detailKills.Add(includeKill);
                        }
                        foreach (var kill in detailKills.OrderBy(dKill => dKill.TimeStamp))
                        {
                            if (kill.weaponCode == "Death" && kill.weaponDamage == DamageTypes.None)
                            {
                                deathBucket.Add(kill);
                                continue;
                            }

                            // See if it matches a specific weapon requirement
                            if (weaponBuckets.Any())
                            {
                                var matchingWeaponBucket = weaponBuckets.FirstOrDefault(bucket => bucket.WeaponCode == kill.weaponCode);
                                if (matchingWeaponBucket != null)
                                {
                                    // It does. See if we can assign the kill to the damage type.
                                    if (matchingWeaponBucket.AddKill(kill))
                                    {
                                        // Kill added, get out
                                        continue;
                                    }
                                }
                            }

                            // Couldn't assign the kill to a specific weapon requirement
                            // Check if the current rule has a need for this kill's damage type
                            // A weapon can thankfully only belong to one damage type
                            DamageBucket matchingDamageBucket = null;
                            foreach (var bucket in damageBuckets)
                            {
                                if (bucket.Weapons.ContainsKey(kill.weaponCode))
                                {
                                    matchingDamageBucket = bucket;
                                    break;
                                }
                            }
                            if (matchingDamageBucket != null)
                            {
                                // It does. See if we can assign the kill to the damage type.
                                if (matchingDamageBucket.Weapons[kill.weaponCode].AddKill(kill))
                                {
                                    // Kill added, get out
                                    continue;
                                }
                            }
                            // Unable to assign the kill. Either we don't have a slot for it, or all the slots for it are full..
                            if (includeKill != null && includeKill == kill)
                            {
                                // We are testing a kill and it couldn't be added anywhere
                                killMeaningful = false;
                            }
                        }
                        Progress = new EntryProgress(_plugin, this, damageBuckets, weaponBuckets, deathBucket);
                        if (includeKill != null)
                        {
                            return killMeaningful;
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error while getting challenge rule completion status.", e));
                    }
                    return false;
                }

                public override string ToString()
                {
                    if (Player == null)
                    {
                        return "PlayerInvalid";
                    }
                    if (Rule == null)
                    {
                        return "RuleInvalid";
                    }
                    if (Progress == null)
                    {
                        RefreshProgress(null);
                    }
                    return Rule.Name + " - " + Player.GetVerboseName() + " - " + Math.Round(Progress.CompletionPercentage) + "% - " + Progress.TotalCompletedKills + " Kills - " + Progress.TotalRequiredKills + " Required";
                }

                public class EntryProgress
                {
                    private AdKats _plugin;

                    private CEntry Entry;
                    private List<DamageBucket> DamageBuckets;
                    private List<WeaponBucket> WeaponBuckets;
                    public List<AKill> Deaths;

                    public Int32 TotalRequiredKills
                    {
                        get; private set;
                    }
                    public Int32 TotalCompletedKills
                    {
                        get; private set;
                    }
                    public Double CompletionPercentage
                    {
                        get; private set;
                    }

                    public EntryProgress(AdKats plugin, CEntry entry, List<DamageBucket> damageBuckets, List<WeaponBucket> weaponBuckets, List<AKill> deaths)
                    {
                        _plugin = plugin;

                        try
                        {
                            Entry = entry;
                            DamageBuckets = damageBuckets;
                            if (DamageBuckets == null)
                            {
                                DamageBuckets = new List<DamageBucket>();
                            }
                            WeaponBuckets = weaponBuckets;
                            if (WeaponBuckets == null)
                            {
                                WeaponBuckets = new List<WeaponBucket>();
                            }
                            Deaths = deaths;
                            if (Deaths == null)
                            {
                                Deaths = new List<AKill>();
                            }

                            // Total for all required kills
                            TotalRequiredKills = DamageBuckets.Sum(bucket => bucket.WeaponCount * bucket.MaxKillsPerWeapon) + WeaponBuckets.Sum(bucket => bucket.MaxKills);

                            // Total for all completed kills
                            TotalCompletedKills = damageBuckets.Sum(bucket => bucket.GetWeapons().Sum(weapon => weapon.Kills.Count())) + weaponBuckets.Sum(weapon => weapon.Kills.Count());

                            // Total completion percentage
                            CompletionPercentage = Math.Max(Math.Min(100 * (Double)TotalCompletedKills / (Double)TotalRequiredKills, 100), 0);
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error creating entry status.", e));
                        }
                    }

                    public void SendStatusForKill(AKill kill)
                    {
                        if (kill.weaponDamage == DamageTypes.None)
                        {
                            _plugin.Log.Error("Damage type for current kill couldn't be found, or was None.");
                        }
                        try
                        {
                            var requiredKills = 0;
                            var completedKills = 0;
                            WeaponBucket weaponBucket = WeaponBuckets.FirstOrDefault(bucket => bucket.WeaponCode == kill.weaponCode);

                            if (weaponBucket != null)
                            {
                                requiredKills += weaponBucket.MaxKills;
                                completedKills += weaponBucket.Kills.Count();
                            }

                            WeaponBucket damageWeaponBucket = null;
                            foreach (var damageBucket in DamageBuckets)
                            {
                                if (damageBucket.Weapons.ContainsKey(kill.weaponCode))
                                {
                                    damageWeaponBucket = damageBucket.Weapons[kill.weaponCode];
                                    requiredKills += damageWeaponBucket.MaxKills;
                                    completedKills += damageWeaponBucket.Kills.Count();
                                    break;
                                }
                            }

                            var weaponCompletionPercentage = Math.Max(Math.Min(100 * (Double)completedKills / (Double)requiredKills, 100), 0);

                            String weaponName = _plugin.WeaponDictionary.GetShortWeaponNameByCode(kill.weaponCode);
                            String completion = "";
                            var respond = true;
                            var completeFlag = false;
                            if (weaponCompletionPercentage >= 99.999)
                            {
                                completion = "COMPLETED!";
                                if (CompletionPercentage < 99.999)
                                {
                                    completion += " Try another weapon!";
                                    completeFlag = true;
                                }
                                if (Entry.LastCompletedWeapon == kill.weaponCode)
                                {
                                    respond = false;
                                }
                                Entry.LastCompletedWeapon = kill.weaponCode;
                            }
                            if (!respond)
                            {
                                return;
                            }
                            var commandText = _plugin.GetChatCommandByKey("self_challenge");
                            var completionSayMessage = commandText + " " + weaponName + " [" + completedKills + "/" + requiredKills + "][" + Math.Round(CompletionPercentage) + "%] " + completion;
                            if (!String.IsNullOrEmpty(completion))
                            {
                                // We are complete, mark the message in pink
                                completionSayMessage = _plugin.Log.CPink(completionSayMessage);
                                kill.killer.Say(completionSayMessage);
                            }
                            else
                            {
                                _plugin.PlayerSayMessage(kill.killer.player_name, completionSayMessage, false, 1);
                            }
                            if (completeFlag)
                            {
                                _plugin.PlayerYellMessage(kill.killer.player_name, completion, false, 1);
                                if (_plugin.GetMatchingVerboseASPlayersOfGroup("challenge_autokill", kill.killer).Any())
                                {
                                    _plugin.Threading.Wait(250);
                                    _plugin.ExecuteCommand("procon.protected.send", "admin.killPlayer", kill.killer.player_name);
                                    kill.killer.Say(_plugin.Log.CPink("Killed automatically. To disable type " + commandText + " autokill"));
                                }
                                else
                                {
                                    if (!Entry.AutoKillTold)
                                    {
                                        kill.killer.Say("Manual admin kill: " + commandText + " k | Auto admin kill: " + commandText + " autokill");
                                        Entry.AutoKillTold = true;
                                    }
                                    Entry.kAllowed = true;
                                }
                            }
                            return;
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error getting status for kill.", e));
                        }
                    }

                    public override string ToString()
                    {
                        try
                        {
                            var status = "";
                            if (Entry.Rule == null)
                            {
                                return "ERROR4";
                            }
                            if (!DamageBuckets.Any() && !WeaponBuckets.Any())
                            {
                                status += "No damage or weapons defined.";
                                return status;
                            }
                            foreach (var weapon in WeaponBuckets)
                            {
                                status += weapon.ToString() + Environment.NewLine;
                            }
                            foreach (var damage in DamageBuckets)
                            {
                                status += damage.ToString() + Environment.NewLine;
                            }
                            return status;
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error getting entry status string.", e));
                        }
                        return "ERROR5";
                    }
                }

                public class DamageBucket
                {
                    private AdKats _plugin;

                    public CDefinition.CDefinitionDetail.DetailDamage Damage;
                    public Int32 WeaponCount;
                    public Int32 MaxKillsPerWeapon;
                    public Dictionary<String, WeaponBucket> Weapons;

                    public DamageBucket(AdKats plugin)
                    {
                        _plugin = plugin;
                        Damage = CDefinition.CDefinitionDetail.DetailDamage.None;
                        Weapons = new Dictionary<String, WeaponBucket>();
                    }

                    public List<WeaponBucket> GetWeapons()
                    {
                        var buckets = new List<WeaponBucket>();
                        try
                        {
                            // Add buckets until the weapon count is reached or we run out of buckets
                            foreach (var bucket in Weapons.Values.Where(dBucket => dBucket.Kills.Count() > 0)
                                                                 .OrderByDescending(dBucket => dBucket.Kills.Count()))
                            {
                                if (buckets.Count() >= WeaponCount)
                                {
                                    break;
                                }
                                buckets.Add(bucket);
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error while getting damage bucket weapons.", e));
                        }
                        return buckets;
                    }

                    public override string ToString()
                    {
                        try
                        {
                            var requiredKills = WeaponCount * MaxKillsPerWeapon;
                            var weaponBuckets = GetWeapons();
                            var completedKills = weaponBuckets.Sum(weapon => weapon.Kills.Count());
                            var completionPercentage = Math.Max(Math.Min(100 * (Double)completedKills / (Double)requiredKills, 100), 0);

                            var status = "Type " + Damage.ToString().Replace("_", " ") + " [" + completedKills + "/" + requiredKills + "][" + Math.Round(completionPercentage) + "%]:";
                            if (weaponBuckets.Any())
                            {
                                foreach (var weaponString in weaponBuckets.Select(bucket => bucket.ToString()))
                                {
                                    status += Environment.NewLine + "-- " + weaponString;
                                }
                            }
                            else
                            {
                                status += Environment.NewLine + "No weapons used yet.";
                            }
                            return status;
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error while getting damage bucket string.", e));
                        }
                        return String.Empty;
                    }
                }

                public class WeaponBucket
                {
                    private AdKats _plugin;

                    public Boolean Completed;
                    public String WeaponCode;
                    public List<AKill> Kills;
                    public Int32 MaxKills;

                    public WeaponBucket(AdKats plugin)
                    {
                        _plugin = plugin;
                        Kills = new List<AKill>();
                    }

                    public Boolean AddKill(AKill kill)
                    {
                        try
                        {
                            if (Kills.Count() < MaxKills)
                            {
                                Kills.Add(kill);
                                return true;
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error while adding kill to weapon bucket.", e));
                        }
                        return false;
                    }

                    public override string ToString()
                    {
                        try
                        {
                            var completedKills = Kills.Count();
                            var completionPercentage = Math.Max(Math.Min(100 * (Double)completedKills / (Double)MaxKills, 100), 0);
                            var weaponName = _plugin.WeaponDictionary.GetShortWeaponNameByCode(WeaponCode);

                            return weaponName + " [" + completedKills + "/" + MaxKills + "][" + Math.Round(completionPercentage) + "%]";
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error while getting weapon bucket string.", e));
                        }
                        return String.Empty;
                    }
                }

                public class CEntryDetail
                {
                    private AdKats _plugin;

                    public Boolean Phantom;

                    // None of these should be changed after creation
                    public CEntry Entry
                    {
                        get; private set;
                    }
                    public Int64 DetailID
                    {
                        get; private set;
                    }
                    public AKill Kill
                    {
                        get; private set;
                    }

                    public CEntryDetail(AdKats plugin, CEntry entry, Int64 detailID, AKill kill, Boolean phantom)
                    {
                        _plugin = plugin;
                        Entry = entry;
                        DetailID = detailID;
                        Kill = kill;
                        Phantom = phantom;
                    }

                    // Done
                    public void DBPush(MySqlConnection con)
                    {
                        try
                        {
                            var localConnection = con;
                            if (localConnection == null)
                            {
                                localConnection = _plugin.GetDatabaseConnection();
                            }
                            try
                            {
                                using (MySqlCommand command = localConnection.CreateCommand())
                                {
                                    command.CommandText = @"
                                    INSERT INTO
                                        `adkats_challenge_entry_detail`
                                    (
                                        `EntryID`,
                                        `DetailID`,
                                        `VictimID`,
                                        `Weapon`,
                                        `RoundID`,
                                        `DetailTime`
                                    )
                                    VALUES
                                    (
                                        @EntryID,
                                        @DetailID,
                                        @VictimID,
                                        @Weapon,
                                        @RoundID,
                                        @DetailTime
                                    )";
                                    command.Parameters.AddWithValue("@EntryID", Entry.ID);
                                    command.Parameters.AddWithValue("@DetailID", DetailID);
                                    command.Parameters.AddWithValue("@VictimID", Kill.victim.player_id);
                                    command.Parameters.AddWithValue("@Weapon", Kill.weaponCode);
                                    command.Parameters.AddWithValue("@RoundID", Kill.RoundID);
                                    command.Parameters.AddWithValue("@DetailTime", Kill.UTCTimeStamp);
                                    if (_plugin.SafeExecuteNonQuery(command) > 0)
                                    {
                                        // This record is no longer phantom
                                        Phantom = false;
                                    }
                                }
                            }
                            finally
                            {
                                if (con == null &&
                                    localConnection != null)
                                {
                                    localConnection.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error performing DBPush for CEntryDetail.", e));
                        }
                    }

                    public void DBRead(MySqlConnection con)
                    {
                        try
                        {
                            if (Entry == null ||
                                Entry.ID <= 0 ||
                                Entry.Player == null ||
                                DetailID <= 0)
                            {
                                _plugin.Log.Error("Challenge entry detail was invalid when reading.");
                                return;
                            }
                            var localConnection = con;
                            if (localConnection == null)
                            {
                                localConnection = _plugin.GetDatabaseConnection();
                            }
                            try
                            {
                                using (MySqlCommand command = localConnection.CreateCommand())
                                {
                                    command.CommandText = @"
                                      SELECT `VictimID`,
                                             `Weapon`,
                                             `RoundID`,
                                             `DetailTime`
                                        FROM `adkats_challenge_entry_detail`
                                       WHERE `EntryID` = @EntryID
                                         AND `DetailID` = @DetailID";
                                    command.Parameters.AddWithValue("@EntryID", Entry.ID);
                                    command.Parameters.AddWithValue("@DetailID", DetailID);
                                    using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                                    {
                                        if (reader.Read())
                                        {
                                            var victimID = reader.GetInt32("VictimID");
                                            var detailVictim = _plugin.FetchPlayer(false, false, false, null, victimID, null, null, null, null);
                                            if (detailVictim == null)
                                            {
                                                _plugin.Log.Error("Victim " + victimID + " was invalid when loading entry detail " + Entry.ID + ":" + DetailID + ", unable to load.");
                                                return;
                                            }
                                            var detailWeaponCode = reader.GetString("Weapon");
                                            var detailWeaponDamage = _plugin.WeaponDictionary.GetDamageTypeByWeaponCode(detailWeaponCode);
                                            // Use damage validation to make sure the weapon is real
                                            if (detailWeaponDamage == DamageTypes.None && detailWeaponCode != "Death")
                                            {
                                                _plugin.Log.Error("Weapon " + detailWeaponCode + " was invalid when loading entry detail " + Entry.ID + ":" + DetailID + ", unable to load.");
                                                return;
                                            }
                                            var detailRoundID = reader.GetInt32("RoundID");
                                            if (detailRoundID <= 0)
                                            {
                                                _plugin.Log.Error("Round ID " + detailRoundID + " was invalid when loading entry detail " + Entry.ID + ":" + DetailID + ", unable to load.");
                                                return;
                                            }
                                            var detailTime = reader.GetDateTime("DetailTime");
                                            // Build the kill object
                                            var kill = new AKill(_plugin)
                                            {
                                                killer = Entry.Player,
                                                weaponCode = detailWeaponCode,
                                                weaponDamage = detailWeaponDamage,
                                                victim = detailVictim,
                                                RoundID = detailRoundID,
                                                UTCTimeStamp = detailTime,
                                                TimeStamp = detailTime.ToLocalTime()
                                            };
                                        }
                                        else
                                        {
                                            _plugin.Log.Error("Unable to find matching CEntryDetail " + Entry.ID + ":" + DetailID + ".");
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                if (con == null &&
                                    localConnection != null)
                                {
                                    localConnection.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error performing DBRead for CEntryDetail.", e));
                        }
                    }
                }
            }

            public class CReward
            {
                public enum RewardType
                {
                    None,
                    ReservedSlot,
                    SpectatorSlot,
                    BalanceWhitelist,
                    PingWhitelist,
                    TeamKillTrackerWhitelist,
                    CommandLock
                }
                public static String RewardTypeEnumString = "enum.ChallengeRewardType(None|ReservedSlot|SpectatorSlot|BalanceWhitelist|PingWhitelist|TeamKillTrackerWhitelist|CommandLock)";

                private AdKats _plugin;

                public Boolean Phantom;

                private AChallengeManager Manager;
                public Int64 ID;
                public Int64 ServerID;
                public Int32 Tier;
                public RewardType Reward = RewardType.None;
                public Boolean Enabled;
                public Int32 DurationMinutes;
                public DateTime CreateTime;
                public DateTime ModifyTime;

                public String getDescriptionString(APlayer player)
                {
                    if (Reward == RewardType.None)
                    {
                        return "";
                    }
                    var rewardString = "+" + getDurationString();
                    switch (Reward)
                    {
                        case CReward.RewardType.ReservedSlot:
                            rewardString += " reserved slot";
                            break;
                        case CReward.RewardType.SpectatorSlot:
                            rewardString += " spectator slot";
                            break;
                        case CReward.RewardType.BalanceWhitelist:
                            rewardString += " autobalance whitelist";
                            break;
                        case CReward.RewardType.PingWhitelist:
                            rewardString += " ping whitelist";
                            break;
                        case CReward.RewardType.TeamKillTrackerWhitelist:
                            rewardString += " teamkill whitelist";
                            break;
                        case CReward.RewardType.CommandLock:
                            var lockString = "command lock";
                            if (_plugin._UseExperimentalTools)
                            {
                                lockString = "rule breaking allowed";
                            }
                            rewardString += " " + lockString;
                            if (player != null)
                            {
                                var lockCommand = _plugin.GetCommandByKey("player_lock");
                                var recentLock = _plugin.FetchRecentRecords(player.player_id, lockCommand.command_id, 1000, 1, true, false).FirstOrDefault();
                                if (recentLock != null && recentLock.source_name == "ChallengeManager")
                                {
                                    var durationSinceLast = _plugin.NowDuration(recentLock.record_time);
                                    if (durationSinceLast.TotalHours < Manager.CommandLockTimeoutHours)
                                    {
                                        var durationTillActive = _plugin.NowDuration(recentLock.record_time.AddHours(Manager.CommandLockTimeoutHours));
                                        rewardString += " (" + _plugin.FormatTimeString(durationTillActive, 2) + " timeout)";
                                    }
                                }
                            }
                            break;
                    }
                    return rewardString;
                }

                public String getDurationString()
                {
                    return _plugin.FormatTimeString(TimeSpan.FromMinutes(DurationMinutes), 2);
                }

                public CReward(AdKats plugin, AChallengeManager manager, Boolean phantom)
                {
                    _plugin = plugin;
                    Manager = manager;
                    Phantom = phantom;
                    CreateTime = _plugin.UtcNow();
                    ModifyTime = _plugin.UtcNow();
                    Tier = 1;
                    DurationMinutes = 1440;
                    Reward = RewardType.None;
                }

                public void DBPush(MySqlConnection con)
                {
                    try
                    {
                        if (con == null)
                        {
                            con = _plugin.GetDatabaseConnection();
                        }
                        if (Phantom)
                        {
                            DBCreate(con);
                        }
                        else
                        {
                            DBUpdate(con);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBPush for CReward.", e));
                    }
                }

                private void DBCreate(MySqlConnection con)
                {
                    try
                    {
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                INSERT INTO
                                    `adkats_challenge_reward`
                                (
                                    `ServerID`,
                                    `Tier`,
                                    `Reward`,
                                    `Enabled`,
                                    `DurationMinutes`,
                                    `CreateTime`,
                                    `ModifyTime`
                                )
                                VALUES
                                (
                                    @ServerID,
                                    @Tier,
                                    @Reward,
                                    @Enabled,
                                    @DurationMinutes,
                                    @CreateTime,
                                    @ModifyTime
                                )";
                                if (ServerID <= 0)
                                {
                                    // This rule doesn't have an associated server ID, and we're creating it.
                                    // Assign this server's ID
                                    ServerID = _plugin._serverInfo.ServerID;
                                }
                                command.Parameters.AddWithValue("@ServerID", ServerID);
                                command.Parameters.AddWithValue("@Tier", Tier);
                                command.Parameters.AddWithValue("@Reward", Reward.ToString());
                                command.Parameters.AddWithValue("@Enabled", Enabled);
                                command.Parameters.AddWithValue("@DurationMinutes", DurationMinutes);
                                command.Parameters.AddWithValue("@CreateTime", CreateTime);
                                command.Parameters.AddWithValue("@ModifyTime", ModifyTime);
                                if (_plugin.SafeExecuteNonQuery(command) > 0)
                                {
                                    ID = command.LastInsertedId;
                                    // This record is no longer phantom
                                    Phantom = false;
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBCreate for CReward.", e));
                    }
                }

                private void DBUpdate(MySqlConnection con)
                {
                    try
                    {
                        if (ID <= 0 ||
                            Phantom)
                        {
                            _plugin.Log.Error("CReward was invalid when updating.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                UPDATE
                                    `adkats_challenge_reward`
                                SET
                                    `ServerID` = @ServerID,
                                    `Tier` = @Tier,
                                    `Reward` = @Reward,
                                    `Enabled` = @Enabled,
                                    `DurationMinutes` = @DurationMinutes,
                                    `ModifyTime` = @ModifyTime
                                WHERE `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                command.Parameters.AddWithValue("@ServerID", ServerID);
                                command.Parameters.AddWithValue("@Tier", Tier);
                                command.Parameters.AddWithValue("@Reward", Reward.ToString());
                                command.Parameters.AddWithValue("@Enabled", Enabled);
                                command.Parameters.AddWithValue("@DurationMinutes", DurationMinutes);
                                command.Parameters.AddWithValue("@ModifyTime", ModifyTime);
                                if (_plugin.SafeExecuteNonQuery(command) <= 0)
                                {
                                    _plugin.Log.Error("Failed to update CReward " + this.ToString() + " in database.");
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBUpdate for CReward.", e));
                    }
                }

                public void DBRead(MySqlConnection con)
                {
                    try
                    {
                        if (ID <= 0 ||
                            Phantom)
                        {
                            _plugin.Log.Error("CReward was invalid when reading.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {
                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                  SELECT `ServerID`,
                                         `Tier`,
                                         `Reward`,
                                         `Enabled`,
                                         `DurationMinutes`,
                                         `CreateTime`,
                                         `ModifyTime`
                                    FROM `adkats_challenge_reward`
                                   WHERE `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                var push = false;
                                var delete = false;
                                using (MySqlDataReader reader = _plugin.SafeExecuteReader(command))
                                {
                                    if (reader.Read())
                                    {
                                        ServerID = reader.GetInt64("ServerID");
                                        if (ServerID != _plugin._serverInfo.ServerID)
                                        {
                                            _plugin.Log.Error("CReward " + this.ToString() + " was loaded, but belongs to another server.");
                                        }
                                        Tier = reader.GetInt32("Tier");
                                        Reward = (RewardType)Enum.Parse(typeof(RewardType), reader.GetString("Reward"));
                                        Enabled = reader.GetBoolean("Enabled");
                                        DurationMinutes = reader.GetInt32("DurationMinutes");
                                        CreateTime = reader.GetDateTime("CreateTime");
                                        ModifyTime = reader.GetDateTime("ModifyTime");
                                    }
                                    else
                                    {
                                        _plugin.Log.Error("Unable to find matching CReward for ID " + ID + ".");
                                        delete = true;
                                    }
                                }
                                if (delete)
                                {
                                    DBDelete(localConnection);
                                }
                                else if (push)
                                {
                                    DBPush(localConnection);
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBRead for CReward.", e));
                    }
                }

                public void DBDelete(MySqlConnection con)
                {
                    try
                    {
                        if (ServerID <= 0 ||
                            Tier < 1 ||
                            Tier > 10 ||
                            Phantom)
                        {
                            _plugin.Log.Error("CReward was invalid when deleting.");
                            return;
                        }
                        var localConnection = con;
                        if (localConnection == null)
                        {
                            localConnection = _plugin.GetDatabaseConnection();
                        }
                        try
                        {

                            using (MySqlCommand command = localConnection.CreateCommand())
                            {
                                command.CommandText = @"
                                DELETE FROM
                                    `adkats_challenge_reward`
                                WHERE `ID` = @ID";
                                command.Parameters.AddWithValue("@ID", ID);
                                if (_plugin.SafeExecuteNonQuery(command) > 0)
                                {
                                    // SUCCESS
                                    _plugin.Log.Info("Deleted CReward " + this.ToString() + ".");
                                    Manager.DeleteReward(ID);
                                }
                            }
                        }
                        finally
                        {
                            if (con == null &&
                                localConnection != null)
                            {
                                localConnection.Dispose();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error performing DBDelete for CReward.", e));
                    }
                }

                public void SetEnabledByString(String enabled)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(enabled))
                        {
                            _plugin.Log.Error("Enabled was empty when setting by string.");
                            return;
                        }
                        var newEnabled = Boolean.Parse(enabled);
                        if (newEnabled != Enabled)
                        {
                            Enabled = newEnabled;
                            ModifyTime = _plugin.UtcNow();
                            DBPush(null);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating enabled state by string for CReward.", e));
                    }
                }

                public void SetTierByString(String tier)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(tier))
                        {
                            _plugin.Log.Error("Rule tier was empty when setting by string.");
                            return;
                        }
                        var newTier = Int32.Parse(tier);
                        if (newTier < 1)
                        {
                            _plugin.Log.Error("Rule tier cannot be less than 1.");
                            newTier = 1;
                        }
                        if (newTier > 10)
                        {
                            _plugin.Log.Error("Rule tier cannot be greter than 10.");
                            newTier = 10;
                        }
                        if (newTier == Tier)
                        {
                            _plugin.Log.Info("Old tier and new tier were the same when setting by string.");
                            return;
                        }
                        Tier = newTier;
                        ModifyTime = _plugin.UtcNow();
                        DBPush(null);
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating tier by string for CReward.", e));
                    }
                }

                public void SetRewardTypeByString(String rewardType)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(rewardType))
                        {
                            _plugin.Log.Error("Reward type was empty when setting by string.");
                            return;
                        }
                        RewardType newReward = RewardType.None;
                        try
                        {
                            newReward = (RewardType)Enum.Parse(typeof(RewardType), rewardType);
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error while parsing reward type by string.", e));
                        }
                        if (Reward == newReward)
                        {
                            _plugin.Log.Error("Old reward type and new reward type were the same when setting by string.");
                            return;
                        }
                        if (Manager.GetRewards().Any(reward => reward.Reward == newReward && reward.Tier == Tier))
                        {
                            _plugin.Log.Error("Tier " + Tier + " already has a " + newReward.ToString() + " reward.");
                            return;
                        }
                        Reward = newReward;
                        ModifyTime = _plugin.UtcNow();
                        DBPush(null);
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating reward type by string for CReward.", e));
                    }
                }

                public void SetDurationMinutesByString(String durationMinutes)
                {
                    try
                    {
                        if (String.IsNullOrEmpty(durationMinutes))
                        {
                            _plugin.Log.Error("Duration minutes was empty when setting by string.");
                            return;
                        }
                        var newDurationMinutes = Int32.Parse(durationMinutes);
                        if (newDurationMinutes < 5)
                        {
                            _plugin.Log.Error("Duration of reward cannot be less than 5 minutes.");
                            newDurationMinutes = 5;
                        }
                        if (newDurationMinutes != DurationMinutes)
                        {
                            DurationMinutes = newDurationMinutes;
                            ModifyTime = _plugin.UtcNow();
                            DBPush(null);
                        }
                    }
                    catch (Exception e)
                    {
                        _plugin.Log.HandleException(new AException("Error updating minute duration by string for CRule.", e));
                    }
                }

                public override string ToString()
                {
                    return ID + " (" + ServerID + "/" + Tier + "/" + Reward.ToString() + ")";
                }
            }
        }


    }
}
