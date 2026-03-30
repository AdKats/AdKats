using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PRoCon.Core;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        public void BuildUnreadySettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (!_settingsLocked)
                {
                    if (_useKeepAlive)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("0") + t + "Auto-Enable/Keep-Alive", typeof(Boolean), true));
                    }

                    buildList.Add(new CPluginVariable("Complete these settings before enabling.", typeof(String), "Once enabled, more settings will appear."));
                    //SQL Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Hostname", typeof(String), _mySqlHostname));
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Port", typeof(String), _mySqlPort));
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Database", typeof(String), _mySqlSchemaName));
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Username", typeof(String), _mySqlUsername));
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Password", typeof(String), _mySqlPassword));
                }
                //Debugging Settings
                buildList.Add(new CPluginVariable(GetSettingSection("D99") + t + "Debug level", typeof(Int32), Log.DebugLevel));
                //Database Timing
                if (_dbTimingChecked && !_dbTimingValid)
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("D98") + t + "Override Timing Confirmation", typeof(Boolean), _timingValidOverride));
                }

                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building unready setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("0") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildReadyLockedSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Server ID (Display)", typeof(int), _serverInfo.ServerID));
                buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Server IP (Display)", typeof(String), _serverInfo.ServerIP));
                buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Server Round (Display)", typeof(String), _roundID));
                if (_UseBanEnforcer)
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("A13-3") + t + "NAME Ban Count", typeof(int), _NameBanCount));
                    buildList.Add(new CPluginVariable(GetSettingSection("A13-3") + t + "GUID Ban Count", typeof(int), _GUIDBanCount));
                    buildList.Add(new CPluginVariable(GetSettingSection("A13-3") + t + "IP Ban Count", typeof(int), _IPBanCount));
                    buildList.Add(new CPluginVariable(GetSettingSection("A13-3") + t + "Ban Search", typeof(String), ""));
                    buildList.AddRange(_BanEnforcerSearchResults.Select(aBan => new CPluginVariable(GetSettingSection("A13-3") + t + "BAN" + aBan.ban_id + s + aBan.ban_record.target_player.player_name + s + aBan.ban_record.source_name + s + aBan.ban_record.record_message, "enum.commandActiveEnum(Active|Disabled|Expired)", aBan.ban_status)));
                }
                buildList.Add(new CPluginVariable(GetSettingSection("D99") + t + "Debug level", typeof(int), Log.DebugLevel));

                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building ready locked setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("1") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildServerSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("1"))
                {
                    //Server Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Setting Import", typeof(String), _serverInfo.ServerID));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Server ID (Display)", typeof(Int32), _serverInfo.ServerID));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Server IP (Display)", typeof(String), _serverInfo.ServerIP));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Server Round (Display)", typeof(String), _roundID));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Server Game (Display)", typeof(String), GameVersion.ToString()));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Short Server Name", typeof(String), _shortServerName));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Low Population Value", typeof(Int32), _lowPopulationPlayerCount));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "High Population Value", typeof(Int32), _highPopulationPlayerCount));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Automatic Server Restart When Empty", typeof(Boolean), _automaticServerRestart));
                    if (_automaticServerRestart)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Automatic Restart Minimum Uptime Hours", typeof(Int32), _automaticServerRestartMinHours));
                        buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Automatic Procon Reboot When Server Reboots", typeof(Boolean), _automaticServerRestartProcon));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Procon Memory Usage MB (Display)", typeof(Int32), _MemoryUsageCurrent));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Procon Memory Usage MB Warning", typeof(Int32), _MemoryUsageWarn));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Procon Memory Usage MB AdKats Restart", typeof(Int32), _MemoryUsageRestartPlugin));
                    buildList.Add(new CPluginVariable(GetSettingSection("1") + t + "Procon Memory Usage MB Procon Restart", typeof(Int32), _MemoryUsageRestartProcon));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building server setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("1") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildSQLSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("2"))
                {
                    //SQL Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Hostname", typeof(String), _mySqlHostname));
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Port", typeof(String), _mySqlPort));
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Database", typeof(String), _mySqlSchemaName));
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Username", typeof(String), _mySqlUsername));
                    buildList.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Password", typeof(String), _mySqlPassword));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building sql setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("2") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildUserSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("3"))
                {
                    //User Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("3") + t + "Add User", typeof(String), ""));
                    if (_userCache.Count > 0)
                    {
                        //Sort access list by access level, then by id
                        List<AUser> tempAccess = _userCache.Values.ToList();
                        tempAccess.Sort((a1, a2) => (a1.user_role.role_powerLevel == a2.user_role.role_powerLevel) ? (String.CompareOrdinal(a1.user_name.ToLower(), a2.user_name.ToLower())) : ((a1.user_role.role_powerLevel > a2.user_role.role_powerLevel) ? (-1) : (1)));
                        String roleEnum = String.Empty;
                        if (_RoleKeyDictionary.Count > 0)
                        {
                            Random random = new Random();
                            foreach (ARole role in _RoleKeyDictionary.Values.ToList())
                            {
                                if (String.IsNullOrEmpty(roleEnum))
                                {
                                    roleEnum += "enum.RoleEnum_" + random.Next(100000, 999999) + "(";
                                }
                                else
                                {
                                    roleEnum += "" + t.ToString();
                                }
                                roleEnum += role.role_name;
                            }
                            roleEnum += ")";
                        }
                        foreach (AUser user in tempAccess)
                        {
                            String userPrefix = GetSettingSection("3") + t + "USR" + user.user_id + s + user.user_name + s;
                            if (_UseEmail)
                            {
                                buildList.Add(new CPluginVariable(userPrefix + "User Email", typeof(String), user.user_email));
                            }
                            buildList.Add(new CPluginVariable(userPrefix + "User Expiration", typeof(String), user.user_expiration.ToShortDateString()));
                            buildList.Add(new CPluginVariable(userPrefix + "User Notes", typeof(String), user.user_notes));
                            //Do not display phone input until that operation is available for use
                            //lstReturn.Add(new CPluginVariable(userPrefix + "User Phone", typeof(String), user.user_phone));
                            buildList.Add(new CPluginVariable(userPrefix + "User Role", roleEnum, user.user_role.role_name));
                            buildList.Add(new CPluginVariable(userPrefix + "Delete User?", typeof(String), ""));
                            buildList.Add(new CPluginVariable(userPrefix + "Add Soldier?", typeof(String), ""));
                            String soldierPrefix = userPrefix + "Soldiers" + s;

                            buildList.AddRange(user.soldierDictionary.Values.Select(aPlayer =>
                                new CPluginVariable(soldierPrefix + aPlayer.player_id + s +
                                    (_gameIDDictionary.ContainsKey(aPlayer.game_id) ? (_gameIDDictionary[aPlayer.game_id].ToString()) : ("INVALID GAME ID [" + aPlayer.game_id + "]")) + s +
                                    aPlayer.player_name + s + "Delete Soldier?", typeof(String), "")));
                        }
                    }
                    else
                    {
                        if (_firstUserListComplete)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("3") + t + "No Users in User List", typeof(String), "Add Users with 'Add User'."));
                        }
                        else
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("3") + t + "Please Wait, Fetching User List.", typeof(String), "Please Wait, Fetching User List."));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building user setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("3") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildSpecialPlayerSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("3-2"))
                {
                    if (_firstUserListComplete)
                    {
                        //Special Player Settings
                        Boolean anyList = false;
                        foreach (ASpecialGroup asGroup in _specialPlayerGroupIDDictionary.Values.OrderBy(aGroup => aGroup.group_name))
                        {
                            List<String> groupList = new List<String>();
                            foreach (ASpecialPlayer asPlayer in GetASPlayersOfGroup(asGroup.group_key).OrderBy(asPlayer => asPlayer.player_object != null ? (asPlayer.player_object.GetVerboseName()) : (asPlayer.player_identifier)))
                            {
                                String playerIdentifier = null;
                                if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_name))
                                {
                                    playerIdentifier = asPlayer.player_object.player_name;
                                }
                                else
                                {
                                    playerIdentifier = asPlayer.player_identifier;
                                }
                                if (String.IsNullOrEmpty(playerIdentifier))
                                {
                                    continue;
                                }
                                TimeSpan duration = (asPlayer.player_expiration - UtcNow()).Duration();
                                if (duration.TotalDays > 3650)
                                {
                                    playerIdentifier += " | Permanent";
                                }
                                else
                                {
                                    playerIdentifier += " | " + FormatTimeString(duration, 3);
                                }
                                if (!groupList.Contains(playerIdentifier))
                                {
                                    groupList.Add(playerIdentifier);
                                }
                            }
                            if (groupList.Any())
                            {
                                anyList = true;
                                buildList.Add(new CPluginVariable(GetSettingSection("3-2") + t + "[" + groupList.Count + "] " + asGroup.group_name + " (Display)", typeof(String[]), groupList.ToArray()));
                            }
                        }
                        if (!anyList)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("3-2") + t + "All Groups Empty", typeof(String), "All Groups Empty"));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building special player setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("3-2") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildVerboseSpecialPlayerSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("3-3"))
                {
                    if (_firstUserListComplete)
                    {
                        //Verbose Special Player Settings
                        Boolean anyVerbostList = false;
                        foreach (ASpecialGroup asGroup in _specialPlayerGroupIDDictionary.Values.OrderBy(aGroup => aGroup.group_name))
                        {
                            List<String> groupList = new List<String>();
                            foreach (ASpecialPlayer asPlayer in GetVerboseASPlayersOfGroup(asGroup.group_key).OrderBy(asPlayer => asPlayer.player_object != null ? (asPlayer.player_object.GetVerboseName()) : (asPlayer.player_identifier)))
                            {
                                String playerIdentifier = null;
                                if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_name))
                                {
                                    playerIdentifier = asPlayer.player_object.player_name;
                                }
                                else
                                {
                                    playerIdentifier = asPlayer.player_identifier;
                                }
                                if (String.IsNullOrEmpty(playerIdentifier))
                                {
                                    continue;
                                }
                                if (!groupList.Contains(playerIdentifier))
                                {
                                    groupList.Add(playerIdentifier);
                                }
                            }
                            if (groupList.Any())
                            {
                                anyVerbostList = true;
                                buildList.Add(new CPluginVariable(GetSettingSection("3-3") + t + "[" + groupList.Count + "] Verbose " + asGroup.group_name + " (Display)", typeof(String[]), groupList.ToArray()));
                            }
                        }
                        if (!anyVerbostList)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("3-3") + t + "All Verbose Groups Empty", typeof(String), "All Verbose Groups Empty"));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building verbose special player setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("3-3") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildRoleSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("4"))
                {
                    //Role Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("4") + t + "Add Role", typeof(String), ""));
                    var useCache = NowDuration(_RoleCommandCacheUpdate).TotalMinutes < 5 &&
                                   _RoleCommandCache != null &&
                                   NowDuration(_RoleCommandCacheUpdateBufferStart) > _RoleCommandCacheUpdateBufferDuration;
                    if (_RoleIDDictionary.Count > 0)
                    {
                        if (!useCache)
                        {
                            // We are not using the cache; clear the entries to we can rebuild it
                            _RoleCommandCache.Clear();
                            _RoleCommandCacheUpdate = UtcNow();
                        }
                        lock (_RoleIDDictionary)
                        {
                            foreach (ARole aRole in _RoleKeyDictionary.Values.ToList())
                            {
                                lock (_CommandIDDictionary)
                                {
                                    Random random = new Random();
                                    String rolePrefix = GetSettingSection("4") + t + "RLE" + aRole.role_id + s + ((RoleIsAdmin(aRole)) ? ("[A]") : ("")) + aRole.role_name + s;
                                    foreach (var aCommand in _CommandNameDictionary.Values.Where(dCommand => dCommand.command_active == ACommand.CommandActive.Active &&
                                                                                                             // Never allow the confirm/cancel commands to be edited, players need these to be universal across servers
                                                                                                             dCommand.command_key != "command_confirm" &&
                                                                                                             dCommand.command_key != "command_cancel" &&
                                                                                                             // Never allow the default guest role to have admin commands assigned to it
                                                                                                             (aRole.role_key != "guest_default" || !dCommand.command_playerInteraction)))
                                    {
                                        var allowed = aRole.RoleAllowedCommands.ContainsKey(aCommand.command_key);
                                        var key = aRole.role_id + "-" + aCommand.command_id;
                                        String display;
                                        if (useCache && _RoleCommandCache.ContainsKey(key))
                                        {
                                            // Using the role command cache; fetch from the dictionary
                                            display = _RoleCommandCache[key];
                                        }
                                        else
                                        {
                                            display = rolePrefix + "CDE" + aCommand.command_id + s + aCommand.command_name + ((aCommand.command_playerInteraction) ? (" [ADMIN]") : ("")) + ((aCommand.command_playerInteraction && allowed) ? (" <---") : (""));
                                            // We've just generated a new display string, add it to the cache
                                            _RoleCommandCache[key] = display;
                                        }
                                        buildList.Add(new CPluginVariable(display, "enum.roleAllowCommandEnum(Allow|Deny)", allowed ? ("Allow") : ("Deny")));
                                    }
                                    //Do not display the delete option for default guest
                                    if (aRole.role_key != "guest_default")
                                    {
                                        buildList.Add(new CPluginVariable(rolePrefix + "Delete Role? (All assignments will be removed)", typeof(String), ""));
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("4") + t + "Role List Empty", typeof(String), "No valid roles found in database."));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building role setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("4") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildRoleGroupSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("4-2"))
                {
                    //Role Group Settings
                    if (_RoleIDDictionary.Count > 0)
                    {
                        lock (_RoleIDDictionary)
                        {
                            foreach (ARole aRole in _RoleKeyDictionary.Values.ToList())
                            {
                                lock (_specialPlayerGroupKeyDictionary)
                                {
                                    Random random = new Random();
                                    String rolePrefix = GetSettingSection("4-2") + t + "RLE" + aRole.role_id + s + ((RoleIsAdmin(aRole)) ? ("[A]") : ("")) + aRole.role_name + s;
                                    // Hedius: the spam bot part is not clean here.... the spamBotExcludeAdmins setting is in A12-2 -> probably should be in the list section
                                    buildList.AddRange(from aGroup in _specialPlayerGroupKeyDictionary.Values
                                                       let allowed = aRole.RoleSetGroups.ContainsKey(aGroup.group_key)
                                                       let required =
                                                        (aGroup.group_key == "slot_reserved" && _FeedServerReservedSlots && _FeedServerReservedSlots_Admins && RoleIsAdmin(aRole)) ||
                                                        (aGroup.group_key == "slot_spectator" && _FeedServerSpectatorList && _FeedServerSpectatorList_Admins && RoleIsAdmin(aRole)) ||
                                                        (aGroup.group_key == "whitelist_multibalancer" && _FeedMultiBalancerWhitelist && _FeedMultiBalancerWhitelist_Admins && RoleIsAdmin(aRole)) ||
                                                        (aGroup.group_key == "whitelist_teamkill" && _FeedTeamKillTrackerWhitelist && _FeedTeamKillTrackerWhitelist_Admins && RoleIsAdmin(aRole)) ||
                                                        (aGroup.group_key == "whitelist_spambot" && _spamBotExcludeWhitelist && _spamBotExcludeAdmins && RoleIsAdmin(aRole))
                                                       let blocked = (aGroup.group_key == "whitelist_adminassistant" && RoleIsAdmin(aRole))
                                                       let enumString = blocked ? "enum.roleSetGroupEnum_blocked(Blocked Based On Other Settings)" : (required ? "enum.roleSetGroupEnum_required(Required Based On Other Settings)" : "enum.roleSetGroupEnum(Assign|Ignore)")
                                                       let display = rolePrefix + "GPE" + aGroup.group_id + s + aGroup.group_name
                                                       select new CPluginVariable(display, enumString, blocked ? "Blocked Based On Other Settings" : (required ? "Required Based On Other Settings" : (allowed ? ("Assign") : ("Ignore")))));
                                }
                            }
                        }
                    }
                    else
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("4-2") + t + "Role List Empty", typeof(String), "No valid roles found in database."));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building role group setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("4-2") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildCommandSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("5"))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Minimum Required Reason Length", typeof(int), _RequiredReasonLength));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Minimum Report Handle Seconds", typeof(int), _MinimumReportHandleSeconds));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Minimum Minutes Into Round To Use Assist", typeof(int), _minimumAssistMinutes));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Maximum Temp-Ban Duration Minutes", typeof(Double), _MaxTempBanDuration.TotalMinutes));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Countdown Duration before a Nuke is fired", typeof(int), _NukeCountdownDurationSeconds));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Allow Commands from Admin Say", typeof(Boolean), _AllowAdminSayCommands));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Bypass all command confirmation -DO NOT USE-", typeof(Boolean), _bypassCommandConfirmation));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "External plugin player commands", typeof(String[]), _ExternalPlayerCommands.ToArray()));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "External plugin admin commands", typeof(String[]), _ExternalAdminCommands.ToArray()));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Command Target Whitelist Commands", typeof(String[]), _CommandTargetWhitelistCommands.ToArray()));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Reserved slot grants access to squad lead command", typeof(Boolean), _ReservedSquadLead));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Reserved slot grants access to self-move command", typeof(Boolean), _ReservedSelfMove));
                    buildList.Add(new CPluginVariable(GetSettingSection("5") + t + "Reserved slot grants access to self-kill command", typeof(Boolean), _ReservedSelfKill));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building command setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("5") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildCommandListSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("6"))
                {
                    //Command Settings
                    if (_CommandNameDictionary.Count > 0)
                    {
                        lock (_CommandIDDictionary)
                        {
                            foreach (ACommand command in _CommandIDDictionary.Values.ToList())
                            {
                                if (command.command_active != ACommand.CommandActive.Invisible)
                                {
                                    String commandPrefix = GetSettingSection("6") + t + "CDE" + command.command_id + s + command.command_name + s;
                                    buildList.Add(new CPluginVariable(commandPrefix + "Active", "enum.commandActiveEnum(Active|Disabled)", command.command_active.ToString()));
                                    if (command.command_active != ACommand.CommandActive.Disabled)
                                    {
                                        if (command.command_logging != ACommand.CommandLogging.Mandatory && command.command_logging != ACommand.CommandLogging.Unable)
                                        {
                                            buildList.Add(new CPluginVariable(commandPrefix + "Logging", "enum.commandLoggingEnum(Log|Ignore)", command.command_logging.ToString()));
                                        }
                                        buildList.Add(new CPluginVariable(commandPrefix + "Text", typeof(String), command.command_text));
                                        buildList.Add(new CPluginVariable(commandPrefix + "Access Method", CreateEnumString(typeof(ACommand.CommandAccess)), command.command_access.ToString()));
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("6") + t + "Command List Empty", typeof(String), "No valid commands found in database."));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building command list setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("6") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildPunishmentSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("7"))
                {
                    //Punishment Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "Punishment Hierarchy", typeof(String[]), _PunishmentHierarchy));
                    buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "Combine Server Punishments", typeof(Boolean), _CombineServerPunishments));
                    buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "Automatic Forgives", typeof(Boolean), _AutomaticForgives));
                    if (_AutomaticForgives)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "Automatic Forgive Days Since Punished", typeof(Int32), _AutomaticForgiveLastPunishDays));
                        buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "Automatic Forgive Days Since Forgiven", typeof(Int32), _AutomaticForgiveLastForgiveDays));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "Only Kill Players when Server in low population", typeof(Boolean), _OnlyKillOnLowPop));
                    buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "Use IRO Punishment", typeof(Boolean), _IROActive));
                    if (_IROActive)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "IRO Timeout Minutes", typeof(Int32), _IROTimeout));
                        buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "IRO Punishment Overrides Low Pop", typeof(Boolean), _IROOverridesLowPop));
                        if (_IROOverridesLowPop)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("7") + t + "IRO Punishment Infractions Required to Override", typeof(Int32), _IROOverridesLowPopInfractions));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building punishment setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("7") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildEmailSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("8"))
                {
                    //Email Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "Send Emails", typeof(Boolean), _UseEmail));
                    if (_UseEmail)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "Use SSL?", typeof(Boolean), _EmailHandler.UseSSL));
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "SMTP-Server address", typeof(String), _EmailHandler.SMTPServer));
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "SMTP-Server port", typeof(int), _EmailHandler.SMTPPort));
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "Sender address", typeof(String), _EmailHandler.SenderEmail));
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "SMTP-Server username", typeof(String), _EmailHandler.SMTPUser));
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "SMTP-Server password", typeof(String), _EmailHandler.SMTPPassword));
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "Custom HTML Addition", typeof(String), _EmailHandler.CustomHTMLAddition));
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "Extra Recipient Email Addresses", typeof(String[]), _EmailHandler.RecipientEmails.ToArray()));
                        buildList.Add(new CPluginVariable(GetSettingSection("8") + t + "Only Send Report Emails When Admins Offline", typeof(Boolean), _EmailReportsOnlyWhenAdminless));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building email setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("8") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildPushbulletSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("8-2"))
                {
                    //PushBullet Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("8-2") + t + "Send PushBullet Reports", typeof(Boolean), _UsePushBullet));
                    if (_UsePushBullet)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("8-2") + t + "PushBullet Access Token", typeof(String), _PushBulletHandler.AccessToken));
                        buildList.Add(new CPluginVariable(GetSettingSection("8-2") + t + "PushBullet Note Target", "enum.pushBulletTargetEnum(Private|Channel)", _PushBulletHandler.DefaultTarget.ToString()));
                        if (_PushBulletHandler.DefaultTarget == PushBulletHandler.Target.Channel)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("8-2") + t + "PushBullet Channel Tag", typeof(String), _PushBulletHandler.DefaultChannelTag));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection("8-2") + t + "Only Send PushBullet Reports When Admins Offline", typeof(Boolean), _PushBulletReportsOnlyWhenAdminless));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building pushbullet setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("8-2") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildDiscordWebHookSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("8-3"))
                {
                    //Discord Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("8-3") + t + "Send Reports to Discord WebHook", typeof(Boolean), _UseDiscordForReports));
                    if (_UseDiscordForReports)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("8-3") + t + "Discord WebHook URL", typeof(String), _DiscordManager.ReportWebhookUrl));
                        buildList.Add(new CPluginVariable(GetSettingSection("8-3") + t + "Only Send Discord Reports When Admins Offline", typeof(Boolean), _DiscordReportsOnlyWhenAdminless));
                        buildList.Add(new CPluginVariable(GetSettingSection("8-3") + t + "Send update if reported players leave without action", typeof(Boolean), _DiscordReportsLeftWithoutAction));
                        buildList.Add(new CPluginVariable(GetSettingSection("8-3") + t + "Discord Role IDs to Mention in Reports", typeof(String[]), _DiscordManager.RoleIDsToMentionReport.ToArray()));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building Discord WebHook setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("8-3") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildTeamswapSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("9"))
                {
                    //TeamSwap Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("9") + t + "Ticket Window High", typeof(int), _TeamSwapTicketWindowHigh));
                    buildList.Add(new CPluginVariable(GetSettingSection("9") + t + "Ticket Window Low", typeof(int), _TeamSwapTicketWindowLow));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building teamswap setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("9") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildAASettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A10"))
                {
                    //Admin Assistant Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("A10") + t + "Enable Admin Assistants", typeof(Boolean), _EnableAdminAssistants));
                    if (_EnableAdminAssistants)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A10") + t + "Minimum Confirmed Reports Per Month", typeof(int), _MinimumRequiredMonthlyReports));
                        buildList.Add(new CPluginVariable(GetSettingSection("A10") + t + "Enable Admin Assistant Perk", typeof(Boolean), _EnableAdminAssistantPerk));
                        buildList.Add(new CPluginVariable(GetSettingSection("A10") + t + "Use AA Report Auto Handler", typeof(Boolean), _UseAAReportAutoHandler));
                        if (_UseAAReportAutoHandler)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("A10") + t + "Auto-Report-Handler Strings", typeof(String[]), _AutoReportHandleStrings));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building AA setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A10") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildMuteSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A11"))
                {
                    //Muting Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "On-Player-Muted Message", typeof(String), _MutedPlayerMuteMessage));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "On-Player-Killed Message", typeof(String), _MutedPlayerKillMessage));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "On-Player-Kicked Message", typeof(String), _MutedPlayerKickMessage));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "Persistent On-Player-Killed Message", typeof(String), _PersistentMutedPlayerKillMessage));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "Persistent On-Player-Kicked Message", typeof(String), _PersistentMutedPlayerKickMessage));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "On-Player-Unmuted Message", typeof(String), _UnMutePlayerMessage));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "# Chances to give player before kicking", typeof(int), _MutedPlayerChances));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "# Chances to give persistent muted player before kicking", typeof(int), _PersistentMutedPlayerChances));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "Ignore commands for mute enforcement", typeof(Boolean), _MutedPlayerIgnoreCommands));
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "Send first spawn warning for persistent muted players", typeof(Boolean), _UseFirstSpawnMutedMessage));
                    if (_UseFirstSpawnMutedMessage)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "First spawn persistent muted warning text", typeof(String), _FirstSpawnMutedMessage));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A11") + t + "Persistent force mute temp-ban duration minutes", typeof(int), _ForceMuteBanDuration));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building mute setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A11") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildMessagingSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A12"))
                {
                    //Message Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Display Admin Name in Action Announcement", typeof(Boolean), _ShowAdminNameInAnnouncement));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Display New Player Announcement", typeof(Boolean), _ShowNewPlayerAnnouncement));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Display Player Name Change Announcement", typeof(Boolean), _ShowPlayerNameChangeAnnouncement));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Display Targeted Player Left Notification", typeof(Boolean), _ShowTargetedPlayerLeftNotification));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Display Ticket Rates in Procon Chat", typeof(Boolean), _DisplayTicketRatesInProconChat));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Inform players of reports against them", typeof(Boolean), _InformReportedPlayers));
                    if (_InformReportedPlayers)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Player Inform Exclusion Strings", typeof(String[]), _PlayerInformExclusionStrings));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Inform reputable players of admin joins", typeof(Boolean), _InformReputablePlayersOfAdminJoins));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Inform admins of admin joins", typeof(Boolean), _InformAdminsOfAdminJoins));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Yell display time seconds", typeof(Int32), _YellDuration));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Pre-Message List", typeof(String[]), _PreMessageList.ToArray()));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Require Use of Pre-Messages", typeof(Boolean), _RequirePreMessageUse));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Use first spawn message", typeof(Boolean), _UseFirstSpawnMessage));
                    if (_UseFirstSpawnMessage)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "First spawn message text", typeof(String), _FirstSpawnMessage));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Use First Spawn Reputation and Infraction Message", typeof(Boolean), _useFirstSpawnRepMessage));

                        buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Enable Alternative Spawn Message for Low Server Playtime", typeof(Boolean), _EnableLowPlaytimeSpawnMessage));
                        if (_EnableLowPlaytimeSpawnMessage)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Low Server Playtime Spawn Message Threshold Hours", typeof(Int32), _LowPlaytimeSpawnMessageHours));
                            buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Low Server Playtime Spawn Message Text", typeof(String), _LowPlaytimeSpawnMessage));
                        }
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Use Perk Expiration Notification", typeof(Boolean), _UsePerkExpirationNotify));
                    if (_UsePerkExpirationNotify)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A12") + t + "Perk Expiration Notify Days Remaining", typeof(Int32), _PerkExpirationNotifyDays));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building messaging setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A12") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildSpambotSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A12-2"))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "SpamBot Enable", typeof(Boolean), _spamBotEnabled));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "SpamBot Say List", typeof(String[]), _spamBotSayList.ToArray()));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "SpamBot Say Delay Seconds", typeof(Int32), _spamBotSayDelaySeconds));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "SpamBot Yell List", typeof(String[]), _spamBotYellList.ToArray()));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "SpamBot Yell Delay Seconds", typeof(Int32), _spamBotYellDelaySeconds));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "SpamBot Tell List", typeof(String[]), _spamBotTellList.ToArray()));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "SpamBot Tell Delay Seconds", typeof(Int32), _spamBotTellDelaySeconds));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "Exclude Whitelist from Spam", typeof(Boolean), _spamBotExcludeWhitelist));
                    if (_spamBotExcludeWhitelist)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "Exclude Admins from Spam", typeof(Boolean), _spamBotExcludeAdmins));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "Exclude Teamspeak and Discord Players from Spam", typeof(Boolean), _spamBotExcludeTeamspeakDiscord));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "Exclude High Reputation Players from Spam", typeof(Boolean), _spamBotExcludeHighReputation));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "Minimum Server Playtime in Hours for Receiving Spam", typeof(Int32), _spamBotMinPlaytimeHours));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building spambot setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A12-2") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildBattlecrySettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A12-3"))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-3") + t + "Player Battlecry Volume", "enum.battlecryVolumeEnum(Disabled|Say|Yell|Tell)", _battlecryVolume.ToString()));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-3") + t + "Player Battlecry Max Length", typeof(Int32), _battlecryMaxLength));
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-3") + t + "Player Battlecry Denied Words", typeof(String[]), _battlecryDeniedWords));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building battlecry setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A12-3") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildAllCapsSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A12-4"))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("A12-4") + t + "Use All Caps Limiter", typeof(Boolean), _UseAllCapsLimiter));
                    if (_UseAllCapsLimiter)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-4") + t + "All Caps Limiter Only Limit Specified Players", typeof(Boolean), _AllCapsLimiterSpecifiedPlayersOnly));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-4") + t + "All Caps Limiter Character Percentage", typeof(Int32), _AllCapsLimterPercentage));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-4") + t + "All Caps Limiter Minimum Characters", typeof(Int32), _AllCapsLimterMinimumCharacters));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-4") + t + "All Caps Limiter Warn Threshold", typeof(Int32), _AllCapsLimiterWarnThreshold));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-4") + t + "All Caps Limiter Kill Threshold", typeof(Int32), _AllCapsLimiterKillThreshold));
                        buildList.Add(new CPluginVariable(GetSettingSection("A12-4") + t + "All Caps Limiter Kick Threshold", typeof(Int32), _AllCapsLimiterKickThreshold));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building all-caps setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A12-4") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildBanSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A13"))
                {
                    //Ban Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("A13") + t + "Use Additional Ban Message", typeof(Boolean), _UseBanAppend));
                    if (_UseBanAppend)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A13") + t + "Additional Ban Message", typeof(String), _BanAppend));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A13") + t + "Procon Ban Admin Name", typeof(String), _CBanAdminName));
                }

                if (IsActiveSettingSection("A13-2"))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("A13-2") + t + "Use Ban Enforcer", typeof(Boolean), _UseBanEnforcer));
                    if (_UseBanEnforcer)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A13-2") + t + "Ban Enforcer BF4 Lenient Kick", typeof(Boolean), _BanEnforcerBF4LenientKick));
                        buildList.Add(new CPluginVariable(GetSettingSection("A13-2") + t + "Enforce New Bans by NAME", typeof(Boolean), _DefaultEnforceName));
                        buildList.Add(new CPluginVariable(GetSettingSection("A13-2") + t + "Enforce New Bans by GUID", typeof(Boolean), _DefaultEnforceGUID));
                        buildList.Add(new CPluginVariable(GetSettingSection("A13-2") + t + "Enforce New Bans by IP", typeof(Boolean), _DefaultEnforceIP));
                    }
                }

                if (IsActiveSettingSection("A13-3"))
                {
                    if (_UseBanEnforcer)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A13-3") + t + "NAME Ban Count", typeof(int), _NameBanCount));
                        buildList.Add(new CPluginVariable(GetSettingSection("A13-3") + t + "GUID Ban Count", typeof(int), _GUIDBanCount));
                        buildList.Add(new CPluginVariable(GetSettingSection("A13-3") + t + "IP Ban Count", typeof(int), _IPBanCount));
                        buildList.Add(new CPluginVariable(GetSettingSection("A13-3") + t + "Ban Search", typeof(String), ""));
                        buildList.AddRange(_BanEnforcerSearchResults.Select(aBan => new CPluginVariable(GetSettingSection("A13-3") + t + "BAN" + aBan.ban_id + s + aBan.ban_record.target_player.player_name + s + aBan.ban_record.source_name + s + aBan.ban_record.record_message, "enum.commandActiveEnum(Active|Disabled|Expired)", aBan.ban_status)));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building ban setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A13") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildExternalCommandSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A14"))
                {
                    //External Command Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("A14") + t + "AdkatsLRT Extension Token", typeof(String), _AdKatsLRTExtensionToken));
                    if (!_UseBanEnforcer)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A14") + t + "Fetch Actions from Database", typeof(Boolean), _fetchActionsFromDb));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building external command setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A14") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildVOIPSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A15"))
                {
                    //VOIP
                    buildList.Add(new CPluginVariable(GetSettingSection("A15") + t + "Server VOIP Address", typeof(String), _ServerVoipAddress));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building VOIP setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A15") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildOrchestrationSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A16"))
                {
                    //MULTIBalancer
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Feed MULTIBalancer Whitelist", typeof(Boolean), _FeedMultiBalancerWhitelist));
                    if (_FeedMultiBalancerWhitelist)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Automatic MULTIBalancer Whitelist for Admins", typeof(Boolean), _FeedMultiBalancerWhitelist_Admins));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Feed MULTIBalancer Even Dispersion List", typeof(Boolean), _FeedMultiBalancerDisperseList));
                    // BF4DB
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Feed BF4DB Whitelist", typeof(Boolean), _FeedBF4DBWhitelist));
                    // BF4DB
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Feed BattlefieldAgency Whitelist", typeof(Boolean), _FeedBAWhitelist));
                    //TeamKillTracker
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Feed TeamKillTracker Whitelist", typeof(Boolean), _FeedTeamKillTrackerWhitelist));
                    if (_FeedTeamKillTrackerWhitelist)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Automatic TeamKillTracker Whitelist for Admins", typeof(Boolean), _FeedTeamKillTrackerWhitelist_Admins));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Feed Server Reserved Slots", typeof(Boolean), _FeedServerReservedSlots));
                    if (_FeedServerReservedSlots)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Automatic Reserved Slot for Admins", typeof(Boolean), _FeedServerReservedSlots_Admins));
                        buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Automatic VIP Kick Whitelist for Admins", typeof(Boolean), _FeedServerReservedSlots_Admins_VIPKickWhitelist));
                    }
                    else
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Send new reserved slots to VIP Slot Manager", typeof(Boolean), _FeedServerReservedSlots_VSM));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Feed Server Spectator List", typeof(Boolean), _FeedServerSpectatorList));
                    if (_FeedServerSpectatorList)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Automatic Spectator Slot for Admins", typeof(Boolean), _FeedServerSpectatorList_Admins));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Feed Stat Logger Settings", typeof(Boolean), _FeedStatLoggerSettings));
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Post Stat Logger Chat Manually", typeof(Boolean), _PostStatLoggerChatManually));
                    if (_PostStatLoggerChatManually)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Post Server Chat Spam", typeof(Boolean), _PostStatLoggerChatManually_PostServerChatSpam));
                        buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Exclude Commands from Chat Logs", typeof(Boolean), _PostStatLoggerChatManually_IgnoreCommands));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Banned Tags", typeof(String[]), _BannedTags));
                    buildList.Add(new CPluginVariable(GetSettingSection("A16") + t + "Auto-Kick Players Who First Joined After This Date", typeof(String), _AutoKickNewPlayerDate.ToShortDateString()));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building orchestration setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A16") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildRoundSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A17"))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("A17") + t + "Round Timer: Enable", typeof(Boolean), _useRoundTimer));
                    if (_useRoundTimer)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A17") + t + "Round Timer: Round Duration Minutes", typeof(Double), _maxRoundTimeMinutes));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building round setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A17") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildFactionSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A17-2") && GameVersion == GameVersionEnum.BF4)
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("A17-2") + t + "Faction Randomizer: Enable", typeof(Boolean), _factionRandomizerEnable));
                    buildList.Add(new CPluginVariable(GetSettingSection("A17-2") + t + "Faction Randomizer: Restriction", "enum.factionRandomizerRestriction2Enum(NoRestriction|NeverSameFaction|AlwaysSameFaction|AlwaysSwapUSvsRU|AlwaysSwapUSvsCN|AlwaysSwapRUvsCN|AlwaysBothUS|AlwaysBothRU|AlwaysBothCN|AlwaysUSvsX|AlwaysRUvsX|AlwaysCNvsX|NeverUSvsX|NeverRUvsX|NeverCNvsX)", _factionRandomizerRestriction.ToString()));
                    buildList.Add(new CPluginVariable(GetSettingSection("A17-2") + t + "Faction Randomizer: Allow Repeat Team Selections", typeof(Boolean), _factionRandomizerAllowRepeatSelection));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building faction setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A17-2") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildAntiCheatSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A18"))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "Use LIVE Anti Cheat System", typeof(Boolean), _useAntiCheatLIVESystem));
                    if (_useAntiCheatLIVESystem)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "LIVE System Includes Mass Murder and Aimbot Checks", typeof(Boolean), _AntiCheatLIVESystemActiveStats));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "DPS Checker: Ban Message", typeof(String), _AntiCheatDPSBanMessage));
                    buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "HSK Checker: Enable", typeof(Boolean), _UseHskChecker));
                    if (_UseHskChecker)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "HSK Checker: Trigger Level", typeof(Double), _HskTriggerLevel));
                        buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "HSK Checker: Ban Message", typeof(String), _AntiCheatHSKBanMessage));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "KPM Checker: Enable", typeof(Boolean), _UseKpmChecker));
                    if (_UseKpmChecker)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "KPM Checker: Trigger Level", typeof(Double), _KpmTriggerLevel));
                        buildList.Add(new CPluginVariable(GetSettingSection("A18") + t + "KPM Checker: Ban Message", typeof(String), _AntiCheatKPMBanMessage));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building anticheat setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A18") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildRuleSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("A19"))
                {
                    //Server rules settings
                    buildList.Add(new CPluginVariable(GetSettingSection("A19") + t + "Rule Print Delay", typeof(Double), _ServerRulesDelay));
                    buildList.Add(new CPluginVariable(GetSettingSection("A19") + t + "Rule Print Interval", typeof(Double), _ServerRulesInterval));
                    buildList.Add(new CPluginVariable(GetSettingSection("A19") + t + "Server Rule List", typeof(String[]), _ServerRulesList));
                    buildList.Add(new CPluginVariable(GetSettingSection("A19") + t + "Server Rule Numbers", typeof(Boolean), _ServerRulesNumbers));
                    buildList.Add(new CPluginVariable(GetSettingSection("A19") + t + "Yell Server Rules", typeof(Boolean), _ServerRulesYell));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building rule setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("A19") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildAFKSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("B20"))
                {
                    //AFK manager settings
                    buildList.Add(new CPluginVariable(GetSettingSection("B20") + t + "AFK System Enable", typeof(Boolean), _AFKManagerEnable));
                    if (_AFKManagerEnable)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("B20") + t + "AFK Ignore Chat", typeof(Boolean), _AFKIgnoreChat));
                        buildList.Add(new CPluginVariable(GetSettingSection("B20") + t + "AFK Auto-Kick Enable", typeof(Boolean), _AFKAutoKickEnable));
                        buildList.Add(new CPluginVariable(GetSettingSection("B20") + t + "AFK Trigger Minutes", typeof(Double), _AFKTriggerDurationMinutes));
                        buildList.Add(new CPluginVariable(GetSettingSection("B20") + t + "AFK Minimum Players", typeof(Int32), _AFKTriggerMinimumPlayers));
                        buildList.Add(new CPluginVariable(GetSettingSection("B20") + t + "AFK Ignore User List", typeof(Boolean), _AFKIgnoreUserList));
                        if (!_AFKIgnoreUserList)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("B20") + t + "AFK Ignore Roles", typeof(String[]), _AFKIgnoreRoles));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building AFK setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("B20") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildPingSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("B21"))
                {
                    //Ping enforcer settings
                    buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Enforcer Enable", typeof(Boolean), _pingEnforcerEnable));
                    if (_pingEnforcerEnable)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Current Ping Limit (Display)", typeof(String), GetPingLimitStatus()));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Moving Average Duration sec", typeof(Double), _pingMovingAverageDurationSeconds));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Low Population Trigger ms", typeof(Double), _pingEnforcerLowTriggerMS));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Low Population Time Modifier", typeof(String[]), _pingEnforcerLowTimeModifier.Select(x => x.ToString()).ToArray()));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Medium Population Trigger ms", typeof(Double), _pingEnforcerMedTriggerMS));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Medium Population Time Modifier", typeof(String[]), _pingEnforcerMedTimeModifier.Select(x => x.ToString()).ToArray()));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick High Population Trigger ms", typeof(Double), _pingEnforcerHighTriggerMS));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick High Population Time Modifier", typeof(String[]), _pingEnforcerHighTimeModifier.Select(x => x.ToString()).ToArray()));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Full Population Trigger ms", typeof(Double), _pingEnforcerFullTriggerMS));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Full Population Time Modifier", typeof(String[]), _pingEnforcerFullTimeModifier.Select(x => x.ToString()).ToArray()));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Minimum Players", typeof(Int32), _pingEnforcerTriggerMinimumPlayers));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Kick Missing Pings", typeof(Boolean), _pingEnforcerKickMissingPings));
                        if (_pingEnforcerKickMissingPings)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Attempt Manual Ping when Missing", typeof(Boolean), _attemptManualPingWhenMissing));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Ignore User List", typeof(Boolean), _pingEnforcerIgnoreUserList));
                        if (!_pingEnforcerIgnoreUserList)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Ignore Roles", typeof(String[]), _pingEnforcerIgnoreRoles));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Ping Kick Message Prefix", typeof(String), _pingEnforcerMessagePrefix));
                        buildList.Add(new CPluginVariable(GetSettingSection("B21") + t + "Display Ping Enforcer Messages In Procon Chat", typeof(Boolean), _pingEnforcerDisplayProconChat));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building ping setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("B21") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildCommanderSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("B22"))
                {
                    //Commander manager settings
                    buildList.Add(new CPluginVariable(GetSettingSection("B22") + t + "Commander Manager Enable", typeof(Boolean), _CMDRManagerEnable));
                    if (_CMDRManagerEnable)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("B22") + t + "Minimum Players to Allow Commanders", typeof(Int32), _CMDRMinimumPlayers));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building commander setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("B22") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildPlayerLockingSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("B23"))
                {
                    //Player locking settings
                    buildList.Add(new CPluginVariable(GetSettingSection("B23") + t + "Player Lock Manual Duration Minutes", typeof(Double), _playerLockingManualDuration));
                    buildList.Add(new CPluginVariable(GetSettingSection("B23") + t + "Automatically Lock Players on Admin Action", typeof(Boolean), _playerLockingAutomaticLock));
                    if (_playerLockingAutomaticLock)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("B23") + t + "Player Lock Automatic Duration Minutes", typeof(Double), _playerLockingAutomaticDuration));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building player locking setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("B23") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildSurrenderSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("B24"))
                {
                    //Surrender Vote settings
                    buildList.Add(new CPluginVariable(GetSettingSection("B24") + t + "Surrender Vote Enable", typeof(Boolean), _surrenderVoteEnable));
                    if (_surrenderVoteEnable)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("B24") + t + "Percentage Votes Needed for Surrender", typeof(Double), _surrenderVoteMinimumPlayerPercentage));
                        buildList.Add(new CPluginVariable(GetSettingSection("B24") + t + "Minimum Player Count to Enable Surrender", typeof(Int32), _surrenderVoteMinimumPlayerCount));
                        buildList.Add(new CPluginVariable(GetSettingSection("B24") + t + "Minimum Ticket Gap to Surrender", typeof(Int32), _surrenderVoteMinimumTicketGap));
                        buildList.Add(new CPluginVariable(GetSettingSection("B24") + t + "Enable Required Ticket Rate Gap to Surrender", typeof(Boolean), _surrenderVoteTicketRateGapEnable));
                        if (_surrenderVoteTicketRateGapEnable)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("B24") + t + "Minimum Ticket Rate Gap to Surrender", typeof(Double), _surrenderVoteMinimumTicketRateGap));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection("B24") + t + "Surrender Vote Timeout Enable", typeof(Boolean), _surrenderVoteTimeoutEnable));
                        if (_surrenderVoteTimeoutEnable)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("B24") + t + "Surrender Vote Timeout Minutes", typeof(Double), _surrenderVoteTimeoutMinutes));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building surrender setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("B24") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildAutoSurrenderSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("B25") || IsActiveSettingSection("B25-2"))
                {
                    //Auto-Surrender Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Enable", typeof(Boolean), _surrenderAutoEnable));
                    if (_surrenderAutoEnable)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Use Optimal Values for Metro Conquest", typeof(Boolean), _surrenderAutoUseMetroValues));
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Use Optimal Values for Locker Conquest", typeof(Boolean), _surrenderAutoUseLockerValues));
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Minimum Ticket Count", typeof(Int32), _surrenderAutoMinimumTicketCount));
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Maximum Ticket Count", typeof(Int32), _surrenderAutoMaximumTicketCount));
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Minimum Ticket Gap", typeof(Int32), _surrenderAutoMinimumTicketGap));
                        if (!_surrenderAutoUseMetroValues && !_surrenderAutoUseLockerValues)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Losing Team Rate Window Max", typeof(Double), _surrenderAutoLosingRateMax));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Losing Team Rate Window Min", typeof(Double), _surrenderAutoLosingRateMin));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Winning Team Rate Window Max", typeof(Double), _surrenderAutoWinningRateMax));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Winning Team Rate Window Min", typeof(Double), _surrenderAutoWinningRateMin));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Trigger Count to Surrender", typeof(Int32), _surrenderAutoTriggerCountToSurrender));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Reset Trigger Count on Cancel", typeof(Boolean), _surrenderAutoResetTriggerCountOnCancel));
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Minimum Players", typeof(Int32), _surrenderAutoMinimumPlayers));
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Nuke Winning Team Instead of Surrendering Losing Team", typeof(Boolean), _surrenderAutoNukeInstead));
                        if (_surrenderAutoNukeInstead)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Maximum Auto-Nukes Each Round", typeof(Int32), _surrenderAutoMaxNukesEachRound));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Reset Auto-Nuke Trigger Count on Fire", typeof(Boolean), _surrenderAutoResetTriggerCountOnFire));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Switch to surrender after max nukes", typeof(Boolean), _surrenderAutoNukeResolveAfterMax));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Minimum Seconds Between Nukes", typeof(Int32), _surrenderAutoNukeMinBetween));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Countdown Duration before a Nuke is fired", typeof(Int32), _NukeCountdownDurationSeconds));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Fire Nuke Triggers if Winning Team up by X Tickets", typeof(Int32), _NukeWinningTeamUpTicketCount));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Only fire ticket difference nukes in high population", typeof(Boolean), _NukeWinningTeamUpTicketHigh));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Announce Nuke Preparation to Players", typeof(Boolean), _surrenderAutoAnnounceNukePrep));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Allow Auto-Nuke to fire on losing teams", typeof(Boolean), _surrenderAutoNukeLosingTeams));
                            if (_surrenderAutoNukeLosingTeams)
                            {
                                buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Maximum Nuke Ticket Difference for Losing Team", typeof(Int32), _surrenderAutoNukeLosingMaxDiff));
                            }
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Auto-Nuke High Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationHigh));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Auto-Nuke Medium Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationMed));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Auto-Nuke Low Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationLow));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Auto-Nuke Consecutive Duration Increase", typeof(Int32), _surrenderAutoNukeDurationIncrease));
                            buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Auto-Nuke Duration Increase Minimum Ticket Difference", typeof(Int32), _surrenderAutoNukeDurationIncreaseTicketDiff));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Start Surrender Vote Instead of Surrendering Losing Team", typeof(Boolean), _surrenderAutoTriggerVote));
                        if (!_surrenderAutoTriggerVote)
                        {
                            if (!_surrenderAutoNukeInstead)
                            {
                                buildList.Add(new CPluginVariable(GetSettingSection("B25") + t + "Auto-Surrender Message", typeof(String), _surrenderAutoMessage));
                            }
                            else
                            {
                                buildList.Add(new CPluginVariable(GetSettingSection("B25-2") + t + "Auto-Nuke Message", typeof(String), _surrenderAutoNukeMessage));
                            }
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building autosurrender setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("B25") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildStatisticsSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection("B26"))
                {
                    //Statistics Settings
                    buildList.Add(new CPluginVariable(GetSettingSection("B26") + t + "Post Map Benefit/Detriment Statistics", typeof(Boolean), _PostMapBenefitStatistics));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building statistics setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("B26") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildPopulatorSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var popMonitorSection = "B27";
            try
            {
                if (IsActiveSettingSection(popMonitorSection))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Monitor Populator Players - Thanks CMWGaming", typeof(Boolean), _PopulatorMonitor));
                    if (_PopulatorMonitor)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "[" + _populatorPlayers.Count() + "] Populator Players (Display)", typeof(String[]), _populatorPlayers.Values.Select(aPlayer => aPlayer.player_name).ToArray()));
                        buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Monitor Specified Populators Only", typeof(Boolean), _PopulatorUseSpecifiedPopulatorsOnly));
                        buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Monitor Populators of This Server Only", typeof(Boolean), _PopulatorPopulatingThisServerOnly));
                        buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Count to Consider Populator Past Week", typeof(Int32), _PopulatorMinimumPopulationCountPastWeek));
                        buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Count to Consider Populator Past 2 Weeks", typeof(Int32), _PopulatorMinimumPopulationCountPast2Weeks));
                        buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Enable Populator Perks", typeof(Boolean), _PopulatorPerksEnable));
                        if (_PopulatorPerksEnable)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Populator Perks - Reserved Slot", typeof(Boolean), _PopulatorPerksReservedSlot));
                            buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Populator Perks - Autobalance Whitelist", typeof(Boolean), _PopulatorPerksBalanceWhitelist));
                            buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Populator Perks - Ping Whitelist", typeof(Boolean), _PopulatorPerksPingWhitelist));
                            buildList.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Populator Perks - TeamKillTracker Whitelist", typeof(Boolean), _PopulatorPerksTeamKillTrackerWhitelist));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building populator setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(popMonitorSection) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildTeamspeakSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var tsMonitorSection = "B28";
            try
            {
                if (IsActiveSettingSection(tsMonitorSection))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Monitor Teamspeak Players - Thanks CMWGaming", typeof(Boolean), _TeamspeakPlayerMonitorView));
                    if (_TeamspeakPlayerMonitorView)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "[" + _TeamspeakPlayers.Count() + "] Teamspeak Players (Display)", typeof(String[]), _TeamspeakPlayers.Values.Select(aPlayer => aPlayer.player_name + " (" + aPlayer.TSClientObject.TsName + ")").ToArray()));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Enable Teamspeak Player Monitor", typeof(Boolean), _TeamspeakPlayerMonitorEnable));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Server IP", typeof(String), _TeamspeakManager.Ts3ServerIp));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Server Port", typeof(Int32), _TeamspeakManager.Ts3ServerPort));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Server Query Port", typeof(Int32), _TeamspeakManager.Ts3QueryPort));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Server Query Username", typeof(String), _TeamspeakManager.Ts3QueryUsername));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Server Query Password", typeof(String), _TeamspeakManager.Ts3QueryPassword));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Server Query Nickname", typeof(String), _TeamspeakManager.Ts3QueryNickname));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Main Channel Name", typeof(String), _TeamspeakManager.Ts3MainChannelName));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Secondary Channel Names", typeof(String[]), _TeamspeakManager.Ts3SubChannelNames));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Debug Display Teamspeak Clients", typeof(Boolean), _TeamspeakManager.DebugClients));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "TeamSpeak Player Join Announcement", "enum.tsAnnounceEnum(Disabled|Say|Yell|Tell)", _TeamspeakManager.JoinDisplay.ToString()));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "TeamSpeak Player Join Message", typeof(String), _TeamspeakManager.JoinDisplayMessage));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "TeamSpeak Player Update Seconds", typeof(Int32), _TeamspeakManager.UpdateIntervalSeconds));
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Enable Teamspeak Player Perks", typeof(Boolean), _TeamspeakPlayerPerksEnable));
                        if (_TeamspeakPlayerPerksEnable)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Player Perks - VIP Kick Whitelist", typeof(Boolean), _TeamspeakPlayerPerksVIPKickWhitelist));
                            buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Player Perks - Autobalance Whitelist", typeof(Boolean), _TeamspeakPlayerPerksBalanceWhitelist));
                            buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Player Perks - Ping Whitelist", typeof(Boolean), _TeamspeakPlayerPerksPingWhitelist));
                            buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Teamspeak Player Perks - TeamKillTracker Whitelist", typeof(Boolean), _TeamspeakPlayerPerksTeamKillTrackerWhitelist));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Announce Online Teamspeak Players - Enable", typeof(Boolean), _TeamspeakOnlinePlayersEnable));
                        if (_TeamspeakOnlinePlayersEnable)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Announce Online Teamspeak Players - Interval Minutes", typeof(Int32), _TeamspeakOnlinePlayersInterval));
                            buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Announce Online Teamspeak Players - Max Players to List", typeof(Int32), _TeamspeakOnlinePlayersMaxPlayersToList));
                            buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Announce Online Teamspeak Players - Single Player Message", typeof(String), _TeamspeakOnlinePlayersAloneMessage));
                            buildList.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Announce Online Teamspeak Players - Multi Player Message", typeof(String), _TeamspeakOnlinePlayersMessage));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building teamspeak setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(tsMonitorSection) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildDiscordSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var discordMonitorSection = "B29";
            try
            {
                if (IsActiveSettingSection(discordMonitorSection))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Monitor Discord Players", typeof(Boolean), _DiscordPlayerMonitorView));
                    if (_DiscordPlayerMonitorView && _DiscordManager != null)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "[" + _DiscordPlayers.Count() + "] Discord Players (Display)", typeof(String[]),
                            _DiscordPlayers.Values.Where(aPlayer => aPlayer != null && aPlayer.DiscordObject != null && aPlayer.DiscordObject.Channel != null)
                                                  .OrderBy(aPlayer => aPlayer.DiscordObject.Channel.Name)
                                                  .Select(aPlayer => aPlayer.player_name + " [" + aPlayer.DiscordObject.Name + "] (" + aPlayer.DiscordObject.Channel.Name + ") " + (String.IsNullOrEmpty(aPlayer.player_discord_id) ? "[Name]" : "[ID]"))
                                                  .ToArray()));
                        var discordMembers = _DiscordManager.GetMembers(false, true, true);
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "[" + discordMembers.Count() + "] Discord Channel Members (Display)", typeof(String[]), discordMembers.OrderBy(aMember => aMember.Channel.Name).Select(aMember => aMember.Name + " (" + aMember.Channel.Name + ")").ToArray()));
                        discordMembers = _DiscordManager.GetMembers(false, false, false);
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "[" + discordMembers.Count() + "] Discord All Members (Display)", typeof(String[]), discordMembers.OrderBy(aMember => (aMember.Channel != null ? aMember.Channel.Name : "_NO VOICE_")).Select(aMember => aMember.Name + " (" + (aMember.Channel != null ? aMember.Channel.Name : "_NO VOICE_") + ")").ToArray()));
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Enable Discord Player Monitor", typeof(Boolean), _DiscordPlayerMonitorEnable));
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord API URL", typeof(String), _DiscordManager.APIUrl));
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord Server ID", typeof(String), _DiscordManager.ServerID));
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord Channel Names", typeof(String[]), _DiscordManager.ChannelNames));
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Require Voice in Discord to Issue Admin Commands", typeof(Boolean), _DiscordPlayerRequireVoiceForAdmin));
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord Player Join Announcement", "enum.tsAnnounceEnum(Disabled|Say|Yell|Tell)", _DiscordManager.JoinDisplay.ToString()));
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord Player Join Message", typeof(String), _DiscordManager.JoinMessage));
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Enable Discord Player Perks", typeof(Boolean), _DiscordPlayerPerksEnable));
                        if (_DiscordPlayerPerksEnable)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord Player Perks - VIP Kick Whitelist", typeof(Boolean), _DiscordPlayerPerksVIPKickWhitelist));
                            buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord Player Perks - Autobalance Whitelist", typeof(Boolean), _DiscordPlayerPerksBalanceWhitelist));
                            buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord Player Perks - Ping Whitelist", typeof(Boolean), _DiscordPlayerPerksPingWhitelist));
                            buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Discord Player Perks - TeamKillTracker Whitelist", typeof(Boolean), _DiscordPlayerPerksTeamKillTrackerWhitelist));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Announce Online Discord Players - Enable", typeof(Boolean), _DiscordOnlinePlayersEnable));
                        if (_DiscordOnlinePlayersEnable)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Announce Online Discord Players - Interval Minutes", typeof(Int32), _DiscordOnlinePlayersInterval));
                            buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Announce Online Discord Players - Max Players to List", typeof(Int32), _DiscordOnlinePlayersMaxPlayersToList));
                            buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Announce Online Discord Players - Single Player Message", typeof(String), _DiscordOnlinePlayersAloneMessage));
                            buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Announce Online Discord Players - Multi Player Message", typeof(String), _DiscordOnlinePlayersMessage));
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Debug Display Discord Members", typeof(Boolean), _DiscordManager.DebugMembers));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building discord setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(discordMonitorSection) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildWatchlistSettings(List<CPluginVariable> lstReturn)
        {
            var section = "B30";
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                if (IsActiveSettingSection(section))
                {
                    //Discord Watchlist Settings
                    buildList.Add(new CPluginVariable(GetSettingSection(section) + t + "Send Watchlist Announcements to Discord WebHook", typeof(Boolean), _UseDiscordForWatchlist));
                    if (_UseDiscordForReports)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(section) + t + "Discord Watchlist WebHook URL", typeof(String), _DiscordManager.WatchlistWebhookUrl));
                        buildList.Add(new CPluginVariable(GetSettingSection(section) + t + "Announce Watchlist Leaves on Discord", typeof(Boolean), _DiscordWatchlistLeftEnabled));
                        buildList.Add(new CPluginVariable(GetSettingSection(section) + t + "Discord Role IDs to Mention in Watchlist Announcements", typeof(String[]), _DiscordManager.RoleIDsToMentionWatchlist.ToArray()));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building Discord Watchlist setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(section) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildTeamPowerSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var teamPowerSection = "C30";
            try
            {
                if (IsActiveSettingSection(teamPowerSection))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Team Power Active Influence", typeof(Double), _TeamPowerActiveInfluence));
                    try
                    {
                        ATeam t1, t2;
                        String teamPower = "Unknown";
                        if (_roundState != RoundState.Loaded && GetTeamByID(1, out t1) && GetTeamByID(2, out t2))
                        {
                            Double t1Power = t1.GetTeamPower();
                            Double t2Power = t2.GetTeamPower();
                            Double percDiff = Math.Abs(t1Power - t2Power) / ((t1Power + t2Power) / 2.0) * 100.0;
                            if (t1Power > t2Power)
                            {
                                teamPower = t1.GetTeamIDKey() + " up " + Math.Round(((t1Power - t2Power) / t2Power) * 100) + "% ";
                            }
                            else
                            {
                                teamPower = t2.GetTeamIDKey() + " up " + Math.Round(((t2Power - t1Power) / t1Power) * 100) + "% ";
                            }
                            teamPower += "(" + t1.TeamKey + ":" + t1.GetTeamPower() + ":" + t1.GetTeamPower(false) + " / " + t2.TeamKey + ":" + t2.GetTeamPower() + ":" + t2.GetTeamPower(false) + ")";
                        }
                        buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Team Power (Display)", typeof(String), teamPower));
                        var onlinePlayers = _PlayerDictionary.Values.ToList()
                            .Where(aPlayer => aPlayer.GetPower(true) > 1);
                        var onlinePlayerListing = onlinePlayers
                            .Select(aPlayer => ((aPlayer.RequiredTeam != null) ? ("(" + ((aPlayer.RequiredTeam.TeamID != aPlayer.fbpInfo.TeamID && _roundState == RoundState.Playing) ? (aPlayer.GetTeamKey() + " -> ") : ("")) + aPlayer.RequiredTeam.TeamKey + "+) ") : ("(" + aPlayer.GetTeamKey() + ") ")) +
                                               "(" + aPlayer.GetPower(true, true, true).ToString("00") +
                                               "|" + aPlayer.GetPower(false, true, true).ToString("00") +
                                               "|" + aPlayer.GetPower(true, true, false).ToString("00") +
                                               "|" + aPlayer.GetPower(true, false, true).ToString("00") +
                                               "|" + aPlayer.GetPower(true, false, false).ToString("00") +
                                               "|" + aPlayer.TopStats.TopCount +
                                               "|" + aPlayer.TopStats.RoundCount +
                                               ") " + aPlayer.GetVerboseName())
                            .OrderByDescending(item => item)
                            .ToArray();
                        buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Player Power (Display)", typeof(String[]), onlinePlayerListing));
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error building team power displays.", e));
                    }
                    //buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Enable Team Power Scrambler", typeof(Boolean), _UseTeamPowerMonitorScrambler));
                    buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Enable Team Power Join Reassignment", typeof(Boolean), _UseTeamPowerMonitorReassign));
                    if (_UseTeamPowerMonitorReassign)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Team Power Join Reassignment Leniency", typeof(Boolean), _UseTeamPowerMonitorReassignLenient));
                        if (_UseTeamPowerMonitorReassignLenient)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Team Power Join Reassignment Leniency Percent", typeof(Double), _TeamPowerMonitorReassignLenientPercent));
                        }
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Enable Team Power Unswitcher", typeof(Boolean), _UseTeamPowerMonitorUnswitcher));
                    buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Enable Team Power Seeder Control", typeof(Boolean), _UseTeamPowerMonitorSeeders));
                    buildList.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Display Team Power In Procon Chat", typeof(Boolean), _UseTeamPowerDisplayBalance));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building team power setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(teamPowerSection) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildWeaponLimiterSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var weaponLimiterSection = "C31";
            try
            {
                if (IsActiveSettingSection(weaponLimiterSection))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(weaponLimiterSection) + t + "Use NO EXPLOSIVES Limiter", typeof(Boolean), _UseWeaponLimiter));
                    if (_UseWeaponLimiter)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(weaponLimiterSection) + t + "NO EXPLOSIVES Weapon String", typeof(String), _WeaponLimiterString));
                        buildList.Add(new CPluginVariable(GetSettingSection(weaponLimiterSection) + t + "NO EXPLOSIVES Exception String", typeof(String), _WeaponLimiterExceptionString));
                        buildList.Add(new CPluginVariable(GetSettingSection(weaponLimiterSection) + t + "Use Grenade Cook Catcher", typeof(Boolean), _UseGrenadeCookCatcher));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building weapon limiter section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(weaponLimiterSection) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildChallengeSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var challengeSettings = "C32";
            try
            {
                if (IsActiveSettingSection(challengeSettings) && ChallengeManager != null)
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(challengeSettings) + t + "Use Challenge System", typeof(Boolean), ChallengeManager.Enabled));
                    buildList.Add(new CPluginVariable(GetSettingSection(challengeSettings) + t + "Challenge System Minimum Players", typeof(Int32), ChallengeManager.MinimumPlayers));
                    buildList.Add(new CPluginVariable(GetSettingSection(challengeSettings) + t + "Use Server-Wide Round Rules", typeof(Boolean), ChallengeManager.EnableServerRoundRules));
                    if (ChallengeManager.EnableServerRoundRules)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(challengeSettings) + t + "Challenge System Auto-Assign Round rules", typeof(Boolean), ChallengeManager.AutoPlay));
                        if (ChallengeManager.AutoPlay)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection(challengeSettings) + t + "Use Different Round Rule For Each Player", typeof(Boolean), ChallengeManager.RandomPlayerRoundRules));
                        }
                    }
                    // DISPLAYS
                    var displaySectionPrefix = GetSettingSection(challengeSettings) + " [1] Displays" + t;
                    var roundRule = ChallengeManager.RoundRule;
                    var roundRuleName = roundRule != null ? roundRule.Name : "No Round Rule";
                    var activeEntries = _PlayerDictionary.Values.ToList().Where(aPlayer => aPlayer.ActiveChallenge != null)
                                                                         .Select(aPlayer => aPlayer.ActiveChallenge)
                                                                         .OrderBy(entry => entry.Rule.Name)
                                                                         .ThenByDescending(entry => entry.Progress.CompletionPercentage)
                                                                         .ToList();
                    var roundRuleEntries = new List<AChallengeManager.CEntry>();
                    if (roundRule != null)
                    {
                        roundRuleEntries.AddRange(activeEntries.Where(entry => entry.Rule == roundRule));
                    }
                    var activeEntriesArray = activeEntries.Select(entry => entry.ToString()).ToArray();
                    buildList.Add(new CPluginVariable(displaySectionPrefix + "[" + activeEntriesArray.Count() + "] Active Entries (Display)", typeof(String[]), activeEntriesArray));
                    buildList.Add(new CPluginVariable(displaySectionPrefix + "Current Server-Wide Round Rule (Display)", typeof(String), roundRuleName));
                    var activeRoundEntriesArray = roundRuleEntries.Select(entry => entry.ToString()).ToArray();
                    buildList.Add(new CPluginVariable(displaySectionPrefix + "[" + activeRoundEntriesArray.Count() + "] Active Round Rule Entries (Display)", typeof(String[]), activeRoundEntriesArray));
                    // ACTIONS
                    var actionSectionPrefix = GetSettingSection(challengeSettings) + " [2] Actions" + t;
                    buildList.Add(new CPluginVariable(actionSectionPrefix + "Run Round Challenge ID", typeof(Int32), 0));
                    // DEFINITIONS
                    var defSectionPrefix = GetSettingSection(challengeSettings) + " [3] Definitions" + t;
                    buildList.Add(new CPluginVariable(defSectionPrefix + "Add Definition?", typeof(String), ""));
                    var definitions = ChallengeManager.GetDefinitions().OrderBy(dDef => dDef.Name);
                    foreach (var def in definitions)
                    {
                        if (def.ID <= 0)
                        {
                            Log.Error("Unable to render challenge definition " + def.ID + ". It had an invalid ID of " + def.ID + ".");
                            continue;
                        }
                        //CDH1 | 5 ARs | Change Name?
                        //CDH1 | 5 ARs | Add Damage Type?
                        //CDH1 | 5 ARs | Add Weapon?
                        //CDH1 | 5 ARs | Delete Definition?
                        //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Damage Type
                        //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Weapon Count
                        //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Kill Count
                        //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Delete Detail?
                        //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Weapon Name
                        //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Kill Count
                        //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Delete Detail?

                        var defPrefix = defSectionPrefix + "CDH" + def.ID + s + def.Name + s;
                        buildList.Add(new CPluginVariable(defPrefix + "Change Name?", typeof(String), def.Name));
                        buildList.Add(new CPluginVariable(defPrefix + "Add Damage Type?", AChallengeManager.CDefinition.DetailDamageEnumString, "None"));
                        buildList.Add(new CPluginVariable(defPrefix + "Add Weapon?", WeaponDictionary.InfantryWeaponNameEnumString, "None"));
                        buildList.Add(new CPluginVariable(defPrefix + "Delete Definition?", typeof(String), ""));
                        foreach (var detail in def.GetDetails())
                        {
                            if (detail.Type == AChallengeManager.CDefinition.CDefinitionDetail.DetailType.None)
                            {
                                Log.Error("Unable to render challenge definition detail " + def.ID + ":" + detail.DetailID + ". It had a type of None.");
                                continue;
                            }
                            var detailPrefix = defPrefix + "CDD" + detail.DetailID + s + detail.ToString() + s;
                            if (detail.Type == AChallengeManager.CDefinition.CDefinitionDetail.DetailType.Damage)
                            {
                                buildList.Add(new CPluginVariable(detailPrefix + "Damage Type", AChallengeManager.CDefinition.DetailDamageEnumString, detail.Damage.ToString()));
                                buildList.Add(new CPluginVariable(detailPrefix + "Weapon Count", typeof(Int32), detail.WeaponCount));
                            }
                            else if (detail.Type == AChallengeManager.CDefinition.CDefinitionDetail.DetailType.Weapon)
                            {
                                buildList.Add(new CPluginVariable(detailPrefix + "Weapon Name", WeaponDictionary.InfantryWeaponNameEnumString, WeaponDictionary.GetDamageTypeByWeaponCode(detail.Weapon) + "\\" + WeaponDictionary.GetShortWeaponNameByCode(detail.Weapon)));
                            }
                            buildList.Add(new CPluginVariable(detailPrefix + "Kill Count", typeof(Int32), detail.KillCount));
                            buildList.Add(new CPluginVariable(detailPrefix + "Delete Detail?", typeof(String), ""));
                        }
                    }
                    // RULES
                    var ruleSectionPrefix = GetSettingSection(challengeSettings) + " [4] Rules" + t;
                    if (!definitions.Any())
                    {
                        // Do not display options to add rules until definitions exist
                        buildList.Add(new CPluginVariable(ruleSectionPrefix + "Add a challenge definition first.", typeof(String), ""));
                    }
                    else
                    {
                        buildList.Add(new CPluginVariable(ruleSectionPrefix + "Add Rule?", ChallengeManager.GetDefinitionEnum(true), "None"));
                        var rules = ChallengeManager.GetRules().OrderBy(dRule => dRule.Definition.Name).ThenBy(dRule => dRule.Name);
                        var defEnum = ChallengeManager.GetDefinitionEnum(false);
                        foreach (var rule in rules)
                        {
                            if (rule.ID <= 0)
                            {
                                Log.Error("Unable to render challenge rule " + rule.Name + ". It had an invalid ID of " + rule.ID + ".");
                                continue;
                            }

                            //CRH1 | 5 ARs 1 Round | Definition
                            //CRH1 | 5 ARs 1 Round | Name
                            //CRH1 | 5 ARs 1 Round | Enabled
                            //CRH1 | 5 ARs 1 Round | Tier
                            //CRH1 | 5 ARs 1 Round | Completion
                            //CRH1 | 5 ARs 1 Round | Round Count
                            //CRH1 | 5 ARs 1 Round | Delete Rule?
                            //CRH2 | 5 ARs 30 Mins | Definition
                            //CRH2 | 5 ARs 30 Mins | Name
                            //CRH2 | 5 ARs 30 Mins | Enabled
                            //CRH2 | 5 ARs 30 Mins | Tier
                            //CRH2 | 5 ARs 30 Mins | Completion
                            //CRH2 | 5 ARs 30 Mins | Duration Minutes
                            //CRH2 | 5 ARs 30 Mins | Delete Rule?

                            var rulePrefix = ruleSectionPrefix + "CRH" + rule.ID + s + rule.Name + s;
                            buildList.Add(new CPluginVariable(rulePrefix + "Definition", defEnum, rule.Definition.Name));
                            buildList.Add(new CPluginVariable(rulePrefix + "Name", typeof(String), rule.Name));
                            buildList.Add(new CPluginVariable(rulePrefix + "Enabled", typeof(Boolean), rule.Enabled));
                            buildList.Add(new CPluginVariable(rulePrefix + "Tier", typeof(Int32), rule.Tier));
                            buildList.Add(new CPluginVariable(rulePrefix + "Completion", AChallengeManager.CRule.CompletionTypeEnumString, rule.Completion.ToString()));
                            if (rule.Completion == AChallengeManager.CRule.CompletionType.None)
                            {
                                buildList.Add(new CPluginVariable(rulePrefix + "^^^SET COMPLETION TYPE^^^", typeof(String), ""));
                            }
                            else if (rule.Completion == AChallengeManager.CRule.CompletionType.Rounds)
                            {
                                buildList.Add(new CPluginVariable(rulePrefix + "Round Count", typeof(Int32), rule.RoundCount));
                            }
                            else if (rule.Completion == AChallengeManager.CRule.CompletionType.Duration)
                            {
                                buildList.Add(new CPluginVariable(rulePrefix + "Duration Minutes", typeof(Int32), rule.DurationMinutes));
                            }
                            else if (rule.Completion == AChallengeManager.CRule.CompletionType.Deaths)
                            {
                                buildList.Add(new CPluginVariable(rulePrefix + "Death Count", typeof(Int32), rule.DeathCount));
                            }
                            buildList.Add(new CPluginVariable(rulePrefix + "Delete Rule?", typeof(String), ""));
                        }
                    }
                    // REWARDS
                    var rewardSectionPrefix = GetSettingSection(challengeSettings) + " [5] Rewards" + t;
                    buildList.Add(new CPluginVariable(rewardSectionPrefix + "Challenge Command Lock Timeout Hours", typeof(Int32), ChallengeManager.CommandLockTimeoutHours));
                    buildList.Add(new CPluginVariable(rewardSectionPrefix + "Add Reward?", typeof(Int32), 0));
                    var rewards = ChallengeManager.GetRewards().OrderBy(dReward => dReward.Tier).ThenBy(dReward => dReward.Reward.ToString());
                    foreach (var reward in rewards)
                    {
                        if (reward.ID <= 0)
                        {
                            Log.Error("Unable to render challenge reward " + reward.ID + ". It had an invalid ID.");
                            continue;
                        }

                        //CCR1 | Tier 1 - ReservedSlot | Tier Level
                        //CCR1 | Tier 1 - ReservedSlot | Reward Type
                        //CCR1 | Tier 1 - ReservedSlot | Enabled
                        //CCR1 | Tier 1 - ReservedSlot | Duration Minutes
                        //CCR1 | Tier 1 - ReservedSlot | Delete Reward?

                        var rewardPrefix = rewardSectionPrefix + "CCR" + reward.ID + s + "Tier " + reward.Tier + " - " + reward.getDescriptionString(null) + s;
                        buildList.Add(new CPluginVariable(rewardPrefix + "Tier Level", typeof(Int32), reward.Tier));
                        buildList.Add(new CPluginVariable(rewardPrefix + "Reward Type", AChallengeManager.CReward.RewardTypeEnumString, reward.Reward.ToString()));
                        buildList.Add(new CPluginVariable(rewardPrefix + "Enabled", typeof(Boolean), reward.Enabled));
                        buildList.Add(new CPluginVariable(rewardPrefix + "Duration Minutes", typeof(Int32), reward.DurationMinutes));
                        buildList.Add(new CPluginVariable(rewardPrefix + "Delete Reward?", typeof(String), ""));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building challenge settings section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(challengeSettings) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildDebugSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            try
            {
                buildList.Add(new CPluginVariable(GetSettingSection("D99") + t + "Debug level", typeof(int), Log.DebugLevel));
                if (IsActiveSettingSection("D99"))
                {
                    //Debug settings
                    buildList.Add(new CPluginVariable(GetSettingSection("D99") + t + "Enforce Single Instance", typeof(Boolean), _enforceSingleInstance));
                    buildList.Add(new CPluginVariable(GetSettingSection("D99") + t + "Disable Automatic Updates", typeof(Boolean), _automaticUpdatesDisabled));
                    buildList.Add(new CPluginVariable(GetSettingSection("D99") + t + "Command Entry", typeof(String), ""));
                    buildList.Add(new CPluginVariable(GetSettingSection("D99") + t + "Client Download URL Entry", typeof(String), ""));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building debug setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection("D99") + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildProxySettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var proxySection = "X98";
            try
            {
                if (IsActiveSettingSection(proxySection))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(proxySection) + t + "Use Proxy for Battlelog", typeof(Boolean), _UseProxy));
                    if (_UseProxy)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(proxySection) + t + "Proxy URL", typeof(String), _ProxyURL));
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building proxy setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(proxySection) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildExperimentalSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var ex = "X99";
            try
            {
                if (IsActiveSettingSection(ex))
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(ex) + t + "Use Experimental Tools", typeof(Boolean), _UseExperimentalTools));
                    if (_UseExperimentalTools)
                    {
                        if (_ShowQuerySettings)
                        {
                            buildList.Add(new CPluginVariable(GetSettingSection(ex) + t + "Send Query", typeof(String), ""));
                            buildList.Add(new CPluginVariable(GetSettingSection(ex) + t + "Send Non-Query", typeof(String), ""));
                        }
                    }
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building experimental setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(ex) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public void BuildEventSettings(List<CPluginVariable> lstReturn)
        {
            List<CPluginVariable> buildList = new List<CPluginVariable>();
            var ex = "X99";
            var ev = "Y99";
            try
            {
                if ((IsActiveSettingSection(ex) || IsActiveSettingSection(ev)) && _UseExperimentalTools)
                {
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + t + "Event Test Round Number", typeof(Int32), _EventTestRoundNumber));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + t + "Automatically Poll Server For Event Options", typeof(Boolean), _EventPollAutomatic));
                    if (_EventPollAutomatic)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + t + "Max Automatic Polls Per Event", typeof(Int32), _EventRoundAutoPollsMax));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + t + "Yell Current Winning Rule Option", typeof(Boolean), _eventPollYellWinningRule));

                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [1] Round Settings" + t + "Event Duration Rounds", typeof(Int32), _EventRoundOptions.Count()));
                    for (int roundNumber = 0; roundNumber < _EventRoundOptions.Count(); roundNumber++)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [1] Round Settings" + t + "Event Round " + (roundNumber + 1) + " Options", _EventRoundOptionsEnum, _EventRoundOptions[roundNumber].getDisplay()));
                    }

                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [2] Schedule Settings" + t + "Weekly Events", typeof(Boolean), _EventWeeklyRepeat));
                    if (_EventWeeklyRepeat)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [2] Schedule Settings" + t + "Event Day", "enum.weekdays(Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday)", _EventWeeklyDay.ToString()));
                    }
                    else
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [2] Schedule Settings" + t + "Event Date", typeof(String), _EventDate.ToShortDateString()));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [2] Schedule Settings" + t + "Event Hour in 24 format", typeof(Double), _EventHour));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [2] Schedule Settings" + t + "Is it daylight savings?", typeof(String), DateTime.Now.IsDaylightSavingTime() ? "Yes, currently daylight savings." : "No, not daylight savings."));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [2] Schedule Settings" + t + "Event Announce Day Difference", typeof(Double), _EventAnnounceDayDifference));

                    if (_EventDate.ToShortDateString() != GetLocalEpochTime().ToShortDateString())
                    {
                        var eventDate = _EventDate.AddHours(_EventHour);
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [3] Schedule Display" + t + "Processed Time Of Event (display)", typeof(String), eventDate.ToShortDateString() + " " + eventDate.ToShortTimeString() + " (" + FormatTimeString(eventDate - DateTime.Now, 3) + ")"));
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [3] Schedule Display" + t + "Current Round Number (display)", typeof(String), String.Format("{0:n0}", _roundID)));
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [3] Schedule Display" + t + "Estimated Event Round Number (display)", typeof(String), String.Format("{0:n0}", FetchEstimatedEventRoundNumber())));
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [3] Schedule Display" + t + "Concrete Event Round Number (display)", typeof(String), _CurrentEventRoundNumber == 999999 ? "Undecided." : String.Format("{0:n0}", _CurrentEventRoundNumber)));
                    }
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [4] Poll Settings" + t + "Poll Max Option Count", typeof(Int32), _EventPollMaxOptions));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [4] Poll Settings" + t + "Poll Mode Rule Combination Count", typeof(Int32), _EventRoundPollOptions.Count()));
                    for (int optionNumber = 0; optionNumber < _EventRoundPollOptions.Count(); optionNumber++)
                    {
                        buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [4] Poll Settings" + t + "Event Poll Option " + (optionNumber + 1), _EventRoundOptionsEnum, _EventRoundPollOptions[optionNumber].getDisplay()));
                    }

                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [5] Name Settings" + t + "Event Base Server Name", typeof(String), _eventBaseServerName));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [5] Name Settings" + t + "Event Countdown Server Name", typeof(String), _eventCountdownServerName));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [5] Name Settings" + t + "Event Concrete Countdown Server Name", typeof(String), _eventConcreteCountdownServerName));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [5] Name Settings" + t + "Event Active Server Name", typeof(String), _eventActiveServerName));

                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [5] Name Display" + t + "Processed Base Server Name (display)", typeof(String), ProcessEventServerName(_eventBaseServerName, false, false)));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [5] Name Display" + t + "Processed Countdown Server Name (display)", typeof(String), ProcessEventServerName(_eventCountdownServerName, false, false)));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [5] Name Display" + t + "Processed Concrete Countdown Server Name (display)", typeof(String), ProcessEventServerName(_eventConcreteCountdownServerName, false, true)));
                    buildList.Add(new CPluginVariable(GetSettingSection(ev) + " [5] Name Display" + t + "Processed Active Server Name (display)", typeof(String), ProcessEventServerName(_eventActiveServerName, true, true)));
                }
                lstReturn.AddRange(buildList);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error building event setting section.", e));
                lstReturn.Add(new CPluginVariable(GetSettingSection(ev) + t + "Failed to build setting section.", typeof(String), ""));
            }
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            try
            {
                Log.Debug(() => "Updating Setting Page [" + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Thread.CurrentThread.Name)) + "]: " + (UtcNow() - _lastSettingPageUpdate).TotalSeconds + " seconds since last update.", 4);
                _lastSettingPageUpdate = UtcNow();
                Stopwatch timer = new Stopwatch();
                timer.Start();
                List<CPluginVariable> lstReturn = new List<CPluginVariable>();

                if (_settingsLocked)
                {
                    lstReturn.Add(new CPluginVariable(GetSettingSection("0") + t + "Unlock Settings", typeof(String), ""));
                }
                else
                {
                    lstReturn.Add(new CPluginVariable(String.IsNullOrEmpty(_settingsPassword) ? (GetSettingSection("0") + t + "Lock Settings - Create Password") : (GetSettingSection("0") + t + "Lock Settings"), typeof(String), ""));
                }

                //Only fetch the following settings when plugin disabled
                if (!_threadsReady)
                {
                    BuildUnreadySettings(lstReturn);
                }
                else
                {
                    if (_settingsLocked)
                    {
                        BuildReadyLockedSettings(lstReturn);
                        timer.Stop();
                        return lstReturn;
                    }

                    lstReturn.Add(new CPluginVariable("* AdKats *|Current Setting Section", _SettingSectionEnum, _CurrentSettingSection));

                    //Auto-Enable Settings
                    lstReturn.Add(new CPluginVariable(GetSettingSection("0") + t + "Auto-Enable/Keep-Alive", typeof(Boolean), _useKeepAlive));

                    BuildServerSettings(lstReturn);

                    BuildSQLSettings(lstReturn);

                    BuildUserSettings(lstReturn);

                    BuildSpecialPlayerSettings(lstReturn);

                    BuildVerboseSpecialPlayerSettings(lstReturn);

                    BuildRoleSettings(lstReturn);

                    BuildRoleGroupSettings(lstReturn);

                    BuildCommandSettings(lstReturn);

                    BuildCommandListSettings(lstReturn);

                    BuildPunishmentSettings(lstReturn);

                    BuildEmailSettings(lstReturn);

                    BuildPushbulletSettings(lstReturn);

                    BuildDiscordWebHookSettings(lstReturn);

                    BuildTeamswapSettings(lstReturn);

                    BuildAASettings(lstReturn);

                    BuildMuteSettings(lstReturn);

                    BuildMessagingSettings(lstReturn);

                    BuildSpambotSettings(lstReturn);

                    BuildBattlecrySettings(lstReturn);

                    BuildAllCapsSettings(lstReturn);

                    BuildBanSettings(lstReturn);

                    BuildExternalCommandSettings(lstReturn);

                    BuildVOIPSettings(lstReturn);

                    BuildOrchestrationSettings(lstReturn);

                    BuildRoundSettings(lstReturn);

                    BuildFactionSettings(lstReturn);

                    BuildAntiCheatSettings(lstReturn);

                    BuildRuleSettings(lstReturn);

                    BuildAFKSettings(lstReturn);

                    BuildPingSettings(lstReturn);

                    BuildCommanderSettings(lstReturn);

                    BuildPlayerLockingSettings(lstReturn);

                    BuildSurrenderSettings(lstReturn);

                    BuildAutoSurrenderSettings(lstReturn);

                    BuildStatisticsSettings(lstReturn);

                    BuildPopulatorSettings(lstReturn);

                    BuildTeamspeakSettings(lstReturn);

                    BuildDiscordSettings(lstReturn);

                    BuildWatchlistSettings(lstReturn);

                    BuildTeamPowerSettings(lstReturn);

                    BuildWeaponLimiterSettings(lstReturn);

                    BuildChallengeSettings(lstReturn);

                    BuildDebugSettings(lstReturn);

                    BuildProxySettings(lstReturn);

                    BuildExperimentalSettings(lstReturn);

                    BuildEventSettings(lstReturn);
                }
                timer.Stop();
                return lstReturn;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching display vars.", e));
                return new List<CPluginVariable>();
            }
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            try
            {
                lstReturn.Add(new CPluginVariable(GetSettingSection("0") + t + "Auto-Enable/Keep-Alive", typeof(Boolean), _useKeepAlive));

                lstReturn.Add(new CPluginVariable(GetSettingSection("1") + t + "Settings Locked", typeof(Boolean), _settingsLocked, true));
                lstReturn.Add(new CPluginVariable(GetSettingSection("1") + t + "Settings Password", typeof(String), String.IsNullOrEmpty(_settingsPassword) ? "" : "********"));

                lstReturn.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Hostname", typeof(String), _mySqlHostname));
                lstReturn.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Port", typeof(String), _mySqlPort));
                lstReturn.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Database", typeof(String), _mySqlSchemaName));
                lstReturn.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Username", typeof(String), _mySqlUsername));
                lstReturn.Add(new CPluginVariable(GetSettingSection("2") + t + "MySQL Password", typeof(String), String.IsNullOrEmpty(_mySqlPassword) ? "" : "********"));

                lstReturn.Add(new CPluginVariable(GetSettingSection("D98") + t + "Override Timing Confirmation", typeof(Boolean), _timingValidOverride));

                lstReturn.Add(new CPluginVariable(GetSettingSection("D99") + t + "Enforce Single Instance", typeof(Boolean), _enforceSingleInstance));
                lstReturn.Add(new CPluginVariable(GetSettingSection("D99") + t + "Debug level", typeof(Int32), Log.DebugLevel));
                lstReturn.Add(new CPluginVariable(GetSettingSection("D99") + t + "Disable Automatic Updates", typeof(Boolean), _automaticUpdatesDisabled));
                lstReturn.Add(new CPluginVariable("startup_durations", typeof(String[]), _startupDurations.Select(duration => ((int)duration.TotalSeconds).ToString()).ToArray()));
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching save vars.", e));
            }

            return lstReturn;
        }

        public void SetPluginVariable(String strVariable, String strValue)
        {
            if (strValue == null)
            {
                return;
            }
            try
            {
                if (strVariable == "UpdateSettings")
                {
                    //Do nothing. Settings page will be updated after return.
                }
                else if (strVariable == "startup_durations")
                {
                    var stringDurations = CPluginVariable.DecodeStringArray(strValue);
                    _startupDurations.Clear();
                    foreach (String stringDuration in stringDurations)
                    {
                        _startupDurations.Enqueue(TimeSpan.FromSeconds(Int32.Parse(stringDuration)));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Enable/Keep-Alive").Success)
                {
                    Boolean autoEnable = Boolean.Parse(strValue);
                    if (autoEnable != _useKeepAlive)
                    {
                        if (autoEnable)
                        {
                            Enable();
                        }
                        _useKeepAlive = autoEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Enable/Keep-Alive", typeof(Boolean), _useKeepAlive));
                    }
                }
                else if (Regex.Match(strVariable, @"Current Setting Section").Success)
                {
                    _CurrentSettingSection = strValue;
                }
                else if (Regex.Match(strVariable, @"Override Timing Confirmation").Success)
                {
                    Boolean dbTimingValidOverride = Boolean.Parse(strValue);
                    if (dbTimingValidOverride != _timingValidOverride)
                    {
                        _timingValidOverride = dbTimingValidOverride;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Override Timing Confirmation", typeof(Boolean), _timingValidOverride));
                    }
                }
                else if (Regex.Match(strVariable, @"Unlock Settings").Success)
                {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 5)
                    {
                        return;
                    }
                    if (strValue != _settingsPassword)
                    {
                        Log.Error("Password incorrect.");
                        return;
                    }
                    _settingsLocked = false;
                    Log.Success("Settings unlocked.");
                    QueueSettingForUpload(new CPluginVariable(@"Settings Locked", typeof(Boolean), _settingsLocked));
                }
                else if (Regex.Match(strVariable, @"Lock Settings - Create Password").Success)
                {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 5)
                    {
                        Log.Error("Password had invalid format/length, unable to submit.");
                        return;
                    }
                    _settingsPassword = strValue;
                    _settingsLocked = true;
                    Log.Success("Password created. Settings Locked.");
                    QueueSettingForUpload(new CPluginVariable(@"Settings Password", typeof(String), _settingsPassword));
                    QueueSettingForUpload(new CPluginVariable(@"Settings Locked", typeof(Boolean), _settingsLocked));
                }
                else if (Regex.Match(strVariable, @"Lock Settings").Success)
                {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 5)
                    {
                        return;
                    }
                    if (strValue != _settingsPassword)
                    {
                        Log.Error("Password incorrect.");
                        return;
                    }
                    _settingsLocked = true;
                    Log.Success("Settings locked.");
                    QueueSettingForUpload(new CPluginVariable(@"Settings Locked", typeof(Boolean), _settingsLocked));
                }
                else if (Regex.Match(strVariable, @"Settings Password").Success)
                {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 5 || strValue == "********")
                    {
                        return;
                    }
                    _settingsPassword = strValue;
                }
                else if (Regex.Match(strVariable, @"Settings Locked").Success)
                {
                    _settingsLocked = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Send Query").Success)
                {
                    if (_databaseConnectionCriticalState)
                    {
                        return;
                    }
                    SendQuery(strValue, true);
                    _ShowQuerySettings = false;
                }
                else if (Regex.Match(strVariable, @"Send Non-Query").Success)
                {
                    if (_databaseConnectionCriticalState)
                    {
                        return;
                    }
                    SendNonQuery("Experimental Query", strValue, true);
                    _ShowQuerySettings = false;
                }
                else if (Regex.Match(strVariable, @"Setting Import").Success)
                {
                    Int32 tmp = -1;
                    if (int.TryParse(strValue, out tmp))
                    {
                        if (tmp != -1)
                        {
                            QueueSettingImport(tmp);
                        }
                    }
                    else
                    {
                        Log.Error("Invalid Input for Setting Import");
                    }
                }
                else if (Regex.Match(strVariable, @"Command Entry").Success)
                {
                    if (String.IsNullOrEmpty(strValue))
                    {
                        return;
                    }
                    //Check if the message is a command
                    if (strValue.StartsWith("@") || strValue.StartsWith("!") || strValue.StartsWith("."))
                    {
                        strValue = strValue.Substring(1);
                    }
                    else if (strValue.StartsWith("/@") || strValue.StartsWith("/!") || strValue.StartsWith("/."))
                    {
                        strValue = strValue.Substring(2);
                    }
                    else if (strValue.StartsWith("/"))
                    {
                        strValue = strValue.Substring(1);
                    }
                    else
                    {
                        Log.Error("Invalid command format.");
                        return;
                    }
                    ARecord record = new ARecord
                    {
                        record_source = ARecord.Sources.Settings,
                        record_access = ARecord.AccessMethod.HiddenExternal,
                        source_name = "SettingsAdmin",
                        record_time = UtcNow()
                    };
                    CompleteRecordInformation(record, new AChatMessage()
                    {
                        Message = strValue
                    });
                }
                else if (Regex.Match(strVariable, @"Client Download URL Entry").Success)
                {
                    if (String.IsNullOrEmpty(strValue))
                    {
                        return;
                    }
                    try
                    {
                        String response = Util.HttpDownload(strValue);
                        if (String.IsNullOrEmpty(response))
                        {
                            Log.Warn("Request response was empty.");
                        }
                        else
                        {
                            Log.Success("Request response received [Length " + response.Length + "], displaying.");
                            try
                            {
                                // Remove surrogate codepoint values as raw text
                                var tester = new Regex(@"[\\][u][d][8-9a-f][0-9a-f][0-9a-f]", RegexOptions.IgnoreCase);
                                if (tester.IsMatch(response))
                                {
                                    Log.Warn("Found invalid codepoint raw text values.");
                                }
                                else
                                {
                                    Log.Success("No invalid codepoint raw text values.");
                                }
                                response = tester.Replace(response, String.Empty);
                                Log.Success("[Length " + response.Length + "].");
                                var responseJSON = (Hashtable)JSON.JsonDecode(response);
                                Log.Warn("Parsed as JSON.");
                            }
                            catch (Exception e)
                            {
                                Log.Warn("Unable to parse as JSON. " + e.Message);
                            }
                            response = response.Length <= 500 ? response : response.Substring(0, 500);
                            Log.Info(response);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error downloading/displaying response from " + strValue, e));
                    }
                }
                else if (Regex.Match(strVariable, @"Debug level").Success)
                {
                    Int32 tmp;
                    if (int.TryParse(strValue, out tmp))
                    {
                        if (tmp == -10)
                        {
                            Log.Info("8345: Clear all fetched players and left players.");
                            Log.Info("3958: Print average database read/write durations.");
                            Log.Info("5682: Toggle discord debug.");
                            Log.Info("2563: Toggle player fetch debug.");
                            Log.Info("7621: Toggle player listing debug.");
                        }
                        else if (tmp == 8345)
                        {
                            _FetchedPlayers.Clear();
                            _PlayerLeftDictionary.Clear();
                        }
                        else if (tmp == 3958)
                        {
                            Log.Info("Avg Read: " + _DatabaseReadAverageDuration + " | Avg Write: " + _DatabaseWriteAverageDuration);
                        }
                        else if (tmp == 2232)
                        {
                            Environment.Exit(2232);
                        }
                        else if (tmp == 3840)
                        {
                            _DebugKills = !_DebugKills;
                        }
                        else if (tmp == 8142)
                        {
                            _ShowQuerySettings = true;
                        }
                        else if (tmp == 5682)
                        {
                            _DiscordManager.DebugService = !_DiscordManager.DebugService;
                            Log.Info("Discord Debug Display: " + _DiscordManager.DebugService);
                        }
                        else if (tmp == 2563)
                        {
                            _debugDisplayPlayerFetches = !_debugDisplayPlayerFetches;
                            Log.Info("Player Fetch Debug Display: " + _debugDisplayPlayerFetches);
                        }
                        else if (tmp == 7621)
                        {
                            _DebugPlayerListing = !_DebugPlayerListing;
                            Log.Info("Player Listing Debug Display: " + _DebugPlayerListing);
                        }
                        else if (tmp != Log.DebugLevel)
                        {
                            if (tmp < 0)
                            {
                                tmp = 0;
                            }
                            Log.DebugLevel = tmp;
                            //Once setting has been changed, upload the change to database
                            QueueSettingForUpload(new CPluginVariable(@"Debug level", typeof(int), Log.DebugLevel));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Maximum Temp-Ban Duration Minutes").Success)
                {
                    Double maxDuration;
                    if (!Double.TryParse(strValue, out maxDuration))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (maxDuration <= 0)
                    {
                        Log.Error("Max duration cannot be negative.");
                        return;
                    }
                    TimeSpan tempMaxDur = TimeSpan.FromMinutes(maxDuration);
                    if (tempMaxDur.TotalDays > 3650)
                    {
                        Log.Error("Max duration cannot be longer than 10 years.");
                        return;
                    }
                    _MaxTempBanDuration = tempMaxDur;
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Maximum Temp-Ban Duration Minutes", typeof(Double), _MaxTempBanDuration.TotalMinutes));
                }
                else if (Regex.Match(strVariable, @"Server VOIP Address").Success)
                {
                    if (strValue != _ServerVoipAddress)
                    {
                        _ServerVoipAddress = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Server VOIP Address", typeof(String), _ServerVoipAddress));
                    }
                }
                else if (Regex.Match(strVariable, @"Rule Print Delay").Success)
                {
                    Double delay;
                    if (!Double.TryParse(strValue, out delay))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_ServerRulesDelay != delay)
                    {
                        if (delay < 0)
                        {
                            Log.Error("Delay cannot be negative.");
                            delay = 0.1;
                        }
                        _ServerRulesDelay = delay;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Rule Print Delay", typeof(Double), _ServerRulesDelay));
                    }
                }
                else if (Regex.Match(strVariable, @"Rule Print Interval").Success)
                {
                    Double interval;
                    if (!Double.TryParse(strValue, out interval))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_ServerRulesInterval != interval)
                    {
                        if (interval <= 0)
                        {
                            Log.Error("Interval cannot be negative.");
                            interval = 5.0;
                        }
                        _ServerRulesInterval = interval;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Rule Print Interval", typeof(Double), _ServerRulesInterval));
                    }
                }
                else if (Regex.Match(strVariable, @"Server Rule List").Success)
                {
                    _ServerRulesList = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Server Rule List", typeof(String), CPluginVariable.EncodeStringArray(_ServerRulesList)));
                }
                else if (Regex.Match(strVariable, @"Server Rule Numbers").Success)
                {
                    Boolean ruleNumbers = Boolean.Parse(strValue);
                    if (ruleNumbers != _ServerRulesNumbers)
                    {
                        _ServerRulesNumbers = ruleNumbers;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Server Rule Numbers", typeof(Boolean), _ServerRulesNumbers));
                    }
                }
                else if (Regex.Match(strVariable, @"Yell Server Rules").Success)
                {
                    Boolean ruleYell = Boolean.Parse(strValue);
                    if (ruleYell != _ServerRulesYell)
                    {
                        _ServerRulesYell = ruleYell;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Yell Server Rules", typeof(Boolean), _ServerRulesYell));
                    }
                }
                else if (Regex.Match(strVariable, @"Disable Automatic Updates").Success)
                {
                    Boolean disableAutomaticUpdates = Boolean.Parse(strValue);
                    if (disableAutomaticUpdates != _automaticUpdatesDisabled)
                    {
                        _automaticUpdatesDisabled = disableAutomaticUpdates;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Disable Automatic Updates", typeof(Boolean), _automaticUpdatesDisabled));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce Single Instance").Success)
                {
                    Boolean enforceSingleInstance = Boolean.Parse(strValue);
                    if (enforceSingleInstance != _enforceSingleInstance)
                    {
                        _enforceSingleInstance = enforceSingleInstance;
                        if (!_enforceSingleInstance && _threadsReady)
                        {
                            var message = "Running multiple instances of AdKats on the same server is a very bad idea. If you are sure this won't happen, it's safe to disable this setting.";
                            Log.Warn(message);
                            Log.Warn(message);
                            Log.Warn(message);
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enforce Single Instance", typeof(Boolean), _enforceSingleInstance));
                    }
                }
                else if (Regex.Match(strVariable, @"AFK System Enable").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean afkSystemEnable = Boolean.Parse(strValue);
                    if (afkSystemEnable != _AFKManagerEnable)
                    {
                        _AFKManagerEnable = afkSystemEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"AFK System Enable", typeof(Boolean), _AFKManagerEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"AFK Ignore Chat").Success)
                {
                    Boolean afkIgnoreChat = Boolean.Parse(strValue);
                    if (afkIgnoreChat != _AFKIgnoreChat)
                    {
                        _AFKIgnoreChat = afkIgnoreChat;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"AFK Ignore Chat", typeof(Boolean), _AFKIgnoreChat));
                    }
                }
                else if (Regex.Match(strVariable, @"AFK Auto-Kick Enable").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean afkAutoKickEnable = Boolean.Parse(strValue);
                    if (afkAutoKickEnable != _AFKAutoKickEnable)
                    {
                        _AFKAutoKickEnable = afkAutoKickEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"AFK Auto-Kick Enable", typeof(Boolean), _AFKAutoKickEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"AFK Trigger Minutes").Success)
                {
                    Double afkAutoKickDurationMinutes;
                    if (!Double.TryParse(strValue, out afkAutoKickDurationMinutes))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_AFKTriggerDurationMinutes != afkAutoKickDurationMinutes)
                    {
                        if (afkAutoKickDurationMinutes < 0)
                        {
                            Log.Error("Duration cannot be negative.");
                            return;
                        }
                        _AFKTriggerDurationMinutes = afkAutoKickDurationMinutes;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"AFK Trigger Minutes", typeof(Double), _AFKTriggerDurationMinutes));
                    }
                }
                else if (Regex.Match(strVariable, @"AFK Minimum Players").Success)
                {
                    Int32 afkAutoKickMinimumPlayers = Int32.Parse(strValue);
                    if (_AFKTriggerMinimumPlayers != afkAutoKickMinimumPlayers)
                    {
                        if (afkAutoKickMinimumPlayers < 0)
                        {
                            Log.Error("Minimum players cannot be negative.");
                            return;
                        }
                        _AFKTriggerMinimumPlayers = afkAutoKickMinimumPlayers;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"AFK Minimum Players", typeof(Int32), _AFKTriggerMinimumPlayers));
                    }
                }
                else if (Regex.Match(strVariable, @"AFK Ignore User List").Success)
                {
                    Boolean afkIgnoreUserList = Boolean.Parse(strValue);
                    if (afkIgnoreUserList != _AFKIgnoreUserList)
                    {
                        _AFKIgnoreUserList = afkIgnoreUserList;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"AFK Ignore User List", typeof(Boolean), _AFKIgnoreUserList));
                    }
                }
                else if (Regex.Match(strVariable, @"AFK Ignore Roles").Success)
                {
                    _AFKIgnoreRoles = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"AFK Ignore Roles", typeof(String), CPluginVariable.EncodeStringArray(_AFKIgnoreRoles)));
                }
                else if (Regex.Match(strVariable, @"Ping Enforcer Enable").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean PingSystemEnable = Boolean.Parse(strValue);
                    if (PingSystemEnable != _pingEnforcerEnable)
                    {
                        _pingEnforcerEnable = PingSystemEnable;
                        //Once setting has been changed, upload the change to database
                        if (_pingEnforcerEnable)
                        {
                            //Disable latency manager
                            ExecuteCommand("procon.protected.plugins.enable", "CLatencyManager", "False");
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Ping Enforcer Enable", typeof(Boolean), _pingEnforcerEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Ping Moving Average Duration sec").Success)
                {
                    Double pingMovingAverageDurationSeconds;
                    if (!Double.TryParse(strValue, out pingMovingAverageDurationSeconds))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_pingMovingAverageDurationSeconds != pingMovingAverageDurationSeconds)
                    {
                        if (pingMovingAverageDurationSeconds < 30)
                        {
                            Log.Error("Duration cannot be less than 30 seconds.");
                            return;
                        }
                        _pingMovingAverageDurationSeconds = pingMovingAverageDurationSeconds;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ping Moving Average Duration sec", typeof(Double), _pingMovingAverageDurationSeconds));
                    }
                }
                else if (Regex.Match(strVariable, @"Ping Kick Low Population Trigger ms").Success)
                {
                    Double pingEnforcerLowTriggerMS;
                    if (!Double.TryParse(strValue, out pingEnforcerLowTriggerMS))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_pingEnforcerLowTriggerMS != pingEnforcerLowTriggerMS)
                    {
                        if (pingEnforcerLowTriggerMS < 10)
                        {
                            Log.Error("Trigger ms cannot be less than 10.");
                            return;
                        }
                        _pingEnforcerLowTriggerMS = pingEnforcerLowTriggerMS;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ping Kick Low Population Trigger ms", typeof(Double), _pingEnforcerLowTriggerMS));
                    }
                }
                else if (Regex.Match(strVariable, @"Ping Kick Low Population Time Modifier").Success)
                {
                    Int32 parser;
                    var timeModifiers = CPluginVariable.DecodeStringArray(strValue)
                        .Select((modifier, index) => ((Int32.TryParse(modifier.Trim(), out parser)) ? (parser) : (0)))
                        .Take(24).ToList();
                    while (timeModifiers.Count() < 24)
                    {
                        Log.Error("Not all hours accounted for, adding 0 for low hour " + (timeModifiers.Count() - 1));
                        timeModifiers.Add(0);
                    }
                    _pingEnforcerLowTimeModifier = timeModifiers.ToArray();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Ping Kick Low Population Time Modifier", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerLowTimeModifier.Select(x => x.ToString()).ToArray())));
                }
                else if (Regex.Match(strVariable, @"Ping Kick Medium Population Trigger ms").Success)
                {
                    Double pingEnforcerMedTriggerMS;
                    if (!Double.TryParse(strValue, out pingEnforcerMedTriggerMS))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_pingEnforcerMedTriggerMS != pingEnforcerMedTriggerMS)
                    {
                        if (pingEnforcerMedTriggerMS < 10)
                        {
                            Log.Error("Trigger ms cannot be less than 10.");
                            return;
                        }
                        _pingEnforcerMedTriggerMS = pingEnforcerMedTriggerMS;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ping Kick Medium Population Trigger ms", typeof(Double), _pingEnforcerMedTriggerMS));
                    }
                }
                else if (Regex.Match(strVariable, @"Ping Kick Medium Population Time Modifier").Success)
                {
                    Int32 parser;
                    var timeModifiers = CPluginVariable.DecodeStringArray(strValue)
                        .Select((modifier, index) => ((Int32.TryParse(modifier.Trim(), out parser)) ? (parser) : (0)))
                        .Take(24).ToList();
                    while (timeModifiers.Count() < 24)
                    {
                        Log.Error("Not all hours accounted for, adding 0 for medium hour " + (timeModifiers.Count() - 1));
                        timeModifiers.Add(0);
                    }
                    _pingEnforcerMedTimeModifier = timeModifiers.ToArray();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Ping Kick Medium Population Time Modifier", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerMedTimeModifier.Select(x => x.ToString()).ToArray())));
                }
                else if (Regex.Match(strVariable, @"Ping Kick High Population Trigger ms").Success)
                {
                    Double pingEnforcerHighTriggerMS;
                    if (!Double.TryParse(strValue, out pingEnforcerHighTriggerMS))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_pingEnforcerHighTriggerMS != pingEnforcerHighTriggerMS)
                    {
                        if (pingEnforcerHighTriggerMS < 10)
                        {
                            Log.Error("Trigger ms cannot be less than 10.");
                            return;
                        }
                        _pingEnforcerHighTriggerMS = pingEnforcerHighTriggerMS;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ping Kick High Population Trigger ms", typeof(Double), _pingEnforcerHighTriggerMS));
                    }
                }
                else if (Regex.Match(strVariable, @"Ping Kick High Population Time Modifier").Success)
                {
                    Int32 parser;
                    var timeModifiers = CPluginVariable.DecodeStringArray(strValue)
                        .Select((modifier, index) => ((Int32.TryParse(modifier.Trim(), out parser)) ? (parser) : (0)))
                        .Take(24).ToList();
                    while (timeModifiers.Count() < 24)
                    {
                        Log.Error("Not all hours accounted for, adding 0 for high hour " + (timeModifiers.Count() - 1));
                        timeModifiers.Add(0);
                    }
                    _pingEnforcerHighTimeModifier = timeModifiers.ToArray();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Ping Kick High Population Time Modifier", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerHighTimeModifier.Select(x => x.ToString()).ToArray())));
                }
                else if (Regex.Match(strVariable, @"Ping Kick Full Population Trigger ms").Success)
                {
                    Double pingEnforcerFullTriggerMS;
                    if (!Double.TryParse(strValue, out pingEnforcerFullTriggerMS))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_pingEnforcerFullTriggerMS != pingEnforcerFullTriggerMS)
                    {
                        if (pingEnforcerFullTriggerMS < 10)
                        {
                            Log.Error("Trigger ms cannot be less than 10.");
                            return;
                        }
                        _pingEnforcerFullTriggerMS = pingEnforcerFullTriggerMS;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ping Kick Full Population Trigger ms", typeof(Double), _pingEnforcerFullTriggerMS));
                    }
                }
                else if (Regex.Match(strVariable, @"Ping Kick Full Population Time Modifier").Success)
                {
                    Int32 parser;
                    var timeModifiers = CPluginVariable.DecodeStringArray(strValue)
                        .Select((modifier, index) => ((Int32.TryParse(modifier.Trim(), out parser)) ? (parser) : (0)))
                        .Take(24).ToList();
                    while (timeModifiers.Count() < 24)
                    {
                        Log.Error("Not all hours accounted for, adding 0 for full hour " + (timeModifiers.Count() - 1));
                        timeModifiers.Add(0);
                    }
                    _pingEnforcerFullTimeModifier = timeModifiers.ToArray();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Ping Kick Full Population Time Modifier", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerFullTimeModifier.Select(x => x.ToString()).ToArray())));
                }
                else if (Regex.Match(strVariable, @"Ping Kick Minimum Players").Success)
                {
                    Int32 pingEnforcerTriggerMinimumPlayers = Int32.Parse(strValue);
                    if (_pingEnforcerTriggerMinimumPlayers != pingEnforcerTriggerMinimumPlayers)
                    {
                        if (pingEnforcerTriggerMinimumPlayers < 0)
                        {
                            Log.Error("Minimum players cannot be negative.");
                            return;
                        }
                        _pingEnforcerTriggerMinimumPlayers = pingEnforcerTriggerMinimumPlayers;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ping Kick Minimum Players", typeof(Int32), _pingEnforcerTriggerMinimumPlayers));
                    }
                }
                else if (Regex.Match(strVariable, @"Kick Missing Pings").Success)
                {
                    Boolean pingEnforcerKickMissingPings = Boolean.Parse(strValue);
                    if (pingEnforcerKickMissingPings != _pingEnforcerKickMissingPings)
                    {
                        _pingEnforcerKickMissingPings = pingEnforcerKickMissingPings;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Kick Missing Pings", typeof(Boolean), _pingEnforcerKickMissingPings));
                    }
                }
                else if (Regex.Match(strVariable, @"Attempt Manual Ping when Missing").Success)
                {
                    Boolean attemptManualPingWhenMissing = Boolean.Parse(strValue);
                    if (attemptManualPingWhenMissing != _attemptManualPingWhenMissing)
                    {
                        _attemptManualPingWhenMissing = attemptManualPingWhenMissing;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Attempt Manual Ping when Missing", typeof(Boolean), _attemptManualPingWhenMissing));
                    }
                }
                else if (Regex.Match(strVariable, @"Display Ping Enforcer Messages In Procon Chat").Success)
                {
                    Boolean pingEnforcerDisplayProconChat = Boolean.Parse(strValue);
                    if (pingEnforcerDisplayProconChat != _pingEnforcerDisplayProconChat)
                    {
                        _pingEnforcerDisplayProconChat = pingEnforcerDisplayProconChat;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Display Ping Enforcer Messages In Procon Chat", typeof(Boolean), _pingEnforcerDisplayProconChat));
                    }
                }
                else if (Regex.Match(strVariable, @"Ping Kick Ignore User List").Success)
                {
                    Boolean pingEnforcerIgnoreUserList = Boolean.Parse(strValue);
                    if (pingEnforcerIgnoreUserList != _pingEnforcerIgnoreUserList)
                    {
                        _pingEnforcerIgnoreUserList = pingEnforcerIgnoreUserList;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ping Kick Ignore User List", typeof(Boolean), _pingEnforcerIgnoreUserList));
                    }
                }
                else if (Regex.Match(strVariable, @"Ping Kick Ignore Roles").Success)
                {
                    _pingEnforcerIgnoreRoles = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Ping Kick Ignore Roles", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerIgnoreRoles)));
                }
                else if (Regex.Match(strVariable, @"Ping Kick Message Prefix").Success)
                {
                    if (strValue != _pingEnforcerMessagePrefix)
                    {
                        _pingEnforcerMessagePrefix = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ping Kick Message Prefix", typeof(String), _pingEnforcerMessagePrefix));
                    }
                }
                else if (Regex.Match(strVariable, @"Commander Manager Enable").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean CMDRManagerEnable = Boolean.Parse(strValue);
                    if (CMDRManagerEnable != _CMDRManagerEnable)
                    {
                        if (GameVersion == GameVersionEnum.BF3 && CMDRManagerEnable)
                        {
                            Log.Error("Commander manager cannot be enabled in BF3");
                            _CMDRManagerEnable = false;
                        }
                        else
                        {
                            _CMDRManagerEnable = CMDRManagerEnable;
                            //Once setting has been changed, upload the change to database
                            QueueSettingForUpload(new CPluginVariable(@"Commander Manager Enable", typeof(Boolean), _CMDRManagerEnable));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Players to Allow Commanders").Success)
                {
                    Int32 CMDRMinimumPlayers = Int32.Parse(strValue);
                    if (_CMDRMinimumPlayers != CMDRMinimumPlayers)
                    {
                        if (CMDRMinimumPlayers < 0)
                        {
                            Log.Error("Minimum players cannot be negative.");
                            return;
                        }
                        _CMDRMinimumPlayers = CMDRMinimumPlayers;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Players to Allow Commanders", typeof(Int32), _CMDRMinimumPlayers));
                    }
                }
                else if (Regex.Match(strVariable, @"Surrender Vote Enable").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean surrenderVoteEnable = Boolean.Parse(strValue);
                    if (surrenderVoteEnable != _surrenderVoteEnable)
                    {
                        _surrenderVoteEnable = surrenderVoteEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Surrender Vote Enable", typeof(Boolean), _surrenderVoteEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Percentage Votes Needed for Surrender").Success)
                {
                    Double surrenderVoteMinimumPlayerPercentage;
                    if (!Double.TryParse(strValue, out surrenderVoteMinimumPlayerPercentage))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_surrenderVoteMinimumPlayerPercentage != surrenderVoteMinimumPlayerPercentage)
                    {
                        if (surrenderVoteMinimumPlayerPercentage < 0)
                        {
                            Log.Error("Minimum player percentage cannot be negative.");
                            return;
                        }
                        if (surrenderVoteMinimumPlayerPercentage > 100)
                        {
                            Log.Error("Minimum player percentage cannot be greater than 100.");
                            return;
                        }
                        _surrenderVoteMinimumPlayerPercentage = surrenderVoteMinimumPlayerPercentage;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Percentage Votes Needed for Surrender", typeof(Double), _surrenderVoteMinimumPlayerPercentage));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Player Count to Enable Surrender").Success)
                {
                    Int32 surrenderVoteMinimumPlayerCount = Int32.Parse(strValue);
                    if (_surrenderVoteMinimumPlayerCount != surrenderVoteMinimumPlayerCount)
                    {
                        if (surrenderVoteMinimumPlayerCount < 0)
                        {
                            Log.Error("Minimum player count cannot be negative.");
                            return;
                        }
                        _surrenderVoteMinimumPlayerCount = surrenderVoteMinimumPlayerCount;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Player Count to Enable Surrender", typeof(Int32), _surrenderVoteMinimumPlayerCount));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Ticket Gap to Surrender").Success)
                {
                    Int32 surrenderVoteMinimumTicketGap = Int32.Parse(strValue);
                    if (_surrenderVoteMinimumTicketGap != surrenderVoteMinimumTicketGap)
                    {
                        if (surrenderVoteMinimumTicketGap < 0)
                        {
                            Log.Error("Minimum ticket gap cannot be negative.");
                            return;
                        }
                        _surrenderVoteMinimumTicketGap = surrenderVoteMinimumTicketGap;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Ticket Gap to Surrender", typeof(Int32), _surrenderVoteMinimumTicketGap));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Required Ticket Rate Gap to Surrender").Success)
                {
                    Boolean surrenderVoteTicketRateGapEnable = Boolean.Parse(strValue);
                    if (surrenderVoteTicketRateGapEnable != _surrenderVoteTicketRateGapEnable)
                    {
                        _surrenderVoteTicketRateGapEnable = surrenderVoteTicketRateGapEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Required Ticket Rate Gap to Surrender", typeof(Boolean), _surrenderVoteTicketRateGapEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Ticket Rate Gap to Surrender").Success)
                {
                    Double surrenderVoteMinimumTicketRateGap;
                    if (!Double.TryParse(strValue, out surrenderVoteMinimumTicketRateGap))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_surrenderVoteMinimumTicketRateGap != surrenderVoteMinimumTicketRateGap)
                    {
                        if (surrenderVoteMinimumTicketRateGap < 0)
                        {
                            Log.Error("Minimum ticket rate gap cannot be negative.");
                            return;
                        }
                        _surrenderVoteMinimumTicketRateGap = surrenderVoteMinimumTicketRateGap;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Ticket Rate Gap to Surrender", typeof(Double), _surrenderVoteMinimumTicketRateGap));
                    }
                }
                else if (Regex.Match(strVariable, @"Surrender Vote Timeout Enable").Success)
                {
                    Boolean surrenderVoteTimeoutEnable = Boolean.Parse(strValue);
                    if (surrenderVoteTimeoutEnable != _surrenderVoteTimeoutEnable)
                    {
                        _surrenderVoteTimeoutEnable = surrenderVoteTimeoutEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Surrender Vote Timeout Enable", typeof(Boolean), _surrenderVoteTimeoutEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Surrender Vote Timeout Minutes").Success)
                {
                    Int32 surrenderVoteTimeoutMinutes = Int32.Parse(strValue);
                    if (_surrenderVoteTimeoutMinutes != surrenderVoteTimeoutMinutes)
                    {
                        if (surrenderVoteTimeoutMinutes < 0)
                        {
                            Log.Error("Timeout cannot be negative.");
                            return;
                        }
                        _surrenderVoteTimeoutMinutes = surrenderVoteTimeoutMinutes;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Surrender Vote Timeout Minutes", typeof(Int32), _surrenderVoteTimeoutMinutes));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Enable").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean surrenderAutoEnable = Boolean.Parse(strValue);
                    if (surrenderAutoEnable != _surrenderAutoEnable)
                    {
                        _surrenderAutoEnable = surrenderAutoEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Enable", typeof(Boolean), _surrenderAutoEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Use Optimal Values for Metro").Success)
                {
                    Boolean surrenderAutoUseMetroValues = Boolean.Parse(strValue);
                    if (surrenderAutoUseMetroValues != _surrenderAutoUseMetroValues)
                    {
                        _surrenderAutoUseMetroValues = surrenderAutoUseMetroValues;
                        if (_surrenderAutoUseMetroValues)
                        {
                            _surrenderAutoUseLockerValues = false;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Use Optimal Values for Metro Conquest", typeof(Boolean), _surrenderAutoUseMetroValues));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Use Optimal Values for Locker").Success)
                {
                    Boolean surrenderAutoUseLockerValues = Boolean.Parse(strValue);
                    if (surrenderAutoUseLockerValues != _surrenderAutoUseLockerValues)
                    {
                        _surrenderAutoUseLockerValues = surrenderAutoUseLockerValues;
                        if (_surrenderAutoUseLockerValues)
                        {
                            _surrenderAutoUseMetroValues = false;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Use Optimal Values for Locker Conquest", typeof(Boolean), _surrenderAutoUseLockerValues));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Reset Trigger Count on Cancel").Success)
                {
                    Boolean surrenderAutoResetTriggerCountOnCancel = Boolean.Parse(strValue);
                    if (surrenderAutoResetTriggerCountOnCancel != _surrenderAutoResetTriggerCountOnCancel)
                    {
                        _surrenderAutoResetTriggerCountOnCancel = surrenderAutoResetTriggerCountOnCancel;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Reset Trigger Count on Cancel", typeof(Boolean), _surrenderAutoResetTriggerCountOnCancel));
                    }
                }
                else if (Regex.Match(strVariable, @"Reset Auto-Nuke Trigger Count on Fire").Success)
                {
                    Boolean surrenderAutoResetTriggerCountOnFire = Boolean.Parse(strValue);
                    if (surrenderAutoResetTriggerCountOnFire != _surrenderAutoResetTriggerCountOnFire)
                    {
                        _surrenderAutoResetTriggerCountOnFire = surrenderAutoResetTriggerCountOnFire;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Reset Auto-Nuke Trigger Count on Fire", typeof(Boolean), _surrenderAutoResetTriggerCountOnFire));
                    }
                }
                else if (Regex.Match(strVariable, @"Nuke Winning Team Instead of Surrendering Losing Team").Success)
                {
                    Boolean surrenderAutoNukeWinning = Boolean.Parse(strValue);
                    if (surrenderAutoNukeWinning != _surrenderAutoNukeInstead)
                    {
                        _surrenderAutoNukeInstead = surrenderAutoNukeWinning;
                        if (_surrenderAutoNukeInstead)
                        {
                            _surrenderAutoTriggerVote = false;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Nuke Winning Team Instead of Surrendering Losing Team", typeof(Boolean), _surrenderAutoNukeInstead));
                    }
                }
                else if (Regex.Match(strVariable, @"Fire Nuke Triggers if Winning Team up by X Tickets").Success)
                {
                    Int32 ticketCount = Int32.Parse(strValue);
                    if (_NukeWinningTeamUpTicketCount != ticketCount)
                    {
                        _NukeWinningTeamUpTicketCount = ticketCount;
                        if (_NukeWinningTeamUpTicketCount < 1)
                        {
                            _NukeWinningTeamUpTicketCount = 1;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Fire Nuke Triggers if Winning Team up by X Tickets", typeof(Int32), _NukeWinningTeamUpTicketCount));
                    }
                }
                else if (Regex.Match(strVariable, @"Maximum Auto-Nukes Each Round").Success)
                {
                    Int32 surrenderAutoMaxNukesEachRound = Int32.Parse(strValue);
                    if (_surrenderAutoMaxNukesEachRound != surrenderAutoMaxNukesEachRound)
                    {
                        if (surrenderAutoMaxNukesEachRound < 0)
                        {
                            Log.Error("Maximum nuke count each round cannot be negative.");
                            return;
                        }
                        _surrenderAutoMaxNukesEachRound = surrenderAutoMaxNukesEachRound;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Maximum Auto-Nukes Each Round", typeof(Int32), _surrenderAutoMaxNukesEachRound));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Seconds Between Nukes").Success)
                {
                    Int32 surrenderAutoNukeMinBetween = Int32.Parse(strValue);
                    if (_surrenderAutoNukeMinBetween != surrenderAutoNukeMinBetween)
                    {
                        if (surrenderAutoNukeMinBetween < 0)
                        {
                            Log.Error("Minimum seconds between nukes must be positive.");
                            surrenderAutoNukeMinBetween = 1;
                        }
                        if (surrenderAutoNukeMinBetween > 300)
                        {
                            Log.Error("Minimum seconds between nukes cannot be longer than 300 seconds.");
                            surrenderAutoNukeMinBetween = 300;
                        }
                        _surrenderAutoNukeMinBetween = surrenderAutoNukeMinBetween;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Seconds Between Nukes", typeof(Int32), _surrenderAutoNukeMinBetween));
                    }
                }
                else if (Regex.Match(strVariable, @"Switch to surrender after max nukes").Success)
                {
                    Boolean surrenderAutoNukeResolveAfterMax = Boolean.Parse(strValue);
                    if (surrenderAutoNukeResolveAfterMax != _surrenderAutoNukeResolveAfterMax)
                    {
                        _surrenderAutoNukeResolveAfterMax = surrenderAutoNukeResolveAfterMax;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Switch to surrender after max nukes", typeof(Boolean), _surrenderAutoNukeResolveAfterMax));
                    }
                }
                else if (Regex.Match(strVariable, @"Only fire ticket difference nukes in high population").Success)
                {
                    Boolean NukeWinningTeamUpTicketHigh = Boolean.Parse(strValue);
                    if (NukeWinningTeamUpTicketHigh != _NukeWinningTeamUpTicketHigh)
                    {
                        _NukeWinningTeamUpTicketHigh = NukeWinningTeamUpTicketHigh;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Only fire ticket difference nukes in high population", typeof(Boolean), _NukeWinningTeamUpTicketHigh));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Nuke Preparation to Players").Success)
                {
                    Boolean surrenderAutoAnnounceNukePrep = Boolean.Parse(strValue);
                    if (surrenderAutoAnnounceNukePrep != _surrenderAutoAnnounceNukePrep)
                    {
                        _surrenderAutoAnnounceNukePrep = surrenderAutoAnnounceNukePrep;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Announce Nuke Preparation to Players", typeof(Boolean), _surrenderAutoAnnounceNukePrep));
                    }
                }
                else if (Regex.Match(strVariable, @"Allow Auto-Nuke to fire on losing teams").Success)
                {
                    Boolean surrenderAutoNukeLosingTeams = Boolean.Parse(strValue);
                    if (surrenderAutoNukeLosingTeams != _surrenderAutoNukeLosingTeams)
                    {
                        _surrenderAutoNukeLosingTeams = surrenderAutoNukeLosingTeams;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Allow Auto-Nuke to fire on losing teams", typeof(Boolean), _surrenderAutoNukeLosingTeams));
                    }
                }
                else if (Regex.Match(strVariable, @"Start Surrender Vote Instead of Surrendering Losing Team").Success)
                {
                    Boolean surrenderAutoTriggerVote = Boolean.Parse(strValue);
                    if (surrenderAutoTriggerVote != _surrenderAutoTriggerVote)
                    {
                        _surrenderAutoTriggerVote = surrenderAutoTriggerVote;
                        if (surrenderAutoTriggerVote)
                        {
                            _surrenderAutoNukeInstead = false;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Start Surrender Vote Instead of Surrendering Losing Team", typeof(Boolean), _surrenderAutoTriggerVote));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Minimum Ticket Gap").Success)
                {
                    Int32 surrenderAutoMinimumTicketGap = Int32.Parse(strValue);
                    if (_surrenderAutoMinimumTicketGap != surrenderAutoMinimumTicketGap)
                    {
                        if (_surrenderAutoMinimumTicketGap < 0)
                        {
                            Log.Error("Minimum ticket gap cannot be negative.");
                            return;
                        }
                        _surrenderAutoMinimumTicketGap = surrenderAutoMinimumTicketGap;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Minimum Ticket Gap", typeof(Int32), _surrenderAutoMinimumTicketGap));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Minimum Ticket Count").Success)
                {
                    Int32 surrenderAutoMinimumTicketCount = Int32.Parse(strValue);
                    if (_surrenderAutoMinimumTicketCount != surrenderAutoMinimumTicketCount)
                    {
                        if (surrenderAutoMinimumTicketCount < 0)
                        {
                            Log.Error("Minimum ticket count cannot be negative.");
                            return;
                        }
                        _surrenderAutoMinimumTicketCount = surrenderAutoMinimumTicketCount;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Minimum Ticket Count", typeof(Int32), _surrenderAutoMinimumTicketCount));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Maximum Ticket Count").Success)
                {
                    Int32 surrenderAutoMaximumTicketCount = Int32.Parse(strValue);
                    if (_surrenderAutoMaximumTicketCount != surrenderAutoMaximumTicketCount)
                    {
                        if (surrenderAutoMaximumTicketCount < 0)
                        {
                            Log.Error("Maximum ticket count cannot be negative.");
                            return;
                        }
                        _surrenderAutoMaximumTicketCount = surrenderAutoMaximumTicketCount;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Maximum Ticket Count", typeof(Int32), _surrenderAutoMaximumTicketCount));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Losing Team Rate Window Max").Success)
                {
                    Double surrenderAutoLosingRateMax;
                    if (!Double.TryParse(strValue, out surrenderAutoLosingRateMax))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_surrenderAutoLosingRateMax != surrenderAutoLosingRateMax)
                    {
                        _surrenderAutoLosingRateMax = surrenderAutoLosingRateMax;

                        if (_surrenderAutoLosingRateMin > _surrenderAutoLosingRateMax)
                        {
                            Log.Info("Min ticket rate cannot be greater than max. Swapping values.");
                            var pivot = _surrenderAutoLosingRateMin;
                            _surrenderAutoLosingRateMin = _surrenderAutoLosingRateMax;
                            _surrenderAutoLosingRateMax = pivot;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Losing Team Rate Window Max", typeof(Double), _surrenderAutoLosingRateMax));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Losing Team Rate Window Min").Success)
                {
                    Double surrenderAutoLosingRateMin;
                    if (!Double.TryParse(strValue, out surrenderAutoLosingRateMin))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_surrenderAutoLosingRateMin != surrenderAutoLosingRateMin)
                    {
                        _surrenderAutoLosingRateMin = surrenderAutoLosingRateMin;

                        if (_surrenderAutoLosingRateMin > _surrenderAutoLosingRateMax)
                        {
                            Log.Info("Min ticket rate cannot be greater than max. Swapping values.");
                            var pivot = _surrenderAutoLosingRateMin;
                            _surrenderAutoLosingRateMin = _surrenderAutoLosingRateMax;
                            _surrenderAutoLosingRateMax = pivot;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Losing Team Rate Window Min", typeof(Double), _surrenderAutoLosingRateMin));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Winning Team Rate Window Max").Success)
                {
                    Double surrenderAutoWinningRateMax;
                    if (!Double.TryParse(strValue, out surrenderAutoWinningRateMax))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_surrenderAutoWinningRateMax != surrenderAutoWinningRateMax)
                    {
                        _surrenderAutoWinningRateMax = surrenderAutoWinningRateMax;

                        if (_surrenderAutoWinningRateMin > _surrenderAutoWinningRateMax)
                        {
                            Log.Info("Min ticket rate cannot be greater than max. Swapping values.");
                            var pivot = _surrenderAutoWinningRateMin;
                            _surrenderAutoWinningRateMin = _surrenderAutoWinningRateMax;
                            _surrenderAutoWinningRateMax = pivot;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Winning Team Rate Window Max", typeof(Double), _surrenderAutoWinningRateMax));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Winning Team Rate Window Min").Success)
                {
                    Double surrenderAutoWinningRateMin;
                    if (!Double.TryParse(strValue, out surrenderAutoWinningRateMin))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_surrenderAutoWinningRateMin != surrenderAutoWinningRateMin)
                    {
                        _surrenderAutoWinningRateMin = surrenderAutoWinningRateMin;

                        if (_surrenderAutoWinningRateMin > _surrenderAutoWinningRateMax)
                        {
                            Log.Info("Min ticket rate cannot be greater than max. Swapping values.");
                            var pivot = _surrenderAutoWinningRateMin;
                            _surrenderAutoWinningRateMin = _surrenderAutoWinningRateMax;
                            _surrenderAutoWinningRateMax = pivot;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Winning Team Rate Window Min", typeof(Double), _surrenderAutoWinningRateMin));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Message").Success)
                {
                    if (strValue != _surrenderAutoMessage)
                    {
                        _surrenderAutoMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Message", typeof(String), _surrenderAutoMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Nuke Message").Success)
                {
                    if (strValue != _surrenderAutoNukeMessage)
                    {
                        _surrenderAutoNukeMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Message", typeof(String), _surrenderAutoNukeMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Trigger Count to Surrender").Success)
                {
                    Int32 surrenderAutoTriggerCountToSurrender = Int32.Parse(strValue);
                    if (_surrenderAutoTriggerCountToSurrender != surrenderAutoTriggerCountToSurrender)
                    {
                        _surrenderAutoTriggerCountToSurrender = surrenderAutoTriggerCountToSurrender;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Trigger Count to Surrender", typeof(Int32), _surrenderAutoTriggerCountToSurrender));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Surrender Minimum Players").Success)
                {
                    Int32 surrenderAutoMinimumPlayers = Int32.Parse(strValue);
                    if (_surrenderAutoMinimumPlayers != surrenderAutoMinimumPlayers)
                    {
                        if (surrenderAutoMinimumPlayers < 0)
                        {
                            Log.Error("Minimum player count cannot be negative.");
                            surrenderAutoMinimumPlayers = 0;
                        }
                        _surrenderAutoMinimumPlayers = surrenderAutoMinimumPlayers;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Minimum Players", typeof(Int32), _surrenderAutoMinimumPlayers));
                    }
                }
                else if (Regex.Match(strVariable, @"Maximum Nuke Ticket Difference for Losing Team").Success)
                {
                    Int32 losingTeamTicketDiff = Int32.Parse(strValue);
                    if (_surrenderAutoNukeLosingMaxDiff != losingTeamTicketDiff)
                    {
                        if (losingTeamTicketDiff < 0)
                        {
                            Log.Error("Max ticket difference must be non-negative.");
                            losingTeamTicketDiff = 0;
                        }
                        _surrenderAutoNukeLosingMaxDiff = losingTeamTicketDiff;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Maximum Nuke Ticket Difference for Losing Team", typeof(Int32), _surrenderAutoNukeLosingMaxDiff));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Nuke High Pop Duration Seconds").Success)
                {
                    Int32 surrenderAutoNukeDuration = Int32.Parse(strValue);
                    if (_surrenderAutoNukeDurationHigh != surrenderAutoNukeDuration)
                    {
                        if (surrenderAutoNukeDuration < 0)
                        {
                            Log.Error("Auto-nuke high population duration must be non-negative.");
                            surrenderAutoNukeDuration = 0;
                        }
                        if (surrenderAutoNukeDuration > 60)
                        {
                            Log.Error("Auto-nuke high population duration cannot be longer than 60 seconds.");
                            surrenderAutoNukeDuration = 60;
                        }
                        _surrenderAutoNukeDurationHigh = surrenderAutoNukeDuration;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke High Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationHigh));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Nuke Medium Pop Duration Seconds").Success)
                {
                    Int32 surrenderAutoNukeDuration = Int32.Parse(strValue);
                    if (_surrenderAutoNukeDurationMed != surrenderAutoNukeDuration)
                    {
                        if (surrenderAutoNukeDuration < 0)
                        {
                            Log.Error("Auto-nuke medium population duration must be non-negative.");
                            surrenderAutoNukeDuration = 0;
                        }
                        if (surrenderAutoNukeDuration > 45)
                        {
                            Log.Error("Auto-nuke medium population duration cannot be longer than 45 seconds.");
                            surrenderAutoNukeDuration = 45;
                        }
                        _surrenderAutoNukeDurationMed = surrenderAutoNukeDuration;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Medium Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationMed));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Nuke Low Pop Duration Seconds").Success)
                {
                    Int32 surrenderAutoNukeDuration = Int32.Parse(strValue);
                    if (_surrenderAutoNukeDurationLow != surrenderAutoNukeDuration)
                    {
                        if (surrenderAutoNukeDuration < 0)
                        {
                            Log.Error("Auto-nuke low population duration must be non-negative.");
                            surrenderAutoNukeDuration = 0;
                        }
                        if (surrenderAutoNukeDuration > 30)
                        {
                            Log.Error("Auto-nuke low population duration cannot be longer than 30 seconds.");
                            surrenderAutoNukeDuration = 30;
                        }
                        _surrenderAutoNukeDurationLow = surrenderAutoNukeDuration;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Low Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationLow));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Nuke Consecutive Duration Increase").Success)
                {
                    Int32 surrenderAutoNukeDurationIncrease = Int32.Parse(strValue);
                    if (_surrenderAutoNukeDurationIncrease != surrenderAutoNukeDurationIncrease)
                    {
                        if (surrenderAutoNukeDurationIncrease < 0)
                        {
                            Log.Error("Auto-nuke consecutive duration increase must be non-negative.");
                            surrenderAutoNukeDurationIncrease = 0;
                        }
                        if (surrenderAutoNukeDurationIncrease > 30)
                        {
                            Log.Error("Auto-nuke consecutive duration cannot be longer than 30 seconds.");
                            surrenderAutoNukeDurationIncrease = 30;
                        }
                        _surrenderAutoNukeDurationIncrease = surrenderAutoNukeDurationIncrease;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Consecutive Duration Increase", typeof(Int32), _surrenderAutoNukeDurationIncrease));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Nuke Duration Increase Minimum Ticket Difference").Success)
                {
                    Int32 surrenderAutoNukeDurationIncreaseTicketDiff = Int32.Parse(strValue);
                    if (_surrenderAutoNukeDurationIncreaseTicketDiff != surrenderAutoNukeDurationIncreaseTicketDiff)
                    {
                        if (surrenderAutoNukeDurationIncreaseTicketDiff < 0)
                        {
                            Log.Error("Auto-Nuke Duration Increase Minimum Ticket Difference must be non-negative.");
                            surrenderAutoNukeDurationIncreaseTicketDiff = 0;
                        }
                        _surrenderAutoNukeDurationIncreaseTicketDiff = surrenderAutoNukeDurationIncreaseTicketDiff;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Duration Increase Minimum Ticket Difference", typeof(Int32), _surrenderAutoNukeDurationIncreaseTicketDiff));
                    }
                }
                else if (Regex.Match(strVariable, @"Player Lock Manual Duration Minutes").Success)
                {
                    Double playerLockingManualDuration;
                    if (!Double.TryParse(strValue, out playerLockingManualDuration))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_playerLockingManualDuration != playerLockingManualDuration)
                    {
                        if (playerLockingManualDuration < 0)
                        {
                            Log.Error("Duration cannot be negative.");
                            return;
                        }
                        _playerLockingManualDuration = playerLockingManualDuration;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Player Lock Manual Duration Minutes", typeof(Double), _playerLockingManualDuration));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatically Lock Players on Admin Action").Success)
                {
                    Boolean playerLockingAutomaticLock = Boolean.Parse(strValue);
                    if (playerLockingAutomaticLock != _playerLockingAutomaticLock)
                    {
                        _playerLockingAutomaticLock = playerLockingAutomaticLock;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatically Lock Players on Admin Action", typeof(Boolean), _playerLockingAutomaticLock));
                    }
                }
                else if (Regex.Match(strVariable, @"Player Lock Automatic Duration Minutes").Success)
                {
                    Double playerLockingAutomaticDuration;
                    if (!Double.TryParse(strValue, out playerLockingAutomaticDuration))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_playerLockingAutomaticDuration != playerLockingAutomaticDuration)
                    {
                        if (playerLockingAutomaticDuration < 0)
                        {
                            Log.Error("Duration cannot be negative.");
                            return;
                        }
                        _playerLockingAutomaticDuration = playerLockingAutomaticDuration;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Player Lock Automatic Duration Minutes", typeof(Double), _playerLockingAutomaticDuration));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed MULTIBalancer Whitelist").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean feedMTBWhite = Boolean.Parse(strValue);
                    if (feedMTBWhite != _FeedMultiBalancerWhitelist)
                    {
                        _FeedMultiBalancerWhitelist = feedMTBWhite;
                        if (_FeedMultiBalancerWhitelist)
                        {
                            SetExternalPluginSetting("MULTIbalancer", "2 - Exclusions|On Whitelist", "True");
                        }
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Whitelist", typeof(Boolean), _FeedMultiBalancerWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic MULTIBalancer Whitelist for Admins").Success)
                {
                    Boolean feedMTBWhiteUser = Boolean.Parse(strValue);
                    if (feedMTBWhiteUser != _FeedMultiBalancerWhitelist_Admins)
                    {
                        _FeedMultiBalancerWhitelist_Admins = feedMTBWhiteUser;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic MULTIBalancer Whitelist for Admins", typeof(Boolean), _FeedMultiBalancerWhitelist_Admins));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed BF4DB Whitelist").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean feedBF4DB = Boolean.Parse(strValue);
                    if (feedBF4DB != _FeedBF4DBWhitelist)
                    {
                        _FeedBF4DBWhitelist = feedBF4DB;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed BF4DB Whitelist", typeof(Boolean), _FeedBF4DBWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed BattlefieldAgency Whitelist").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean feedBA = Boolean.Parse(strValue);
                    if (feedBA != _FeedBAWhitelist)
                    {
                        _FeedBAWhitelist = feedBA;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed BattlefieldAgency Whitelist", typeof(Boolean), _FeedBAWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed TeamKillTracker Whitelist").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean FeedTeamKillTrackerWhitelist = Boolean.Parse(strValue);
                    if (FeedTeamKillTrackerWhitelist != _FeedTeamKillTrackerWhitelist)
                    {
                        _FeedTeamKillTrackerWhitelist = FeedTeamKillTrackerWhitelist;
                        if (_FeedTeamKillTrackerWhitelist)
                        {
                            SetExternalPluginSetting("TeamKillTracker", "Who should be protected?", "Whitelist");
                        }
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed TeamKillTracker Whitelist", typeof(Boolean), _FeedTeamKillTrackerWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic TeamKillTracker Whitelist for Admins").Success)
                {
                    Boolean FeedTeamKillTrackerWhitelist_Admins = Boolean.Parse(strValue);
                    if (FeedTeamKillTrackerWhitelist_Admins != _FeedTeamKillTrackerWhitelist_Admins)
                    {
                        _FeedTeamKillTrackerWhitelist_Admins = FeedTeamKillTrackerWhitelist_Admins;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic TeamKillTracker Whitelist for Admins", typeof(Boolean), _FeedTeamKillTrackerWhitelist_Admins));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed MULTIBalancer Even Dispersion List").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean feedMTBBlack = Boolean.Parse(strValue);
                    if (feedMTBBlack != _FeedMultiBalancerDisperseList)
                    {
                        _FeedMultiBalancerDisperseList = feedMTBBlack;
                        if (_FeedMultiBalancerDisperseList)
                        {
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Conquest Large|Conquest Large: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Conquest Small|Conquest Small: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Defuse|Defuse: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Domination|Domination: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Obliteration|Obliteration: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Rush|Rush: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Squad Deathmatch|Squad Deathmatch: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Superiority|Superiority: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Team Deathmatch|Team Deathmatch: Enable Disperse Evenly List", "True");
                            SetExternalPluginSetting("MULTIbalancer", "8 - Settings for Unknown or New Mode|Unknown or New Mode: Enable Disperse Evenly List", "True");
                        }
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Even Dispersion List", typeof(Boolean), _FeedMultiBalancerDisperseList));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Server Reserved Slots").Success)
                {
                    Boolean FeedServerReservedSlots = Boolean.Parse(strValue);
                    if (FeedServerReservedSlots != _FeedServerReservedSlots)
                    {
                        _FeedServerReservedSlots = FeedServerReservedSlots;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed Server Reserved Slots", typeof(Boolean), _FeedServerReservedSlots));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Reserved Slot for Admins").Success)
                {
                    Boolean FeedServerReservedSlots_Admins = Boolean.Parse(strValue);
                    if (FeedServerReservedSlots_Admins != _FeedServerReservedSlots_Admins)
                    {
                        _FeedServerReservedSlots_Admins = FeedServerReservedSlots_Admins;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Reserved Slot for Admins", typeof(Boolean), _FeedServerReservedSlots_Admins));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic VIP Kick Whitelist for Admins").Success)
                {
                    Boolean FeedServerReservedSlots_Admins_VIPKickWhitelist = Boolean.Parse(strValue);
                    if (FeedServerReservedSlots_Admins_VIPKickWhitelist != _FeedServerReservedSlots_Admins_VIPKickWhitelist)
                    {
                        _FeedServerReservedSlots_Admins_VIPKickWhitelist = FeedServerReservedSlots_Admins_VIPKickWhitelist;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic VIP Kick Whitelist for Admins", typeof(Boolean), _FeedServerReservedSlots_Admins_VIPKickWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Send new reserved slots to VIP Slot Manager").Success)
                {
                    Boolean FeedServerReservedSlots_VSM = Boolean.Parse(strValue);
                    if (FeedServerReservedSlots_VSM != _FeedServerReservedSlots_VSM)
                    {
                        _FeedServerReservedSlots_VSM = FeedServerReservedSlots_VSM;
                        QueueSettingForUpload(new CPluginVariable(@"Send new reserved slots to VIP Slot Manager", typeof(Boolean), _FeedServerReservedSlots_VSM));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Server Spectator List").Success)
                {
                    Boolean feedSSL = Boolean.Parse(strValue);
                    if (feedSSL != _FeedServerSpectatorList)
                    {
                        if (GameVersion == GameVersionEnum.BF3)
                        {
                            Log.Error("This feature cannot be enabled on BF3 servers.");
                            return;
                        }
                        _FeedServerSpectatorList = feedSSL;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed Server Spectator List", typeof(Boolean), _FeedServerSpectatorList));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Spectator Slot for Admins").Success)
                {
                    Boolean feedSSLUser = Boolean.Parse(strValue);
                    if (feedSSLUser != _FeedServerSpectatorList_Admins)
                    {
                        _FeedServerSpectatorList_Admins = feedSSLUser;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Spectator Slot for Admins", typeof(Boolean), _FeedServerSpectatorList_Admins));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Stat Logger Settings").Success)
                {
                    Boolean feedSLS = Boolean.Parse(strValue);
                    if (feedSLS != _FeedStatLoggerSettings)
                    {
                        _FeedStatLoggerSettings = feedSLS;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed Stat Logger Settings", typeof(Boolean), _FeedStatLoggerSettings));
                    }
                }
                else if (Regex.Match(strVariable, @"Post Stat Logger Chat Manually").Success)
                {
                    Boolean PostStatLoggerChatManually = Boolean.Parse(strValue);
                    if (PostStatLoggerChatManually != _PostStatLoggerChatManually)
                    {
                        _PostStatLoggerChatManually = PostStatLoggerChatManually;
                        if (_PostStatLoggerChatManually)
                        {
                            SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Chatlogging?", "No");
                            SetExternalPluginSetting("CChatGUIDStatsLogger", "Instant Logging of Chat Messages?", "No");
                            SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Chatlogging?", "No");
                            SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Instant Logging of Chat Messages?", "No");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Post Stat Logger Chat Manually", typeof(Boolean), _PostStatLoggerChatManually));
                    }
                }
                else if (Regex.Match(strVariable, @"Post Server Chat Spam").Success)
                {
                    Boolean PostServerChatSpam = Boolean.Parse(strValue);
                    if (PostServerChatSpam != _PostStatLoggerChatManually_PostServerChatSpam)
                    {
                        _PostStatLoggerChatManually_PostServerChatSpam = PostServerChatSpam;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Post Server Chat Spam", typeof(Boolean), _PostStatLoggerChatManually_PostServerChatSpam));
                    }
                }
                else if (Regex.Match(strVariable, @"Exclude Commands from Chat Logs").Success)
                {
                    Boolean PostStatLoggerChatManually_IgnoreCommands = Boolean.Parse(strValue);
                    if (PostStatLoggerChatManually_IgnoreCommands != _PostStatLoggerChatManually_IgnoreCommands)
                    {
                        _PostStatLoggerChatManually_IgnoreCommands = PostStatLoggerChatManually_IgnoreCommands;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Exclude Commands from Chat Logs", typeof(Boolean), _PostStatLoggerChatManually_IgnoreCommands));
                    }
                }
                else if (Regex.Match(strVariable, @"Post Map Benefit/Detriment Statistics").Success)
                {
                    Boolean PostMapBenefitStatistics = Boolean.Parse(strValue);
                    if (PostMapBenefitStatistics != _PostMapBenefitStatistics)
                    {
                        _PostMapBenefitStatistics = PostMapBenefitStatistics;
                        if (_threadsReady)
                        {
                            if (_PostMapBenefitStatistics)
                            {
                                Log.Info("Statistics for map benefit/detriment to the server will now be logged.");
                            }
                            else
                            {
                                Log.Info("Statistics for map benefit/detriment to the server will no longer be logged.");
                            }
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Post Map Benefit/Detriment Statistics", typeof(Boolean), _PostMapBenefitStatistics));
                    }
                }
                else if (Regex.Match(strVariable, @"Team Power Active Influence").Success)
                {
                    //Initial parse
                    Int32 TeamPowerActiveInfluence = Int32.Parse(strValue);
                    //Check for changed value
                    if (_TeamPowerActiveInfluence != TeamPowerActiveInfluence)
                    {
                        if (TeamPowerActiveInfluence < 1)
                        {
                            TeamPowerActiveInfluence = 1;
                        }
                        foreach (var aPlayer in GetFetchedPlayers())
                        {
                            aPlayer.TopStats.TempTopPower = 0.0;
                        }
                        //Assignment
                        _TeamPowerActiveInfluence = TeamPowerActiveInfluence;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Team Power Active Influence", typeof(Double), _TeamPowerActiveInfluence));
                    }
                }
                else if (Regex.Match(strVariable, @"Display Team Power In Procon Chat").Success)
                {
                    //Initial parse
                    Boolean UseTeamPowerDisplayBalance = Boolean.Parse(strValue);
                    //Check for changed value
                    if (UseTeamPowerDisplayBalance != _UseTeamPowerDisplayBalance)
                    {
                        //Assignment
                        _UseTeamPowerDisplayBalance = UseTeamPowerDisplayBalance;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Display Team Power In Procon Chat", typeof(Boolean), _UseTeamPowerDisplayBalance));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Team Power Scrambler").Success)
                {
                    //Initial parse
                    Boolean UseTeamPowerMonitorScrambler = false; //Boolean.Parse(strValue);
                    //Check for changed value
                    if (UseTeamPowerMonitorScrambler != _UseTeamPowerMonitorScrambler)
                    {
                        //Assignment
                        _UseTeamPowerMonitorScrambler = UseTeamPowerMonitorScrambler;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_UseTeamPowerMonitorScrambler)
                            {
                                Log.Info("Team scrambling is now being controlled by the team power monitor.");
                            }
                            else
                            {
                                Log.Info("Team scrambling is no longer being controlled by the team power monitor.");
                            }
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Team Power Scrambler", typeof(Boolean), _UseTeamPowerMonitorScrambler));
                    }
                }
                else if (strVariable == "Enable Team Power Join Reassignment")
                {
                    //Initial parse
                    Boolean UseTeamPowerMonitorReassign = Boolean.Parse(strValue);
                    //Check for changed value
                    if (UseTeamPowerMonitorReassign != _UseTeamPowerMonitorReassign)
                    {
                        //Assignment
                        _UseTeamPowerMonitorReassign = UseTeamPowerMonitorReassign;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_UseTeamPowerMonitorReassign)
                            {
                                Log.Info("When players join the server and are over rank 15 they are now automatically placed on the weak team, unless that team would be up by 4 or more players.");
                            }
                            else
                            {
                                Log.Info("Team join reassignment is no longer being controlled by the team power monitor.");
                            }
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Team Power Join Reassignment", typeof(Boolean), _UseTeamPowerMonitorReassign));
                    }
                }
                else if (strVariable == "Team Power Join Reassignment Leniency")
                {
                    //Initial parse
                    Boolean UseTeamPowerMonitorReassignLenient = Boolean.Parse(strValue);
                    //Check for changed value
                    if (UseTeamPowerMonitorReassignLenient != _UseTeamPowerMonitorReassignLenient)
                    {
                        //Assignment
                        _UseTeamPowerMonitorReassignLenient = UseTeamPowerMonitorReassignLenient;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_UseTeamPowerMonitorReassignLenient)
                            {
                                Log.Info("If a reassignment would normally not happen, but a team is down by more than the configured percentage of power, it will assign players to the weak team anyway.");
                            }
                            else
                            {
                                Log.Info("Team join reassignment leniency is no longer enabled.");
                            }
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Team Power Join Reassignment Leniency", typeof(Boolean), _UseTeamPowerMonitorReassignLenient));
                    }
                }
                else if (strVariable == "Team Power Join Reassignment Leniency Percent")
                {
                    Double leniencyPercent;
                    if (!Double.TryParse(strValue, out leniencyPercent))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_TeamPowerMonitorReassignLenientPercent != leniencyPercent)
                    {
                        if (leniencyPercent <= 15.0)
                        {
                            leniencyPercent = 15.0;
                        }
                        _TeamPowerMonitorReassignLenientPercent = leniencyPercent;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Team Power Join Reassignment Leniency Percent", typeof(Double), _TeamPowerMonitorReassignLenientPercent));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Team Power Unswitcher").Success)
                {
                    //Initial parse
                    Boolean UseTeamPowerMonitorUnswitcher = Boolean.Parse(strValue);
                    //Check for changed value
                    if (UseTeamPowerMonitorUnswitcher != _UseTeamPowerMonitorUnswitcher)
                    {
                        //Assignment
                        _UseTeamPowerMonitorUnswitcher = UseTeamPowerMonitorUnswitcher;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_UseTeamPowerMonitorUnswitcher)
                            {
                                Log.Info("Based on the 'unswitcher' in MULTIBalancer, this system works based on team power and wont let players move to the more powerful team.");
                            }
                            else
                            {
                                Log.Info("Manual player movement is no longer being controlled by the team power monitor.");
                            }
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Team Power Unswitcher", typeof(Boolean), _UseTeamPowerMonitorUnswitcher));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Team Power Seeder Control").Success)
                {
                    //Initial parse
                    Boolean UseTeamPowerMonitorSeeders = Boolean.Parse(strValue);
                    //Check for changed value
                    if (UseTeamPowerMonitorSeeders != _UseTeamPowerMonitorSeeders)
                    {
                        //Assignment
                        _UseTeamPowerMonitorSeeders = UseTeamPowerMonitorSeeders;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_UseTeamPowerMonitorSeeders)
                            {
                                Log.Info("Team seeders is now being controlled by the team power monitor.");
                            }
                            else
                            {
                                Log.Info("Team seeders is no longer being controlled by the team power monitor.");
                            }
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Team Power Seeder Control", typeof(Boolean), _UseTeamPowerMonitorSeeders));
                    }
                }
                else if (Regex.Match(strVariable, @"Monitor Populator Players").Success)
                {
                    //Initial parse
                    Boolean PopulatorMonitor = Boolean.Parse(strValue);
                    //Check for changed value
                    if (PopulatorMonitor != _PopulatorMonitor)
                    {
                        //Assignment
                        _PopulatorMonitor = PopulatorMonitor;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_PopulatorMonitor)
                            {
                                Log.Info("Populator players are now being monitored.");
                                UpdatePopulatorPlayers();
                            }
                            else
                            {
                                Log.Info("Populator players are no longer being monitored.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Monitor Populator Players", typeof(Boolean), _PopulatorMonitor));
                    }
                }
                else if (Regex.Match(strVariable, @"Monitor Specified Populators Only").Success)
                {
                    //Initial parse
                    Boolean PopulatorUseSpecifiedPopulatorsOnly = Boolean.Parse(strValue);
                    //Check for changed value
                    if (PopulatorUseSpecifiedPopulatorsOnly != _PopulatorUseSpecifiedPopulatorsOnly)
                    {
                        //Assignment
                        _PopulatorUseSpecifiedPopulatorsOnly = PopulatorUseSpecifiedPopulatorsOnly;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_PopulatorUseSpecifiedPopulatorsOnly)
                            {
                                Log.Info("Only players under whitelist_populator specialplayer group can be considered populators now.");
                            }
                            else
                            {
                                Log.Info("All players can be considered populators now.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Monitor Specified Populators Only", typeof(Boolean), _PopulatorUseSpecifiedPopulatorsOnly));
                    }
                }
                else if (Regex.Match(strVariable, @"Monitor Populators of This Server Only").Success)
                {
                    //Initial parse
                    Boolean PopulatorPopulatingThisServerOnly = Boolean.Parse(strValue);
                    //Check for changed value
                    if (PopulatorPopulatingThisServerOnly != _PopulatorPopulatingThisServerOnly)
                    {
                        //Assignment
                        _PopulatorPopulatingThisServerOnly = PopulatorPopulatingThisServerOnly;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_PopulatorPopulatingThisServerOnly)
                            {
                                Log.Info("Only populations of this server will be considered toward a player's populator status on this server.");
                            }
                            else
                            {
                                Log.Info("Populations of all servers will be considered toward a player's population status on this server.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Monitor Populators of This Server Only", typeof(Boolean), _PopulatorPopulatingThisServerOnly));
                    }
                }
                else if (Regex.Match(strVariable, @"Count to Consider Populator Past Week").Success)
                {
                    //Initial parse
                    Int32 PopulatorMinimumPopulationCountPastWeek = Int32.Parse(strValue);
                    //Check for changed value
                    if (_PopulatorMinimumPopulationCountPastWeek != PopulatorMinimumPopulationCountPastWeek)
                    {
                        //Rejection cases
                        if (PopulatorMinimumPopulationCountPastWeek < 1)
                        {
                            Log.Error("'Count to Consider Populator Past Week' cannot be less than 1.");
                            PopulatorMinimumPopulationCountPastWeek = 1;
                        }
                        //Assignment
                        _PopulatorMinimumPopulationCountPastWeek = PopulatorMinimumPopulationCountPastWeek;
                        //Notification
                        if (_threadsReady)
                        {
                            Log.Info("Players are now considered populator if they contribute to " + _PopulatorMinimumPopulationCountPastWeek + " populations in the past week, or " + _PopulatorMinimumPopulationCountPast2Weeks + " populations in the past 2 weeks.");

                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Count to Consider Populator Past Week", typeof(Int32), _PopulatorMinimumPopulationCountPastWeek));
                    }
                }
                else if (Regex.Match(strVariable, @"Count to Consider Populator Past 2 Weeks").Success)
                {
                    //Initial parse
                    Int32 PopulatorMinimumPopulationCountPast2Weeks = Int32.Parse(strValue);
                    //Check for changed value
                    if (_PopulatorMinimumPopulationCountPast2Weeks != PopulatorMinimumPopulationCountPast2Weeks)
                    {
                        //Rejection cases
                        if (PopulatorMinimumPopulationCountPast2Weeks < 1)
                        {
                            Log.Error("'Count to Consider Populator Past 2 Weeks' cannot be less than 1.");
                            PopulatorMinimumPopulationCountPast2Weeks = 1;
                        }
                        //Assignment
                        _PopulatorMinimumPopulationCountPast2Weeks = PopulatorMinimumPopulationCountPast2Weeks;
                        //Notification
                        if (_threadsReady)
                        {
                            Log.Info("Players are now considered populator if they contribute to " + _PopulatorMinimumPopulationCountPastWeek + " populations in the past week, or " + _PopulatorMinimumPopulationCountPast2Weeks + " populations in the past 2 weeks.");

                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Count to Consider Populator Past 2 Weeks", typeof(Int32), _PopulatorMinimumPopulationCountPast2Weeks));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Populator Perks").Success)
                {
                    //Initial parse
                    Boolean PopulatorPerksEnable = Boolean.Parse(strValue);
                    //Check for changed value
                    if (PopulatorPerksEnable != _PopulatorPerksEnable)
                    {
                        //Assignment
                        _PopulatorPerksEnable = PopulatorPerksEnable;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_PopulatorPerksEnable)
                            {
                                Log.Info("Populator perks are now enabled.");
                            }
                            else
                            {
                                Log.Info("Populator perks are now disabled.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Populator Perks", typeof(Boolean), _PopulatorPerksEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Populator Perks - Reserved Slot").Success)
                {
                    //Initial parse
                    Boolean PopulatorPerksReservedSlot = Boolean.Parse(strValue);
                    //Check for changed value
                    if (PopulatorPerksReservedSlot != _PopulatorPerksReservedSlot)
                    {
                        //Rejection cases
                        if (_threadsReady && !_FeedServerReservedSlots && PopulatorPerksReservedSlot)
                        {
                            Log.Error("'Populator Perks - Reserved Slot' cannot be enabled when 'Feed Server Reserved Slots' is disabled.");
                            return;
                        }
                        //Assignment
                        _PopulatorPerksReservedSlot = PopulatorPerksReservedSlot;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_PopulatorPerksReservedSlot)
                            {
                                Log.Info("Populator perks now include reserved slot.");
                            }
                            else
                            {
                                Log.Info("Populator perks no longer include reserved slot.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Populator Perks - Reserved Slot", typeof(Boolean), _PopulatorPerksReservedSlot));
                    }
                }
                else if (Regex.Match(strVariable, @"Populator Perks - Autobalance Whitelist").Success)
                {
                    //Initial parse
                    Boolean PopulatorPerksBalanceWhitelist = Boolean.Parse(strValue);
                    //Check for changed value
                    if (PopulatorPerksBalanceWhitelist != _PopulatorPerksBalanceWhitelist)
                    {
                        //Rejection cases
                        if (_threadsReady && !_FeedMultiBalancerWhitelist && PopulatorPerksBalanceWhitelist)
                        {
                            Log.Error("'Populator Perks - Autobalance Whitelist' cannot be enabled when 'Feed MULTIBalancer Whitelist' is disabled.");
                            return;
                        }
                        //Assignment
                        _PopulatorPerksBalanceWhitelist = PopulatorPerksBalanceWhitelist;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_PopulatorPerksBalanceWhitelist)
                            {
                                Log.Info("Populator perks now include autobalance whitelist.");
                            }
                            else
                            {
                                Log.Info("Populator perks no longer include autobalance whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Populator Perks - Autobalance Whitelist", typeof(Boolean), _PopulatorPerksBalanceWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Populator Perks - Ping Whitelist").Success)
                {
                    //Initial parse
                    Boolean PopulatorPerksPingWhitelist = Boolean.Parse(strValue);
                    //Check for changed value
                    if (PopulatorPerksPingWhitelist != _PopulatorPerksPingWhitelist)
                    {
                        //Rejection cases
                        if (_threadsReady && !_pingEnforcerEnable && PopulatorPerksPingWhitelist)
                        {
                            Log.Error("'Populator Perks - Ping Whitelist' cannot be enabled when Ping Enforcer is disabled.");
                            return;
                        }
                        //Assignment
                        _PopulatorPerksPingWhitelist = PopulatorPerksPingWhitelist;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_PopulatorPerksPingWhitelist)
                            {
                                Log.Info("Populator perks now include ping whitelist.");
                            }
                            else
                            {
                                Log.Info("Populator perks no longer include ping whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Populator Perks - Ping Whitelist", typeof(Boolean), _PopulatorPerksPingWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Populator Perks - TeamKillTracker Whitelist").Success)
                {
                    //Initial parse
                    Boolean PopulatorPerksTeamKillTrackerWhitelist = Boolean.Parse(strValue);
                    //Check for changed value
                    if (PopulatorPerksTeamKillTrackerWhitelist != _PopulatorPerksTeamKillTrackerWhitelist)
                    {
                        //Rejection cases
                        if (_threadsReady && !_FeedTeamKillTrackerWhitelist && PopulatorPerksTeamKillTrackerWhitelist)
                        {
                            Log.Error("'Populator Perks - TeamKillTracker Whitelist' cannot be enabled when 'Feed TeamKillTracker Whitelist' is disabled.");
                            return;
                        }
                        //Assignment
                        _PopulatorPerksTeamKillTrackerWhitelist = PopulatorPerksTeamKillTrackerWhitelist;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_PopulatorPerksTeamKillTrackerWhitelist)
                            {
                                Log.Info("Populator perks now include TeamKillTracker whitelist.");
                            }
                            else
                            {
                                Log.Info("Populator perks no longer include TeamKillTracker whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Populator Perks - TeamKillTracker Whitelist", typeof(Boolean), _PopulatorPerksTeamKillTrackerWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Monitor Teamspeak Players").Success)
                {
                    //Initial parse
                    Boolean TeamspeakPlayerMonitorView = Boolean.Parse(strValue);
                    //Check for changed value
                    if (TeamspeakPlayerMonitorView != _TeamspeakPlayerMonitorView)
                    {
                        //Assignment
                        _TeamspeakPlayerMonitorView = TeamspeakPlayerMonitorView;
                        //No Notification
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Monitor Teamspeak Players", typeof(Boolean), _TeamspeakPlayerMonitorView));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Teamspeak Player Monitor").Success)
                {
                    //Initial parse
                    Boolean TeamspeakPlayerMonitorEnable = Boolean.Parse(strValue);
                    //Check for changed value
                    if (TeamspeakPlayerMonitorEnable != _TeamspeakPlayerMonitorEnable)
                    {
                        //Assignment
                        _TeamspeakPlayerMonitorEnable = TeamspeakPlayerMonitorEnable;
                        if (_threadsReady)
                        {
                            if (TeamspeakPlayerMonitorEnable)
                            {
                                _TeamspeakManager.Enable();
                            }
                            else
                            {
                                _TeamspeakManager.Disable();
                            }
                        }
                        //No Notification
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Teamspeak Player Monitor", typeof(Boolean), _TeamspeakPlayerMonitorEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Server IP").Success)
                {
                    if (_TeamspeakManager.Ts3ServerIp != strValue)
                    {
                        _TeamspeakManager.Ts3ServerIp = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server IP", typeof(String), _TeamspeakManager.Ts3ServerIp));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Server Port").Success)
                {
                    //Initial parse
                    Int32 Ts3ServerPort = Int32.Parse(strValue);
                    //Check for changed value
                    if (_TeamspeakManager.Ts3ServerPort != Ts3ServerPort)
                    {
                        //Assignment
                        _TeamspeakManager.Ts3ServerPort = (ushort)Ts3ServerPort;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Port", typeof(Int32), _TeamspeakManager.Ts3ServerPort));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Server Query Port").Success)
                {
                    //Initial parse
                    Int32 Ts3QueryPort = Int32.Parse(strValue);
                    //Check for changed value
                    if (_TeamspeakManager.Ts3QueryPort != Ts3QueryPort)
                    {
                        //Assignment
                        _TeamspeakManager.Ts3QueryPort = (ushort)Ts3QueryPort;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Query Port", typeof(Int32), _TeamspeakManager.Ts3QueryPort));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Server Query Username").Success)
                {
                    if (_TeamspeakManager.Ts3QueryUsername != strValue)
                    {
                        _TeamspeakManager.Ts3QueryUsername = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Query Username", typeof(String), _TeamspeakManager.Ts3QueryUsername));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Server Query Password").Success)
                {
                    if (_TeamspeakManager.Ts3QueryPassword != strValue)
                    {
                        _TeamspeakManager.Ts3QueryPassword = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Query Password", typeof(String), _TeamspeakManager.Ts3QueryPassword));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Server Query Nickname").Success)
                {
                    if (_TeamspeakManager.Ts3QueryNickname != strValue)
                    {
                        _TeamspeakManager.Ts3QueryNickname = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Query Nickname", typeof(String), _TeamspeakManager.Ts3QueryNickname));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Main Channel Name").Success)
                {
                    if (_TeamspeakManager.Ts3MainChannelName != strValue)
                    {
                        _TeamspeakManager.Ts3MainChannelName = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Main Channel Name", typeof(String), _TeamspeakManager.Ts3MainChannelName));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Secondary Channel Names").Success)
                {
                    _TeamspeakManager.Ts3SubChannelNames = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Teamspeak Secondary Channel Names", typeof(String), CPluginVariable.EncodeStringArray(_TeamspeakManager.Ts3SubChannelNames)));
                }
                else if (Regex.Match(strVariable, @"Debug Display Teamspeak Clients").Success)
                {
                    //Initial parse
                    Boolean DbgClients = Boolean.Parse(strValue);
                    //Check for changed value
                    if (DbgClients != _TeamspeakManager.DebugClients)
                    {
                        //Assignment
                        _TeamspeakManager.DebugClients = DbgClients;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Debug Display Teamspeak Clients", typeof(Boolean), _TeamspeakManager.DebugClients));
                    }
                }
                else if (Regex.Match(strVariable, @"TeamSpeak Player Join Announcement").Success)
                {
                    switch (strValue)
                    {
                        case "Disabled":
                            _TeamspeakManager.JoinDisplay = VoipJoinDisplayType.Disabled;
                            break;
                        case "Say":
                            _TeamspeakManager.JoinDisplay = VoipJoinDisplayType.Say;
                            break;
                        case "Yell":
                            _TeamspeakManager.JoinDisplay = VoipJoinDisplayType.Yell;
                            break;
                        case "Tell":
                            _TeamspeakManager.JoinDisplay = VoipJoinDisplayType.Tell;
                            break;
                        default:
                            Log.Error("Unknown setting when setting teamspeak player announcement.");
                            return;
                    }
                    QueueSettingForUpload(new CPluginVariable(@"TeamSpeak Player Join Announcement", typeof(String), _TeamspeakManager.JoinDisplay.ToString()));
                }
                else if (Regex.Match(strVariable, @"TeamSpeak Player Join Message").Success)
                {
                    if (_TeamspeakManager.JoinDisplayMessage != strValue && (strValue.Contains("%player%") || strValue.Contains("%username%") || strValue.Contains("%playerusername%")))
                    {
                        _TeamspeakManager.JoinDisplayMessage = strValue;
                        QueueSettingForUpload(new CPluginVariable(@"TeamSpeak Player Join Message", typeof(String), _TeamspeakManager.JoinDisplayMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"TeamSpeak Player Update Seconds").Success)
                {
                    //Initial parse
                    Int32 UpdateIntervalSeconds = Int32.Parse(strValue);
                    //Check for changed value
                    if (_TeamspeakManager.UpdateIntervalSeconds != UpdateIntervalSeconds)
                    {
                        if (UpdateIntervalSeconds < 5)
                        {
                            UpdateIntervalSeconds = 5;
                        }
                        //Assignment
                        _TeamspeakManager.UpdateIntervalSeconds = UpdateIntervalSeconds;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"TeamSpeak Player Update Seconds", typeof(Int32), _TeamspeakManager.UpdateIntervalSeconds));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Teamspeak Player Perks").Success)
                {
                    //Initial parse
                    Boolean TeamspeakPlayerPerksEnable = Boolean.Parse(strValue);
                    //Check for changed value
                    if (TeamspeakPlayerPerksEnable != _TeamspeakPlayerPerksEnable)
                    {
                        //Assignment
                        _TeamspeakPlayerPerksEnable = TeamspeakPlayerPerksEnable;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Teamspeak Player Perks", typeof(Boolean), _TeamspeakPlayerPerksEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Player Perks - VIP Kick Whitelist").Success)
                {
                    //Initial parse
                    Boolean TeamspeakPlayerPerksVIPKickWhitelist = Boolean.Parse(strValue);
                    //Check for changed value
                    if (TeamspeakPlayerPerksVIPKickWhitelist != _TeamspeakPlayerPerksVIPKickWhitelist)
                    {
                        //Rejection cases
                        if (_threadsReady && !_FeedServerReservedSlots && TeamspeakPlayerPerksVIPKickWhitelist)
                        {
                            Log.Error("'Teamspeak Player Perks - VIP Kick Whitelist' cannot be enabled when 'Feed Server Reserved Slots' is disabled.");
                            return;
                        }
                        //Assignment
                        _TeamspeakPlayerPerksVIPKickWhitelist = TeamspeakPlayerPerksVIPKickWhitelist;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_TeamspeakPlayerPerksVIPKickWhitelist)
                            {
                                Log.Info("Teamspeak Player perks now include VIP Kick Whitelist.");
                            }
                            else
                            {
                                Log.Info("Teamspeak Player perks no longer include VIP Kick Whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Player Perks - VIP Kick Whitelist", typeof(Boolean), _TeamspeakPlayerPerksVIPKickWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Player Perks - Autobalance Whitelist").Success)
                {
                    //Initial parse
                    Boolean TeamspeakPlayerPerksBalanceWhitelist = Boolean.Parse(strValue);
                    //Check for changed value
                    if (TeamspeakPlayerPerksBalanceWhitelist != _TeamspeakPlayerPerksBalanceWhitelist)
                    {
                        //Rejection cases
                        if (_threadsReady && !_FeedMultiBalancerWhitelist && TeamspeakPlayerPerksBalanceWhitelist)
                        {
                            Log.Error("'Teamspeak Player Perks - Autobalance Whitelist' cannot be enabled when 'Feed MULTIBalancer Whitelist' is disabled.");
                            return;
                        }
                        //Assignment
                        _TeamspeakPlayerPerksBalanceWhitelist = TeamspeakPlayerPerksBalanceWhitelist;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_TeamspeakPlayerPerksBalanceWhitelist)
                            {
                                Log.Info("Teamspeak Player perks now include autobalance whitelist.");
                            }
                            else
                            {
                                Log.Info("Teamspeak Player perks no longer include autobalance whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Player Perks - Autobalance Whitelist", typeof(Boolean), _TeamspeakPlayerPerksBalanceWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Player Perks - Ping Whitelist").Success)
                {
                    //Initial parse
                    Boolean TeamspeakPlayerPerksPingWhitelist = Boolean.Parse(strValue);
                    //Check for changed value
                    if (TeamspeakPlayerPerksPingWhitelist != _TeamspeakPlayerPerksPingWhitelist)
                    {
                        //Rejection cases
                        if (_threadsReady && !_pingEnforcerEnable && TeamspeakPlayerPerksPingWhitelist)
                        {
                            Log.Error("'Teamspeak Player Perks - Ping Whitelist' cannot be enabled when Ping Enforcer is disabled.");
                            return;
                        }
                        //Assignment
                        _TeamspeakPlayerPerksPingWhitelist = TeamspeakPlayerPerksPingWhitelist;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_TeamspeakPlayerPerksPingWhitelist)
                            {
                                Log.Info("Teamspeak Player perks now include ping whitelist.");
                            }
                            else
                            {
                                Log.Info("Teamspeak Player perks no longer include ping whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Player Perks - Ping Whitelist", typeof(Boolean), _TeamspeakPlayerPerksPingWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Teamspeak Player Perks - TeamKillTracker Whitelist").Success)
                {
                    //Initial parse
                    Boolean TeamspeakPlayerPerksTeamKillTrackerWhitelist = Boolean.Parse(strValue);
                    //Check for changed value
                    if (TeamspeakPlayerPerksTeamKillTrackerWhitelist != _TeamspeakPlayerPerksTeamKillTrackerWhitelist)
                    {
                        //Rejection cases
                        if (_threadsReady && !_FeedTeamKillTrackerWhitelist && TeamspeakPlayerPerksTeamKillTrackerWhitelist)
                        {
                            Log.Error("'Teamspeak Player Perks - TeamKillTracker Whitelist' cannot be enabled when 'Feed TeamKillTracker Whitelist' is disabled.");
                            return;
                        }
                        //Assignment
                        _TeamspeakPlayerPerksTeamKillTrackerWhitelist = TeamspeakPlayerPerksTeamKillTrackerWhitelist;
                        //Notification
                        if (_threadsReady)
                        {
                            if (_TeamspeakPlayerPerksTeamKillTrackerWhitelist)
                            {
                                Log.Info("Teamspeak Player perks now include TeamKillTracker whitelist.");
                            }
                            else
                            {
                                Log.Info("Teamspeak Player perks no longer include TeamKillTracker whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Teamspeak Player Perks - TeamKillTracker Whitelist", typeof(Boolean), _TeamspeakPlayerPerksTeamKillTrackerWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Teamspeak Players - Enable").Success)
                {
                    //Initial parse
                    Boolean TeamspeakOnlinePlayersEnable = Boolean.Parse(strValue);
                    //Check for changed value
                    if (TeamspeakOnlinePlayersEnable != _TeamspeakOnlinePlayersEnable)
                    {
                        //Assignment
                        _TeamspeakOnlinePlayersEnable = TeamspeakOnlinePlayersEnable;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Enable", typeof(Boolean), _TeamspeakOnlinePlayersEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Teamspeak Players - Interval Minutes").Success)
                {
                    //Initial parse
                    Int32 TeamspeakOnlinePlayersInterval = Int32.Parse(strValue);
                    //Check for changed value
                    if (_TeamspeakOnlinePlayersInterval != TeamspeakOnlinePlayersInterval)
                    {
                        if (TeamspeakOnlinePlayersInterval < 2)
                        {
                            TeamspeakOnlinePlayersInterval = 2;
                        }
                        //Assignment
                        _TeamspeakOnlinePlayersInterval = TeamspeakOnlinePlayersInterval;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Interval Minutes", typeof(Int32), _TeamspeakOnlinePlayersInterval));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Teamspeak Players - Max Players to List").Success)
                {
                    //Initial parse
                    Int32 TeamspeakOnlinePlayersMaxPlayersToList = Int32.Parse(strValue);
                    //Check for changed value
                    if (_TeamspeakOnlinePlayersMaxPlayersToList != TeamspeakOnlinePlayersMaxPlayersToList)
                    {
                        if (TeamspeakOnlinePlayersMaxPlayersToList < 1)
                        {
                            TeamspeakOnlinePlayersMaxPlayersToList = 1;
                        }
                        else if (TeamspeakOnlinePlayersMaxPlayersToList > 8)
                        {
                            TeamspeakOnlinePlayersMaxPlayersToList = 8;
                        }
                        //Assignment
                        _TeamspeakOnlinePlayersMaxPlayersToList = TeamspeakOnlinePlayersMaxPlayersToList;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Max Players to List", typeof(Int32), _TeamspeakOnlinePlayersMaxPlayersToList));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Teamspeak Players - Single Player Message").Success)
                {
                    if (_TeamspeakOnlinePlayersAloneMessage != strValue)
                    {
                        _TeamspeakOnlinePlayersAloneMessage = strValue;
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Single Player Message", typeof(String), _TeamspeakOnlinePlayersAloneMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Teamspeak Players - Multi Player Message").Success)
                {
                    if (_TeamspeakOnlinePlayersMessage != strValue)
                    {
                        _TeamspeakOnlinePlayersMessage = strValue;
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Multi Player Message", typeof(String), _TeamspeakOnlinePlayersMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Monitor Discord Players").Success)
                {
                    Boolean DiscordPlayerMonitorView = Boolean.Parse(strValue);
                    if (DiscordPlayerMonitorView != _DiscordPlayerMonitorView)
                    {
                        _DiscordPlayerMonitorView = DiscordPlayerMonitorView;
                        QueueSettingForUpload(new CPluginVariable(@"Monitor Discord Players", typeof(Boolean), _DiscordPlayerMonitorView));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Discord Player Monitor").Success)
                {
                    Boolean DiscordPlayerMonitorEnable = Boolean.Parse(strValue);
                    if (DiscordPlayerMonitorEnable != _DiscordPlayerMonitorEnable)
                    {
                        _DiscordPlayerMonitorEnable = DiscordPlayerMonitorEnable;
                        if (_threadsReady)
                        {
                            if (DiscordPlayerMonitorEnable)
                            {
                                _DiscordManager.Enable();
                            }
                            else
                            {
                                _DiscordManager.Disable();
                            }
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Enable Discord Player Monitor", typeof(Boolean), _DiscordPlayerMonitorEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord API URL").Success)
                {
                    if (_DiscordManager.APIUrl != strValue)
                    {
                        _DiscordManager.APIUrl = strValue;
                        QueueSettingForUpload(new CPluginVariable(@"Discord API URL", typeof(String), _DiscordManager.APIUrl));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Server ID").Success)
                {
                    if (_DiscordManager.ServerID != strValue)
                    {
                        _DiscordManager.ServerID = strValue;
                        QueueSettingForUpload(new CPluginVariable(@"Discord Server ID", typeof(String), _DiscordManager.ServerID));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Channel Names").Success)
                {
                    _DiscordManager.ChannelNames = CPluginVariable.DecodeStringArray(strValue);
                    QueueSettingForUpload(new CPluginVariable(@"Discord Channel Names", typeof(String), CPluginVariable.EncodeStringArray(_DiscordManager.ChannelNames)));
                }
                else if (Regex.Match(strVariable, @"Debug Display Discord Members").Success)
                {
                    Boolean DebugMembers = Boolean.Parse(strValue);
                    if (DebugMembers != _DiscordManager.DebugMembers)
                    {
                        _DiscordManager.DebugMembers = DebugMembers;
                        QueueSettingForUpload(new CPluginVariable(@"Debug Display Discord Members", typeof(Boolean), _DiscordManager.DebugMembers));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Player Join Announcement").Success)
                {
                    switch (strValue)
                    {
                        case "Disabled":
                            _DiscordManager.JoinDisplay = VoipJoinDisplayType.Disabled;
                            break;
                        case "Say":
                            _DiscordManager.JoinDisplay = VoipJoinDisplayType.Say;
                            break;
                        case "Yell":
                            _DiscordManager.JoinDisplay = VoipJoinDisplayType.Yell;
                            break;
                        case "Tell":
                            _DiscordManager.JoinDisplay = VoipJoinDisplayType.Tell;
                            break;
                        default:
                            Log.Error("Unknown value when setting discord player announcement.");
                            return;
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Discord Player Join Announcement", typeof(String), _DiscordManager.JoinDisplay.ToString()));
                }
                else if (Regex.Match(strVariable, @"Discord Player Join Message").Success)
                {
                    if (_DiscordManager.JoinMessage != strValue && (strValue.Contains("%player%") || strValue.Contains("%username%") || strValue.Contains("%playerusername%")))
                    {
                        _DiscordManager.JoinMessage = strValue;
                        QueueSettingForUpload(new CPluginVariable(@"Discord Player Join Message", typeof(String), _DiscordManager.JoinMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Discord Player Perks").Success)
                {
                    Boolean DiscordPlayerPerksEnable = Boolean.Parse(strValue);
                    if (DiscordPlayerPerksEnable != _DiscordPlayerPerksEnable)
                    {
                        _DiscordPlayerPerksEnable = DiscordPlayerPerksEnable;
                        QueueSettingForUpload(new CPluginVariable(@"Enable Discord Player Perks", typeof(Boolean), _DiscordPlayerPerksEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Require Voice in Discord to Issue Admin Commands").Success)
                {
                    Boolean DiscordPlayerRequireVoiceForAdmin = Boolean.Parse(strValue);
                    if (DiscordPlayerRequireVoiceForAdmin != _DiscordPlayerRequireVoiceForAdmin)
                    {
                        _DiscordPlayerRequireVoiceForAdmin = DiscordPlayerRequireVoiceForAdmin;
                        QueueSettingForUpload(new CPluginVariable(@"Require Voice in Discord to Issue Admin Commands", typeof(Boolean), _DiscordPlayerRequireVoiceForAdmin));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Player Perks - VIP Kick Whitelist").Success)
                {
                    Boolean DiscordPlayerPerksVIPKickWhitelist = Boolean.Parse(strValue);
                    if (DiscordPlayerPerksVIPKickWhitelist != _DiscordPlayerPerksVIPKickWhitelist)
                    {
                        if (_threadsReady && !_FeedServerReservedSlots && DiscordPlayerPerksVIPKickWhitelist)
                        {
                            Log.Error("'Discord Player Perks - VIP Kick Whitelist' cannot be enabled when 'Feed Server Reserved Slots' is disabled.");
                            return;
                        }
                        _DiscordPlayerPerksVIPKickWhitelist = DiscordPlayerPerksVIPKickWhitelist;
                        if (_threadsReady)
                        {
                            if (_DiscordPlayerPerksVIPKickWhitelist)
                            {
                                Log.Info("Discord Player perks now include VIP Kick Whitelist.");
                            }
                            else
                            {
                                Log.Info("Discord Player perks no longer include VIP Kick Whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Discord Player Perks - VIP Kick Whitelist", typeof(Boolean), _DiscordPlayerPerksVIPKickWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Player Perks - Autobalance Whitelist").Success)
                {
                    Boolean DiscordPlayerPerksBalanceWhitelist = Boolean.Parse(strValue);
                    if (DiscordPlayerPerksBalanceWhitelist != _DiscordPlayerPerksBalanceWhitelist)
                    {
                        if (_threadsReady && !_FeedMultiBalancerWhitelist && DiscordPlayerPerksBalanceWhitelist)
                        {
                            Log.Error("'Discord Player Perks - Autobalance Whitelist' cannot be enabled when 'Feed MULTIBalancer Whitelist' is disabled.");
                            return;
                        }
                        _DiscordPlayerPerksBalanceWhitelist = DiscordPlayerPerksBalanceWhitelist;
                        if (_threadsReady)
                        {
                            if (_DiscordPlayerPerksBalanceWhitelist)
                            {
                                Log.Info("Discord Player perks now include autobalance whitelist.");
                            }
                            else
                            {
                                Log.Info("Discord Player perks no longer include autobalance whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Discord Player Perks - Autobalance Whitelist", typeof(Boolean), _DiscordPlayerPerksBalanceWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Player Perks - Ping Whitelist").Success)
                {
                    Boolean DiscordPlayerPerksPingWhitelist = Boolean.Parse(strValue);
                    if (DiscordPlayerPerksPingWhitelist != _DiscordPlayerPerksPingWhitelist)
                    {
                        if (_threadsReady && !_pingEnforcerEnable && DiscordPlayerPerksPingWhitelist)
                        {
                            Log.Error("'Discord Player Perks - Ping Whitelist' cannot be enabled when Ping Enforcer is disabled.");
                            return;
                        }
                        _DiscordPlayerPerksPingWhitelist = DiscordPlayerPerksPingWhitelist;
                        if (_threadsReady)
                        {
                            if (_DiscordPlayerPerksPingWhitelist)
                            {
                                Log.Info("Discord Player perks now include ping whitelist.");
                            }
                            else
                            {
                                Log.Info("Discord Player perks no longer include ping whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Discord Player Perks - Ping Whitelist", typeof(Boolean), _DiscordPlayerPerksPingWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Player Perks - TeamKillTracker Whitelist").Success)
                {
                    Boolean DiscordPlayerPerksTeamKillTrackerWhitelist = Boolean.Parse(strValue);
                    if (DiscordPlayerPerksTeamKillTrackerWhitelist != _DiscordPlayerPerksTeamKillTrackerWhitelist)
                    {
                        if (_threadsReady && !_FeedTeamKillTrackerWhitelist && DiscordPlayerPerksTeamKillTrackerWhitelist)
                        {
                            Log.Error("'Discord Player Perks - TeamKillTracker Whitelist' cannot be enabled when 'Feed TeamKillTracker Whitelist' is disabled.");
                            return;
                        }
                        _DiscordPlayerPerksTeamKillTrackerWhitelist = DiscordPlayerPerksTeamKillTrackerWhitelist;
                        if (_threadsReady)
                        {
                            if (_DiscordPlayerPerksTeamKillTrackerWhitelist)
                            {
                                Log.Info("Discord Player perks now include TeamKillTracker whitelist.");
                            }
                            else
                            {
                                Log.Info("Discord Player perks no longer include TeamKillTracker whitelist.");
                            }
                            FetchAllAccess(true);
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Discord Player Perks - TeamKillTracker Whitelist", typeof(Boolean), _DiscordPlayerPerksTeamKillTrackerWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Discord Players - Enable").Success)
                {
                    //Initial parse
                    Boolean DiscordOnlinePlayersEnable = Boolean.Parse(strValue);
                    //Check for changed value
                    if (DiscordOnlinePlayersEnable != _DiscordOnlinePlayersEnable)
                    {
                        //Assignment
                        _DiscordOnlinePlayersEnable = DiscordOnlinePlayersEnable;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Enable", typeof(Boolean), _DiscordOnlinePlayersEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Discord Players - Interval Minutes").Success)
                {
                    //Initial parse
                    Int32 DiscordOnlinePlayersInterval = Int32.Parse(strValue);
                    //Check for changed value
                    if (_DiscordOnlinePlayersInterval != DiscordOnlinePlayersInterval)
                    {
                        if (DiscordOnlinePlayersInterval < 2)
                        {
                            DiscordOnlinePlayersInterval = 2;
                        }
                        //Assignment
                        _DiscordOnlinePlayersInterval = DiscordOnlinePlayersInterval;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Interval Minutes", typeof(Int32), _DiscordOnlinePlayersInterval));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Discord Players - Max Players to List").Success)
                {
                    //Initial parse
                    Int32 DiscordOnlinePlayersMaxPlayersToList = Int32.Parse(strValue);
                    //Check for changed value
                    if (_DiscordOnlinePlayersMaxPlayersToList != DiscordOnlinePlayersMaxPlayersToList)
                    {
                        if (DiscordOnlinePlayersMaxPlayersToList < 1)
                        {
                            DiscordOnlinePlayersMaxPlayersToList = 1;
                        }
                        else if (DiscordOnlinePlayersMaxPlayersToList > 8)
                        {
                            DiscordOnlinePlayersMaxPlayersToList = 8;
                        }
                        //Assignment
                        _DiscordOnlinePlayersMaxPlayersToList = DiscordOnlinePlayersMaxPlayersToList;
                        //Upload change to database
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Max Players to List", typeof(Int32), _DiscordOnlinePlayersMaxPlayersToList));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Discord Players - Single Player Message").Success)
                {
                    if (_DiscordOnlinePlayersAloneMessage != strValue)
                    {
                        _DiscordOnlinePlayersAloneMessage = strValue;
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Single Player Message", typeof(String), _DiscordOnlinePlayersAloneMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Announce Online Discord Players - Multi Player Message").Success)
                {
                    if (_DiscordOnlinePlayersMessage != strValue)
                    {
                        _DiscordOnlinePlayersMessage = strValue;
                        QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Multi Player Message", typeof(String), _DiscordOnlinePlayersMessage));
                    }
                }
                // Discord Watchlist Settings
                else if (Regex.Match(strVariable, @"Send Watchlist Announcements to Discord WebHook").Success)
                {
                    Boolean UseDiscordForWatchlist = Boolean.Parse(strValue);
                    if (UseDiscordForWatchlist != _UseDiscordForWatchlist)
                    {
                        _UseDiscordForWatchlist = UseDiscordForWatchlist;
                        QueueSettingForUpload(new CPluginVariable(@"Send Watchlist Announcements to Discord WebHook", typeof(Boolean), _UseDiscordForWatchlist));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Watchlist WebHook URL").Success)
                {
                    _DiscordManager.WatchlistWebhookUrl = strValue;
                    if (_UseDiscordForWatchlist && _firstPlayerListComplete && String.IsNullOrEmpty(_shortServerName))
                    {
                        Log.Warn("The 'Short Server Name' setting must be filled in before posting discord announcements.");
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Discord Watchlist WebHook URL", typeof(String), _DiscordManager.WatchlistWebhookUrl));
                }
                else if (Regex.Match(strVariable, @"Announce Watchlist Leaves on Discord").Success)
                {
                    Boolean DiscordWatchlistLeftEnabled = Boolean.Parse(strValue);
                    if (DiscordWatchlistLeftEnabled != _DiscordWatchlistLeftEnabled)
                    {
                        _DiscordWatchlistLeftEnabled = DiscordWatchlistLeftEnabled;
                        QueueSettingForUpload(new CPluginVariable(@"Announce Watchlist Leaves on Discord", typeof(Boolean), _DiscordWatchlistLeftEnabled));
                    }
                }
                else if (Regex.Match(strVariable, @"Discord Role IDs to Mention in Watchlist Announcements").Success)
                {
                    _DiscordManager.RoleIDsToMentionWatchlist = CPluginVariable.DecodeStringArray(strValue).ToList();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Discord Role IDs to Mention in Watchlist Announcements", typeof(String), strValue));
                }
                else if (Regex.Match(strVariable, @"Use Experimental Tools").Success)
                {
                    Boolean useEXP = Boolean.Parse(strValue);
                    if (useEXP != _UseExperimentalTools)
                    {
                        _UseExperimentalTools = useEXP;
                        if (_UseExperimentalTools)
                        {
                            if (_threadsReady)
                            {
                                Log.Warn("Using experimental tools. Take caution.");
                            }
                        }
                        else
                        {
                            Log.Info("Experimental tools disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Experimental Tools", typeof(Boolean), _UseExperimentalTools));
                    }
                }
                else if (Regex.Match(strVariable, @"Round Timer: Enable").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean useTimer = Boolean.Parse(strValue);
                    if (useTimer != _useRoundTimer)
                    {
                        _useRoundTimer = useTimer;
                        if (_useRoundTimer)
                        {
                            if (_threadsReady)
                            {
                                Log.Info("Round Timer activated, will enable on next round.");
                            }
                        }
                        else
                        {
                            Log.Info("Round Timer disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Round Timer: Enable", typeof(Boolean), _useRoundTimer));
                    }
                }
                else if (Regex.Match(strVariable, @"Round Timer: Round Duration Minutes").Success)
                {
                    Double duration;
                    if (!Double.TryParse(strValue, out duration))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_maxRoundTimeMinutes != duration)
                    {
                        if (duration <= 0)
                        {
                            duration = 30.0;
                        }
                        _maxRoundTimeMinutes = duration;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Round Timer: Round Duration Minutes", typeof(Double), _maxRoundTimeMinutes));
                    }
                }
                else if (Regex.Match(strVariable, @"Faction Randomizer: Enable").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("Faction Randomizer cannot be enabled on official servers.");
                        return;
                    }
                    Boolean useRandomizer = Boolean.Parse(strValue);
                    if (useRandomizer != _factionRandomizerEnable)
                    {
                        _factionRandomizerEnable = useRandomizer;
                        if (_factionRandomizerEnable)
                        {
                            if (_threadsReady)
                            {
                                Log.Info("Faction randomizer enabled, will activate on next round.");
                            }
                        }
                        else
                        {
                            Log.Info("Faction randomizer disabled.");
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Faction Randomizer: Enable", typeof(Boolean), _factionRandomizerEnable));
                    }
                }
                else if (Regex.Match(strVariable, @"Faction Randomizer: Restriction").Success)
                {
                    switch (strValue)
                    {
                        case "NoRestriction":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.NoRestriction;
                            break;
                        case "NeverSameFaction":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.NeverSameFaction;
                            break;
                        case "AlwaysSameFaction":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysSameFaction;
                            break;
                        case "AlwaysSwapUSvsRU":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysSwapUSvsRU;
                            break;
                        case "AlwaysSwapUSvsCN":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysSwapUSvsCN;
                            break;
                        case "AlwaysSwapRUvsCN":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysSwapRUvsCN;
                            break;
                        case "AlwaysBothUS":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysBothUS;
                            break;
                        case "AlwaysBothRU":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysBothRU;
                            break;
                        case "AlwaysBothCN":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysBothCN;
                            break;
                        case "AlwaysUSvsX":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysUSvsX;
                            break;
                        case "AlwaysRUvsX":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysRUvsX;
                            break;
                        case "AlwaysCNvsX":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.AlwaysCNvsX;
                            break;
                        case "NeverUSvsX":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.NeverUSvsX;
                            break;
                        case "NeverRUvsX":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.NeverRUvsX;
                            break;
                        case "NeverCNvsX":
                            _factionRandomizerRestriction = FactionRandomizerRestriction.NeverCNvsX;
                            break;
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Faction Randomizer: Restriction", typeof(String), _factionRandomizerRestriction.ToString()));
                }
                else if (Regex.Match(strVariable, @"Faction Randomizer: Allow Repeat Team Selections").Success)
                {
                    Boolean allowRepeatSelections = Boolean.Parse(strValue);
                    if (allowRepeatSelections != _factionRandomizerAllowRepeatSelection)
                    {
                        _factionRandomizerAllowRepeatSelection = allowRepeatSelections;
                        QueueSettingForUpload(new CPluginVariable(@"Faction Randomizer: Allow Repeat Team Selections", typeof(Boolean), _factionRandomizerAllowRepeatSelection));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Challenge System").Success)
                {
                    Boolean enabled = Boolean.Parse(strValue);
                    if (ChallengeManager != null &&
                        enabled != ChallengeManager.Enabled)
                    {
                        ChallengeManager.SetEnabled(enabled);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Challenge System", typeof(Boolean), ChallengeManager.Enabled));
                    }
                }
                else if (Regex.Match(strVariable, @"Run Round Challenge ID").Success)
                {
                    Int32 RuleID = Int32.Parse(strValue);
                    if (ChallengeManager != null)
                    {
                        if (!ChallengeManager.Enabled)
                        {
                            Log.Error("Unable to run challenge rule. Challenge system not enabled.");
                            return;
                        }
                        Log.Info("Attempting to run challenge rule " + RuleID);
                        ChallengeManager.RunRoundChallenge(RuleID);
                    }
                }
                else if (Regex.Match(strVariable, @"Challenge System Auto-Assign Round rules").Success)
                {
                    Boolean autoAssign = Boolean.Parse(strValue);
                    if (ChallengeManager != null &&
                        autoAssign != ChallengeManager.AutoPlay)
                    {
                        ChallengeManager.AutoPlay = autoAssign;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Challenge System Auto-Assign Round rules", typeof(Boolean), ChallengeManager.AutoPlay));
                    }
                }
                else if (Regex.Match(strVariable, @"Challenge System Minimum Players").Success)
                {
                    Int32 minPlayers = Int32.Parse(strValue);
                    if (ChallengeManager != null &&
                        minPlayers != ChallengeManager.MinimumPlayers)
                    {
                        ChallengeManager.MinimumPlayers = minPlayers;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Challenge System Minimum Players", typeof(Int32), ChallengeManager.MinimumPlayers));
                    }
                }
                else if (Regex.Match(strVariable, @"Challenge Command Lock Timeout Hours").Success)
                {
                    Int32 commandLockTimeout = Int32.Parse(strValue);
                    if (ChallengeManager != null &&
                        commandLockTimeout != ChallengeManager.CommandLockTimeoutHours)
                    {
                        if (commandLockTimeout < 12)
                        {
                            commandLockTimeout = 12;
                        }
                        ChallengeManager.CommandLockTimeoutHours = commandLockTimeout;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Challenge Command Lock Timeout Hours", typeof(Int32), ChallengeManager.CommandLockTimeoutHours));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Server-Wide Round Rules").Success)
                {
                    Boolean useRoundRules = Boolean.Parse(strValue);
                    if (ChallengeManager != null &&
                        useRoundRules != ChallengeManager.EnableServerRoundRules)
                    {
                        ChallengeManager.EnableServerRoundRules = useRoundRules;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Server-Wide Round Rules", typeof(Boolean), ChallengeManager.EnableServerRoundRules));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Different Round Rule For Each Player").Success)
                {
                    Boolean useRandomRules = Boolean.Parse(strValue);
                    if (ChallengeManager != null &&
                        useRandomRules != ChallengeManager.RandomPlayerRoundRules)
                    {
                        ChallengeManager.RandomPlayerRoundRules = useRandomRules;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Different Round Rule For Each Player", typeof(Boolean), ChallengeManager.RandomPlayerRoundRules));
                    }
                }
                else if (Regex.Match(strVariable, @"Use NO EXPLOSIVES Limiter").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean useLimiter = Boolean.Parse(strValue);
                    if (useLimiter != _UseWeaponLimiter)
                    {
                        _UseWeaponLimiter = useLimiter;
                        if (_threadsReady)
                        {
                            if (_UseWeaponLimiter)
                            {
                                Log.Info("NO EXPLOSIVES punish limit activated.");
                            }
                            else
                            {
                                Log.Info("NO EXPLOSIVES punish limit disabled.");
                            }
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof(Boolean), _UseWeaponLimiter));
                    }
                }
                else if (Regex.Match(strVariable, @"NO EXPLOSIVES Weapon String").Success)
                {
                    if (_WeaponLimiterString != strValue)
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            _WeaponLimiterString = strValue;
                        }
                        else
                        {
                            Log.Error("Weapon String cannot be empty.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Weapon String", typeof(String), _WeaponLimiterString));
                    }
                }
                else if (Regex.Match(strVariable, @"NO EXPLOSIVES Exception String").Success)
                {
                    if (_WeaponLimiterExceptionString != strValue)
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            _WeaponLimiterExceptionString = strValue;
                        }
                        else
                        {
                            Log.Error("Weapon exception String cannot be empty.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Exception String", typeof(String), _WeaponLimiterExceptionString));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Base Server Name").Success)
                {
                    if (_eventBaseServerName != strValue)
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            _eventBaseServerName = strValue;
                        }
                        else
                        {
                            Log.Error("Server name selection cannot be empty.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Base Server Name", typeof(String), _eventBaseServerName));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Countdown Server Name").Success)
                {
                    if (_eventCountdownServerName != strValue)
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            _eventCountdownServerName = strValue;
                        }
                        else
                        {
                            Log.Error("Server name selection cannot be empty.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Countdown Server Name", typeof(String), _eventCountdownServerName));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Concrete Countdown Server Name").Success)
                {
                    if (_eventConcreteCountdownServerName != strValue)
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            _eventConcreteCountdownServerName = strValue;
                        }
                        else
                        {
                            Log.Error("Server name selection cannot be empty.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Concrete Countdown Server Name", typeof(String), _eventConcreteCountdownServerName));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Active Server Name").Success)
                {
                    if (_eventActiveServerName != strValue)
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            _eventActiveServerName = strValue;
                        }
                        else
                        {
                            Log.Error("Server name selection cannot be empty.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Active Server Name", typeof(String), _eventActiveServerName));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Grenade Cook Catcher").Success)
                {
                    Boolean useCookCatcher = Boolean.Parse(strValue);
                    if (useCookCatcher != _UseGrenadeCookCatcher)
                    {
                        _UseGrenadeCookCatcher = useCookCatcher;
                        if (_UseGrenadeCookCatcher)
                        {
                            if (_threadsReady)
                            {
                                Log.Info("Grenade Cook Catcher activated.");
                            }
                        }
                        else
                        {
                            Log.Info("Grenade Cook Catcher disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Grenade Cook Catcher", typeof(Boolean), _UseGrenadeCookCatcher));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatically Poll Server For Event Options").Success)
                {
                    Boolean eventPollAutomatic = Boolean.Parse(strValue);
                    if (eventPollAutomatic != _EventPollAutomatic)
                    {
                        _EventPollAutomatic = eventPollAutomatic;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatically Poll Server For Event Options", typeof(Boolean), _EventPollAutomatic));
                    }
                }
                else if (Regex.Match(strVariable, @"Max Automatic Polls Per Event").Success)
                {
                    Int32 EventRoundAutoPollsMax = Int32.Parse(strValue);
                    if (EventRoundAutoPollsMax != _EventRoundAutoPollsMax)
                    {
                        if (EventRoundAutoPollsMax < 1)
                        {
                            EventRoundAutoPollsMax = 1;
                        }
                        if (EventRoundAutoPollsMax > 20)
                        {
                            EventRoundAutoPollsMax = 20;
                        }
                        _EventRoundAutoPollsMax = EventRoundAutoPollsMax;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Max Automatic Polls Per Event", typeof(Double), _EventRoundAutoPollsMax));
                    }
                }
                else if (Regex.Match(strVariable, @"Yell Current Winning Rule Option").Success)
                {
                    Boolean PollPrintWinning = Boolean.Parse(strValue);
                    if (PollPrintWinning != _eventPollYellWinningRule)
                    {
                        _eventPollYellWinningRule = PollPrintWinning;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Yell Current Winning Rule Option", typeof(Boolean), _eventPollYellWinningRule));
                    }
                }
                else if (strVariable == "Weekly Events")
                {
                    Boolean EventWeeklyRepeat = Boolean.Parse(strValue);
                    if (EventWeeklyRepeat != _EventWeeklyRepeat)
                    {
                        _EventWeeklyRepeat = EventWeeklyRepeat;
                        // Do not recalculate the event round number from downloaded settings
                        // Only recalculate it from user input
                        if (_firstUserListComplete)
                        {
                            _CurrentEventRoundNumber = 999999;
                            QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                            if (_EventWeeklyRepeat)
                            {
                                _EventDate = GetNextWeekday(DateTime.Now.Date, _EventWeeklyDay);
                                if (GetEventRoundDateTime() < DateTime.Now)
                                {
                                    // If the given event date is today, but is already in the past
                                    // reset it to the same day next week
                                    _EventDate = _EventDate.AddDays(7);
                                }
                                QueueSettingForUpload(new CPluginVariable(@"Event Date", typeof(String), _EventDate.ToShortDateString()));
                            }
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Weekly Events", typeof(Boolean), _EventWeeklyRepeat));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Day").Success)
                {
                    //Check for valid value
                    DayOfWeek update;
                    switch (strValue)
                    {
                        case "Sunday":
                            update = DayOfWeek.Sunday;
                            break;
                        case "Monday":
                            update = DayOfWeek.Monday;
                            break;
                        case "Tuesday":
                            update = DayOfWeek.Tuesday;
                            break;
                        case "Wednesday":
                            update = DayOfWeek.Wednesday;
                            break;
                        case "Thursday":
                            update = DayOfWeek.Thursday;
                            break;
                        case "Friday":
                            update = DayOfWeek.Friday;
                            break;
                        case "Saturday":
                            update = DayOfWeek.Saturday;
                            break;
                        default:
                            Log.Error("Day of week setting " + strValue + " was invalid.");
                            return;
                    }
                    if (_EventWeeklyDay != update)
                    {
                        _EventWeeklyDay = update;
                        // Do not recalculate the event round number from downloaded settings
                        // Only recalculate it from user input
                        if (_firstUserListComplete)
                        {
                            _CurrentEventRoundNumber = 999999;
                            QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                        }
                        if (_EventWeeklyRepeat)
                        {
                            _EventDate = GetNextWeekday(DateTime.Now.Date, _EventWeeklyDay);
                            if (GetEventRoundDateTime() < DateTime.Now)
                            {
                                // If the given event date is today, but is already in the past
                                // reset it to the same day next week
                                _EventDate = _EventDate.AddDays(7);
                            }
                            QueueSettingForUpload(new CPluginVariable(@"Event Date", typeof(String), _EventDate.ToShortDateString()));
                        }
                        QueueSettingForUpload(new CPluginVariable(@"Event Day", typeof(String), _EventWeeklyDay.ToString()));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Date").Success)
                {
                    DateTime eventDate = DateTime.Parse(strValue);
                    if (eventDate.ToShortDateString() != _EventDate.ToShortDateString())
                    {
                        _EventDate = eventDate;
                        // Do not recalculate the event round number from downloaded settings
                        // Only recalculate it from user input
                        if (_firstUserListComplete)
                        {
                            _CurrentEventRoundNumber = 999999;
                            QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Date", typeof(String), _EventDate.ToShortDateString()));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Kick Players Who First Joined After This Date").Success)
                {
                    DateTime autoKickDate = DateTime.Parse(strValue);
                    if (autoKickDate.ToShortDateString() != _AutoKickNewPlayerDate.ToShortDateString())
                    {
                        _AutoKickNewPlayerDate = autoKickDate;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Kick Players Who First Joined After This Date", typeof(String), _AutoKickNewPlayerDate.ToShortDateString()));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Hour in 24 format").Success)
                {
                    Double eventHour = Double.Parse(strValue);
                    if (eventHour != _EventHour)
                    {
                        if (eventHour < 0)
                        {
                            eventHour = 0;
                        }
                        if (eventHour > 23.9)
                        {
                            eventHour = 23.9;
                        }
                        _EventHour = eventHour;
                        // Do not recalculate the event round number from downloaded settings
                        // Only recalculate it from user input
                        if (_firstUserListComplete)
                        {
                            _CurrentEventRoundNumber = 999999;
                            QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Hour in 24 format", typeof(Double), _EventHour));
                    }
                }
                else if (Regex.Match(strVariable, @"Poll Max Option Count").Success)
                {
                    Int32 EventPollMaxOptions = Int32.Parse(strValue);
                    if (EventPollMaxOptions != _EventPollMaxOptions)
                    {
                        if (EventPollMaxOptions < 1)
                        {
                            EventPollMaxOptions = 1;
                        }
                        if (EventPollMaxOptions > 5)
                        {
                            EventPollMaxOptions = 5;
                        }
                        _EventPollMaxOptions = EventPollMaxOptions;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Poll Max Option Count", typeof(Double), _EventPollMaxOptions));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Test Round Number").Success)
                {
                    Int32 roundNumber = Int32.Parse(strValue);
                    if (roundNumber != _EventTestRoundNumber)
                    {
                        if (roundNumber < 1)
                        {
                            roundNumber = 1;
                        }
                        _EventTestRoundNumber = roundNumber;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Test Round Number", typeof(Int32), _EventTestRoundNumber));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Current Round Number").Success)
                {
                    Int32 roundNumber = Int32.Parse(strValue);
                    if (roundNumber != _CurrentEventRoundNumber)
                    {
                        if (roundNumber < 1)
                        {
                            roundNumber = 1;
                        }
                        _CurrentEventRoundNumber = roundNumber;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Announce Day Difference").Success)
                {
                    Double dayDifference = Double.Parse(strValue);
                    if (dayDifference != _EventAnnounceDayDifference)
                    {
                        _EventAnnounceDayDifference = dayDifference;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Announce Day Difference", typeof(Double), _EventAnnounceDayDifference));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Duration Rounds").Success)
                {
                    Int32 roundCount = Int32.Parse(strValue);
                    if (roundCount != _EventRoundOptions.Count())
                    {
                        if (roundCount < 0)
                        {
                            roundCount = 0;
                        }
                        // Rebuild the round selection list
                        List<AEventOption> optionList = new List<AEventOption>();
                        for (int roundNumber = 0; roundNumber < roundCount; roundNumber++)
                        {
                            // If the round option already exists, save it
                            if (roundNumber < _EventRoundOptions.Count())
                            {
                                optionList.Add(_EventRoundOptions[roundNumber]);
                            }
                            else
                            {
                                AEventOption.MapCode chosenMap = AEventOption.MapCode.UNKNOWN;
                                AEventOption.ModeCode chosenMode = AEventOption.ModeCode.UNKNOWN;
                                AEventOption.RuleCode chosenRule = AEventOption.RuleCode.UNKNOWN;
                                Boolean chosen = false;
                                foreach (AEventOption.MapCode map in AEventOption.MapNames.Keys)
                                {
                                    foreach (AEventOption.ModeCode mode in AEventOption.ModeNames.Keys)
                                    {
                                        foreach (AEventOption.RuleCode rule in AEventOption.RuleNames.Keys.Where(rule => rule != AEventOption.RuleCode.ENDEVENT))
                                        {
                                            if (!optionList.Any(option => option.Map == map && option.Mode == mode && option.Rule == rule))
                                            {
                                                chosenMap = map;
                                                chosenMode = mode;
                                                chosenRule = rule;
                                                chosen = true;
                                                break;
                                            }
                                        }
                                        if (chosen)
                                        {
                                            break;
                                        }
                                    }
                                    if (chosen)
                                    {
                                        break;
                                    }
                                }
                                if (chosen)
                                {
                                    optionList.Add(new AEventOption()
                                    {
                                        Map = chosenMap,
                                        Mode = chosenMode,
                                        Rule = chosenRule
                                    });
                                }
                            }
                        }
                        _EventRoundOptions = optionList;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Round Codes", typeof(String[]), _EventRoundOptions.Select(round => round.getCode()).ToArray()));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Round \d+ Options").Success)
                {
                    var regex = new Regex("[0-9]+");
                    Int32 roundNumber = Int32.Parse(regex.Match(strVariable).Value) - 1;
                    if (strValue == "Remove")
                    {
                        _EventRoundOptions.RemoveAt(roundNumber);
                    }
                    else
                    {
                        var newOption = AEventOption.FromDisplay(strValue);
                        if (_EventRoundOptions.Any(option => option.Map == newOption.Map &&
                                                             option.Mode == newOption.Mode &&
                                                             option.Rule == newOption.Rule))
                        {
                            Log.Error("Event round option " + newOption.getDisplay() + " already exists.");
                            return;
                        }
                        _EventRoundOptions[roundNumber] = newOption;
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Event Round Codes", typeof(String[]), _EventRoundOptions.Select(round => round.getCode()).ToArray()));
                }
                else if (Regex.Match(strVariable, @"Event Round Codes").Success)
                {
                    if (strValue.Trim().Length > 0)
                    {
                        _EventRoundOptions = CPluginVariable.DecodeStringArray(strValue).Select(option => AEventOption.FromCode(option)).ToList();
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Event Round Codes", typeof(String[]), _EventRoundOptions.Select(round => round.getCode()).ToArray()));
                }
                else if (Regex.Match(strVariable, @"Poll Mode Rule Combination Count").Success)
                {
                    Int32 optionCount = Int32.Parse(strValue);
                    if (optionCount != _EventRoundPollOptions.Count())
                    {
                        if (optionCount < 0)
                        {
                            optionCount = 0;
                        }
                        // Rebuild the option selection list
                        List<AEventOption> optionList = new List<AEventOption>();
                        for (int optionNumber = 0; optionNumber < optionCount; optionNumber++)
                        {
                            // If the option already exists, save it
                            if (optionNumber < _EventRoundPollOptions.Count())
                            {
                                optionList.Add(_EventRoundPollOptions[optionNumber]);
                            }
                            else
                            {
                                AEventOption.MapCode chosenMap = AEventOption.MapCode.UNKNOWN;
                                AEventOption.ModeCode chosenMode = AEventOption.ModeCode.UNKNOWN;
                                AEventOption.RuleCode chosenRule = AEventOption.RuleCode.UNKNOWN;
                                Boolean chosen = false;
                                foreach (AEventOption.MapCode mapCode in AEventOption.MapNames.Keys)
                                {
                                    foreach (AEventOption.ModeCode modeCode in AEventOption.ModeNames.Keys)
                                    {
                                        foreach (AEventOption.RuleCode ruleCode in AEventOption.RuleNames.Keys.Where(rule => rule != AEventOption.RuleCode.ENDEVENT))
                                        {
                                            if (!optionList.Any(option => option.Map == mapCode &&
                                                                          option.Mode == modeCode &&
                                                                          option.Rule == ruleCode))
                                            {
                                                chosenMap = mapCode;
                                                chosenMode = modeCode;
                                                chosenRule = ruleCode;
                                                chosen = true;
                                                break;
                                            }
                                        }
                                        if (chosen)
                                        {
                                            break;
                                        }
                                    }
                                    if (chosen)
                                    {
                                        break;
                                    }
                                }
                                if (chosen)
                                {
                                    optionList.Add(new AEventOption()
                                    {
                                        Map = chosenMap,
                                        Mode = chosenMode,
                                        Rule = chosenRule
                                    });
                                }
                            }
                        }
                        _EventRoundPollOptions = optionList;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Event Round Poll Codes", typeof(String[]), _EventRoundPollOptions.Select(option => option.getCode()).ToArray()));
                    }
                }
                else if (Regex.Match(strVariable, @"Event Poll Option \d+").Success)
                {
                    var regex = new Regex("[0-9]+");
                    Int32 optionNumber = Int32.Parse(regex.Match(strVariable).Value) - 1;
                    if (strValue == "Remove")
                    {
                        _EventRoundPollOptions.RemoveAt(optionNumber);
                    }
                    else
                    {
                        var newOption = AEventOption.FromDisplay(strValue);
                        if (_EventRoundPollOptions.Any(option => option.Map == newOption.Map &&
                                                                 option.Mode == newOption.Mode &&
                                                                 option.Rule == newOption.Rule))
                        {
                            Log.Error("Event poll option " + newOption.getDisplay() + " already exists.");
                            return;
                        }
                        _EventRoundPollOptions[optionNumber] = newOption;
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Event Round Poll Codes", typeof(String[]), _EventRoundPollOptions.Select(option => option.getCode()).ToArray()));
                }
                else if (Regex.Match(strVariable, @"Event Round Poll Codes").Success)
                {
                    if (strValue.Trim().Length > 0)
                    {
                        _EventRoundPollOptions = CPluginVariable.DecodeStringArray(strValue).Select(option => AEventOption.FromCode(option)).ToList();
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Event Round Poll Codes", typeof(String[]), _EventRoundPollOptions.Select(option => option.getCode()).ToArray()));
                }
                else if (Regex.Match(strVariable, @"Use LIVE Anti Cheat System").Success)
                {
                    Boolean useLIVESystem = Boolean.Parse(strValue);
                    if (useLIVESystem != _useAntiCheatLIVESystem)
                    {
                        _useAntiCheatLIVESystem = useLIVESystem;
                        if (_threadsReady)
                        {
                            if (_useAntiCheatLIVESystem)
                            {
                                Log.Info("AntiCheat now using the LIVE System.");
                            }
                            else
                            {
                                Log.Info("AntiCheat LIVE system disabled. This should ONLY be disabled if you are seeing 'Issue connecting to Battlelog' warnings.");
                            }
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use LIVE Anti Cheat System", typeof(Boolean), _useAntiCheatLIVESystem));
                    }
                }
                else if (Regex.Match(strVariable, @"LIVE System Includes Mass Murder and Aimbot Checks").Success)
                {
                    Boolean AntiCheatLIVESystemActiveStats = Boolean.Parse(strValue);
                    if (AntiCheatLIVESystemActiveStats != _AntiCheatLIVESystemActiveStats)
                    {
                        _AntiCheatLIVESystemActiveStats = AntiCheatLIVESystemActiveStats;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"LIVE System Includes Mass Murder and Aimbot Checks", typeof(Boolean), _AntiCheatLIVESystemActiveStats));
                    }
                }
                else if (Regex.Match(strVariable, @"DPS Checker: Ban Message").Success)
                {
                    if (_AntiCheatDPSBanMessage != strValue)
                    {
                        _AntiCheatDPSBanMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"DPS Checker: Ban Message", typeof(String), _AntiCheatDPSBanMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"HSK Checker: Enable").Success)
                {
                    Boolean useAimbotChecker = Boolean.Parse(strValue);
                    if (useAimbotChecker != _UseHskChecker)
                    {
                        _UseHskChecker = useAimbotChecker;
                        if (_UseHskChecker)
                        {
                            if (_threadsReady)
                            {
                                Log.Info("Aimbot Checker activated.");
                            }
                        }
                        else
                        {
                            Log.Info("Aimbot Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HSK Checker: Enable", typeof(Boolean), _UseHskChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HSK Checker: Trigger Level").Success)
                {
                    Double triggerLevel;
                    if (!Double.TryParse(strValue, out triggerLevel))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_HskTriggerLevel != triggerLevel)
                    {
                        if (triggerLevel < 45)
                        {
                            triggerLevel = 45;
                        }
                        else if (triggerLevel > 100)
                        {
                            triggerLevel = 100;
                        }
                        _HskTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HSK Checker: Trigger Level", typeof(Double), _HskTriggerLevel));
                    }
                }
                else if (Regex.Match(strVariable, @"HSK Checker: Ban Message").Success)
                {
                    if (_AntiCheatHSKBanMessage != strValue)
                    {
                        _AntiCheatHSKBanMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HSK Checker: Ban Message", typeof(String), _AntiCheatHSKBanMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"KPM Checker: Enable").Success)
                {
                    Boolean useKPMChecker = Boolean.Parse(strValue);
                    if (useKPMChecker != _UseKpmChecker)
                    {
                        _UseKpmChecker = useKPMChecker;
                        if (_UseKpmChecker)
                        {
                            if (_threadsReady)
                            {
                                Log.Info("KPM Checker activated.");
                            }
                        }
                        else
                        {
                            Log.Info("KPM Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"KPM Checker: Enable", typeof(Boolean), _UseKpmChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"KPM Checker: Trigger Level").Success)
                {
                    Double triggerLevel;
                    if (!Double.TryParse(strValue, out triggerLevel))
                    {
                        Log.HandleException(new AException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_KpmTriggerLevel != triggerLevel)
                    {
                        if (triggerLevel < 4)
                        {
                            triggerLevel = 4;
                        }
                        else if (triggerLevel > 10)
                        {
                            triggerLevel = 10;
                        }
                        _KpmTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"KPM Checker: Trigger Level", typeof(Double), _KpmTriggerLevel));
                    }
                }
                else if (Regex.Match(strVariable, @"KPM Checker: Ban Message").Success)
                {
                    if (_AntiCheatKPMBanMessage != strValue)
                    {
                        _AntiCheatKPMBanMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"KPM Checker: Ban Message", typeof(String), _AntiCheatKPMBanMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Fetch Actions from Database").Success)
                {
                    Boolean fetch = Boolean.Parse(strValue);
                    if (fetch != _fetchActionsFromDb)
                    {
                        _fetchActionsFromDb = fetch;
                        _DbCommunicationWaitHandle.Set();
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Fetch Actions from Database", typeof(Boolean), _fetchActionsFromDb));
                    }
                }
                else if (Regex.Match(strVariable, @"AdkatsLRT Extension Token").Success)
                {
                    if (_AdKatsLRTExtensionToken != strValue)
                    {
                        _AdKatsLRTExtensionToken = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"AdkatsLRT Extension Token", typeof(String), _AdKatsLRTExtensionToken));
                        CheckForPluginUpdates(true);
                    }
                }
                else if (Regex.Match(strVariable, @"Use Additional Ban Message").Success)
                {
                    Boolean use = Boolean.Parse(strValue);
                    if (_UseBanAppend != use)
                    {
                        _UseBanAppend = use;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Additional Ban Message", typeof(Boolean), _UseBanAppend));
                    }
                }
                else if (Regex.Match(strVariable, @"Additional Ban Message").Success)
                {
                    if (strValue.Length > 30)
                    {
                        strValue = strValue.Substring(0, 30);
                        Log.Error("Ban append cannot be more than 30 characters.");
                    }
                    if (_BanAppend != strValue)
                    {
                        _BanAppend = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Additional Ban Message", typeof(String), _BanAppend));
                    }
                }
                else if (Regex.Match(strVariable, @"Procon Ban Admin Name").Success)
                {
                    if (strValue.Length > 16)
                    {
                        strValue = strValue.Substring(0, 16);
                        Log.Error("Procon ban admin id cannot be more than 16 characters.");
                    }
                    if (_CBanAdminName != strValue)
                    {
                        _CBanAdminName = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Procon Ban Admin Name", typeof(String), _CBanAdminName));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Ban Enforcer").Success)
                {
                    if (_serverInfo.ServerType == "OFFICIAL" && Boolean.Parse(strValue) == true)
                    {
                        strValue = "False";
                        Log.Error("'" + strVariable + "' cannot be enabled on official servers.");
                        return;
                    }
                    Boolean use = Boolean.Parse(strValue);
                    if (_UseBanEnforcer != use)
                    {
                        _UseBanEnforcer = use;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Ban Enforcer", typeof(Boolean), _UseBanEnforcer));
                        if (_UseBanEnforcer)
                        {
                            _fetchActionsFromDb = true;
                            _DbCommunicationWaitHandle.Set();
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Ban Enforcer BF4 Lenient Kick").Success)
                {
                    Boolean lenientKick = Boolean.Parse(strValue);
                    if (_BanEnforcerBF4LenientKick != lenientKick)
                    {
                        _BanEnforcerBF4LenientKick = lenientKick;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ban Enforcer BF4 Lenient Kick", typeof(Boolean), _BanEnforcerBF4LenientKick));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by NAME").Success)
                {
                    Boolean enforceName = Boolean.Parse(strValue);
                    if (_DefaultEnforceName != enforceName)
                    {
                        _DefaultEnforceName = enforceName;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by NAME", typeof(Boolean), _DefaultEnforceName));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by GUID").Success)
                {
                    Boolean enforceGUID = Boolean.Parse(strValue);
                    if (_DefaultEnforceGUID != enforceGUID)
                    {
                        _DefaultEnforceGUID = enforceGUID;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by GUID", typeof(Boolean), _DefaultEnforceGUID));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by IP").Success)
                {
                    Boolean enforceIP = Boolean.Parse(strValue);
                    if (_DefaultEnforceIP != enforceIP)
                    {
                        _DefaultEnforceIP = enforceIP;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by IP", typeof(Boolean), _DefaultEnforceIP));
                    }
                }
                else if (Regex.Match(strVariable, @"Ban Search").Success)
                {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 3)
                    {
                        Log.Error("Search query must be 3 or more characters.");
                        return;
                    }
                    lock (_BanEnforcerSearchResults)
                    {
                        _BanEnforcerSearchResults = FetchMatchingBans(strValue, 5);
                        if (_BanEnforcerSearchResults.Count == 0)
                        {
                            Log.Error("No players matching '" + strValue + "' have active bans.");
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Countdown Duration before a Nuke is fired").Success)
                {
                    Int32 duration = Int32.Parse(strValue);
                    if (_NukeCountdownDurationSeconds != duration)
                    {
                        _NukeCountdownDurationSeconds = duration;
                        if (_NukeCountdownDurationSeconds < 0)
                        {
                            _NukeCountdownDurationSeconds = 0;
                        }
                        if (_NukeCountdownDurationSeconds > 30)
                        {
                            _NukeCountdownDurationSeconds = 30;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Countdown Duration before a Nuke is fired", typeof(Int32), _NukeCountdownDurationSeconds));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Required Reason Length").Success)
                {
                    Int32 required = Int32.Parse(strValue);
                    if (_RequiredReasonLength != required)
                    {
                        _RequiredReasonLength = required;
                        if (_RequiredReasonLength < 1)
                        {
                            _RequiredReasonLength = 1;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Required Reason Length", typeof(Int32), _RequiredReasonLength));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Report Handle Seconds").Success)
                {
                    Int32 minimumReportHandleSeconds = Int32.Parse(strValue);
                    if (_MinimumReportHandleSeconds != minimumReportHandleSeconds)
                    {
                        _MinimumReportHandleSeconds = minimumReportHandleSeconds;
                        if (_MinimumReportHandleSeconds < 0)
                        {
                            _MinimumReportHandleSeconds = 0;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Report Handle Seconds", typeof(Int32), _MinimumReportHandleSeconds));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Minutes Into Round To Use Assist").Success)
                {
                    Int32 minimumAssistMinutes = Int32.Parse(strValue);
                    if (_minimumAssistMinutes != minimumAssistMinutes)
                    {
                        _minimumAssistMinutes = minimumAssistMinutes;
                        if (_minimumAssistMinutes < 0)
                        {
                            _minimumAssistMinutes = 0;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Minutes Into Round To Use Assist", typeof(Int32), _minimumAssistMinutes));
                    }
                }
                else if (Regex.Match(strVariable, @"Allow Commands from Admin Say").Success)
                {
                    Boolean allowSayCommands = Boolean.Parse(strValue);
                    if (_AllowAdminSayCommands != allowSayCommands)
                    {
                        _AllowAdminSayCommands = allowSayCommands;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Allow Commands from Admin Say", typeof(Boolean), _AllowAdminSayCommands));
                    }
                }
                else if (Regex.Match(strVariable, @"Reserved slot grants access to squad lead command").Success)
                {
                    Boolean reservedSquadLead = Boolean.Parse(strValue);
                    if (_ReservedSquadLead != reservedSquadLead)
                    {
                        _ReservedSquadLead = reservedSquadLead;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Reserved slot grants access to squad lead command", typeof(Boolean), _ReservedSquadLead));
                    }
                }
                else if (Regex.Match(strVariable, @"Reserved slot grants access to self-move command").Success)
                {
                    Boolean reservedSelfMove = Boolean.Parse(strValue);
                    if (_ReservedSelfMove != reservedSelfMove)
                    {
                        _ReservedSelfMove = reservedSelfMove;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Reserved slot grants access to self-move command", typeof(Boolean), _ReservedSelfMove));
                    }
                }
                else if (Regex.Match(strVariable, @"Reserved slot grants access to self-kill command").Success)
                {
                    Boolean reservedSelfKill = Boolean.Parse(strValue);
                    if (_ReservedSelfKill != reservedSelfKill)
                    {
                        _ReservedSelfKill = reservedSelfKill;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Reserved slot grants access to self-kill command", typeof(Boolean), _ReservedSelfKill));
                    }
                }
                else if (Regex.Match(strVariable, @"Bypass all command confirmation -DO NOT USE-").Success)
                {
                    Boolean bypassAllConfirmation = Boolean.Parse(strValue);
                    if (_bypassCommandConfirmation != bypassAllConfirmation)
                    {
                        _bypassCommandConfirmation = bypassAllConfirmation;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Bypass all command confirmation -DO NOT USE-", typeof(Boolean), _bypassCommandConfirmation));
                    }
                }
                else if (Regex.Match(strVariable, @"External plugin player commands").Success)
                {
                    _ExternalPlayerCommands = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"External plugin player commands", typeof(String), CPluginVariable.EncodeStringArray(_ExternalPlayerCommands.ToArray())));
                }
                else if (Regex.Match(strVariable, @"External plugin admin commands").Success)
                {
                    _ExternalAdminCommands = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"External plugin admin commands", typeof(String), CPluginVariable.EncodeStringArray(_ExternalAdminCommands.ToArray())));
                }
                else if (Regex.Match(strVariable, @"Command Target Whitelist Commands").Success)
                {
                    _CommandTargetWhitelistCommands = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    if (_firstUserListComplete)
                    {
                        foreach (string commandText in _CommandTargetWhitelistCommands.ToList())
                        {
                            if (!_CommandTextDictionary.ContainsKey(commandText))
                            {
                                Log.Error("Command " + commandText + " not found, removing from command target whitelist commands.");
                                _CommandTargetWhitelistCommands.Remove(commandText);
                            }
                        }
                    }
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Command Target Whitelist Commands", typeof(String), CPluginVariable.EncodeStringArray(_CommandTargetWhitelistCommands.ToArray())));
                }
                else if (strVariable.StartsWith("USR"))
                {
                    //USR1 | ColColonCleaner | User Email
                    //USR1 | ColColonCleaner | User Phone
                    //USR1 | ColColonCleaner | User Role
                    //USR1 | ColColonCleaner | Delete User?
                    //USR1 | ColColonCleaner | Add Soldier?
                    //USR1 | ColColonCleaner | Soldiers | 293492 | ColColonCleaner | Delete Soldier?

                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String user_id_str = commandSplit[0].TrimStart("USR".ToCharArray()).Trim();
                    Int32 user_id = Int32.Parse(user_id_str);
                    String section = commandSplit[2].Trim();

                    AUser aUser = null;
                    if (_userCache.TryGetValue(user_id, out aUser))
                    {
                        switch (section)
                        {
                            case "User Email":
                                if (String.IsNullOrEmpty(strValue) || Regex.IsMatch(strValue, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$"))
                                {
                                    aUser.user_email = strValue;
                                    //Reupload the user
                                    QueueUserForUpload(aUser);
                                }
                                else
                                {
                                    Log.Error(strValue + " is an invalid email address.");
                                    return;
                                }
                                break;
                            case "User Expiration":
                                DateTime newExpiration;
                                if (DateTime.TryParse(strValue, out newExpiration))
                                {
                                    aUser.user_expiration = newExpiration;
                                    //Reupload the user
                                    QueueUserForUpload(aUser);
                                }
                                else
                                {
                                    Log.Error(strValue + " is an invalid date.");
                                }
                                break;
                            case "User Notes":
                                if (String.IsNullOrEmpty(strValue))
                                {
                                    Log.Error("User notes cannot be blank.");
                                    return;
                                }
                                aUser.user_notes = strValue;
                                //Reupload the user
                                QueueUserForUpload(aUser);
                                break;
                            case "User Phone":
                                aUser.user_phone = strValue;
                                //Reupload the user
                                QueueUserForUpload(aUser);
                                break;
                            case "User Role":
                                ARole aRole = null;
                                if (_RoleNameDictionary.TryGetValue(strValue, out aRole))
                                {
                                    aUser.user_role = aRole;
                                }
                                else
                                {
                                    Log.Error("Role " + strValue + " not found.");
                                    return;
                                }
                                //Reupload the user
                                QueueUserForUpload(aUser);
                                break;
                            case "Delete User?":
                                if (strValue.ToLower() == "delete")
                                {
                                    QueueUserForRemoval(aUser);
                                }
                                break;
                            case "Add Soldier?":
                                Thread addSoldierThread = new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "AddSoldier";
                                    Log.Debug(() => "Starting a user change thread.", 2);
                                    TryAddUserSoldier(aUser, strValue);
                                    QueueUserForUpload(aUser);
                                    Log.Debug(() => "Exiting a user change thread.", 2);
                                    Threading.StopWatchdog();
                                }));
                                Threading.StartWatchdog(addSoldierThread);
                                break;
                            case "Soldiers":
                                if (strVariable.Contains("Delete Soldier?") && strValue.ToLower() == "delete")
                                {
                                    String player_id_str = commandSplit[3].Trim();
                                    Int64 player_id = Int64.Parse(player_id_str);
                                    aUser.soldierDictionary.Remove(player_id);
                                    //Reupload the user
                                    QueueUserForUpload(aUser);
                                }
                                break;
                            default:
                                Log.Error("Section " + section + " not found.");
                                break;
                        }
                    }
                }
                else if (strVariable.StartsWith("CDE"))
                {
                    //Trim off all but the command ID and section
                    //5. Command List|CDE1 | Kill Player | Active
                    //5. Command List|CDE1 | Kill Player | Logging
                    //5. Command List|CDE1 | Kill Player | Text

                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String command_id_str = commandSplit[0].TrimStart("CDE".ToCharArray()).Trim();
                    Int32 command_id = Int32.Parse(command_id_str);
                    String section = commandSplit[2].Trim();

                    ACommand command = null;
                    if (_CommandIDDictionary.TryGetValue(command_id, out command))
                    {
                        if (section == "Active")
                        {
                            //Check for valid value
                            if (strValue == "Active")
                            {
                                _RoleCommandCacheUpdateBufferStart = UtcNow();
                                command.command_active = ACommand.CommandActive.Active;
                            }
                            else if (strValue == "Disabled")
                            {
                                _RoleCommandCacheUpdateBufferStart = UtcNow();
                                command.command_active = ACommand.CommandActive.Disabled;
                            }
                            else if (strValue == "Invisible")
                            {
                                command.command_active = ACommand.CommandActive.Invisible;
                            }
                            else
                            {
                                Log.Error("Activity setting " + strValue + " was invalid.");
                                return;
                            }
                            switch (command.command_key)
                            {
                                case "command_confirm":
                                    if (command.command_active != ACommand.CommandActive.Active)
                                    {
                                        Log.Warn("Confirm command must be active. Resetting.");
                                        command.command_active = ACommand.CommandActive.Active;
                                    }
                                    break;
                                case "command_cancel":
                                    if (command.command_active != ACommand.CommandActive.Active)
                                    {
                                        Log.Warn("Cancel command must be active. Resetting.");
                                        command.command_active = ACommand.CommandActive.Active;
                                    }
                                    break;
                            }
                        }
                        else if (section == "Logging")
                        {
                            //Check for valid value
                            switch (strValue)
                            {
                                case "Log":
                                    command.command_logging = ACommand.CommandLogging.Log;
                                    break;
                                case "Mandatory":
                                    command.command_logging = ACommand.CommandLogging.Mandatory;
                                    break;
                                case "Ignore":
                                    command.command_logging = ACommand.CommandLogging.Ignore;
                                    break;
                                case "Unable":
                                    command.command_logging = ACommand.CommandLogging.Unable;
                                    break;
                                default:
                                    Log.Error("Logging setting " + strValue + " was invalid.");
                                    return;
                            }
                        }
                        else if (section == "Text")
                        {
                            if (String.IsNullOrEmpty(strValue))
                            {
                                Log.Error("Command text cannot be blank.");
                                return;
                            }
                            //Make sure command text only contains alphanumeric chars, underscores, and dashes
                            Regex rgx = new Regex("[^a-zA-Z0-9_-]");
                            strValue = rgx.Replace(strValue, "").ToLower();
                            //Check to make sure text is not a duplicate
                            foreach (ACommand testCommand in _CommandNameDictionary.Values.ToList())
                            {
                                if (testCommand.command_text == strValue)
                                {
                                    Log.Error("Command text cannot be the same as another command.");
                                    return;
                                }
                            }
                            switch (command.command_key)
                            {
                                case "command_confirm":
                                    if (strValue != "yes")
                                    {
                                        Log.Warn("Confirm command text must be 'yes'. Resetting.");
                                        strValue = "yes";
                                    }
                                    break;
                                case "command_cancel":
                                    if (strValue != "no")
                                    {
                                        Log.Warn("Cancel command text must be 'no'. Resetting.");
                                        strValue = "no";
                                    }
                                    break;
                            }
                            //Assign the command text
                            lock (_CommandIDDictionary)
                            {
                                _CommandTextDictionary.Remove(command.command_text);
                                command.command_text = strValue;
                                _CommandTextDictionary.Add(command.command_text, command);
                            }
                        }
                        else if (section == "Access Method")
                        {
                            if (String.IsNullOrEmpty(strValue))
                            {
                                Log.Error("Command access method cannot be blank.");
                                return;
                            }
                            command.command_access = (ACommand.CommandAccess)Enum.Parse(typeof(ACommand.CommandAccess), strValue);
                            switch (command.command_key)
                            {
                                case "command_confirm":
                                    if (command.command_access != ACommand.CommandAccess.Any)
                                    {
                                        Log.Warn("Confirm command access must be 'Any'. Resetting.");
                                        command.command_access = ACommand.CommandAccess.Any;
                                    }
                                    break;
                                case "command_cancel":
                                    if (command.command_access != ACommand.CommandAccess.Any)
                                    {
                                        Log.Warn("Confirm command access must be 'Any'. Resetting.");
                                        command.command_access = ACommand.CommandAccess.Any;
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            Log.Error("Section " + section + " not understood.");
                            return;
                        }
                        //Upload the command changes
                        QueueCommandForUpload(command);
                    }
                    else
                    {
                        Log.Error("Command " + command_id + " not found in command dictionary.");
                    }
                }
                else if (strVariable.StartsWith("RLE"))
                {
                    //Trim off all but the role ID and section
                    //RLE1 | Default Guest | CDE3 | Kill Player

                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String roleIDStr = commandSplit[0].TrimStart("RLE".ToCharArray()).Trim();
                    Int32 roleID = Int32.Parse(roleIDStr);

                    //If second section is a command prefix, this is the allow/deny clause
                    if (commandSplit[2].Trim().StartsWith("CDE"))
                    {
                        String commandIDStr = commandSplit[2].Trim().TrimStart("CDE".ToCharArray());
                        Int32 commandID = Int32.Parse(commandIDStr);

                        //Fetch needed role
                        ARole aRole = null;
                        if (_RoleIDDictionary.TryGetValue(roleID, out aRole))
                        {
                            //Fetch needed command
                            ACommand aCommand = null;
                            if (_CommandIDDictionary.TryGetValue(commandID, out aCommand))
                            {
                                switch (strValue.ToLower())
                                {
                                    case "allow":
                                        lock (aRole.RoleAllowedCommands)
                                        {
                                            if (!aRole.RoleAllowedCommands.ContainsKey(aCommand.command_key))
                                            {
                                                aRole.RoleAllowedCommands.Add(aCommand.command_key, aCommand);
                                            }
                                        }
                                        QueueRoleForUpload(aRole);
                                        break;
                                    case "deny":
                                        switch (aCommand.command_key)
                                        {
                                            case "command_confirm":
                                                Log.Error("Confirm command cannot be denied for any role. [M]");
                                                return;
                                            case "command_cancel":
                                                Log.Error("Cancel command cannot be denied for any role. [M]");
                                                return;
                                        }
                                        lock (aRole.RoleAllowedCommands)
                                        {
                                            aRole.RoleAllowedCommands.Remove(aCommand.command_key);
                                        }
                                        QueueRoleForUpload(aRole);
                                        break;
                                    default:
                                        Log.Error("Unknown setting when assigning command allowance.");
                                        return;
                                }
                            }
                            else
                            {
                                Log.Error("Command " + commandID + " not found in command dictionary.");
                            }
                        }
                        else
                        {
                            Log.Error("Role " + roleID + " not found in role dictionary.");
                        }
                    }
                    else if (commandSplit[2].Trim().StartsWith("GPE"))
                    {
                        String groupIDStr = commandSplit[2].Trim().TrimStart("GPE".ToCharArray());
                        Int32 groupID = Int32.Parse(groupIDStr);

                        //Fetch needed role
                        ARole aRole = null;
                        if (_RoleIDDictionary.TryGetValue(roleID, out aRole))
                        {
                            //Fetch needed group
                            ASpecialGroup aGroup = null;
                            if (_specialPlayerGroupIDDictionary.TryGetValue(groupID, out aGroup))
                            {
                                switch (strValue.ToLower())
                                {
                                    case "assign":
                                        lock (aRole.RoleSetGroups)
                                        {
                                            if (!aRole.RoleSetGroups.ContainsKey(aGroup.group_key))
                                            {
                                                aRole.RoleSetGroups.Add(aGroup.group_key, aGroup);
                                            }
                                        }
                                        QueueRoleForUpload(aRole);
                                        break;
                                    case "ignore":
                                        lock (aRole.RoleSetGroups)
                                        {
                                            aRole.RoleSetGroups.Remove(aGroup.group_key);
                                        }
                                        QueueRoleForUpload(aRole);
                                        break;
                                    case "required based on other settings":
                                    case "blocked based on other settings":
                                        return;
                                    default:
                                        Log.Error("Unknown setting when changing role group assignment.");
                                        return;
                                }
                            }
                            else
                            {
                                Log.Error("Group " + groupID + " not found in group dictionary.");
                            }
                        }
                        else
                        {
                            Log.Error("Role " + roleID + " not found in role dictionary.");
                        }
                    }
                    else if (commandSplit[2].Contains("Delete Role?") && strValue.ToLower() == "delete")
                    {
                        //Fetch needed role
                        ARole aRole = null;
                        if (_RoleIDDictionary.TryGetValue(roleID, out aRole))
                        {
                            _RoleCommandCacheUpdateBufferStart = UtcNow();
                            QueueRoleForRemoval(aRole);
                        }
                        else
                        {
                            Log.Error("Unable to fetch role " + roleID + " for deletion.");
                        }
                    }
                }
                else if (strVariable.StartsWith("BAN"))
                {
                    //Trim off all but the command ID and section
                    //BAN1 | ColColonCleaner | Some Reason

                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String banIDStr = commandSplit[0].TrimStart("BAN".ToCharArray()).Trim();
                    Int32 banID = Int32.Parse(banIDStr);

                    ABan aBan = null;
                    foreach (ABan innerBan in _BanEnforcerSearchResults.ToList())
                    {
                        if (innerBan.ban_id == banID)
                        {
                            aBan = innerBan;
                            break;
                        }
                    }
                    if (aBan != null)
                    {
                        switch (strValue)
                        {
                            case "Active":
                                aBan.ban_status = strValue;
                                break;
                            case "Disabled":
                                aBan.ban_status = strValue;
                                break;
                            default:
                                Log.Error("Unknown setting when assigning ban status.");
                                return;
                        }
                        if (aBan.ban_status == "Disabled")
                        {
                            if (aBan.ban_record.command_action.command_key == "player_ban_perm" || aBan.ban_record.command_action.command_key == "player_ban_perm_future")
                            {
                                aBan.ban_record.command_action = GetCommandByKey("player_ban_perm_old");
                            }
                            else if (aBan.ban_record.command_action.command_key == "player_ban_temp")
                            {
                                aBan.ban_record.command_action = GetCommandByKey("player_ban_temp_old");
                            }
                            UpdateRecord(aBan.ban_record);
                        }
                        else if (aBan.ban_status == "Active")
                        {
                            if (aBan.ban_record.command_action.command_key == "player_ban_perm_old")
                            {
                                aBan.ban_record.command_action = GetCommandByKey("player_ban_perm");
                            }
                            else if (aBan.ban_record.command_action.command_key == "player_ban_temp_old")
                            {
                                aBan.ban_record.command_action = GetCommandByKey("player_ban_temp");
                            }
                            UpdateRecord(aBan.ban_record);
                        }
                        UpdateBanStatus(aBan);
                        Log.Success("Ban " + aBan.ban_id + " is now " + strValue);
                    }
                    else
                    {
                        Log.Error("Unable to update ban. This should not happen.");
                    }
                }
                else if (Regex.Match(strVariable, @"Banned Tags").Success)
                {
                    _BannedTags = CPluginVariable.DecodeStringArray(strValue).Where(entry => !String.IsNullOrEmpty(entry)).ToArray();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Banned Tags", typeof(String), CPluginVariable.EncodeStringArray(_BannedTags)));
                }
                else if (Regex.Match(strVariable, @"Punishment Hierarchy").Success)
                {
                    _PunishmentHierarchy = CPluginVariable.DecodeStringArray(strValue).Where(punishType => _PunishmentSeverityIndex.Contains(punishType)).ToArray();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Punishment Hierarchy", typeof(String), CPluginVariable.EncodeStringArray(_PunishmentHierarchy)));
                }
                else if (Regex.Match(strVariable, @"Combine Server Punishments").Success)
                {
                    Boolean combine = Boolean.Parse(strValue);
                    if (_CombineServerPunishments != combine)
                    {
                        _CombineServerPunishments = combine;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Combine Server Punishments", typeof(Boolean), _CombineServerPunishments));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Forgives").Success)
                {
                    Boolean AutomaticForgives = Boolean.Parse(strValue);
                    if (_AutomaticForgives != AutomaticForgives)
                    {
                        _AutomaticForgives = AutomaticForgives;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Forgives", typeof(Boolean), _AutomaticForgives));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Forgive Days Since Punished").Success)
                {
                    Int32 AutomaticForgiveLastPunishDays = Int32.Parse(strValue);
                    if (AutomaticForgiveLastPunishDays != _AutomaticForgiveLastPunishDays)
                    {
                        if (AutomaticForgiveLastPunishDays < 7)
                        {
                            AutomaticForgiveLastPunishDays = 7;
                        }
                        _AutomaticForgiveLastPunishDays = AutomaticForgiveLastPunishDays;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Forgive Days Since Punished", typeof(Int32), _AutomaticForgiveLastPunishDays));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Forgive Days Since Forgiven").Success)
                {
                    Int32 AutomaticForgiveLastForgiveDays = Int32.Parse(strValue);
                    if (AutomaticForgiveLastForgiveDays != _AutomaticForgiveLastForgiveDays)
                    {
                        if (AutomaticForgiveLastForgiveDays < 7)
                        {
                            AutomaticForgiveLastForgiveDays = 7;
                        }
                        _AutomaticForgiveLastForgiveDays = AutomaticForgiveLastForgiveDays;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Forgive Days Since Forgiven", typeof(Int32), _AutomaticForgiveLastForgiveDays));
                    }
                }
                else if (Regex.Match(strVariable, @"Only Kill Players when Server in low population").Success)
                {
                    Boolean onlyKill = Boolean.Parse(strValue);
                    if (onlyKill != _OnlyKillOnLowPop)
                    {
                        _OnlyKillOnLowPop = onlyKill;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Only Kill Players when Server in low population", typeof(Boolean), _OnlyKillOnLowPop));
                    }
                }
                else if (Regex.Match(strVariable, @"Short Server Name").Success)
                {
                    var newName = MakeAlphanumeric(strValue);
                    if (newName.Length > 30)
                    {
                        newName = newName.Substring(0, Math.Min(30, newName.Length - 1));
                    }
                    _shortServerName = strValue;
                    QueueSettingForUpload(new CPluginVariable(@"Short Server Name", typeof(String), _shortServerName));
                }
                else if (Regex.Match(strVariable, @"Low Population Value").Success)
                {
                    Int32 lowPopulationPlayerCount = Int32.Parse(strValue);
                    if (lowPopulationPlayerCount != _lowPopulationPlayerCount)
                    {
                        if (lowPopulationPlayerCount < 0)
                        {
                            Log.Error("Low population value cannot be less than 0.");
                            lowPopulationPlayerCount = 0;
                        }
                        if (lowPopulationPlayerCount > _highPopulationPlayerCount)
                        {
                            Log.Error("Low population value cannot be greater than high population value.");
                            lowPopulationPlayerCount = _highPopulationPlayerCount;
                        }
                        _lowPopulationPlayerCount = lowPopulationPlayerCount;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Low Population Value", typeof(Int32), _lowPopulationPlayerCount));
                    }
                }
                else if (Regex.Match(strVariable, @"High Population Value").Success)
                {
                    Int32 HighPopulationPlayerCount = Int32.Parse(strValue);
                    if (HighPopulationPlayerCount != _highPopulationPlayerCount)
                    {
                        if (HighPopulationPlayerCount > 64)
                        {
                            Log.Error("High population value cannot be greater than 64.");
                            HighPopulationPlayerCount = 64;
                        }
                        if (HighPopulationPlayerCount < _lowPopulationPlayerCount)
                        {
                            Log.Error("High population value cannot be less than low population value.");
                            HighPopulationPlayerCount = _lowPopulationPlayerCount;
                        }
                        _highPopulationPlayerCount = HighPopulationPlayerCount;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"High Population Value", typeof(Int32), _highPopulationPlayerCount));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Server Restart When Empty").Success)
                {
                    Boolean automaticServerRestart = Boolean.Parse(strValue);
                    if (automaticServerRestart != _automaticServerRestart)
                    {
                        _automaticServerRestart = automaticServerRestart;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Server Restart When Empty", typeof(Boolean), _automaticServerRestart));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Restart Minimum Uptime Hours").Success)
                {
                    Int32 automaticServerRestartMinHours = Int32.Parse(strValue);
                    if (automaticServerRestartMinHours != _automaticServerRestartMinHours)
                    {
                        if (automaticServerRestartMinHours < 5)
                        {
                            Log.Error("Duration between automatic restarts cannot be less than 5 hours.");
                            automaticServerRestartMinHours = 5;
                        }
                        _automaticServerRestartMinHours = automaticServerRestartMinHours;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Restart Minimum Uptime Hours", typeof(Int32), _automaticServerRestartMinHours));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Procon Reboot When Server Reboots").Success)
                {
                    Boolean automaticServerRestartProcon = Boolean.Parse(strValue);
                    if (automaticServerRestartProcon != _automaticServerRestartProcon)
                    {
                        _automaticServerRestartProcon = automaticServerRestartProcon;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Procon Reboot When Server Reboots", typeof(Boolean), _automaticServerRestartProcon));
                    }
                }
                else if (Regex.Match(strVariable, @"Procon Memory Usage MB Warning").Success)
                {
                    Int32 MemoryUsageWarn = Int32.Parse(strValue);
                    if (MemoryUsageWarn != _MemoryUsageWarn)
                    {
                        if (MemoryUsageWarn < 256)
                        {
                            Log.Error("Memory warning level cannot be less than 256MB.");
                            MemoryUsageWarn = 256;
                        }
                        _MemoryUsageWarn = MemoryUsageWarn;
                        QueueSettingForUpload(new CPluginVariable(@"Procon Memory Usage MB Warning", typeof(Int32), _MemoryUsageWarn));
                    }
                }
                else if (Regex.Match(strVariable, @"Procon Memory Usage MB AdKats Restart").Success)
                {
                    Int32 MemoryUsageRestartPlugin = Int32.Parse(strValue);
                    if (MemoryUsageRestartPlugin != _MemoryUsageRestartPlugin)
                    {
                        if (MemoryUsageRestartPlugin < 512)
                        {
                            Log.Error("AdKats reboot for memory level cannot be less than 512MB.");
                            MemoryUsageRestartPlugin = 512;
                        }
                        _MemoryUsageRestartPlugin = MemoryUsageRestartPlugin;
                        QueueSettingForUpload(new CPluginVariable(@"Procon Memory Usage MB AdKats Restart", typeof(Int32), _MemoryUsageRestartPlugin));
                    }
                }
                else if (Regex.Match(strVariable, @"Procon Memory Usage MB Procon Restart").Success)
                {
                    Int32 MemoryUsageRestartProcon = Int32.Parse(strValue);
                    if (MemoryUsageRestartProcon != _MemoryUsageRestartProcon)
                    {
                        if (MemoryUsageRestartProcon < 1024)
                        {
                            Log.Error("Procon shutdown for memory level cannot be less than 1024MB.");
                            MemoryUsageRestartProcon = 1024;
                        }
                        _MemoryUsageRestartProcon = MemoryUsageRestartProcon;
                        QueueSettingForUpload(new CPluginVariable(@"Procon Memory Usage MB Procon Restart", typeof(Int32), _MemoryUsageRestartProcon));
                    }
                }
                else if (Regex.Match(strVariable, @"Use IRO Punishment").Success)
                {
                    Boolean iro = Boolean.Parse(strValue);
                    if (iro != _IROActive)
                    {
                        _IROActive = iro;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use IRO Punishment", typeof(Boolean), _IROActive));
                    }
                }
                else if (Regex.Match(strVariable, @"IRO Punishment Overrides Low Pop").Success)
                {
                    Boolean overrideIRO = Boolean.Parse(strValue);
                    if (overrideIRO != _IROOverridesLowPop)
                    {
                        _IROOverridesLowPop = overrideIRO;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"IRO Punishment Overrides Low Pop", typeof(Boolean), _IROOverridesLowPop));
                    }
                }
                else if (Regex.Match(strVariable, @"IRO Punishment Infractions Required to Override").Success)
                {
                    Int32 IROOverridesLowPopInfractions = Int32.Parse(strValue);
                    if (IROOverridesLowPopInfractions != _IROOverridesLowPopInfractions)
                    {
                        _IROOverridesLowPopInfractions = IROOverridesLowPopInfractions;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"IRO Punishment Infractions Required to Override", typeof(Int32), _IROOverridesLowPopInfractions));
                    }
                }
                else if (Regex.Match(strVariable, @"IRO Timeout Minutes").Success)
                {
                    Int32 timeout = Int32.Parse(strValue);
                    if (timeout != _IROTimeout)
                    {
                        _IROTimeout = timeout;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"IRO Timeout Minutes", typeof(Int32), _IROTimeout));
                    }
                }
                else if (Regex.Match(strVariable, @"MySQL Hostname").Success)
                {
                    _mySqlHostname = strValue;
                    _dbSettingsChanged = true;
                    _DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Port").Success)
                {
                    Int32 tmp = 3306;
                    if (!Int32.TryParse(strValue, out tmp))
                    {
                        tmp = 3306;
                    }
                    if (tmp > 0 && tmp < 65536)
                    {
                        _mySqlPort = strValue;
                        _dbSettingsChanged = true;
                        _DbCommunicationWaitHandle.Set();
                    }
                    else
                    {
                        Log.Error("Invalid value for MySQL Port: '" + strValue + "'. Must be number between 1 and 65535!");
                    }
                }
                else if (Regex.Match(strVariable, @"MySQL Database").Success)
                {
                    if (!Regex.IsMatch(strValue, @"^[a-zA-Z0-9_]+$"))
                    {
                        Log.Error("Invalid MySQL Database name: '" + strValue + "'. Only alphanumeric characters and underscores are allowed.");
                        return;
                    }
                    _mySqlSchemaName = strValue;
                    _dbSettingsChanged = true;
                    _DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Username").Success)
                {
                    _mySqlUsername = strValue;
                    _dbSettingsChanged = true;
                    _DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Password").Success)
                {
                    if (strValue == "********") return;
                    _mySqlPassword = strValue;
                    _dbSettingsChanged = true;
                    _DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"Send Emails").Success)
                {
                    //Disabled
                    _UseEmail = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable("Send Emails", typeof(Boolean), _UseEmail));
                }
                else if (Regex.Match(strVariable, @"Use SSL?").Success)
                {
                    _EmailHandler.UseSSL = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable("Use SSL?", typeof(Boolean), _EmailHandler.UseSSL));
                }
                else if (Regex.Match(strVariable, @"SMTP-Server address").Success)
                {
                    if (!String.IsNullOrEmpty(strValue))
                    {
                        _EmailHandler.SMTPServer = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("SMTP-Server address", typeof(String), _EmailHandler.SMTPServer));
                    }
                }
                else if (Regex.Match(strVariable, @"SMTP-Server port").Success)
                {
                    Int32 iPort = Int32.Parse(strValue);
                    if (iPort > 0)
                    {
                        _EmailHandler.SMTPPort = iPort;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("SMTP-Server port", typeof(Int32), _EmailHandler.SMTPPort));
                    }
                }
                else if (Regex.Match(strVariable, @"Sender address").Success)
                {
                    if (string.IsNullOrEmpty(strValue))
                    {
                        _EmailHandler.SenderEmail = "SENDER_CANNOT_BE_EMPTY";
                        Log.Error("No sender for email was given! Cancelling Operation.");
                    }
                    else
                    {
                        _EmailHandler.SenderEmail = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("Sender address", typeof(String), _EmailHandler.SenderEmail));
                    }
                }
                else if (Regex.Match(strVariable, @"SMTP-Server username").Success)
                {
                    if (string.IsNullOrEmpty(strValue))
                    {
                        _EmailHandler.SMTPUser = "SMTP_USERNAME_CANNOT_BE_EMPTY";
                        Log.Error("No username for SMTP was given! Cancelling Operation.");
                    }
                    else
                    {
                        _EmailHandler.SMTPUser = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("SMTP-Server username", typeof(String), _EmailHandler.SMTPUser));
                    }
                }
                else if (Regex.Match(strVariable, @"SMTP-Server password").Success)
                {
                    if (string.IsNullOrEmpty(strValue))
                    {
                        _EmailHandler.SMTPPassword = "SMTP_PASSWORD_CANNOT_BE_EMPTY";
                        Log.Error("No password for SMTP was given! Cancelling Operation.");
                    }
                    else
                    {
                        _EmailHandler.SMTPPassword = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("SMTP-Server password", typeof(String), _EmailHandler.SMTPPassword));
                    }
                }
                else if (Regex.Match(strVariable, @"Custom HTML Addition").Success)
                {
                    _EmailHandler.CustomHTMLAddition = strValue;
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable("Custom HTML Addition", typeof(String), _EmailHandler.CustomHTMLAddition));
                }
                else if (Regex.Match(strVariable, @"Extra Recipient Email Addresses").Success)
                {
                    _EmailHandler.RecipientEmails = CPluginVariable.DecodeStringArray(strValue).ToList();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Extra Recipient Email Addresses", typeof(String), strValue));
                }
                else if (Regex.Match(strVariable, @"Only Send Report Emails When Admins Offline").Success)
                {
                    Boolean emailReportsOnlyWhenAdminless = Boolean.Parse(strValue);
                    if (emailReportsOnlyWhenAdminless != _EmailReportsOnlyWhenAdminless)
                    {
                        _EmailReportsOnlyWhenAdminless = emailReportsOnlyWhenAdminless;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Only Send Report Emails When Admins Offline", typeof(Boolean), _EmailReportsOnlyWhenAdminless));
                    }
                }
                else if (Regex.Match(strVariable, @"Send PushBullet Reports").Success)
                {
                    _UsePushBullet = Boolean.Parse(strValue);
                    QueueSettingForUpload(new CPluginVariable(@"Send PushBullet Reports", typeof(Boolean), _UsePushBullet));
                }
                else if (Regex.Match(strVariable, @"PushBullet Access Token").Success)
                {
                    _PushBulletHandler.AccessToken = strValue;
                    QueueSettingForUpload(new CPluginVariable(@"PushBullet Access Token", typeof(String), _PushBulletHandler.AccessToken));
                }
                else if (Regex.Match(strVariable, @"PushBullet Note Target").Success)
                {
                    switch (strValue)
                    {
                        case "Private":
                            _PushBulletHandler.DefaultTarget = PushBulletHandler.Target.Private;
                            break;
                        case "Channel":
                            _PushBulletHandler.DefaultTarget = PushBulletHandler.Target.Channel;
                            break;
                        default:
                            Log.Error("Unknown setting when changing PushBullet note target.");
                            return;
                    }
                    QueueSettingForUpload(new CPluginVariable(@"PushBullet Note Target", typeof(String), _PushBulletHandler.DefaultTarget.ToString()));
                }
                else if (Regex.Match(strVariable, @"PushBullet Channel Tag").Success)
                {
                    _PushBulletHandler.DefaultChannelTag = strValue;
                    QueueSettingForUpload(new CPluginVariable(@"PushBullet Channel Tag", typeof(String), _PushBulletHandler.DefaultChannelTag));
                }
                else if (Regex.Match(strVariable, @"Only Send PushBullet Reports When Admins Offline").Success)
                {
                    _PushBulletReportsOnlyWhenAdminless = Boolean.Parse(strValue);
                    QueueSettingForUpload(new CPluginVariable(@"Only Send PushBullet Reports When Admins Offline", typeof(Boolean), _PushBulletReportsOnlyWhenAdminless));
                }
                else if (Regex.Match(strVariable, @"Send Reports to Discord WebHook").Success)
                {
                    _UseDiscordForReports = Boolean.Parse(strValue);
                    if (_UseDiscordForReports && _firstPlayerListComplete && String.IsNullOrEmpty(_shortServerName))
                    {
                        Log.Warn("The 'Short Server Name' setting must be filled in before posting discord reports.");
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Send Reports to Discord WebHook", typeof(Boolean), _UseDiscordForReports));
                }
                else if (Regex.Match(strVariable, @"Discord WebHook URL").Success)
                {
                    _DiscordManager.ReportWebhookUrl = strValue;
                    if (_UseDiscordForReports && _firstPlayerListComplete && String.IsNullOrEmpty(_shortServerName))
                    {
                        Log.Warn("The 'Short Server Name' setting must be filled in before posting discord reports.");
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Discord WebHook URL", typeof(String), _DiscordManager.ReportWebhookUrl));
                }
                else if (Regex.Match(strVariable, @"Only Send Discord Reports When Admins Offline").Success)
                {
                    _DiscordReportsOnlyWhenAdminless = Boolean.Parse(strValue);
                    if (_UseDiscordForReports && _firstPlayerListComplete && String.IsNullOrEmpty(_shortServerName))
                    {
                        Log.Warn("The 'Short Server Name' setting must be filled in before posting discord reports.");
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Only Send Discord Reports When Admins Offline", typeof(Boolean), _DiscordReportsOnlyWhenAdminless));
                }
                else if (Regex.Match(strVariable, @"Send update if reported players leave without action").Success)
                {
                    _DiscordReportsLeftWithoutAction = Boolean.Parse(strValue);
                    if (_UseDiscordForReports && _firstPlayerListComplete && String.IsNullOrEmpty(_shortServerName))
                    {
                        Log.Warn("The 'Short Server Name' setting must be filled in before posting discord reports.");
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Send update if reported players leave without action", typeof(Boolean), _DiscordReportsLeftWithoutAction));
                }
                else if (Regex.Match(strVariable, @"Discord Role IDs to Mention in Reports").Success)
                {
                    _DiscordManager.RoleIDsToMentionReport = CPluginVariable.DecodeStringArray(strValue).ToList();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Discord Role IDs to Mention in Reports", typeof(String), strValue));
                }
                else if (Regex.Match(strVariable, @"On-Player-Muted Message").Success)
                {
                    if (_MutedPlayerMuteMessage != strValue)
                    {
                        _MutedPlayerMuteMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"On-Player-Muted Message", typeof(String), _MutedPlayerMuteMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Persistent On-Player-Killed Message").Success)
                {
                    if (_PersistentMutedPlayerKillMessage != strValue)
                    {
                        _PersistentMutedPlayerKillMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Persistent On-Player-Killed Message", typeof(String), _PersistentMutedPlayerKillMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Persistent On-Player-Kicked Message").Success)
                {
                    if (_PersistentMutedPlayerKickMessage != strValue)
                    {
                        _PersistentMutedPlayerKickMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Persistent On-Player-Kicked Message", typeof(String), _PersistentMutedPlayerKickMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Killed Message").Success)
                {
                    if (_MutedPlayerKillMessage != strValue)
                    {
                        _MutedPlayerKillMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"On-Player-Killed Message", typeof(String), _MutedPlayerKillMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Kicked Message").Success)
                {
                    if (_MutedPlayerKickMessage != strValue)
                    {
                        _MutedPlayerKickMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"On-Player-Kicked Message", typeof(String), _MutedPlayerKickMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Unmuted Message").Success)
                {
                    if (_UnMutePlayerMessage != strValue)
                    {
                        _UnMutePlayerMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"On-Player-Unmuted Message", typeof(String), _UnMutePlayerMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"# Chances to give player before kicking").Success)
                {
                    Int32 tmp = 5;
                    int.TryParse(strValue, out tmp);
                    if (_MutedPlayerChances != tmp)
                    {
                        _MutedPlayerChances = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"# Chances to give player before kicking", typeof(Int32), _MutedPlayerChances));
                    }
                }
                else if (Regex.Match(strVariable, @"# Chances to give persistent muted player before kicking").Success)
                {
                    Int32 tmp = 5;
                    int.TryParse(strValue, out tmp);
                    if (_PersistentMutedPlayerChances != tmp)
                    {
                        _PersistentMutedPlayerChances = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"# Chances to give persistent muted player before kicking", typeof(Int32), _PersistentMutedPlayerChances));
                    }
                }
                else if (Regex.Match(strVariable, @"Ignore commands for mute enforcement").Success)
                {
                    Boolean ignoreCommands = Boolean.Parse(strValue);
                    if (_MutedPlayerIgnoreCommands != ignoreCommands)
                    {
                        _MutedPlayerIgnoreCommands = ignoreCommands;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ignore commands for mute enforcement", typeof(Boolean), _MutedPlayerIgnoreCommands));
                    }
                }
                else if (Regex.Match(strVariable, @"Send first spawn warning for persistent muted players").Success)
                {
                    Boolean useFirstSpawnMutedMessage = Boolean.Parse(strValue);
                    if (_UseFirstSpawnMutedMessage != useFirstSpawnMutedMessage)
                    {
                        _UseFirstSpawnMutedMessage = useFirstSpawnMutedMessage;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Send first spawn warning for persistent muted players", typeof(Boolean), _UseFirstSpawnMutedMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"First spawn persistent muted warning text").Success)
                {
                    if (_FirstSpawnMutedMessage != strValue)
                    {
                        _FirstSpawnMutedMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"First spawn persistent muted warning text", typeof(String), _FirstSpawnMutedMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Persistent force mute temp-ban duration minutes").Success)
                {
                    Int32 tmp = 45;
                    int.TryParse(strValue, out tmp);
                    if (_ForceMuteBanDuration != tmp)
                    {
                        _ForceMuteBanDuration = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Persistent force mute temp-ban duration minutes", typeof(Int32), _ForceMuteBanDuration));
                    }
                }
                else if (Regex.Match(strVariable, @"Ticket Window High").Success)
                {
                    Int32 tmp = 2;
                    int.TryParse(strValue, out tmp);
                    if (tmp != _TeamSwapTicketWindowHigh)
                    {
                        _TeamSwapTicketWindowHigh = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ticket Window High", typeof(Int32), _TeamSwapTicketWindowHigh));
                    }
                }
                else if (Regex.Match(strVariable, @"Ticket Window Low").Success)
                {
                    Int32 tmp = 2;
                    int.TryParse(strValue, out tmp);
                    if (tmp != _TeamSwapTicketWindowLow)
                    {
                        _TeamSwapTicketWindowLow = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ticket Window Low", typeof(Int32), _TeamSwapTicketWindowLow));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Admin Assistants").Success)
                {
                    Boolean enableAA = Boolean.Parse(strValue);
                    if (_EnableAdminAssistants != enableAA)
                    {
                        _EnableAdminAssistants = enableAA;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistants", typeof(Boolean), _EnableAdminAssistants));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Admin Assistant Perk").Success)
                {
                    Boolean enableAA = Boolean.Parse(strValue);
                    if (_EnableAdminAssistantPerk != enableAA)
                    {
                        _EnableAdminAssistantPerk = enableAA;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistant Perk", typeof(Boolean), _EnableAdminAssistantPerk));
                    }
                }
                else if (Regex.Match(strVariable, @"Use AA Report Auto Handler").Success)
                {
                    Boolean useAAHandler = Boolean.Parse(strValue);
                    if (useAAHandler != _UseAAReportAutoHandler)
                    {
                        _UseAAReportAutoHandler = useAAHandler;
                        if (_UseAAReportAutoHandler)
                        {
                            if (_threadsReady)
                            {
                                Log.Info("Automatic Report Handler activated.");
                            }
                        }
                        else
                        {
                            Log.Info("Automatic Report Handler disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use AA Report Auto Handler", typeof(Boolean), _UseAAReportAutoHandler));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Report-Handler Strings").Success)
                {
                    _AutoReportHandleStrings = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Auto-Report-Handler Strings", typeof(String), CPluginVariable.EncodeStringArray(_AutoReportHandleStrings)));
                }
                else if (Regex.Match(strVariable, @"Minimum Confirmed Reports Per Month").Success)
                {
                    Int32 monthlyReports = Int32.Parse(strValue);
                    if (_MinimumRequiredMonthlyReports != monthlyReports)
                    {
                        _MinimumRequiredMonthlyReports = monthlyReports;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Confirmed Reports Per Month", typeof(Int32), _MinimumRequiredMonthlyReports));
                    }
                }
                else if (Regex.Match(strVariable, @"Yell display time seconds").Success)
                {
                    Int32 yellTime = Int32.Parse(strValue);
                    if (_YellDuration != yellTime)
                    {
                        if (yellTime < 0)
                        {
                            Log.Error("Yell duration cannot be negative.");
                            return;
                        }
                        if (yellTime > 10)
                        {
                            Log.Error("Yell duration cannot be greater than 10 seconds.");
                            return;
                        }
                        _YellDuration = yellTime;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Yell display time seconds", typeof(Int32), _YellDuration));
                    }
                }
                else if (Regex.Match(strVariable, @"Pre-Message List").Success)
                {
                    _PreMessageList = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Pre-Message List", typeof(String), CPluginVariable.EncodeStringArray(_PreMessageList.ToArray())));
                }
                else if (Regex.Match(strVariable, @"Require Use of Pre-Messages").Success)
                {
                    Boolean require = Boolean.Parse(strValue);
                    if (require != _RequirePreMessageUse)
                    {
                        _RequirePreMessageUse = require;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Require Use of Pre-Messages", typeof(Boolean), _RequirePreMessageUse));
                    }
                }
                else if (Regex.Match(strVariable, @"Use first spawn message").Success)
                {
                    Boolean useFirstSpawnMessage = Boolean.Parse(strValue);
                    if (useFirstSpawnMessage != _UseFirstSpawnMessage)
                    {
                        _UseFirstSpawnMessage = useFirstSpawnMessage;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use first spawn message", typeof(Boolean), _UseFirstSpawnMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"First spawn message text").Success)
                {
                    if (_FirstSpawnMessage != strValue)
                    {
                        _FirstSpawnMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"First spawn message text", typeof(String), _FirstSpawnMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Alternative Spawn Message for Low Server Playtime").Success)
                {
                    Boolean enableLowPlaytimeSpawnMessage = Boolean.Parse(strValue);
                    if (enableLowPlaytimeSpawnMessage != _EnableLowPlaytimeSpawnMessage)
                    {
                        _EnableLowPlaytimeSpawnMessage = enableLowPlaytimeSpawnMessage;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Alternative Spawn Message for Low Server Playtime", typeof(Boolean), _EnableLowPlaytimeSpawnMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Low Server Playtime Spawn Message Threshold Hours").Success)
                {
                    Int32 minPlaytimeHours = Int32.Parse(strValue);
                    if (_LowPlaytimeSpawnMessageHours != minPlaytimeHours)
                    {
                        if (minPlaytimeHours < 0)
                        {
                            Log.Error("Low Server Playtime Spawn Message Threshold Hours cannot be negative.");
                            return;
                        }
                        _LowPlaytimeSpawnMessageHours = minPlaytimeHours;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Low Server Playtime Spawn Message Threshold Hours", typeof(Int32), _LowPlaytimeSpawnMessageHours));
                    }
                }
                else if (Regex.Match(strVariable, @"Low Server Playtime Spawn Message Text").Success)
                {
                    if (_LowPlaytimeSpawnMessage != strValue)
                    {
                        _LowPlaytimeSpawnMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Low Server Playtime Spawn Message Text", typeof(String), _LowPlaytimeSpawnMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Use First Spawn Reputation and Infraction Message").Success)
                {
                    Boolean UseFirstSpawnRepMessage = Boolean.Parse(strValue);
                    if (UseFirstSpawnRepMessage != _useFirstSpawnRepMessage)
                    {
                        _useFirstSpawnRepMessage = UseFirstSpawnRepMessage;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use First Spawn Reputation and Infraction Message", typeof(Boolean), _useFirstSpawnRepMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Perk Expiration Notification").Success)
                {
                    Boolean UsePerkExpirationNotify = Boolean.Parse(strValue);
                    if (UsePerkExpirationNotify != _UsePerkExpirationNotify)
                    {
                        _UsePerkExpirationNotify = UsePerkExpirationNotify;
                        QueueSettingForUpload(new CPluginVariable(@"Use Perk Expiration Notification", typeof(Boolean), _UsePerkExpirationNotify));
                    }
                }
                else if (Regex.Match(strVariable, @"Perk Expiration Notify Days Remaining").Success)
                {
                    Int32 PerkExpirationNotifyDays = Int32.Parse(strValue);
                    if (_PerkExpirationNotifyDays != PerkExpirationNotifyDays)
                    {
                        if (PerkExpirationNotifyDays <= 0)
                        {
                            Log.Error("Notify duration must be a positive number of days.");
                            return;
                        }
                        if (PerkExpirationNotifyDays > 90)
                        {
                            Log.Error("Notify duration cannot be longer than 90 days.");
                            return;
                        }
                        _PerkExpirationNotifyDays = PerkExpirationNotifyDays;
                        QueueSettingForUpload(new CPluginVariable(@"Perk Expiration Notify Days Remaining", typeof(Int32), _PerkExpirationNotifyDays));
                    }
                }
                else if (Regex.Match(strVariable, @"SpamBot Enable").Success)
                {
                    Boolean spamBotEnable = Boolean.Parse(strValue);
                    if (spamBotEnable != _spamBotEnabled)
                    {
                        if (spamBotEnable)
                        {
                            _spamBotSayLastPost = UtcNow() - TimeSpan.FromSeconds(10);
                            _spamBotYellLastPost = UtcNow() - TimeSpan.FromSeconds(10);
                            _spamBotTellLastPost = UtcNow() - TimeSpan.FromSeconds(10);
                        }
                        _spamBotEnabled = spamBotEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"SpamBot Enable", typeof(Boolean), _spamBotEnabled));
                    }
                }
                else if (Regex.Match(strVariable, @"SpamBot Say List").Success)
                {
                    List<String> spamBotSayList = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    if (!_spamBotSayList.SequenceEqual(spamBotSayList))
                    {
                        _spamBotSayQueue.Clear();
                        foreach (String line in spamBotSayList.Where(message => !String.IsNullOrEmpty(message)).ToList())
                        {
                            _spamBotSayQueue.Enqueue(line);
                        }
                    }
                    _spamBotSayList = spamBotSayList;
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"SpamBot Say List", typeof(String), CPluginVariable.EncodeStringArray(_spamBotSayList.ToArray())));
                }
                else if (Regex.Match(strVariable, @"SpamBot Say Delay Seconds").Success)
                {
                    Int32 spamBotSayDelaySeconds = Int32.Parse(strValue);
                    if (_spamBotSayDelaySeconds != spamBotSayDelaySeconds)
                    {
                        if (spamBotSayDelaySeconds < 60)
                        {
                            Log.Error("SpamBot Say Delay cannot be less than 60 seconds.");
                            spamBotSayDelaySeconds = 60;
                        }
                        _spamBotSayDelaySeconds = spamBotSayDelaySeconds;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"SpamBot Say Delay Seconds", typeof(Int32), _spamBotSayDelaySeconds));
                    }
                }
                else if (Regex.Match(strVariable, @"SpamBot Yell List").Success)
                {
                    List<String> spamBotYellList = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    if (!_spamBotYellList.SequenceEqual(spamBotYellList))
                    {
                        _spamBotYellQueue.Clear();
                        foreach (String line in spamBotYellList.Where(message => !String.IsNullOrEmpty(message)).ToList())
                        {
                            _spamBotYellQueue.Enqueue(line);
                        }
                    }
                    _spamBotYellList = spamBotYellList;
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"SpamBot Yell List", typeof(String), CPluginVariable.EncodeStringArray(_spamBotYellList.ToArray())));
                }
                else if (Regex.Match(strVariable, @"SpamBot Yell Delay Seconds").Success)
                {
                    Int32 spamBotYellDelaySeconds = Int32.Parse(strValue);
                    if (_spamBotYellDelaySeconds != spamBotYellDelaySeconds)
                    {
                        if (spamBotYellDelaySeconds < 60)
                        {
                            Log.Error("SpamBot Yell Delay cannot be less than 60 seconds.");
                            spamBotYellDelaySeconds = 60;
                        }
                        _spamBotYellDelaySeconds = spamBotYellDelaySeconds;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"SpamBot Yell Delay Seconds", typeof(Int32), _spamBotYellDelaySeconds));
                    }
                }
                else if (Regex.Match(strVariable, @"SpamBot Tell List").Success)
                {
                    List<String> spamBotTellList = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    if (!_spamBotTellList.SequenceEqual(spamBotTellList))
                    {
                        _spamBotTellQueue.Clear();
                        foreach (String line in spamBotTellList.Where(message => !String.IsNullOrEmpty(message)).ToList())
                        {
                            _spamBotTellQueue.Enqueue(line);
                        }
                    }
                    _spamBotTellList = spamBotTellList;
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"SpamBot Tell List", typeof(String), CPluginVariable.EncodeStringArray(_spamBotTellList.ToArray())));
                }
                else if (Regex.Match(strVariable, @"SpamBot Tell Delay Seconds").Success)
                {
                    Int32 spamBotTellDelaySeconds = Int32.Parse(strValue);
                    if (_spamBotTellDelaySeconds != spamBotTellDelaySeconds)
                    {
                        if (spamBotTellDelaySeconds < 60)
                        {
                            Log.Error("SpamBot Tell Delay cannot be less than 60 seconds.");
                            spamBotTellDelaySeconds = 60;
                        }
                        _spamBotTellDelaySeconds = spamBotTellDelaySeconds;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"SpamBot Tell Delay Seconds", typeof(Int32), _spamBotTellDelaySeconds));
                    }
                }
                else if (Regex.Match(strVariable, @"Exclude Whitelist from Spam").Success)
                {
                    Boolean spamBotExcludeWhitelist = Boolean.Parse(strValue);
                    if (spamBotExcludeWhitelist != _spamBotExcludeWhitelist)
                    {
                        _spamBotExcludeWhitelist = spamBotExcludeWhitelist;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Exclude Whitelist from Spam", typeof(Boolean), _spamBotExcludeWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Exclude Admins from Spam").Success)
                {
                    Boolean spamBotExcludeAdmins = Boolean.Parse(strValue);
                    if (spamBotExcludeAdmins != _spamBotExcludeAdmins)
                    {
                        _spamBotExcludeAdmins = spamBotExcludeAdmins;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Exclude Admins from Spam", typeof(Boolean), _spamBotExcludeAdmins));
                    }
                }
                else if (Regex.Match(strVariable, @"Exclude Teamspeak and Discord Players from Spam").Success)
                {
                    Boolean spamBotExcludeTeamspeakDiscord = Boolean.Parse(strValue);
                    if (spamBotExcludeTeamspeakDiscord != _spamBotExcludeTeamspeakDiscord)
                    {
                        _spamBotExcludeTeamspeakDiscord = spamBotExcludeTeamspeakDiscord;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Exclude Teamspeak and Discord Players from Spam", typeof(Boolean), _spamBotExcludeTeamspeakDiscord));
                    }
                }
                else if (Regex.Match(strVariable, @"Exclude High Reputation Players from Spam").Success)
                {
                    Boolean spamBotExcludeHighReputation = Boolean.Parse(strValue);
                    if (spamBotExcludeHighReputation != _spamBotExcludeHighReputation)
                    {
                        _spamBotExcludeHighReputation = spamBotExcludeHighReputation;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Exclude High Reputation Players from Spam", typeof(Boolean), _spamBotExcludeHighReputation));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Server Playtime in Hours for Receiving Spam").Success)
                {
                    Int32 spamBotMinPlaytimeHours = Int32.Parse(strValue);
                    if (_spamBotMinPlaytimeHours != spamBotMinPlaytimeHours)
                    {
                        if (spamBotMinPlaytimeHours < 0)
                        {
                            Log.Error("Minimum Server Playtime in Hours for Receiving Spam cannot be less than 0 hours.");
                            spamBotMinPlaytimeHours = 0;
                        }
                        _spamBotMinPlaytimeHours = spamBotMinPlaytimeHours;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Server Playtime in Hours for Receiving Spam", typeof(Int32), _spamBotMinPlaytimeHours));
                    }
                }
                else if (Regex.Match(strVariable, @"Player Battlecry Volume").Success)
                {
                    switch (strValue)
                    {
                        case "Disabled":
                            _battlecryVolume = BattlecryVolume.Disabled;
                            break;
                        case "Say":
                            _battlecryVolume = BattlecryVolume.Say;
                            break;
                        case "Yell":
                            _battlecryVolume = BattlecryVolume.Yell;
                            break;
                        case "Tell":
                            _battlecryVolume = BattlecryVolume.Tell;
                            break;
                        default:
                            Log.Error("Unknown setting when updating player battlecry volume.");
                            return;
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Player Battlecry Volume", typeof(String), _battlecryVolume.ToString()));
                }
                else if (Regex.Match(strVariable, @"Player Battlecry Max Length").Success)
                {
                    Int32 battleCryMaxLength = Int32.Parse(strValue);
                    if (_battlecryMaxLength != battleCryMaxLength)
                    {
                        if (battleCryMaxLength < 20)
                        {
                            Log.Error("Battlecry max length cannot be less than 20 characters.");
                            battleCryMaxLength = 20;
                        }
                        if (battleCryMaxLength > 300)
                        {
                            Log.Error("Battlecry max length cannot be more than 300 characters.");
                            battleCryMaxLength = 300;
                        }
                        _battlecryMaxLength = battleCryMaxLength;
                        QueueSettingForUpload(new CPluginVariable(@"Player Battlecry Max Length", typeof(Int32), _battlecryMaxLength));
                    }
                }
                else if (Regex.Match(strVariable, @"Player Battlecry Denied Words").Success)
                {
                    _battlecryDeniedWords = CPluginVariable.DecodeStringArray(strValue);
                    QueueSettingForUpload(new CPluginVariable(@"Player Battlecry Denied Words", typeof(String), CPluginVariable.EncodeStringArray(_battlecryDeniedWords)));
                }
                else if (Regex.Match(strVariable, @"Display Admin Name in ").Success)
                {
                    Boolean display = Boolean.Parse(strValue);
                    if (display != _ShowAdminNameInAnnouncement)
                    {
                        _ShowAdminNameInAnnouncement = display;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Display Admin Name in Kick and Ban Announcement", typeof(Boolean), _ShowAdminNameInAnnouncement));
                    }
                }
                else if (Regex.Match(strVariable, @"Display New Player Announcement").Success)
                {
                    Boolean display = Boolean.Parse(strValue);
                    if (display != _ShowNewPlayerAnnouncement)
                    {
                        _ShowNewPlayerAnnouncement = display;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Display New Player Announcement", typeof(Boolean), _ShowNewPlayerAnnouncement));
                    }
                }
                else if (Regex.Match(strVariable, @"Display Player Name Change Announcement").Success)
                {
                    Boolean display = Boolean.Parse(strValue);
                    if (display != _ShowPlayerNameChangeAnnouncement)
                    {
                        _ShowPlayerNameChangeAnnouncement = display;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Display Player Name Change Announcement", typeof(Boolean), _ShowPlayerNameChangeAnnouncement));
                    }
                }
                else if (Regex.Match(strVariable, @"Display Targeted Player Left Notification").Success)
                {
                    Boolean ShowTargetedPlayerLeftNotification = Boolean.Parse(strValue);
                    if (ShowTargetedPlayerLeftNotification != _ShowTargetedPlayerLeftNotification)
                    {
                        _ShowTargetedPlayerLeftNotification = ShowTargetedPlayerLeftNotification;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Display Targeted Player Left Notification", typeof(Boolean), _ShowTargetedPlayerLeftNotification));
                    }
                }
                else if (Regex.Match(strVariable, @"Display Ticket Rates in Procon Chat").Success)
                {
                    Boolean display = Boolean.Parse(strValue);
                    if (display != _DisplayTicketRatesInProconChat)
                    {
                        _DisplayTicketRatesInProconChat = display;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Display Ticket Rates in Procon Chat", typeof(Boolean), _DisplayTicketRatesInProconChat));
                    }
                }
                else if (Regex.Match(strVariable, @"Inform players of reports against them").Success)
                {
                    Boolean inform = Boolean.Parse(strValue);
                    if (inform != _InformReportedPlayers)
                    {
                        _InformReportedPlayers = inform;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Inform players of reports against them", typeof(Boolean), _InformReportedPlayers));
                    }
                }
                else if (Regex.Match(strVariable, @"Player Inform Exclusion Strings").Success)
                {
                    _PlayerInformExclusionStrings = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Player Inform Exclusion Strings", typeof(String), CPluginVariable.EncodeStringArray(_PlayerInformExclusionStrings)));
                }
                else if (Regex.Match(strVariable, @"Inform reputable players of admin joins").Success)
                {
                    Boolean InformReputablePlayersOfAdminJoins = Boolean.Parse(strValue);
                    if (InformReputablePlayersOfAdminJoins != _InformReputablePlayersOfAdminJoins)
                    {
                        _InformReputablePlayersOfAdminJoins = InformReputablePlayersOfAdminJoins;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Inform reputable players of admin joins", typeof(Boolean), _InformReputablePlayersOfAdminJoins));
                    }
                }
                else if (Regex.Match(strVariable, @"Inform admins of admin joins").Success)
                {
                    Boolean InformAdminsOfAdminJoins = Boolean.Parse(strValue);
                    if (InformAdminsOfAdminJoins != _InformAdminsOfAdminJoins)
                    {
                        _InformAdminsOfAdminJoins = InformAdminsOfAdminJoins;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Inform admins of admin joins", typeof(Boolean), _InformAdminsOfAdminJoins));
                    }
                }
                else if (Regex.Match(strVariable, @"Use All Caps Limiter").Success)
                {
                    Boolean UseAllCapsLimiter = Boolean.Parse(strValue);
                    if (UseAllCapsLimiter != _UseAllCapsLimiter)
                    {
                        _UseAllCapsLimiter = UseAllCapsLimiter;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use All Caps Limiter", typeof(Boolean), _UseAllCapsLimiter));
                    }
                }
                else if (Regex.Match(strVariable, @"All Caps Limiter Only Limit Specified Players").Success)
                {
                    Boolean AllCapsLimiterSpecifiedPlayersOnly = Boolean.Parse(strValue);
                    if (AllCapsLimiterSpecifiedPlayersOnly != _AllCapsLimiterSpecifiedPlayersOnly)
                    {
                        _AllCapsLimiterSpecifiedPlayersOnly = AllCapsLimiterSpecifiedPlayersOnly;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Only Limit Specified Players", typeof(Boolean), _AllCapsLimiterSpecifiedPlayersOnly));
                    }
                }
                else if (Regex.Match(strVariable, @"All Caps Limiter Character Percentage").Success)
                {
                    Int32 AllCapsLimterPercentage = Int32.Parse(strValue);
                    if (_AllCapsLimterPercentage != AllCapsLimterPercentage)
                    {
                        if (AllCapsLimterPercentage < 50)
                        {
                            Log.Error("All Caps Limiter Character Percentage cannot be less than 50%.");
                            AllCapsLimterPercentage = 50;
                        }
                        if (AllCapsLimterPercentage > 100)
                        {
                            Log.Error("All Caps Limiter Character Percentage cannot be greater than 100%.");
                            AllCapsLimterPercentage = 100;
                        }
                        _AllCapsLimterPercentage = AllCapsLimterPercentage;
                        QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Character Percentage", typeof(Int32), _AllCapsLimterPercentage));
                    }
                }
                else if (Regex.Match(strVariable, @"All Caps Limiter Minimum Characters").Success)
                {
                    Int32 AllCapsLimterMinimumCharacters = Int32.Parse(strValue);
                    if (_AllCapsLimterMinimumCharacters != AllCapsLimterMinimumCharacters)
                    {
                        if (AllCapsLimterMinimumCharacters < 1)
                        {
                            Log.Error("All Caps Limiter Minimum Characters cannot be less than 1.");
                            AllCapsLimterMinimumCharacters = 1;
                        }
                        if (AllCapsLimterMinimumCharacters > 100)
                        {
                            Log.Error("All Caps Limiter Minimum Characters cannot be greater than 100.");
                            AllCapsLimterMinimumCharacters = 100;
                        }
                        _AllCapsLimterMinimumCharacters = AllCapsLimterMinimumCharacters;
                        QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Minimum Characters", typeof(Int32), _AllCapsLimterMinimumCharacters));
                    }
                }
                else if (Regex.Match(strVariable, @"All Caps Limiter Warn Threshold").Success)
                {
                    Int32 AllCapsLimiterWarnThreshold = Int32.Parse(strValue);
                    if (_AllCapsLimiterWarnThreshold != AllCapsLimiterWarnThreshold)
                    {
                        if (AllCapsLimiterWarnThreshold < 1)
                        {
                            Log.Error("All Caps Limiter Warn Threshold cannot be less than 1.");
                            AllCapsLimiterWarnThreshold = 1;
                        }
                        if (_threadsReady)
                        {
                            if (AllCapsLimiterWarnThreshold >= _AllCapsLimiterKillThreshold)
                            {
                                Log.Error("All Caps Limiter Warn Threshold must be less than All Caps Limiter Kill Threshold.");
                                //Reset the value
                                AllCapsLimiterWarnThreshold = _AllCapsLimiterWarnThreshold;
                            }
                        }
                        _AllCapsLimiterWarnThreshold = AllCapsLimiterWarnThreshold;
                        QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Warn Threshold", typeof(Int32), _AllCapsLimiterWarnThreshold));
                    }
                }
                else if (Regex.Match(strVariable, @"All Caps Limiter Kill Threshold").Success)
                {
                    Int32 AllCapsLimiterKillThreshold = Int32.Parse(strValue);
                    if (_AllCapsLimiterKillThreshold != AllCapsLimiterKillThreshold)
                    {
                        if (AllCapsLimiterKillThreshold < 2)
                        {
                            Log.Error("All Caps Limiter Kill Threshold cannot be less than 2.");
                            AllCapsLimiterKillThreshold = 2;
                        }
                        if (_threadsReady)
                        {
                            if (AllCapsLimiterKillThreshold >= _AllCapsLimiterKickThreshold)
                            {
                                Log.Error("All Caps Limiter Kill Threshold must be less than All Caps Limiter Kick Threshold.");
                                //Reset the value
                                AllCapsLimiterKillThreshold = _AllCapsLimiterKillThreshold;
                            }
                            if (AllCapsLimiterKillThreshold <= _AllCapsLimiterWarnThreshold)
                            {
                                Log.Error("All Caps Limiter Kill Threshold must be greater than All Caps Limiter Warn Threshold.");
                                //Reset the value
                                AllCapsLimiterKillThreshold = _AllCapsLimiterKillThreshold;
                            }
                        }
                        _AllCapsLimiterKillThreshold = AllCapsLimiterKillThreshold;
                        QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Kill Threshold", typeof(Int32), _AllCapsLimiterKillThreshold));
                    }
                }
                else if (Regex.Match(strVariable, @"All Caps Limiter Kick Threshold").Success)
                {
                    Int32 AllCapsLimiterKickThreshold = Int32.Parse(strValue);
                    if (_AllCapsLimiterKickThreshold != AllCapsLimiterKickThreshold)
                    {
                        if (AllCapsLimiterKickThreshold < 3)
                        {
                            Log.Error("All Caps Limiter Kick Threshold cannot be less than 3.");
                            AllCapsLimiterKickThreshold = 3;
                        }
                        if (_threadsReady)
                        {
                            if (AllCapsLimiterKickThreshold <= _AllCapsLimiterKillThreshold)
                            {
                                Log.Error("All Caps Limiter Kick Threshold must be greater than All Caps Limiter Kill Threshold.");
                                //Reset the value
                                AllCapsLimiterKickThreshold = _AllCapsLimiterKickThreshold;
                            }
                        }
                        _AllCapsLimiterKickThreshold = AllCapsLimiterKickThreshold;
                        QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Kick Threshold", typeof(Int32), _AllCapsLimiterKickThreshold));
                    }
                }
                else if (Regex.Match(strVariable, @"Add User").Success)
                {
                    if (IsSoldierNameValid(strValue))
                    {
                        AUser aUser = new AUser
                        {
                            user_name = strValue,
                            user_expiration = UtcNow().AddYears(20),
                            user_notes = "No Notes"
                        };
                        if (_userCache.Values.Any(iUser => aUser.user_name == iUser.user_name))
                        {
                            Log.Error("Unable to add " + aUser.user_name + ", a user with that name already exists.");
                            return;
                        }
                        Thread addUserThread = new Thread(new ThreadStart(delegate
                        {
                            Thread.CurrentThread.Name = "UserChange";
                            Log.Debug(() => "Starting a user change thread.", 2);
                            //Attempt to add soldiers matching the user's name
                            TryAddUserSoldier(aUser, aUser.user_name);
                            QueueUserForUpload(aUser);
                            Log.Debug(() => "Exiting a user change thread.", 2);
                            Threading.StopWatchdog();
                        }));
                        Threading.StartWatchdog(addUserThread);
                    }
                    else
                    {
                        Log.Error("User id had invalid formatting, please try again.");
                    }
                }
                else if (Regex.Match(strVariable, @"Add Role").Success)
                {
                    if (!String.IsNullOrEmpty(strValue))
                    {
                        String roleName = new Regex("[^a-zA-Z0-9 _-]").Replace(strValue, "");
                        String roleKey = roleName.Replace(' ', '_');
                        if (!String.IsNullOrEmpty(roleName) && !String.IsNullOrEmpty(roleKey))
                        {
                            ARole aRole = new ARole
                            {
                                role_key = roleKey,
                                role_name = roleName
                            };
                            //By default we should include all commands as allowed
                            lock (_CommandIDDictionary)
                            {
                                foreach (ACommand aCommand in _RoleKeyDictionary["guest_default"].RoleAllowedCommands.Values)
                                {
                                    aRole.RoleAllowedCommands.Add(aCommand.command_key, aCommand);
                                }
                            }
                            //Queue it for upload
                            _RoleCommandCacheUpdateBufferStart = UtcNow();
                            QueueRoleForUpload(aRole);
                        }
                        else
                        {
                            Log.Error("Role had invalid characters, please try again.");
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Add Definition?").Success)
                {
                    var sanitizedName = strValue.Replace("|", "");
                    if (!String.IsNullOrEmpty(sanitizedName))
                    {
                        ChallengeManager.CreateDefinition(sanitizedName);
                    }
                }
                else if (strVariable.StartsWith("CDH"))
                {
                    //CDH1 | 5 ARs | Change Name?
                    //CDH1 | 5 ARs | Add Damage Type?
                    //CDH1 | 5 ARs | Add Weapon?
                    //CDH1 | 5 ARs | Delete Definition?
                    //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Damage Type
                    //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Weapon Count
                    //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Kill Count
                    //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Delete Detail?
                    //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Weapon Name
                    //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Kill Count
                    //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Delete Detail?

                    // Split the variable name on | characters using the library
                    var variableSplit = CPluginVariable.DecodeStringArray(strVariable);
                    var definitionIDStr = variableSplit[0].TrimStart("CDH".ToCharArray()).Trim();
                    var defID = Int64.Parse(definitionIDStr);
                    if (defID <= 0)
                    {
                        Log.Error("Definition setting had an invalid definition ID of " + defID + ".");
                        return;
                    }
                    var definition = ChallengeManager.GetDefinition(defID);
                    if (definition == null)
                    {
                        Log.Error("Unable to fetch definition for ID " + defID + ".");
                        return;
                    }
                    var section = variableSplit[2].Trim();
                    switch (section)
                    {
                        case "Change Name?":
                            definition.SetNameByString(strValue);
                            break;
                        case "Add Damage Type?":
                            if (strValue != "None")
                            {
                                definition.CreateDetail("Damage", strValue);
                            }
                            break;
                        case "Add Weapon?":
                            if (strValue != "None")
                            {
                                definition.CreateDetail("Weapon", strValue);
                            }
                            break;
                        case "Delete Definition?":
                            if (strValue.ToLower().Trim() == "delete")
                            {
                                definition.DBDelete(null);
                            }
                            break;
                        default:
                            // None of the main sections match. Maybe we're in a detail?
                            if (section.StartsWith("CDD"))
                            {
                                // Yep, we're in a detail. Parse it.

                                //CDH1 | 5 ARs | Change Name?
                                //CDH1 | 5 ARs | Add Damage Type?
                                //CDH1 | 5 ARs | Add Weapon?
                                //CDH1 | 5 ARs | Delete Definition?
                                //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Damage Type
                                //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Weapon Count
                                //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Kill Count
                                //CDH1 | 5 ARs | CDD1 | Damage - Assault Rifle | Delete Detail?
                                //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Weapon Name
                                //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Kill Count
                                //CDH1 | 5 ARs | CDD2 | Weapon - AEK-971 | Delete Detail?

                                var detailIDStr = section.TrimStart("CDD".ToCharArray()).Trim();
                                var detailID = Int64.Parse(detailIDStr);
                                if (detailID <= 0)
                                {
                                    Log.Error("Definition setting had an invalid definition ID of " + detailID + ".");
                                    return;
                                }
                                var detail = definition.GetDetail(detailID);
                                if (detail == null)
                                {
                                    Log.Error("Unable to fetch definition " + definition.ID + " detail for ID " + detailID + ".");
                                    return;
                                }
                                var detailSection = variableSplit[4].Trim();

                                switch (detailSection)
                                {
                                    case "Damage Type":
                                        detail.SetDamageTypeByString(strValue);
                                        break;
                                    case "Weapon Count":
                                        detail.SetWeaponCountByString(strValue);
                                        break;
                                    case "Weapon Name":
                                        detail.SetWeaponNameByString(strValue);
                                        break;
                                    case "Kill Count":
                                        detail.SetKillCountByString(strValue);
                                        break;
                                    case "Delete Detail?":
                                        if (strValue.ToLower().Trim() == "delete")
                                        {
                                            detail.DBDelete(null);
                                            definition.SortDetails(null);
                                        }
                                        break;
                                    default:
                                        Log.Error("Section " + detailSection + " not found.");
                                        break;
                                }
                            }
                            else
                            {
                                // Nope, no idea where we are. Get out of here.
                                Log.Error("Unknown setting section " + section + " parsed in challenge definition section.");
                                return;
                            }
                            break;
                    }
                }
                else if (Regex.Match(strVariable, @"Add Rule?").Success)
                {
                    var sanitizedName = strValue.Replace("|", "");
                    if (!String.IsNullOrEmpty(sanitizedName))
                    {
                        ChallengeManager.CreateRule(sanitizedName);
                    }
                }
                else if (strVariable.StartsWith("CRH"))
                {
                    //CRH1 | 5 ARs 1 Round | Definition
                    //CRH1 | 5 ARs 1 Round | Name
                    //CRH1 | 5 ARs 1 Round | Enabled
                    //CRH1 | 5 ARs 1 Round | Tier
                    //CRH1 | 5 ARs 1 Round | Completion
                    //CRH1 | 5 ARs 1 Round | Round Count
                    //CRH1 | 5 ARs 1 Round | Delete Rule?
                    //CRH2 | 5 ARs 30 Mins | Definition
                    //CRH2 | 5 ARs 30 Mins | Name
                    //CRH2 | 5 ARs 30 Mins | Enabled
                    //CRH2 | 5 ARs 30 Mins | Tier
                    //CRH2 | 5 ARs 30 Mins | Completion
                    //CRH2 | 5 ARs 30 Mins | Duration Minutes
                    //CRH2 | 5 ARs 30 Mins | Delete Rule?

                    // Split the variable name on | characters using the library
                    var variableSplit = CPluginVariable.DecodeStringArray(strVariable);
                    var ruleIDStr = variableSplit[0].TrimStart("CRH".ToCharArray()).Trim();
                    var ruleID = Int64.Parse(ruleIDStr);
                    if (ruleID <= 0)
                    {
                        Log.Error("Rule setting had an invalid rule ID of " + ruleID + ".");
                        return;
                    }
                    var rule = ChallengeManager.GetRule(ruleID);
                    if (rule == null)
                    {
                        Log.Error("Unable to fetch rule for ID " + ruleID + ".");
                        return;
                    }
                    var section = variableSplit[2].Trim();
                    switch (section)
                    {
                        case "Definition":
                            rule.SetDefinitionByString(strValue);
                            break;
                        case "Name":
                            rule.SetNameByString(strValue);
                            break;
                        case "Enabled":
                            rule.SetEnabledByString(strValue);
                            break;
                        case "Tier":
                            rule.SetTierByString(strValue);
                            break;
                        case "Completion":
                            rule.SetCompletionTypeByString(strValue);
                            break;
                        case "Round Count":
                            rule.SetRoundCountByString(strValue);
                            break;
                        case "Duration Minutes":
                            rule.SetDurationMinutesByString(strValue);
                            break;
                        case "Death Count":
                            rule.SetDeathCountByString(strValue);
                            break;
                        case "Delete Rule?":
                            if (strValue.ToLower().Trim() == "delete")
                            {
                                rule.DBDelete(null);
                            }
                            break;
                        case "^^^SET COMPLETION TYPE^^^":
                            // Ignore this
                            break;
                        default:
                            // No idea where we are. Get out of here.
                            Log.Error("Unknown setting section " + section + " parsed in challenge rule section.");
                            return;
                    }
                }
                else if (Regex.Match(strVariable, @"Add Reward?").Success)
                {
                    Int32 newTier = 1;
                    try
                    {
                        newTier = Int32.Parse(strValue);
                        if (newTier == 0)
                        {
                            return;
                        }
                        if (newTier < 1)
                        {
                            Log.Error("Rule tier cannot be less than 1.");
                            newTier = 1;
                        }
                        if (newTier > 10)
                        {
                            Log.Error("Rule tier cannot be greter than 10.");
                            newTier = 10;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error parsing Tier. Create rewards with tier 1-10.");
                    }
                    ChallengeManager.CreateReward(newTier);
                }
                else if (strVariable.StartsWith("CCR"))
                {
                    //CCR1 | Tier 1 - ReservedSlot | Tier Level
                    //CCR1 | Tier 1 - ReservedSlot | Reward Type
                    //CCR1 | Tier 1 - ReservedSlot | Enabled
                    //CCR1 | Tier 1 - ReservedSlot | Duration Minutes
                    //CCR1 | Tier 1 - ReservedSlot | Delete Reward?

                    // Split the variable name on | characters using the library
                    var variableSplit = CPluginVariable.DecodeStringArray(strVariable);
                    var rewardIDStr = variableSplit[0].TrimStart("CCR".ToCharArray()).Trim();
                    var rewardID = Int64.Parse(rewardIDStr);
                    if (rewardID <= 0)
                    {
                        Log.Error("Reward setting had an invalid reward ID of " + rewardID + ".");
                        return;
                    }
                    var reward = ChallengeManager.GetReward(rewardID);
                    if (reward == null)
                    {
                        Log.Error("Unable to fetch reward for ID " + rewardID + ".");
                        return;
                    }
                    var section = variableSplit[2].Trim();
                    switch (section)
                    {
                        case "Tier Level":
                            reward.SetTierByString(strValue);
                            break;
                        case "Reward Type":
                            reward.SetRewardTypeByString(strValue);
                            break;
                        case "Enabled":
                            reward.SetEnabledByString(strValue);
                            break;
                        case "Duration Minutes":
                            reward.SetDurationMinutesByString(strValue);
                            break;
                        case "Delete Reward?":
                            if (strValue.ToLower().Trim() == "delete")
                            {
                                reward.DBDelete(null);
                            }
                            break;
                        default:
                            // No idea where we are. Get out of here.
                            Log.Error("Unknown setting section " + section + " parsed in challenge reward section.");
                            return;
                    }
                }
                else if (Regex.Match(strVariable, @"Use Proxy for Battlelog").Success)
                {
                    _UseProxy = Boolean.Parse(strValue);
                    if (_UseProxy && _firstPlayerListComplete && String.IsNullOrEmpty(_ProxyURL))
                    {
                        Log.Warn("The 'Proxy URL' setting must be filled in before using a proxy.");
                    }
                    QueueSettingForUpload(new CPluginVariable(@"Use Proxy for Battlelog", typeof(Boolean), _UseProxy));
                }
                else if (Regex.Match(strVariable, @"Proxy URL").Success)
                {
                    try
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            Uri uri = new Uri(strValue);
                            Log.Debug(() => "Proxy URL set to " + strValue + ".", 1);
                        }
                    }
                    catch (UriFormatException)
                    {
                        strValue = _ProxyURL;
                        Log.Warn("Invalid Proxy URL! Make sure that the URI is valid!");
                    }
                    _ProxyURL = strValue;
                    QueueSettingForUpload(new CPluginVariable(@"Proxy URL", typeof(String), _ProxyURL));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured while updating AdKats settings.", e));
            }
        }

    }
}
