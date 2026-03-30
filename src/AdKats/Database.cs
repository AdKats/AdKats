using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Dapper;

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
        private void DatabaseCommunicationThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Database Comm Thread", 1);
                Thread.CurrentThread.Name = "DatabaseComm";
                Boolean firstRun = true;
                DateTime loopStart;
                Stopwatch counter = new Stopwatch();
                while (true)
                {
                    loopStart = UtcNow();
                    try
                    {
                        Log.Debug(() => "Entering Database Comm Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        //Check if database connection settings have changed
                        if (_dbSettingsChanged)
                        {
                            Log.Debug(() => "DB Settings have changed, calling test.", 6);
                            if (TestDatabaseConnection())
                            {
                                Log.Debug(() => "Database Connection Good. Continuing Thread.", 6);
                            }
                            else
                            {
                                _dbSettingsChanged = true;
                                continue;
                            }
                        }
                        //On first run, pull all roles and commands, update database if needed
                        if (firstRun)
                        {
                            //Run any available SQL Updates
                            counter.Reset();
                            counter.Start();
                            RunSQLUpdates(false);
                            counter.Stop();
                            //Log.Write("RunSQLUpdates took " + counter.ElapsedMilliseconds + "ms");

                            counter.Reset();
                            counter.Start();

                            FetchCommands();
                            FetchRoles();

                            counter.Stop();
                            //Log.Write("Initial command fetch took " + counter.ElapsedMilliseconds + "ms");
                        }
                        counter.Reset();
                        counter.Start();
                        //FeedStatLoggerSettings();
                        //Log.Write("FeedStatLoggerSettings took " + counter.ElapsedMilliseconds + "ms");

                        //Update server ID
                        if (_serverInfo.ServerID <= 0)
                        {
                            //Checking for database server info
                            if (FetchDBServerInfo())
                            {
                                if (_serverInfo.ServerID <= 0)
                                {
                                    //Inform the user
                                    Log.Error("Database Server info could not be fetched! Make sure XpKiller's Stat Logger is running on this server!");
                                    //Disable the plugin
                                    Disable();
                                    break;
                                }
                                Log.Success("Database server info fetched. Server ID is " + _serverInfo.ServerID + ".");
                                //Push all settings for this instance to the database
                                UploadAllSettings();
                            }
                            else
                            {
                                //Inform the user
                                Log.Error("Database Server info could not be fetched! Make sure XpKiller's Stat Logger is running on this server!");
                                //Disable the plugin
                                Disable();
                                break;
                            }
                        }
                        else
                        {
                            Log.Debug(() => "Skipping server ID fetch. Server ID: " + _serverInfo.ServerID, 7);
                        }

                        //Check if settings need sync
                        if (firstRun || _settingImportID != _serverInfo.ServerID || _lastDbSettingFetch.AddSeconds(DbSettingFetchFrequency) < UtcNow())
                        {
                            Log.Debug(() => "Preparing to fetch settings from server " + _serverInfo.ServerID, 6);
                            //Fetch new settings from the database
                            FetchSettings(_settingImportID, _settingImportID != _serverInfo.ServerID);

                            counter.Reset();
                            counter.Start();
                            RunPluginOrchestration();
                            counter.Stop();
                            //Log.Write("RunPluginOrchestration took " + counter.ElapsedMilliseconds + "ms");
                            //Run any available SQL Updates
                            counter.Reset();
                            counter.Start();
                            RunSQLUpdates(true);
                            counter.Stop();
                            //Log.Write("RunSQLUpdates took " + counter.ElapsedMilliseconds + "ms");
                        }

                        Boolean displayUpdate = false;

                        counter.Reset();
                        counter.Start();
                        HandleSettingUploads();
                        counter.Stop();
                        //Log.Write("HandleSettingUploads took " + counter.ElapsedMilliseconds + "ms");

                        counter.Reset();
                        counter.Start();
                        if (HandleCommandUploads())
                        {
                            displayUpdate = true;
                        }
                        counter.Stop();
                        //Log.Write("HandleCommandUploads took " + counter.ElapsedMilliseconds + "ms");

                        counter.Reset();
                        counter.Start();
                        if (HandleRoleUploads())
                        {
                            displayUpdate = true;
                        }
                        counter.Stop();
                        //Log.Write("HandleRoleUploads took " + counter.ElapsedMilliseconds + "ms");

                        counter.Reset();
                        counter.Start();
                        if (HandleRoleRemovals())
                        {
                            displayUpdate = true;
                        }
                        counter.Stop();
                        //Log.Write("HandleRoleRemovals took " + counter.ElapsedMilliseconds + "ms");

                        counter.Reset();
                        counter.Start();
                        HandleStatisticUploads();
                        counter.Stop();
                        //Log.Write("HandleStatisticUploads took " + counter.ElapsedMilliseconds + "ms");

                        if (displayUpdate)
                        {
                            UpdateSettingPage();
                        }

                        counter.Reset();
                        counter.Start();
                        //Check for new actions from the database at given interval
                        if (_fetchActionsFromDb && (UtcNow() > _lastDbActionFetch.AddSeconds(DbActionFetchFrequency)))
                        {
                            RunActionsFromDB();
                        }
                        else
                        {
                            Log.Debug(() => "Skipping DB action fetch", 7);
                        }
                        counter.Stop();

                        HandleUserChanges();

                        //Start the other threads
                        if (firstRun)
                        {
                            //Set the start time
                            _AdKatsStartTime = UtcNow();

                            //Import round ID
                            FetchRoundID(false);

                            //Start other threads
                            Threading.StartWatchdog(_PlayerListingThread);
                            Threading.StartWatchdog(_AccessFetchingThread);
                            Threading.StartWatchdog(_KillProcessingThread);
                            Threading.StartWatchdog(_MessageProcessingThread);
                            Threading.StartWatchdog(_CommandParsingThread);
                            Threading.StartWatchdog(_ActionHandlingThread);
                            Threading.StartWatchdog(_TeamSwapThread);
                            Threading.StartWatchdog(_BanEnforcerThread);
                            Threading.StartWatchdog(_AntiCheatThread);

                            firstRun = false;
                            _threadsReady = true;
                        }

                        if (ChallengeManager != null)
                        {
                            ChallengeManager.HandleRead(null, false);
                        }

                        counter.Reset();
                        counter.Start();
                        if (_UseBanEnforcer)
                        {
                            HandleActiveBanEnforcer();
                        }
                        else
                        {
                            if (_UseBanEnforcerPreviousState)
                            {
                                RepopulateProconBanList();
                                _UseBanEnforcerPreviousState = false;
                            }
                        }
                        counter.Stop();
                        //Log.Write("HandleActiveBanEnforcer took " + counter.ElapsedMilliseconds + "ms");

                        if (_UnprocessedRecordQueue.Count > 0)
                        {
                            counter.Reset();
                            counter.Start();
                            Log.Debug(() => "Unprocessed Record: " + _UnprocessedRecordQueue.Count + " Current: 0", 4);
                            Log.Debug(() => "Preparing to lock inbound record queue to retrive new records", 7);
                            Queue<ARecord> inboundRecords;
                            lock (_UnprocessedRecordQueue)
                            {
                                Log.Debug(() => "Inbound records found. Grabbing.", 6);
                                //Grab all records in the queue
                                inboundRecords = new Queue<ARecord>(_UnprocessedRecordQueue.ToArray());
                                //Clear the queue for next run
                                _UnprocessedRecordQueue.Clear();
                            }
                            //Loop through all records in order that they came in
                            while (inboundRecords.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                Log.Debug(() => "Unprocessed Record: " + _UnprocessedRecordQueue.Count + " Current: " + inboundRecords.Count, 4);
                                //Pull the next record
                                ARecord record = inboundRecords.Dequeue();
                                //Process the record message
                                record.record_message = ReplacePlayerInformation(record.record_message, record.target_player);
                                //Upload the record
                                Boolean success = HandleRecordUpload(record);
                                //Check for action handling needs
                                if (success && !record.record_action_executed && !record.record_orchestrate)
                                {
                                    //Action is only called after initial upload, not after update
                                    Log.Debug(() => "Upload success. Attempting to add to action queue.", 6);

                                    //Only queue the record for action handling if it's not an enforced ban
                                    if (record.command_type.command_key != "banenforcer_enforce")
                                    {
                                        QueueRecordForActionHandling(record);
                                    }
                                }
                                else
                                {
                                    Log.Debug(() => "Record does not need action handling by this server.", 6);
                                    //finalize the record
                                    FinalizeRecord(record);
                                }
                            }
                            counter.Stop();
                            //Log.Write("UnprocessedRecords took " + counter.ElapsedMilliseconds + "ms");
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                        }
                        else
                        {
                            counter.Reset();
                            counter.Start();
                            Log.Debug(() => "No unprocessed records. Waiting for input", 7);
                            _DbCommunicationWaitHandle.Reset();
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _DbCommunicationWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                            counter.Stop();
                            //Log.Write("Waiting after complete took " + counter.ElapsedMilliseconds + "ms");
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("Database Comm thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in Database Comm thread. Skipping current loop.", e));
                    }
                }
                Log.Debug(() => "Ending Database Comm Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in database comm thread.", e));
            }
        }

        private void FeedStatLoggerSettings()
        {
            Log.Debug(() => "FeedStatLoggerSettings starting!", 6);
            //Every 60 minutes feed stat logger settings
            if (_lastStatLoggerStatusUpdateTime.AddMinutes(60) < UtcNow())
            {
                if (Threading.IsAlive("StatLoggerSettingsFeeder"))
                {
                    return;
                }
                Thread statLoggerFeedingThread = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "StatLoggerSettingsFeeder";
                        Thread.Sleep(250);
                        Log.Debug(() => "Starting a stat logger setting feeder thread.", 5);
                        _lastStatLoggerStatusUpdateTime = UtcNow();
                        if (_statLoggerVersion == "BF3")
                        {
                            SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Livescoreboard in DB?", "Yes");
                            if (_FeedStatLoggerSettings)
                            {
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Statslogging?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Weaponstats?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable KDR correction?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "MapStats ON?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Session ON?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Save Sessiondata to DB?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Log playerdata only (no playerstats)?", "No");
                                Double slOffset = UtcNow().Subtract(DateTime.Now).TotalHours;
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Servertime Offset", slOffset.ToString());
                            }
                            if (_PostStatLoggerChatManually)
                            {
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Chatlogging?", "No");
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Instant Logging of Chat Messages?", "No");
                            }
                            else if (_FeedStatLoggerSettings)
                            {
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Chatlogging?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Instant Logging of Chat Messages?", "Yes");
                            }
                        }
                        else if (_statLoggerVersion == "UNIVERSAL")
                        {
                            SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Livescoreboard in DB?", "Yes");
                            if (_FeedStatLoggerSettings)
                            {
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Statslogging?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Weaponstats?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable KDR correction?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "MapStats ON?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Session ON?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Save Sessiondata to DB?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Log playerdata only (no playerstats)?", "No");
                                Double slOffset = UtcNow().Subtract(DateTime.Now).TotalHours;
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Servertime Offset", slOffset.ToString());
                            }
                            if (_PostStatLoggerChatManually)
                            {
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Chatlogging?", "No");
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Instant Logging of Chat Messages?", "No");
                            }
                            else if (_FeedStatLoggerSettings)
                            {
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Chatlogging?", "Yes");
                                SetExternalPluginSetting("CChatGUIDStatsLogger", "Instant Logging of Chat Messages?", "Yes");
                            }
                        }
                        else
                        {
                            Log.Error("Stat logger version is unknown, unable to feed stat logger settings.");
                        }
                        //TODO put back in the future
                        //confirmStatLoggerSetup();
                        Log.Debug(() => "Exiting a stat logger setting feeder thread.", 5);
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error while feeding stat logger settings.", e));
                    }
                    Threading.StopWatchdog();
                }));
                Threading.StartWatchdog(statLoggerFeedingThread);
            }
            Log.Debug(() => "FeedStatLoggerSettings finished!", 6);
        }

        private void HandleSettingUploads()
        {
            try
            {
                if (_SettingUploadQueue.Count > 0)
                {
                    if (Threading.IsAlive("SettingUploader"))
                    {
                        return;
                    }
                    Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                    {
                        Thread.CurrentThread.Name = "SettingUploader";
                        Thread.Sleep(250);
                        try
                        {
                            Log.Debug(() => "Preparing to lock inbound setting queue to get new settings", 7);
                            Queue<CPluginVariable> inboundSettingUpload;
                            lock (_SettingUploadQueue)
                            {
                                Log.Debug(() => "Inbound settings found. Grabbing.", 6);
                                //Grab all settings in the queue
                                inboundSettingUpload = new Queue<CPluginVariable>(_SettingUploadQueue.ToArray());
                                //Clear the queue for next run
                                _SettingUploadQueue.Clear();
                            }
                            //Loop through all settings in order that they came in
                            while (inboundSettingUpload.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                CPluginVariable setting = inboundSettingUpload.Dequeue();

                                UploadSetting(setting);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error while uploading settings.", e));
                        }
                        Threading.StopWatchdog();
                    })));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling setting uploads.", e));
            }
        }

        private Boolean HandleCommandUploads()
        {
            try
            {
                //Handle Inbound Command Uploads
                if (_CommandUploadQueue.Count > 0)
                {
                    Log.Debug(() => "Preparing to lock inbound command queue to get new commands", 7);
                    Queue<ACommand> inboundCommandUpload;
                    lock (_CommandUploadQueue)
                    {
                        Log.Debug(() => "Inbound commands found. Grabbing.", 6);
                        //Grab all commands in the queue
                        inboundCommandUpload = new Queue<ACommand>(_CommandUploadQueue.ToArray());
                        //Clear the queue for next run
                        _CommandUploadQueue.Clear();
                    }
                    //Loop through all commands in order that they came in
                    while (inboundCommandUpload.Count > 0)
                    {
                        ACommand command = inboundCommandUpload.Dequeue();
                        UploadCommand(command);
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling command uploads.", e));
            }
            return false;
        }

        private void HandleStatisticUploads()
        {
            try
            {
                if (_UnprocessedStatisticQueue.Count > 0)
                {
                    Log.Debug(() => "Unprocessed Statistic: " + _UnprocessedStatisticQueue.Count + " Current: 0", 4);
                    Log.Debug(() => "Preparing to lock inbound statistic queue to retrive new records", 7);
                    Queue<AStatistic> inboundStats;
                    lock (_UnprocessedStatisticQueue)
                    {
                        Log.Debug(() => "Inbound statistics found. Grabbing.", 6);
                        //Grab all statistics in the queue
                        inboundStats = new Queue<AStatistic>(_UnprocessedStatisticQueue.ToArray());
                        //Clear the queue for next run
                        _UnprocessedStatisticQueue.Clear();
                    }
                    //Loop through all statistics in order that they came in
                    while (inboundStats.Count > 0)
                    {
                        if (!_pluginEnabled)
                        {
                            break;
                        }
                        Log.Debug(() => "Unprocessed Statistic: " + _UnprocessedStatisticQueue.Count + " Current: " + inboundStats.Count, 4);
                        //Pull the next statistic
                        AStatistic aStat = inboundStats.Dequeue();
                        //Upload the statistic
                        UploadStatistic(aStat);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling statistic uploads.", e));
            }
        }

        private Boolean HandleRoleUploads()
        {
            try
            {
                if (_RoleUploadQueue.Count > 0)
                {
                    Log.Debug(() => "Preparing to lock inbound role queue to get new roles", 7);
                    Queue<ARole> inboundRoleUpload;
                    lock (_RoleUploadQueue)
                    {
                        Log.Debug(() => "Inbound roles found. Grabbing.", 6);
                        //Grab all roles in the queue
                        inboundRoleUpload = new Queue<ARole>(_RoleUploadQueue.ToArray());
                        //Clear the queue for next run
                        _RoleUploadQueue.Clear();
                    }
                    //Loop through all roles in order that they came in
                    var uploaded = false;
                    while (inboundRoleUpload.Count > 0)
                    {
                        ARole aRole = inboundRoleUpload.Dequeue();
                        UploadRole(aRole);
                        lock (_RoleIDDictionary)
                        {
                            if (_RoleIDDictionary.ContainsKey(aRole.role_id))
                            {
                                _RoleIDDictionary[aRole.role_id] = aRole;
                            }
                            else
                            {
                                _RoleIDDictionary.Add(aRole.role_id, aRole);
                            }
                            if (_RoleKeyDictionary.ContainsKey(aRole.role_key))
                            {
                                _RoleKeyDictionary[aRole.role_key] = aRole;
                            }
                            else
                            {
                                _RoleKeyDictionary.Add(aRole.role_key, aRole);
                            }
                            if (_RoleNameDictionary.ContainsKey(aRole.role_name))
                            {
                                _RoleNameDictionary[aRole.role_name] = aRole;
                            }
                            else
                            {
                                _RoleNameDictionary.Add(aRole.role_name, aRole);
                            }
                        }
                        uploaded = true;
                    }
                    if (uploaded)
                    {
                        FetchAllAccess(true);
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling role uploads.", e));
            }
            return false;
        }

        private Boolean HandleRoleRemovals()
        {
            try
            {
                if (_RoleRemovalQueue.Count > 0)
                {
                    Log.Debug(() => "Preparing to lock removal role queue to get new roles", 7);
                    Queue<ARole> inboundRoleRemoval;
                    lock (_RoleRemovalQueue)
                    {
                        Log.Debug(() => "Inbound roles found. Grabbing.", 6);
                        //Grab all roles in the queue
                        inboundRoleRemoval = new Queue<ARole>(_RoleRemovalQueue.ToArray());
                        //Clear the queue for next run
                        _RoleRemovalQueue.Clear();
                    }
                    //Loop through all commands in order that they came in
                    while (inboundRoleRemoval.Count > 0)
                    {
                        ARole aRole = inboundRoleRemoval.Dequeue();
                        RemoveRole(aRole);
                        lock (_RoleIDDictionary)
                        {
                            if (_RoleIDDictionary.ContainsKey(aRole.role_id))
                            {
                                _RoleIDDictionary.Remove(aRole.role_id);
                            }
                            if (_RoleKeyDictionary.ContainsKey(aRole.role_key))
                            {
                                _RoleKeyDictionary.Remove(aRole.role_key);
                            }
                            if (_RoleNameDictionary.ContainsKey(aRole.role_name))
                            {
                                _RoleNameDictionary.Remove(aRole.role_name);
                            }
                        }
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling role removals.", e));
            }
            return false;
        }

        private void HandleUserChanges()
        {
            try
            {
                if (_UserUploadQueue.Count > 0 || _UserRemovalQueue.Count > 0)
                {
                    Log.Debug(() => "Inbound access changes found. Grabbing.", 6);
                    Queue<AUser> inboundUserUploads;
                    lock (_UserUploadQueue)
                    {
                        inboundUserUploads = new Queue<AUser>(_UserUploadQueue.ToArray());
                        _UserUploadQueue.Clear();
                    }
                    Queue<AUser> inboundUserRemoval;
                    lock (_UserRemovalQueue)
                    {
                        inboundUserRemoval = new Queue<AUser>(_UserRemovalQueue.ToArray());
                        _UserRemovalQueue.Clear();
                    }
                    //Loop through all records in order that they came in
                    while (inboundUserUploads.Count > 0)
                    {
                        AUser user = inboundUserUploads.Dequeue();
                        UploadUser(user);
                    }
                    //Loop through all records in order that they came in
                    while (inboundUserRemoval.Count > 0)
                    {
                        AUser user = inboundUserRemoval.Dequeue();
                        Log.Info("Removing user " + user.user_name);
                        RemoveUser(user);
                    }
                    FetchAllAccess(true);
                }
                else if (UtcNow() > _lastUserFetch.AddSeconds(DbUserFetchFrequency) || !_firstUserListComplete)
                {
                    FetchAllAccess(true);
                }
                else
                {
                    Log.Debug(() => "No inbound user changes.", 7);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling user changes.", e));
            }
        }

        private void HandleActiveBanEnforcer()
        {
            try
            {
                //Call banlist at set interval (20 seconds)
                if (_UseBanEnforcerPreviousState && (UtcNow() > _lastBanListCall.AddSeconds(20)))
                {
                    _lastBanListCall = UtcNow();
                    Log.Debug(() => "banlist.list called at interval.", 6);
                    ExecuteCommand("procon.protected.send", "banList.list");

                    FetchNameBanCount();
                    FetchGUIDBanCount();
                    FetchIPBanCount();
                }
                if (!_UseBanEnforcerPreviousState || (UtcNow() > _lastDbBanFetch.AddSeconds(DbBanFetchFrequency)))
                {
                    //Load all bans on startup
                    if (!_UseBanEnforcerPreviousState)
                    {
                        //Get all bans from procon
                        Log.Info("Preparing to queue procon bans for import. Please wait.");
                        _DbCommunicationWaitHandle.Reset();
                        ExecuteCommand("procon.protected.send", "banList.list");
                        _DbCommunicationWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
                        if (_CBanProcessingQueue.Count > 0)
                        {
                            Log.Write(_CBanProcessingQueue.Count + " procon bans queued for import. Import might take several minutes if you have many bans!");
                        }
                        else
                        {
                            Log.Write("No procon bans to import into Ban Enforcer.");
                        }
                    }
                }
                else
                {
                    Log.Debug(() => "Skipping DB ban fetch", 7);
                }

                //Handle Inbound Ban Comms
                if (_BanEnforcerProcessingQueue.Count > 0)
                {
                    Log.Debug(() => "Preparing to lock inbound ban enforcer queue to retrive new bans", 7);
                    Queue<ABan> inboundBans;
                    lock (_BanEnforcerProcessingQueue)
                    {
                        Log.Debug(() => "Inbound bans found. Grabbing.", 6);
                        //Grab all messages in the queue
                        inboundBans = new Queue<ABan>(_BanEnforcerProcessingQueue.ToArray());
                        //Clear the queue for next run
                        _BanEnforcerProcessingQueue.Clear();
                    }
                    Int32 index = 1;
                    //Loop through all bans in order that they came in
                    while (inboundBans.Count > 0)
                    {
                        if (!_pluginEnabled || !_UseBanEnforcer)
                        {
                            Log.Warn("Cancelling ban import mid-operation.");
                            break;
                        }
                        //Grab the ban
                        ABan aBan = inboundBans.Dequeue();

                        Log.Debug(() => "Processing Frostbite Ban: " + index++, 6);

                        //Upload the ban
                        UploadBan(aBan);

                        //Only perform special action when ban is direct
                        //Indirect bans are through the procon banlist, so the player has already been kicked
                        if (aBan.ban_record.source_name != "BanEnforcer")
                        {
                            //Enforce the ban
                            EnforceBan(aBan, false);
                        }
                    }
                }

                //Handle BF3 Ban Manager imports
                if (!_UseBanEnforcerPreviousState)
                {
                    //Import all bans from BF3 Ban Manager
                    ImportBansFromBBM5108();
                }

                //Handle Inbound CBan Uploads
                if (_CBanProcessingQueue.Count > 0)
                {
                    if (!_UseBanEnforcerPreviousState)
                    {
                        Log.Warn("Do not disable AdKats or change any settings until upload is complete!");
                    }
                    Log.Debug(() => "Preparing to lock inbound cBan queue to retrive new cBans", 7);
                    Double totalCBans = 0;
                    Double bansImported = 0;
                    Boolean earlyExit = false;
                    DateTime startTime = UtcNow();
                    Queue<CBanInfo> inboundCBans;
                    lock (_CBanProcessingQueue)
                    {
                        Log.Debug(() => "Inbound cBans found. Grabbing.", 6);
                        //Grab all cBans in the queue
                        inboundCBans = new Queue<CBanInfo>(_CBanProcessingQueue.ToArray());
                        totalCBans = inboundCBans.Count;
                        //Clear the queue for next run
                        _CBanProcessingQueue.Clear();
                    }
                    //Loop through all cBans in order that they came in
                    Boolean bansFound = false;
                    while (inboundCBans.Count > 0)
                    {
                        //Break from the loop if the plugin is disabled or the setting is reverted.
                        if (!_pluginEnabled || !_UseBanEnforcer)
                        {
                            Log.Warn("You exited the ban upload process early, the process was not completed.");
                            earlyExit = true;
                            break;
                        }

                        bansFound = true;

                        CBanInfo cBan = inboundCBans.Dequeue();

                        //Create the record
                        ARecord record = new ARecord();
                        record.record_time = UtcNow();
                        record.record_source = ARecord.Sources.Automated;
                        //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                        switch (cBan.BanLength.Subset)
                        {
                            case TimeoutSubset.TimeoutSubsetType.Seconds:
                                //Don't import bans 1s or less.  BA/BF4DB kick players using 1s bans.
                                if (cBan.BanLength.Seconds <= 1)
                                {
                                    Log.Debug(() => "Skipping import of ban with 1 second length, likely from BA/BF4DB plugins", 5);
                                    continue;
                                }
                                record.command_type = GetCommandByKey("player_ban_temp");
                                record.command_action = GetCommandByKey("player_ban_temp");
                                record.command_numeric = cBan.BanLength.Seconds / 60;
                                break;
                            case TimeoutSubset.TimeoutSubsetType.Permanent:
                                record.command_type = GetCommandByKey("player_ban_perm");
                                record.command_action = GetCommandByKey("player_ban_perm");
                                record.command_numeric = 0;
                                break;
                            case TimeoutSubset.TimeoutSubsetType.Round:
                                //Accept round ban as 1 hour timeban
                                record.command_type = GetCommandByKey("player_ban_temp");
                                record.command_action = GetCommandByKey("player_ban_temp");
                                record.command_numeric = 60;
                                break;
                            default:
                                //Ban type is unknown, unable to process
                                continue;
                        }
                        record.source_name = _CBanAdminName;
                        record.server_id = _serverInfo.ServerID;
                        if (String.IsNullOrEmpty(cBan.SoldierName) && String.IsNullOrEmpty(cBan.Guid) && String.IsNullOrEmpty(cBan.IpAddress))
                        {
                            Log.Error("Player did not contain any identifiers when processing CBan. Ignoring.");
                            continue;
                        }
                        record.target_player = FetchPlayer(true, false, false, null, -1, cBan.SoldierName, (!String.IsNullOrEmpty(cBan.Guid)) ? (cBan.Guid.ToUpper()) : (null), cBan.IpAddress, null);
                        if (record.target_player == null)
                        {
                            Log.Error("Player could not be found/added when processing CBan. Ignoring.");
                            continue;
                        }
                        if (!String.IsNullOrEmpty(record.target_player.player_name))
                        {
                            record.target_name = record.target_player.player_name;
                        }
                        record.isIRO = false;
                        record.record_message = cBan.Reason;

                        //Update the ban enforcement depending on available information
                        Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                        Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                        Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                        //Create the ban
                        ABan aBan = new ABan
                        {
                            ban_record = record,
                            ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable) || !String.IsNullOrEmpty(cBan.SoldierName)),
                            ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable) || !String.IsNullOrEmpty(cBan.Guid)),
                            ban_enforceIP = ipAvailable && (_DefaultEnforceIP || (!nameAvailable && !guidAvailable) || !String.IsNullOrEmpty(cBan.IpAddress))
                        };
                        if (!aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP)
                        {
                            Log.Error("Unable to create ban, no proper player information");
                            continue;
                        }

                        //Check for duplicate ban posting
                        Boolean duplicateFound = false;
                        foreach (ABan storedBan in FetchPlayerBans(record.target_player))
                        {
                            if (storedBan.ban_record.record_message == record.record_message && storedBan.ban_record.source_name == record.source_name)
                            {
                                duplicateFound = true;
                            }
                        }
                        if (duplicateFound)
                        {
                            continue;
                        }

                        //Upload the ban
                        Log.Debug(() => "Uploading ban from procon.", 5);
                        UploadBan(aBan);

                        if (!_UseBanEnforcerPreviousState && (++bansImported % 25 == 0))
                        {
                            Log.Write(Math.Round(100 * bansImported / totalCBans, 2) + "% of bans uploaded. AVG " + Math.Round(bansImported / ((UtcNow() - startTime).TotalSeconds), 2) + " uploads/sec.");
                        }
                    }
                    if (bansFound && !earlyExit)
                    {
                        //If all bans have been queued for processing, clear the ban list
                        ExecuteCommand("procon.protected.send", "banList.clear");
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                        if (!_UseBanEnforcerPreviousState)
                        {
                            Log.Success("All bans uploaded into AdKats database.");
                        }
                    }
                }
                _UseBanEnforcerPreviousState = true;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling active ban enforcer.", e));
            }
        }

        public void api_ExecuteCommand(params string[] commands)
        {
            ExecuteCommand(commands);
        }

        private Boolean ConnectionCapable()
        {
            if (!string.IsNullOrEmpty(_mySqlSchemaName) && !string.IsNullOrEmpty(_mySqlHostname) && !string.IsNullOrEmpty(_mySqlPassword) && !string.IsNullOrEmpty(_mySqlPort) && !string.IsNullOrEmpty(_mySqlUsername))
            {
                Log.Debug(() => "MySql Connection capable. All variables in place.", 8);
                return true;
            }
            return false;
        }

        private MySqlConnection GetDatabaseConnection()
        {
            if (ConnectionCapable())
            {
                MySqlConnection conn = new MySqlConnection(PrepareMySqlConnectionString());
                conn.Open();
                return conn;
            }
            Log.Error("Attempted to connect to database without all variables in place.");
            return null;
        }

        private void UpdateMySqlConnectionStringBuilder()
        {
            lock (_dbCommStringBuilder)
            {
                UInt32 uintport = 3306;
                UInt32.TryParse(_mySqlPort, out uintport);
                //Add connection variables
                _dbCommStringBuilder.Port = uintport;
                _dbCommStringBuilder.Server = _mySqlHostname;
                _dbCommStringBuilder.UserID = _mySqlUsername;
                _dbCommStringBuilder.Password = _mySqlPassword;
                _dbCommStringBuilder.Database = _mySqlSchemaName;
                //Set up connection pooling
                if (UseConnectionPooling)
                {
                    _dbCommStringBuilder.Pooling = true;
                    _dbCommStringBuilder.MinimumPoolSize = Convert.ToUInt32(MinConnectionPoolSize);
                    _dbCommStringBuilder.MaximumPoolSize = Convert.ToUInt32(MaxConnectionPoolSize);
                    _dbCommStringBuilder.ConnectionLifeTime = 600;
                }
                else
                {
                    _dbCommStringBuilder.Pooling = false;
                }
                //Set Compression
                _dbCommStringBuilder.UseCompression = UseCompressedConnection;
                //Allow User Settings
                _dbCommStringBuilder.AllowUserVariables = true;
                //Set Timeout Settings
                _dbCommStringBuilder.DefaultCommandTimeout = 3600;
                _dbCommStringBuilder.ConnectionTimeout = 50;
            }
        }

        private String PrepareMySqlConnectionString()
        {
            return _dbCommStringBuilder.ConnectionString;
        }

        private Boolean TestDatabaseConnection()
        {
            Boolean databaseValid = false;
            Log.Debug(() => "testDatabaseConnection starting!", 6);
            if (ConnectionCapable())
            {
                Boolean success = false;
                Int32 attempt = 0;
                do
                {
                    if (!_pluginEnabled)
                    {
                        return false;
                    }
                    attempt++;
                    try
                    {
                        UpdateMySqlConnectionStringBuilder();
                        //Prepare the connection String and create the connection object
                        using (MySqlConnection connection = GetDatabaseConnection())
                        {
                            if (attempt > 1)
                            {
                                Log.Write("Attempting database connection. Attempt " + attempt + " of 5.");
                            }
                            //Attempt a ping through the connection
                            if (connection.Ping())
                            {
                                //Connection good
                                Log.Success("Database connection open.");
                                success = true;
                            }
                            else
                            {
                                //Connection poor
                                Log.Error("Database connection FAILED ping test.");
                            }
                        } //databaseConnection gets closed here
                        if (success)
                        {
                            //Make sure database structure is good
                            if (ConfirmDatabaseSetup())
                            {
                                //Confirm the database is valid
                                databaseValid = true;
                                //clear setting change monitor
                                _dbSettingsChanged = false;
                            }
                            else
                            {
                                Disable();
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        //Only perform retries if the error was a timeout
                        if (e.ToString().Contains("Unable to connect"))
                        {
                            Log.Error("Database connection failed. Attempt " + attempt + " of 5. " + ((attempt <= 5) ? ("Retrying in 5 seconds. ") : ("")));
                            Threading.Wait(5000);
                        }
                        else
                        {
                            break;
                        }
                    }
                } while (!success && attempt < 5);
                if (!success)
                {
                    //Invalid credentials or no connection to database
                    Log.Error("Database connection FAILED with EXCEPTION. Bad credentials, invalid hostname, or invalid port.");
                    Disable();
                }
                else
                {
                    TimeSpan diffDBUTC;
                    _dbTimingValid = TestDBTiming(true, out diffDBUTC);
                    _dbTimingOffset = diffDBUTC;
                }
            }
            else
            {
                Log.Error("Not DB connection capable yet, complete SQL connection variables.");
                Disable();
                Threading.Wait(500);
            }
            Log.Debug(() => "testDatabaseConnection finished!", 6);

            return databaseValid;
        }

        private Boolean ConfirmDatabaseSetup()
        {
            Log.Debug(() => "Confirming Database Structure.", 3);
            try
            {
                if (!ConfirmStatLoggerTables())
                {
                    Log.Error("Tables from XPKiller's Stat Logger not present in the database. Enable that plugin then re-run AdKats!");
                    return false;
                }
                if (!ConfirmAdKatsTables())
                {
                    Log.Error("AdKats tables not present or valid in the database. Have you run the AdKats database setup script yet? If so, are your tables InnoDB?");
                    return false;
                }
                Log.Success("Database confirmed functional for AdKats use.");
                return true;
            }
            catch (Exception e)
            {
                Log.Error("ERROR in ConfirmDatabaseSetup: " + e);
                return false;
            }
        }

        private Boolean runDBSetupScript()
        {
            try
            {
                Log.Write("Running database setup script. You will not lose any data.");
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    String sqlScript = null;
                    Log.Debug(() => "Fetching plugin changelog...", 2);
                    try
                    {
                        sqlScript = Util.HttpDownload("https://raw.githubusercontent.com/Hedius/E4GLAdKats/main/adkats.sql?cacherand=" + Environment.TickCount);
                        Log.Debug(() => "SQL setup script fetched.", 1);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            sqlScript = Util.HttpDownload("https://adkats.e4gl.com/adkats.sql?cacherand=" + Environment.TickCount);
                            Log.Debug(() => "SQL setup script fetched from backup location.", 1);
                        }
                        catch (Exception)
                        {
                            Log.Error("Failed to fetch SQL setup script.");
                            return false;
                        }
                    }
                    try
                    {
                        //Attempt to execute the query
                        Int32 rowsAffected = connection.Execute(sqlScript);
                        Log.Write("Setup script successful, your database is now prepared for use by AdKats " + GetPluginVersion());
                        return true;
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Your database did not accept the script. Does your account have access to table, trigger, and stored procedure creation?", e));
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Unable to set up the database for AdKats use." + e);
            }
            return false;
        }

        private Boolean ConfirmAdKatsTables()
        {
            if (_databaseConnectionCriticalState)
            {
                return false;
            }
            if (!ConfirmTable("adkats_battlelog_players"))
            {
                Log.Info("Battlelog information table not found. Attempting to add.");
                SendNonQuery("Adding battlelog information table", @"
                    CREATE TABLE `adkats_battlelog_players` (
                      `player_id` int(10) unsigned NOT NULL,
                      `persona_id` bigint(20) unsigned NOT NULL,
                      `user_id` bigint(20) unsigned NOT NULL,
                      `gravatar` varchar(32) COLLATE utf8_unicode_ci DEFAULT NULL,
                      `persona_banned` tinyint(1) NOT NULL DEFAULT 0,
                      PRIMARY KEY (`player_id`),
                      UNIQUE KEY `adkats_battlelog_players_player_id_persona_id_unique` (`player_id`,`persona_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Player Battlelog Info';", true);
                SendNonQuery("Adding battlelog information table foreign keys", @"
                    ALTER TABLE `adkats_battlelog_players` ADD CONSTRAINT `adkats_battlelog_players_ibfk_1` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE", true);
            }
            if (!ConfirmTable("adkats_battlecries"))
            {
                Log.Info("Battlecries table not found. Attempting to add.");
                SendNonQuery("Adding battlecries table", @"
                    CREATE TABLE IF NOT EXISTS `adkats_battlecries`(
                      `player_id` int(10) UNSIGNED NOT NULL,
                      `player_battlecry` varchar(300) COLLATE utf8_unicode_ci DEFAULT NULL,
                      PRIMARY KEY (`player_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Battlecries List'", true);
                SendNonQuery("Adding battlecries table foreign keys", @"
                    ALTER TABLE `adkats_battlecries` ADD CONSTRAINT `adkats_battlecries_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata`(`PlayerID`) ON UPDATE NO ACTION ON DELETE CASCADE", true);
            }
            if (!ConfirmTable("adkats_specialplayers"))
            {
                Log.Info("Special players table not found. Attempting to add.");
                SendNonQuery("Adding special soldiers table", @"
                    CREATE TABLE IF NOT EXISTS `adkats_specialplayers`(
                      `specialplayer_id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
                      `player_group` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
                      `player_id` int(10) UNSIGNED DEFAULT NULL,
                      `player_game` tinyint(4) UNSIGNED DEFAULT NULL,
                      `player_server` smallint(5) UNSIGNED DEFAULT NULL,
                      `player_identifier` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
                      `player_expiration` DATETIME NOT NULL,
                      PRIMARY KEY (`specialplayer_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Special Player List'", true);
                SendNonQuery("Adding special soldiers table foreign keys", @"
                    ALTER TABLE `adkats_specialplayers`
                        ADD CONSTRAINT `adkats_specialplayers_game_id` FOREIGN KEY (`player_game`) REFERENCES `tbl_games`(`GameID`) ON UPDATE NO ACTION ON DELETE CASCADE,
                        ADD CONSTRAINT `adkats_specialplayers_server_id` FOREIGN KEY (`player_server`) REFERENCES `tbl_server`(`ServerID`) ON UPDATE NO ACTION ON DELETE CASCADE,
                        ADD CONSTRAINT `adkats_specialplayers_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata`(`PlayerID`) ON UPDATE NO ACTION ON DELETE CASCADE", true);
            }
            if (!ConfirmTable("adkats_player_reputation"))
            {
                Log.Info("Player reputation table not found. Attempting to add.");
                SendNonQuery("Adding player reputation table", @"
                    CREATE TABLE `adkats_player_reputation` (
                      `player_id` int(10) unsigned NOT NULL,
                      `game_id` tinyint(4) unsigned NOT NULL,
                      `target_rep` float NOT NULL,
                      `source_rep` float NOT NULL,
                      `total_rep` float NOT NULL,
                      `total_rep_co` float NOT NULL,
                      PRIMARY KEY (`player_id`),
                      KEY `game_id` (`game_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Player Reputation'", true);
                SendNonQuery("Adding player reputation table foreign keys", @"
                    ALTER TABLE `adkats_player_reputation`
                        ADD CONSTRAINT `adkats_player_reputation_ibfk_1` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
                        ADD CONSTRAINT `adkats_player_reputation_ibfk_2` FOREIGN KEY (`game_id`) REFERENCES `tbl_games` (`GameID`) ON DELETE CASCADE ON UPDATE CASCADE", true);
            }
            if (!ConfirmTable("adkats_orchestration"))
            {
                Log.Info("Plugin orchestration table not found. Attempting to add.");
                SendNonQuery("Adding plugin orchestration table", @"
                     CREATE TABLE `adkats_orchestration` (
                        `setting_id` int(10) NOT NULL AUTO_INCREMENT,
                        `setting_server` SMALLINT(5) NOT NULL,
                        `setting_plugin` VARCHAR(100) NOT NULL,
                        `setting_name` VARCHAR(100) NOT NULL,
                        `setting_value` VARCHAR (5000) NOT NULL,
                        PRIMARY KEY (`setting_id`),
                        UNIQUE(`setting_server`, `setting_plugin`, `setting_name`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Plugin Orchestration'", true);
            }
            if (!ConfirmTable("tbl_extendedroundstats"))
            {
                Log.Info("Extended round stats table not found. Attempting to add.");
                SendNonQuery("Adding extended round stats table", @"
                    CREATE TABLE `tbl_extendedroundstats` (
                        `roundstat_id` int(10) unsigned NOT NULL AUTO_INCREMENT,
                        `server_id` smallint(5) unsigned NOT NULL,
                        `round_id` int(10) unsigned NOT NULL,
                        `round_elapsedTimeSec` int(10) unsigned NOT NULL,
                        `team1_count` int(10) unsigned NOT NULL,
                        `team2_count` int(10) unsigned NOT NULL,
                        `team1_score` int(10) NOT NULL,
                        `team2_score` int(10) NOT NULL,
                        `team1_spm` double NOT NULL,
                        `team2_spm` double NOT NULL,
                        `team1_tickets` int(10) NOT NULL,
                        `team2_tickets` int(10) NOT NULL,
                        `team1_tpm` double NOT NULL,
                        `team2_tpm` double NOT NULL,
                        `roundstat_time` datetime NOT NULL,
                        `map` varchar(25) CHARACTER SET 'utf8' COLLATE 'utf8_general_ci' NULL DEFAULT NULL,
                        PRIMARY KEY (`roundstat_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Extended Round Stats'", true);
            }
            if (!ConfirmTable("adkats_statistics"))
            {
                Log.Info("AdKats statistics table not found. Attempting to add.");
                SendNonQuery("Adding AdKats statistics table", @"
                    CREATE TABLE `adkats_statistics` (
                      `stat_id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
                      `server_id` SMALLINT(5) UNSIGNED NOT NULL,
                      `round_id` INT(10) UNSIGNED NOT NULL,
                      `stat_type` varchar(50) NOT NULL,
                      `target_name` varchar(50) NOT NULL,
                      `target_id` INT(10) UNSIGNED DEFAULT NULL,
                      `stat_value` FLOAT NOT NULL,
                      `stat_comment` TEXT,
                      `stat_time` DATETIME NOT NULL DEFAULT '0000-00-00 00:00:00',
                      PRIMARY KEY (`stat_id`),
                      KEY `server_id` (`server_id`),
                      KEY `stat_type` (`stat_type`),
                      KEY `target_id` (`target_id`),
                      KEY `stat_time` (`stat_time`),
                      CONSTRAINT `adkats_statistics_target_id_fk` FOREIGN KEY (`target_id`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE NO ACTION ON UPDATE NO ACTION,
                      CONSTRAINT `adkats_statistics_server_id_fk` FOREIGN KEY (`server_id`) REFERENCES `tbl_server` (`ServerID`) ON DELETE NO ACTION ON UPDATE NO ACTION
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Statistics'", true);
            }
            if (!SendQuery("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE ( TABLE_SCHEMA = @Schema AND TABLE_NAME = 'adkats_specialplayers' AND COLUMN_NAME = 'player_effective' )", false, new { Schema = _mySqlSchemaName }))
            {
                Log.Info("Special player effective not found. Attempting to add.");
                SendNonQuery("Adding special player effective.", "ALTER TABLE `adkats_specialplayers` ADD COLUMN `player_effective` DATETIME NOT NULL AFTER `player_identifier`", true);
                SendNonQuery("Adding initial special player effective values.", "UPDATE `adkats_specialplayers` SET `player_effective` = UTC_TIMESTAMP()", true);
            }
            if (!SendQuery("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE ( TABLE_SCHEMA = @Schema AND TABLE_NAME = 'adkats_specialplayers' AND COLUMN_NAME = 'player_expiration' )", false, new { Schema = _mySqlSchemaName }))
            {
                Log.Info("Special player expiration not found. Attempting to add.");
                SendNonQuery("Adding special player expiration.", "ALTER TABLE `adkats_specialplayers` ADD COLUMN `player_expiration` DATETIME NOT NULL AFTER `player_effective`", true);
                SendNonQuery("Adding initial special player expiration values.", "UPDATE `adkats_specialplayers` SET `player_expiration` = DATE_ADD(UTC_TIMESTAMP(), INTERVAL 20 YEAR)", true);
            }
            if (!SendQuery("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE ( TABLE_SCHEMA = @Schema AND TABLE_NAME = 'tbl_playerdata' AND COLUMN_NAME = 'DiscordID' )", false, new { Schema = _mySqlSchemaName }))
            {
                Log.Info("Player discord info column not found. Attempting to add.");
                SendNonQuery("Adding special player expiration.", "ALTER TABLE `tbl_playerdata` ADD COLUMN `DiscordID` VARCHAR(50) AFTER `IP_Address`", true);
            }
            if (SendQuery("SELECT specialplayer_id FROM adkats_specialplayers WHERE adkats_specialplayers.player_group = 'whitelist_hackerchecker'", false))
            {
                Log.Info("Updating whitelist_hackerchecker to new definition whitelist_anticheat.");
                SendNonQuery("Updating whitelist_hackerchecker to new definition.", "update adkats_specialplayers set adkats_specialplayers.player_group = 'whitelist_anticheat' WHERE adkats_specialplayers.player_group = 'whitelist_hackerchecker'", true);
            }
            if (!ConfirmTable("adkats_rolegroups"))
            {
                Log.Info("AdKats role groups table not found. Attempting to add.");
                SendNonQuery("Adding AdKats role groups table", @"
                    CREATE TABLE `adkats_rolegroups` (
                      `role_id` int(11) unsigned NOT NULL,
                      `group_key` VARCHAR(100) NOT NULL,
                      PRIMARY KEY (`role_id`,`group_key`),
                      KEY `adkats_rolegroups_fk_role` (`role_id`),
                      KEY `adkats_rolegroups_fk_command` (`group_key`),
                      CONSTRAINT `adkats_rolegroups_fk_role` FOREIGN KEY (`role_id`) REFERENCES `adkats_roles` (`role_id`) ON DELETE CASCADE ON UPDATE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Connection of groups to roles'", true);
            }
            if (!ConfirmTable("adkats_challenge_definition"))
            {
                Log.Info("AdKats challenge definition table not found. Attempting to add.");
                SendNonQuery("Adding challenge definition table", @"
                    CREATE TABLE IF NOT EXISTS `adkats_challenge_definition` (
                      `ID` int(10) unsigned NOT NULL AUTO_INCREMENT,
                      `Name` varchar(200) COLLATE utf8_unicode_ci NOT NULL,
                      `CreateTime` datetime NOT NULL,
                      `ModifyTime` datetime NOT NULL,
                      PRIMARY KEY (`ID`),
                      UNIQUE KEY `adkats_challenge_definition_idx_Name` (`Name`),
                      KEY `adkats_challenge_definition_idx_CreateTime` (`CreateTime`),
                      KEY `adkats_challenge_definition_idx_ModifyTime` (`ModifyTime`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Definitions'", true);
            }
            if (!ConfirmTable("adkats_challenge_definition_detail"))
            {
                Log.Info("AdKats challenge definition detail table not found. Attempting to add.");
                SendNonQuery("Adding challenge definition detail table", @"
                    CREATE TABLE IF NOT EXISTS `adkats_challenge_definition_detail` (
                      `DefID` int(10) unsigned NOT NULL,
                      `DetailID` int(10) unsigned NOT NULL,
                      `Type` varchar(100) COLLATE utf8_unicode_ci NOT NULL,
                      `Damage` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
                      `WeaponCount` int(10) unsigned NOT NULL,
                      `Weapon` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
                      `KillCount` int(10) unsigned NOT NULL,
                      `CreateTime` datetime NOT NULL,
                      `ModifyTime` datetime NOT NULL,
                      PRIMARY KEY (`DefID`, `DetailID`),
                      KEY `adkats_challenge_definition_detail_idx_CreateTime` (`CreateTime`),
                      KEY `adkats_challenge_definition_detail_idx_ModifyTime` (`ModifyTime`),
                      CONSTRAINT `adkats_challenge_definition_detail_fk_DefID` FOREIGN KEY (`DefID`) REFERENCES `adkats_challenge_definition` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Definition Details'", true);
            }
            if (!ConfirmTable("adkats_challenge_rule"))
            {
                Log.Info("AdKats challenge rule table not found. Attempting to add.");
                SendNonQuery("Adding challenge rule table", @"
                    CREATE TABLE IF NOT EXISTS `adkats_challenge_rule` (
                      `ID` int(10) unsigned NOT NULL AUTO_INCREMENT,
                      `ServerID` smallint(5) unsigned NOT NULL,
                      `DefID` int(10) unsigned NOT NULL,
                      `Enabled` int(1) unsigned NOT NULL DEFAULT 1,
                      `Name` varchar(200) COLLATE utf8_unicode_ci NOT NULL,
                      `Tier` int(10) unsigned NOT NULL DEFAULT 1,
                      `CompletionType` varchar(100) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'None',
                      `RoundCount` int(10) unsigned NOT NULL DEFAULT 1,
                      `DurationMinutes` int(10) unsigned NOT NULL DEFAULT 60, -- 4294967295
                      `DeathCount` int(10) unsigned NOT NULL DEFAULT 1,
                      `CreateTime` datetime NOT NULL,
                      `ModifyTime` datetime NOT NULL,
                      `RoundLastUsedTime` datetime NOT NULL DEFAULT '1970-01-01 00:00:00',
                      `PersonalLastUsedTime` datetime NOT NULL DEFAULT '1970-01-01 00:00:00',
                      PRIMARY KEY (`ID`),
                      UNIQUE KEY `adkats_challenge_rule_idx_Name_Server` (`Name`, `ServerID`),
                      KEY `adkats_challenge_rule_idx_ServerID` (`ServerID`),
                      KEY `adkats_challenge_rule_idx_DefID` (`DefID`),
                      KEY `adkats_challenge_rule_idx_CreateTime` (`CreateTime`),
                      KEY `adkats_challenge_rule_idx_ModifyTime` (`ModifyTime`),
                      KEY `adkats_challenge_rule_idx_RoundLastUsedTime` (`RoundLastUsedTime`),
                      KEY `adkats_challenge_rule_idx_PersonalLastUsedTime` (`PersonalLastUsedTime`),
                      CONSTRAINT `adkats_challenge_rule_fk_ServerID` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE NO ACTION ON UPDATE CASCADE, -- No action for delete. If people move their servers, don't want to lose this record.
                      CONSTRAINT `adkats_challenge_rule_fk_DefID` FOREIGN KEY (`DefID`) REFERENCES `adkats_challenge_definition` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Rules'", true);
            }
            if (!ConfirmTable("adkats_challenge_entry"))
            {
                Log.Info("AdKats challenge entry table not found. Attempting to add.");
                SendNonQuery("Adding challenge entry table", @"
                    CREATE TABLE IF NOT EXISTS `adkats_challenge_entry` (
                      `ID` int(10) unsigned NOT NULL AUTO_INCREMENT,
                      `PlayerID` int(10) unsigned NOT NULL,
                      `RuleID` int(10) unsigned NOT NULL,
                      `Completed` int(1) unsigned NOT NULL,
                      `Failed` int(1) unsigned NOT NULL,
                      `Canceled` int(1) unsigned NOT NULL,
                      `StartRound` int(10) unsigned NOT NULL,
                      `StartTime` datetime NOT NULL,
                      `CompleteTime` datetime NOT NULL,
                      PRIMARY KEY (`ID`),
                      KEY `adkats_challenge_entry_idx_PlayerID` (`PlayerID`),
                      KEY `adkats_challenge_entry_idx_RuleID` (`RuleID`),
                      KEY `adkats_challenge_entry_idx_StartTime` (`StartTime`),
                      KEY `adkats_challenge_entry_idx_CompleteTime` (`CompleteTime`),
                      CONSTRAINT `adkats_challenge_entry_fk_Play erID` FOREIGN KEY (`PlayerID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE,
                      CONSTRAINT `adkats_challenge_entry_fk_RuleID` FOREIGN KEY (`RuleID`) REFERENCES `adkats_challenge_rule` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Entries'", true);
            }
            if (!ConfirmTable("adkats_challenge_entry_detail"))
            {
                Log.Info("AdKats challenge entry detail table not found. Attempting to add.");
                SendNonQuery("Adding challenge entry detail table", @"
                    CREATE TABLE IF NOT EXISTS `adkats_challenge_entry_detail` (
                      `EntryID` int(10) unsigned NOT NULL,
                      `DetailID` int(10) unsigned NOT NULL,
                      `VictimID` int(10) unsigned NOT NULL,
                      `Weapon` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
                      `RoundID` int(10) unsigned NOT NULL,
                      `DetailTime` datetime NOT NULL,
                      PRIMARY KEY (`EntryID`, `DetailID`),
                      KEY `adkats_challenge_entry_detail_idx_VictimID` (`VictimID`),
                      KEY `adkats_challenge_entry_detail_idx_DetailTime` (`DetailTime`),
                      CONSTRAINT `adkats_challenge_entry_detail_fk_EntryID` FOREIGN KEY (`EntryID`) REFERENCES `adkats_challenge_entry` (`ID`) ON DELETE CASCADE ON UPDATE CASCADE,
                      CONSTRAINT `adkats_challenge_entry_detail_fk_VictimID` FOREIGN KEY (`VictimID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Entry Details'", true);
            }
            if (!ConfirmTable("adkats_challenge_reward"))
            {
                Log.Info("AdKats challenge reward table not found. Attempting to add.");
                SendNonQuery("Adding challenge reward table", @"
                    CREATE TABLE IF NOT EXISTS `adkats_challenge_reward` (
                      `ID` int(10) unsigned NOT NULL AUTO_INCREMENT,
                      `ServerID` smallint(5) unsigned NOT NULL,
                      `Tier` int(10) unsigned NOT NULL,
                      `Reward` varchar(100) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'None',
                      `Enabled` int(1) unsigned NOT NULL DEFAULT 0,
                      `DurationMinutes` int(10) unsigned NOT NULL DEFAULT 60, -- 4294967295
                      `CreateTime` datetime NOT NULL,
                      `ModifyTime` datetime NOT NULL,
                      PRIMARY KEY (`ID`),
                      UNIQUE (`ServerID`, `Tier`, `Reward`),
                      KEY `adkats_challenge_reward_idx_CreateTime` (`CreateTime`),
                      KEY `adkats_challenge_reward_idx_ModifyTime` (`ModifyTime`),
                      CONSTRAINT `adkats_challenge_reward_fk_ServerID` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE NO ACTION ON UPDATE CASCADE
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Challenge Rewards'", true);
            }
            SendNonQuery("Updating setting value length to 10000.", "ALTER TABLE adkats_settings MODIFY setting_value varchar(10000)", false);
            return ConfirmTable("adkats_bans") &&
                   ConfirmTable("adkats_commands") &&
                   ConfirmTable("adkats_infractions_global") &&
                   ConfirmTable("adkats_infractions_server") &&
                   ConfirmTable("adkats_records_debug") &&
                   ConfirmTable("adkats_records_main") &&
                   ConfirmTable("adkats_rolecommands") &&
                   ConfirmTable("adkats_roles") &&
                   ConfirmTable("adkats_settings") &&
                   ConfirmTable("adkats_users") &&
                   ConfirmTable("adkats_usersoldiers") &&
                   ConfirmTable("adkats_specialplayers") &&
                   ConfirmTable("adkats_player_reputation") &&
                   ConfirmTable("adkats_orchestration") &&
                   ConfirmTable("adkats_statistics") &&
                   ConfirmTable("adkats_rolegroups") &&
                   ConfirmTable("adkats_challenge_definition") &&
                   ConfirmTable("adkats_challenge_definition_detail") &&
                   ConfirmTable("adkats_challenge_rule") &&
                   ConfirmTable("adkats_challenge_entry") &&
                   ConfirmTable("adkats_challenge_entry_detail") &&
                   ConfirmTable("adkats_challenge_reward") &&
                   ConfirmTable("tbl_extendedroundstats") &&
                   !SendQuery("SELECT `TABLE_NAME` AS `table_name` FROM `INFORMATION_SCHEMA`.`TABLES` WHERE `TABLE_SCHEMA` = @Schema AND `TABLE_NAME` LIKE 'adkats_%' AND ENGINE <> 'InnoDB'", false, new { Schema = _mySqlSchemaName });
        }

        private Boolean ConfirmStatLoggerTables()
        {
            Boolean confirmed = true;
            //All versions of stat logger should have these tables
            if (ConfirmTable("tbl_playerdata") && ConfirmTable("tbl_server") && ConfirmTable("tbl_chatlog"))
            {
                //The universal version has a tbl_games table, detect that
                if (ConfirmTable("tbl_games"))
                {
                    _statLoggerVersion = "UNIVERSAL";
                    Boolean gameIDFound = false;
                    using (MySqlConnection connection = GetDatabaseConnection())
                    {
                        var rows = connection.Query(@"
                            SELECT
                                `GameID` AS `game_id`,
                                `Name` AS `game_name`
                            FROM
                                `tbl_games`");
                        lock (_gameIDDictionary)
                        {
                            _gameIDDictionary.Clear();
                            foreach (var row in rows)
                            {
                                String gameName = (String)row.game_name;
                                Int32 gameID = (Int32)row.game_id;
                                if (!_gameIDDictionary.ContainsKey(gameID))
                                {
                                    if (GameVersion.ToString() == gameName)
                                    {
                                        _serverInfo.GameID = gameID;
                                        gameIDFound = true;
                                    }
                                    switch (gameName)
                                    {
                                        case "BF3":
                                            _gameIDDictionary.Add(gameID, GameVersionEnum.BF3);
                                            break;
                                        case "BF4":
                                            _gameIDDictionary.Add(gameID, GameVersionEnum.BF4);
                                            break;
                                        case "BFHL":
                                            _gameIDDictionary.Add(gameID, GameVersionEnum.BFHL);
                                            break;
                                        default:
                                            Log.Error("Game name " + gameName + " not recognized.");
                                            break;
                                    }
                                }
                            }
                        }
                        confirmed = gameIDFound;
                    }
                }
            }
            else
            {
                confirmed = false;
            }
            return confirmed;
        }

        private Boolean ConfirmTable(String tableName)
        {
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var result = connection.QueryFirstOrDefault<dynamic>(
                        "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table",
                        new { schema = _mySqlSchemaName, table = tableName });
                    return result != null;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while confirming table '" + tableName + "'", e));
                return false;
            }
        }

        private void UploadAllSettings()
        {
            if (!_pluginEnabled)
            {
                return;
            }
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                Log.Debug(() => "uploadAllSettings starting!", 6);
                QueueSettingForUpload(new CPluginVariable(@"Auto-Enable/Keep-Alive", typeof(Boolean), _useKeepAlive));
                QueueSettingForUpload(new CPluginVariable(@"Override Timing Confirmation", typeof(Boolean), _timingValidOverride));
                QueueSettingForUpload(new CPluginVariable(@"Debug level", typeof(int), Log.DebugLevel));
                QueueSettingForUpload(new CPluginVariable(@"Server VOIP Address", typeof(String), _ServerVoipAddress));
                QueueSettingForUpload(new CPluginVariable(@"Rule Print Delay", typeof(Double), _ServerRulesDelay));
                QueueSettingForUpload(new CPluginVariable(@"Rule Print Interval", typeof(Double), _ServerRulesInterval));
                QueueSettingForUpload(new CPluginVariable(@"Server Rule List", typeof(String), CPluginVariable.EncodeStringArray(_ServerRulesList)));
                QueueSettingForUpload(new CPluginVariable(@"Server Rule Numbers", typeof(Boolean), _ServerRulesNumbers));
                QueueSettingForUpload(new CPluginVariable(@"Yell Server Rules", typeof(Boolean), _ServerRulesYell));
                QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Whitelist", typeof(Boolean), _FeedMultiBalancerWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Even Dispersion List", typeof(Boolean), _FeedMultiBalancerDisperseList));
                QueueSettingForUpload(new CPluginVariable(@"Automatic MULTIBalancer Whitelist for Admins", typeof(Boolean), _FeedMultiBalancerWhitelist_Admins));
                QueueSettingForUpload(new CPluginVariable(@"Feed BF4DB Whitelist", typeof(Boolean), _FeedBF4DBWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Feed BattlefieldAgency Whitelist", typeof(Boolean), _FeedBAWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Feed TeamKillTracker Whitelist", typeof(Boolean), _FeedTeamKillTrackerWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Automatic TeamKillTracker Whitelist for Admins", typeof(Boolean), _FeedTeamKillTrackerWhitelist_Admins));
                QueueSettingForUpload(new CPluginVariable(@"Automatic Reserved Slot for Admins", typeof(Boolean), _FeedServerReservedSlots_Admins));
                QueueSettingForUpload(new CPluginVariable(@"Automatic VIP Kick Whitelist for Admins", typeof(Boolean), _FeedServerReservedSlots_Admins_VIPKickWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Send new reserved slots to VIP Slot Manager", typeof(Boolean), _FeedServerReservedSlots_VSM));
                QueueSettingForUpload(new CPluginVariable(@"Automatic Spectator Slot for Admins", typeof(Boolean), _FeedServerSpectatorList_Admins));
                QueueSettingForUpload(new CPluginVariable(@"Feed Server Reserved Slots", typeof(Boolean), _FeedServerReservedSlots));
                QueueSettingForUpload(new CPluginVariable(@"Feed Server Spectator List", typeof(Boolean), _FeedServerSpectatorList));
                QueueSettingForUpload(new CPluginVariable(@"Feed Stat Logger Settings", typeof(Boolean), _FeedStatLoggerSettings));
                QueueSettingForUpload(new CPluginVariable(@"Post Stat Logger Chat Manually", typeof(Boolean), _PostStatLoggerChatManually));
                QueueSettingForUpload(new CPluginVariable(@"Post Server Chat Spam", typeof(Boolean), _PostStatLoggerChatManually_PostServerChatSpam));
                QueueSettingForUpload(new CPluginVariable(@"Exclude Commands from Chat Logs", typeof(Boolean), _PostStatLoggerChatManually_IgnoreCommands));
                QueueSettingForUpload(new CPluginVariable(@"Post Map Benefit/Detriment Statistics", typeof(Boolean), _PostMapBenefitStatistics));
                // Populator Monitor
                QueueSettingForUpload(new CPluginVariable(@"Monitor Populator Players", typeof(Boolean), _PopulatorMonitor));
                QueueSettingForUpload(new CPluginVariable(@"Monitor Specified Populators Only", typeof(Boolean), _PopulatorUseSpecifiedPopulatorsOnly));
                QueueSettingForUpload(new CPluginVariable(@"Monitor Populators of This Server Only", typeof(Boolean), _PopulatorPopulatingThisServerOnly));
                QueueSettingForUpload(new CPluginVariable(@"Count to Consider Populator Past Week", typeof(Int32), _PopulatorMinimumPopulationCountPastWeek));
                QueueSettingForUpload(new CPluginVariable(@"Count to Consider Populator Past 2 Weeks", typeof(Int32), _PopulatorMinimumPopulationCountPast2Weeks));
                QueueSettingForUpload(new CPluginVariable(@"Enable Populator Perks", typeof(Boolean), _PopulatorPerksEnable));
                QueueSettingForUpload(new CPluginVariable(@"Populator Perks - Reserved Slot", typeof(Boolean), _PopulatorPerksReservedSlot));
                QueueSettingForUpload(new CPluginVariable(@"Populator Perks - Autobalance Whitelist", typeof(Boolean), _PopulatorPerksBalanceWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Populator Perks - Ping Whitelist", typeof(Boolean), _PopulatorPerksPingWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Populator Perks - TeamKillTracker Whitelist", typeof(Boolean), _PopulatorPerksTeamKillTrackerWhitelist));
                // Teamspeak Monitor
                QueueSettingForUpload(new CPluginVariable(@"Monitor Teamspeak Players", typeof(Boolean), _TeamspeakPlayerMonitorView));
                QueueSettingForUpload(new CPluginVariable(@"Enable Teamspeak Player Monitor", typeof(Boolean), _TeamspeakPlayerMonitorEnable));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server IP", typeof(String), _TeamspeakManager.Ts3ServerIp));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Port", typeof(Int32), _TeamspeakManager.Ts3ServerPort));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Query Port", typeof(Int32), _TeamspeakManager.Ts3QueryPort));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Query Username", typeof(String), _TeamspeakManager.Ts3QueryUsername));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Query Password", typeof(String), _TeamspeakManager.Ts3QueryPassword));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Server Query Nickname", typeof(String), _TeamspeakManager.Ts3QueryNickname));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Main Channel Name", typeof(String), _TeamspeakManager.Ts3MainChannelName));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Secondary Channel Names", typeof(String), CPluginVariable.EncodeStringArray(_TeamspeakManager.Ts3SubChannelNames)));
                QueueSettingForUpload(new CPluginVariable(@"Debug Display Teamspeak Clients", typeof(Boolean), _TeamspeakManager.DebugClients));
                QueueSettingForUpload(new CPluginVariable(@"TeamSpeak Player Join Announcement", typeof(String), _TeamspeakManager.JoinDisplay.ToString()));
                QueueSettingForUpload(new CPluginVariable(@"TeamSpeak Player Join Message", typeof(String), _TeamspeakManager.JoinDisplayMessage));
                QueueSettingForUpload(new CPluginVariable(@"TeamSpeak Player Update Seconds", typeof(Int32), _TeamspeakManager.UpdateIntervalSeconds));
                QueueSettingForUpload(new CPluginVariable(@"Enable Teamspeak Player Perks", typeof(Boolean), _TeamspeakPlayerPerksEnable));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Player Perks - VIP Kick Whitelist", typeof(Boolean), _TeamspeakPlayerPerksVIPKickWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Player Perks - Autobalance Whitelist", typeof(Boolean), _TeamspeakPlayerPerksBalanceWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Player Perks - Ping Whitelist", typeof(Boolean), _TeamspeakPlayerPerksPingWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Teamspeak Player Perks - TeamKillTracker Whitelist", typeof(Boolean), _TeamspeakPlayerPerksTeamKillTrackerWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Enable", typeof(Boolean), _TeamspeakOnlinePlayersEnable));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Interval Minutes", typeof(Int32), _TeamspeakOnlinePlayersInterval));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Max Players to List", typeof(Int32), _TeamspeakOnlinePlayersMaxPlayersToList));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Single Player Message", typeof(String), _TeamspeakOnlinePlayersAloneMessage));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Teamspeak Players - Multi Player Message", typeof(String), _TeamspeakOnlinePlayersMessage));
                // Discord Monitor
                QueueSettingForUpload(new CPluginVariable(@"Monitor Discord Players", typeof(Boolean), _DiscordPlayerMonitorView));
                QueueSettingForUpload(new CPluginVariable(@"Enable Discord Player Monitor", typeof(Boolean), _DiscordPlayerMonitorEnable));
                QueueSettingForUpload(new CPluginVariable(@"Discord API URL", typeof(String), _DiscordManager.APIUrl));
                QueueSettingForUpload(new CPluginVariable(@"Discord Server ID", typeof(String), _DiscordManager.ServerID));
                QueueSettingForUpload(new CPluginVariable(@"Discord Channel Names", typeof(String), CPluginVariable.EncodeStringArray(_DiscordManager.ChannelNames)));
                QueueSettingForUpload(new CPluginVariable(@"Require Voice in Discord to Issue Admin Commands", typeof(Boolean), _DiscordPlayerRequireVoiceForAdmin));
                QueueSettingForUpload(new CPluginVariable(@"Discord Player Join Announcement", typeof(String), _DiscordManager.JoinDisplay.ToString()));
                QueueSettingForUpload(new CPluginVariable(@"Discord Player Join Message", typeof(String), _DiscordManager.JoinMessage));
                QueueSettingForUpload(new CPluginVariable(@"Enable Discord Player Perks", typeof(Boolean), _DiscordPlayerPerksEnable));
                QueueSettingForUpload(new CPluginVariable(@"Discord Player Perks - VIP Kick Whitelist", typeof(Boolean), _DiscordPlayerPerksVIPKickWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Discord Player Perks - Autobalance Whitelist", typeof(Boolean), _DiscordPlayerPerksBalanceWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Discord Player Perks - Ping Whitelist", typeof(Boolean), _DiscordPlayerPerksPingWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Discord Player Perks - TeamKillTracker Whitelist", typeof(Boolean), _DiscordPlayerPerksTeamKillTrackerWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Enable", typeof(Boolean), _DiscordOnlinePlayersEnable));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Interval Minutes", typeof(Int32), _DiscordOnlinePlayersInterval));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Max Players to List", typeof(Int32), _DiscordOnlinePlayersMaxPlayersToList));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Single Player Message", typeof(String), _DiscordOnlinePlayersAloneMessage));
                QueueSettingForUpload(new CPluginVariable(@"Announce Online Discord Players - Multi Player Message", typeof(String), _DiscordOnlinePlayersMessage));
                QueueSettingForUpload(new CPluginVariable(@"Debug Display Discord Members", typeof(Boolean), _DiscordManager.DebugMembers));
                // Discord Watchlist Settings
                QueueSettingForUpload(new CPluginVariable(@"Send Watchlist Announcements to Discord WebHook", typeof(Boolean), _UseDiscordForWatchlist));
                QueueSettingForUpload(new CPluginVariable(@"Discord Watchlist WebHook URL", typeof(String), _DiscordManager.WatchlistWebhookUrl));
                QueueSettingForUpload(new CPluginVariable(@"Announce Watchlist Leaves on Discord", typeof(Boolean), _DiscordWatchlistLeftEnabled));
                QueueSettingForUpload(new CPluginVariable(@"Discord Role IDs to Mention in Watchlist Announcements", typeof(String[]), _DiscordManager.RoleIDsToMentionWatchlist.ToArray()));
                // Team Power Monitor
                QueueSettingForUpload(new CPluginVariable(@"Team Power Active Influence", typeof(Double), _TeamPowerActiveInfluence));
                QueueSettingForUpload(new CPluginVariable(@"Display Team Power In Procon Chat", typeof(Boolean), _UseTeamPowerDisplayBalance));
                QueueSettingForUpload(new CPluginVariable(@"Enable Team Power Scrambler", typeof(Boolean), _UseTeamPowerMonitorScrambler));
                QueueSettingForUpload(new CPluginVariable(@"Enable Team Power Join Reassignment", typeof(Boolean), _UseTeamPowerMonitorReassign));
                QueueSettingForUpload(new CPluginVariable(@"Team Power Join Reassignment Leniency", typeof(Boolean), _UseTeamPowerMonitorReassignLenient));
                QueueSettingForUpload(new CPluginVariable(@"Team Power Join Reassignment Leniency Percent", typeof(Double), _TeamPowerMonitorReassignLenientPercent));
                QueueSettingForUpload(new CPluginVariable(@"Enable Team Power Unswitcher", typeof(Boolean), _UseTeamPowerMonitorUnswitcher));
                QueueSettingForUpload(new CPluginVariable(@"Enable Team Power Seeder Control", typeof(Boolean), _UseTeamPowerMonitorSeeders));
                QueueSettingForUpload(new CPluginVariable(@"Round Timer: Enable", typeof(Boolean), _useRoundTimer));
                QueueSettingForUpload(new CPluginVariable(@"Round Timer: Round Duration Minutes", typeof(Double), _maxRoundTimeMinutes));
                QueueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof(Boolean), _UseWeaponLimiter));
                QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Weapon String", typeof(String), _WeaponLimiterString));
                QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Exception String", typeof(String), _WeaponLimiterExceptionString));
                QueueSettingForUpload(new CPluginVariable(@"Use AA Report Auto Handler", typeof(Boolean), _UseAAReportAutoHandler));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Report-Handler Strings", typeof(String), CPluginVariable.EncodeStringArray(_AutoReportHandleStrings)));
                QueueSettingForUpload(new CPluginVariable(@"Use Grenade Cook Catcher", typeof(Boolean), _UseGrenadeCookCatcher));
                QueueSettingForUpload(new CPluginVariable(@"Automatically Poll Server For Event Options", typeof(Boolean), _EventPollAutomatic));
                QueueSettingForUpload(new CPluginVariable(@"Max Automatic Polls Per Event", typeof(Double), _EventRoundAutoPollsMax));
                QueueSettingForUpload(new CPluginVariable(@"Yell Current Winning Rule Option", typeof(Boolean), _eventPollYellWinningRule));
                QueueSettingForUpload(new CPluginVariable(@"Weekly Events", typeof(Boolean), _EventWeeklyRepeat));
                QueueSettingForUpload(new CPluginVariable(@"Event Day", typeof(String), _EventWeeklyDay.ToString()));
                QueueSettingForUpload(new CPluginVariable(@"Event Date", typeof(String), _EventDate.ToShortDateString()));
                QueueSettingForUpload(new CPluginVariable(@"Event Hour in 24 format", typeof(Double), _EventHour));
                QueueSettingForUpload(new CPluginVariable(@"Event Test Round Number", typeof(Int32), _EventTestRoundNumber));
                QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                QueueSettingForUpload(new CPluginVariable(@"Event Announce Day Difference", typeof(Double), _EventAnnounceDayDifference));
                QueueSettingForUpload(new CPluginVariable(@"Event Round Codes", typeof(String[]), _EventRoundOptions.Select(round => round.getCode()).ToArray()));
                QueueSettingForUpload(new CPluginVariable(@"Poll Max Option Count", typeof(Double), _EventPollMaxOptions));
                QueueSettingForUpload(new CPluginVariable(@"Event Round Poll Codes", typeof(String[]), _EventRoundPollOptions.Select(option => option.getCode()).ToArray()));
                QueueSettingForUpload(new CPluginVariable(@"Event Base Server Name", typeof(String), _eventBaseServerName));
                QueueSettingForUpload(new CPluginVariable(@"Event Countdown Server Name", typeof(String), _eventCountdownServerName));
                QueueSettingForUpload(new CPluginVariable(@"Event Concrete Countdown Server Name", typeof(String), _eventConcreteCountdownServerName));
                QueueSettingForUpload(new CPluginVariable(@"Event Active Server Name", typeof(String), _eventActiveServerName));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Kick Players Who First Joined After This Date", typeof(String), _AutoKickNewPlayerDate.ToShortDateString()));
                QueueSettingForUpload(new CPluginVariable(@"Use LIVE Anti Cheat System", typeof(Boolean), _useAntiCheatLIVESystem));
                QueueSettingForUpload(new CPluginVariable(@"LIVE System Includes Mass Murder and Aimbot Checks", typeof(Boolean), _AntiCheatLIVESystemActiveStats));
                QueueSettingForUpload(new CPluginVariable(@"DPS Checker: Ban Message", typeof(String), _AntiCheatDPSBanMessage));
                QueueSettingForUpload(new CPluginVariable(@"HSK Checker: Enable", typeof(Boolean), _UseHskChecker));
                QueueSettingForUpload(new CPluginVariable(@"HSK Checker: Trigger Level", typeof(Double), _HskTriggerLevel));
                QueueSettingForUpload(new CPluginVariable(@"HSK Checker: Ban Message", typeof(String), _AntiCheatHSKBanMessage));
                QueueSettingForUpload(new CPluginVariable(@"KPM Checker: Enable", typeof(Boolean), _UseKpmChecker));
                QueueSettingForUpload(new CPluginVariable(@"KPM Checker: Trigger Level", typeof(Double), _KpmTriggerLevel));
                QueueSettingForUpload(new CPluginVariable(@"KPM Checker: Ban Message", typeof(String), _AntiCheatKPMBanMessage));
                QueueSettingForUpload(new CPluginVariable(@"AdkatsLRT Extension Token", typeof(String), _AdKatsLRTExtensionToken));
                QueueSettingForUpload(new CPluginVariable(@"Fetch Actions from Database", typeof(Boolean), _fetchActionsFromDb));
                QueueSettingForUpload(new CPluginVariable(@"Use Additional Ban Message", typeof(Boolean), _UseBanAppend));
                QueueSettingForUpload(new CPluginVariable(@"Additional Ban Message", typeof(String), _BanAppend));
                QueueSettingForUpload(new CPluginVariable(@"Procon Ban Admin Name", typeof(String), _CBanAdminName));
                QueueSettingForUpload(new CPluginVariable(@"Use Ban Enforcer", typeof(Boolean), _UseBanEnforcer));
                QueueSettingForUpload(new CPluginVariable(@"Ban Enforcer BF4 Lenient Kick", typeof(Boolean), _BanEnforcerBF4LenientKick));
                QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by NAME", typeof(Boolean), _DefaultEnforceName));
                QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by GUID", typeof(Boolean), _DefaultEnforceGUID));
                QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by IP", typeof(Boolean), _DefaultEnforceIP));
                QueueSettingForUpload(new CPluginVariable(@"Countdown Duration before a Nuke is fired", typeof(Int32), _NukeCountdownDurationSeconds));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Required Reason Length", typeof(Int32), _RequiredReasonLength));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Report Handle Seconds", typeof(Int32), _MinimumReportHandleSeconds));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Minutes Into Round To Use Assist", typeof(Int32), _minimumAssistMinutes));
                QueueSettingForUpload(new CPluginVariable(@"Allow Commands from Admin Say", typeof(Boolean), _AllowAdminSayCommands));
                QueueSettingForUpload(new CPluginVariable(@"Reserved slot grants access to squad lead command", typeof(Boolean), _ReservedSquadLead));
                QueueSettingForUpload(new CPluginVariable(@"Reserved slot grants access to self-move command", typeof(Boolean), _ReservedSelfMove));
                QueueSettingForUpload(new CPluginVariable(@"Reserved slot grants access to self-kill command", typeof(Boolean), _ReservedSelfKill));
                QueueSettingForUpload(new CPluginVariable(@"Banned Tags", typeof(String), CPluginVariable.EncodeStringArray(_BannedTags)));
                QueueSettingForUpload(new CPluginVariable(@"Punishment Hierarchy", typeof(String), CPluginVariable.EncodeStringArray(_PunishmentHierarchy)));
                QueueSettingForUpload(new CPluginVariable(@"Combine Server Punishments", typeof(Boolean), _CombineServerPunishments));
                QueueSettingForUpload(new CPluginVariable(@"Automatic Forgives", typeof(Boolean), _AutomaticForgives));
                QueueSettingForUpload(new CPluginVariable(@"Only Kill Players when Server in low population", typeof(Boolean), _OnlyKillOnLowPop));
                QueueSettingForUpload(new CPluginVariable(@"Short Server Name", typeof(String), _shortServerName));
                QueueSettingForUpload(new CPluginVariable(@"Low Population Value", typeof(Int32), _lowPopulationPlayerCount));
                QueueSettingForUpload(new CPluginVariable(@"High Population Value", typeof(Int32), _highPopulationPlayerCount));
                QueueSettingForUpload(new CPluginVariable(@"Automatic Server Restart When Empty", typeof(Boolean), _automaticServerRestart));
                QueueSettingForUpload(new CPluginVariable(@"Automatic Restart Minimum Uptime Hours", typeof(Int32), _automaticServerRestartMinHours));
                QueueSettingForUpload(new CPluginVariable(@"Automatic Procon Reboot When Server Reboots", typeof(Boolean), _automaticServerRestartProcon));
                QueueSettingForUpload(new CPluginVariable(@"Procon Memory Usage MB Warning", typeof(Int32), _MemoryUsageWarn));
                QueueSettingForUpload(new CPluginVariable(@"Procon Memory Usage MB AdKats Restart", typeof(Int32), _MemoryUsageRestartPlugin));
                QueueSettingForUpload(new CPluginVariable(@"Procon Memory Usage MB Procon Restart", typeof(Int32), _MemoryUsageRestartProcon));
                QueueSettingForUpload(new CPluginVariable(@"Use IRO Punishment", typeof(Boolean), _IROActive));
                QueueSettingForUpload(new CPluginVariable(@"IRO Punishment Overrides Low Pop", typeof(Boolean), _IROOverridesLowPop));
                QueueSettingForUpload(new CPluginVariable(@"IRO Punishment Infractions Required to Override", typeof(Int32), _IROOverridesLowPopInfractions));
                QueueSettingForUpload(new CPluginVariable(@"IRO Timeout Minutes", typeof(Int32), _IROTimeout));
                QueueSettingForUpload(new CPluginVariable(@"Maximum Temp-Ban Duration Minutes", typeof(Double), _MaxTempBanDuration.TotalMinutes));
                QueueSettingForUpload(new CPluginVariable(@"Send Emails", typeof(Boolean), _UseEmail));
                QueueSettingForUpload(new CPluginVariable(@"Use SSL?", typeof(Boolean), _EmailHandler.UseSSL));
                QueueSettingForUpload(new CPluginVariable(@"SMTP-Server address", typeof(String), _EmailHandler.SMTPServer));
                QueueSettingForUpload(new CPluginVariable(@"SMTP-Server port", typeof(Int32), _EmailHandler.SMTPPort));
                QueueSettingForUpload(new CPluginVariable(@"Sender address", typeof(String), _EmailHandler.SenderEmail));
                QueueSettingForUpload(new CPluginVariable(@"SMTP-Server username", typeof(String), _EmailHandler.SMTPUser));
                QueueSettingForUpload(new CPluginVariable(@"SMTP-Server password", typeof(String), _EmailHandler.SMTPPassword));
                QueueSettingForUpload(new CPluginVariable(@"Custom HTML Addition", typeof(String), _EmailHandler.CustomHTMLAddition));
                QueueSettingForUpload(new CPluginVariable(@"Extra Recipient Email Addresses", typeof(String[]), _EmailHandler.RecipientEmails.ToArray()));
                QueueSettingForUpload(new CPluginVariable(@"Only Send Report Emails When Admins Offline", typeof(Boolean), _EmailReportsOnlyWhenAdminless));
                QueueSettingForUpload(new CPluginVariable(@"Send PushBullet Reports", typeof(Boolean), _UsePushBullet));
                QueueSettingForUpload(new CPluginVariable(@"PushBullet Access Token", typeof(String), _PushBulletHandler.AccessToken));
                QueueSettingForUpload(new CPluginVariable(@"PushBullet Note Target", typeof(String), _PushBulletHandler.DefaultTarget.ToString()));
                QueueSettingForUpload(new CPluginVariable(@"PushBullet Channel Tag", typeof(String), _PushBulletHandler.DefaultChannelTag));
                QueueSettingForUpload(new CPluginVariable(@"Only Send PushBullet Reports When Admins Offline", typeof(Boolean), _PushBulletReportsOnlyWhenAdminless));
                QueueSettingForUpload(new CPluginVariable(@"Send Reports to Discord WebHook", typeof(Boolean), _UseDiscordForReports));
                QueueSettingForUpload(new CPluginVariable(@"Discord WebHook URL", typeof(String), _DiscordManager.ReportWebhookUrl));
                QueueSettingForUpload(new CPluginVariable(@"Only Send Discord Reports When Admins Offline", typeof(Boolean), _DiscordReportsOnlyWhenAdminless));
                QueueSettingForUpload(new CPluginVariable(@"Send update if reported players leave without action", typeof(Boolean), _DiscordReportsLeftWithoutAction));
                QueueSettingForUpload(new CPluginVariable(@"Discord Role IDs to Mention in Reports", typeof(String[]), _DiscordManager.RoleIDsToMentionReport.ToArray()));
                QueueSettingForUpload(new CPluginVariable(@"On-Player-Muted Message", typeof(String), _MutedPlayerMuteMessage));
                QueueSettingForUpload(new CPluginVariable(@"On-Player-Killed Message", typeof(String), _MutedPlayerKillMessage));
                QueueSettingForUpload(new CPluginVariable(@"On-Player-Kicked Message", typeof(String), _MutedPlayerKickMessage));
                QueueSettingForUpload(new CPluginVariable(@"Persistent On-Player-Killed Message", typeof(String), _PersistentMutedPlayerKillMessage));
                QueueSettingForUpload(new CPluginVariable(@"Persistent On-Player-Kicked Message", typeof(String), _PersistentMutedPlayerKickMessage));
                QueueSettingForUpload(new CPluginVariable(@"On-Player-Unmuted Message", typeof(String), _UnMutePlayerMessage));
                QueueSettingForUpload(new CPluginVariable(@"# Chances to give player before kicking", typeof(Int32), _MutedPlayerChances));
                QueueSettingForUpload(new CPluginVariable(@"# Chances to give persistent muted player before kicking", typeof(Int32), _PersistentMutedPlayerChances));
                QueueSettingForUpload(new CPluginVariable(@"Ignore commands for mute enforcement", typeof(Boolean), _MutedPlayerIgnoreCommands));
                QueueSettingForUpload(new CPluginVariable(@"Send first spawn warning for persistent muted players", typeof(Boolean), _UseFirstSpawnMutedMessage));
                QueueSettingForUpload(new CPluginVariable(@"First spawn persistent muted warning text", typeof(String), _FirstSpawnMutedMessage));
                QueueSettingForUpload(new CPluginVariable(@"Persistent force mute temp-ban duration minutes", typeof(Int32), _ForceMuteBanDuration));
                QueueSettingForUpload(new CPluginVariable(@"Ticket Window High", typeof(Int32), _TeamSwapTicketWindowHigh));
                QueueSettingForUpload(new CPluginVariable(@"Ticket Window Low", typeof(Int32), _TeamSwapTicketWindowLow));
                QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistants", typeof(Boolean), _EnableAdminAssistants));
                QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistant Perk", typeof(Boolean), _EnableAdminAssistantPerk));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Confirmed Reports Per Month", typeof(Int32), _MinimumRequiredMonthlyReports));
                QueueSettingForUpload(new CPluginVariable(@"Yell display time seconds", typeof(Int32), _YellDuration));
                QueueSettingForUpload(new CPluginVariable(@"Pre-Message List", typeof(String), CPluginVariable.EncodeStringArray(_PreMessageList.ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"Require Use of Pre-Messages", typeof(Boolean), _RequirePreMessageUse));
                QueueSettingForUpload(new CPluginVariable(@"Use first spawn message", typeof(Boolean), _UseFirstSpawnMessage));
                QueueSettingForUpload(new CPluginVariable(@"First spawn message text", typeof(String), _FirstSpawnMessage));
                QueueSettingForUpload(new CPluginVariable(@"Enable Alternative Spawn Message for Low Server Playtime", typeof(Boolean), _EnableLowPlaytimeSpawnMessage));
                QueueSettingForUpload(new CPluginVariable(@"Low Server Playtime Spawn Message Threshold Hours", typeof(Int32), _LowPlaytimeSpawnMessageHours));
                QueueSettingForUpload(new CPluginVariable(@"Low Server Playtime Spawn Message Text", typeof(String), _LowPlaytimeSpawnMessage));
                QueueSettingForUpload(new CPluginVariable(@"Use First Spawn Reputation and Infraction Message", typeof(Boolean), _useFirstSpawnRepMessage));
                QueueSettingForUpload(new CPluginVariable(@"Use Perk Expiration Notification", typeof(Boolean), _UsePerkExpirationNotify));
                QueueSettingForUpload(new CPluginVariable(@"Perk Expiration Notify Days Remaining", typeof(Int32), _PerkExpirationNotifyDays));
                QueueSettingForUpload(new CPluginVariable(@"SpamBot Enable", typeof(Boolean), _spamBotEnabled));
                QueueSettingForUpload(new CPluginVariable(@"SpamBot Say List", typeof(String), CPluginVariable.EncodeStringArray(_spamBotSayList.ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"SpamBot Say Delay Seconds", typeof(Int32), _spamBotSayDelaySeconds));
                QueueSettingForUpload(new CPluginVariable(@"SpamBot Yell List", typeof(String), CPluginVariable.EncodeStringArray(_spamBotYellList.ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"SpamBot Yell Delay Seconds", typeof(Int32), _spamBotYellDelaySeconds));
                QueueSettingForUpload(new CPluginVariable(@"SpamBot Tell List", typeof(String), CPluginVariable.EncodeStringArray(_spamBotTellList.ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"SpamBot Tell Delay Seconds", typeof(Int32), _spamBotTellDelaySeconds));
                QueueSettingForUpload(new CPluginVariable(@"Exclude Whitelist from Spam", typeof(Boolean), _spamBotExcludeWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Exclude Admins from Spam", typeof(Boolean), _spamBotExcludeAdmins));
                QueueSettingForUpload(new CPluginVariable(@"Exclude Teamspeak and Discord Players from Spam", typeof(Boolean), _spamBotExcludeTeamspeakDiscord));
                QueueSettingForUpload(new CPluginVariable(@"Exclude High Reputation Players from Spam", typeof(Boolean), _spamBotExcludeHighReputation));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Server Playtime in Hours for Receiving Spam", typeof(Int32), _spamBotMinPlaytimeHours));
                QueueSettingForUpload(new CPluginVariable(@"Player Battlecry Volume", typeof(String), _battlecryVolume.ToString()));
                QueueSettingForUpload(new CPluginVariable(@"Player Battlecry Max Length", typeof(Int32), _battlecryMaxLength));
                QueueSettingForUpload(new CPluginVariable(@"Player Battlecry Denied Words", typeof(String), CPluginVariable.EncodeStringArray(_battlecryDeniedWords)));
                QueueSettingForUpload(new CPluginVariable(@"Display Admin Name in Kick and Ban Announcement", typeof(Boolean), _ShowAdminNameInAnnouncement));
                QueueSettingForUpload(new CPluginVariable(@"Display New Player Announcement", typeof(Boolean), _ShowNewPlayerAnnouncement));
                QueueSettingForUpload(new CPluginVariable(@"Display Player Name Change Announcement", typeof(Boolean), _ShowPlayerNameChangeAnnouncement));
                QueueSettingForUpload(new CPluginVariable(@"Display Targeted Player Left Notification", typeof(Boolean), _ShowTargetedPlayerLeftNotification));
                QueueSettingForUpload(new CPluginVariable(@"Inform players of reports against them", typeof(Boolean), _InformReportedPlayers));
                QueueSettingForUpload(new CPluginVariable(@"Inform reputable players of admin joins", typeof(Boolean), _InformReputablePlayersOfAdminJoins));
                QueueSettingForUpload(new CPluginVariable(@"Inform admins of admin joins", typeof(Boolean), _InformAdminsOfAdminJoins));
                QueueSettingForUpload(new CPluginVariable(@"Use All Caps Limiter", typeof(Boolean), _UseAllCapsLimiter));
                QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Only Limit Specified Players", typeof(Boolean), _AllCapsLimiterSpecifiedPlayersOnly));
                QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Character Percentage", typeof(Int32), _AllCapsLimterPercentage));
                QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Minimum Characters", typeof(Int32), _AllCapsLimterMinimumCharacters));
                QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Warn Threshold", typeof(Int32), _AllCapsLimiterWarnThreshold));
                QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Kill Threshold", typeof(Int32), _AllCapsLimiterKillThreshold));
                QueueSettingForUpload(new CPluginVariable(@"All Caps Limiter Kick Threshold", typeof(Int32), _AllCapsLimiterKickThreshold));
                QueueSettingForUpload(new CPluginVariable(@"Player Inform Exclusion Strings", typeof(String), CPluginVariable.EncodeStringArray(_PlayerInformExclusionStrings)));
                QueueSettingForUpload(new CPluginVariable(@"Disable Automatic Updates", typeof(Boolean), _automaticUpdatesDisabled));
                QueueSettingForUpload(new CPluginVariable(@"Enforce Single Instance", typeof(Boolean), _enforceSingleInstance));
                QueueSettingForUpload(new CPluginVariable(@"AFK System Enable", typeof(Boolean), _AFKManagerEnable));
                QueueSettingForUpload(new CPluginVariable(@"AFK Ignore Chat", typeof(Boolean), _AFKIgnoreChat));
                QueueSettingForUpload(new CPluginVariable(@"AFK Auto-Kick Enable", typeof(Boolean), _AFKAutoKickEnable));
                QueueSettingForUpload(new CPluginVariable(@"AFK Trigger Minutes", typeof(Double), _AFKTriggerDurationMinutes));
                QueueSettingForUpload(new CPluginVariable(@"AFK Minimum Players", typeof(Int32), _AFKTriggerMinimumPlayers));
                QueueSettingForUpload(new CPluginVariable(@"AFK Ignore User List", typeof(Boolean), _AFKIgnoreUserList));
                QueueSettingForUpload(new CPluginVariable(@"AFK Ignore Roles", typeof(String), CPluginVariable.EncodeStringArray(_AFKIgnoreRoles)));
                QueueSettingForUpload(new CPluginVariable(@"Ping Enforcer Enable", typeof(Boolean), _pingEnforcerEnable));
                QueueSettingForUpload(new CPluginVariable(@"Ping Moving Average Duration sec", typeof(Double), _pingMovingAverageDurationSeconds));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Low Population Trigger ms", typeof(Double), _pingEnforcerLowTriggerMS));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Medium Population Trigger ms", typeof(Double), _pingEnforcerMedTriggerMS));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick High Population Trigger ms", typeof(Double), _pingEnforcerHighTriggerMS));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Full Population Trigger ms", typeof(Double), _pingEnforcerFullTriggerMS));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Low Population Time Modifier", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerLowTimeModifier.Select(x => x.ToString()).ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Medium Population Time Modifier", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerMedTimeModifier.Select(x => x.ToString()).ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick High Population Time Modifier", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerHighTimeModifier.Select(x => x.ToString()).ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Full Population Time Modifier", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerFullTimeModifier.Select(x => x.ToString()).ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Minimum Players", typeof(Int32), _pingEnforcerTriggerMinimumPlayers));
                QueueSettingForUpload(new CPluginVariable(@"Kick Missing Pings", typeof(Boolean), _pingEnforcerKickMissingPings));
                QueueSettingForUpload(new CPluginVariable(@"Attempt Manual Ping when Missing", typeof(Boolean), _attemptManualPingWhenMissing));
                QueueSettingForUpload(new CPluginVariable(@"Display Ping Enforcer Messages In Procon Chat", typeof(Boolean), _pingEnforcerDisplayProconChat));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Ignore User List", typeof(Boolean), _pingEnforcerIgnoreUserList));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Ignore Roles", typeof(String), CPluginVariable.EncodeStringArray(_pingEnforcerIgnoreRoles)));
                QueueSettingForUpload(new CPluginVariable(@"Ping Kick Message Prefix", typeof(String), _pingEnforcerMessagePrefix));
                QueueSettingForUpload(new CPluginVariable(@"Commander Manager Enable", typeof(Boolean), _CMDRManagerEnable));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Players to Allow Commanders", typeof(Int32), _CMDRMinimumPlayers));
                QueueSettingForUpload(new CPluginVariable(@"Player Lock Manual Duration Minutes", typeof(Double), _playerLockingManualDuration));
                QueueSettingForUpload(new CPluginVariable(@"Automatically Lock Players on Admin Action", typeof(Boolean), _playerLockingAutomaticLock));
                QueueSettingForUpload(new CPluginVariable(@"Player Lock Automatic Duration Minutes", typeof(Double), _playerLockingAutomaticDuration));
                QueueSettingForUpload(new CPluginVariable(@"Display Ticket Rates in Procon Chat", typeof(Boolean), _DisplayTicketRatesInProconChat));
                QueueSettingForUpload(new CPluginVariable(@"Surrender Vote Enable", typeof(Boolean), _surrenderVoteEnable));
                QueueSettingForUpload(new CPluginVariable(@"Percentage Votes Needed for Surrender", typeof(Double), _surrenderVoteMinimumPlayerPercentage));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Player Count to Enable Surrender", typeof(Int32), _surrenderVoteMinimumPlayerCount));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Ticket Gap to Surrender", typeof(Int32), _surrenderVoteMinimumTicketGap));
                QueueSettingForUpload(new CPluginVariable(@"Enable Required Ticket Rate Gap to Surrender", typeof(Boolean), _surrenderVoteTicketRateGapEnable));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Ticket Rate Gap to Surrender", typeof(Double), _surrenderVoteMinimumTicketRateGap));
                QueueSettingForUpload(new CPluginVariable(@"Surrender Vote Timeout Enable", typeof(Boolean), _surrenderVoteTimeoutEnable));
                QueueSettingForUpload(new CPluginVariable(@"Surrender Vote Timeout Minutes", typeof(Int32), _surrenderVoteTimeoutMinutes));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Enable", typeof(Boolean), _surrenderAutoEnable));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Use Optimal Values for Metro Conquest", typeof(Boolean), _surrenderAutoUseMetroValues));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Use Optimal Values for Locker Conquest", typeof(Boolean), _surrenderAutoUseLockerValues));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Reset Trigger Count on Cancel", typeof(Boolean), _surrenderAutoResetTriggerCountOnCancel));
                QueueSettingForUpload(new CPluginVariable(@"Reset Auto-Nuke Trigger Count on Fire", typeof(Boolean), _surrenderAutoResetTriggerCountOnFire));
                QueueSettingForUpload(new CPluginVariable(@"Nuke Winning Team Instead of Surrendering Losing Team", typeof(Boolean), _surrenderAutoNukeInstead));
                QueueSettingForUpload(new CPluginVariable(@"Fire Nuke Triggers if Winning Team up by X Tickets", typeof(Int32), _NukeWinningTeamUpTicketCount));
                QueueSettingForUpload(new CPluginVariable(@"Switch to surrender after max nukes", typeof(Boolean), _surrenderAutoNukeResolveAfterMax));
                QueueSettingForUpload(new CPluginVariable(@"Only fire ticket difference nukes in high population", typeof(Boolean), _NukeWinningTeamUpTicketHigh));
                QueueSettingForUpload(new CPluginVariable(@"Announce Nuke Preparation to Players", typeof(Boolean), _surrenderAutoAnnounceNukePrep));
                QueueSettingForUpload(new CPluginVariable(@"Allow Auto-Nuke to fire on losing teams", typeof(Boolean), _surrenderAutoNukeLosingTeams));
                QueueSettingForUpload(new CPluginVariable(@"Maximum Nuke Ticket Difference for Losing Team", typeof(Int32), _surrenderAutoNukeLosingMaxDiff));
                QueueSettingForUpload(new CPluginVariable(@"Start Surrender Vote Instead of Surrendering Losing Team", typeof(Boolean), _surrenderAutoTriggerVote));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Minimum Ticket Gap", typeof(Int32), _surrenderAutoMinimumTicketGap));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Minimum Ticket Count", typeof(Int32), _surrenderAutoMinimumTicketCount));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Maximum Ticket Count", typeof(Int32), _surrenderAutoMaximumTicketCount));
                QueueSettingForUpload(new CPluginVariable(@"Maximum Auto-Nukes Each Round", typeof(Int32), _surrenderAutoMaxNukesEachRound));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Seconds Between Nukes", typeof(Int32), _surrenderAutoNukeMinBetween));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Losing Team Rate Window Max", typeof(Double), _surrenderAutoLosingRateMax));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Losing Team Rate Window Min", typeof(Double), _surrenderAutoLosingRateMin));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Winning Team Rate Window Max", typeof(Double), _surrenderAutoWinningRateMax));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Winning Team Rate Window Min", typeof(Double), _surrenderAutoWinningRateMin));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Message", typeof(String), _surrenderAutoMessage));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Message", typeof(String), _surrenderAutoNukeMessage));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Trigger Count to Surrender", typeof(Int32), _surrenderAutoTriggerCountToSurrender));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Surrender Minimum Players", typeof(Int32), _surrenderAutoMinimumPlayers));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke High Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationHigh));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Medium Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationMed));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Low Pop Duration Seconds", typeof(Int32), _surrenderAutoNukeDurationLow));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Consecutive Duration Increase", typeof(Int32), _surrenderAutoNukeDurationIncrease));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Nuke Duration Increase Minimum Ticket Difference", typeof(Int32), _surrenderAutoNukeDurationIncreaseTicketDiff));
                QueueSettingForUpload(new CPluginVariable(@"Faction Randomizer: Enable", typeof(Boolean), _factionRandomizerEnable));
                QueueSettingForUpload(new CPluginVariable(@"Faction Randomizer: Restriction", typeof(String), _factionRandomizerRestriction.ToString()));
                QueueSettingForUpload(new CPluginVariable(@"Faction Randomizer: Allow Repeat Team Selections", typeof(Boolean), _factionRandomizerAllowRepeatSelection));
                if (ChallengeManager != null)
                {
                    QueueSettingForUpload(new CPluginVariable(@"Use Challenge System", typeof(Boolean), ChallengeManager.Enabled));
                    QueueSettingForUpload(new CPluginVariable(@"Challenge System Minimum Players", typeof(Int32), ChallengeManager.MinimumPlayers));
                    QueueSettingForUpload(new CPluginVariable(@"Challenge Command Lock Timeout Hours", typeof(Int32), ChallengeManager.CommandLockTimeoutHours));
                    QueueSettingForUpload(new CPluginVariable(@"Challenge System Auto-Assign Round rules", typeof(Boolean), ChallengeManager.AutoPlay));
                    QueueSettingForUpload(new CPluginVariable(@"Use Server-Wide Round Rules", typeof(Boolean), ChallengeManager.EnableServerRoundRules));
                    QueueSettingForUpload(new CPluginVariable(@"Use Different Round Rule For Each Player", typeof(Boolean), ChallengeManager.RandomPlayerRoundRules));
                }
                QueueSettingForUpload(new CPluginVariable(@"Use Proxy for Battlelog", typeof(Boolean), _UseProxy));
                QueueSettingForUpload(new CPluginVariable(@"Proxy URL", typeof(String), _ProxyURL));
                Log.Debug(() => "uploadAllSettings finished!", 6);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing all settings for upload.", e));
            }
        }

        private void UploadSetting(CPluginVariable var)
        {
            Log.Debug(() => "uploadSetting starting!", 7);
            //Make sure database connection active
            if (_databaseConnectionCriticalState || !_settingsFetched)
            {
                return;
            }
            try
            {
                //Check for length too great
                if (var.Value.Length > 9999)
                {
                    Log.Error("Unable to upload setting, length of setting too great. Really dude? It's 10000+ characters. This is battlefield, not a book club.");
                    return;
                }
                Log.Debug(() => var.Value, 7);
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Int32 rowsAffected = connection.Execute(@"
                        INSERT INTO `" + _mySqlSchemaName + @"`.`adkats_settings`
                        (
                            `server_id`,
                            `setting_name`,
                            `setting_type`,
                            `setting_value`
                        )
                        VALUES
                        (
                            @server_id,
                            @setting_name,
                            @setting_type,
                            @setting_value
                        )
                        ON DUPLICATE KEY
                        UPDATE
                            `setting_value` = @setting_value",
                        new
                        {
                            server_id = _serverInfo.ServerID,
                            setting_name = var.Name,
                            setting_type = var.Type,
                            setting_value = var.Value
                        });
                    if (rowsAffected > 0)
                    {
                        Log.Debug(() => "Setting " + var.Name + " pushed to database", 7);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while uploading setting to database.", e));
            }

            Log.Debug(() => "uploadSetting finished!", 7);
        }

        private void FetchSettings(long serverID, Boolean verbose)
        {
            Log.Debug(() => "fetchSettings starting!", 6);
            Boolean success = false;
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                //Success fetching settings
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var rows = connection.Query(@"
                        SELECT
                            `setting_name`,
                            `setting_type`,
                            `setting_value`
                        FROM
                            `" + _mySqlSchemaName + @"`.`adkats_settings`
                        WHERE
                            `server_id` = @server_id",
                        new { server_id = serverID });
                    foreach (var row in rows)
                    {
                        success = true;
                        CPluginVariable var = new CPluginVariable((string)row.setting_name, (string)row.setting_type, (string)row.setting_value);
                        SetPluginVariable(var.Name, var.Value);
                    }
                    if (success)
                    {
                        _lastDbSettingFetch = UtcNow();
                        UpdateSettingPage();
                    }
                    else if (verbose)
                    {
                        Log.Error("Settings could not be loaded. Server " + serverID + " invalid.");
                    }
                    UploadAllSettings();
                    _settingsFetched = true;
                    _settingImportID = _serverInfo.ServerID;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching settings from database.", e));
            }
            Log.Debug(() => "fetchSettings finished!", 6);
        }

        private void UploadCommand(ACommand aCommand)
        {
            Log.Debug(() => "uploadCommand starting!", 6);

            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    connection.Execute(@"
                        INSERT INTO
                        `" + _mySqlSchemaName + @"`.`adkats_commands`
                        (
                            `command_id`,
                            `command_active`,
                            `command_key`,
                            `command_logging`,
                            `command_name`,
                            `command_text`,
                            `command_playerInteraction`,
                            `command_access`
                        )
                        VALUES
                        (
                            @command_id,
                            @command_active,
                            @command_key,
                            @command_logging,
                            @command_name,
                            @command_text,
                            @command_playerInteraction,
                            @command_access
                        )
                        ON DUPLICATE KEY
                        UPDATE
                            `command_active` = @command_active,
                            `command_logging` = @command_logging,
                            `command_name` = @command_name,
                            `command_text` = @command_text,
                            `command_playerInteraction` = @command_playerInteraction,
                            `command_access` = @command_access",
                        new
                        {
                            command_id = aCommand.command_id,
                            command_active = aCommand.command_active.ToString(),
                            command_key = aCommand.command_key,
                            command_logging = aCommand.command_logging.ToString(),
                            command_name = aCommand.command_name,
                            command_text = aCommand.command_text,
                            command_playerInteraction = aCommand.command_playerInteraction,
                            command_access = aCommand.command_access.ToString()
                        });
                }

                Log.Debug(() => "uploadCommand finished!", 6);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Unexpected error uploading command.", e));
            }
        }

        private List<APlayer> FetchAdminSoldiers()
        {
            List<APlayer> adminSoldiers = new List<APlayer>();
            try
            {
                //Loop over the user list
                foreach (AUser user in _userCache.Values.ToList().Where(UserIsAdmin))
                {
                    adminSoldiers.AddRange(user.soldierDictionary.Values);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching admin soldiers.", e));
            }
            return adminSoldiers;
        }

        private List<APlayer> FetchOnlineAdminSoldiers()
        {
            List<APlayer> onlineAdminSoldiers = new List<APlayer>();
            try
            {
                onlineAdminSoldiers.AddRange(_PlayerDictionary.Values.ToList().Where(PlayerIsAdmin));
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching online admin soldiers", e));
            }
            return onlineAdminSoldiers;
        }

        private List<APlayer> FetchOnlineNonAdminSoldiers()
        {
            List<APlayer> nonAdminSoldiers = new List<APlayer>();
            try
            {
                nonAdminSoldiers.AddRange(_PlayerDictionary.Values.ToList().Where(aPlayer => !PlayerIsAdmin(aPlayer)));
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching online non-admin soldiers", e));
            }
            return nonAdminSoldiers;
        }

        private List<APlayer> FetchElevatedSoldiers()
        {
            List<APlayer> elevatedSoldiers = new List<APlayer>();
            //Loop over the user list
            foreach (AUser aUser in _userCache.Values.ToList().Where(user => !UserIsAdmin(user) && user.user_role.role_key != "guest_default"))
            {
                elevatedSoldiers.AddRange(aUser.soldierDictionary.Values);
            }
            return elevatedSoldiers;
        }

        private List<APlayer> FetchSoldiersOfRole(ARole aRole)
        {
            List<APlayer> roleSoldiers = new List<APlayer>();
            //Loop over the user list
            foreach (AUser user in _userCache.Values.ToList().Where(user => user.user_role.role_key == aRole.role_key))
            {
                roleSoldiers.AddRange(user.soldierDictionary.Values);
            }
            return roleSoldiers;
        }

        private List<APlayer> FetchAllUserSoldiers()
        {
            List<APlayer> userSoldiers = new List<APlayer>();
            //Loop over the user list
            foreach (AUser user in _userCache.Values.ToList().Where(aUser => aUser.user_role.role_key != "guest_default"))
            {
                userSoldiers.AddRange(user.soldierDictionary.Values);
            }
            return userSoldiers;
        }

        private Boolean HandleRecordUpload(ARecord record)
        {
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                record.record_exception = new AException("Database not connected.");
                return true;
            }
            try
            {
                Log.Debug(() => "Entering handle record upload", 5);
                if (record.record_id != -1 || record.record_action_executed)
                {
                    //Record already has a record ID, or action has already been taken, it can only be updated
                    if (record.command_type.command_logging != ACommand.CommandLogging.Ignore && record.command_type.command_logging != ACommand.CommandLogging.Unable && !record.record_orchestrate)
                    {
                        if (record.record_exception == null)
                        {
                            //Only call update if the record contained no errors
                            Log.Debug(() => "UPDATING record " + record.record_id + " for " + record.command_type, 5);
                            //Update Record
                            UpdateRecord(record);
                            return false;
                        }
                        Log.Debug(() => "" + record.command_type + " record contained errors, skipping UPDATE", 4);
                    }
                    else
                    {
                        Log.Debug(() => "Skipping record UPDATE for " + record.command_type, 5);
                    }
                }
                else
                {
                    Log.Debug(() => "Record needs full upload, checking.", 5);
                    //No record ID. Perform full upload
                    switch (record.command_type.command_key)
                    {
                        //TODO: Add ability for multiple targets
                        case "player_punish":
                            //Upload for punish is required
                            if (CanPunish(record, 20))
                            {
                                //Check if the punish will be Double counted
                                Boolean iroStatus = _IROActive && FetchIROStatus(record);
                                if (iroStatus)
                                {
                                    record.isIRO = true;
                                    //Upload record twice
                                    Log.Debug(() => "UPLOADING IRO Punish", 5); //IRO - Immediate Repeat Offence
                                    UploadRecord(record);
                                    UploadRecord(record);
                                }
                                else
                                {
                                    //Upload record once
                                    Log.Debug(() => "UPLOADING Punish", 5);
                                    UploadRecord(record);
                                }
                            }
                            else
                            {
                                SendMessageToSource(record, record.GetTargetNames() + " already acted on in the last 20 seconds.");
                                FinalizeRecord(record);
                                return false;
                            }
                            break;
                        //TODO: Add ability for multiple targets
                        case "player_forgive":
                            //Upload for forgive is required
                            //No restriction on forgives/minute
                            Log.Debug(() => "UPLOADING Forgive", 5);
                            UploadRecord(record);
                            break;
                        default:
                            //Case for any other command
                            //Check logging setting for record command type
                            if (record.command_type.command_logging != ACommand.CommandLogging.Ignore && record.command_type.command_logging != ACommand.CommandLogging.Unable)
                            {
                                Log.Debug(() => "UPLOADING record for " + record.command_type, 5);
                                //Upload Record
                                UploadRecord(record);
                            }
                            else
                            {
                                Log.Debug(() => "Skipping record UPLOAD for " + record.command_type, 6);
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = Log.HandleException(new AException("Error while handling record upload.", e));
            }
            return true;
        }

        private Boolean UploadRecord(ARecord record)
        {
            Boolean success = true;
            //If record has multiple targets, create a new record for each target
            if (record.TargetPlayersLocal.Any())
            {
                record.TargetInnerRecords.Clear();
                foreach (APlayer aPlayer in record.TargetPlayersLocal)
                {
                    ARecord aRecord = new ARecord
                    {
                        isAliveChecked = record.isAliveChecked,
                        isContested = record.isContested,
                        isDebug = record.isDebug,
                        isIRO = record.isIRO,
                        record_source = record.record_source,
                        record_access = record.record_access,
                        server_id = record.server_id,
                        command_type = record.command_type,
                        command_action = record.command_action,
                        command_numeric = record.command_numeric,
                        target_name = aPlayer.player_name,
                        target_player = aPlayer,
                        source_name = record.source_name,
                        source_player = record.source_player,
                        record_message = record.record_message,
                        record_action_executed = record.record_action_executed,
                        record_time = record.record_time
                    };
                    record.TargetInnerRecords.Add(aRecord);
                    if (!UploadInnerRecord(aRecord))
                    {
                        success = false;
                    }
                }
            }
            else
            {
                success = UploadInnerRecord(record);
            }
            return success;
        }

        private Boolean UploadInnerRecord(ARecord record)
        {
            Log.Debug(() => "uploadRecord starting!", 6);

            Boolean success = false;
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                record.record_exception = new AException("Database not connected.");
                return false;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    //Decide which table the record should be added to
                    String tablename = (record.isDebug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");

                    //Fill the parameters
                    if (record.server_id == 0)
                    {
                        Log.Error("Record server ID was invalid, unable to continue.");
                        return false;
                    }
                    if (record.command_type == null)
                    {
                        Log.Error("Command type was null in uploadRecord, unable to continue.");
                        return false;
                    }
                    if (record.command_action == null)
                    {
                        record.command_action = record.command_type;
                    }
                    String tName = "NoNameTarget";
                    if (!String.IsNullOrEmpty(record.target_name))
                    {
                        tName = record.target_name;
                    }
                    Int32? targetId = null;
                    if (record.target_player != null)
                    {
                        if (!String.IsNullOrEmpty(record.target_player.player_name))
                        {
                            tName = record.target_player.player_name;
                        }
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Target ID invalid when uploading record. Unable to complete.");
                            record.record_exception = new AException("Target ID invalid when uploading record. Unable to complete.");
                            SendMessageToSource(record, "Target ID invalid when uploading record. Unable to complete.");
                            FinalizeRecord(record);
                            return false;
                        }
                        targetId = (Int32?)record.target_player.player_id;
                    }
                    String sName = "NoNameSource";
                    if (!String.IsNullOrEmpty(record.source_name))
                    {
                        sName = record.source_name;
                    }
                    Int32? sourceId = null;
                    if (record.source_player != null)
                    {
                        if (!String.IsNullOrEmpty(record.source_player.player_name))
                        {
                            sName = record.source_player.player_name;
                        }
                        if (record.source_player.player_id <= 0)
                        {
                            Log.Error("Source ID invalid when uploading record. Unable to complete.");
                            record.record_exception = new AException("Source ID invalid when uploading record. Unable to complete.");
                            SendMessageToSource(record, "Source ID invalid when uploading record. Unable to complete.");
                            FinalizeRecord(record);
                            return false;
                        }
                        sourceId = (Int32?)record.source_player.player_id;
                    }

                    String messageIRO = record.record_message + ((record.isIRO) ? (" [IRO]") : (""));
                    //Trim to 500 characters (Should only hit this limit when processing error messages)
                    messageIRO = messageIRO.Length <= 500 ? messageIRO : messageIRO.Substring(0, 500);

                    //Orchestration of other AdKats instances
                    String adkatsRead = record.record_orchestrate ? "N" : "Y";

                    //Set the insert command structure
                    String sql;
                    if (record.record_held)
                    {
                        sql = @"
                            INSERT INTO " + tablename + @"
                            (
                                `server_id`,
                                `command_type`,
                                `command_action`,
                                `command_numeric`,
                                `target_name`,
                                `target_id`,
                                `source_name`,
                                `source_id`,
                                `record_message`,
                                `record_time`,
                                `adkats_read`
                            )
                            VALUES
                            (
                                @server_id,
                                @command_type,
                                @command_action,
                                @command_numeric,
                                @target_name,
                                @target_id,
                                @source_name,
                                @source_id,
                                @record_message,
                                @record_time,
                                @adkats_read
                            ); SELECT LAST_INSERT_ID();";
                    }
                    else
                    {
                        sql = @"
                            INSERT INTO " + tablename + @"
                            (
                                `server_id`,
                                `command_type`,
                                `command_action`,
                                `command_numeric`,
                                `target_name`,
                                `target_id`,
                                `source_name`,
                                `source_id`,
                                `record_message`,
                                `record_time`,
                                `adkats_read`
                            )
                            VALUES
                            (
                                @server_id,
                                @command_type,
                                @command_action,
                                @command_numeric,
                                @target_name,
                                @target_id,
                                @source_name,
                                @source_id,
                                @record_message,
                                UTC_TIMESTAMP(),
                                @adkats_read
                            ); SELECT LAST_INSERT_ID();";
                    }

                    var parameters = new
                    {
                        server_id = record.server_id,
                        command_type = record.command_type.command_id,
                        command_action = record.command_action.command_id,
                        command_numeric = record.command_numeric,
                        target_name = tName,
                        target_id = targetId,
                        source_name = sName,
                        source_id = sourceId,
                        record_message = messageIRO,
                        record_time = record.record_time,
                        adkats_read = adkatsRead
                    };

                    //Attempt to execute the query
                    var lastId = connection.ExecuteScalar<Int64>(sql, parameters);
                    if (lastId > 0)
                    {
                        success = true;
                        record.record_id = lastId;
                    }
                }

                if (success)
                {
                    Log.Debug(() => record.command_action.command_key + " upload for " + record.GetTargetNames() + " by " + record.GetSourceName() + " SUCCESSFUL!", 3);
                }
                else
                {
                    record.record_exception = new AException("Unknown error uploading record.");
                    Log.HandleException(record.record_exception);
                }

                Log.Debug(() => "uploadRecord finished!", 6);
                return success;
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Unexpected error uploading Record.", e);
                Log.HandleException(record.record_exception);
                return false;
            }
        }

        private Boolean UploadStatistic(AStatistic aStat)
        {
            Log.Debug(() => "UploadStatistic starting!", 6);

            Boolean success = false;
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return false;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    //Fill the parameters
                    if (aStat.server_id == 0)
                    {
                        Log.HandleException(new AException("Statistic server ID was invalid when uploading, unable to continue."));
                        return false;
                    }
                    if (aStat.round_id == 0)
                    {
                        return false;
                    }
                    String tName = null;
                    Int32? targetId = null;
                    if (aStat.target_player != null)
                    {
                        if (!String.IsNullOrEmpty(aStat.target_player.player_name))
                        {
                            tName = aStat.target_player.player_name;
                        }
                        targetId = (Int32?)aStat.target_player.player_id;
                    }
                    if (String.IsNullOrEmpty(tName))
                    {
                        if (!String.IsNullOrEmpty(aStat.target_name))
                        {
                            tName = aStat.target_name;
                        }
                        else
                        {
                            Log.HandleException(new AException("Statistic target name null or empty when uploading, unable to continue."));
                            return false;
                        }
                    }

                    var lastId = connection.ExecuteScalar<Int64>(@"
                        INSERT INTO
                            `adkats_statistics`
                        (
                            `server_id`,
                            `round_id`,
                            `stat_type`,
                            `target_name`,
                            `target_id`,
                            `stat_value`,
                            `stat_comment`,
                            `stat_time`
                        )
                        VALUES
                        (
                            @server_id,
                            @round_id,
                            @stat_type,
                            @target_name,
                            @target_id,
                            @stat_value,
                            @stat_comment,
                            @stat_time
                        ); SELECT LAST_INSERT_ID();",
                        new
                        {
                            server_id = aStat.server_id,
                            round_id = aStat.round_id,
                            stat_type = aStat.stat_type.ToString(),
                            target_name = tName,
                            target_id = targetId,
                            stat_value = aStat.stat_value,
                            stat_comment = aStat.stat_comment,
                            stat_time = aStat.stat_time
                        });
                    if (lastId > 0)
                    {
                        success = true;
                        aStat.stat_id = lastId;
                    }
                }

                if (success)
                {
                    Log.Debug(() => aStat.stat_type + " stat upload for " + aStat.target_name + " SUCCESSFUL!", 4);
                }
                else
                {
                    Log.HandleException(new AException("Unknown error uploading statistic."));
                }

                Log.Debug(() => "UploadStatistic finished!", 6);
                return success;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Unexpected error uploading statistic.", e));
                return false;
            }
        }

        private Boolean UploadChatLog(AChatMessage messageObject)
        {
            Log.Debug(() => "UploadChatLog starting!", 6);
            Boolean success = false;
            if (!_threadsReady)
            {
                return success;
            }
            //comorose BF4/BFHL chat handle
            if (messageObject.OriginalMessage.Contains("ID_CHAT") || messageObject.OriginalMessage.Contains("AdKatsInstanceCheck"))
            {
                success = true;
                return success;
            }
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                Log.HandleException(new AException("Database not connected on chat upload."));
                return success;
            }
            //Server spam check
            if (!_PostStatLoggerChatManually_PostServerChatSpam && messageObject.Speaker == "Server")
            {
                success = true;
                return success;
            }
            //Ignore command check
            if (_PostStatLoggerChatManually_IgnoreCommands && (messageObject.OriginalMessage.StartsWith("@") || messageObject.OriginalMessage.StartsWith("!") || messageObject.OriginalMessage.StartsWith(".") || messageObject.OriginalMessage.StartsWith("/")))
            {
                success = true;
                return success;
            }

            try
            {
                //Fetch the player from player dictionary
                APlayer aPlayer = null;
                if (_PlayerDictionary.TryGetValue(messageObject.Speaker, out aPlayer))
                {
                    aPlayer.LastUsage = UtcNow();
                    Log.Debug(() => "Player found for chat log upload.", 5);
                }

                //Trim to 255 characters
                String logMessage = messageObject.Message.Length <= 255 ? messageObject.OriginalMessage : messageObject.OriginalMessage.Substring(0, 255);

                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Int32 rowsAffected = connection.Execute(@"INSERT INTO `tbl_chatlog`
                        (
                            `logDate`,
                            `ServerID`,
                            `logSubset`,
                            `logPlayerID`,
                            `logSoldierName`,
                            `logMessage`
                        )
                        VALUES
                        (
                            UTC_TIMESTAMP(),
                            @server_id,
                            @log_subset,
                            @log_player_id,
                            @log_player_name,
                            @log_message
                        )",
                        new
                        {
                            server_id = _serverInfo.ServerID,
                            log_subset = messageObject.Subset.ToString(),
                            log_player_id = (aPlayer != null && aPlayer.player_id > 0) ? (object)aPlayer.player_id : null,
                            log_player_name = messageObject.Speaker,
                            log_message = logMessage
                        });
                    if (rowsAffected > 0)
                    {
                        success = true;
                    }
                }
                if (success)
                {
                    Log.Debug(() => "Chat upload for " + messageObject.Speaker + " SUCCESSFUL!", 5);
                }
                else
                {
                    Log.HandleException(new AException("Error uploading chat log. Success not reached."));
                    return success;
                }
                Log.Debug(() => "UploadChatLog finished!", 6);
                return success;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Unexpected error uploading chat log.", e));
                return success;
            }
        }

        private void UpdateRecordEndPointReputations(ARecord aRecord)
        {
            Log.Debug(() => "Updating endpoint reputation for " + aRecord.command_action + " record.", 5);
            if (aRecord.source_player != null && aRecord.source_player.player_id > 0)
            {
                UpdatePlayerReputation(aRecord.source_player, true);
            }
            if (aRecord.target_player != null && aRecord.target_player.player_id > 0)
            {
                UpdatePlayerReputation(aRecord.target_player, true);
            }
            if (aRecord.TargetPlayersLocal != null)
            {
                foreach (APlayer aPlayer in aRecord.TargetPlayersLocal)
                {
                    UpdatePlayerReputation(aPlayer, true);
                }
            }
        }

        private void UpdatePlayerReputation(APlayer aPlayer, Boolean informPlayer)
        {
            try
            {
                if (aPlayer == null)
                {
                    Log.Error("Attempted to update reputation of invalid player.");
                    return;
                }
                if (_commandSourceReputationDictionary == null || !_commandSourceReputationDictionary.Any() || _commandTargetReputationDictionary == null || !_commandTargetReputationDictionary.Any())
                {
                    Log.Debug(() => "Reputation dictionaries not populated. Can't update reputation for " + aPlayer.GetVerboseName() + ".", 4);
                }
                double sourceReputation = 0.0;
                double targetReputation = 0.0;
                double pointReputation = 0;
                List<ARecord> recentPunishments = FetchRecentRecords(aPlayer.player_id, GetCommandByKey("player_punish").command_id, 10000, 10000, true, false);
                foreach (ARecord punishment in recentPunishments)
                {
                    TimeSpan timeSince = UtcNow() - punishment.record_time;
                    if (timeSince.TotalDays < 50)
                    {
                        pointReputation -= 20 * ((50 - timeSince.TotalDays) / 50);
                    }
                }
                List<ARecord> recentForgives = FetchRecentRecords(aPlayer.player_id, GetCommandByKey("player_forgive").command_id, 10000, 10000, true, false);
                foreach (ARecord forgive in recentForgives)
                {
                    TimeSpan timeSince = UtcNow() - forgive.record_time;
                    if (timeSince.TotalDays < 50)
                    {
                        pointReputation += 20 * ((50 - timeSince.TotalDays) / 50);
                    }
                }
                if (pointReputation > 0)
                {
                    pointReputation = 0;
                }
                targetReputation = pointReputation;
                double totalReputation = 0;
                double totalReputationConstrained = 0;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var sourceRows = connection.Query(@"
                        SELECT
                            command_type,
                            command_action,
                            count(record_id) command_count
                        FROM
                            adkats_records_main
                        WHERE
                            source_id = @player_id
                        AND
                            target_name <> source_name
                        GROUP BY command_type, command_action",
                        new { player_id = aPlayer.player_id });
                    foreach (var row in sourceRows)
                    {
                        String typeAction = (int)row.command_type + "|" + (int)row.command_action;
                        Double command_count = (Double)row.command_count;
                        Double weight = 0;
                        if (_commandSourceReputationDictionary.TryGetValue(typeAction, out weight))
                        {
                            sourceReputation += (weight * command_count);
                        }
                        else
                        {
                            Log.Warn("Unable to find source weight for command " + typeAction);
                        }
                    }
                    var targetRows = connection.Query(@"
                        SELECT
                            command_type,
                            command_action,
                            count(record_id) command_count
                        FROM
                            adkats_records_main
                        WHERE
                            target_id = @player_id
                        AND
                            target_name <> source_name
                        GROUP BY command_type, command_action",
                        new { player_id = aPlayer.player_id });
                    foreach (var row in targetRows)
                    {
                        String typeAction = (int)row.command_type + "|" + (int)row.command_action;
                        Double command_count = (Double)row.command_count;
                        Double weight = 0;
                        if (_commandTargetReputationDictionary.TryGetValue(typeAction, out weight))
                        {
                            targetReputation += (weight * command_count);
                        }
                        else
                        {
                            Log.Warn("Unable to find target weight for command " + typeAction);
                        }
                    }
                    //Special case for certain commands with same source and target, but should still be counted
                    //Currently only the assist command is counted (Command ID 51)
                    var assistRows = connection.Query(@"
                        SELECT
                            command_type,
                            command_action,
                            count(record_id) command_count
                        FROM
                            adkats_records_main
                        WHERE
                            source_id = @player_id
                        AND
                            target_id = source_id
                        AND
                            command_type = 51
                        AND
                            command_action = 51
                        GROUP BY command_type, command_action",
                        new { player_id = aPlayer.player_id });
                    foreach (var row in assistRows)
                    {
                        String typeAction = (int)row.command_type + "|" + (int)row.command_action;
                        Double command_count = (Double)row.command_count;
                        Double weight = 0;
                        if (_commandSourceReputationDictionary.TryGetValue(typeAction, out weight))
                        {
                            sourceReputation += (weight * command_count);
                        }
                        else
                        {
                            Log.Error("Unable to find source weight for command " + typeAction);
                        }
                        if (_commandTargetReputationDictionary.TryGetValue(typeAction, out weight))
                        {
                            targetReputation += (weight * command_count);
                        }
                        else
                        {
                            Log.Error("Unable to find target weight for command " + typeAction);
                        }
                    }
                    totalReputation = sourceReputation + targetReputation;
                    if (totalReputation >= 0)
                    {
                        totalReputationConstrained = (1000 * totalReputation) / (totalReputation + 1000);
                    }
                    else
                    {
                        totalReputationConstrained = -(1000 * Math.Abs(totalReputation)) / (Math.Abs(totalReputation) + 1000);
                    }
                    {
                        if (aPlayer.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when updating player reputation. Unable to complete.");
                            return;
                        }
                        if (aPlayer.game_id <= 0)
                        {
                            aPlayer.game_id = _serverInfo.GameID;
                        }
                        Int32 rowsAffected = connection.Execute(@"
                        REPLACE INTO
                            adkats_player_reputation
                        VALUES
                        (
                            @player_id,
                            @game_id,
                            @target_rep,
                            @source_rep,
                            @total_rep,
                            @total_rep_co
                        )",
                        new
                        {
                            player_id = aPlayer.player_id,
                            game_id = aPlayer.game_id,
                            target_rep = targetReputation,
                            source_rep = sourceReputation,
                            total_rep = totalReputation,
                            total_rep_co = totalReputationConstrained
                        });
                        if (_firstPlayerListComplete && Math.Abs(aPlayer.player_reputation - totalReputationConstrained) > .02)
                        {
                            Log.Debug(() => aPlayer.GetVerboseName() + "'s reputation updated from " + Math.Round(aPlayer.player_reputation, 2) + " to " + Math.Round(totalReputationConstrained, 2), 3);
                            if (aPlayer.player_spawnedOnce || (aPlayer.fbpInfo != null && aPlayer.fbpInfo.TeamID == 0))
                            {
                                if (!PlayerIsAdmin(aPlayer))
                                {
                                    String message = "Your reputation ";
                                    if (totalReputationConstrained > aPlayer.player_reputation)
                                    {
                                        if (Math.Round(totalReputationConstrained, 2) == 0)
                                        {
                                            message += "increased from " + Math.Round(aPlayer.player_reputation, 2) + " to " + Math.Round(totalReputationConstrained, 2) + "!";
                                        }
                                        else if (totalReputationConstrained > 0)
                                        {
                                            message += "increased from " + Math.Round(aPlayer.player_reputation, 2) + " to " + Math.Round(totalReputationConstrained, 2) + "! Thanks for your help!";
                                        }
                                        else
                                        {
                                            message += "increased from " + Math.Round(aPlayer.player_reputation, 2) + " to " + Math.Round(totalReputationConstrained, 2) + ", but is still negative.";
                                        }
                                    }
                                    else
                                    {
                                        if (aPlayer.player_reputation >= 0)
                                        {
                                            if (totalReputationConstrained < 0)
                                            {
                                                message += "has gone negative! Be careful, it's now " + Math.Round(totalReputationConstrained, 2);
                                            }
                                            else
                                            {
                                                message += "decreased from " + Math.Round(aPlayer.player_reputation, 2) + " to " + Math.Round(totalReputationConstrained, 2);
                                            }
                                        }
                                        else
                                        {
                                            message += "decreased further from " + Math.Round(aPlayer.player_reputation, 2) + " to " + Math.Round(totalReputationConstrained, 2);
                                        }
                                    }
                                    if (informPlayer)
                                    {
                                        aPlayer.Say(message);
                                    }
                                }
                            }
                        }
                        aPlayer.player_reputation = totalReputationConstrained;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating player reputation.", e));
            }
        }

        private Boolean SendQuery(String query, Boolean verbose, Object param = null)
        {
            if (String.IsNullOrEmpty(query))
            {
                return false;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var result = connection.QueryFirstOrDefault<dynamic>(query, param);
                    if (result != null)
                    {
                        if (verbose)
                        {
                            var dict = (IDictionary<String, Object>)result;
                            Log.Success("Query returned value " + dict.Values.First()?.ToString() + ".");
                        }
                        return true;
                    }
                    else
                    {
                        if (verbose)
                        {
                            Log.Error("Query returned no results.");
                        }
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                if (verbose)
                {
                    Log.HandleException(new AException("Verbose. Error while performing query.", e));
                }
                return false;
            }
        }

        private Boolean SendNonQuery(String desc, String nonQuery, Boolean verbose)
        {
            if (String.IsNullOrEmpty(nonQuery))
            {
                return false;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Int32 rowsAffected = connection.Execute(nonQuery);
                    if (verbose)
                    {
                        Log.Success("Non-Query success. " + rowsAffected + " rows affected. [" + desc + "]");
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                if (verbose)
                {
                    Log.Error("Non-Query failed. [" + desc + "]: " + e);
                }
                return false;
            }
        }


        private void UpdateRecord(ARecord record)
        {
            //If record has multiple inner records, update those instead
            if (record.TargetInnerRecords.Any())
            {
                foreach (ARecord innerRecord in record.TargetInnerRecords)
                {
                    //Update the inner record with action, numeric, and message, before pushing
                    innerRecord.command_action = record.command_action;
                    innerRecord.command_numeric = record.command_numeric;
                    innerRecord.record_message = record.record_message;
                    //Call inner upload
                    UpdateInnerRecord(innerRecord);
                }
            }
            else
            {
                UpdateInnerRecord(record);
            }
        }


        private void UpdateInnerRecord(ARecord record)
        {
            Log.Debug(() => "UpdateInnerRecord starting!", 6);

            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                record.record_exception = new AException("Database not connected.");
                return;
            }
            try
            {
                Int32 attempts = 0;
                Boolean success = false;
                do
                {
                    try
                    {
                        using (MySqlConnection connection = GetDatabaseConnection())
                        {
                            String tablename = (record.isDebug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");
                            //Trim to 500 characters
                            record.record_message = record.record_message.Length <= 500 ? record.record_message : record.record_message.Substring(0, 500);
                            Int32 rowsAffected = connection.Execute(
                                "UPDATE " + tablename + @"
                                SET
                                    `command_action` = @command_action,
                                    `command_numeric` = @command_numeric,
                                    `record_message` = @record_message,
                                    `adkats_read` = 'Y'
                                WHERE
                                    `record_id` = @record_id",
                                new
                                {
                                    record_id = record.record_id,
                                    command_numeric = record.command_numeric,
                                    record_message = record.record_message,
                                    command_action = record.command_action.command_id
                                });
                            if (rowsAffected > 0)
                            {
                                success = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error while updating record.", e));
                        success = false;
                    }
                } while (!success && attempts++ < 5);

                UpdateRecordEndPointReputations(record);
                if (success)
                {
                    Log.Debug(() => record.command_action.command_key + " update for " + record.GetTargetNames() + " by " + record.GetSourceName() + " SUCCESSFUL!", 3);
                }
                else
                {
                    Log.Error(record.command_action.command_key + " update for " + record.GetTargetNames() + " by " + record.GetSourceName() + " FAILED!");
                }

                Log.Debug(() => "UpdateInnerRecord finished!", 6);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating record", e));
            }
        }

        private ARecord FetchRecordUpdate(ARecord record)
        {
            Log.Debug(() => "FetchRecordUpdate starting!", 6);
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return record;
            }
            if (record.record_id < 1)
            {
                return record;
            }
            try
            {
                List<ARecord> reportsToExpire = new List<ARecord>();
                var reupload = false;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    {
                        String tablename = (record.isDebug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");
                        var row = connection.QueryFirstOrDefault(@"
                        SELECT
                            `command_type`,
                            `command_action`,
                            `command_numeric`,
                            `target_name`,
                            `target_id`,
                            `source_name`,
                            `source_id`,
                            `record_message`
                        FROM
                            " + tablename + @"
                        WHERE
                            `record_id` = @record_id",
                            new { record_id = record.record_id });
                        if (row != null)
                        {
                            var commandNumeric = (Int32)row.command_numeric;
                            if (commandNumeric != record.command_numeric)
                            {
                                // Don't allow command numeric updates if the new number is 0, but we already have a number
                                if (record.command_numeric != 0 && commandNumeric == 0)
                                {
                                    // In fact, fix the record on the database side
                                    reupload = true;
                                    Log.Info("Record " + record.record_id + " had an invalid command numeric. Fixing back to " + record.command_numeric + ".");
                                }
                                else
                                {
                                    Log.Info("Record " + record.record_id + " command numeric changed from " + record.command_numeric + " to " + commandNumeric);
                                    record.command_numeric = commandNumeric;
                                }
                            }
                            var targetName = (String)row.target_name;
                            if (targetName != record.target_name)
                            {
                                Log.Info("Record " + record.record_id + " target name changed from " + record.target_name + " to " + targetName);
                                record.target_name = targetName;
                            }
                            if (row.target_id != null)
                            {
                                Int64 targetID = (Int64)(Int32)row.target_id;
                                if (record.target_player == null)
                                {
                                    record.target_player = FetchPlayer(false, false, false, null, targetID, null, null, null, null);
                                    if (record.target_player == null)
                                    {
                                        Log.Error("Unable to fetch target player for ID " + targetID + " when fetching record " + record.record_id + " updates.");
                                    }
                                    else
                                    {
                                        Log.Info("Record " + record.record_id + " added target player " + record.target_player.GetVerboseName());
                                    }
                                }
                                else if (targetID != record.target_player.player_id)
                                {
                                    var newPlayer = FetchPlayer(false, false, false, null, targetID, null, null, null, null);
                                    if (newPlayer == null)
                                    {
                                        Log.Error("Unable to fetch target player change for ID " + targetID + " when fetching record " + record.record_id + " updates.");
                                    }
                                    else
                                    {
                                        Log.Info("Record " + record.record_id + " target player changed from " + record.target_player.GetVerboseName() + " to " + newPlayer.GetVerboseName());
                                        record.target_player = newPlayer;
                                    }
                                }
                            }
                            var sourceName = (String)row.source_name;
                            if (sourceName != record.source_name)
                            {
                                Log.Info("Record " + record.record_id + " source name changed from " + record.source_name + " to " + sourceName);
                                record.source_name = sourceName;
                            }
                            if (row.source_id != null)
                            {
                                Int64 sourceID = (Int64)(Int32)row.source_id;
                                if (record.source_player == null)
                                {
                                    record.source_player = FetchPlayer(false, false, false, null, sourceID, null, null, null, null);
                                    if (record.source_player == null)
                                    {
                                        Log.Error("Unable to fetch source player for ID " + sourceID + " when fetching record " + record.record_id + " updates.");
                                    }
                                    else
                                    {
                                        Log.Info("Record " + record.record_id + " added source player " + record.source_player.GetVerboseName());
                                    }
                                }
                                else if (sourceID != record.source_player.player_id)
                                {
                                    var newPlayer = FetchPlayer(false, false, false, null, sourceID, null, null, null, null);
                                    if (newPlayer == null)
                                    {
                                        Log.Error("Unable to fetch source player change for ID " + sourceID + " when fetching record " + record.record_id + " updates.");
                                    }
                                    else
                                    {
                                        Log.Info("Record " + record.record_id + " source player changed from " + record.source_player.GetVerboseName() + " to " + newPlayer.GetVerboseName());
                                        record.source_player = newPlayer;
                                    }
                                }
                            }
                            var recordMessage = (String)row.record_message;
                            if (recordMessage != record.record_message)
                            {
                                Log.Info("Record " + record.record_id + " message changed from '" + record.record_message + "' to '" + recordMessage + "'");
                                record.record_message = recordMessage;
                            }
                            Int32 commandTypeInt = (Int32)row.command_type;
                            ACommand commandType;
                            if (!_CommandIDDictionary.TryGetValue(commandTypeInt, out commandType))
                            {
                                Log.Error("Unable to parse command type " + commandTypeInt + " when fetching record " + record.record_id + " updates.");
                            }
                            if (commandType.command_key != record.command_type.command_key)
                            {
                                Log.Info("Record " + record.record_id + " command type changed from " + record.command_type.command_name + " to " + commandType.command_name);
                                record.command_type = commandType;
                            }
                            Int32 commandActionInt = (Int32)row.command_action;
                            ACommand commandAction;
                            if (!_CommandIDDictionary.TryGetValue(commandActionInt, out commandAction))
                            {
                                Log.Error("Unable to parse command action " + commandTypeInt + " when fetching record " + record.record_id + " updates.");
                            }
                            if (commandAction.command_key != record.command_action.command_key)
                            {
                                Log.Info("Record " + record.record_id + " command action changed from " + record.command_action.command_name + " to " + commandAction.command_name);
                                record.command_action = commandAction;
                                if (record.target_player != null)
                                {
                                    if (record.command_action.command_key == "player_report_confirm")
                                    {
                                        // Expire all other active reports against the player since this is the one that we acted on
                                        reportsToExpire.AddRange(record.target_player.TargetedRecords.Where(aRecord => IsActiveReport(aRecord) &&
                                                                                                                       aRecord.record_id != record.record_id));

                                        SendMessageToSource(record, "Your report [" + record.command_numeric + "] has been accepted. Thank you.");
                                        OnlineAdminSayMessage("Report [" + record.command_numeric + "] has been accepted.");
                                    }
                                    else if (record.command_action.command_key == "player_report_deny")
                                    {
                                        SendMessageToSource(record, "Your report [" + record.command_numeric + "] has been denied.");
                                        OnlineAdminSayMessage("Report [" + record.command_numeric + "] has been denied.");
                                    }
                                    else if (record.command_action.command_key == "player_report_ignore")
                                    {
                                        OnlineAdminSayMessage("Report [" + record.command_numeric + "] has been ignored by " + record.GetSourceName() + ".");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.Error("Unable to fetch update for record " + record.record_id + " no matching record found.");
                        }
                    }
                }
                // Need to do this separately because otherwise it's stacked database contexts
                foreach (var report in reportsToExpire)
                {
                    ExpireActiveReport(report);
                }
                if (reupload)
                {
                    UpdateRecord(record);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching record update", e));
            }

            Log.Debug(() => "FetchRecordUpdate finished!", 6);
            return record;
        }

        private ARecord FetchRecordByID(Int64 recordID, Boolean debug)
        {
            Log.Debug(() => "fetchRecordByID starting!", 6);
            ARecord record = null;
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return null;
            }
            try
            {
                //Success fetching record
                Boolean success = false;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    String tablename = (debug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");
                    var row = connection.QueryFirstOrDefault(@"
                        SELECT
                            `record_id`,
                            `server_id`,
                            `command_type`,
                            `command_action`,
                            `command_numeric`,
                            `target_name`,
                            `target_id`,
                            `source_name`,
                            `source_id`,
                            `record_message`,
                            `record_time`
                        FROM
                            " + tablename + @"
                        WHERE
                            `record_id` = @recordID",
                        new { recordID });
                    if (row != null)
                    {
                        success = true;

                        record = new ARecord();
                        record.record_source = ARecord.Sources.Database;
                        record.record_access = ARecord.AccessMethod.HiddenExternal;
                        record.record_id = (Int64)row.record_id;
                        record.server_id = (Int64)(Int16)row.server_id;
                        Int32 commandTypeInt = (Int32)row.command_type;
                        if (!_CommandIDDictionary.TryGetValue(commandTypeInt, out record.command_type))
                        {
                            Log.Error("Unable to parse command type " + commandTypeInt + " when fetching record by ID.");
                        }
                        Int32 commandActionInt = (Int32)row.command_action;
                        if (!_CommandIDDictionary.TryGetValue(commandActionInt, out record.command_action))
                        {
                            Log.Error("Unable to parse command action " + commandActionInt + " when fetching record by ID.");
                        }
                        record.command_numeric = (Int32)row.command_numeric;
                        record.target_name = (String)row.target_name;
                        if (row.target_id != null)
                        {
                            record.target_player = new APlayer(this)
                            {
                                player_id = (Int64)(Int32)row.target_id
                            };
                        }
                        record.source_name = (String)row.source_name;
                        if (row.source_id != null)
                        {
                            record.source_player = new APlayer(this)
                            {
                                player_id = (Int64)(Int32)row.source_id
                            };
                        }
                        record.record_message = (String)row.record_message;
                        record.record_time = (DateTime)row.record_time;
                    }
                    if (success)
                    {
                        Log.Debug(() => "Record found for ID " + recordID, 5);
                    }
                    else
                    {
                        Log.Debug(() => "No record found for ID " + recordID, 5);
                    }
                    if (success && record.target_player != null)
                    {
                        long oldID = record.target_player.player_id;
                        record.target_player = FetchPlayer(false, true, false, null, oldID, null, null, null, null);
                        if (record.target_player == null)
                        {
                            Log.Error("Unable to find player ID: " + oldID);
                            return null;
                        }
                        if (!String.IsNullOrEmpty(record.target_player.player_name))
                        {
                            record.target_name = record.target_player.player_name;
                        }
                        else
                        {
                            record.target_name = "NoNameTarget";
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching record by ID", e));
            }

            Log.Debug(() => "fetchRecordByID finished!", 6);
            return record;
        }

        private List<ARecord> FetchRecentRecords(Int64? player_id, Int64? command_id, Int64 limit_days, Int64 limit_records, Boolean target_only, Boolean debug)
        {
            Log.Debug(() => "FetchRecentRecords starting!", 6);
            List<ARecord> records = new List<ARecord>();
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return records;
            }
            try
            {
                //Success fetching record
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    String tablename = (debug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");
                    String sql = @"
                        (SELECT
                            `record_id`,
                            `server_id`,
                            `command_type`,
                            `command_action`,
                            `command_numeric`,
                            `target_name`,
                            `target_id`,
                            `source_name`,
                            `source_id`,
                            `record_message`,
                            `record_time`
                        FROM
                            " + tablename + @"
                        WHERE
                            `record_id` = `record_id`";
                    if (command_id != null && command_id > 0)
                    {
                        sql += @"
                            AND
                            (
                                `command_type` = @command_id
                                OR
                                `command_action` = @command_id
                            )";
                    }
                    if (player_id != null && player_id > 0)
                    {
                        sql += @"
                            AND
                            (
                                `target_id` = @player_id
                                " + ((target_only) ? ("") : (" OR `source_id` = @player_id ")) + @"
                            )";
                    }
                    sql += @"
                        AND
                        (
                            DATE_ADD(`record_time`, INTERVAL @limit_days DAY) > UTC_TIMESTAMP()
                        )
                        ORDER BY
                            `record_id` DESC
                        LIMIT
                            @limit_records)
                        ORDER BY `record_id` ASC";
                    var rows = connection.Query(sql, new
                    {
                        command_id = command_id,
                        player_id = player_id,
                        limit_days = limit_days,
                        limit_records = limit_records
                    });
                    foreach (var row in rows)
                    {
                        ARecord record = new ARecord();
                        record.record_source = ARecord.Sources.Database;
                        record.record_access = ARecord.AccessMethod.HiddenExternal;
                        record.record_id = (Int64)row.record_id;
                        record.server_id = (Int64)(Int16)row.server_id;
                        Int32 commandTypeInt = (Int32)row.command_type;
                        if (!_CommandIDDictionary.TryGetValue(commandTypeInt, out record.command_type))
                        {
                            Log.Error("Unable to parse command type " + commandTypeInt + " when fetching record.");
                        }
                        Int32 commandActionInt = (Int32)row.command_action;
                        if (!_CommandIDDictionary.TryGetValue(commandActionInt, out record.command_action))
                        {
                            Log.Error("Unable to parse command action " + commandActionInt + " when fetching record.");
                        }
                        record.command_numeric = (Int32)row.command_numeric;
                        record.target_name = (String)row.target_name;
                        if (row.target_id != null)
                        {
                            Int64 targetID = (Int64)(Int32)row.target_id;
                            APlayer tPlayer;
                            if ((_PlayerDictionary.TryGetValue(record.target_name, out tPlayer) || _PlayerLeftDictionary.TryGetValue(record.target_name, out tPlayer)) && tPlayer.player_id == targetID)
                            {
                                tPlayer.LastUsage = UtcNow();
                                Log.Debug(() => "Target player fetched from memory.", 7);
                            }
                            else
                            {
                                tPlayer = FetchPlayer(false, true, false, null, targetID, null, null, null, null);
                            }
                            record.target_player = tPlayer;
                        }
                        record.source_name = (String)row.source_name;
                        if (row.source_id != null)
                        {
                            Int64 targetID = (Int64)(Int32)row.source_id;
                            APlayer sPlayer;
                            if ((_PlayerDictionary.TryGetValue(record.target_name, out sPlayer) || _PlayerLeftDictionary.TryGetValue(record.target_name, out sPlayer)) && sPlayer.player_id == targetID)
                            {
                                sPlayer.LastUsage = UtcNow();
                                Log.Debug(() => "Target player fetched from memory.", 7);
                            }
                            else
                            {
                                sPlayer = FetchPlayer(false, true, false, null, targetID, null, null, null, null);
                            }
                            record.source_player = sPlayer;
                        }
                        record.record_message = (String)row.record_message;
                        record.record_time = (DateTime)row.record_time;
                        records.Add(record);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching recent records", e));
            }

            Log.Debug(() => "FetchRecentRecords finished!", 6);
            return records;
        }


        private List<ARecord> FetchUnreadRecords()
        {
            Log.Debug(() => "fetchUnreadRecords starting!", 6);
            //Create return list
            List<ARecord> records = new List<ARecord>();
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return records;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var rows = connection.Query(@"
                        SELECT
                            `record_id`,
                            `server_id`,
                            `command_type`,
                            `command_action`,
                            `command_numeric`,
                            `target_name`,
                            `target_id`,
                            `source_name`,
                            `source_id`,
                            `record_message`,
                            `record_time`
                        FROM
                            `" + _mySqlSchemaName + @"`.`adkats_records_main`
                        WHERE
                            `adkats_read` = 'N'
                        AND
                            `command_type` NOT IN (72, 73)
                        AND
                            `command_action` NOT IN (72, 73)
                        AND
                            `server_id` = @server_id",
                        new { server_id = _serverInfo.ServerID });
                    foreach (var row in rows)
                    {
                        ARecord record = new ARecord();
                        record.record_source = ARecord.Sources.Database;
                        record.record_access = ARecord.AccessMethod.HiddenExternal;
                        record.record_id = (Int64)row.record_id;
                        record.server_id = (Int64)(Int16)row.server_id;
                        Int32 commandTypeInt = (Int32)row.command_type;
                        if (!_CommandIDDictionary.TryGetValue(commandTypeInt, out record.command_type))
                        {
                            Log.Error("Unable to parse command type " + commandTypeInt + " when fetching record by ID.");
                        }
                        Int32 commandActionInt = (Int32)row.command_action;
                        if (!_CommandIDDictionary.TryGetValue(commandActionInt, out record.command_action))
                        {
                            Log.Error("Unable to parse command action " + commandActionInt + " when fetching record by ID.");
                        }
                        record.command_numeric = (Int32)row.command_numeric;
                        record.target_name = (String)row.target_name;
                        Int64 targetIDParse = -1;
                        if (row.target_id != null)
                        {
                            targetIDParse = (Int64)(Int32)row.target_id;
                            Log.Debug(() => "id parsed! " + targetIDParse, 6);
                            //Check if the player needs to be imported, or if they are already in the server
                            APlayer importedPlayer = FetchPlayer(false, true, false, null, targetIDParse, null, null, null, null);
                            if (importedPlayer == null)
                            {
                                continue;
                            }
                            APlayer currentPlayer = null;
                            if (!String.IsNullOrEmpty(importedPlayer.player_name) && _PlayerDictionary.TryGetValue(importedPlayer.player_name, out currentPlayer))
                            {
                                currentPlayer.LastUsage = UtcNow();
                                Log.Debug(() => "External player " + currentPlayer.GetVerboseName() + " is currently in the server, using existing data.", 5);
                                record.target_player = currentPlayer;
                            }
                            else
                            {
                                Log.Debug(() => "External player " + importedPlayer.GetVerboseName() + " is not in the server, fetching from database.", 5);
                                record.target_player = importedPlayer;
                            }
                            record.target_name = record.target_player.player_name;
                        }
                        else
                        {
                            Log.Debug(() => "id parse failed!", 6);
                        }
                        record.source_name = (String)row.source_name;
                        if (row.source_id != null)
                        {
                            Int64 sourceIDParse = (Int64)(Int32)row.source_id;
                            Log.Debug(() => "source id parsed! " + sourceIDParse, 6);
                            //Check if the player needs to be imported, or if they are already in the server
                            APlayer importedPlayer = FetchPlayer(false, true, false, null, sourceIDParse, null, null, null, null);
                            if (importedPlayer == null)
                            {
                                continue;
                            }
                            APlayer currentPlayer = null;
                            if (!String.IsNullOrEmpty(importedPlayer.player_name) && _PlayerDictionary.TryGetValue(importedPlayer.player_name, out currentPlayer))
                            {
                                Log.Debug(() => "External player " + currentPlayer.GetVerboseName() + " is currently in the server, using existing data.", 5);
                                record.source_player = currentPlayer;
                            }
                            else
                            {
                                Log.Debug(() => "External player " + importedPlayer.GetVerboseName() + " is not in the server, fetching from database.", 5);
                                record.source_player = importedPlayer;
                            }
                            record.target_name = record.target_player.player_name;
                        }
                        record.record_message = (String)row.record_message;
                        record.record_time = (DateTime)row.record_time;

                        records.Add(record);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching unread records from database.", e));
            }

            Log.Debug(() => "fetchUnreadRecords finished!", 6);
            return records;
        }

        private List<APlayer> FetchExternalOnlinePlayers()
        {
            Log.Debug(() => "FetchExternalOnlinePlayers starting!", 6);
            //Create return list
            List<APlayer> onlinePlayers = new List<APlayer>();
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return onlinePlayers;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var rows = connection.Query(@"
                        SELECT
                            `tbl_server`.`ServerID` AS `server_id`,
                            `tbl_server`.`ServerName` AS `server_name`,
                            `tbl_playerdata`.`PlayerID` AS `player_id`,
                            `tbl_playerdata`.`SoldierName` AS `player_name`,
                            `tbl_playerdata`.`EAGUID` AS `player_guid`
                        FROM
                            `tbl_currentplayers`
                        INNER JOIN
                            `tbl_server`
                        ON
                            `tbl_server`.`ServerID` = `tbl_currentplayers`.`ServerID`
                        INNER JOIN
                            `tbl_playerdata`
                        ON
                            `tbl_currentplayers`.`EA_GUID` = `tbl_playerdata`.`EAGUID`
                            AND
                            `tbl_server`.`GameID` = `tbl_playerdata`.`GameID`
                        WHERE
                            `tbl_currentplayers`.`ServerID` != @current_server_id
                        GROUP BY
                            `tbl_playerdata`.`PlayerID`",
                        new { current_server_id = _serverInfo.ServerID });
                    foreach (var row in rows)
                    {
                        APlayer ePlayer = FetchPlayer(false, false, false, null, (Int64)(Int32)row.player_id, null, null, null, null);
                        if (ePlayer != null)
                        {
                            ePlayer.player_server = new AServer(this)
                            {
                                ServerID = (Int64)(Int16)row.server_id,
                                ServerName = (String)row.server_name
                            };
                            onlinePlayers.Add(ePlayer);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching external online players.", e));
            }
            Log.Debug(() => "FetchExternalOnlinePlayers finished!", 6);
            return onlinePlayers;
        }

        private APlayer FetchMatchingExternalOnlinePlayer(String searchName)
        {
            Log.Debug(() => "FetchMatchingExternalOnlinePlayer starting!", 6);
            APlayer aPlayer = null;
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return null;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var rows = connection.Query(@"
                        SELECT
                            `tbl_server`.`ServerID` AS `server_id`,
                            `tbl_server`.`ServerName` AS `server_name`,
                            `tbl_playerdata`.`PlayerID` AS `player_id`,
                            `tbl_playerdata`.`SoldierName` AS `player_name`,
                            `tbl_playerdata`.`EAGUID` AS `player_guid`
                        FROM
                            `tbl_currentplayers`
                        INNER JOIN
                            `tbl_server`
                        ON
                            `tbl_server`.`ServerID` = `tbl_currentplayers`.`ServerID`
                        INNER JOIN
                            `tbl_playerdata`
                        ON
                            `tbl_currentplayers`.`EA_GUID` = `tbl_playerdata`.`EAGUID`
                            AND
                            `tbl_server`.`GameID` = `tbl_playerdata`.`GameID`
                        WHERE
                            `tbl_currentplayers`.`ServerID` != @current_server_id",
                        new { current_server_id = _serverInfo.ServerID });
                    foreach (var row in rows)
                    {
                        if (Regex.Match((String)row.player_name, searchName, RegexOptions.IgnoreCase).Success)
                        {
                            aPlayer = FetchPlayer(false, true, false, null, (Int64)(Int32)row.player_id, null, null, null, null);
                            if (aPlayer == null)
                            {
                                return null;
                            }
                            aPlayer.player_server = new AServer(this)
                            {
                                ServerID = (Int64)(Int16)row.server_id,
                                ServerName = (String)row.server_name
                            };
                            return aPlayer;
                        }
                    }
                    return null;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching matching external online from database.", e));
            }
            Log.Debug(() => "FetchMatchingExternalOnlinePlayer finished!", 6);
            return aPlayer;
        }

        private void RunPluginOrchestration()
        {
            Log.Debug(() => "RunPluginOrchestration starting!", 6);
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                Log.Debug(() => "Running plugin orchestration", 5);
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var rows = connection.Query(@"
                        SELECT
                            `setting_plugin`,
                            `setting_name`,
                            `setting_value`
                        FROM
                            `adkats_orchestration`
                        WHERE
                            `setting_server` = @server_id",
                        new { server_id = _serverInfo.ServerID });
                    foreach (var row in rows)
                    {
                        SetExternalPluginSetting((String)row.setting_plugin, (String)row.setting_name, (String)row.setting_value);
                        Threading.Wait(10);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while running plugin orchestration.", e));
            }

            Log.Debug(() => "RunPluginOrchestration finished!", 6);
        }


        private Int64 FetchNameBanCount()
        {
            Log.Debug(() => "fetchNameBanCount starting!", 7);
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return 0;
            }
            if (_NameBanCount >= 0 && (UtcNow() - _lastNameBanCountFetch).TotalSeconds < 30)
            {
                return _NameBanCount;
            }
            _lastNameBanCountFetch = UtcNow();
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    _NameBanCount = connection.ExecuteScalar<Int64>(@"
                        SELECT COUNT(ban_id)
                        FROM `adkats_bans`
                        WHERE `ban_enforceName` = 'Y'
                        AND `ban_status` = 'Active'");
                    return _NameBanCount;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching number of name bans.", e));
            }

            Log.Debug(() => "fetchNameBanCount finished!", 7);
            return -1;
        }


        private Int64 FetchGUIDBanCount()
        {
            Log.Debug(() => "fetchGUIDBanCount starting!", 7);
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return 0;
            }
            if (_GUIDBanCount >= 0 && (UtcNow() - _lastGUIDBanCountFetch).TotalSeconds < 30)
            {
                return _GUIDBanCount;
            }
            _lastGUIDBanCountFetch = UtcNow();
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    _GUIDBanCount = connection.ExecuteScalar<Int64>(@"
                        SELECT COUNT(ban_id)
                        FROM `adkats_bans`
                        WHERE `ban_enforceGUID` = 'Y'
                        AND `ban_status` = 'Active'");
                    return _GUIDBanCount;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching number of GUID bans.", e));
            }

            Log.Debug(() => "fetchGUIDBanCount finished!", 7);
            return -1;
        }


        private Int64 FetchIPBanCount()
        {
            Log.Debug(() => "fetchIPBanCount starting!", 7);
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return 0;
            }
            if (_IPBanCount >= 0 && (UtcNow() - _lastIPBanCountFetch).TotalSeconds < 30)
            {
                return _IPBanCount;
            }
            _lastIPBanCountFetch = UtcNow();
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    _IPBanCount = connection.ExecuteScalar<Int64>(@"
                        SELECT COUNT(ban_id)
                        FROM `adkats_bans`
                        WHERE `ban_enforceIP` = 'Y'
                        AND `ban_status` = 'Active'");
                    return _IPBanCount;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching number of IP bans.", e));
            }

            Log.Debug(() => "fetchIPBanCount finished!", 7);
            return -1;
        }

        private void RemoveUser(AUser user)
        {
            Log.Debug(() => "removeUser starting!", 6);
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    connection.Execute(
                        "DELETE FROM `" + _mySqlSchemaName + "`.`adkats_users` WHERE `user_id` = @user_id",
                        new { user_id = user.user_id });
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while removing user.", e));
            }
            Log.Debug(() => "removeUser finished!", 6);
        }

        private void RemoveRole(ARole aRole)
        {
            Log.Debug(() => "removeRole starting!", 6);
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                //Assign "Default Guest" to all users currently on this role
                ARole guestRole = null;
                if (_RoleKeyDictionary.TryGetValue("guest_default", out guestRole))
                {
                    foreach (AUser aUser in _userCache.Values)
                    {
                        if (aUser.user_role.role_key == aRole.role_key)
                        {
                            aUser.user_role = guestRole;
                        }
                        UploadUser(aUser);
                    }
                }
                else
                {
                    Log.Error("Could not fetch default guest user role. Unsafe to remove requested user role.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    connection.Execute(
                        "DELETE FROM `" + _mySqlSchemaName + "`.`adkats_rolecommands` WHERE `role_id` = @role_id",
                        new { role_id = aRole.role_id });
                    connection.Execute(
                        "DELETE FROM `" + _mySqlSchemaName + "`.`adkats_roles` WHERE `role_id` = @role_id",
                        new { role_id = aRole.role_id });
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while removing user.", e));
            }
            Log.Debug(() => "removeRole finished!", 6);
        }

        private void UploadUser(AUser aUser)
        {
            Log.Debug(() => "uploadUser starting!", 6);
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                Log.Debug(() => "Uploading user: " + aUser.user_name, 5);

                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    if (aUser.user_role == null)
                    {
                        ARole aRole = null;
                        if (_RoleKeyDictionary.TryGetValue("guest_default", out aRole))
                        {
                            aUser.user_role = aRole;
                        }
                        else
                        {
                            Log.Error("Unable to assign default guest role to user " + aUser.user_name + ". Unable to upload user.");
                            return;
                        }
                    }
                    String sql = @"
                        INSERT INTO
                            `adkats_users`
                        (
                            " + ((aUser.user_id > 0) ? ("`user_id`,") : ("")) + @"
                            `user_name`,
                            `user_email`,
                            `user_phone`,
                            `user_role`,
                            `user_expiration`,
                            `user_notes`
                        )
                        VALUES
                        (
                            " + ((aUser.user_id > 0) ? ("@user_id,") : ("")) + @"
                            @user_name,
                            @user_email,
                            @user_phone,
                            @user_role,
                            @user_expiration,
                            @user_notes
                        )
                        ON DUPLICATE KEY UPDATE
                            `user_name` = @user_name,
                            `user_email` = @user_email,
                            `user_phone` = @user_phone,
                            `user_role` = @user_role,
                            `user_expiration` = @user_expiration,
                            `user_notes` = @user_notes";

                    //Attempt to execute the query
                    if (aUser.user_id < 0)
                    {
                        sql += "; SELECT LAST_INSERT_ID();";
                        var lastId = connection.ExecuteScalar<Int64>(sql,
                            new
                            {
                                user_name = aUser.user_name,
                                user_email = aUser.user_email,
                                user_phone = aUser.user_phone,
                                user_role = aUser.user_role.role_id,
                                user_expiration = aUser.user_expiration,
                                user_notes = aUser.user_notes
                            });
                        if (lastId > 0)
                        {
                            aUser.user_id = lastId;
                            Log.Debug(() => "User uploaded to database SUCCESSFULY.", 5);
                        }
                        else
                        {
                            Log.Error("Unable to upload user " + aUser.user_name + " to database.");
                            return;
                        }
                    }
                    else
                    {
                        Int32 rowsAffected = connection.Execute(sql,
                            new
                            {
                                user_id = aUser.user_id,
                                user_name = aUser.user_name,
                                user_email = aUser.user_email,
                                user_phone = aUser.user_phone,
                                user_role = aUser.user_role.role_id,
                                user_expiration = aUser.user_expiration,
                                user_notes = aUser.user_notes
                            });
                        if (rowsAffected > 0)
                        {
                            Log.Debug(() => "User uploaded to database SUCCESSFULY.", 5);
                        }
                        else
                        {
                            Log.Error("Unable to upload user " + aUser.user_name + " to database.");
                            return;
                        }
                    }
                    //Run command to delete all current soldiers
                    connection.Execute(@"DELETE FROM `adkats_usersoldiers` where `user_id` = @user_id",
                        new { user_id = aUser.user_id });
                    //Upload/Update the user's soldier list
                    if (aUser.soldierDictionary.Count > 0)
                    {
                        //Refill user with current soldiers
                        foreach (APlayer aPlayer in aUser.soldierDictionary.Values)
                        {
                            Int32 rowsAffected = connection.Execute(@"
                                INSERT INTO
                                    `adkats_usersoldiers`
                                (
                                    `user_id`,
                                    `player_id`
                                )
                                VALUES
                                (
                                    @user_id,
                                    @player_id
                                )
                                ON DUPLICATE KEY UPDATE
                                    `player_id` = @player_id",
                                new { user_id = aUser.user_id, player_id = aPlayer.player_id });
                            if (rowsAffected > 0)
                            {
                                Log.Debug(() => "Soldier link " + aUser.user_id + "->" + aPlayer.player_id + " uploaded to database SUCCESSFULY.", 5);
                            }
                            else
                            {
                                Log.Error("Unable to upload soldier link for " + aUser.user_name + " to database.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating player access.", e));
            }

            Log.Debug(() => "uploadUser finished!", 6);
        }

        private void TryAddUserSoldier(AUser aUser, String soldierName)
        {
            try
            {
                //Attempt to fetch the soldier
                if (!String.IsNullOrEmpty(soldierName) && IsSoldierNameValid(soldierName))
                {
                    List<APlayer> matchingPlayers;
                    if (FetchMatchingPlayers(soldierName, out matchingPlayers, false))
                    {
                        if (matchingPlayers.Count > 0)
                        {
                            APlayer exactMatch = null;
                            foreach (APlayer aPlayer in matchingPlayers)
                            {
                                if (aPlayer.player_name == soldierName)
                                {
                                    exactMatch = aPlayer;
                                    break;
                                }
                            }
                            if (matchingPlayers.Count > 10)
                            {
                                if (exactMatch != null)
                                {
                                    exactMatch.LastUsage = UtcNow();
                                    bool playerDuplicate = false;
                                    //Make sure the player is not already assigned to another user
                                    lock (_userCache)
                                    {
                                        if (_userCache.Values.Any(innerUser => innerUser.soldierDictionary.ContainsKey(exactMatch.player_id)))
                                        {
                                            playerDuplicate = true;
                                        }
                                    }
                                    if (!playerDuplicate)
                                    {
                                        if (aUser.soldierDictionary.ContainsKey(exactMatch.player_id))
                                        {
                                            aUser.soldierDictionary.Remove(exactMatch.player_id);
                                        }
                                        aUser.soldierDictionary.Add(exactMatch.player_id, exactMatch);
                                        return;
                                    }
                                    else
                                    {
                                        Log.Error("Player " + exactMatch.GetVerboseName() + "(" + _gameIDDictionary[exactMatch.game_id] + ") already assigned to a user.");
                                    }
                                }
                                Log.Error("Too many players matched the query, unable to add.");
                                return;
                            }
                            foreach (APlayer matchingPlayer in matchingPlayers)
                            {
                                matchingPlayer.LastUsage = UtcNow();
                                bool playerDuplicate = false;
                                //Make sure the player is not already assigned to another user
                                lock (_userCache)
                                {
                                    if (_userCache.Values.Any(innerUser => innerUser.soldierDictionary.ContainsKey(matchingPlayer.player_id)))
                                    {
                                        playerDuplicate = true;
                                    }
                                }
                                if (!playerDuplicate)
                                {
                                    if (aUser.soldierDictionary.ContainsKey(matchingPlayer.player_id))
                                    {
                                        aUser.soldierDictionary.Remove(matchingPlayer.player_id);
                                    }
                                    aUser.soldierDictionary.Add(matchingPlayer.player_id, matchingPlayer);
                                }
                                else
                                {
                                    Log.Error("Player " + matchingPlayer.GetVerboseName() + "(" + _gameIDDictionary[matchingPlayer.game_id] + ") already assigned to a user.");
                                }
                            }
                            return;
                        }
                        Log.Error("Players matching '" + soldierName + "' not found in database. Unable to assign to user.");
                    }
                }
                else
                {
                    Log.Error("'" + soldierName + "' is an invalid player name. Unable to assign to user.");
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while attempting to add user soldier.", e));
            }
        }


        private void UploadRole(ARole aRole)
        {
            Log.Debug(() => "uploadRole starting!", 6);
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                Log.Debug(() => "Uploading role: " + aRole.role_name, 5);

                //Open db connection
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    //Upload/Update the main role object
                    {
                        String sql = @"
                                INSERT INTO
                                    `adkats_roles`
                                (
                                    `role_key`,
                                    `role_name`
                                )
                                VALUES
                                (
                                    @role_key,
                                    @role_name
                                )
                                ON DUPLICATE KEY UPDATE
                                    `role_key` = @role_key,
                                    `role_name` = @role_name";
                        if (aRole.role_id < 0)
                        {
                            sql += "; SELECT LAST_INSERT_ID();";
                            var lastId = connection.ExecuteScalar<Int64>(sql,
                                new { role_key = aRole.role_key, role_name = aRole.role_name });
                            if (lastId > 0)
                            {
                                aRole.role_id = lastId;
                                Log.Debug(() => "Role " + aRole.role_name + " uploaded to database.", 5);
                            }
                            else
                            {
                                Log.Error("Unable to upload role " + aRole.role_name + " to database.");
                                return;
                            }
                        }
                        else
                        {
                            Int32 rowsAffected = connection.Execute(sql,
                                new { role_key = aRole.role_key, role_name = aRole.role_name });
                            if (rowsAffected > 0)
                            {
                                Log.Debug(() => "Role " + aRole.role_name + " uploaded to database.", 5);
                            }
                            else
                            {
                                Log.Error("Unable to upload role " + aRole.role_name + " to database.");
                                return;
                            }
                        }
                    }
                    //Delete all current allowed commands
                    connection.Execute(@"DELETE FROM `adkats_rolecommands` where `role_id` = @role_id",
                        new { role_id = aRole.role_id });
                    foreach (ACommand aCommand in aRole.RoleAllowedCommands.Values.ToList())
                    {
                        //Upload the role's allowed commands
                        Int32 rowsAffected = connection.Execute(@"
                                    INSERT INTO
                                        `adkats_rolecommands`
                                    (
                                        `role_id`,
                                        `command_id`
                                    )
                                    VALUES
                                    (
                                        @role_id,
                                        @command_id
                                    )
                                    ON DUPLICATE KEY UPDATE
                                        `role_id` = @role_id,
                                        `command_id` = @command_id",
                            new { role_id = aRole.role_id, command_id = aCommand.command_id });
                        if (rowsAffected > 0)
                        {
                            Log.Debug(() => "Role-command " + aRole.role_name + " uploaded to database.", 5);
                        }
                        else
                        {
                            Log.Error("Unable to upload role-command for " + aRole.role_name + ".");
                            return;
                        }
                    }
                    //Delete all current role groups
                    {
                        Int32 rowsAffected = connection.Execute(@"DELETE FROM `adkats_rolegroups` where `role_id` = @role_id",
                            new { role_id = aRole.role_id });
                        if (rowsAffected > 0)
                        {
                            Log.Debug(() => "Deleted existing database role-group info for " + aRole.role_name + ".", 5);
                        }
                    }
                    foreach (ASpecialGroup aGroup in aRole.RoleSetGroups.Values.ToList())
                    {
                        //Upload the role's set groups
                        Int32 rowsAffected = connection.Execute(@"
                                    INSERT INTO
                                        `adkats_rolegroups`
                                    (
                                        `role_id`,
                                        `group_key`
                                    )
                                    VALUES
                                    (
                                        @role_id,
                                        @group_key
                                    )
                                    ON DUPLICATE KEY UPDATE
                                        `role_id` = @role_id,
                                        `group_key` = @group_key",
                            new { role_id = aRole.role_id, group_key = aGroup.group_key });
                        if (rowsAffected > 0)
                        {
                            Log.Debug(() => "Role-group " + aGroup.group_key + " for " + aRole.role_name + " uploaded to database.", 5);
                        }
                        else
                        {
                            Log.Error("Unable to upload role-group " + aGroup.group_key + " for " + aRole.role_name + ".");
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while uploading role.", e));
            }
            Log.Debug(() => "uploadRole finished!", 6);
        }


        private void UploadBan(ABan aBan)
        {
            Log.Debug(() => "uploadBan starting!", 6);

            Boolean success = false;
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            if (aBan == null)
            {
                Log.Error("Ban invalid in uploadBan.");
            }
            else
            {
                try
                {
                    //Upload the inner record if needed
                    if (aBan.ban_record.record_id < 0)
                    {
                        if (!UploadRecord(aBan.ban_record))
                        {
                            return;
                        }
                    }

                    using (MySqlConnection connection = GetDatabaseConnection())
                    {
                        if (String.IsNullOrEmpty(aBan.ban_status))
                        {
                            aBan.ban_exception = new AException("Ban status was null or empty when posting.");
                            Log.HandleException(aBan.ban_exception);
                            return;
                        }
                        if (aBan.ban_status != "Active" && aBan.ban_status != "Disabled" && aBan.ban_status != "Expired")
                        {
                            aBan.ban_exception = new AException("Ban status of '" + aBan.ban_status + "' was invalid when posting.");
                            Log.HandleException(aBan.ban_exception);
                            return;
                        }
                        if (String.IsNullOrEmpty(aBan.ban_notes))
                        {
                            aBan.ban_notes = "NoNotes";
                        }
                        //Handle permaban case
                        Int32 banDurationMinutes;
                        if (aBan.ban_record.command_action.command_key.Contains("player_ban_perm"))
                        {
                            banDurationMinutes = (Int32)UtcNow().AddYears(20).Subtract(UtcNow()).TotalMinutes;
                        }
                        else
                        {
                            banDurationMinutes = aBan.ban_record.command_numeric;
                        }
                        DateTime banStartTime;
                        if (aBan.ban_record.command_action.command_key == "player_ban_perm_future")
                        {
                            banStartTime = aBan.ban_record.record_time + TimeSpan.FromMinutes(aBan.ban_record.command_numeric);
                        }
                        else
                        {
                            banStartTime = aBan.ban_record.record_time;
                        }
                        //Attempt to execute the query
                        Int32 rowsAffected = connection.Execute(@"
                            INSERT INTO
                            `" + _mySqlSchemaName + @"`.`adkats_bans`
                            (
                                `player_id`,
                                `latest_record_id`,
                                `ban_status`,
                                `ban_notes`,
                                `ban_startTime`,
                                `ban_endTime`,
                                `ban_enforceName`,
                                `ban_enforceGUID`,
                                `ban_enforceIP`,
                                `ban_sync`
                            )
                            VALUES
                            (
                                @player_id,
                                @latest_record_id,
                                @ban_status,
                                @ban_notes,
                                @ban_startTime,
                                DATE_ADD(@ban_startTime, INTERVAL @ban_durationMinutes MINUTE),
                                @ban_enforceName,
                                @ban_enforceGUID,
                                @ban_enforceIP,
                                @ban_sync
                            )
                            ON DUPLICATE KEY
                            UPDATE
                                `latest_record_id` = @latest_record_id,
                                `ban_status` = @ban_status,
                                `ban_notes` = @ban_notes,
                                `ban_startTime` = @ban_startTime,
                                `ban_endTime` = DATE_ADD(@ban_startTime, INTERVAL @ban_durationMinutes MINUTE),
                                `ban_enforceName` = @ban_enforceName,
                                `ban_enforceGUID` = @ban_enforceGUID,
                                `ban_enforceIP` = @ban_enforceIP,
                                `ban_sync` = @ban_sync",
                            new
                            {
                                player_id = aBan.ban_record.target_player.player_id,
                                latest_record_id = aBan.ban_record.record_id,
                                ban_status = aBan.ban_status,
                                ban_notes = aBan.ban_notes,
                                ban_enforceName = aBan.ban_enforceName ? 'Y' : 'N',
                                ban_enforceGUID = aBan.ban_enforceGUID ? 'Y' : 'N',
                                ban_enforceIP = aBan.ban_enforceIP ? 'Y' : 'N',
                                ban_sync = "*" + _serverInfo.ServerID + "*",
                                ban_durationMinutes = banDurationMinutes,
                                ban_startTime = banStartTime
                            });
                        if (rowsAffected >= 0)
                        {
                            //Rows affected should be > 0
                            Log.Debug(() => "Success Uploading Ban on player " + aBan.ban_record.target_player.player_id, 5);
                            success = true;
                        }
                        if (success)
                        {
                            var banRow = connection.QueryFirstOrDefault(@"
                                SELECT
                                    `ban_id`,
                                    `ban_startTime`,
                                    `ban_endTime`,
                                    `ban_status`
                                FROM
                                    `adkats_bans`
                                WHERE
                                    `player_id` = @player_id",
                                new { player_id = aBan.ban_record.target_player.player_id });
                            if (banRow != null)
                            {
                                aBan.ban_id = (Int64)banRow.ban_id;
                                aBan.ban_startTime = (DateTime)banRow.ban_startTime;
                                aBan.ban_endTime = (DateTime)banRow.ban_endTime;
                                String status = (String)banRow.ban_status;
                                if (status != aBan.ban_status)
                                {
                                    aBan.ban_exception = new AException("Ban status was invalid when confirming ban post. Your database is not in strict mode.");
                                    Log.HandleException(aBan.ban_exception);
                                    return;
                                }
                                Log.Debug(() => "Ban ID: " + aBan.ban_id, 5);
                            }
                            else
                            {
                                Log.Error("Could not fetch ban information after upload");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error while uploading new ban.", e));
                }
            }
            Log.Debug(() => "uploadBan finished!", 6);
        }

        private Boolean FetchMatchingPlayers(String playerName, out List<APlayer> resultPlayers, Boolean verbose)
        {
            Log.Debug(() => "FetchMatchingPlayers starting!", 6);
            resultPlayers = new List<APlayer>();
            if (String.IsNullOrEmpty(playerName))
            {
                if (verbose)
                {
                    Log.Error("Player id was blank when fetching matching players.");
                }
                return false;
            }
            using (MySqlConnection connection = GetDatabaseConnection())
            {
                var playerIds = connection.Query<Int64>(@"
                    SELECT `PlayerID`
                    FROM `tbl_playerdata`
                    WHERE `SoldierName` LIKE @namePattern",
                    new { namePattern = "%" + playerName + "%" });
                foreach (var playerId in playerIds)
                {
                    APlayer aPlayer = FetchPlayer(false, false, false, null, playerId, null, null, null, null);
                    if (aPlayer != null)
                    {
                        resultPlayers.Add(aPlayer);
                    }
                }
                if (resultPlayers.Count == 0)
                {
                    if (verbose)
                    {
                        Log.Error("No players found matching '" + playerName + "'");
                    }
                    return false;
                }
            }
            Log.Debug(() => "FetchMatchingPlayers finished!", 6);
            return true;
        }

        private APlayer FetchPlayer(Boolean allowUpdate, Boolean allowOtherGames, Boolean allowNameSubstringSearch, Int32? gameID, Int64 playerID, String playerName, String playerGUID, String playerIP, String playerDiscordID)
        {
            Log.Debug(() => "fetchPlayer starting!", 6);
            //Create return object
            APlayer aPlayer = null;
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                //If AdKats is disconnected from the database, return the player as-is
                aPlayer = new APlayer(this)
                {
                    game_id = _serverInfo.GameID,
                    player_name = playerName,
                    player_guid = playerGUID,
                    LastUsage = UtcNow()
                };
                aPlayer.SetIP(playerIP);
                AssignPlayerRole(aPlayer);
                Log.Warn(aPlayer.player_name + " " + aPlayer.player_guid + " " + aPlayer.player_ip + " loaded without a database connection!");
                return aPlayer;
            }
            if (playerID < 0 && String.IsNullOrEmpty(playerName) && String.IsNullOrEmpty(playerGUID) && String.IsNullOrEmpty(playerIP) && String.IsNullOrEmpty(playerDiscordID))
            {
                Log.Error("Attempted to fetch player with no information.");
            }
            else
            {
                try
                {
                    if (playerID > 0)
                    {
                        aPlayer = GetFetchedPlayers().FirstOrDefault(dPlayer => dPlayer.player_id == playerID);
                    }
                    if (aPlayer != null)
                    {
                        Log.Debug(() => "Player " + playerID + " successfully fetched from pre-fetch list by ID.", 6);
                        aPlayer.LastUsage = UtcNow();
                        return aPlayer;
                    }
                    if (!String.IsNullOrEmpty(playerGUID))
                    {
                        aPlayer = GetFetchedPlayers().FirstOrDefault(dPlayer => dPlayer.player_guid == playerGUID);
                    }
                    if (aPlayer != null)
                    {
                        Log.Debug(() => "Player " + playerID + " successfully fetched from pre-fetch list by GUID.", 6);
                        aPlayer.LastUsage = UtcNow();
                        return aPlayer;
                    }
                    if (!String.IsNullOrEmpty(playerIP))
                    {
                        aPlayer = GetFetchedPlayers().FirstOrDefault(dPlayer => dPlayer.player_ip == playerIP);
                    }
                    if (aPlayer != null)
                    {
                        Log.Debug(() => "Player " + playerID + " successfully fetched from pre-fetch list by IP.", 6);
                        aPlayer.LastUsage = UtcNow();
                        return aPlayer;
                    }
                    if (!String.IsNullOrEmpty(playerName))
                    {
                        aPlayer = GetFetchedPlayers().FirstOrDefault(dPlayer => dPlayer.player_name == playerName);
                    }
                    if (aPlayer != null)
                    {
                        Log.Debug(() => "Player " + playerID + " successfully fetched from pre-fetch list by Name.", 6);
                        aPlayer.LastUsage = UtcNow();
                        return aPlayer;
                    }
                    if (!String.IsNullOrEmpty(playerDiscordID))
                    {
                        aPlayer = GetFetchedPlayers().FirstOrDefault(dPlayer => dPlayer.player_discord_id == playerDiscordID);
                    }
                    if (aPlayer != null)
                    {
                        Log.Debug(() => "Player " + playerID + " successfully fetched from pre-fetch list by Discord ID.", 6);
                        aPlayer.LastUsage = UtcNow();
                        return aPlayer;
                    }
                    using (MySqlConnection connection = GetDatabaseConnection())
                    {
                        {
                            String sql = @"
                            SELECT
                                `tbl_playerdata`.`PlayerID` as `player_id`,
                                `tbl_playerdata`.`SoldierName` as `player_name`,
                                `tbl_playerdata`.`EAGUID` as `player_guid`,
                                `tbl_playerdata`.`PBGUID` as `player_pbguid`,
                                `tbl_playerdata`.`IP_Address` as `player_ip`,
                                `tbl_playerdata`.`DiscordID` as `player_discord_id`,
                                `tbl_playerdata`.`ClanTag` as `player_clantag`,
                                `adkats_battlecries`.`player_battlecry`,
                                `adkats_battlelog_players`.`persona_id` as `player_personaID`,
                                `adkats_battlelog_players`.`user_id` as `player_userID`";
                            if (_serverInfo.GameID > 0)
                            {
                                sql += ",`GameID` as `game_id` ";
                            }
                            sql += "FROM `" + _mySqlSchemaName + @"`.`tbl_playerdata`
                                    LEFT JOIN `adkats_battlecries`
                                    ON `tbl_playerdata`.`PlayerID` = `adkats_battlecries`.`player_id`
                                    LEFT JOIN `adkats_battlelog_players`
                                    ON `tbl_playerdata`.`PlayerID` = `adkats_battlelog_players`.`player_id` ";
                            var conditions = new List<String>();
                            var dynParams = new DynamicParameters();

                            if (playerID >= 0)
                            {
                                conditions.Add("`PlayerID` = @playerID");
                                dynParams.Add("playerID", playerID);
                            }
                            if (!String.IsNullOrEmpty(playerGUID))
                            {
                                conditions.Add("`EAGUID` = @playerGUID");
                                dynParams.Add("playerGUID", playerGUID);
                            }
                            if (String.IsNullOrEmpty(playerGUID) && !String.IsNullOrEmpty(playerName))
                            {
                                conditions.Add("`SoldierName` LIKE @playerName");
                                dynParams.Add("playerName", allowNameSubstringSearch ? "%" + playerName + "%" : playerName);
                            }
                            if (String.IsNullOrEmpty(playerGUID) && !String.IsNullOrEmpty(playerIP))
                            {
                                conditions.Add("`IP_Address` = @playerIP");
                                dynParams.Add("playerIP", playerIP);
                            }
                            if (String.IsNullOrEmpty(playerGUID) && !String.IsNullOrEmpty(playerDiscordID))
                            {
                                conditions.Add("`DiscordID` = @playerDiscordID");
                                dynParams.Add("playerDiscordID", playerDiscordID);
                            }
                            if (conditions.Any())
                            {
                                sql += " WHERE (" + String.Join(" OR ", conditions) + ")";
                            }
                            if ((_serverInfo.GameID > 0 && !allowOtherGames) || gameID != null)
                            {
                                if (gameID != null)
                                {
                                    sql += " AND `GameID` = @gameID";
                                    dynParams.Add("gameID", gameID);
                                }
                                else
                                {
                                    sql += " AND `GameID` = @serverGameID";
                                    dynParams.Add("serverGameID", _serverInfo.GameID);
                                }
                            }
                            sql += @"
                            LIMIT 1";
                            if (_debugDisplayPlayerFetches)
                            {
                                Log.Debug(() => "FetchPlayer SQL: " + sql, 3);
                            }
                            var row = connection.QueryFirstOrDefault(sql, dynParams);
                            if (row != null)
                            {
                                aPlayer = new APlayer(this);
                                //Player ID will never be null
                                aPlayer.player_id = (Int64)(Int32)row.player_id;
                                if (_serverInfo.GameID > 0)
                                {
                                    aPlayer.game_id = (Int32)(SByte)row.game_id;
                                }
                                if (row.player_name != null)
                                {
                                    aPlayer.player_name = (String)row.player_name;
                                }
                                if (row.player_guid != null)
                                {
                                    aPlayer.player_guid = (String)row.player_guid;
                                }
                                if (row.player_pbguid != null)
                                {
                                    aPlayer.player_pbguid = (String)row.player_pbguid;
                                }
                                if (row.player_ip != null)
                                {
                                    aPlayer.SetIP((String)row.player_ip);
                                }
                                if (row.player_discord_id != null)
                                {
                                    aPlayer.player_discord_id = (String)row.player_discord_id;
                                }
                                if (row.player_clantag != null)
                                {
                                    aPlayer.player_clanTag = (String)row.player_clantag;
                                }
                                if (row.player_battlecry != null)
                                {
                                    aPlayer.player_battlecry = (String)row.player_battlecry;
                                }
                                if (row.player_personaID != null)
                                {
                                    aPlayer.player_battlelog_personaID = row.player_personaID.ToString();
                                }
                                if (row.player_userID != null)
                                {
                                    aPlayer.player_battlelog_userID = row.player_userID.ToString();
                                }
                                if (!String.IsNullOrEmpty(aPlayer.player_battlelog_personaID) && !String.IsNullOrEmpty(aPlayer.player_battlelog_userID))
                                {
                                    aPlayer.BLInfoStored = true;
                                }
                            }
                            else
                            {
                                var infoString = "No player matching search information. " + allowUpdate + ", " + allowOtherGames + ", " + ((gameID != null) ? (gameID.ToString()) : ("No game ID")) + ", " + playerID + ", " + ((!String.IsNullOrEmpty(playerName)) ? (playerName) : ("No name search")) + ", " + ((!String.IsNullOrEmpty(playerGUID)) ? (playerGUID) : ("No GUID search")) + ", " + ((!String.IsNullOrEmpty(playerIP)) ? (playerIP) : ("No IP search")) + ", " + ((!String.IsNullOrEmpty(playerDiscordID)) ? (playerDiscordID) : ("No Discord ID search"));
                                if (_debugDisplayPlayerFetches)
                                {
                                    Log.Info(infoString);
                                }
                                else
                                {
                                    Log.Debug(() => infoString, 4);
                                }
                            }
                        }
                        if (allowUpdate)
                        {
                            if (aPlayer == null)
                            {
                                Log.Debug(() => "Adding player to database.", 5);
                                Int32? useableGameID = null;
                                if (gameID != null)
                                {
                                    useableGameID = gameID;
                                }
                                else if (_serverInfo.GameID > 0)
                                {
                                    useableGameID = (Int32?)_serverInfo.GameID;
                                }
                                String insertSql;
                                object insertParams;
                                //Set the insert command structure
                                if (useableGameID != null)
                                {
                                    insertSql = @"
                                        INSERT INTO `" + _mySqlSchemaName + @"`.`tbl_playerdata`
                                        (
                                            `GameID`,
                                            `SoldierName`,
                                            `EAGUID`,
                                            `IP_Address`
                                        )
                                        VALUES
                                        (
                                            @GameID,
                                            @SoldierName,
                                            @EAGUID,
                                            @IP_Address
                                        )
                                        ON DUPLICATE KEY
                                        UPDATE
                                            `PlayerID` = LAST_INSERT_ID(`PlayerID`),
                                            `SoldierName` = @SoldierName,
                                            `EAGUID` = @EAGUID,
                                            `IP_Address` = @IP_Address; SELECT LAST_INSERT_ID();";
                                    insertParams = new
                                    {
                                        GameID = _serverInfo.GameID,
                                        SoldierName = String.IsNullOrEmpty(playerName) ? null : playerName,
                                        EAGUID = String.IsNullOrEmpty(playerGUID) ? null : playerGUID,
                                        IP_Address = String.IsNullOrEmpty(playerIP) ? null : playerIP
                                    };
                                }
                                else
                                {
                                    insertSql = @"
                                        INSERT INTO `" + _mySqlSchemaName + @"`.`tbl_playerdata`
                                        (
                                            `SoldierName`,
                                            `EAGUID`,
                                            `IP_Address`
                                        )
                                        VALUES
                                        (
                                            @SoldierName,
                                            @EAGUID,
                                            @IP_Address
                                        )
                                        ON DUPLICATE KEY
                                        UPDATE
                                            `PlayerID` = LAST_INSERT_ID(`PlayerID`),
                                            `SoldierName` = @SoldierName,
                                            `EAGUID` = @EAGUID,
                                            `IP_Address` = @IP_Address; SELECT LAST_INSERT_ID();";
                                    insertParams = new
                                    {
                                        SoldierName = String.IsNullOrEmpty(playerName) ? null : playerName,
                                        EAGUID = String.IsNullOrEmpty(playerGUID) ? null : playerGUID,
                                        IP_Address = String.IsNullOrEmpty(playerIP) ? null : playerIP
                                    };
                                }
                                //Attempt to execute the query
                                var lastId = connection.ExecuteScalar<Int64>(insertSql, insertParams);
                                if (lastId > 0)
                                {
                                    //Rows affected should be > 0
                                    aPlayer = new APlayer(this)
                                    {
                                        player_id = lastId,
                                        player_name = playerName,
                                        player_guid = playerGUID
                                    };
                                    aPlayer.SetIP(playerIP);
                                    if (useableGameID != null)
                                    {
                                        aPlayer.game_id = (long)useableGameID;
                                    }
                                    else
                                    {
                                        aPlayer.game_id = _serverInfo.GameID;
                                    }
                                    aPlayer.player_new = true;
                                }
                                else
                                {
                                    Log.Error("Unable to add player to database.");
                                    return null;
                                }
                            }
                            //check for name changes
                            if (!String.IsNullOrEmpty(playerName) && !String.IsNullOrEmpty(aPlayer.player_guid) && playerName != aPlayer.player_name)
                            {
                                aPlayer.player_name_previous = aPlayer.player_name;
                                aPlayer.player_name = playerName;
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
                                Log.Debug(() => aPlayer.player_name_previous + " changed their name to " + playerName + ". Updating the database.", 2);
                                if (_ShowPlayerNameChangeAnnouncement)
                                {
                                    OnlineAdminSayMessage(aPlayer.player_name_previous + " changed their name to " + playerName);
                                }
                                UpdatePlayer(aPlayer);
                            }
                        }

                        if (aPlayer == null)
                        {
                            return null;
                        }

                        //Assign player role
                        AssignPlayerRole(aPlayer);

                        //Pull player first seen
                        if (aPlayer.player_id > 0)
                        {
                            {
                                var firstSeenRow = connection.QueryFirstOrDefault(@"
                                SELECT
                                    FirstSeenOnServer
                                FROM
                                    tbl_server_player
                                        INNER JOIN
                                    tbl_playerstats ON tbl_playerstats.StatsID = tbl_server_player.StatsID
                                WHERE
                                    tbl_server_player.PlayerID = @player_id
                                ORDER BY
                                    tbl_playerstats.FirstSeenOnServer
                                LIMIT 1",
                                    new { player_id = aPlayer.player_id });
                                if (firstSeenRow != null)
                                {
                                    aPlayer.player_firstseen = (DateTime)firstSeenRow.FirstSeenOnServer;
                                }
                                else
                                {
                                    aPlayer.player_firstseen = UtcNow();
                                    Log.Debug(() => "No stats found to fetch first seen time.", 5);
                                }
                            }

                            {
                                var playtimeRow = connection.QueryFirstOrDefault(@"
                                SELECT
                                    (Playtime/60.0) as playtime_minutes
                                FROM
                                    tbl_server_player
                                        INNER JOIN
                                    tbl_playerstats ON tbl_playerstats.StatsID = tbl_server_player.StatsID
                                WHERE
                                    tbl_server_player.PlayerID = @player_id
                                AND
                                    tbl_server_player.Serverid = @server_id
                                ORDER BY
                                    Serverid ASC",
                                    new { player_id = aPlayer.player_id, server_id = _serverInfo.ServerID });
                                if (playtimeRow != null)
                                {
                                    aPlayer.player_serverplaytime = TimeSpan.FromMinutes((Double)playtimeRow.playtime_minutes);
                                }
                                else
                                {
                                    Log.Debug(() => "No stats found to fetch time on server.", 5);
                                }
                            }
                        }
                    }
                    if (aPlayer != null && aPlayer.player_id > 0)
                    {
                        aPlayer.LastUsage = UtcNow();
                        AddFetchedPlayer(aPlayer);
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error while fetching player.", e));
                }
            }
            Log.Debug(() => "fetchPlayer finished!", 6);
            if (aPlayer != null)
            {
                aPlayer.LastUsage = UtcNow();
            }
            return aPlayer;
        }

        private void AddFetchedPlayer(APlayer aPlayer)
        {
            try
            {
                lock (_FetchedPlayers)
                {
                    //Remove all old values
                    List<Int64> removeIDs = _FetchedPlayers.Values.ToList()
                        .Where(dPlayer => (UtcNow() - dPlayer.LastUsage).TotalMinutes > 120)
                        .Select(dPlayer => dPlayer.player_id).ToList();
                    foreach (Int64 removeID in removeIDs)
                    {
                        _FetchedPlayers.Remove(removeID);
                    }
                    aPlayer.LastUsage = UtcNow();
                    _FetchedPlayers[aPlayer.player_id] = aPlayer;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error adding new fetched player.", e));
            }
        }

        private List<APlayer> GetFetchedPlayers()
        {
            try
            {
                lock (_FetchedPlayers)
                {
                    return _FetchedPlayers.Values.ToList();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error getting fetched players.", e));
            }
            return new List<APlayer>();
        }

        private APlayer UpdatePlayer(APlayer aPlayer)
        {
            Log.Debug(() => "updatePlayer starting!", 6);
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return aPlayer;
            }
            if (aPlayer == null || aPlayer.player_id < 0 || (String.IsNullOrEmpty(aPlayer.player_name) && String.IsNullOrEmpty(aPlayer.player_guid) & String.IsNullOrEmpty(aPlayer.player_ip)))
            {
                Log.Error("Attempted to update player without required information.");
            }
            else
            {
                try
                {
                    using (MySqlConnection connection = GetDatabaseConnection())
                    {
                        if (connection.Execute(@"
                            UPDATE IGNORE
                                `tbl_playerdata`
                            SET
                                `SoldierName` = @player_name,
                                `EAGUID` = @player_guid,
                                `ClanTag` = @player_clanTag,
                                `IP_Address` = @player_ip
                                -- `DiscordID` = @player_discord_id
                            WHERE
                                `PlayerID` = @player_id",
                            new
                            {
                                player_id = aPlayer.player_id,
                                player_name = aPlayer.player_name,
                                player_guid = aPlayer.player_guid,
                                player_clanTag = String.IsNullOrEmpty(aPlayer.player_clanTag) ? "" : aPlayer.player_clanTag,
                                player_ip = String.IsNullOrEmpty(aPlayer.player_ip) ? null : aPlayer.player_ip
                                // Do not update the discord ID from AdKats -> this is fully handled by the E4GL discord bot.
                            }) > 0)
                        {
                            Log.Debug(() => "Update player info success.", 5);
                        }
                        if (!String.IsNullOrEmpty(aPlayer.player_battlelog_personaID) && !String.IsNullOrEmpty(aPlayer.player_battlelog_personaID) && !aPlayer.BLInfoStored)
                        {
                            connection.Execute(@"
                                REPLACE INTO
                                    `adkats_battlelog_players`
                                (
                                    `player_id`,
                                    `persona_id`,
                                    `user_id`
                                )
                                VALUES
                                (
                                    @player_id,
                                    @persona_id,
                                    @user_id
                                )",
                                new
                                {
                                    player_id = aPlayer.player_id,
                                    persona_id = aPlayer.player_battlelog_personaID,
                                    user_id = aPlayer.player_battlelog_userID
                                });
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error while updating player.", e));
                }
            }
            Log.Debug(() => "updatePlayer finished!", 6);
            return aPlayer;
        }

        private void UpdatePopulatorPlayers()
        {
            Log.Debug(() => "UpdatePopulatingPlayers starting!", 6);
            try
            {
                //List for current valid populator player IDs
                List<Int64> validIDs = new List<Int64>();
                lock (_populatorPlayers)
                {
                    //Rejection case
                    if (!_PopulatorMonitor)
                    {
                        _populatorPlayers.Clear();
                        return;
                    }
                    List<APlayer> populatorsPastWeek = GetPopulatingPlayers(TimeSpan.FromDays(7), _PopulatorMinimumPopulationCountPastWeek, _PopulatorPopulatingThisServerOnly);
                    List<APlayer> populatorsPast2Weeks = GetPopulatingPlayers(TimeSpan.FromDays(14), _PopulatorMinimumPopulationCountPast2Weeks, _PopulatorPopulatingThisServerOnly);
                    //Find all populators from the past week
                    foreach (APlayer aPlayer in populatorsPastWeek)
                    {
                        if (!_pluginEnabled)
                        {
                            return;
                        }
                        //If using specified populators only, reject any non-specified populator entries
                        if (_PopulatorUseSpecifiedPopulatorsOnly && !GetMatchingVerboseASPlayersOfGroup("whitelist_populator", aPlayer).Any())
                        {
                            continue;
                        }
                        //Add the valid ID
                        if (!validIDs.Contains(aPlayer.player_id))
                        {
                            validIDs.Add(aPlayer.player_id);
                        }
                        //Add the player
                        if (!_populatorPlayers.ContainsKey(aPlayer.player_name))
                        {
                            if (_firstPlayerListComplete)
                            {
                                Log.Info("Adding " + aPlayer.player_name + " to current populator players.");
                            }
                        }
                        _populatorPlayers[aPlayer.player_name] = aPlayer;
                    }
                    //Find all populators from the past 2 weeks
                    foreach (APlayer aPlayer in populatorsPast2Weeks)
                    {
                        if (!_pluginEnabled)
                        {
                            return;
                        }
                        //If using specified populators only, reject any non-specified populator entries
                        if (_PopulatorUseSpecifiedPopulatorsOnly && !GetMatchingVerboseASPlayersOfGroup("whitelist_populator", aPlayer).Any())
                        {
                            continue;
                        }
                        //Add the valid ID
                        if (!validIDs.Contains(aPlayer.player_id))
                        {
                            validIDs.Add(aPlayer.player_id);
                        }
                        //Add the player
                        if (!_populatorPlayers.ContainsKey(aPlayer.player_name))
                        {
                            if (_firstPlayerListComplete)
                            {
                                Log.Info("Adding " + aPlayer.player_name + " to current populator players.");
                            }
                        }
                        _populatorPlayers[aPlayer.player_name] = aPlayer;
                    }
                    //Remove invalid players
                    foreach (APlayer aPlayer in _populatorPlayers.Values.Where(dPlayer => !validIDs.Contains(dPlayer.player_id)).ToList())
                    {
                        if (!_pluginEnabled)
                        {
                            return;
                        }
                        if (_firstPlayerListComplete)
                        {
                            Log.Info("Removing " + aPlayer.player_name + " from current populator players.");
                        }
                        _populatorPlayers.Remove(aPlayer.player_name);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching baserape causing players", e));
            }
            Log.Debug(() => "UpdatePopulatingPlayers finished!", 6);
        }

        private List<APlayer> GetPopulatingPlayers(TimeSpan duration, Int32 minPopulations, Boolean thisServerOnly)
        {
            Log.Debug(() => "GetPopulatingPlayers starting!", 6);
            List<APlayer> resultPlayers = new List<APlayer>();
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    String sql;
                    object parameters;
                    if (thisServerOnly)
                    {
                        sql = @"
                            SELECT
                                *
                            FROM
                            (SELECT
                                `target_id` AS `player_id`,
                                `target_name` AS `player_name`,
                                COUNT(`record_id`) AS `population_count`
                            FROM
                                `adkats_records_main`
                            WHERE
                                `server_id` = @server_id
                            AND
                                `command_type` = 88
                            AND
                                DATE_ADD(`record_time`, INTERVAL @duration_minutes MINUTE) > UTC_TIMESTAMP()
                            GROUP BY
                                `target_id`
                            ORDER BY
                                `population_count` DESC, `target_name` ASC) AS InnerResults
                            WHERE
                                `population_count` >= @population_minimum";
                        parameters = new
                        {
                            server_id = _serverInfo.ServerID,
                            duration_minutes = (Int32)duration.TotalMinutes,
                            population_minimum = minPopulations
                        };
                    }
                    else
                    {
                        sql = @"
                            SELECT
                                *
                            FROM
                            (SELECT
                                `target_id` AS `player_id`,
                                `target_name` AS `player_name`,
                                COUNT(`record_id`) AS `population_count`
                            FROM
                                `adkats_records_main`
                            WHERE
                                `command_type` = 88
                            AND
                                DATE_ADD(`record_time`, INTERVAL @duration_minutes MINUTE) > UTC_TIMESTAMP()
                            GROUP BY
                                `target_id`
                            ORDER BY
                                `population_count` DESC, `target_name` ASC) AS InnerResults
                            WHERE
                                `population_count` >= @population_minimum";
                        parameters = new
                        {
                            duration_minutes = (Int32)duration.TotalMinutes,
                            population_minimum = minPopulations
                        };
                    }
                    var rows = connection.Query(sql, parameters);
                    foreach (var row in rows)
                    {
                        APlayer aPlayer = FetchPlayer(false, false, false, null, (Int64)row.player_id, null, null, null, null);
                        if (aPlayer != null)
                        {
                            resultPlayers.Add(aPlayer);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching populating players", e));
            }
            Log.Debug(() => "GetPopulatingPlayers finished!", 6);
            return resultPlayers;
        }

        private ABan FetchBanByID(Int64 ban_id)
        {
            Log.Debug(() => "FetchBanByID starting!", 6);
            ABan aBan = null;
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return null;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var row = connection.QueryFirstOrDefault(@"
                        SELECT
                            `ban_id`,
                            `player_id`,
                            `latest_record_id`,
                            `ban_status`,
                            `ban_notes`,
                            `ban_startTime`,
                            `ban_endTime`,
                            `ban_enforceName`,
                            `ban_enforceGUID`,
                            `ban_enforceIP`,
                            `ban_sync`
                        FROM
                            `adkats_bans`
                        WHERE
                            `ban_id` = @ban_id",
                        new { ban_id });
                    if (row != null)
                    {
                        //Create the ban object
                        aBan = new ABan
                        {
                            ban_id = (Int64)row.ban_id,
                            ban_status = (String)row.ban_status,
                            ban_notes = (String)row.ban_notes,
                            ban_sync = (String)row.ban_sync,
                            ban_startTime = (DateTime)row.ban_startTime,
                            ban_endTime = (DateTime)row.ban_endTime,
                            ban_enforceName = ((String)row.ban_enforceName == "Y"),
                            ban_enforceGUID = ((String)row.ban_enforceGUID == "Y"),
                            ban_enforceIP = ((String)row.ban_enforceIP == "Y"),
                            ban_record = FetchRecordByID((Int64)row.latest_record_id, false)
                        };
                        if (aBan.ban_endTime.Subtract(UtcNow()).TotalSeconds < 0)
                        {
                            aBan.ban_status = "Expired";
                            UpdateBanStatus(aBan);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching ban.", e));
            }
            Log.Debug(() => "FetchBanByID finished!", 6);
            return aBan;
        }

        private void InfoOrRespond(ARecord debugRecord, String message)
        {
            if (debugRecord != null)
            {
                SendMessageToSource(debugRecord, message);
            }
            else
            {
                Log.Info(message);
            }
        }

        private Boolean RunAssist(APlayer aPlayer, ARecord realRecord, ARecord debugRecord, Boolean auto)
        {
            //Locals
            var powerPercentageThreshold = 18.0;
            var roundMinutes = Math.Round(_serverInfo.GetRoundElapsedTime().TotalMinutes, 1);

            //Team Info Check
            ATeam team1, team2;
            String rejectionMessage = "Error";
            if (!GetTeamByID(1, out team1))
            {
                if (_roundState == RoundState.Playing)
                {
                    InfoOrRespond(debugRecord, "Teams not loaded when they should be.");
                }
                return false;
            }
            if (!GetTeamByID(2, out team2))
            {
                if (_roundState == RoundState.Playing)
                {
                    InfoOrRespond(debugRecord, "Teams not loaded when they should be.");
                }
                return false;
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
            ATeam friendlyTeam, enemyTeam;
            if (aPlayer.fbpInfo.TeamID == team1.TeamID)
            {
                friendlyTeam = team1;
                enemyTeam = team2;
            }
            else if (aPlayer.fbpInfo.TeamID == team2.TeamID)
            {
                friendlyTeam = team2;
                enemyTeam = team1;
            }
            else
            {
                InfoOrRespond(debugRecord, Log.CViolet("Invalid teams when attempting to assist. Team ID was " + aPlayer.fbpInfo.TeamID + "."));
                return false;
            }
            ATeam mapUpTeam, mapDownTeam;
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

            String recordMessage = "Assist Weak Team [" + winningTeam.TeamTicketCount + ":" + losingTeam.TeamTicketCount + "][" + FormatTimeString(_serverInfo.GetRoundElapsedTime(), 3) + "]";
            if (realRecord != null)
            {
                realRecord.record_message = recordMessage;
            }
            if (!auto)
            {
                InfoOrRespond(debugRecord, recordMessage);
            }
            Boolean canAssist = true;
            Boolean ticketBypass = false;
            Double ticketBypassAmount = (_startingTicketCount > 0 ? (_startingTicketCount / 3.5) : 250);

            rejectionMessage = "";
            var oldFriendlyPower = friendlyTeam.GetTeamPower();
            var oldEnemyPower = enemyTeam.GetTeamPower();
            var newFriendlyPower = friendlyTeam.GetTeamPower(aPlayer, null);
            var newEnemyPower = enemyTeam.GetTeamPower(null, aPlayer);
            if (enemyTeam == mapUpTeam)
            {
                powerPercentageThreshold = 0;
            }
            var enemyMetro1 = _serverInfo.InfoObject.Map == "XP0_Metro" &&
                              _serverInfo.InfoObject.GameMode == "ConquestLarge0" &&
                              enemyTeam.TeamID == 1;
            var debugOldPower = oldEnemyPower;
            var debugNewPower = newEnemyPower;
            if (enemyMetro1)
            {
                if (roundMinutes < 20 &&
                    team1.TeamTicketCount + 500 > team2.TeamTicketCount)
                {
                    powerPercentageThreshold = 0;
                }

                // If this is metro, overstate the power of the lower team slightly
                // The upper team needs a slight stat boost over normal
                if (enemyTeam == mapUpTeam)
                {
                    // If the lower team has the map, overstate its power even more
                    if ((team2.TeamTicketCount + 500 < team1.TeamTicketCount || roundMinutes < 10) &&
                        _populationStatus == PopulationState.High)
                    {
                        oldEnemyPower *= 1.35;
                        newEnemyPower *= 1.35;
                    }
                    else
                    {
                        oldEnemyPower *= 1.22;
                        newEnemyPower *= 1.22;
                    }
                }
                else if (team1.TeamTicketCount + 500 > team2.TeamTicketCount)
                {
                    if (roundMinutes <= 10)
                    {
                        oldEnemyPower *= 1.12;
                        newEnemyPower *= 1.12;
                    }
                    else if (_populationStatus == PopulationState.High)
                    {
                        oldEnemyPower *= 1.08;
                        newEnemyPower *= 1.08;
                    }
                }
            }
            var newFriendlyCount = GetPlayerCount(true, true, true, friendlyTeam.TeamID) - 1;
            var newEnemyCount = GetPlayerCount(true, true, true, enemyTeam.TeamID) + 1;
            // Weed out bad assumptions
            // like a team being more powerful without someone on it
            newFriendlyPower = Math.Min(oldFriendlyPower, newFriendlyPower);
            // or less powerful with someone on it
            newEnemyPower = Math.Max(oldEnemyPower, newEnemyPower);
            // Calculate power differences
            var newPowerDiff = Math.Abs(newEnemyPower - newFriendlyPower);
            var oldPowerDiff = Math.Abs(oldEnemyPower - oldFriendlyPower);
            // Calculate percent differences
            var newPercDiff = Math.Abs(newFriendlyPower - newEnemyPower) / ((newFriendlyPower + newEnemyPower) / 2.0) * 100.0;
            var oldPercDiff = Math.Abs(oldFriendlyPower - oldEnemyPower) / ((oldFriendlyPower + oldEnemyPower) / 2.0) * 100.0;
            Boolean enemyWinning = (aPlayer.fbpInfo.TeamID == losingTeam.TeamID);
            Boolean enemyHasMoreMap = enemyTeam.GetTicketDifferenceRate() > friendlyTeam.GetTicketDifferenceRate();

            if (_serverInfo.GetRoundElapsedTime().TotalMinutes < _minimumAssistMinutes)
            {
                canAssist = false;
                var duration = TimeSpan.FromMinutes(_minimumAssistMinutes - _serverInfo.GetRoundElapsedTime().TotalMinutes);
                rejectionMessage += "assist off for " + FormatTimeString(duration, 2);
            }
            else if (enemyWinning && enemyHasMoreMap)
            {
                canAssist = false;
                rejectionMessage += "winning and strong";
            }
            else if (newEnemyCount - 4 >= newFriendlyCount)
            {
                // Hard cap the number of players a team can have over another
                canAssist = false;
                rejectionMessage += "too many players";
            }
            else
            {
                var enemyMorePowerful = newEnemyPower > newFriendlyPower;
                var powerDifferenceIncreased = newPowerDiff > oldPowerDiff;
                var powerDifferencePercOverThreshold = newPercDiff > powerPercentageThreshold;

                // Check team power
                if (_previousRoundDuration.TotalSeconds > 0 &&
                    _serverInfo.GetRoundElapsedTime().TotalMinutes >= 10 &&
                    Math.Abs(winningTeam.TeamTicketCount - losingTeam.TeamTicketCount) > ticketBypassAmount &&
                    enemyTeam == losingTeam)
                {
                    ticketBypass = true;
                }
                else
                {
                    if (// The new team would be absolutely more powerful than the current team
                        enemyMorePowerful &&
                        // The differenct in power between the teams would go up
                        powerDifferenceIncreased &&
                        // The difference in power would be over the threshold, or the enemy has more map
                        (powerDifferencePercOverThreshold || enemyHasMoreMap))
                    {
                        canAssist = false;
                        rejectionMessage += "would be too strong";
                    }

                    // Special rejection for metro 1
                    if (canAssist &&
                        enemyMetro1 &&
                        roundMinutes < 15 &&
                        (enemyMorePowerful || enemyHasMoreMap))
                    {
                        canAssist = false;
                        rejectionMessage += "1 would be too strong";
                    }
                }
            }
            if (!canAssist)
            {
                if (realRecord != null)
                {
                    rejectionMessage = Log.FBold(Log.CViolet(rejectionMessage));
                    rejectionMessage = realRecord.GetSourceName() + " (" + Math.Round(realRecord.target_player.GetPower(true)) + ") assist to " + enemyTeam.GetTeamIDKey() + " rejected (" + rejectionMessage + ").";
                    if (!auto)
                    {
                        rejectionMessage += " Queued #" + (_AssistAttemptQueue.Count() + 1) + " for auto-assist.";
                        lock (_AssistAttemptQueue)
                        {
                            _AssistAttemptQueue.Enqueue(realRecord);
                        }
                        AdminSayMessage(Log.CViolet(rejectionMessage));
                    }
                }
                else if (debugRecord != null)
                {
                    rejectionMessage = debugRecord.GetTargetNames() + " (" + Math.Round(debugRecord.target_player.GetPower(true)) + ") assist to " + enemyTeam.GetTeamIDKey() + " rejected, " + rejectionMessage;
                    if (!auto)
                    {
                        InfoOrRespond(debugRecord, rejectionMessage);
                    }
                }
            }
            else
            {
                if (realRecord != null)
                {
                    SendMessageToSource(realRecord, Log.CViolet("Queuing you to assist the weak team. Thank you."));
                    var powerDiffString = Math.Round(newPercDiff) + "<" + Math.Round(oldPercDiff);
                    if (newPercDiff > oldPercDiff && newPercDiff <= powerPercentageThreshold)
                    {
                        powerDiffString = "Lenient";
                    }
                    if (ticketBypass)
                    {
                        powerDiffString = "Bypass";
                    }
                    AdminSayMessage(Log.CViolet(realRecord.GetTargetNames() + " (" + Math.Round(realRecord.target_player.GetPower(true)) + ") assist to " + enemyTeam.GetTeamIDKey() + " accepted (" + powerDiffString + "), queueing."));
                    realRecord.command_action = GetCommandByKey("self_assist_unconfirmed");
                }
                else if (debugRecord != null)
                {
                    SendMessageToSource(debugRecord, Log.CViolet("Assist accepted."));
                }
            }
            return canAssist;
        }

        private Int32 FetchEstimatedEventRoundNumber()
        {
            var roundDate = GetEventRoundDateTime();
            if (DateTime.Now >= roundDate)
            {
                return 0;
            }
            var durationTillEvent = roundDate.Subtract(DateTime.Now);
            var estimate = _roundID + (int)Math.Ceiling((durationTillEvent.TotalMinutes + _serverInfo.GetRoundElapsedTime().TotalMinutes) / FetchAverageRoundMinutes(durationTillEvent.TotalHours < 72));
            if (estimate < 1)
            {
                estimate = 1;
            }
            if (estimate > 1000000)
            {
                estimate = 1000000;
            }
            return estimate;
        }

        private DateTime GetEventRoundDateTime()
        {
            return _EventDate.AddHours(_EventHour);
        }

        private Double FetchAverageRoundMinutes(Boolean active)
        {
            try
            {
                String sql;
                if (active)
                {
                    //Only include active rounds, this is best for durations when the event is near
                    sql = @"
                        select avg(round_duration) `AvgRoundDuration`
                          from (select *
                                  from (select round_id,
                                               min(roundstat_time) round_starttime,
                                               max(roundstat_time) round_endtime,
                                               timestampdiff(minute, min(roundstat_time), max(roundstat_time)) round_duration from tbl_extendedroundstats
                                         where server_id = @ServerID
                                           and timestampdiff(minute, `roundstat_time`, utc_timestamp()) < 15080
                                      group by round_id) round_times
                                 where round_duration < 100
                                   and round_duration > 5) round_durations";
                }
                else
                {
                    //Non-active round inclusion is good for estimating long-term
                    sql = @"
                        SELECT
                            TIMESTAMPDIFF(SECOND, MIN(`roundstart_time`), MAX(`roundstart_time`)) /
                            (REPLACE(COUNT(`round_id`), 0, 1.0)) / 60.0 as `AvgRoundDuration`
                        FROM
                        (SELECT
                            `round_id`,
                            `roundstat_time` AS `roundstart_time`
                        FROM
                            `tbl_extendedroundstats`
                        WHERE
                            `server_id` = @ServerID
                        AND
                            TIMESTAMPDIFF(MINUTE, `roundstat_time`, UTC_TIMESTAMP()) < 15080
                        GROUP BY
                            `round_id`
                        ORDER BY
                            `roundstat_id` DESC) AS `RoundStartTimes`";
                }

                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var result = connection.ExecuteScalar<Double?>(sql, new { ServerID = _serverInfo.ServerID });
                    return result ?? 0;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching average round duration.", e));
            }
            return 0;
        }

        private DateTime FetchFutureRoundDate(Int32 TargetRoundID)
        {
            if (_roundID < 1 || TargetRoundID <= 1 || _databaseConnectionCriticalState)
            {
                return DateTime.MinValue;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    //The most ham-handed SQL I've ever written
                    var row = connection.QueryFirstOrDefault(@"
                        SELECT
                            *,
                            (@TargetRound - `CurrentRoundId`) AS `RemainingRounds`,
                            `AvgRoundDuration` * (@TargetRound - `CurrentRoundId`) AS `RemainingMinutes`,
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL `AvgRoundDuration` * (@TargetRound - `CurrentRoundId`) MINUTE) AS `TargetTime`
                        FROM
                            (SELECT
                            (SELECT MAX(`round_id`) FROM `tbl_extendedroundstats` WHERE `server_id` = @ServerID) AS `CurrentRoundId`,
                            (SELECT
                                TIMESTAMPDIFF(SECOND, MIN(`roundstart_time`), MAX(`roundstart_time`)) /
                                (REPLACE(COUNT(`round_id`), 0, 1.0)) / 60.0
                            FROM
                            (SELECT
                                `round_id`,
                                `roundstat_time` AS `roundstart_time`
                            FROM
                                `tbl_extendedroundstats`
                            WHERE
                                `server_id` = @ServerID
                            AND
                                TIMESTAMPDIFF(MINUTE, `roundstat_time`, UTC_TIMESTAMP()) < 10080
                            GROUP BY
                                `round_id`
                            ORDER BY
                                `roundstat_id` DESC) AS `RoundStartTimes`) AS `AvgRoundDuration`) AS `RoundInfo`",
                        new { ServerID = _serverInfo.ServerID, TargetRound = TargetRoundID });
                    if (row != null)
                    {
                        return (DateTime)row.TargetTime;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching future round time.", e));
            }
            return DateTime.MinValue;
        }

        private List<ABan> FetchPlayerBans(APlayer player)
        {
            Log.Debug(() => "FetchPlayerBans starting!", 6);
            List<ABan> aBanList = new List<ABan>();
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return aBanList;
            }
            if (player == null)
            {
                Log.Error("Player null when fetching player bans. Contact ColColonCleaner.");
                return aBanList;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    //Build the query
                    String query = @"
                        SELECT
                            `adkats_bans`.`ban_id`,
                            `adkats_bans`.`player_id`,
                            `adkats_bans`.`latest_record_id`,
                            `adkats_bans`.`ban_status`,
                            `adkats_bans`.`ban_notes`,
                            `adkats_bans`.`ban_startTime`,
                            `adkats_bans`.`ban_endTime`,
                            `adkats_bans`.`ban_enforceName`,
                            `adkats_bans`.`ban_enforceGUID`,
                            `adkats_bans`.`ban_enforceIP`,
                            `adkats_bans`.`ban_sync`
                        FROM
                            `adkats_bans`
                        INNER JOIN
                            `tbl_playerdata`
                        ON
                            `tbl_playerdata`.`PlayerID` = `adkats_bans`.`player_id`
                        WHERE
                            `adkats_bans`.`ban_status` = 'Active' ";
                    if (_serverInfo.GameID > 0 && player.game_id < 0)
                    {
                        query += " AND `tbl_playerdata`.`GameID` = " + _serverInfo.GameID;
                    }
                    else if (player.game_id > 0)
                    {
                        query += " AND `tbl_playerdata`.`GameID` = " + player.game_id;
                    }
                    else
                    {
                        Log.Error("Unusable game IDs when fetching player bans for " + player.player_name + ".");
                        return aBanList;
                    }
                    query += " AND (";
                    Boolean started = false;
                    if (!String.IsNullOrEmpty(player.player_name))
                    {
                        started = true;
                        query += "(`tbl_playerdata`.`SoldierName` = '" + player.player_name + @"' AND `adkats_bans`.`ban_enforceName` = 'Y')";
                    }
                    if (!String.IsNullOrEmpty(player.player_guid))
                    {
                        if (started)
                        {
                            query += " OR ";
                        }
                        started = true;
                        query += "(`tbl_playerdata`.`EAGUID` = '" + player.player_guid + "' AND `adkats_bans`.`ban_enforceGUID` = 'Y')";
                    }
                    if (!String.IsNullOrEmpty(player.player_ip) && player.player_ip != "127.0.0.1")
                    {
                        if (started)
                        {
                            query += " OR ";
                        }
                        started = true;
                        query += "(`tbl_playerdata`.`IP_Address` = '" + player.player_ip + "' AND `adkats_bans`.`ban_enforceIP` = 'Y')";
                    }
                    if (!started)
                    {
                        Log.HandleException(new AException("No data to fetch ban with. This should never happen."));
                        return aBanList;
                    }
                    query += ")";

                    if (_debugDisplayPlayerFetches)
                    {
                        Log.Debug(() => "FetchPlayerBans SQL: " + query, 3);
                    }
                    var rows = connection.Query(query);
                    foreach (var row in rows)
                    {
                        //Create the ban element
                        ABan aBan = new ABan
                        {
                            ban_id = (Int64)row.ban_id,
                            ban_status = (String)row.ban_status,
                            ban_notes = (String)row.ban_notes,
                            ban_sync = (String)row.ban_sync,
                            ban_startTime = (DateTime)row.ban_startTime,
                            ban_endTime = (DateTime)row.ban_endTime,
                            ban_enforceName = ((String)row.ban_enforceName == "Y"),
                            ban_enforceGUID = ((String)row.ban_enforceGUID == "Y"),
                            ban_enforceIP = ((String)row.ban_enforceIP == "Y"),
                            ban_record = FetchRecordByID((Int64)row.latest_record_id, false)
                        };
                        if (aBan.ban_endTime.Subtract(UtcNow()).TotalSeconds < 0)
                        {
                            aBan.ban_status = "Expired";
                            UpdateBanStatus(aBan);
                        }
                        else if (!String.IsNullOrEmpty(player.player_name_previous) && aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP)
                        {
                            ARecord record = new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_unban"),
                                command_numeric = 0,
                                target_name = player.player_name,
                                target_player = player,
                                source_name = "BanEnforcer",
                                record_message = "Name-Banned player has changed their name. (" + player.player_name_previous + " -> " + player.player_name + ")",
                                record_time = UtcNow()
                            };
                            QueueRecordForProcessing(record);
                        }
                        else if (_serverInfo.ServerGroup == FetchServerGroup(aBan.ban_record.server_id) && aBan.ban_startTime < UtcNow())
                        {
                            aBanList.Add(aBan);
                        }
                    }
                    if (aBanList.Count > 1)
                    {
                        Log.Warn("Multiple bans matched player " + player.player_id + ". Linked accounts detected.");
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching player ban.", e));
            }
            Log.Debug(() => "FetchPlayerBans finished!", 6);
            return aBanList;
        }


        private List<ABan> FetchMatchingBans(String playerSubstring, Int64 searchLimit)
        {
            Log.Debug(() => "FetchMatchingBans starting!", 6);
            List<ABan> aBanList = new List<ABan>();
            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return null;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var banIds = connection.Query<Int64>(@"
                        SELECT
                            `ban_id`
                        FROM
                            `tbl_playerdata`
                        INNER JOIN
                            `adkats_bans`
                        ON
                            `PlayerID` = `player_id`
                        INNER JOIN
                            `adkats_records_main`
                        ON
                            `latest_record_id` = `record_id`
                        WHERE
                            `ban_status` = 'Active'
                        AND
                        (
                            `SoldierName` LIKE @PlayerSubstring
                            OR
                            `target_name` LIKE @PlayerSubstring
                        )
                        ORDER BY
                            `record_time` DESC
                        LIMIT
                            @searchLimit",
                        new { PlayerSubstring = "%" + playerSubstring + "%", searchLimit });
                    foreach (var banId in banIds)
                    {
                        aBanList.Add(FetchBanByID(banId));
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching player ban.", e));
            }
            return aBanList;
        }


        private void RepopulateProconBanList()
        {
            Log.Debug(() => "repopulateProconBanList starting!", 6);
            Log.Info("Downloading bans from database, please wait. This might take several minutes depending on your ban count!");

            //Make sure database connection active
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            Double totalBans = 0;
            Double bansDownloaded = 0;
            Double bansRepopulated = 0;
            Boolean earlyExit = false;
            DateTime startTime = UtcNow();

            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    totalBans = connection.ExecuteScalar<Int64>(@"
                        SELECT
                            COUNT(*) AS `ban_count`
                        FROM
                            `adkats_bans`
                        WHERE
                            `ban_status` = 'Active'");
                    if (totalBans < 1)
                    {
                        return;
                    }
                    var banRows = connection.Query(@"
                        SELECT
                            `ban_id`,
                            `player_id`,
                            `latest_record_id`,
                            `ban_status`,
                            `ban_notes`,
                            `ban_sync`,
                            `ban_startTime`,
                            `ban_endTime`,
                            `ban_enforceName`,
                            `ban_enforceGUID`,
                            `ban_enforceIP`
                        FROM
                            `adkats_bans`
                        WHERE
                            `ban_status` = 'Active'");

                    List<ABan> importedBans = new List<ABan>();
                    foreach (var row in banRows)
                    {
                        //Break from the loop if the plugin is disabled or the setting is reverted.
                        if (!_pluginEnabled || _UseBanEnforcer)
                        {
                            Log.Warn("You exited the ban download process early, the process was not completed.");
                            earlyExit = true;
                            break;
                        }
                        //Create the ban element
                        ABan aBan = new ABan
                        {
                            ban_id = (Int64)row.ban_id,
                            player_id = (Int64)(Int32)row.player_id,
                            ban_status = (String)row.ban_status,
                            ban_notes = (String)row.ban_notes,
                            ban_sync = (String)row.ban_sync,
                            ban_startTime = (DateTime)row.ban_startTime,
                            ban_endTime = (DateTime)row.ban_endTime,
                            ban_record = FetchRecordByID((Int64)row.latest_record_id, false),
                            ban_enforceName = ((String)row.ban_enforceName == "Y"),
                            ban_enforceGUID = ((String)row.ban_enforceGUID == "Y"),
                            ban_enforceIP = ((String)row.ban_enforceIP == "Y")
                        };

                        if (aBan.ban_status != "Active")
                        {
                            continue;
                        }

                        if (aBan.ban_record == null)
                        {
                            aBan.ban_record = new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                isDebug = false,
                                target_player = FetchPlayer(false, true, false, null, aBan.player_id, null, null, null, null),
                                source_name = "AdKats",
                                record_message = "Ban Reason Expunged",
                                record_time = UtcNow()
                            };
                            aBan.ban_record.target_name = aBan.ban_record.target_player.player_name;
                        }
                        if (aBan.ban_record.target_player == null)
                        {
                            aBan.ban_record.target_player = FetchPlayer(false, true, false, null, aBan.player_id, null, null, null, null);
                        }
                        if (aBan.ban_record.target_player != null)
                        {
                            importedBans.Add(aBan);
                            if (++bansDownloaded % 15 == 0)
                            {
                                Log.Write(Math.Round(100 * bansDownloaded / totalBans, 2) + "% of bans downloaded. AVG " + Math.Round(bansDownloaded / ((UtcNow() - startTime).TotalSeconds), 2) + " downloads/sec.");
                            }
                        }
                    }
                    if (importedBans.Count > 0)
                    {
                        Log.Info(importedBans.Count + " bans downloaded, beginning repopulation to ban list.");
                    }
                    startTime = UtcNow();
                    foreach (ABan aBan in importedBans)
                    {
                        //Get the record information
                        long totalBanSeconds = (long)aBan.ban_endTime.Subtract(UtcNow()).TotalSeconds;
                        if (totalBanSeconds > 0)
                        {
                            Log.Debug(() => "Re-ProconBanning: " + aBan.ban_record.GetTargetNames() + " for " + totalBanSeconds + "sec for " + aBan.ban_record.record_message, 4);

                            //Push the id ban
                            if (aBan.ban_enforceName)
                            {
                                Threading.Wait(75);
                                //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                if (totalBanSeconds > 0 && totalBanSeconds < 31536000)
                                {
                                    ExecuteCommand("procon.protected.send", "banList.add", "id", aBan.ban_record.target_player.player_name, "seconds", totalBanSeconds.ToString(), aBan.ban_record.record_message);
                                }
                                else
                                {
                                    ExecuteCommand("procon.protected.send", "banList.add", "id", aBan.ban_record.target_player.player_name, "perm", aBan.ban_record.record_message);
                                }
                            }

                            //Push the guid ban
                            if (aBan.ban_enforceGUID)
                            {
                                Threading.Wait(75);
                                //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                if (totalBanSeconds > 0 && totalBanSeconds < 31536000)
                                {
                                    ExecuteCommand("procon.protected.send", "banList.add", "guid", aBan.ban_record.target_player.player_guid, "seconds", totalBanSeconds.ToString(), aBan.ban_record.record_message);
                                }
                                else
                                {
                                    ExecuteCommand("procon.protected.send", "banList.add", "guid", aBan.ban_record.target_player.player_guid, "perm", aBan.ban_record.record_message);
                                }
                            }

                            //Push the IP ban
                            if (aBan.ban_enforceIP)
                            {
                                Threading.Wait(75);
                                //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                if (totalBanSeconds > 0 && totalBanSeconds < 31536000)
                                {
                                    ExecuteCommand("procon.protected.send", "banList.add", "ip", aBan.ban_record.target_player.player_ip, "seconds", totalBanSeconds.ToString(), aBan.ban_record.record_message);
                                }
                                else
                                {
                                    ExecuteCommand("procon.protected.send", "banList.add", "ip", aBan.ban_record.target_player.player_ip, "perm", aBan.ban_record.record_message);
                                }
                            }
                        }

                        if (++bansRepopulated % 15 == 0)
                        {
                            Log.Write(Math.Round(100 * bansRepopulated / totalBans, 2) + "% of bans repopulated. AVG " + Math.Round(bansRepopulated / ((UtcNow() - startTime).TotalSeconds), 2) + " downloads/sec.");
                        }
                    }
                    ExecuteCommand("procon.protected.send", "banList.save");
                    ExecuteCommand("procon.protected.send", "banList.list");
                    if (!earlyExit)
                    {
                        Log.Success("All AdKats Enforced bans repopulated to procon's ban list.");
                    }

                    //Update the last db ban fetch time
                    _lastDbBanFetch = UtcNow();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while repopulating procon banlist.", e));
            }
        }

        private Int32 FetchPoints(APlayer player, Boolean combineOverride, Boolean update)
        {
            Int32 returnVal = player.player_infractionPoints;
            //Make sure database connection active
            if (_databaseConnectionCriticalState || (!update && player.player_infractionPoints != Int32.MinValue))
            {
                return (returnVal > 0) ? (returnVal) : (0);
            }
            Log.Debug(() => "FetchPoints starting!", 6);
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Int32? points;
                    if (_CombineServerPunishments || combineOverride)
                    {
                        points = connection.ExecuteScalar<Int32?>(@"SELECT `total_points` FROM `" + _mySqlSchemaName + @"`.`adkats_infractions_global` WHERE `player_id` = @player_id",
                            new { player_id = player.player_id });
                    }
                    else
                    {
                        points = connection.ExecuteScalar<Int32?>(@"SELECT `total_points` FROM `" + _mySqlSchemaName + @"`.`adkats_infractions_server` WHERE `player_id` = @player_id and `server_id` = @server_id",
                            new { player_id = player.player_id, server_id = _serverInfo.ServerID });
                    }
                    returnVal = points ?? 0;
                    player.player_infractionPoints = returnVal;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting infraction points for player.", e));
            }
            Log.Debug(() => "FetchPoints finished!", 6);
            return (returnVal > 0) ? (returnVal) : (0);
        }

        private void FetchCommands()
        {
            Log.Debug(() => "fetchCommands starting!", 6);
            Boolean displayUpdate = false;
            if (_databaseConnectionCriticalState)
            {
                return;
            }
            try
            {
                lock (_CommandIDDictionary)
                {
                    using (MySqlConnection connection = GetDatabaseConnection())
                    {
                        {
                            var rows = connection.Query(@"
                            SELECT
                                `command_id`,
                                `command_active`,
                                `command_key`,
                                `command_logging`,
                                `command_name`,
                                `command_text`,
                                `command_playerInteraction`,
                                `command_access`
                            FROM
                                `adkats_commands`");
                            HashSet<long> validIDs = new HashSet<Int64>();
                            _CommandKeyDictionary.Clear();
                            _CommandNameDictionary.Clear();
                            _CommandTextDictionary.Clear();
                            foreach (var row in rows)
                            {
                                if (!_pluginEnabled)
                                {
                                    return;
                                }
                                //ID is the immutable element
                                Int32 commandID = (Int32)row.command_id;
                                ACommand.CommandActive commandActive = (ACommand.CommandActive)Enum.Parse(typeof(ACommand.CommandActive), (String)row.command_active);
                                String commandKey = (String)row.command_key;
                                ACommand.CommandLogging commandLogging = (ACommand.CommandLogging)Enum.Parse(typeof(ACommand.CommandLogging), (String)row.command_logging);
                                String commandName = (String)row.command_name;
                                String commandText = (String)row.command_text;
                                ACommand.CommandAccess commandAccess = (ACommand.CommandAccess)Enum.Parse(typeof(ACommand.CommandAccess), (String)row.command_access);
                                Boolean commandPlayerInteraction = (Boolean)row.command_playerInteraction;

                                validIDs.Add(commandID);
                                ACommand currentCommand;
                                if (_CommandIDDictionary.TryGetValue(commandID, out currentCommand))
                                {
                                    if (!currentCommand.command_active.Equals(commandActive))
                                    {
                                        Log.Info(currentCommand.command_key + " active state being changed from " + currentCommand.command_active + " to " + commandActive);
                                        currentCommand.command_active = commandActive;
                                        displayUpdate = true;
                                    }
                                    if (currentCommand.command_key != commandKey)
                                    {
                                        Log.Info(currentCommand.command_key + " command key being changed from " + currentCommand.command_key + " to " + commandKey);
                                        currentCommand.command_key = commandKey;
                                        displayUpdate = true;
                                    }
                                    if (!currentCommand.command_logging.Equals((commandLogging)))
                                    {
                                        Log.Info(currentCommand.command_key + " logging state being changed from " + currentCommand.command_logging + " to " + commandLogging);
                                        currentCommand.command_logging = commandLogging;
                                        displayUpdate = true;
                                    }
                                    if (currentCommand.command_name != commandName)
                                    {
                                        Log.Info(currentCommand.command_key + " command name being changed from " + currentCommand.command_name + " to " + commandName);
                                        currentCommand.command_name = commandName;
                                        displayUpdate = true;
                                    }
                                    if (currentCommand.command_text != commandText)
                                    {
                                        Log.Info(currentCommand.command_key + " command text being changed from " + currentCommand.command_text + " to " + commandText);
                                        currentCommand.command_text = commandText;
                                        displayUpdate = true;
                                    }
                                    if (currentCommand.command_playerInteraction != commandPlayerInteraction)
                                    {
                                        Log.Info(currentCommand.command_key + " player interaction state being changed from " + currentCommand.command_playerInteraction + " to " + commandPlayerInteraction);
                                        currentCommand.command_playerInteraction = commandPlayerInteraction;
                                        displayUpdate = true;
                                    }
                                    if (!currentCommand.command_access.Equals(commandAccess))
                                    {
                                        Log.Info(currentCommand.command_key + " command access being changed from " + currentCommand.command_access + " to " + commandAccess);
                                        currentCommand.command_access = commandAccess;
                                        displayUpdate = true;
                                    }
                                }
                                else
                                {
                                    currentCommand = new ACommand
                                    {
                                        command_id = commandID,
                                        command_active = commandActive,
                                        command_key = commandKey,
                                        command_logging = commandLogging,
                                        command_name = commandName,
                                        command_text = commandText,
                                        command_playerInteraction = commandPlayerInteraction,
                                        command_access = commandAccess
                                    };

                                    _CommandIDDictionary.Add(currentCommand.command_id, currentCommand);
                                    displayUpdate = true;
                                }
                                _CommandKeyDictionary.Add(currentCommand.command_key, currentCommand);
                                _CommandNameDictionary.Add(currentCommand.command_name, currentCommand);
                                _CommandTextDictionary.Add(currentCommand.command_text, currentCommand);
                                if (!_commandUsageTimes.ContainsKey(currentCommand.command_key))
                                {
                                    _commandUsageTimes[currentCommand.command_key] = UtcNow();
                                }
                                //Handle mandatory defaults
                                Boolean changed = false;
                                switch (currentCommand.command_key)
                                {
                                    case "command_confirm":
                                        if (currentCommand.command_active != ACommand.CommandActive.Active)
                                        {
                                            Log.Warn("Confirm command must be active. Resetting.");
                                            currentCommand.command_active = ACommand.CommandActive.Active;
                                            changed = true;
                                        }
                                        if (currentCommand.command_text != "yes")
                                        {
                                            Log.Warn("Confirm command text must be 'yes'. Resetting.");
                                            currentCommand.command_text = "yes";
                                            changed = true;
                                        }
                                        if (currentCommand.command_access != ACommand.CommandAccess.Any)
                                        {
                                            Log.Warn("Confirm command access must be 'Any'. Resetting.");
                                            currentCommand.command_access = ACommand.CommandAccess.Any;
                                            changed = true;
                                        }
                                        break;
                                    case "command_cancel":
                                        if (currentCommand.command_active != ACommand.CommandActive.Active)
                                        {
                                            Log.Warn("Cancel command must be active. Resetting.");
                                            currentCommand.command_active = ACommand.CommandActive.Active;
                                            changed = true;
                                        }
                                        if (currentCommand.command_text != "no")
                                        {
                                            Log.Warn("Cancel command text must be 'no'. Resetting.");
                                            currentCommand.command_text = "no";
                                            changed = true;
                                        }
                                        if (currentCommand.command_access != ACommand.CommandAccess.Any)
                                        {
                                            Log.Warn("Confirm command access must be 'Any'. Resetting.");
                                            currentCommand.command_access = ACommand.CommandAccess.Any;
                                            changed = true;
                                        }
                                        break;
                                    case "player_say":
                                        if (currentCommand.command_access != ACommand.CommandAccess.AnyHidden)
                                        {
                                            Log.Info(currentCommand.command_name + " access must be 'AnyHidden'. Resetting.");
                                            currentCommand.command_access = ACommand.CommandAccess.AnyHidden;
                                            changed = true;
                                        }
                                        break;
                                    case "player_yell":
                                        if (currentCommand.command_access != ACommand.CommandAccess.AnyHidden)
                                        {
                                            Log.Info(currentCommand.command_name + " access must be 'AnyHidden'. Resetting.");
                                            currentCommand.command_access = ACommand.CommandAccess.AnyHidden;
                                            changed = true;
                                        }
                                        break;
                                    case "player_tell":
                                        if (currentCommand.command_access != ACommand.CommandAccess.AnyHidden)
                                        {
                                            Log.Info(currentCommand.command_name + " access must be 'AnyHidden'. Resetting.");
                                            currentCommand.command_access = ACommand.CommandAccess.AnyHidden;
                                            changed = true;
                                        }
                                        break;
                                    case "player_find":
                                        if (currentCommand.command_access != ACommand.CommandAccess.AnyHidden)
                                        {
                                            Log.Info(currentCommand.command_name + " access must be 'AnyHidden'. Resetting.");
                                            currentCommand.command_access = ACommand.CommandAccess.AnyHidden;
                                            changed = true;
                                        }
                                        break;
                                }
                                if (changed)
                                {
                                    QueueCommandForUpload(currentCommand);
                                    displayUpdate = true;
                                }
                            }
                            if (_CommandIDDictionary.Count > 0)
                            {
                                foreach (ACommand remCommand in _CommandIDDictionary.Values.Where(aRole => !validIDs.Contains(aRole.command_id)).ToList())
                                {
                                    Log.Info("Removing command " + remCommand.command_key);
                                    _CommandIDDictionary.Remove(remCommand.command_id);
                                }
                                Boolean newCommands = false;
                                if (!_CommandIDDictionary.ContainsKey(1))
                                {
                                    SendNonQuery("Adding command command_confirm", "INSERT INTO `adkats_commands` VALUES(1, 'Active', 'command_confirm', 'Unable', 'Confirm Command', 'yes', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(2))
                                {
                                    SendNonQuery("Adding command command_cancel", "INSERT INTO `adkats_commands` VALUES(2, 'Active', 'command_cancel', 'Unable', 'Cancel Command', 'no', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(3))
                                {
                                    SendNonQuery("Adding command player_kill", "INSERT INTO `adkats_commands` VALUES(3, 'Active', 'player_kill', 'Log', 'Kill Player', 'kill', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(4))
                                {
                                    SendNonQuery("Adding command player_kill_lowpop", "INSERT INTO `adkats_commands` VALUES(4, 'Invisible', 'player_kill_lowpop', 'Log', 'Kill Player (Low Population)', 'lowpopkill', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(5))
                                {
                                    SendNonQuery("Adding command player_kill_repeat", "INSERT INTO `adkats_commands` VALUES(5, 'Invisible', 'player_kill_repeat', 'Log', 'Kill Player (Repeat Kill)', 'repeatkill', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(6))
                                {
                                    SendNonQuery("Adding command player_kick", "INSERT INTO `adkats_commands` VALUES(6, 'Active', 'player_kick', 'Log', 'Kick Player', 'kick', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(7))
                                {
                                    SendNonQuery("Adding command player_ban_temp", "INSERT INTO `adkats_commands` VALUES(7, 'Active', 'player_ban_temp', 'Log', 'Temp-Ban Player', 'tban', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(8))
                                {
                                    SendNonQuery("Adding command player_ban_perm", "INSERT INTO `adkats_commands` VALUES(8, 'Active', 'player_ban_perm', 'Log', 'Permaban Player', 'ban', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(9))
                                {
                                    SendNonQuery("Adding command player_punish", "INSERT INTO `adkats_commands` VALUES(9, 'Active', 'player_punish', 'Mandatory', 'Punish Player', 'punish', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(10))
                                {
                                    SendNonQuery("Adding command player_forgive", "INSERT INTO `adkats_commands` VALUES(10, 'Active', 'player_forgive', 'Mandatory', 'Forgive Player', 'forgive', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(11))
                                {
                                    SendNonQuery("Adding command player_mute", "INSERT INTO `adkats_commands` VALUES(11, 'Active', 'player_mute', 'Log', 'Mute Player', 'mute', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(12))
                                {
                                    SendNonQuery("Adding command player_join", "INSERT INTO `adkats_commands` VALUES(12, 'Active', 'player_join', 'Log', 'Join Player', 'join', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(14))
                                {
                                    SendNonQuery("Adding command player_move", "INSERT INTO `adkats_commands` VALUES(14, 'Active', 'player_move', 'Log', 'On-Death Move Player', 'move', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(15))
                                {
                                    SendNonQuery("Adding command player_fmove", "INSERT INTO `adkats_commands` VALUES(15, 'Active', 'player_fmove', 'Log', 'Force Move Player', 'fmove', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(16))
                                {
                                    SendNonQuery("Adding command self_teamswap", "INSERT INTO `adkats_commands` VALUES(16, 'Active', 'self_teamswap', 'Log', 'Teamswap Self', 'moveme', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(17))
                                {
                                    SendNonQuery("Adding command self_kill", "INSERT INTO `adkats_commands` VALUES(17, 'Active', 'self_kill', 'Log', 'Kill Self', 'killme', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(18))
                                {
                                    SendNonQuery("Adding command player_report", "INSERT INTO `adkats_commands` VALUES(18, 'Active', 'player_report', 'Log', 'Report Player', 'report', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(19))
                                {
                                    SendNonQuery("Adding command player_report_confirm", "INSERT INTO `adkats_commands` VALUES(19, 'Invisible', 'player_report_confirm', 'Log', 'Report Player (Confirmed)', 'confirmreport', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(20))
                                {
                                    SendNonQuery("Adding command player_calladmin", "INSERT INTO `adkats_commands` VALUES(20, 'Active', 'player_calladmin', 'Log', 'Call Admin on Player', 'admin', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(21))
                                {
                                    SendNonQuery("Adding command admin_say", "INSERT INTO `adkats_commands` VALUES(21, 'Active', 'admin_say', 'Log', 'Admin Say', 'say', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(22))
                                {
                                    SendNonQuery("Adding command player_say", "INSERT INTO `adkats_commands` VALUES(22, 'Active', 'player_say', 'Log', 'Player Say', 'psay', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(23))
                                {
                                    SendNonQuery("Adding command admin_yell", "INSERT INTO `adkats_commands` VALUES(23, 'Active', 'admin_yell', 'Log', 'Admin Yell', 'yell', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(24))
                                {
                                    SendNonQuery("Adding command player_yell", "INSERT INTO `adkats_commands` VALUES(24, 'Active', 'player_yell', 'Log', 'Player Yell', 'pyell', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(25))
                                {
                                    SendNonQuery("Adding command admin_tell", "INSERT INTO `adkats_commands` VALUES(25, 'Active', 'admin_tell', 'Log', 'Admin Tell', 'tell', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(26))
                                {
                                    SendNonQuery("Adding command player_tell", "INSERT INTO `adkats_commands` VALUES(26, 'Active', 'player_tell', 'Log', 'Player Tell', 'ptell', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(27))
                                {
                                    SendNonQuery("Adding command self_whatis", "INSERT INTO `adkats_commands` VALUES(27, 'Active', 'self_whatis', 'Unable', 'What Is', 'whatis', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(28))
                                {
                                    SendNonQuery("Adding command self_voip", "INSERT INTO `adkats_commands` VALUES(28, 'Active', 'self_voip', 'Unable', 'VOIP', 'voip', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(29))
                                {
                                    SendNonQuery("Adding command self_rules", "INSERT INTO `adkats_commands` VALUES(29, 'Active', 'self_rules', 'Log', 'Request Rules', 'rules', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(30))
                                {
                                    SendNonQuery("Adding command round_restart", "INSERT INTO `adkats_commands` VALUES(30, 'Active', 'round_restart', 'Log', 'Restart Current Round', 'restart', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(31))
                                {
                                    SendNonQuery("Adding command round_next", "INSERT INTO `adkats_commands` VALUES(31, 'Active', 'round_next', 'Log', 'Run Next Round', 'nextlevel', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(32))
                                {
                                    SendNonQuery("Adding command round_end", "INSERT INTO `adkats_commands` VALUES(32, 'Active', 'round_end', 'Log', 'End Current Round', 'endround', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(33))
                                {
                                    SendNonQuery("Adding command server_nuke", "INSERT INTO `adkats_commands` VALUES(33, 'Active', 'server_nuke', 'Log', 'Server Nuke', 'nuke', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(34))
                                {
                                    SendNonQuery("Adding command server_kickall", "INSERT INTO `adkats_commands` VALUES(34, 'Active', 'server_kickall', 'Log', 'Kick All Guests', 'kickall', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(35))
                                {
                                    SendNonQuery("Adding command adkats_exception", "INSERT INTO `adkats_commands` VALUES(35, 'Invisible', 'adkats_exception', 'Mandatory', 'Logged Exception', 'logexception', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(36))
                                {
                                    SendNonQuery("Adding command banenforcer_enforce", "INSERT INTO `adkats_commands` VALUES(36, 'Invisible', 'banenforcer_enforce', 'Mandatory', 'Enforce Active Ban', 'enforceban', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(37))
                                {
                                    SendNonQuery("Adding command player_unban", "INSERT INTO `adkats_commands` VALUES(37, 'Active', 'player_unban', 'Log', 'Unban Player', 'unban', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(38))
                                {
                                    SendNonQuery("Adding command self_admins", "INSERT INTO `adkats_commands` VALUES(38, 'Active', 'self_admins', 'Log', 'Request Online Admins', 'admins', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(39))
                                {
                                    SendNonQuery("Adding command self_lead", "INSERT INTO `adkats_commands` VALUES(39, 'Active', 'self_lead', 'Log', 'Lead Current Squad', 'lead', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(40))
                                {
                                    SendNonQuery("Adding command admin_accept", "INSERT INTO `adkats_commands` VALUES(40, 'Active', 'admin_accept', 'Log', 'Accept player report', 'accept', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(41))
                                {
                                    SendNonQuery("Adding command admin_deny", "INSERT INTO `adkats_commands` VALUES(41, 'Active', 'admin_deny', 'Log', 'Deny player report', 'deny', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(42))
                                {
                                    SendNonQuery("Adding command player_report_deny", "INSERT INTO `adkats_commands` VALUES(42, 'Invisible', 'player_report_deny', 'Log', 'Report Player (Denied)', 'denyreport', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(43))
                                {
                                    SendNonQuery("Adding command server_swapnuke", "INSERT INTO `adkats_commands` VALUES(43, 'Active', 'server_swapnuke', 'Log', 'SwapNuke Server', 'swapnuke', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(44))
                                {
                                    SendNonQuery("Adding command player_blacklistdisperse", "INSERT INTO `adkats_commands` VALUES(44, 'Active', 'player_blacklistdisperse', 'Log', 'Autobalance Disperse Player', 'disperse', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(45))
                                {
                                    SendNonQuery("Adding command player_whitelistbalance", "INSERT INTO `adkats_commands` VALUES(45, 'Active', 'player_whitelistbalance', 'Log', 'Autobalance Whitelist Player', 'mbwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(46))
                                {
                                    SendNonQuery("Adding command player_slotreserved", "INSERT INTO `adkats_commands` VALUES(46, 'Active', 'player_slotreserved', 'Log', 'Reserved Slot Player', 'reserved', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(47))
                                {
                                    SendNonQuery("Adding command player_slotspectator", "INSERT INTO `adkats_commands` VALUES(47, 'Active', 'player_slotspectator', 'Log', 'Spectator Slot Player', 'spectator', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(48))
                                {
                                    SendNonQuery("Adding command player_changename", "INSERT INTO `adkats_commands` VALUES(48, 'Invisible', 'player_changename', 'Mandatory', 'Player Changed Name', 'changename', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(49))
                                {
                                    SendNonQuery("Adding command player_changeip", "INSERT INTO `adkats_commands` VALUES(49, 'Invisible', 'player_changeip', 'Mandatory', 'Player Changed IP', 'changeip', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(50))
                                {
                                    SendNonQuery("Adding command player_ban_perm_future", "INSERT INTO `adkats_commands` VALUES(50, 'Active', 'player_ban_perm_future', 'Log', 'Future Permaban Player', 'fban', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(51))
                                {
                                    SendNonQuery("Adding command self_assist", "INSERT INTO `adkats_commands` VALUES(51, 'Active', 'self_assist', 'Log', 'Assist Losing Team', 'assist', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                SendNonQuery("Updating command 51 player interaction", "UPDATE `adkats_commands` SET `command_playerInteraction`=0 WHERE `command_id`=51", false);
                                if (!_CommandIDDictionary.ContainsKey(52))
                                {
                                    SendNonQuery("Adding command self_uptime", "INSERT INTO `adkats_commands` VALUES(52, 'Active', 'self_uptime', 'Log', 'Request Uptimes', 'uptime', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(53))
                                {
                                    SendNonQuery("Adding command self_contest", "INSERT INTO `adkats_commands` VALUES(53, 'Active', 'self_contest', 'Log', 'Contest Report', 'contest', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(54))
                                {
                                    SendNonQuery("Adding command player_kill_force", "INSERT INTO `adkats_commands` VALUES(54, 'Active', 'player_kill_force', 'Log', 'Kill Player (Force)', 'fkill', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(55))
                                {
                                    SendNonQuery("Adding command player_info", "INSERT INTO `adkats_commands` VALUES(55, 'Active', 'player_info', 'Log', 'Fetch Player Info', 'pinfo', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(56))
                                {
                                    SendNonQuery("Adding command player_dequeue", "INSERT INTO `adkats_commands` VALUES(56, 'Active', 'player_dequeue', 'Log', 'Dequeue Player Action', 'deq', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(57))
                                {
                                    SendNonQuery("Adding command self_help", "INSERT INTO `adkats_commands` VALUES(57, 'Active', 'self_help', 'Log', 'Request Server Commands', 'help', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(58))
                                {
                                    SendNonQuery("Adding command player_find", "INSERT INTO `adkats_commands` VALUES(58, 'Active', 'player_find', 'Log', 'Find Player', 'find', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(59))
                                {
                                    SendNonQuery("Adding command server_afk", "INSERT INTO `adkats_commands` VALUES(59, 'Active', 'server_afk', 'Log', 'Manage AFK Players', 'afk', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(60))
                                {
                                    SendNonQuery("Adding command player_pull", "INSERT INTO `adkats_commands` VALUES(60, 'Active', 'player_pull', 'Log', 'Pull Player', 'pull', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(61))
                                {
                                    SendNonQuery("Adding command admin_ignore", "INSERT INTO `adkats_commands` VALUES(61, 'Active', 'admin_ignore', 'Log', 'Ignore player report', 'ignore', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(62))
                                {
                                    SendNonQuery("Adding command player_report_ignore", "INSERT INTO `adkats_commands` VALUES(62, 'Invisible', 'player_report_ignore', 'Log', 'Report Player (Ignored)', 'ignorereport', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(63))
                                {
                                    SendNonQuery("Adding command player_mark", "INSERT INTO `adkats_commands` VALUES(63, 'Active', 'player_mark', 'Unable', 'Mark Player', 'mark', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(64))
                                {
                                    SendNonQuery("Adding command player_chat", "INSERT INTO `adkats_commands` VALUES(64, 'Active', 'player_chat', 'Log', 'Fetch Player Chat', 'pchat', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(65))
                                {
                                    SendNonQuery("Adding command player_whitelistanticheat", "INSERT INTO `adkats_commands` VALUES(65, 'Active', 'player_whitelistanticheat', 'Log', 'AntiCheat Whitelist Player', 'acwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (SendQuery("SELECT command_id FROM adkats_commands WHERE command_key = 'player_whitelisthackerchecker'", false))
                                {
                                    Log.Info("Updating command player_whitelisthackerchecker to new definition player_whitelistanticheat.");
                                    SendNonQuery("Updating command player_whitelisthackerchecker command_text to new definition.", "UPDATE adkats_commands SET adkats_commands.command_text = 'acwhitelist' WHERE command_id = 65", true);
                                    SendNonQuery("Updating command player_whitelisthackerchecker command_name to new definition.", "UPDATE adkats_commands SET adkats_commands.command_name = 'AntiCheat Whitelist Player' WHERE command_id = 65", true);
                                    SendNonQuery("Updating command player_whitelisthackerchecker command_key to new definition.", "UPDATE adkats_commands SET adkats_commands.command_key = 'player_whitelistanticheat' WHERE command_id = 65", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(66))
                                {
                                    SendNonQuery("Adding command player_lock", "INSERT INTO `adkats_commands` VALUES(66, 'Active', 'player_lock', 'Log', 'Lock Player Commands', 'lock', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(67))
                                {
                                    SendNonQuery("Adding command player_unlock", "INSERT INTO `adkats_commands` VALUES(67, 'Active', 'player_unlock', 'Log', 'Unlock Player Commands', 'unlock', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(68))
                                {
                                    SendNonQuery("Adding command self_rep", "INSERT INTO `adkats_commands` VALUES(68, 'Active', 'self_rep', 'Log', 'Request Server Reputation', 'rep', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(69))
                                {
                                    SendNonQuery("Adding command player_repboost", "INSERT INTO `adkats_commands` VALUES(69, 'Invisible', 'player_repboost', 'Log', 'Boost Player Reputation', 'rboost', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(70))
                                {
                                    SendNonQuery("Adding command player_log", "INSERT INTO `adkats_commands` VALUES(70, 'Active', 'player_log', 'Log', 'Log Player Information', 'log', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(71))
                                {
                                    SendNonQuery("Adding command player_whitelistping", "INSERT INTO `adkats_commands` VALUES(71, 'Active', 'player_whitelistping', 'Log', 'Ping Whitelist Player', 'pwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(72))
                                {
                                    SendNonQuery("Adding command player_ban_temp_old", "INSERT INTO `adkats_commands` VALUES(72, 'Invisible', 'player_ban_temp_old', 'Log', 'Previous Temp Ban', 'pretban', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(73))
                                {
                                    SendNonQuery("Adding command player_ban_perm_old", "INSERT INTO `adkats_commands` VALUES(73, 'Invisible', 'player_ban_perm_old', 'Log', 'Previous Perm Ban', 'preban', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(74))
                                {
                                    SendNonQuery("Adding command player_pm_send", "INSERT INTO `adkats_commands` VALUES(74, 'Active', 'player_pm_send', 'Unable', 'Player Private Message', 'msg', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(75))
                                {
                                    SendNonQuery("Adding command player_pm_reply", "INSERT INTO `adkats_commands` VALUES(75, 'Active', 'player_pm_reply', 'Unable', 'Player Private Reply', 'r', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(76))
                                {
                                    SendNonQuery("Adding command admin_pm_send", "INSERT INTO `adkats_commands` VALUES(76, 'Active', 'admin_pm_send', 'Unable', 'Admin Private Message', 'adminmsg', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(77))
                                {
                                    SendNonQuery("Adding command player_whitelistaa", "INSERT INTO `adkats_commands` VALUES(77, 'Active', 'player_whitelistaa', 'Log', 'AA Whitelist Player', 'aawhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(78))
                                {
                                    SendNonQuery("Adding command self_surrender", "INSERT INTO `adkats_commands` VALUES(78, 'Active', 'self_surrender', 'Log', 'Vote Surrender', 'surrender', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(79))
                                {
                                    SendNonQuery("Adding command self_votenext", "INSERT INTO `adkats_commands` VALUES(79, 'Active', 'self_votenext', 'Log', 'Vote Next Round', 'votenext', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(80))
                                {
                                    SendNonQuery("Adding command self_reportlist", "INSERT INTO `adkats_commands` VALUES(80, 'Active', 'self_reportlist', 'Log', 'List player reports', 'reportlist', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(81))
                                {
                                    SendNonQuery("Adding command plugin_restart", "INSERT INTO `adkats_commands` VALUES(81, 'Active', 'plugin_restart', 'Log', 'Restart AdKats', 'prestart', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(82))
                                {
                                    SendNonQuery("Adding command server_shutdown", "INSERT INTO `adkats_commands` VALUES(82, 'Active', 'server_shutdown', 'Log', 'Shutdown Server', 'shutdown', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(83))
                                {
                                    SendNonQuery("Adding command self_nosurrender", "INSERT INTO `adkats_commands` VALUES(83, 'Active', 'self_nosurrender', 'Log', 'Vote Against Surrender', 'nosurrender', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(84))
                                {
                                    SendNonQuery("Adding command player_whitelistspambot", "INSERT INTO `adkats_commands` VALUES(84, 'Active', 'player_whitelistspambot', 'Log', 'SpamBot Whitelist Player', 'spamwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(85))
                                {
                                    SendNonQuery("Adding command player_pm_start", "INSERT INTO `adkats_commands` VALUES(85, 'Invisible', 'player_pm_start', 'Log', 'Player Private Message Start', 'pmstart', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(86))
                                {
                                    SendNonQuery("Adding command player_pm_transmit", "INSERT INTO `adkats_commands` VALUES(86, 'Invisible', 'player_pm_transmit', 'Log', 'Player Private Message Transmit', 'pmtransmit', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(87))
                                {
                                    SendNonQuery("Adding command player_pm_cancel", "INSERT INTO `adkats_commands` VALUES(87, 'Invisible', 'player_pm_cancel', 'Log', 'Player Private Message Cancel', 'pmcancel', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(88))
                                {
                                    SendNonQuery("Adding command player_population_success", "INSERT INTO `adkats_commands` VALUES(88, 'Invisible', 'player_population_success', 'Log', 'Player Successfully Populated Server', 'popsuccess', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(89))
                                {
                                    SendNonQuery("Adding command server_map_detriment", "INSERT INTO `adkats_commands` VALUES(89, 'Invisible', 'server_map_detriment', 'Log', 'Map Detriment Log', 'mapdetriment', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(90))
                                {
                                    SendNonQuery("Adding command server_map_benefit", "INSERT INTO `adkats_commands` VALUES(90, 'Invisible', 'server_map_benefit', 'Log', 'Map Benefit Log', 'mapbenefit', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(91))
                                {
                                    SendNonQuery("Adding command plugin_update", "INSERT INTO `adkats_commands` VALUES(91, 'Active', 'plugin_update', 'Unable', 'Update AdKats', 'pupdate', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(92))
                                {
                                    SendNonQuery("Adding command player_warn", "INSERT INTO `adkats_commands` VALUES(92, 'Active', 'player_warn', 'Log', 'Warn Player', 'warn', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(93))
                                {
                                    SendNonQuery("Adding command server_countdown", "INSERT INTO `adkats_commands` VALUES(93, 'Active', 'server_countdown', 'Log', 'Run Countdown', 'cdown', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(94))
                                {
                                    SendNonQuery("Adding command player_whitelistreport", "INSERT INTO `adkats_commands` VALUES(94, 'Active', 'player_whitelistreport', 'Log', 'Report Whitelist Player', 'rwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(95))
                                {
                                    SendNonQuery("Adding command player_whitelistreport_remove", "INSERT INTO `adkats_commands` VALUES(95, 'Active', 'player_whitelistreport_remove', 'Log', 'Remove Report Whitelist', 'unrwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(96))
                                {
                                    SendNonQuery("Adding command player_whitelistspambot_remove", "INSERT INTO `adkats_commands` VALUES(96, 'Active', 'player_whitelistspambot_remove', 'Log', 'Remove SpamBot Whitelist', 'unspamwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(97))
                                {
                                    SendNonQuery("Adding command player_whitelistaa_remove", "INSERT INTO `adkats_commands` VALUES(97, 'Active', 'player_whitelistaa_remove', 'Log', 'Remove AA Whitelist', 'unaawhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(98))
                                {
                                    SendNonQuery("Adding command player_whitelistping_remove", "INSERT INTO `adkats_commands` VALUES(98, 'Active', 'player_whitelistping_remove', 'Log', 'Remove Ping Whitelist', 'unpwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(99))
                                {
                                    SendNonQuery("Adding command player_whitelistanticheat_remove", "INSERT INTO `adkats_commands` VALUES(99, 'Active', 'player_whitelistanticheat_remove', 'Log', 'Remove AntiCheat Whitelist', 'unacwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (SendQuery("SELECT command_id FROM adkats_commands WHERE command_key = 'player_whitelisthackerchecker_remove'", false))
                                {
                                    Log.Info("Updating command player_whitelisthackerchecker_remove to new definition player_whitelistanticheat_remove.");
                                    SendNonQuery("Updating command player_whitelisthackerchecker_remove command_text to new definition.", "UPDATE adkats_commands SET adkats_commands.command_text = 'unacwhitelist' WHERE command_id = 99", true);
                                    SendNonQuery("Updating command player_whitelisthackerchecker_remove command_name to new definition.", "UPDATE adkats_commands SET adkats_commands.command_name = 'Remove AntiCheat Whitelist' WHERE command_id = 99", true);
                                    SendNonQuery("Updating command player_whitelisthackerchecker_remove command_key to new definition.", "UPDATE adkats_commands SET adkats_commands.command_key = 'player_whitelistanticheat_remove' WHERE command_id = 99", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(100))
                                {
                                    SendNonQuery("Adding command player_slotspectator_remove", "INSERT INTO `adkats_commands` VALUES(100, 'Active', 'player_slotspectator_remove', 'Log', 'Remove Spectator Slot', 'unspectator', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(101))
                                {
                                    SendNonQuery("Adding command player_slotreserved_remove", "INSERT INTO `adkats_commands` VALUES(101, 'Active', 'player_slotreserved_remove', 'Log', 'Remove Reserved Slot', 'unreserved', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(102))
                                {
                                    SendNonQuery("Adding command player_whitelistbalance_remove", "INSERT INTO `adkats_commands` VALUES(102, 'Active', 'player_whitelistbalance_remove', 'Log', 'Remove Autobalance Whitelist', 'unmbwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(103))
                                {
                                    SendNonQuery("Adding command player_blacklistdisperse_remove", "INSERT INTO `adkats_commands` VALUES(103, 'Active', 'player_blacklistdisperse_remove', 'Log', 'Remove Autobalance Dispersion', 'undisperse', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(104))
                                {
                                    SendNonQuery("Adding command player_whitelistpopulator", "INSERT INTO `adkats_commands` VALUES(104, 'Active', 'player_whitelistpopulator', 'Log', 'Populator Whitelist Player', 'popwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(105))
                                {
                                    SendNonQuery("Adding command player_whitelistpopulator_remove", "INSERT INTO `adkats_commands` VALUES(105, 'Active', 'player_whitelistpopulator_remove', 'Log', 'Remove Populator Whitelist', 'unpopwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(106))
                                {
                                    SendNonQuery("Adding command player_whitelistteamkill", "INSERT INTO `adkats_commands` VALUES(106, 'Active', 'player_whitelistteamkill', 'Log', 'TeamKillTracker Whitelist Player', 'tkwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(107))
                                {
                                    SendNonQuery("Adding command player_whitelistteamkill_remove", "INSERT INTO `adkats_commands` VALUES(107, 'Active', 'player_whitelistteamkill_remove', 'Log', 'Remove TeamKillTracker Whitelist', 'untkwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(108))
                                {
                                    SendNonQuery("Adding command self_assist_unconfirmed", "INSERT INTO `adkats_commands` VALUES(108, 'Invisible', 'self_assist_unconfirmed', 'Log', 'Unconfirmed Assist', 'uassist', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(109))
                                {
                                    SendNonQuery("Adding command player_blacklistspectator", "INSERT INTO `adkats_commands` VALUES(109, 'Active', 'player_blacklistspectator', 'Log', 'Spectator Blacklist Player', 'specblacklist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(110))
                                {
                                    SendNonQuery("Adding command player_blacklistspectator_remove", "INSERT INTO `adkats_commands` VALUES(110, 'Active', 'player_blacklistspectator_remove', 'Log', 'Remove Spectator Blacklist', 'unspecblacklist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(111))
                                {
                                    SendNonQuery("Adding command player_blacklistreport", "INSERT INTO `adkats_commands` VALUES(111, 'Active', 'player_blacklistreport', 'Log', 'Report Source Blacklist', 'rblacklist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(112))
                                {
                                    SendNonQuery("Adding command player_blacklistreport_remove", "INSERT INTO `adkats_commands` VALUES(112, 'Active', 'player_blacklistreport_remove', 'Log', 'Remove Report Source Blacklist', 'unrblacklist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(113))
                                {
                                    SendNonQuery("Adding command player_whitelistcommand", "INSERT INTO `adkats_commands` VALUES(113, 'Active', 'player_whitelistcommand', 'Log', 'Command Target Whitelist', 'cwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(114))
                                {
                                    SendNonQuery("Adding command player_whitelistcommand_remove", "INSERT INTO `adkats_commands` VALUES(114, 'Active', 'player_whitelistcommand_remove', 'Log', 'Remove Command Target Whitelist', 'uncwhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(115))
                                {
                                    SendNonQuery("Adding command player_blacklistautoassist", "INSERT INTO `adkats_commands` VALUES(115, 'Active', 'player_blacklistautoassist', 'Log', 'Auto-Assist Blacklist', 'auablacklist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(116))
                                {
                                    SendNonQuery("Adding command player_blacklistautoassist_remove", "INSERT INTO `adkats_commands` VALUES(116, 'Active', 'player_blacklistautoassist_remove', 'Log', 'Remove Auto-Assist Blacklist', 'unauablacklist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(117))
                                {
                                    SendNonQuery("Adding command player_isadmin", "INSERT INTO `adkats_commands` VALUES(117, 'Active', 'player_isadmin', 'Log', 'Fetch Admin Status', 'isadmin', FALSE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(118))
                                {
                                    SendNonQuery("Adding command self_feedback", "INSERT INTO `adkats_commands` VALUES(118, 'Active', 'self_feedback', 'Log', 'Give Server Feedback', 'feedback', FALSE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(119))
                                {
                                    SendNonQuery("Adding command player_loadout", "INSERT INTO `adkats_commands` VALUES(119, 'Active', 'player_loadout', 'Log', 'Fetch Player Loadout', 'loadout', FALSE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(120))
                                {
                                    SendNonQuery("Adding command player_loadout_force", "INSERT INTO `adkats_commands` VALUES(120, 'Active', 'player_loadout_force', 'Log', 'Force Player Loadout', 'floadout', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(121))
                                {
                                    SendNonQuery("Adding command self_battlecry", "INSERT INTO `adkats_commands` VALUES(121, 'Active', 'self_battlecry', 'Log', 'Set Own Battlecry', 'battlecry', FALSE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(122))
                                {
                                    SendNonQuery("Adding command player_battlecry", "INSERT INTO `adkats_commands` VALUES(122, 'Active', 'player_battlecry', 'Log', 'Set Player Battlecry', 'setbattlecry', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(123))
                                {
                                    SendNonQuery("Adding command player_perks", "INSERT INTO `adkats_commands` VALUES(123, 'Active', 'player_perks', 'Log', 'Fetch Player Perks', 'perks', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                SendNonQuery("Updating command 123 player interaction", "UPDATE `adkats_commands` SET `command_playerInteraction` = 0 WHERE `command_id` = 123", false);
                                if (!_CommandIDDictionary.ContainsKey(124))
                                {
                                    SendNonQuery("Adding command player_ping", "INSERT INTO `adkats_commands` VALUES(124, 'Active', 'player_ping', 'Log', 'Fetch Player Ping', 'ping', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(125))
                                {
                                    SendNonQuery("Adding command player_forceping", "INSERT INTO `adkats_commands` VALUES(125, 'Active', 'player_forceping', 'Log', 'Force Manual Player Ping', 'fping', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(126))
                                {
                                    SendNonQuery("Adding command player_debugassist", "INSERT INTO `adkats_commands` VALUES(126, 'Active', 'player_debugassist', 'Log', 'Debug Assist Losing Team', 'debugassist', FALSE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(127))
                                {
                                    SendNonQuery("Adding command player_changetag", "INSERT INTO `adkats_commands` VALUES(127, 'Invisible', 'player_changetag', 'Mandatory', 'Player Changed Clan Tag', 'changetag', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(128))
                                {
                                    SendNonQuery("Adding command player_discordlink", "INSERT INTO `adkats_commands` VALUES(128, 'Active', 'player_discordlink', 'Log', 'Link Player to Discord Member', 'discordlink', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(129))
                                {
                                    SendNonQuery("Adding command player_blacklistallcaps", "INSERT INTO `adkats_commands` VALUES(129, 'Active', 'player_blacklistallcaps', 'Log', 'All-Caps Chat Blacklist', 'allcapsblacklist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(130))
                                {
                                    SendNonQuery("Adding command player_blacklistallcaps_remove", "INSERT INTO `adkats_commands` VALUES(130, 'Active', 'player_blacklistallcaps_remove', 'Log', 'Remove All-Caps Chat Blacklist', 'unallcapsblacklist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(131))
                                {
                                    SendNonQuery("Adding command poll_trigger", "INSERT INTO `adkats_commands` VALUES(131, 'Active', 'poll_trigger', 'Log', 'Trigger Poll', 'poll', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(132))
                                {
                                    SendNonQuery("Adding command poll_vote", "INSERT INTO `adkats_commands` VALUES(132, 'Active', 'poll_vote', 'Log', 'Vote In Poll', 'vote', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(133))
                                {
                                    SendNonQuery("Adding command poll_cancel", "INSERT INTO `adkats_commands` VALUES(133, 'Active', 'poll_cancel', 'Unable', 'Cancel Active Poll', 'pollcancel', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(134))
                                {
                                    SendNonQuery("Adding command poll_complete", "INSERT INTO `adkats_commands` VALUES(134, 'Active', 'poll_complete', 'Unable', 'Complete Active Poll', 'pollcomplete', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(135))
                                {
                                    SendNonQuery("Adding command server_nuke_winning", "INSERT INTO `adkats_commands` VALUES(135, 'Active', 'server_nuke_winning', 'Log', 'Server Nuke Winning Team', 'wnuke', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(136))
                                {
                                    SendNonQuery("Adding command player_loadout_ignore", "INSERT INTO `adkats_commands` VALUES(136, 'Active', 'player_loadout_ignore', 'Log', 'Ignore Player Loadout', 'iloadout', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(137))
                                {
                                    SendNonQuery("Adding command player_challenge_play", "INSERT INTO `adkats_commands` VALUES(137, 'Active', 'player_challenge_play', 'Log', 'Challenge Playing Status', 'challengeplay', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(138))
                                {
                                    SendNonQuery("Adding command player_challenge_ignore", "INSERT INTO `adkats_commands` VALUES(138, 'Active', 'player_challenge_ignore', 'Log', 'Challenge Ignoring Status', 'challengeignore', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(139))
                                {
                                    SendNonQuery("Adding command self_challenge", "INSERT INTO `adkats_commands` VALUES(139, 'Active', 'self_challenge', 'Log', 'Challenge', 'ch', FALSE, 'Any')", true);
                                    newCommands = true;
                                }
                                else if (_CommandIDDictionary[139].command_text == "challenge")
                                {
                                    SendNonQuery("Updating command 139 text", "UPDATE `adkats_commands` SET `command_text` = 'ch' WHERE `command_id` = 139", false);
                                }
                                if (!_CommandIDDictionary.ContainsKey(140))
                                {
                                    SendNonQuery("Adding command player_challenge_autokill", "INSERT INTO `adkats_commands` VALUES(140, 'Active', 'player_challenge_autokill', 'Log', 'Challenge AutoKill Status', 'challengeautokill', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(141))
                                {
                                    SendNonQuery("Adding command player_challenge_autokill_remove", "INSERT INTO `adkats_commands` VALUES(141, 'Active', 'player_challenge_autokill_remove', 'Log', 'Remove Challenge AutoKill Status', 'unchallengeautokill', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(142))
                                {
                                    SendNonQuery("Adding command player_challenge_play_remove", "INSERT INTO `adkats_commands` VALUES(142, 'Active', 'player_challenge_play_remove', 'Log', 'Remove Challenge Playing Status', 'unchallengeplay', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(143))
                                {
                                    SendNonQuery("Adding command player_challenge_ignore_remove", "INSERT INTO `adkats_commands` VALUES(143, 'Active', 'player_challenge_ignore_remove', 'Log', 'Remove Challenge Ignoring Status', 'unchallengeignore', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(144))
                                {
                                    SendNonQuery("Adding command player_challenge_complete", "INSERT INTO `adkats_commands` VALUES(144, 'Invisible', 'player_challenge_complete', 'Log', 'Player Completed Challenge', 'challengecomplete', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(145))
                                {
                                    SendNonQuery("Adding command player_report_expire", "INSERT INTO `adkats_commands` VALUES(145, 'Invisible', 'player_report_expire', 'Log', 'Report Player (Expired)', 'expirereport', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(146))
                                {
                                    SendNonQuery("Adding command player_unmute", "INSERT INTO `adkats_commands` VALUES(146, 'Active', 'player_unmute', 'Log', 'Unmute Player', 'unmute', TRUE, 'Any')", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(147))
                                {
                                    SendNonQuery("Adding command player_whitelistbf4db", "INSERT INTO `adkats_commands` VALUES(147, 'Active', 'player_whitelistbf4db', 'Log', 'BF4DB Whitelist Player', 'bf4dbwhitelist', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(148))
                                {
                                    SendNonQuery("Adding command player_whitelistbf4db_remove", "INSERT INTO `adkats_commands` VALUES(148, 'Active', 'player_whitelistbf4db_remove', 'Log', 'Remove BF4DB Whitelist', 'unbf4dbwhitelist', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(149))
                                {
                                    SendNonQuery("Adding command player_persistentmute", "INSERT INTO `adkats_commands` VALUES(149, 'Active', 'player_persistentmute', 'Log', 'Persistent Mute Player', 'pmute', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(150))
                                {
                                    SendNonQuery("Adding command player_persistentmute_remove", "INSERT INTO `adkats_commands` VALUES(150, 'Active', 'player_persistentmute_remove', 'Log', 'Remove Persistent Mute', 'punmute', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(151))
                                {
                                    SendNonQuery("Adding command player_watchlist", "INSERT INTO `adkats_commands` VALUES(151, 'Active', 'player_watchlist', 'Log', 'Add Player to Watchlist', 'watch', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(152))
                                {
                                    SendNonQuery("Adding command player_watchlist_remove", "INSERT INTO `adkats_commands` VALUES(152, 'Active', 'player_watchlist_remove', 'Log', 'Remove Player from Watchlist', 'rwatch', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(153))
                                {
                                    SendNonQuery("Adding command player_persistentmute_force", "INSERT INTO `adkats_commands` VALUES(153, 'Active', 'player_persistentmute_force', 'Log', 'Persistent Force Mute Player', 'fmute', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(154))
                                {
                                    SendNonQuery("Adding command player_whitelistmoveprotection", "INSERT INTO `adkats_commands` VALUES(154, 'Active', 'player_whitelistmoveprotection', 'Log', 'Move Protection Whitelist Player', 'movewhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(155))
                                {
                                    SendNonQuery("Adding command player_whitelistmoveprotection_remove", "INSERT INTO `adkats_commands` VALUES(155, 'Active', 'player_whitelistmoveprotection_remove', 'Log', 'Remove Move Protection Whitelist', 'unmovewhitelist', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(156))
                                {
                                    SendNonQuery("Adding command player_language_punish", "INSERT INTO `adkats_commands` VALUES(156, 'Active', 'player_language_punish', 'Log', 'Issue Language Punish', 'lpunish', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(157))
                                {
                                    SendNonQuery("Adding command player_language_reset", "INSERT INTO `adkats_commands` VALUES(157, 'Active', 'player_language_reset', 'Log', 'Issue Language Counter Reset', 'lreset', TRUE, 'Any')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(158))
                                {
                                    SendNonQuery("Adding command player_whitelistba", "INSERT INTO `adkats_commands` VALUES(158, 'Active', 'player_whitelistba', 'Log', 'BattlefieldAgency Whitelist Player', 'bawhitelist', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (!_CommandIDDictionary.ContainsKey(159))
                                {
                                    SendNonQuery("Adding command player_whitelistba_remove", "INSERT INTO `adkats_commands` VALUES(159, 'Active', 'player_whitelistba_remove', 'Log', 'Remove BattlefieldAgency Whitelist', 'unbawhitelist', TRUE, 'AnyHidden')", true);
                                    newCommands = true;
                                }
                                if (newCommands)
                                {
                                    FetchCommands();
                                    return;
                                }
                            }
                            else
                            {
                                Log.Error("Commands could not be fetched.");
                            }
                            //Update functions for command timeouts
                            UpdateCommandTimeouts();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching commands from database.", e));
            }
            if (displayUpdate)
            {
                UpdateSettingPage();
            }
            Log.Debug(() => "fetchCommands finished!", 6);
        }

        private String GetReadableMap(String mapKey)
        {
            try
            {
                String map = mapKey;
                ReadableMaps.TryGetValue(mapKey, out map);
                return map;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error getting readable map.", e));
            }
            return "Unknown";
        }

        private String GetReadableMode(String modeKey)
        {
            try
            {
                String mode = modeKey;
                ReadableMaps.TryGetValue(modeKey, out mode);
                return mode;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error getting readable mode.", e));
            }
            return "Unknown";
        }

        private void UpdateCommandTimeouts()
        {
            _commandTimeoutDictionary["self_rules"] = (plugin => (plugin._ServerRulesList.Count() * plugin._ServerRulesInterval));
            _commandTimeoutDictionary["player_punish"] = (plugin => (18));
            _commandTimeoutDictionary["player_kick"] = (plugin => (45));
            _commandTimeoutDictionary["player_blacklistdisperse"] = (plugin => (30));
            _commandTimeoutDictionary["player_ban_temp"] = (plugin => (30));
            _commandTimeoutDictionary["player_ban_perm"] = (plugin => (90));
            _commandTimeoutDictionary["player_ban_perm_future"] = (plugin => (90));
            _commandTimeoutDictionary["player_report"] = (plugin => (10));
            _commandTimeoutDictionary["self_kill"] = (plugin => (10 * 60));
        }

        private void FillConditionalAllowedCommands(ARole aRole)
        {
            //Teamswap Command
            ACommand teamswapCommand;
            if (_CommandKeyDictionary.TryGetValue("self_teamswap", out teamswapCommand))
            {
                if (!aRole.ConditionalAllowedCommands.ContainsKey(teamswapCommand.command_key))
                {
                    aRole.ConditionalAllowedCommands.Add(teamswapCommand.command_key, new KeyValuePair<Func<AdKats, APlayer, Boolean>, ACommand>(TeamSwapFunc, teamswapCommand));
                }
            }
            else
            {
                Log.Error("Unable to find teamswap command when assigning conditional commands.");
            }
            //Admins Command
            ACommand adminsCommand;
            if (_CommandKeyDictionary.TryGetValue("self_admins", out adminsCommand))
            {
                if (!aRole.ConditionalAllowedCommands.ContainsKey(adminsCommand.command_key))
                {
                    aRole.ConditionalAllowedCommands.Add(adminsCommand.command_key, new KeyValuePair<Func<AdKats, APlayer, Boolean>, ACommand>(AAPerkFunc, adminsCommand));
                }
            }
            else
            {
                Log.Error("Unable to find teamswap command when assigning conditional commands.");
            }
        }

        private void AssignPlayerAdminAssistant(APlayer aPlayer)
        {
            Log.Debug(() => "PlayerIsAdminAssistant starting!", 7);
            if (!_firstUserListComplete)
            {
                // Completely bypass this on the first user listing
                // Adminship is not loaded yet
                return;
            }
            if (!_EnableAdminAssistants)
            {
                aPlayer.player_aa = false;
                return;
            }
            if (aPlayer.player_aa_fetched)
            {
                return;
            }
            if (PlayerIsAdmin(aPlayer))
            {
                aPlayer.player_aa_fetched = true;
                aPlayer.player_aa = false;
                return;
            }
            List<ASpecialPlayer> matchingPlayers = GetMatchingVerboseASPlayersOfGroup("whitelist_adminassistant", aPlayer);
            if (matchingPlayers.Count > 0)
            {
                aPlayer.player_aa_fetched = true;
                aPlayer.player_aa = true;
                return;
            }
            if (_databaseConnectionCriticalState)
            {
                aPlayer.player_aa = false;
                return;
            }
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var row = connection.QueryFirstOrDefault(@"
                        (SELECT
                             'isAdminAssistant'
                         FROM
                             `adkats_records_main`
                         WHERE (
                             SELECT count(`command_action`)
                             FROM `adkats_records_main`
                             WHERE `command_action` = " + GetCommandByKey("player_report_confirm").command_id + @"
                             AND `source_id` = " + aPlayer.player_id + @"
                             AND (`adkats_records_main`.`record_time` BETWEEN date_sub(UTC_TIMESTAMP(),INTERVAL 30 DAY) AND UTC_TIMESTAMP())
                         ) >= " + _MinimumRequiredMonthlyReports + @" LIMIT 1)
                        UNION
                        (SELECT
                             'isGrandfatheredAdminAssistant'
                         FROM
                             `adkats_records_main`
                         WHERE (
                             SELECT count(`command_action`)
                             FROM `adkats_records_main`
                             WHERE `command_action` = " + GetCommandByKey("player_report_confirm").command_id + @"
                             AND `source_id` = " + aPlayer.player_id + @"
                         ) >= 75 LIMIT 1)");
                    if (row != null)
                    {
                        aPlayer.player_aa = true;
                    }
                    aPlayer.player_aa_fetched = true;
                    return;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while checking if player is an admin assistant.", e));
            }
            Log.Debug(() => "PlayerIsAdminAssistant finished!", 7);
        }

        public Boolean GetGlobalUTCTimestamp(out DateTime globalUTCTime)
        {
            globalUTCTime = UtcNow();
            try
            {
                String response = Util.HttpDownload("http://www.timeanddate.com/clocks/onlyforusebyconfiguration2.php");
                String[] elements = response.Split(' ');
                Double epochSeconds = 0;
                if (Double.TryParse(elements[0], out epochSeconds))
                {
                    globalUTCTime = DateTimeFromEpochSeconds(epochSeconds);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        private Boolean GetDatabaseUTCTimestamp(out DateTime dbUTC)
        {
            dbUTC = UtcNow();
            try
            {
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    var row = connection.QueryFirstOrDefault(@"SELECT UTC_TIMESTAMP() AS `current_time`");
                    if (row != null)
                    {
                        dbUTC = (DateTime)row.current_time;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        private void UpdateMULTIBalancerWhitelist()
        {
            try
            {
                if (_FeedMultiBalancerWhitelist)
                {
                    List<string> autobalanceWhitelistedPlayers = new List<String>();
                    //Pull players from special player cache
                    List<ASpecialPlayer> whitelistedPlayers = GetVerboseASPlayersOfGroup("whitelist_multibalancer");
                    if (whitelistedPlayers.Any())
                    {
                        foreach (ASpecialPlayer asPlayer in whitelistedPlayers)
                        {
                            String playerIdentifier = null;
                            if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_guid))
                            {
                                playerIdentifier = asPlayer.player_object.player_guid;
                            }
                            else
                            {
                                playerIdentifier = asPlayer.player_identifier;
                            }
                            //Skip if no valid info found
                            if (String.IsNullOrEmpty(playerIdentifier))
                            {
                                Log.Error("Player under whitelist_multibalancer was not valid. Unable to add to MULTIBalancer whitelist.");
                                continue;
                            }
                            if (!autobalanceWhitelistedPlayers.Contains(playerIdentifier))
                            {
                                autobalanceWhitelistedPlayers.Add(playerIdentifier);
                            }
                        }
                    }
                    SetExternalPluginSetting("MULTIbalancer", "1 - Settings|Whitelist", CPluginVariable.EncodeStringArray(autobalanceWhitelistedPlayers.ToArray()));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating MULTIBalancer whitelist.", e));
            }
        }

        private void UpdateMULTIBalancerDisperseList()
        {
            try
            {
                if (_FeedMultiBalancerDisperseList)
                {
                    List<string> evenDispersionList = new List<String>();
                    //Pull players from special player cache
                    List<ASpecialPlayer> evenDispersedPlayers = GetVerboseASPlayersOfGroup("blacklist_dispersion");
                    if (evenDispersedPlayers.Any())
                    {
                        foreach (ASpecialPlayer asPlayer in evenDispersedPlayers)
                        {
                            String playerIdentifier = null;
                            if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_guid))
                            {
                                playerIdentifier = asPlayer.player_object.player_guid;
                            }
                            else
                            {
                                playerIdentifier = asPlayer.player_identifier;
                            }
                            //Skip if no valid info found
                            if (String.IsNullOrEmpty(playerIdentifier))
                            {
                                Log.Error("Player under blacklist_dispersion was not valid. Unable to add to MULTIBalancer even dispersion list.");
                                continue;
                            }
                            if (!evenDispersionList.Contains(playerIdentifier))
                            {
                                evenDispersionList.Add(playerIdentifier);
                            }
                        }
                    }
                    SetExternalPluginSetting("MULTIbalancer", "1 - Settings|Disperse Evenly List", CPluginVariable.EncodeStringArray(evenDispersionList.ToArray()));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating MULTIBalancer even dispersion list.", e));
            }
        }

        private void UpdateTeamKillTrackerWhitelist()
        {
            try
            {
                if (_FeedTeamKillTrackerWhitelist)
                {
                    List<string> teamKillTrackerWhitelistedPlayers = new List<String>();
                    //Pull players from special player cache
                    List<ASpecialPlayer> whitelistedPlayers = GetVerboseASPlayersOfGroup("whitelist_teamkill");
                    if (whitelistedPlayers.Any())
                    {
                        foreach (ASpecialPlayer asPlayer in whitelistedPlayers)
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
                            //Skip if no valid info found
                            if (String.IsNullOrEmpty(playerIdentifier))
                            {
                                Log.Error("Player under whitelist_teamkill was not valid. Unable to add to TeamKillTracker whitelist.");
                                continue;
                            }
                            if (!teamKillTrackerWhitelistedPlayers.Contains(playerIdentifier))
                            {
                                teamKillTrackerWhitelistedPlayers.Add(playerIdentifier);
                            }
                        }
                    }
                    SetExternalPluginSetting("TeamKillTracker", "Whitelist", CPluginVariable.EncodeStringArray(teamKillTrackerWhitelistedPlayers.ToArray()));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating TeamKillTracker whitelist.", e));
            }
        }

        private void UpdateBF4DBWhitelist()
        {
            try
            {
                if (_FeedBF4DBWhitelist)
                {
                    List<string> bf4dbWhitelistedPlayers = new List<String>();
                    //Pull players from special player cache
                    List<ASpecialPlayer> whitelistedPlayers = GetVerboseASPlayersOfGroup("whitelist_bf4db");
                    if (whitelistedPlayers.Any())
                    {
                        foreach (ASpecialPlayer asPlayer in whitelistedPlayers)
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
                            //Skip if no valid info found
                            if (String.IsNullOrEmpty(playerIdentifier))
                            {
                                Log.Error("Player under whitelist_bf4db was not valid. Unable to add to BF4DB whitelist.");
                                continue;
                            }
                            if (!bf4dbWhitelistedPlayers.Contains(playerIdentifier))
                            {
                                bf4dbWhitelistedPlayers.Add(playerIdentifier);
                            }
                        }
                    }
                    SetExternalPluginSetting("BF4DB", "Whitelist", CPluginVariable.EncodeStringArray(bf4dbWhitelistedPlayers.ToArray()));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating BF4DB whitelist.", e));
            }
        }

        private void UpdateBAWhitelist()
        {
            try
            {
                if (_FeedBAWhitelist)
                {
                    List<string> baWhitelistedPlayers = new List<String>();
                    //Pull players from special player cache
                    List<ASpecialPlayer> whitelistedPlayers = GetVerboseASPlayersOfGroup("whitelist_ba");
                    if (whitelistedPlayers.Any())
                    {
                        foreach (ASpecialPlayer asPlayer in whitelistedPlayers)
                        {
                            String playerIdentifier = null;
                            if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_guid))
                            {
                                playerIdentifier = asPlayer.player_object.player_guid;
                            }
                            else
                            {
                                playerIdentifier = asPlayer.player_identifier;
                            }
                            //Skip if no valid info found
                            if (String.IsNullOrEmpty(playerIdentifier))
                            {
                                Log.Error("Player under whitelist_ba was not valid. Unable to add to BattlefieldAgency whitelist.");
                                continue;
                            }
                            if (!baWhitelistedPlayers.Contains(playerIdentifier))
                            {
                                baWhitelistedPlayers.Add(playerIdentifier);
                            }
                        }
                    }
                    SetExternalPluginSetting("BattlefieldAgency", "Local Whitelist", CPluginVariable.EncodeStringArray(baWhitelistedPlayers.ToArray()));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating BattlefieldAgency whitelist.", e));
            }
        }

        private void UpdateReservedSlots()
        {
            try
            {
                if (_CurrentReservedSlotPlayers == null)
                {
                    return;
                }
                if (!_FeedServerReservedSlots)
                {
                    ExecuteCommand("procon.protected.send", "reservedSlotsList.list");
                    return;
                }
                Log.Debug(() => "Checking validity of reserved slotted players.", 6);
                List<string> allowedReservedSlotPlayers = new List<string>();
                //Pull players from special player cache
                List<ASpecialPlayer> reservedPlayers = GetVerboseASPlayersOfGroup("slot_reserved");
                if (reservedPlayers.Any())
                {
                    foreach (ASpecialPlayer asPlayer in reservedPlayers)
                    {
                        String playerIdentifier = null;
                        if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_name))
                        {
                            playerIdentifier = asPlayer.player_object.player_name;
                        }
                        else
                        {
                            if (IsSoldierNameValid(asPlayer.player_identifier))
                            {
                                playerIdentifier = asPlayer.player_identifier;
                            }
                            else
                            {
                                Log.Error("Player under reserved_slot list '" + asPlayer.player_identifier + "' was not a valid soldier name. Unable to add to reserved slot list.");
                            }
                        }
                        //Skip if no valid info found
                        if (String.IsNullOrEmpty(playerIdentifier))
                        {
                            continue;
                        }
                        if (!allowedReservedSlotPlayers.Contains(playerIdentifier))
                        {
                            allowedReservedSlotPlayers.Add(playerIdentifier);
                        }
                    }
                }
                //All players fetched, update the server lists
                //Remove soldiers from the list where needed
                foreach (String playerName in _CurrentReservedSlotPlayers)
                {
                    if (!allowedReservedSlotPlayers.Contains(playerName))
                    {
                        Log.Debug(() => playerName + " in server reserved slots, but not in allowed reserved players. Removing.", 3);
                        ExecuteCommand("procon.protected.send", "reservedSlotsList.remove", playerName);
                        Threading.Wait(5);
                    }
                }
                //Add soldiers to the list where needed
                foreach (String playerName in allowedReservedSlotPlayers)
                {
                    if (!_CurrentReservedSlotPlayers.Contains(playerName))
                    {
                        Log.Debug(() => playerName + " in allowed reserved players, but not in server reserved slots. Adding.", 3);
                        ExecuteCommand("procon.protected.send", "reservedSlotsList.add", playerName);
                        Threading.Wait(5);
                    }
                }
                //Save the list
                ExecuteCommand("procon.protected.send", "reservedSlotsList.save");
                //Display the list
                ExecuteCommand("procon.protected.send", "reservedSlotsList.list");
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating server reserved slots.", e));
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

        private void UpdateSpectatorList()
        {
            Log.Debug(() => "Entering UpdateSpectatorList", 6);
            try
            {
                if (!_FeedServerSpectatorList || _CurrentSpectatorListPlayers == null)
                {
                    return;
                }
                Log.Debug(() => "Updating spectator list players.", 6);
                List<string> allowedSpectatorSlotPlayers = new List<string>();
                //Pull players from special player cache
                List<ASpecialPlayer> spectators = GetVerboseASPlayersOfGroup("slot_spectator");
                if (spectators.Any())
                {
                    foreach (ASpecialPlayer asPlayer in spectators)
                    {
                        String playerIdentifier = null;
                        if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_name))
                        {
                            playerIdentifier = asPlayer.player_object.player_name;
                        }
                        else
                        {
                            if (IsSoldierNameValid(asPlayer.player_identifier))
                            {
                                playerIdentifier = asPlayer.player_identifier;
                            }
                            else
                            {
                                Log.Error("Player under slot_spectator list '" + asPlayer.player_identifier + "' was not a valid soldier name. Unable to add to spectator slot list.");
                            }
                        }
                        //Skip if no valid info found
                        if (String.IsNullOrEmpty(playerIdentifier))
                        {
                            continue;
                        }
                        if (!allowedSpectatorSlotPlayers.Contains(playerIdentifier))
                        {
                            Log.Debug(() => "Valid slot_spectator " + playerIdentifier + " fetched.", 5);
                            allowedSpectatorSlotPlayers.Add(playerIdentifier);
                        }
                    }
                }
                else
                {
                    Log.Debug(() => "No players under special player group slot_spectator.", 5);
                }
                //All players fetched, update the server lists
                if (allowedSpectatorSlotPlayers.Count() < 15)
                {
                    //Remove soldiers from the list where needed
                    foreach (String playerName in _CurrentSpectatorListPlayers)
                    {
                        if (!allowedSpectatorSlotPlayers.Contains(playerName))
                        {
                            Log.Debug(() => playerName + " in server spectator slots, but not in allowed spectator players. Removing.", 3);
                            ExecuteCommand("procon.protected.send", "spectatorList.remove", playerName);
                            Threading.Wait(5);
                        }
                    }
                    //Add soldiers to the list where needed
                    foreach (String playerName in allowedSpectatorSlotPlayers)
                    {
                        if (!_CurrentSpectatorListPlayers.Contains(playerName))
                        {
                            Log.Debug(() => playerName + " in allowed spectator players, but not in server spectator slots. Adding.", 3);
                            ExecuteCommand("procon.protected.send", "spectatorList.add", playerName);
                            Threading.Wait(5);
                        }
                    }
                }
                else
                {
                    //If there are 15 or more players in the list, don't push to the server
                    //The server cannot take over 15 players in the spectator list, yay DICE
                    ExecuteCommand("procon.protected.send", "spectatorList.clear");
                }
                //Save the list
                ExecuteCommand("procon.protected.send", "spectatorList.save");
                //Display the list
                ExecuteCommand("procon.protected.send", "spectatorList.list");
                Log.Debug(() => "DONE checking validity of spectator list players.", 6);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating server spectator list.", e));
            }
            Log.Debug(() => "Exiting UpdateSpectatorList", 6);
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

        private void ParseExternalCommand(Object commandParams)
        {
            Log.Debug(() => "ParseExternalCommand starting!", 6);
            try
            {
                //Set current thread id
                Thread.CurrentThread.Name = "ParseExternalCommand";

                //Create the new record
                ARecord record = new ARecord
                {
                    record_source = ARecord.Sources.ExternalPlugin,
                    record_access = ARecord.AccessMethod.HiddenExternal,
                    server_id = _serverInfo.ServerID,
                    record_time = UtcNow()
                };

                //Parse information into a record
                if (commandParams == null)
                {
                    Log.Error("Command params were null when parsing external command. Unable to continue.");
                    return;
                }
                String[] paramArray = commandParams as String[];
                if (paramArray == null)
                {
                    Log.Error("Command params could not be properly converted to String[]. Unable to continue.");
                    return;
                }
                if (paramArray.Length != 2)
                {
                    Log.Error("Invalid parameter count [source, jsonParams]. Unable to continue.");
                    return;
                }
                String commandSource = paramArray[0];
                String unparsedCommandJSON = paramArray[1];

                Hashtable parsedClientInformation = (Hashtable)JSON.JsonDecode(unparsedCommandJSON);
                if (parsedClientInformation == null)
                {
                    Log.Error("Command params could not be properly converted from JSON. Unable to continue.");
                    return;
                }

                //Import the caller identity
                if (!parsedClientInformation.ContainsKey("caller_identity"))
                {
                    Log.Error("Parsed command didn't contain a caller_identity! Unable to process external command.");
                    return;
                }
                string callerIdentity = (String)parsedClientInformation["caller_identity"];
                if (String.IsNullOrEmpty(callerIdentity))
                {
                    Log.Error("caller_identity was empty. Unable to process external command.");
                    return;
                }
                record.external_callerIdentity = callerIdentity;

                //Import the callback options
                if (!parsedClientInformation.ContainsKey("response_requested"))
                {
                    Log.Error("Parsed command didn't contain response_requested! Unable to process external command.");
                    return;
                }
                bool callbackRequested = (Boolean)parsedClientInformation["response_requested"];
                record.external_responseRequested = callbackRequested;
                if (callbackRequested)
                {
                    if (!parsedClientInformation.ContainsKey("response_class"))
                    {
                        Log.Error("Parsed command didn't contain a response_class! Unable to process external command.");
                        return;
                    }
                    string callbackClass = (String)parsedClientInformation["response_class"];
                    if (String.IsNullOrEmpty(callbackClass))
                    {
                        Log.Error("response_class was empty. Unable to process external command.");
                        return;
                    }
                    record.external_responseClass = callbackClass;

                    if (!parsedClientInformation.ContainsKey("response_method"))
                    {
                        Log.Error("Parsed command didn't contain a response_method! Unable to process external command.");
                        return;
                    }
                    string callbackMethod = (String)parsedClientInformation["response_method"];
                    if (String.IsNullOrEmpty(callbackMethod))
                    {
                        Log.Error("response_method was empty. Unable to process external command.");
                        return;
                    }
                    record.external_responseMethod = callbackMethod;
                }

                //Import the command type
                if (!parsedClientInformation.ContainsKey("command_type"))
                {
                    record.record_exception = Log.HandleException(new AException("Parsed command didn't contain a command_type!"));
                    return;
                }
                string unparsedCommandType = (String)parsedClientInformation["command_type"];
                if (String.IsNullOrEmpty(unparsedCommandType))
                {
                    Log.Error("command_type was empty. Unable to process external command.");
                    return;
                }
                if (!_CommandKeyDictionary.TryGetValue(unparsedCommandType, out record.command_type))
                {
                    Log.Error("command_type was invalid, not found in definition. Unable to process external command.");
                    return;
                }

                //Import the command numeric
                //Only required for temp ban & persistent mutes
                //ToDo: what about whitelists?
                if (record.command_type.command_key == "player_ban_temp"
                    || record.command_type.command_key == "player_persistentmute"
                    || record.command_type.command_key == "player_persistentmute_force")
                {
                    if (!parsedClientInformation.ContainsKey("command_numeric"))
                    {
                        Log.Error("Parsed command didn't contain a command_numeric! Unable to parse command.");
                        return;
                    }
                    if (!Int32.TryParse(parsedClientInformation["command_numeric"].ToString(), out record.command_numeric))
                    {
                        Log.Error("Parsed command command_numeric was not a number! Unable to parse command.");
                        return;
                    }
                }

                //Import the source name
                if (!parsedClientInformation.ContainsKey("source_name"))
                {
                    Log.Error("Parsed command didn't contain a source_name!");
                    return;
                }
                string sourceName = (String)parsedClientInformation["source_name"];
                if (String.IsNullOrEmpty(sourceName))
                {
                    Log.Error("source_name was empty. Unable to process external command.");
                    return;
                }
                record.source_name = sourceName;

                //Import the target name
                if (!parsedClientInformation.ContainsKey("target_name"))
                {
                    Log.Error("Parsed command didn't contain a target_name! Unable to process external command.");
                    return;
                }
                string targetName = (String)parsedClientInformation["target_name"];
                if (String.IsNullOrEmpty(targetName))
                {
                    Log.Error("source_name was empty. Unable to process external command.");
                    return;
                }
                record.target_name = targetName;

                //Import the target guid
                String target_guid = null;
                if (parsedClientInformation.ContainsKey("target_guid"))
                {
                    target_guid = (String)parsedClientInformation["target_guid"];
                }

                //Import the record message
                if (!parsedClientInformation.ContainsKey("record_message"))
                {
                    Log.Error("Parsed command didn't contain a record_message! Unable to process external command.");
                    return;
                }
                string recordMessage = (String)parsedClientInformation["record_message"];
                if (String.IsNullOrEmpty(recordMessage))
                {
                    Log.Error("record_message was empty. Unable to process external command.");
                    return;
                }
                record.record_message = recordMessage;

                _PlayerDictionary.TryGetValue(record.source_name, out record.source_player);
                if (record.source_player != null)
                {
                    if (!HasAccess(record.source_player, record.command_type))
                    {
                        Log.Warn("External command blocked: " + record.source_name + " lacks access to " + record.command_type.command_key);
                        return;
                    }
                    record.source_player.LastUsage = UtcNow();
                }
                if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player) && record.command_type.command_key.StartsWith("player_"))
                {
                    if (String.IsNullOrEmpty(target_guid))
                    {
                        Log.Error("Target player '" + record.GetTargetNames() + "' was not found. And target_guid was not provided. Unable to process external command.");
                        return;
                    }
                    record.target_player = FetchPlayer(true, false, false, null, -1, record.target_name, target_guid, null, null);
                }
                if (record.target_player != null)
                {
                    record.target_player.LastUsage = UtcNow();
                }
                QueueRecordForProcessing(record);
            }
            catch (Exception e)
            {
                //Log the error in console
                Log.HandleException(new AException("Unable to process external command.", e));
            }
            Log.Debug(() => "ParseExternalCommand finished!", 6);
        }

        private void SendAuthorizedSoldiers(Object clientInformation)
        {
            Log.Debug(() => "SendAuthorizedSoldiers starting!", 6);
            try
            {
                //Set current thread id
                Thread.CurrentThread.Name = "SendAuthorizedSoldiers";

                //Create the new record
                ARecord record = new ARecord
                {
                    record_source = ARecord.Sources.ExternalPlugin,
                    record_access = ARecord.AccessMethod.HiddenExternal,
                    record_time = UtcNow()
                };

                //Parse information into a record
                Hashtable parsedClientInformation = (Hashtable)JSON.JsonDecode((String)clientInformation);

                //Import the caller identity
                if (!parsedClientInformation.ContainsKey("caller_identity"))
                {
                    Log.Error("Parsed command didn't contain a caller_identity! Unable to process soldier fetch.");
                    return;
                }
                string callerIdentity = (String)parsedClientInformation["caller_identity"];
                if (String.IsNullOrEmpty(callerIdentity))
                {
                    Log.Error("caller_identity was empty. Unable to process soldier fetch.");
                    return;
                }
                record.external_callerIdentity = callerIdentity;

                //Import the callback options
                if (!parsedClientInformation.ContainsKey("response_requested"))
                {
                    Log.Error("Parsed command didn't contain response_requested! Unable to process soldier fetch.");
                    return;
                }
                bool callbackRequested = (Boolean)parsedClientInformation["response_requested"];
                record.external_responseRequested = callbackRequested;
                if (callbackRequested)
                {
                    if (!parsedClientInformation.ContainsKey("response_class"))
                    {
                        Log.Error("Parsed command didn't contain a response_class! Unable to process soldier fetch.");
                        return;
                    }
                    string callbackClass = (String)parsedClientInformation["response_class"];
                    if (String.IsNullOrEmpty(callbackClass))
                    {
                        Log.Error("response_class was empty. Unable to process soldier fetch.");
                        return;
                    }
                    record.external_responseClass = callbackClass;

                    if (!parsedClientInformation.ContainsKey("response_method"))
                    {
                        Log.Error("Parsed command didn't contain a response_method!");
                        return;
                    }
                    string callbackMethod = (String)parsedClientInformation["response_method"];
                    if (String.IsNullOrEmpty(callbackMethod))
                    {
                        Log.Error("response_method was empty. Unable to process soldier fetch.");
                        return;
                    }
                    record.external_responseMethod = callbackMethod;
                }
                else
                {
                    Log.Error("response_requested must be true to return authorized soldiers. Unable to process soldier fetch.");
                    return;
                }

                List<APlayer> soldierList;
                Boolean containsUserSubset = parsedClientInformation.ContainsKey("user_subset");
                Boolean containsUserRole = parsedClientInformation.ContainsKey("user_role");
                if (containsUserRole && containsUserSubset)
                {
                    Log.Error("Both user_subset and user_role were used in request. Only one may be used at any time. Unable to process soldier fetch.");
                    return;
                }
                if (containsUserRole)
                {
                    string roleString = (String)parsedClientInformation["user_role"];
                    if (String.IsNullOrEmpty(roleString))
                    {
                        Log.Error("user_role was found in request, but it was empty. Unable to process soldier fetch.");
                        return;
                    }
                    ARole aRole;
                    if (!_RoleKeyDictionary.TryGetValue(roleString, out aRole))
                    {
                        Log.Error("Specified user role '" + roleString + "' was not found. Unable to process soldier fetch.");
                        return;
                    }
                    soldierList = FetchSoldiersOfRole(aRole);
                }
                else if (containsUserSubset)
                {
                    string subset = (String)parsedClientInformation["user_subset"];
                    if (String.IsNullOrEmpty(subset))
                    {
                        Log.Debug(() => "user_subset was found in request, but it was empty. Unable to process soldier fetch.", 3);
                        return;
                    }
                    switch (subset)
                    {
                        case "all":
                            soldierList = FetchAllUserSoldiers();
                            break;
                        case "admin":
                            soldierList = FetchAdminSoldiers();
                            break;
                        case "elevated":
                            soldierList = FetchElevatedSoldiers();
                            break;
                        default:
                            Log.Error("request_subset was found in request, but it was invalid. Unable to process soldier fetch.");
                            return;
                    }
                }
                else
                {
                    Log.Error("Neither user_subset nor user_role was found in request. Unable to process soldier fetch.");
                    return;
                }

                if (soldierList == null)
                {
                    Log.Error("Internal error, all parameters were correct, but soldier list was not fetched.");
                    return;
                }

                String[] soldierNames = (from aPlayer in soldierList where (!String.IsNullOrEmpty(aPlayer.player_name) && aPlayer.game_id == _serverInfo.GameID) select aPlayer.player_name).ToArray();

                Hashtable responseHashtable = new Hashtable();
                responseHashtable.Add("caller_identity", "AdKats");
                responseHashtable.Add("response_requested", false);
                responseHashtable.Add("response_type", "FetchAuthorizedSoldiers");
                responseHashtable.Add("response_value", CPluginVariable.EncodeStringArray(soldierNames));

                //TODO: add error message if target not found

                ExecuteCommand("procon.protected.plugins.call", record.external_responseClass, record.external_responseMethod, "AdKats", JSON.JsonEncode(responseHashtable));
            }
            catch (Exception e)
            {
                //Log the error in console
                Log.HandleException(new AException("Error returning authorized soldiers .", e));
            }
            Log.Debug(() => "SendAuthorizedSoldiers finished!", 6);
        }

        private Boolean SubscribeClient(AClient aClient)
        {
            if (aClient == null)
            {
                Log.Error("24134: Client null when issuing subscription.");
                return false;
            }
            if (String.IsNullOrEmpty(aClient.ClientName))
            {
                Log.Error("Attempted to enable subscription without a client name.");
                return false;
            }
            if (String.IsNullOrEmpty(aClient.ClientMethod))
            {
                Log.Error("Attempted to enable subscription for " + aClient.ClientName + " without a client method.");
                return false;
            }
            if (String.IsNullOrEmpty(aClient.SubscriptionGroup))
            {
                Log.Error("Attempted to enable subscription for " + aClient.ClientName + " with a blank group.");
                return false;
            }
            if (!_subscriptionGroups.Contains(aClient.SubscriptionGroup))
            {
                Log.Error("Attempted to enable subscription for " + aClient.ClientName + " with an invalid group.");
                return false;
            }
            if (_subscribedClients.Any(iClient => iClient.ClientName == aClient.ClientName && iClient.ClientMethod == aClient.ClientMethod && iClient.SubscriptionGroup == aClient.SubscriptionGroup))
            {
                Log.Error("Client " + aClient.ClientName + " already subscribed to " + aClient.SubscriptionGroup + ". Events are being sent to " + aClient.ClientMethod + ".");
                return false;
            }
            _subscribedClients.Add(aClient);
            Log.Success(aClient.ClientName + " now subscribed to " + aClient.SubscriptionGroup + ". Events will be sent to " + aClient.ClientMethod + ".");
            return true;
        }

        private Boolean UnsubscribeClient(AClient aClient)
        {
            if (aClient == null)
            {
                Log.Error("24169: Client null when issuing subscription.");
                return false;
            }
            AClient eClient = _subscribedClients.Where(iClient => iClient.ClientName == aClient.ClientName && iClient.ClientMethod == aClient.ClientMethod && iClient.SubscriptionGroup == aClient.SubscriptionGroup).FirstOrDefault();
            if (eClient != null)
            {
                _subscribedClients.Remove(eClient);
                Log.Success("Client " + aClient.ClientName + " unsubscribed from " + aClient.SubscriptionGroup + ". Events no longer being sent to " + aClient.ClientMethod + ".");
                return true;
            }
            Log.Error("Client " + aClient.ClientName + " attempted to unsubscribe from " + aClient.SubscriptionGroup + " when they don't have an active subscription.");
            return false;
        }

        private ArrayList FetchAdKatsReputationDefinitions()
        {
            Log.Debug(() => "Entering FetchAdKatsReputationDefinitions", 7);
            ArrayList repTable = null;
            String repInfo;
            Log.Debug(() => "Fetching reputation definitions...", 2);
            try
            {
                repInfo = Util.HttpDownload("https://raw.githubusercontent.com/Hedius/E4GLAdKats/main/adkatsreputationstats.json" + "?cacherand=" + Environment.TickCount);
                Log.Debug(() => "Reputation definitions fetched.", 1);
            }
            catch (Exception)
            {
                try
                {
                    repInfo = Util.HttpDownload("https://adkats.e4gl.com/adkatsreputationstats.json" + "?cacherand=" + Environment.TickCount);
                    Log.Debug(() => "Reputation definitions fetched from backup location.", 1);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            try
            {
                repTable = (ArrayList)JSON.JsonDecode(repInfo);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while parsing reputation definitions.", e));
            }
            Log.Debug(() => "Exiting FetchAdKatsReputationDefinitions", 7);
            return repTable;
        }

        private List<ASpecialGroup> FetchASpecialGroupDefinitions()
        {
            Log.Debug(() => "Entering FetchASpecialGroupDefinitions", 7);
            List<ASpecialGroup> SpecialGroupsList = null;
            String groupInfo;
            Log.Debug(() => "Fetching special group definitions...", 2);
            try
            {
                groupInfo = Util.HttpDownload("https://raw.githubusercontent.com/Hedius/E4GLAdKats/main/adkatsspecialgroups.json" + "?cacherand=" + Environment.TickCount);
                Log.Debug(() => "Special group definitions fetched.", 1);
            }
            catch (Exception)
            {
                try
                {
                    groupInfo = Util.HttpDownload("https://adkats.e4gl.com/adkatsspecialgroups.json" + "?cacherand=" + Environment.TickCount);
                    Log.Debug(() => "Special group definitions fetched from backup location.", 1);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            try
            {
                Hashtable groupsTable = (Hashtable)JSON.JsonDecode(groupInfo);
                ArrayList GroupsList = (ArrayList)groupsTable["SpecialGroups"];
                if (GroupsList == null || GroupsList.Count == 0)
                {
                    return null;
                }
                SpecialGroupsList = new List<ASpecialGroup>();
                foreach (Hashtable groupHash in GroupsList)
                {
                    ASpecialGroup update = new ASpecialGroup();
                    //update_id
                    update.group_id = Convert.ToInt32(groupHash["group_id"]);
                    //group_key
                    Object group_key = groupHash["group_key"];
                    if (group_key == null)
                    {
                        Log.Error("AdKats special group entry group_key was not found.");
                        continue;
                    }
                    update.group_key = (String)group_key;
                    //group_name
                    Object group_name = groupHash["group_name"];
                    if (group_name == null)
                    {
                        Log.Error("AdKats special group entry group_name was not found.");
                        continue;
                    }
                    update.group_name = (String)group_name;
                    //Add
                    SpecialGroupsList.Add(update);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while parsing special group definitions.", e));
                return null;
            }
            Log.Debug(() => "Exiting FetchASpecialGroupDefinitions", 7);
            return SpecialGroupsList;
        }

        private void RunSQLUpdates(Boolean async)
        {
            Log.Debug(() => "Entering RunSQLUpdates", 7);
            if (Threading.IsAlive("SQLUpdater"))
            {
                return;
            }
            if (async)
            {
                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                {
                    Thread.CurrentThread.Name = "SQLUpdater";
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                    RunSQLUpdates();
                    Threading.StopWatchdog();
                })));
            }
            else
            {
                RunSQLUpdates();
            }
            Log.Debug(() => "Exiting RunSQLUpdates", 7);
        }

        private List<ASQLUpdate> FetchSQLUpdates()
        {
            Log.Debug(() => "Entering FetchSQLUpdates", 7);
            List<ASQLUpdate> SQLUpdates = new List<ASQLUpdate>();
            try
            {
                String updateInfo;
                try
                {
                    updateInfo = Util.HttpDownload("https://raw.githubusercontent.com/Hedius/E4GLAdKats/main/adkatsupdates.json" + "?cacherand=" + Environment.TickCount);
                    Log.Debug(() => "SQL updates fetched.", 1);
                }
                catch (Exception)
                {
                    try
                    {
                        updateInfo = Util.HttpDownload("https://adkats.e4gl.com/adkatsupdates.json" + "?cacherand=" + Environment.TickCount);
                        Log.Debug(() => "SQL updates fetched from backup location.", 1);
                    }
                    catch (Exception)
                    {
                        Log.Error("Unable to download SQL updates.");
                        return SQLUpdates;
                    }
                }
                Hashtable updateTable = (Hashtable)JSON.JsonDecode(updateInfo);
                ArrayList SQLUpdateList = (ArrayList)updateTable["SQLUpdates"];
                if (SQLUpdateList != null && SQLUpdateList.Count > 0)
                {
                    Log.Debug(() => "SQL updates found. Parsing...", 5);
                    foreach (Hashtable updateHash in SQLUpdateList)
                    {
                        ASQLUpdate update = new ASQLUpdate();
                        //update_id
                        update.update_id = (String)updateHash["update_id"];
                        if (String.IsNullOrEmpty(update.update_id))
                        {
                            Log.Error("SQL update update_id was not found or empty.");
                            continue;
                        }
                        Log.Debug(() => "Parsing SQL Update '" + update.update_id + "'", 5);
                        //version_minimum
                        update.version_minimum = (String)updateHash["version_minimum"];
                        Log.Debug(() => "SQL update '" + update.update_id + "' version_minimum: " + update.version_minimum, 5);
                        //version_maximum
                        update.version_maximum = (String)updateHash["version_maximum"];
                        Log.Debug(() => "SQL update '" + update.update_id + "' version_maximum: " + update.version_maximum, 5);
                        //message_name
                        update.message_name = (String)updateHash["message_name"];
                        if (String.IsNullOrEmpty(update.message_name))
                        {
                            Log.Error("SQL update '" + update.update_id + "' message_name was not found or empty.");
                            continue;
                        }
                        Log.Debug(() => "SQL update '" + update.update_id + "' message_name: " + update.message_name, 5);
                        //message_success
                        update.message_success = (String)updateHash["message_success"];
                        if (String.IsNullOrEmpty(update.message_success))
                        {
                            Log.Error("SQL update '" + update.update_id + "' message_success was not found or empty.");
                            continue;
                        }
                        Log.Debug(() => "SQL update '" + update.update_id + "' message_success: " + update.message_success, 5);
                        //message_failure
                        update.message_failure = (String)updateHash["message_failure"];
                        if (String.IsNullOrEmpty(update.message_failure))
                        {
                            Log.Error("SQL update '" + update.update_id + "' message_failure was not found or empty.");
                            continue;
                        }
                        Log.Debug(() => "SQL update '" + update.update_id + "' message_failure: " + update.message_failure, 5);
                        //update_checks_hasResults
                        Object update_checks_hasResults = updateHash["update_checks_hasResults"];
                        if (update_checks_hasResults == null)
                        {
                            Log.Error("SQL update '" + update.update_id + "' update_checks_hasResults was not found.");
                            continue;
                        }
                        update.update_checks_hasResults = (Boolean)update_checks_hasResults;
                        Log.Debug(() => "SQL update '" + update.update_id + "' update_checks_hasResults: " + update.update_checks_hasResults, 5);
                        //update_checks
                        ArrayList update_checks = (ArrayList)updateHash["update_checks"];
                        if (update_checks == null)
                        {
                            Log.Error("SQL update '" + update.update_id + "' update_checks was not found.");
                            continue;
                        }
                        foreach (String line in update_checks)
                        {
                            update.update_checks.Add(line);
                        }
                        Log.Debug(() => "SQL update '" + update.update_id + "' update_checks: " + update.update_checks.Count, 5);
                        //update_execute_requiresModRows
                        Object update_execute_requiresModRows = updateHash["update_execute_requiresModRows"];
                        if (update_execute_requiresModRows == null)
                        {
                            Log.Error("SQL update '" + update.update_id + "' update_execute_requiresModRows was not found.");
                            continue;
                        }
                        update.update_execute_requiresModRows = (Boolean)update_execute_requiresModRows;
                        Log.Debug(() => "SQL update '" + update.update_id + "' update_execute_requiresModRows: " + update.update_execute_requiresModRows, 5);
                        //update_execute
                        ArrayList update_execute = (ArrayList)updateHash["update_execute"];
                        if (update_execute == null)
                        {
                            Log.Error("SQL update '" + update.update_id + "' update_execute was not found.");
                            continue;
                        }
                        foreach (String line in update_execute)
                        {
                            update.update_execute.Add(line);
                        }
                        Log.Debug(() => "SQL update '" + update.update_id + "' update_execute: " + update.update_execute.Count, 5);
                        //update_success
                        ArrayList update_success = (ArrayList)updateHash["update_success"];
                        if (update_success == null)
                        {
                            Log.Error("SQL update '" + update.update_id + "' update_success was not found.");
                            continue;
                        }
                        foreach (String line in update_success)
                        {
                            update.update_success.Add(line);
                        }
                        Log.Debug(() => "SQL update '" + update.update_id + "' update_success: " + update.update_success.Count, 5);
                        //update_failure
                        ArrayList update_failure = (ArrayList)updateHash["update_failure"];
                        if (update_failure == null)
                        {
                            Log.Error("SQL update '" + update.update_id + "' update_failure was not found.");
                            continue;
                        }
                        foreach (String line in update_failure)
                        {
                            update.update_failure.Add(line);
                        }
                        Log.Debug(() => "SQL update '" + update.update_id + "' update_failure: " + update.update_failure.Count, 5);
                        //Add
                        SQLUpdates.Add(update);
                    }
                }
                else
                {
                    Log.Debug(() => "No SQL updates found.", 5);
                }
            }
            catch (Exception)
            {
                Log.Error("Unable to process SQL updates.");
            }
            Log.Debug(() => "Exiting FetchSQLUpdates", 7);
            return SQLUpdates;
        }

        public Boolean PlayerIsAdmin(APlayer aPlayer)
        {
            return aPlayer != null &&
                   aPlayer.player_role != null &&
                   RoleIsAdmin(aPlayer.player_role);
        }

        public Boolean RoleIsAdmin(ARole aRole)
        {
            try
            {
                if (aRole == null)
                {
                    Log.Error("role null in RoleIsAdmin");
                    return false;
                }
                if (aRole.RoleAllowedCommands.Values.Any(command => command.command_playerInteraction))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching role admin status.", e));
            }
            return false;
        }

        public ACommand GetCommandByKey(String commandKey)
        {
            ACommand command = null;
            try
            {
                if (String.IsNullOrEmpty(commandKey))
                {
                    Log.HandleException(new AException("commandKey was null when fetching command"));
                    return command;
                }
                if (!_CommandKeyDictionary.TryGetValue(commandKey, out command))
                {
                    Threading.Wait(1000);
                    if (!_CommandKeyDictionary.TryGetValue(commandKey, out command))
                    {
                        Log.HandleException(new AException("Unable to get command for key '" + commandKey + "'"));
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching command by key.", e));
            }
            return command;
        }

        public Boolean GetTeamByID(Int32 teamID, out ATeam aTeam)
        {
            aTeam = null;
            if (_teamDictionary.TryGetValue(teamID, out aTeam))
            {
                return true;
            }
            if (_roundState == RoundState.Playing)
            {
                Log.HandleException(new AException("Team not found for ID " + teamID + " in dictionary of " + _teamDictionary.Count + " teams."));
            }
            return false;
        }

        public Boolean IsSoldierNameValid(String soldierName)
        {
            try
            {
                Log.Debug(() => "Checking player '" + soldierName + "' for validity.", 7);
                if (String.IsNullOrEmpty(soldierName))
                {
                    Log.Debug(() => "Soldier Name empty or null.", 5);
                    return false;
                }
                if (soldierName.Length > 16)
                {
                    Log.Debug(() => "Soldier Name '" + soldierName + "' too long, maximum length is 16 characters.", 5);
                    return false;
                }
                if (!Regex.IsMatch(soldierName, @"^[a-zA-Z0-9_\-]+$"))
                {
                    Log.Debug(() => "Soldier Name '" + soldierName + "' contained invalid characters.", 5);
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                //Soldier id caused exception in the regex, definitely not valid
                Log.Error("Soldier Name '" + soldierName + "' contained invalid characters.");
                return false;
            }
        }

        public TimeSpan NowDuration(DateTime diff)
        {
            return (UtcNow() - diff).Duration();
        }

        public String FormatTimeString(TimeSpan timeSpan, Int32 maxComponents)
        {
            Log.Debug(() => "Entering formatTimeString", 7);
            String timeString = null;
            if (maxComponents < 1)
            {
                return timeString;
            }
            try
            {
                String formattedTime = (timeSpan.TotalMilliseconds >= 0) ? ("") : ("-");

                Double secondSubset = Math.Abs(timeSpan.TotalSeconds);
                if (secondSubset < 1)
                {
                    return "0s";
                }
                Double minuteSubset = (secondSubset / 60);
                Double hourSubset = (minuteSubset / 60);
                Double daySubset = (hourSubset / 24);
                Double weekSubset = (daySubset / 7);
                Double monthSubset = (weekSubset / 4);
                Double yearSubset = (monthSubset / 12);

                int years = (Int32)yearSubset;
                Int32 months = (Int32)monthSubset % 12;
                Int32 weeks = (Int32)weekSubset % 4;
                Int32 days = (Int32)daySubset % 7;
                Int32 hours = (Int32)hourSubset % 24;
                Int32 minutes = (Int32)minuteSubset % 60;
                Int32 seconds = (Int32)secondSubset % 60;

                Int32 usedComponents = 0;
                if (years > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += years + "y";
                }
                if (months > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += months + "M";
                }
                if (weeks > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += weeks + "w";
                }
                if (days > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += days + "d";
                }
                if (hours > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += hours + "h";
                }
                if (minutes > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += minutes + "m";
                }
                if (seconds > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += seconds + "s";
                }
                timeString = formattedTime;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while formatting time String.", e));
            }
            if (String.IsNullOrEmpty(timeString))
            {
                timeString = "0s";
            }
            Log.Debug(() => "Exiting formatTimeString", 7);
            return timeString;
        }

        public DateTime UtcNow()
        {
            return DateTime.UtcNow + _dbTimingOffset;
        }

        public void KickPlayerMessage(APlayer player, String message)
        {
            var kickDuration = 0;
            if (GameVersion == GameVersionEnum.BF4)
            {
                kickDuration = 30;
                if (player.player_spawnedOnce)
                {
                    kickDuration = 6;
                }
            }
            KickPlayerMessage(player.player_name, message, kickDuration);
        }

        public void BanKickPlayerMessage(APlayer aPlayer, String message)
        {
            Int32 kickDuration = 0;
            if (GameVersion == GameVersionEnum.BF4 &&
                _BanEnforcerBF4LenientKick)
            {
                kickDuration = 30;
                if (aPlayer.player_spawnedOnce)
                {
                    kickDuration = 6;
                }
                if (aPlayer.BanEnforceCount > 2)
                {
                    kickDuration = 0;
                }
            }
            BanKickPlayerMessage(aPlayer, message, kickDuration);
        }

        public static String Encode(String str)
        {
            byte[] encbuff = Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(encbuff);
        }

        //Calling this method will make the settings window refresh with new data
        public void UpdateSettingPage()
        {
            SetExternalPluginSetting("AdKats", "UpdateSettings", "Update");
        }

        //Calls setVariable with the given parameters
        public void SetExternalPluginSetting(String pluginName, String settingName, String settingValue)
        {
            if (String.IsNullOrEmpty(pluginName) || String.IsNullOrEmpty(settingName) || settingValue == null)
            {
                Log.Error("Required inputs null or empty in setExternalPluginSetting");
                return;
            }
            ExecuteCommand("procon.protected.plugins.setVariable", pluginName, settingName, settingValue);
        }

        private Int32 GetPlayerCount()
        {
            return GetPlayerCount(true, true, true, null);
        }
    }
}
