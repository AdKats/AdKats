using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;

using Flurl;
using Flurl.Http;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PRoCon.Core;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        // =====================================================================
        // VPN/Proxy Kick Settings
        // =====================================================================

        private Boolean _VpnKickEnabled = false;
        private Boolean _VpnKickVpn = true;
        private Boolean _VpnKickProxy = true;
        private Boolean _VpnKickTor = true;
        private Int32 _VpnKickRiskThreshold = 66;
        private String _VpnKickMessage = "VPN/Proxy connections are not allowed on this server.";
        private Boolean _VpnKickScheduleEnabled = false;
        private String _VpnKickScheduleTimezone = "UTC";
        private String _VpnKickScheduleJson = @"{
  ""monday"":    [{""start"": ""00:00"", ""end"": ""23:59""}],
  ""tuesday"":   [{""start"": ""00:00"", ""end"": ""23:59""}],
  ""wednesday"": [{""start"": ""00:00"", ""end"": ""23:59""}],
  ""thursday"":  [{""start"": ""00:00"", ""end"": ""23:59""}],
  ""friday"":    [{""start"": ""00:00"", ""end"": ""23:59""}],
  ""saturday"":  [{""start"": ""00:00"", ""end"": ""23:59""}],
  ""sunday"":    [{""start"": ""00:00"", ""end"": ""23:59""}]
}";

        // Parsed schedule cache
        private Dictionary<DayOfWeek, List<Tuple<TimeSpan, TimeSpan>>> _VpnKickScheduleParsed;

        // Pending IP-to-player lookups (with timestamp for expiry sweep)
        private readonly Dictionary<String, Tuple<APlayer, DateTime>> _PendingIPChecks = new Dictionary<String, Tuple<APlayer, DateTime>>();
        private readonly Object _PendingIPChecksLock = new Object();

        // =====================================================================
        // IP Check via Procon v2 OnIPChecked event
        // =====================================================================

        /// <summary>
        /// Requests an IP check from Procon's shared proxycheck.io service.
        /// Called when a player joins and their IP is known.
        /// </summary>
        private void RequestIPCheck(APlayer aPlayer)
        {
            if (String.IsNullOrEmpty(aPlayer.player_ip))
            {
                return;
            }

            // Strip port if present
            String ip = aPlayer.player_ip;
            int colonIdx = ip.LastIndexOf(':');
            if (colonIdx > 0)
            {
                ip = ip.Substring(0, colonIdx);
            }

            // Track this pending lookup and sweep stale entries
            lock (_PendingIPChecksLock)
            {
                _PendingIPChecks[ip] = Tuple.Create(aPlayer, UtcNow());
                // Sweep entries older than 60 seconds
                var staleKeys = _PendingIPChecks
                    .Where(kvp => (UtcNow() - kvp.Value.Item2).TotalSeconds > 60)
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var key in staleKeys)
                {
                    _PendingIPChecks.Remove(key);
                }
            }

            // Request the check from Procon's shared IPCheckService
            ExecuteCommand("procon.protected.ipcheck", ip);
            Log.Debug(() => "Requested IP check for " + aPlayer.player_name + " (" + ip + ")", 4);
        }

        /// <summary>
        /// Receives IP check results asynchronously from Procon's shared proxycheck.io service.
        /// </summary>
        public override void OnIPChecked(string ip, string countryName, string countryCode,
            string city, string provider, bool isVPN, bool isProxy, bool isTor, int risk)
        {
            if (!_pluginEnabled) return;

            try
            {
                APlayer aPlayer = null;
                lock (_PendingIPChecksLock)
                {
                    if (_PendingIPChecks.ContainsKey(ip))
                    {
                        aPlayer = _PendingIPChecks[ip].Item1;
                        _PendingIPChecks.Remove(ip);
                    }
                }

                if (aPlayer == null)
                {
                    Log.Debug(() => "Received IP check for unknown IP: " + ip, 5);
                    return;
                }

                // Store location data on the player
                aPlayer.location = new IPLocation
                {
                    IP = ip,
                    CountryName = countryName ?? "",
                    CountryCode = countryCode ?? "",
                    City = city ?? "",
                    Provider = provider ?? "",
                    IsVPN = isVPN,
                    IsProxy = isProxy,
                    IsTor = isTor,
                    Risk = risk,
                    Status = "success"
                };

                Log.Debug(() => "IP check result for " + aPlayer.player_name + ": " +
                    countryName + " (" + countryCode + "), VPN=" + isVPN +
                    ", Proxy=" + isProxy + ", Tor=" + isTor + ", Risk=" + risk, 3);

                // Check for VPN/Proxy kick
                if (_VpnKickEnabled)
                {
                    CheckVpnKick(aPlayer);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error processing IP check result for " + ip, e));
            }
        }

        // =====================================================================
        // VPN/Proxy Kick Logic
        // =====================================================================

        private void CheckVpnKick(APlayer aPlayer)
        {
            if (aPlayer.location == null) return;

            // Check if player is whitelisted
            if (IsPlayerInSpecialGroup(aPlayer, "whitelist_vpn"))
            {
                Log.Debug(() => aPlayer.player_name + " is VPN-whitelisted, skipping VPN kick check.", 4);
                return;
            }

            // Check schedule
            if (_VpnKickScheduleEnabled && !IsWithinVpnKickSchedule())
            {
                Log.Debug(() => "VPN kick is outside scheduled active window. Skipping.", 5);
                return;
            }

            Boolean shouldKick = false;
            String reason = "";

            if (_VpnKickVpn && aPlayer.location.IsVPN)
            {
                shouldKick = true;
                reason = "VPN";
            }
            else if (_VpnKickProxy && aPlayer.location.IsProxy)
            {
                shouldKick = true;
                reason = "Proxy";
            }
            else if (_VpnKickTor && aPlayer.location.IsTor)
            {
                shouldKick = true;
                reason = "Tor";
            }
            else if (aPlayer.location.Risk >= _VpnKickRiskThreshold)
            {
                shouldKick = true;
                reason = "High risk (" + aPlayer.location.Risk + ")";
            }

            if (shouldKick)
            {
                Log.Info(aPlayer.player_name + " kicked for " + reason + " connection from " +
                    aPlayer.location.CountryName + " (" + aPlayer.location.Provider + ")");

                // Issue kick through AdKats action system
                ARecord record = new ARecord
                {
                    record_source = ARecord.Sources.Automated,
                    server_id = _serverInfo.ServerID,
                    command_type = GetCommandByKey("player_kick"),
                    command_numeric = 0,
                    target_name = aPlayer.player_name,
                    target_player = aPlayer,
                    source_name = "AdKats",
                    record_message = _VpnKickMessage + " [" + reason + "]",
                    record_time = UtcNow()
                };
                QueueRecordForProcessing(record);
            }
        }

        /// <summary>
        /// Checks whether the current time falls within the VPN kick active schedule.
        /// </summary>
        private Boolean IsWithinVpnKickSchedule()
        {
            try
            {
                if (_VpnKickScheduleParsed == null)
                {
                    ParseVpnKickSchedule();
                }

                TimeZoneInfo tz;
                try
                {
                    tz = TimeZoneInfo.FindSystemTimeZoneById(_VpnKickScheduleTimezone);
                }
                catch
                {
                    // Fallback to UTC if timezone is invalid
                    tz = TimeZoneInfo.Utc;
                }

                DateTime localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                DayOfWeek today = localNow.DayOfWeek;
                TimeSpan currentTime = localNow.TimeOfDay;

                if (!_VpnKickScheduleParsed.ContainsKey(today))
                {
                    return false; // No schedule for today = kicks disabled
                }

                foreach (var window in _VpnKickScheduleParsed[today])
                {
                    if (currentTime >= window.Item1 && currentTime <= window.Item2)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error checking VPN kick schedule.", e));
                return true; // Default to active on error
            }
        }

        /// <summary>
        /// Parses the JSON schedule string into a lookup dictionary.
        /// </summary>
        private void ParseVpnKickSchedule()
        {
            _VpnKickScheduleParsed = new Dictionary<DayOfWeek, List<Tuple<TimeSpan, TimeSpan>>>();

            try
            {
                var schedule = JObject.Parse(_VpnKickScheduleJson);

                var dayMap = new Dictionary<String, DayOfWeek>
                {
                    { "monday", DayOfWeek.Monday },
                    { "tuesday", DayOfWeek.Tuesday },
                    { "wednesday", DayOfWeek.Wednesday },
                    { "thursday", DayOfWeek.Thursday },
                    { "friday", DayOfWeek.Friday },
                    { "saturday", DayOfWeek.Saturday },
                    { "sunday", DayOfWeek.Sunday }
                };

                foreach (var kvp in dayMap)
                {
                    if (schedule[kvp.Key] is JArray windows)
                    {
                        var windowList = new List<Tuple<TimeSpan, TimeSpan>>();
                        foreach (JObject window in windows)
                        {
                            String startStr = window["start"]?.ToString();
                            String endStr = window["end"]?.ToString();
                            if (!String.IsNullOrEmpty(startStr) && !String.IsNullOrEmpty(endStr))
                            {
                                TimeSpan start = TimeSpan.ParseExact(startStr, "hh\\:mm", CultureInfo.InvariantCulture);
                                TimeSpan end = TimeSpan.ParseExact(endStr, "hh\\:mm", CultureInfo.InvariantCulture);
                                windowList.Add(new Tuple<TimeSpan, TimeSpan>(start, end));
                            }
                        }
                        if (windowList.Count > 0)
                        {
                            _VpnKickScheduleParsed[kvp.Value] = windowList;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error parsing VPN kick schedule JSON.", e));
                _VpnKickScheduleParsed = null;
            }
        }

        // =====================================================================
        // Battlelog Communication Thread (legacy, delegates to EZScale when enabled)
        // =====================================================================

        private void QueuePlayerForBattlelogInfoFetch(APlayer aPlayer)
        {
            Log.Debug(() => "Entering QueuePlayerForBattlelogInfoFetch", 6);
            try
            {
                Log.Debug(() => "Preparing to queue player for battlelog info fetch.", 6);
                lock (_BattlelogFetchQueue)
                {
                    if (!_BattlelogFetchQueue.Contains(aPlayer))
                    {
                        _BattlelogFetchQueue.Enqueue(aPlayer);
                        Log.Debug(() => "Player queued for battlelog info fetch.", 6);
                    }

                    _BattlelogCommWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queuing player for battlelog info fetch.", e));
            }
            Log.Debug(() => "Exiting QueuePlayerForBattlelogInfoFetch", 6);
        }

        public void BattlelogCommThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Battlelog Comm Thread", 1);
                Thread.CurrentThread.Name = "BattlelogComm";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering Battlelog Comm Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        //Sleep for 10ms
                        Threading.Wait(10);

                        if (!_firstPlayerListComplete)
                        {
                            Log.Debug(() => "Playerlist not complete yet. Skipping loop until completed.", 6);
                            continue;
                        }

                        //Handle Inbound player fetches
                        if (_BattlelogFetchQueue.Count > 0)
                        {
                            APlayer aPlayer;

                            lock (_BattlelogFetchQueue)
                            {
                                //Dequeue the record
                                aPlayer = _BattlelogFetchQueue.Dequeue();
                            }

                            // Skip player if they left the server already while in queue.
                            if (!_PlayerDictionary.ContainsKey(aPlayer.player_name))
                            {
                                Log.Debug(() => "Player not in PlayerDictionary when fetching battlelog info. Skipping.", 6);
                                continue;
                            }

                            Log.Debug(() => "Preparing to fetch battlelog info for player", 6);

                            //Old Tag
                            String oldTag = aPlayer.player_clanTag;

                            // If EZScale is enabled, use it instead of direct Battlelog scraping
                            if (_EZScaleEnabled)
                            {
                                FetchPlayerEZScaleInfo(aPlayer);
                            }
                            else
                            {
                                FetchPlayerBattlelogInformation(aPlayer);
                            }

                            Log.Debug(() => "Battlelog info fetched for " + aPlayer.GetVerboseName() + ".", 6);

                            //Check for clan tag changes
                            if (!String.IsNullOrEmpty(aPlayer.player_clanTag) && (String.IsNullOrEmpty(oldTag) || aPlayer.player_clanTag != oldTag))
                            {
                                ARecord record = new ARecord
                                {
                                    record_source = ARecord.Sources.Automated,
                                    server_id = _serverInfo.ServerID,
                                    command_type = GetCommandByKey("player_changetag"),
                                    command_numeric = 0,
                                    target_name = aPlayer.player_name,
                                    target_player = aPlayer,
                                    source_name = "AdKats",
                                    record_message = oldTag,
                                    record_time = UtcNow()
                                };
                                QueueRecordForProcessing(record);
                                var changeMessage = aPlayer.player_name + " changed their tag from " + (String.IsNullOrEmpty(oldTag) ? "NOTHING" : "[" + oldTag + "]") + " to " + (String.IsNullOrEmpty(aPlayer.player_clanTag) ? "NOTHING" : "[" + aPlayer.player_clanTag + "]") + ".";
                                Log.Debug(() => changeMessage + " Updating the database.", 2);
                                if (_ShowPlayerNameChangeAnnouncement && !String.IsNullOrEmpty(oldTag))
                                {
                                    OnlineAdminSayMessage(changeMessage);
                                }
                                UpdatePlayer(aPlayer);
                            }
                        }
                        else
                        {
                            Log.Debug(() => "No inbound players. Waiting.", 6);
                            //Wait for new actions
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _BattlelogCommWaitHandle.Reset();
                            _BattlelogCommWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                            loopStart = UtcNow();
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("Battlelog comm thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in Battlelog comm thread. Skipping current loop.", e));
                    }
                }
                Log.Debug(() => "Ending Battlelog Comm Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in battlelog comm thread.", e));
            }
        }

        private void QueueRecordForActionHandling(ARecord record)
        {
            Log.Debug(() => "Entering queueRecordForActionHandling", 6);
            try
            {
                Log.Debug(() => "Preparing to queue record for action handling", 6);
                lock (_UnprocessedActionQueue)
                {
                    _UnprocessedActionQueue.Enqueue(record);
                    Log.Debug(() => "Record queued for action handling", 6);
                    _ActionHandlingWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while queuing record for action handling.", e);
                Log.HandleException(record.record_exception);
            }
            Log.Debug(() => "Exiting queueRecordForActionHandling", 6);
        }
    }
}
