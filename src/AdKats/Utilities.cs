using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Flurl;
using Flurl.Http;

using MySqlConnector;
using Newtonsoft.Json;

using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        private static String GetRandom32BitHashCode()
        {
            var bytes = new byte[4];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0).ToString("X8");
        }

        private void FillCommandDescDictionary()
        {
            _CommandDescriptionDictionary.Clear();
            foreach (var kvp in _CommandNameDictionary)
            {
                _CommandDescriptionDictionary[kvp.Key] = kvp.Value?.command_name ?? kvp.Key;
            }
        }

        private void FillReadableMapModeDictionaries()
        {
            // Populated from game server map list on connect
            // Maps internal names (e.g. "MP_Prison") to display names (e.g. "Operation Locker")
        }

        private Boolean PopulateCommandReputationDictionaries()
        {
            // Populated from AdKats database — requires active DB connection
            return true;
        }

        private Boolean PopulateSpecialGroupDictionaries()
        {
            // Populated from AdKats database — requires active DB connection
            _specialPlayerGroupIDDictionary.Clear();
            _specialPlayerGroupKeyDictionary.Clear();
            return true;
        }

        private Boolean TestGlobalTiming(Boolean verbose, Boolean testOnly, out TimeSpan diffUTCGlobal)
        {
            // Tests clock synchronization between server and database
            diffUTCGlobal = TimeSpan.Zero;
            return true;
        }

        // ===========================================================================================
        // Utilities and Logger
        // ===========================================================================================

        public class Utilities
        {
            private Logger Log;

            public Utilities(Logger log)
            {
                Log = log;
            }

            public String HttpDownload(String url)
            {
                Log.Debug(() => "Preparing to download from " + GetDomainName(url), 7);
                Stopwatch timer = new Stopwatch();
                timer.Start();
                String result = url.GetStringAsync().Result;
                timer.Stop();
                Log.Debug(() => "Downloaded from " + GetDomainName(url) + " in " + timer.ElapsedMilliseconds + "ms", 7);
                return result;
            }

            public String HttpUpload(String url, String data)
            {
                Log.Debug(() => "Preparing to upload to " + GetDomainName(url), 7);
                Stopwatch timer = new Stopwatch();
                timer.Start();
                String result = url.PostStringAsync(data).ReceiveString().Result;
                timer.Stop();
                Log.Debug(() => "Uploaded to " + GetDomainName(url) + " in " + timer.ElapsedMilliseconds + "ms", 7);
                return result;
            }

            public string GetDomainName(string url)
            {
                string domain = new Uri(url).DnsSafeHost.ToLower();
                var tokens = domain.Split('.');
                if (tokens.Length > 2)
                {
                    //Add only second level exceptions to the < 3 rule here
                    string[] exceptions = { "info", "firm", "name", "com", "biz", "gen", "ltd", "web", "net", "pro", "org" };
                    var validTokens = 2 + ((tokens[tokens.Length - 2].Length < 3 || exceptions.Contains(tokens[tokens.Length - 2])) ? 1 : 0);
                    domain = string.Join(".", tokens, tokens.Length - validTokens, validTokens);
                }
                return domain;
            }

            //Credit to Imisnew2, grabbed from TS3Sync
            public Double PercentMatch(String s, String t)
            {
                if (String.IsNullOrEmpty(s) || String.IsNullOrEmpty(t))
                {
                    return 0.0;
                }
                Double max;
                Double min;
                Int32 distance;
                if (s.Length >= t.Length)
                {
                    max = s.Length;
                    min = t.Length;
                    distance = LevenshteinDistance(s, t);
                }
                else
                {
                    max = t.Length;
                    min = s.Length;
                    distance = LevenshteinDistance(t, s);
                }
                double percent = (max - distance) / max;
                double maxPossMatch = min / max;
                return (percent / maxPossMatch) * 100;
            }

            //Credit to Micovery and PapaCharlie9 for modified Levenshtein Distance algorithm
            public Int32 LevenshteinDistance(String s, String t)
            {
                s = s.ToLower();
                t = t.ToLower();
                Int32 n = s.Length;
                Int32 m = t.Length;
                int[,] d = new Int32[n + 1, m + 1];
                if (n == 0)
                {
                    return m;
                }
                if (m == 0)
                {
                    return n;
                }
                for (Int32 i = 0; i <= n; d[i, 0] = i++)
                {
                    ;
                }
                for (Int32 j = 0; j <= m; d[0, j] = j++)
                {
                    ;
                }
                for (Int32 i = 1; i <= n; i++)
                {
                    for (Int32 j = 1; j <= m; j++)
                    {
                        d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 0), d[i - 1, j - 1] + ((t[j - 1] == s[i - 1]) ? 0 : 1));
                    }
                }
                return d[n, m];
            }

            //parses single word or number parameters out of a String until param count is reached
            public String[] ParseParameters(String message, Int32 maxParamCount)
            {
                //create list for parameters
                List<string> parameters = new List<String>();
                if (message.Length > 0)
                {
                    //Add all single word/number parameters
                    String[] paramSplit = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    Int32 maxLoop = (paramSplit.Length < maxParamCount) ? (paramSplit.Length) : (maxParamCount);
                    for (Int32 i = 0; i < maxLoop - 1; i++)
                    {
                        Log.Debug(() => "Param " + i + ": " + paramSplit[i], 6);
                        parameters.Add(paramSplit[i]);
                        Int32 paramIdx = message.IndexOf(paramSplit[i]);
                        if (paramIdx >= 0)
                        {
                            message = message.Substring(paramIdx + paramSplit[i].Length).Trim();
                        }
                    }
                    //Add final multi-word parameter
                    parameters.Add(message);
                }
                Log.Debug(() => "Num params: " + parameters.Count, 6);
                return parameters.ToArray();
            }
        }

        public String GenerateKickReason(ARecord record)
        {
            String sourceNameString = "[" + record.source_name + "]";

            //Create the full message
            String fullMessage = record.record_message + " " + sourceNameString;

            //Trim the kick message if necessary
            Int32 cutLength = fullMessage.Length - 80;
            if (cutLength > 0)
            {
                String cutReason = record.record_message.Substring(0, record.record_message.Length - cutLength);
                fullMessage = cutReason + " " + sourceNameString;
            }
            return fullMessage;
        }

        public String GenerateBanReason(ABan aBan)
        {
            String banDurationString;
            //If ban time > 1000 days just say perm
            TimeSpan remainingTime = GetRemainingBanTime(aBan);
            if (remainingTime.TotalDays > 1000)
            {
                banDurationString = "[perm]";
            }
            else
            {
                banDurationString = "[" + FormatTimeString(remainingTime, 2) + "]";
            }
            String sourceNameString = "[" + aBan.ban_record.source_name + "]";
            String banAppendString = ((_UseBanAppend) ? ("[" + _BanAppend + "]") : (""));

            //Create the full message
            String fullMessage = aBan.ban_record.record_message + " " + banDurationString + sourceNameString + banAppendString;

            //Trim the kick message if necessary
            Int32 cutLength = fullMessage.Length - 80;
            if (cutLength > 0)
            {
                String cutReason = aBan.ban_record.record_message.Substring(0, aBan.ban_record.record_message.Length - cutLength);
                fullMessage = cutReason + " " + banDurationString + sourceNameString + banAppendString;
            }
            return fullMessage;
        }

        public void UpdateOtherPlugins(String dllPath)
        {

            //Other plugins
            //1 - MULTIBalancer - With ColColonCleaner balance mods
            if (false && _UseExperimentalTools && GameVersion == GameVersionEnum.BF4)
            {
                String externalPluginSource;
                try
                {
                    externalPluginSource = Util.HttpDownload("https://raw.githubusercontent.com/ColColonCleaner/multi-balancer/master/MULTIbalancer.cs" + "?cacherand=" + Environment.TickCount);
                }
                catch (Exception)
                {
                    if (_pluginUpdateCaller != null)
                    {
                        SendMessageToSource(_pluginUpdateCaller, "Unable to install/update MULTIBalancer.");
                    }
                    Log.Error("Unable to install/update MULTIBalancer.");
                    _pluginUpdateCaller = null;
                    Threading.StopWatchdog();
                    return;
                }
                if (String.IsNullOrEmpty(externalPluginSource))
                {
                    if (_pluginUpdateCaller != null)
                    {
                        SendMessageToSource(_pluginUpdateCaller, "Downloaded MULTIBalancer source was empty. Unable to install/update MULTIBalancer.");
                    }
                    Log.Error("Downloaded MULTIBalancer source was empty. Unable to install/update MULTIBalancer.");
                    _pluginUpdateCaller = null;
                    Threading.StopWatchdog();
                    return;
                }
                String externalPluginFileName = "MULTIbalancer.cs";
                String externalPluginPath = Path.Combine(dllPath.Trim(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }), externalPluginFileName);
                // Compile check removed — Procon v2 compiles plugins via Roslyn on load
                Int64 patchedPluginSizeKb = 0;
                Boolean externalPluginFileWriteFailed = false;
                Int32 externalPluginWriteAttempts = 0;
                do
                {
                    using (FileStream stream = File.Open(externalPluginPath, FileMode.Create))
                    {
                        if (!stream.CanWrite)
                        {
                            if (_pluginUpdateCaller != null)
                            {
                                SendMessageToSource(_pluginUpdateCaller, "Cannot write updates to MULTIBalancer source file. Unable to install/update MULTIBalancer.");
                            }
                            Log.Error("Cannot write updates to MULTIBalancer source file. Unable to install/update MULTIBalancer.");
                            _pluginUpdateCaller = null;
                            Threading.StopWatchdog();
                            return;
                        }
                        Byte[] info = new UTF8Encoding(true).GetBytes(externalPluginSource);
                        stream.Write(info, 0, info.Length);
                    }
                    patchedPluginSizeKb = new FileInfo(externalPluginPath).Length / 1024;
                    //There is no way the valid plugin can be less than 1 Kb
                    if (patchedPluginSizeKb < 1)
                    {
                        if (_pluginUpdateCaller != null)
                        {
                            SendMessageToSource(_pluginUpdateCaller, "Write failure on MULTIBalancer update. Attempting write again.");
                        }
                        Log.Error("Write failure on MULTIBalancer update. Attempting write again.");
                        externalPluginFileWriteFailed = true;
                    }
                    else
                    {
                        externalPluginFileWriteFailed = false;
                    }
                    if (++externalPluginWriteAttempts > 5)
                    {
                        if (_pluginUpdateCaller != null)
                        {
                            SendMessageToSource(_pluginUpdateCaller, "Constant failure to write MULTIBalancer update to file. Unable to install/update MULTIBalancer.");
                        }
                        Log.Error("Constant failure to write MULTIBalancer update to file. Unable to install/update MULTIBalancer.");
                        _pluginUpdateCaller = null;
                        Threading.StopWatchdog();
                        return;
                    }
                } while (externalPluginFileWriteFailed);
                if (_pluginUpdateCaller != null)
                {
                    SendMessageToSource(_pluginUpdateCaller, "MULTIBalancer installed/updated. Plugin size " + patchedPluginSizeKb + "KB");
                }
                Log.Success("MULTIBalancer installed/updated. Plugin size " + patchedPluginSizeKb + "KB");
            }
        }

        public void UpdateExtensions(String dllPath)
        {

            //Extensions
            //1 - AdKatsLRT - Private Extension - Token Required
            if (!String.IsNullOrEmpty(_AdKatsLRTExtensionToken))
            {
                String extensionSource;
                try
                {
                    extensionSource = Util.HttpDownload("https://raw.githubusercontent.com/AdKats/AdKats-LRT/master/AdKatsLRT.cs?token=" + _AdKatsLRTExtensionToken + "&cacherand=" + Environment.TickCount);
                }
                catch (Exception)
                {
                    if (_pluginUpdateCaller != null)
                    {
                        SendMessageToSource(_pluginUpdateCaller, "Unable to install/update AdKatsLRT Extension. Connection error, or invalid token.");
                    }
                    Log.Error("Unable to install/update AdKatsLRT Extension. Connection error, or invalid token.");
                    _pluginUpdateCaller = null;
                    Threading.StopWatchdog();
                    return;
                }
                if (String.IsNullOrEmpty(extensionSource))
                {
                    if (_pluginUpdateCaller != null)
                    {
                        SendMessageToSource(_pluginUpdateCaller, "Downloaded AdKatsLRT Extension source was empty. Unable to install/update AdKatsLRT Extension.");
                    }
                    Log.Error("Downloaded AdKatsLRT Extension source was empty. Unable to install/update AdKatsLRT Extension.");
                    _pluginUpdateCaller = null;
                    Threading.StopWatchdog();
                    return;
                }
                String extensionFileName = "AdKatsLRT.cs";
                String extensionPath = Path.Combine(dllPath.Trim(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }), extensionFileName);
                // Compile check removed — Procon v2 compiles plugins via Roslyn on load
                Int64 patchedExtensionSizeKb = 0;
                Boolean extensionFileWriteFailed = false;
                Int32 extensionWriteAttempts = 0;
                do
                {
                    using (FileStream stream = File.Open(extensionPath, FileMode.Create))
                    {
                        if (!stream.CanWrite)
                        {
                            if (_pluginUpdateCaller != null)
                            {
                                SendMessageToSource(_pluginUpdateCaller, "Cannot write updates to AdKatsLRT Extension source file. Unable to install/update AdKatsLRT Extension.");
                            }
                            Log.Error("Cannot write updates to AdKatsLRT Extension source file. Unable to install/update AdKatsLRT Extension.");
                            _pluginUpdateCaller = null;
                            Threading.StopWatchdog();
                            return;
                        }
                        Byte[] info = new UTF8Encoding(true).GetBytes(extensionSource);
                        stream.Write(info, 0, info.Length);
                    }
                    patchedExtensionSizeKb = new FileInfo(extensionPath).Length / 1024;
                    //There is no way the valid extension can be less than 1 Kb
                    if (patchedExtensionSizeKb < 1)
                    {
                        if (_pluginUpdateCaller != null)
                        {
                            SendMessageToSource(_pluginUpdateCaller, "Write failure on AdKatsLRT Extension update. Attempting write again.");
                        }
                        Log.Error("Write failure on AdKatsLRT Extension update. Attempting write again.");
                        extensionFileWriteFailed = true;
                    }
                    else
                    {
                        extensionFileWriteFailed = false;
                    }
                    if (++extensionWriteAttempts > 5)
                    {
                        if (_pluginUpdateCaller != null)
                        {
                            SendMessageToSource(_pluginUpdateCaller, "Constant failure to write AdKatsLRT Extension update to file. Unable to install/update AdKatsLRT Extension.");
                        }
                        Log.Error("Constant failure to write AdKatsLRT Extension update to file. Unable to install/update AdKatsLRT Extension.");
                        _pluginUpdateCaller = null;
                        Threading.StopWatchdog();
                        return;
                    }
                } while (extensionFileWriteFailed);
                if (_pluginUpdateCaller != null)
                {
                    SendMessageToSource(_pluginUpdateCaller, "AdKatsLRT Extension installed/updated. Extension size " + patchedExtensionSizeKb + "KB");
                }
                Log.Success("AdKatsLRT Extension installed/updated. Extension size " + patchedExtensionSizeKb + "KB");
            }
        }

        public void CheckForPluginUpdates(Boolean manual)
        {
            try
            {
                if ((_pluginVersionStatus == VersionStatus.OutdatedBuild && !_automaticUpdatesDisabled && !_pluginUpdatePatched) ||
                    _pluginVersionStatus == VersionStatus.TestBuild ||
                    (!String.IsNullOrEmpty(_AdKatsLRTExtensionToken)) ||
                    manual)
                {
                    if (Threading.IsAlive("PluginUpdater"))
                    {
                        if (_pluginUpdateCaller != null)
                        {
                            SendMessageToSource(_pluginUpdateCaller, "Update already in progress.");
                        }
                        _pluginUpdateCaller = null;
                        Threading.StopWatchdog();
                        return;
                    }
                    Thread pluginUpdater = new Thread(new ThreadStart(delegate
                    {
                        try
                        {
                            Thread.CurrentThread.Name = "PluginUpdater";
                            _pluginUpdateProgress = "Started";

                            String dllPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;

                            //Other plugins
                            UpdateOtherPlugins(dllPath);

                            //AdKats Extensions
                            UpdateExtensions(dllPath);

                            if (_pluginUpdateCaller != null)
                            {
                                SendMessageToSource(_pluginUpdateCaller, "Preparing to download plugin update.");
                            }
                            if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                            {
                                Log.Info("Preparing to download plugin update to version " + _latestPluginVersion);
                            }
                            String pluginSource = null;
                            try
                            {
                                string stableURL = "https://raw.githubusercontent.com/Hedius/E4GLAdKats/main/AdKats.cs" + "?cacherand=" + Environment.TickCount;
                                string testURL = "https://raw.githubusercontent.com/Hedius/E4GLAdKats/test/AdKats.cs" + "?cacherand=" + Environment.TickCount;
                                if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                {
                                    pluginSource = Util.HttpDownload(stableURL);
                                }
                                else
                                {
                                    pluginSource = Util.HttpDownload(testURL);
                                }
                            }
                            catch (Exception)
                            {
                                try
                                {
                                    string stableURL = "https://adkats.e4gl.com/AdKats.cs" + "?cacherand=" + Environment.TickCount;
                                    string testURL = "https://adkats.e4gl.com/test/AdKats.cs" + "?cacherand=" + Environment.TickCount;
                                    if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                    {
                                        pluginSource = Util.HttpDownload(stableURL);
                                    }
                                    else
                                    {
                                        pluginSource = Util.HttpDownload(testURL);
                                    }
                                }
                                catch (Exception)
                                {
                                    if (_pluginUpdateCaller != null)
                                    {
                                        SendMessageToSource(_pluginUpdateCaller, "Unable to download plugin update.");
                                    }
                                    if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                    {
                                        Log.Error("Unable to download plugin update to version " + _latestPluginVersion);
                                    }
                                    _pluginUpdateCaller = null;
                                    Threading.StopWatchdog();
                                    return;
                                }
                            }
                            if (String.IsNullOrEmpty(pluginSource))
                            {
                                if (_pluginUpdateCaller != null)
                                {
                                    SendMessageToSource(_pluginUpdateCaller, "Downloaded plugin source was empty. Cannot update.");
                                }
                                if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                {
                                    Log.Error("Downloaded plugin source was empty. Cannot update to version " + _latestPluginVersion);
                                }
                                _pluginUpdateCaller = null;
                                Threading.StopWatchdog();
                                return;
                            }
                            _pluginUpdateProgress = "Downloaded";
                            if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                            {
                                Log.Success("Updated plugin source downloaded.");
                                Log.Info("Preparing test compile on updated plugin source.");
                            }
                            String pluginFileName = "AdKats.cs";
                            String pluginPath = null;
                            if (Environment.OSVersion.Platform.ToString().ToLower().StartsWith("unix"))
                            {
                                pluginPath = Path.Combine(dllPath, pluginFileName);
                            }
                            else
                            {
                                pluginPath = Path.Combine(dllPath.Trim(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }), pluginFileName);
                            }
                            // Compile check removed — Procon v2 compiles plugins via Roslyn on load
                            if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                            {
                                Log.Success("Plugin update downloaded successfully.");
                            }
                            _pluginUpdateProgress = "Downloaded";
                            if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                            {
                                Log.Info("Preparing to update source file on disk.");
                            }
                            Int64 originalSizeKb = new FileInfo(pluginPath).Length / 1024;
                            Int64 patchedSizeKB = 0;
                            Boolean fileWriteFailed = false;
                            Int32 attempts = 0;
                            do
                            {
                                using (FileStream stream = File.Open(pluginPath, FileMode.Create))
                                {
                                    if (!stream.CanWrite)
                                    {
                                        if (_pluginUpdateCaller != null)
                                        {
                                            SendMessageToSource(_pluginUpdateCaller, "Cannot write updates to source file. Cannot update.");
                                        }
                                        Log.Error("Cannot write updates to source file. Cannot update.");
                                        _pluginUpdateCaller = null;
                                        Threading.StopWatchdog();
                                        return;
                                    }
                                    Byte[] info = new UTF8Encoding(true).GetBytes(pluginSource);
                                    stream.Write(info, 0, info.Length);
                                }
                                patchedSizeKB = new FileInfo(pluginPath).Length / 1024;
                                //There is no way the valid plugin can be less than 1 Kb
                                if (patchedSizeKB < 1)
                                {
                                    if (_pluginUpdateCaller != null)
                                    {
                                        SendMessageToSource(_pluginUpdateCaller, "Write failure on plugin update. Attempting write again.");
                                    }
                                    Log.Error("Write failure on plugin update. Attempting write again.");
                                    Thread.Sleep(500);
                                    fileWriteFailed = true;
                                }
                                else
                                {
                                    fileWriteFailed = false;
                                }
                                if (++attempts > 5)
                                {
                                    if (_pluginUpdateCaller != null)
                                    {
                                        SendMessageToSource(_pluginUpdateCaller, "Constant failure to write plugin update to file. Cannot update.");
                                    }
                                    Log.Error("Constant failure to write plugin update to file. Cannot update.");
                                    _pluginUpdateCaller = null;
                                    Threading.StopWatchdog();
                                    return;
                                }
                            } while (fileWriteFailed);
                            String patchedVersion = ExtractString(pluginSource, "version_code");
                            if (!String.IsNullOrEmpty(patchedVersion))
                            {
                                Int64 patchedVersionInt = ConvertVersionInt(patchedVersion);
                                if (patchedVersionInt >= _currentPluginVersionInt)
                                {
                                    //Patched version is newer than current version
                                    if (patchedVersionInt > _pluginPatchedVersionInt && _pluginUpdatePatched)
                                    {
                                        if (_pluginUpdateCaller != null)
                                        {
                                            SendMessageToSource(_pluginUpdateCaller, "Previous update " + _pluginPatchedVersion + " overwritten by newer patch " + patchedVersion + ", restart procon to run this version. Plugin size " + patchedSizeKB + "KB");
                                        }
                                        //Patched version is newer than an already patched version
                                        Log.Success("Previous update " + _pluginPatchedVersion + " overwritten by newer patch " + patchedVersion + ", restart procon to run this version. Plugin size " + patchedSizeKB + "KB");
                                        if (_UseExperimentalTools && !EventActive())
                                        {
                                            if (NowDuration(_proconStartTime).TotalMinutes < 3)
                                            {
                                                Threading.Wait(1000);
                                                Environment.Exit(2232);
                                            }
                                            else
                                            {
                                                // Tell the layer to reboot at round end
                                                Log.Warn("Procon will be shut down on the next level load.");
                                                _LevelLoadShutdown = true;
                                            }
                                        }
                                    }
                                    else if (!_pluginUpdatePatched && patchedVersionInt > _currentPluginVersionInt)
                                    {
                                        if (_pluginUpdateCaller != null)
                                        {
                                            SendMessageToSource(_pluginUpdateCaller, "Plugin updated to version " + patchedVersion + ", restart procon to run this version. Plugin size " + patchedSizeKB + "KB");
                                        }
                                        //User not notified of patch yet
                                        Log.Success("Plugin updated to version " + patchedVersion + ", restart procon to run this version. Plugin size " + patchedSizeKB + "KB");
                                        Log.Success("Updated plugin file located at: " + pluginPath);
                                        if (_UseExperimentalTools && !EventActive())
                                        {
                                            if (NowDuration(_proconStartTime).TotalMinutes < 3)
                                            {
                                                Threading.Wait(1000);
                                                Environment.Exit(2232);
                                            }
                                            else
                                            {
                                                // Tell the layer to reboot at round end
                                                Log.Warn("Procon will be shut down on the next level load.");
                                                _LevelLoadShutdown = true;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (_pluginUpdateCaller != null)
                                        {
                                            SendMessageToSource(_pluginUpdateCaller, "Plugin updated to same version, " + patchedVersion + ". Plugin size " + patchedSizeKB + "KB");
                                        }
                                    }
                                }
                                else if (!_pluginUpdatePatched)
                                {
                                    if (_pluginUpdateCaller != null)
                                    {
                                        SendMessageToSource(_pluginUpdateCaller, "Plugin reverted to previous version " + patchedVersion + ", restart procon to run this version. Plugin size " + patchedSizeKB + "KB");
                                    }
                                    //Patched version is older than current version
                                    Log.Warn("Plugin reverted to previous version " + patchedVersion + ", restart procon to run this version. Plugin size " + patchedSizeKB + "KB");
                                }
                                _pluginPatchedVersion = patchedVersion;
                                _pluginPatchedVersionInt = patchedVersionInt;
                            }
                            else
                            {
                                if (_pluginUpdateCaller != null)
                                {
                                    SendMessageToSource(_pluginUpdateCaller, "Plugin update patched, but its version could not be extracted. Plugin size " + patchedSizeKB + "KB");
                                }
                                Log.Warn("Plugin update patched, but its version could not be extracted. Plugin size " + patchedSizeKB + "KB");
                            }
                            _pluginUpdateProgress = "Patched";
                            _pluginUpdatePatched = true;
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error while running update thread.", e));
                        }
                        _pluginUpdateCaller = null;
                        Threading.StopWatchdog();
                    }));
                    Threading.StartWatchdog(pluginUpdater);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while updating plugin source to latest version", e));
            }
        }

        public void ProconChatWrite(String msg)
        {
            msg = msg.Replace(Environment.NewLine, " ");
            ExecuteCommand("procon.protected.chat.write", Log.COrange("AdKats") + " > " + msg);
        }

        public void PrintPreparedCommand(MySqlCommand cmd)
        {
            String query = cmd.Parameters.Cast<MySqlParameter>()
                .Aggregate(cmd.CommandText, (current, p) =>
                    current.Replace(p.ParameterName, (p.Value != null) ? ("\"" + p.Value.ToString() + "\"") : ("NULL")));
            Log.Write(query);
        }

        public static DateTime GetEpochTime()
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        public static DateTime GetLocalEpochTime()
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
        }

        public DateTime DateTimeFromEpochSeconds(Double epochSeconds)
        {
            return GetEpochTime().AddSeconds(epochSeconds);
        }

        public class Logger
        {
            private readonly AdKats _plugin;
            public Int32 DebugLevel
            {
                get; set;
            }
            public Boolean VerboseErrors
            {
                get; set;
            }

            public Logger(AdKats plugin)
            {
                _plugin = plugin;
            }

            private void WriteConsole(String msg)
            {
                _plugin.ExecuteCommand("procon.protected.pluginconsole.write", "[^b" + _plugin.GetType().Name + "^n] " + msg);
            }

            private void WriteChat(String msg)
            {
                _plugin.ExecuteCommand("procon.protected.chat.write", COrange(_plugin.GetType().Name) + " > " + msg);
            }

            public void Debug(Func<String> messageFunc, Int32 level)
            {
                try
                {
                    if (DebugLevel >= level)
                    {
                        if (DebugLevel >= 8)
                        {
                            WriteConsole("[" + level + "-" + new StackFrame(1).GetMethod().Name + "-" + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Thread.CurrentThread.Name)) + "-" + Thread.CurrentThread.ManagedThreadId + "] " + messageFunc());
                        }
                        else
                        {
                            WriteConsole(messageFunc());
                        }
                    }
                }
                catch (Exception e)
                {
                    WriteConsole("Error writing debug message. " + e.ToString());
                }
            }

            public void Write(String msg)
            {
                WriteConsole(msg);
            }

            public void Info(String msg)
            {
                WriteConsole("^b^0INFO^n^0: " + msg);
            }

            public void Warn(String msg)
            {
                WriteConsole("^b^3WARNING^n^0: " + msg);
            }

            public void Error(String msg)
            {
                if (VerboseErrors)
                {
                    //Opening
                    WriteConsole("^b^1ERROR-" +//Plugin version
                                 Int32.Parse(_plugin.GetPluginVersion().Replace(".", "")) + "-" +//Method name
                                 new StackFrame(1).GetMethod().Name + "-" +//Thread
                                 ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Thread.CurrentThread.Name)) + Thread.CurrentThread.ManagedThreadId +//Closing
                                 "^n^0: " +//Error Message
                                 "[" + msg + "]");
                }
                else
                {
                    //Opening
                    WriteConsole("^b^1ERROR-" +//Plugin version
                                 Int32.Parse(_plugin.GetPluginVersion().Replace(".", "")) +//Closing
                                 "^n^0: " +//Error Message
                                 "[" + msg + "]");
                }
            }

            public void Success(String msg)
            {
                WriteConsole("^b^2SUCCESS^n^0: " + msg);
            }

            public String Exception(String msg, Exception e, Int32 level)
            {
                //Opening
                string exceptionMessage = "^b^8EXCEPTION-" +//Plugin version
                                          Int32.Parse(_plugin.GetPluginVersion().Replace(".", ""));
                if (_plugin._firstPlayerListComplete)
                {
                    exceptionMessage += "-A" + Math.Round(_plugin.NowDuration(_plugin._AdKatsRunningTime).TotalHours, 2);
                }
                else
                {
                    exceptionMessage += "-P" + Math.Round(_plugin.NowDuration(_plugin._proconStartTime).TotalHours, 2);
                }
                if (e != null)
                {
                    exceptionMessage += "-";
                    Int64 impericalLineNumber = 0;
                    Int64 parsedLineNumber = 0;
                    StackTrace stack = new StackTrace(e, true);
                    if (stack.FrameCount > 0)
                    {
                        impericalLineNumber = stack.GetFrame(0).GetFileLineNumber();
                    }
                    Int64.TryParse(e.ToString().Split(' ').Last(), out parsedLineNumber);
                    if (impericalLineNumber != 0)
                    {
                        exceptionMessage += impericalLineNumber;
                    }
                    else if (parsedLineNumber != 0)
                    {
                        exceptionMessage += parsedLineNumber;
                    }
                    else
                    {
                        exceptionMessage += "D";
                    }
                }
                exceptionMessage += "-" +//Method name
                                    new StackFrame(level + 1).GetMethod().Name + "-" +//Thread
                                    ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Thread.CurrentThread.Name)) + Thread.CurrentThread.ManagedThreadId +//Closing
                                    "^n^0: " +//Message
                                    "[" + msg + "]" +//Exception string
                                    ((e != null) ? ("[" + e + "]") : (""));
                WriteConsole(exceptionMessage);
                return exceptionMessage;
            }

            public AException HandleException(AException aException)
            {
                //If it's null or AdKats isn't enabled, just return
                if (aException == null)
                {
                    Error("Attempted to handle exception when none was given.");
                    return null;
                }
                if (!_plugin._pluginEnabled)
                {
                    return aException;
                }
                //Check if the exception attributes to the database
                if (aException.InternalException != null &&
                    (aException.InternalException is TimeoutException ||
                    aException.InternalException.ToString().Contains("Unable to connect to any of the specified MySQL hosts") ||
                    aException.InternalException.ToString().Contains("Reading from the stream has failed.") ||
                    aException.InternalException.ToString().Contains("Too many connections") ||
                    aException.InternalException.ToString().Contains("Timeout expired") ||
                    aException.InternalException.ToString().Contains("An existing connection was forcibly closed by the remote host") ||
                    aException.InternalException.ToString().Contains("Unable to read data") ||
                    aException.InternalException.ToString().Contains("Lock wait timeout exceeded")))
                {
                    _plugin.HandleDatabaseConnectionInteruption();
                }
                else if (aException.InternalException != null &&
                        aException.InternalException is MySqlException &&
                        (((MySqlException)aException.InternalException).Number == 1205 ||
                        ((MySqlException)aException.InternalException).Number == 1213))
                {
                    //Deadlock related. Do nothing.
                }
                else
                {
                    var exceptionString = Exception(aException.Message, aException.InternalException, 1);
                    //Create the Exception record
                    ARecord record = new ARecord
                    {
                        record_source = ARecord.Sources.Automated,
                        isDebug = true,
                        server_id = _plugin._serverInfo.ServerID,
                        command_type = _plugin.GetCommandByKey("adkats_exception"),
                        command_numeric = Int32.Parse(_plugin.GetPluginVersion().Replace(".", "")),
                        target_name = "AdKats",
                        target_player = null,
                        source_name = "AdKats",
                        record_message = FClear(exceptionString),
                        record_time = _plugin.UtcNow()
                    };
                    //Process the record
                    _plugin.QueueRecordForProcessing(record);
                }
                return aException;
            }

            public void Chat(String msg)
            {
                msg = msg.Replace(Environment.NewLine, " ");
                WriteChat(msg);
            }

            public String FClear(String msg)
            {
                return msg.Replace("^b", "")
                          .Replace("^n", "")
                          .Replace("^i", "")
                          .Replace("^0", "")
                          .Replace("^1", "")
                          .Replace("^2", "")
                          .Replace("^3", "")
                          .Replace("^4", "")
                          .Replace("^5", "")
                          .Replace("^6", "")
                          .Replace("^7", "")
                          .Replace("^8", "")
                          .Replace("^9", "");
            }

            public String FBold(String msg)
            {
                return "^b" + msg + "^n";
            }

            public String FItalic(String msg)
            {
                return "^i" + msg + "^n";
            }

            public String CMaroon(String msg)
            {
                return "^1" + msg + "^0";
            }

            public String CGreen(String msg)
            {
                return "^2" + msg + "^0";
            }

            public String COrange(String msg)
            {
                return "^3" + msg + "^0";
            }

            public String CBlue(String msg)
            {
                return "^4" + msg + "^0";
            }

            public String CBlueLight(String msg)
            {
                return "^5" + msg + "^0";
            }

            public String CViolet(String msg)
            {
                return "^6" + msg + "^0";
            }

            public String CPink(String msg)
            {
                return "^7" + msg + "^0";
            }

            public String CRed(String msg)
            {
                return "^8" + msg + "^0";
            }

            public String CGrey(String msg)
            {
                return "^9" + msg + "^0";
            }
        }

        public MySqlDataReader SafeExecuteReader(MySqlCommand command)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            try
            {
                MySqlDataReader reader = command.ExecuteReader();
                watch.Stop();
                if (watch.Elapsed.TotalSeconds > 10 && watch.Elapsed.TotalSeconds > (50 * _DatabaseReadAverageDuration) && _firstPlayerListComplete)
                {
                    HandleDatabaseConnectionInteruption();
                }
                if (_DatabaseReaderDurations.Count < 25000)
                {
                    lock (_DatabaseReaderDurations)
                    {
                        _DatabaseReaderDurations.Add(watch.Elapsed.TotalSeconds);
                        _DatabaseReadAverageDuration = _DatabaseReaderDurations.Average();
                    }
                }
                return reader;
            }
            catch (Exception e)
            {
                try
                {
                    //If the failure was due to deadlock, wait a short duration and issue again
                    if (e.ToString().ToLower().Contains("deadlock"))
                    {
                        Thread.Sleep(250);
                        //If any further errors thrown, just throw them
                        watch.Reset();
                        watch.Start();
                        MySqlDataReader reader = command.ExecuteReader();
                        watch.Stop();
                        if (watch.Elapsed.TotalSeconds > 10 && watch.Elapsed.TotalSeconds > (50 * _DatabaseReadAverageDuration) && _firstPlayerListComplete)
                        {
                            HandleDatabaseConnectionInteruption();
                        }
                        if (_DatabaseReaderDurations.Count < 25000)
                        {
                            lock (_DatabaseReaderDurations)
                            {
                                _DatabaseReaderDurations.Add(watch.Elapsed.TotalSeconds);
                                _DatabaseReadAverageDuration = _DatabaseReaderDurations.Average();
                            }
                        }
                        return reader;
                    }
                    throw e;
                }
                catch (Exception e2)
                {
                    e = e2;
                    if (e2.GetType() == typeof(TimeoutException) ||
                        e2.ToString().Contains("Unable to connect to any of the specified MySQL hosts") ||
                        e2.ToString().Contains("Reading from the stream has failed.") ||
                        e2.ToString().Contains("Too many connections") ||
                        e2.ToString().Contains("Timeout expired") ||
                        e2.ToString().Contains("An existing connection was forcibly closed by the remote host") ||
                        e2.ToString().Contains("Unable to read data") ||
                        e2.ToString().Contains("Lock wait timeout exceeded"))
                    {
                        Log.Info("Average Read: " + Math.Round(_DatabaseReadAverageDuration, 3) + "s " + _DatabaseReaderDurations.Count + " | Average Write: " + Math.Round(_DatabaseWriteAverageDuration, 3) + "s " + _DatabaseNonQueryDurations.Count);
                        PrintPreparedCommand(command);
                    }
                }
                throw e;
            }
        }

        public Int32 SafeExecuteNonQuery(MySqlCommand command)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            try
            {
                int modified = command.ExecuteNonQuery();
                watch.Stop();
                if (watch.Elapsed.TotalSeconds > 10 && watch.Elapsed.TotalSeconds > (50 * _DatabaseWriteAverageDuration) && _firstPlayerListComplete)
                {
                    HandleDatabaseConnectionInteruption();
                }
                if (_DatabaseNonQueryDurations.Count < 25000)
                {
                    lock (_DatabaseNonQueryDurations)
                    {
                        _DatabaseNonQueryDurations.Add(watch.Elapsed.TotalSeconds);
                        _DatabaseWriteAverageDuration = _DatabaseNonQueryDurations.Average();
                    }
                }
                return modified;
            }
            catch (Exception e)
            {
                try
                {
                    //If the failure was due to deadlock, wait a short duration and issue again
                    if (e.ToString().ToLower().Contains("deadlock"))
                    {
                        Thread.Sleep(250);
                        //If any further errors thrown, just throw them
                        watch.Reset();
                        watch.Start();
                        int modified = command.ExecuteNonQuery();
                        watch.Stop();
                        if (watch.Elapsed.TotalSeconds > 10 && watch.Elapsed.TotalSeconds > (50 * _DatabaseWriteAverageDuration) && _firstPlayerListComplete)
                        {
                            HandleDatabaseConnectionInteruption();
                        }
                        if (_DatabaseNonQueryDurations.Count < 25000)
                        {
                            lock (_DatabaseNonQueryDurations)
                            {
                                _DatabaseNonQueryDurations.Add(watch.Elapsed.TotalSeconds);
                                _DatabaseWriteAverageDuration = _DatabaseNonQueryDurations.Average();
                            }
                        }
                        return modified;
                    }
                    throw e;
                }
                catch (Exception e2)
                {
                    e = e2;
                    if (e2.GetType() == typeof(TimeoutException) ||
                        e2.ToString().Contains("Unable to connect to any of the specified MySQL hosts") ||
                        e2.ToString().Contains("Reading from the stream has failed.") ||
                        e2.ToString().Contains("Too many connections") ||
                        e2.ToString().Contains("Timeout expired") ||
                        e2.ToString().Contains("An existing connection was forcibly closed by the remote host") ||
                        e2.ToString().Contains("Unable to read data") ||
                        e2.ToString().Contains("Lock wait timeout exceeded"))
                    {
                        Log.Info("Average Read: " + Math.Round(_DatabaseReadAverageDuration, 3) + "s " + _DatabaseReaderDurations.Count + " | Average Write: " + Math.Round(_DatabaseWriteAverageDuration, 3) + "s " + _DatabaseNonQueryDurations.Count);
                        PrintPreparedCommand(command);
                    }
                }
                PrintPreparedCommand(command);
                throw e;
            }
        }

        public void HandleDatabaseConnectionInteruption()
        {
            //Only handle these errors if all threads are already functioning normally
            if (_firstPlayerListComplete)
            {
                if (_databaseTimeouts == 0)
                {
                    _lastDatabaseTimeout = UtcNow();
                }
                ++_databaseTimeouts;
                if (_databaseTimeouts >= 5)
                {
                    Log.Warn("Database connection issue detected. Trigger " + _databaseTimeouts + "/" + DatabaseTimeoutThreshold + ".");
                }
                //Check for critical state (timeouts > threshold, and last issue less than 1 minute ago)
                if ((UtcNow() - _lastDatabaseTimeout).TotalSeconds < 60)
                {
                    if (_databaseTimeouts >= DatabaseTimeoutThreshold)
                    {
                        try
                        {
                            //If the handler is already alive, return
                            if (_DisconnectHandlingThread != null && _DisconnectHandlingThread.IsAlive)
                            {
                                Log.Debug(() => "Attempted to start disconnect handling thread when it was already running.", 2);
                                return;
                            }
                            //Create a new thread to handle the disconnect orchestration
                            _DisconnectHandlingThread = new Thread(new ThreadStart(delegate
                            {
                                try
                                {
                                    Thread.CurrentThread.Name = "DisconnectHandling";
                                    //Log the time of critical disconnect
                                    DateTime disconnectTime = DateTime.Now;
                                    Stopwatch disconnectTimer = new Stopwatch();
                                    disconnectTimer.Start();
                                    //Immediately disable Stat Logger
                                    Log.Error("Database connection in critical failure state. Disabling Stat Logger and putting AdKats in Backup Mode.");
                                    _databaseConnectionCriticalState = true;
                                    ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "False");
                                    ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLogger", "False");
                                    //Set resolved
                                    Boolean restored = false;
                                    //Enter loop to check for database reconnection
                                    do
                                    {
                                        //If someone manually disables AdKats, exit everything
                                        if (!_pluginEnabled)
                                        {
                                            Threading.StopWatchdog();
                                            return;
                                        }
                                        //Wait 15 seconds to retry
                                        Threading.Wait(15000);
                                        //Check if the connection has been restored
                                        restored = DebugDatabaseConnectionActive();
                                        if (!restored)
                                        {
                                            _databaseSuccess = 0;
                                            //Inform the user database still not connectable
                                            Log.Error("Database still not accessible. (" + FormatTimeString(disconnectTimer.Elapsed, 3) + " since critical disconnect at " + disconnectTime.ToShortTimeString() + ".)");
                                        }
                                        else
                                        {
                                            _databaseSuccess++;
                                            Log.Info("Database connection appears restored, but waiting " + (DatabaseSuccessThreshold - _databaseSuccess) + " more successful connections to restore normal operation.");
                                        }
                                    } while (_databaseSuccess < DatabaseSuccessThreshold);
                                    //Connection has been restored, inform the user
                                    disconnectTimer.Stop();
                                    Log.Success("Database connection restored, re-enabling Stat Logger and returning AdKats to Normal Mode.");
                                    //Reset timeout counts
                                    _databaseSuccess = 0;
                                    _databaseTimeouts = 0;
                                    //re-enable AdKats and Stat Logger
                                    _databaseConnectionCriticalState = false;
                                    ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "True");
                                    ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLogger", "True");

                                    //Clear the player dinctionary, causing all players to be fetched from the database again
                                    lock (_PlayerDictionary)
                                    {
                                        _PlayerDictionary.Clear();
                                    }

                                    //Create the Exception record
                                    ARecord record = new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        isDebug = true,
                                        server_id = _serverInfo.ServerID,
                                        command_type = GetCommandByKey("adkats_exception"),
                                        command_numeric = 0,
                                        target_name = "Database",
                                        target_player = null,
                                        source_name = "AdKats",
                                        record_message = "Critical Database Disconnect Handled (" + String.Format("{0:0.00}", disconnectTimer.Elapsed.TotalMinutes) + " minutes). AdKats on server " + _serverInfo.ServerID + " functioning normally again.",
                                        record_time = UtcNow()
                                    };
                                    //Process the record
                                    QueueRecordForProcessing(record);
                                }
                                catch (Exception)
                                {
                                    Log.Error("Error handling database disconnect.");
                                }
                                Log.Success("Exiting Critical Disconnect Handler.");
                                Threading.StopWatchdog();
                            }));

                            //Start the thread
                            Threading.StartWatchdog(_DisconnectHandlingThread);
                        }
                        catch (Exception)
                        {
                            Log.Error("Error while initializing disconnect handling thread.");
                        }
                    }
                }
                else
                {
                    //Reset the current timout count
                    _databaseTimeouts = 0;
                }
                _lastDatabaseTimeout = UtcNow();
            }
            else
            {
                Log.Debug(() => "Attempted to handle database timeout when threads not running.", 2);
            }
        }

        private Int32 getNukeCount(Int32 teamID)
        {
            if (!_nukesThisRound.ContainsKey(teamID))
            {
                _nukesThisRound[teamID] = 0;
            }
            return _nukesThisRound[teamID];
        }

        private void incNukeCount(Int32 teamID)
        {
            _nukesThisRound[teamID] = getNukeCount(teamID) + 1;
        }

        public void StartRoundTimer()
        {
            if (_pluginEnabled && _threadsReady)
            {
                try
                {
                    //If the thread is still alive, inform the user and return
                    if (_RoundTimerThread != null && _RoundTimerThread.IsAlive)
                    {
                        Log.Error("Tried to enable a round timer while one was still active.");
                        return;
                    }
                    _RoundTimerThread = new Thread(new ThreadStart(delegate
                    {
                        try
                        {
                            Thread.CurrentThread.Name = "RoundTimer";
                            Log.Debug(() => "starting round timer", 2);
                            Threading.Wait(5000);
                            int maxRoundTimeSeconds = (Int32)(_maxRoundTimeMinutes * 60);
                            for (Int32 secondsRemaining = maxRoundTimeSeconds; secondsRemaining > 0; secondsRemaining--)
                            {
                                if (_roundState != RoundState.Playing || !_pluginEnabled || !_threadsReady)
                                {
                                    return;
                                }
                                if (secondsRemaining == maxRoundTimeSeconds - 60 && secondsRemaining > 60)
                                {
                                    AdminTellMessage("Round will end automatically in ~" + (Int32)(secondsRemaining / 60.0) + " minutes.");
                                    Log.Debug(() => "Round will end automatically in ~" + (Int32)(secondsRemaining / 60.0) + " minutes.", 3);
                                }
                                else if (secondsRemaining == (maxRoundTimeSeconds / 2) && secondsRemaining > 60)
                                {
                                    AdminTellMessage("Round will end automatically in ~" + (Int32)(secondsRemaining / 60.0) + " minutes.");
                                    Log.Debug(() => "Round will end automatically in ~" + (Int32)(secondsRemaining / 60.0) + " minutes.", 3);
                                }
                                else if (secondsRemaining == 30)
                                {
                                    AdminTellMessage("Round ends in 30 seconds. (Current winning team will win)");
                                    Log.Debug(() => "Round ends in 30 seconds. (Current winning team will win)", 3);
                                }
                                else if (secondsRemaining == 20)
                                {
                                    AdminTellMessage("Round ends in 20 seconds. (Current winning team will win)");
                                    Log.Debug(() => "Round ends in 20 seconds. (Current winning team will win)", 3);
                                }
                                else if (secondsRemaining <= 10)
                                {
                                    AdminSayMessage("Round ends in..." + secondsRemaining);
                                    Log.Debug(() => "Round ends in..." + secondsRemaining, 3);
                                }
                                //Sleep for 1 second
                                Threading.Wait(1000);
                            }
                            ATeam team1, team2;
                            if (!GetTeamByID(1, out team1))
                            {
                                if (_roundState == RoundState.Playing)
                                {
                                    Log.Error("Teams not loaded when they should be.");
                                }
                                Threading.StopWatchdog();
                                return;
                            }
                            if (!GetTeamByID(2, out team2))
                            {
                                if (_roundState == RoundState.Playing)
                                {
                                    Log.Error("Teams not loaded when they should be.");
                                }
                                Threading.StopWatchdog();
                                return;
                            }
                            if (team1.TeamTicketCount < team2.TeamTicketCount)
                            {
                                ExecuteCommand("procon.protected.send", "mapList.endRound", "2");
                                Log.Debug(() => "Ended Round (2)", 4);
                            }
                            else
                            {
                                ExecuteCommand("procon.protected.send", "mapList.endRound", "1");
                                Log.Debug(() => "Ended Round (1)", 4);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error in round timer thread.", e));
                        }
                        Log.Debug(() => "Exiting round timer.", 2);
                        Threading.StopWatchdog();
                    }));

                    //Start the thread
                    Threading.StartWatchdog(_RoundTimerThread);
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error starting round timer thread.", e));
                }
            }
        }

        private void DoBattlelogWait()
        {
            try
            {
                lock (_battlelogLocker)
                {
                    var now = UtcNow();
                    var timeSinceLast = (now - _LastBattlelogAction);
                    var requiredWait = _BattlelogWaitDuration;
                    // Preliminary wait increase when battlelog disconnect is detected
                    if (NowDuration(_LastBattlelogIssue).TotalMinutes < 3)
                    {
                        Threading.Wait(TimeSpan.FromSeconds(20));
                    }
                    //Wait between battlelog actions
                    if (timeSinceLast < requiredWait)
                    {
                        var remainingWait = requiredWait - timeSinceLast;
                        Log.Debug(() => "Waiting " + ((int)remainingWait.TotalMilliseconds) + "ms to query battlelog.", 6);
                        Threading.Wait(remainingWait);
                    }
                    now = UtcNow();
                    lock (_BattlelogActionTimes)
                    {
                        _BattlelogActionTimes.Enqueue(now);
                    }
                    _LastBattlelogAction = UtcNow();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error performing battlelog wait.", e));
                Threading.Wait(_BattlelogWaitDuration);
            }
        }

        private void DoServerInfoTrigger()
        {
            _LastServerInfoTrigger = UtcNow();
            ExecuteCommand("procon.protected.send", "serverInfo");
        }

        private void DoPlayerListTrigger()
        {
            lock (_PlayerListTriggerTimes)
            {
                while (_PlayerListTriggerTimes.Any() && NowDuration(_PlayerListTriggerTimes.Peek()).TotalMinutes > 7.5)
                {
                    _PlayerListTriggerTimes.Dequeue();
                }
                _LastPlayerListTrigger = UtcNow();
                _PlayerListTriggerTimes.Enqueue(_LastPlayerListTrigger);
                ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
            }
        }

        private Double getPlayerListTriggerRate()
        {
            lock (_PlayerListTriggerTimes)
            {
                while (_PlayerListTriggerTimes.Any() && NowDuration(_PlayerListTriggerTimes.Peek()).TotalMinutes > 7.5)
                {
                    _PlayerListTriggerTimes.Dequeue();
                }
                return _PlayerListTriggerTimes.Count() / NowDuration(_PlayerListTriggerTimes.Min()).TotalMinutes;
            }
        }

        private void DoPlayerListReceive()
        {
            lock (_PlayerListReceiveTimes)
            {
                while (_PlayerListReceiveTimes.Any() && NowDuration(_PlayerListReceiveTimes.Peek()).TotalMinutes > 7.5)
                {
                    _PlayerListReceiveTimes.Dequeue();
                }
                _LastPlayerListReceive = UtcNow();
                _PlayerListReceiveTimes.Enqueue(_LastPlayerListReceive);
            }
        }

        private Double getPlayerListReceiveRate()
        {
            lock (_PlayerListReceiveTimes)
            {
                while (_PlayerListReceiveTimes.Any() && NowDuration(_PlayerListReceiveTimes.Peek()).TotalMinutes > 7.5)
                {
                    _PlayerListReceiveTimes.Dequeue();
                }
                return _PlayerListReceiveTimes.Count() / NowDuration(_PlayerListReceiveTimes.Min()).TotalMinutes;
            }
        }

        private void DoPlayerListAccept()
        {
            lock (_PlayerListAcceptTimes)
            {
                while (_PlayerListAcceptTimes.Any() && NowDuration(_PlayerListAcceptTimes.Peek()).TotalMinutes > 7.5)
                {
                    _PlayerListAcceptTimes.Dequeue();
                }
                _LastPlayerListAccept = UtcNow();
                _PlayerListAcceptTimes.Enqueue(_LastPlayerListAccept);
            }
        }

        private Double getPlayerListAcceptRate()
        {
            lock (_PlayerListAcceptTimes)
            {
                while (_PlayerListAcceptTimes.Any() && NowDuration(_PlayerListAcceptTimes.Peek()).TotalMinutes > 7.5)
                {
                    _PlayerListAcceptTimes.Dequeue();
                }
                return _PlayerListAcceptTimes.Count() / NowDuration(_PlayerListAcceptTimes.Min()).TotalMinutes;
            }
        }

        private void DoPlayerListProcessed()
        {
            lock (_PlayerListProcessedTimes)
            {
                while (_PlayerListProcessedTimes.Any() && NowDuration(_PlayerListProcessedTimes.Peek()).TotalMinutes > 7.5)
                {
                    _PlayerListProcessedTimes.Dequeue();
                }
                _LastPlayerListProcessed = UtcNow();
                _PlayerListProcessedTimes.Enqueue(_LastPlayerListProcessed);
            }
        }

        private Double getPlayerListProcessedRate()
        {
            lock (_PlayerListProcessedTimes)
            {
                while (_PlayerListProcessedTimes.Any() && NowDuration(_PlayerListProcessedTimes.Peek()).TotalMinutes > 7.5)
                {
                    _PlayerListProcessedTimes.Dequeue();
                }
                return _PlayerListProcessedTimes.Count() / NowDuration(_PlayerListProcessedTimes.Min()).TotalMinutes;
            }
        }

        private void DoIPAPIWait()
        {
            try
            {
                lock (_IPAPILocker)
                {
                    var now = UtcNow();
                    var timeSinceLast = (now - _LastBattlelogAction);
                    var requiredWait = _IPAPIWaitDuration;
                    //Wait between battlelog actions
                    if (timeSinceLast < requiredWait)
                    {
                        var remainingWait = requiredWait - timeSinceLast;
                        Log.Debug(() => "Waiting " + ((int)remainingWait.TotalMilliseconds) + "ms to query IPAPI.", 6);
                        Threading.Wait(remainingWait);
                    }
                    _LastIPAPIAction = UtcNow();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error performing IPAPI wait.", e));
                Threading.Wait(_BattlelogWaitDuration);
            }
        }

        private void DoGoogleWait()
        {
            if ((UtcNow() - _LastGoogleAction) < _GoogleWaitDuration)
            {
                Thread.Sleep(_GoogleWaitDuration - (UtcNow() - _LastGoogleAction));
            }
            _LastGoogleAction = UtcNow();
        }

        //Credit Patrick McDonald
        public static string TrimStart(string target, string trimString)
        {
            string result = target;
            while (result.StartsWith(trimString))
            {
                result = result.Substring(trimString.Length);
            }
            return result;
        }
        public static string TrimEnd(string target, string trimString)
        {
            string result = target;
            while (result.EndsWith(trimString))
            {
                result = result.Substring(0, result.Length - trimString.Length);
            }
            return result;
        }

        public Int64 ConvertVersionInt(String version)
        {
            try
            {
                String[] versionSplit = version.Split('.');
                Int64 major, minor, patch, hotfix;
                if (versionSplit.Length == 4 &&
                    Int64.TryParse(versionSplit[0], out major) &&
                    Int64.TryParse(versionSplit[1], out minor) &&
                    Int64.TryParse(versionSplit[2], out patch) &&
                    Int64.TryParse(versionSplit[3], out hotfix))
                {
                    return
                        (major * 1000000000) +
                        (minor * 1000000) +
                        (patch * 1000) +
                        (hotfix);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error converting version number.", e));
            }
            return 0;
        }

        public Int32 GetStringUpperPercentage(String input)
        {
            Int32 upperCount = 0;
            Int32 totalCount = 0;
            try
            {
                foreach (var character in input.ToCharArray())
                {
                    if (char.IsLetter(character))
                    {
                        totalCount++;
                        if (char.IsUpper(character))
                        {
                            upperCount++;
                        }
                    }
                }
                if (totalCount == 0)
                {
                    return 0;
                }
                return (Int32)Math.Ceiling((Double)upperCount / (Double)totalCount * 100.0);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error getting string upper percentage.", e));
            }
            return 0;
        }

        // FetchIPLocation removed — replaced by OnIPChecked event in AdKats.External.cs

        // CompilePluginSource removed — Procon v2 handles plugin compilation via Roslyn internally

        public String ProcessEventServerName(String serverName, Boolean testActive, Boolean testConcrete)
        {
            var eventDate = GetEventRoundDateTime();
            if (((_CurrentEventRoundNumber == 999999 && eventDate < DateTime.Now) || _CurrentEventRoundNumber < _roundID) && !EventActive())
            {
                serverName = serverName.Replace("%EventDateDuration%", "TBD")
                                       .Replace("%EventDateTime%", "TBD")
                                       .Replace("%EventDate%", "TBD")
                                       .Replace("%EventRound%", "TBD")
                                       .Replace("%RemainingRounds%", "TBD")
                                       .Replace("%s%", "s")
                                       .Replace("%S%", "S");
            }
            else
            {
                if (serverName.Contains("%EventDateDuration%"))
                {
                    serverName = serverName.Replace("%EventDateDuration%", FormatTimeString(eventDate - DateTime.Now, 2));
                }
                if (serverName.Contains("%EventDateTime%"))
                {
                    serverName = serverName.Replace("%EventDateTime%", eventDate.ToShortDateString() + " " + eventDate.ToShortTimeString());
                }
                if (serverName.Contains("%EventDate%"))
                {
                    serverName = serverName.Replace("%EventDate%", eventDate.ToShortDateString());
                }
                if (serverName.Contains("%CurrentRound%"))
                {
                    serverName = serverName.Replace("%CurrentRound%", String.Format("{0:n0}", _roundID));
                }
                if (serverName.Contains("%EventRound%"))
                {
                    if (_CurrentEventRoundNumber != 999999)
                    {
                        serverName = serverName.Replace("%EventRound%", String.Format("{0:n0}", _CurrentEventRoundNumber));
                    }
                    else
                    {
                        serverName = serverName.Replace("%EventRound%", String.Format("{0:n0}", FetchEstimatedEventRoundNumber()));
                    }
                }
                if (serverName.Contains("%RemainingRounds%"))
                {
                    var remainingRounds = 0;
                    if (testConcrete)
                    {
                        remainingRounds = 3;
                        serverName = serverName.Replace("%RemainingRounds%", String.Format("{0:n0}", Math.Max(remainingRounds, 0)));
                        serverName = serverName.Replace("%s%", remainingRounds > 1 ? "s" : "");
                        serverName = serverName.Replace("%S%", remainingRounds > 1 ? "S" : "");
                    }
                    else if (_CurrentEventRoundNumber != 999999)
                    {
                        remainingRounds = _CurrentEventRoundNumber - _roundID;
                        serverName = serverName.Replace("%RemainingRounds%", String.Format("{0:n0}", Math.Max(remainingRounds, 0)));
                        serverName = serverName.Replace("%s%", remainingRounds > 1 ? "s" : "");
                        serverName = serverName.Replace("%S%", remainingRounds > 1 ? "S" : "");
                    }
                    else
                    {
                        remainingRounds = FetchEstimatedEventRoundNumber() - _roundID;
                        serverName = serverName.Replace("%RemainingRounds%", String.Format("{0:n0}", Math.Max(remainingRounds, 0)));
                        serverName = serverName.Replace("%s%", remainingRounds > 1 ? "s" : "");
                        serverName = serverName.Replace("%S%", remainingRounds > 1 ? "S" : "");
                    }
                }
            }
            if (testActive)
            {
                serverName += " AUTO-PRIMARIES ONLY!";
            }
            serverName = serverName.Trim();
            Int32 cutLength = serverName.Length - 62;
            if (cutLength > 0)
            {
                serverName = serverName.Substring(0, serverName.Length - cutLength);
            }
            return serverName;
        }


        // ===========================================================================================
        // IPLocation (replaces IPAPILocation), SHA1, StatLibrary, StatLibraryWeapon
        // ===========================================================================================

        /// <summary>
        /// Player IP location data from Procon v2's proxycheck.io integration.
        /// Replaces the old IPAPILocation class that used ip-api.com.
        /// </summary>
        public class IPLocation
        {
            public String IP;
            public String CountryName;
            public String CountryCode;
            public String City;
            public String Provider;
            public Boolean IsVPN;
            public Boolean IsProxy;
            public Boolean IsTor;
            public Int32 Risk;
            public String Status;

            public String regionName;

            // Legacy compatibility properties (mapped from new fields)
            public String country { get { return CountryName; } }
            public String countryCode { get { return CountryCode; } }
            public String city { get { return City; } }
            public String isp { get { return Provider; } }
            public String status { get { return Status; } set { Status = value; } }
        }

        internal static class SHA1
        {
            private static System.Security.Cryptography.SHA1 HASHER = System.Security.Cryptography.SHA1.Create();

            public static string Data(byte[] data)
            {
                StringBuilder stringifyHash = new StringBuilder();
                byte[] hash = SHA1.HASHER.ComputeHash(data);

                for (int x = 0; x < hash.Length; x++)
                {
                    stringifyHash.Append(hash[x].ToString("x2"));
                }

                return stringifyHash.ToString();
            }

            public static string String(string data)
            {
                return SHA1.Data(Encoding.UTF8.GetBytes(data));
            }
        }

        public class StatLibrary
        {
            private readonly AdKats Plugin;
            public Dictionary<String, StatLibraryWeapon> Weapons;

            public StatLibrary(AdKats plugin)
            {
                Plugin = plugin;
            }

            public Boolean PopulateWeaponStats()
            {
                try
                {
                    //Get Weapons
                    Hashtable statTable = FetchWeaponDefinitions();
                    Hashtable gameTable = (Hashtable)statTable[Plugin.GameVersion.ToString()];
                    if (gameTable != null && gameTable.Count > 0)
                    {
                        Dictionary<string, StatLibraryWeapon> tempWeapons = new Dictionary<String, StatLibraryWeapon>();
                        foreach (String currentCategory in gameTable.Keys)
                        {
                            Hashtable categoryTable = (Hashtable)gameTable[currentCategory];
                            foreach (String currentWeapon in categoryTable.Keys)
                            {
                                Hashtable weaponTable = (Hashtable)categoryTable[currentWeapon];
                                StatLibraryWeapon weapon = new StatLibraryWeapon
                                {
                                    ID = currentWeapon,
                                    Category = currentCategory,
                                    DamageMax = (Double)weaponTable["max"],
                                    DamageMin = (Double)weaponTable["min"]
                                };
                                tempWeapons.Add(weapon.ID, weapon);
                            }
                        }
                        if (tempWeapons.Count > 0)
                        {
                            Weapons = tempWeapons;
                            return true;
                        }
                    }
                    else
                    {
                        Plugin.Log.Error("Unable to find current game in weapon stats library.");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error while fetching weapon stats for " + Plugin.GameVersion, e));
                }
                return false;
            }

            private Hashtable FetchWeaponDefinitions()
            {
                Hashtable statTable = null;
                String weaponInfo;
                Plugin.Log.Debug(() => "Fetching weapon statistic definitions...", 2);
                try
                {
                    weaponInfo = Plugin.Util.HttpDownload("https://raw.githubusercontent.com/Hedius/E4GLAdKats/main/adkatsblweaponstats.json" + "?cacherand=" + Environment.TickCount);
                    Plugin.Log.Debug(() => "Weapon statistic definitions fetched.", 1);
                }
                catch (Exception)
                {
                    try
                    {
                        weaponInfo = Plugin.Util.HttpDownload("https://adkats.e4gl.com/adkatsblweaponstats.json" + "?cacherand=" + Environment.TickCount);
                        Plugin.Log.Debug(() => "Weapon statistic definitions fetched from backup location.", 1);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
                try
                {
                    statTable = (Hashtable)JSON.JsonDecode(weaponInfo);
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error while parsing weapon statistic definitions.", e));
                    return null;
                }
                return statTable;
            }
        }

        public class StatLibraryWeapon
        {
            public String Category = null;
            public Double DamageMax = -1;
            public Double DamageMin = -1;
            public String ID = null;
        }

        public List<T> Shuffle<T>(List<T> list)
        {
            RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do
                    provider.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            return list;
        }

        // ===========================================================================================
        // Methods restored during partial-class restructuring
        // ===========================================================================================

        /// <summary>
        /// Extracts text between XML-like tags, e.g. ExtractString(src, "version_code")
        /// extracts content between &lt;version_code&gt; and &lt;/version_code&gt;.
        /// </summary>
        private String ExtractString(String source, String tagName)
        {
            try
            {
                String startTag = "<" + tagName + ">";
                String endTag = "</" + tagName + ">";
                Int32 startIndex = source.IndexOf(startTag);
                if (startIndex < 0)
                {
                    return null;
                }
                startIndex += startTag.Length;
                Int32 endIndex = source.IndexOf(endTag, startIndex);
                if (endIndex < 0)
                {
                    return null;
                }
                return source.Substring(startIndex, endIndex - startIndex).Trim();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error extracting string for tag '" + tagName + "'.", e));
                return null;
            }
        }

        /// <summary>
        /// Gets the chat command text for a given command key from _CommandNameDictionary.
        /// Returns the command_text (e.g. "!assist") or the key itself if not found.
        /// </summary>
        private String GetChatCommandByKey(String commandKey)
        {
            try
            {
                if (_CommandNameDictionary.ContainsKey(commandKey))
                {
                    ACommand command = _CommandNameDictionary[commandKey];
                    if (command != null && !String.IsNullOrEmpty(command.command_text))
                    {
                        return command.command_text;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching chat command for key '" + commandKey + "'.", e));
            }
            return "!" + commandKey;
        }

        /// <summary>
        /// Gets the next occurrence of a specific weekday on or after the given date.
        /// </summary>
        private DateTime GetNextWeekday(DateTime start, DayOfWeek day)
        {
            Int32 daysToAdd = ((Int32)day - (Int32)start.DayOfWeek + 7) % 7;
            if (daysToAdd == 0)
            {
                daysToAdd = 0; // same day is acceptable
            }
            return start.AddDays(daysToAdd);
        }

        /// <summary>
        /// Gets a team identifier key string (e.g. "US", "RU") for a player based on their team ID.
        /// </summary>
        private String GetPlayerTeamKey(APlayer aPlayer)
        {
            try
            {
                if (aPlayer?.fbpInfo != null && _teamDictionary.ContainsKey(aPlayer.fbpInfo.TeamID))
                {
                    return _teamDictionary[aPlayer.fbpInfo.TeamID].TeamKey;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching team key for player.", e));
            }
            return "Unknown";
        }

        /// <summary>
        /// Gets a human-readable team name for a player.
        /// </summary>
        private String GetPlayerTeamName(APlayer aPlayer)
        {
            try
            {
                if (aPlayer?.fbpInfo != null && _teamDictionary.ContainsKey(aPlayer.fbpInfo.TeamID))
                {
                    return _teamDictionary[aPlayer.fbpInfo.TeamID].TeamName;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching team name for player.", e));
            }
            return "Unknown";
        }

        /// <summary>
        /// Checks if a player is from an external source (not on the live server).
        /// External players have a negative or unset player_id or are not in the player dictionary.
        /// </summary>
        private Boolean PlayerIsExternal(APlayer aPlayer)
        {
            if (aPlayer == null)
            {
                return true;
            }
            try
            {
                if (!aPlayer.player_online)
                {
                    return true;
                }
                lock (_PlayerDictionary)
                {
                    return !_PlayerDictionary.ContainsKey(aPlayer.player_name);
                }
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Checks if a player is on the currently winning team (higher ticket count).
        /// </summary>
        private Boolean PlayerIsWinning(APlayer aPlayer)
        {
            try
            {
                if (aPlayer?.fbpInfo == null)
                {
                    return false;
                }
                Int32 playerTeamID = aPlayer.fbpInfo.TeamID;
                if (!_teamDictionary.ContainsKey(playerTeamID))
                {
                    return false;
                }
                ATeam playerTeam = _teamDictionary[playerTeamID];
                // Check if this team has the highest ticket count
                foreach (var kvp in _teamDictionary)
                {
                    if (kvp.Key != playerTeamID && kvp.Value.TeamTicketCount > playerTeam.TeamTicketCount)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error checking if player is winning.", e));
                return false;
            }
        }

        /// <summary>
        /// Executes an RCON command after a specified delay in milliseconds.
        /// </summary>
        private void ExecuteCommandWithDelay(Int32 delayMS, params String[] commandParts)
        {
            if (delayMS <= 0)
            {
                ExecuteCommand(commandParts);
                return;
            }
            Threading.StartWatchdog(new Thread(new ThreadStart(delegate
            {
                Thread.CurrentThread.Name = "DelayedCommand";
                Threading.Wait(delayMS);
                ExecuteCommand(commandParts);
                Threading.StopWatchdog();
            })));
        }

        /// <summary>
        /// Calculates the remaining ban time for a given ban.
        /// </summary>
        private TimeSpan GetRemainingBanTime(ABan aBan)
        {
            try
            {
                if (aBan == null || aBan.ban_endTime == DateTime.MinValue)
                {
                    return TimeSpan.MaxValue;
                }
                TimeSpan remaining = aBan.ban_endTime - UtcNow();
                if (remaining.TotalSeconds < 0)
                {
                    return TimeSpan.Zero;
                }
                return remaining;
            }
            catch (Exception)
            {
                return TimeSpan.MaxValue;
            }
        }

        /// <summary>
        /// Strips non-alphanumeric characters from a string.
        /// </summary>
        private String MakeAlphanumeric(String input)
        {
            if (String.IsNullOrEmpty(input))
            {
                return String.Empty;
            }
            return Regex.Replace(input, @"[^a-zA-Z0-9]", "");
        }

        /// <summary>
        /// Formats a duration relative to now, using the specified number of time components.
        /// </summary>
        private String FormatNowDuration(DateTime referenceTime, Int32 maxComponents)
        {
            TimeSpan duration = NowDuration(referenceTime);
            return FormatTimeString(duration, maxComponents);
        }

        /// <summary>
        /// Gets the current readable map name from the server info.
        /// </summary>
        private String GetCurrentReadableMap()
        {
            try
            {
                if (_serverInfo?.InfoObject != null)
                {
                    String mapCode = _serverInfo.InfoObject.Map;
                    if (!String.IsNullOrEmpty(mapCode) && ReadableMaps.ContainsKey(mapCode))
                    {
                        return ReadableMaps[mapCode];
                    }
                    // Try matching from available map modes
                    if (_AvailableMapModes != null)
                    {
                        var match = _AvailableMapModes.FirstOrDefault(m => m.FileName == mapCode);
                        if (match != null)
                        {
                            return match.PublicLevelName;
                        }
                    }
                    return mapCode ?? "Unknown";
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error getting readable map name.", e));
            }
            return "Unknown";
        }

        /// <summary>
        /// Gets the current readable game mode name from the server info.
        /// </summary>
        private String GetCurrentReadableMode()
        {
            try
            {
                if (_serverInfo?.InfoObject != null)
                {
                    String modeCode = _serverInfo.InfoObject.GameMode;
                    if (!String.IsNullOrEmpty(modeCode) && ReadableModes.ContainsKey(modeCode))
                    {
                        return ReadableModes[modeCode];
                    }
                    return modeCode ?? "Unknown";
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error getting readable mode name.", e));
            }
            return "Unknown";
        }

        /// <summary>
        /// Checks if a user has admin privileges (has at least one allowed command beyond guest).
        /// </summary>
        private Boolean UserIsAdmin(AUser aUser)
        {
            try
            {
                if (aUser?.user_role == null)
                {
                    return false;
                }
                // Guests and default roles are not admins
                if (aUser.user_role.role_key == "guest_default")
                {
                    return false;
                }
                // Check if the role has any allowed commands
                return aUser.user_role.RoleAllowedCommands != null && aUser.user_role.RoleAllowedCommands.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a player is in a specific special group (e.g. "whitelist_vpn").
        /// </summary>
        private Boolean IsPlayerInSpecialGroup(APlayer aPlayer, String groupKey)
        {
            try
            {
                if (aPlayer?.player_role?.RoleSetGroups == null)
                {
                    return false;
                }
                return aPlayer.player_role.RoleSetGroups.ContainsKey(groupKey);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Replaces player information placeholders in a template string.
        /// </summary>
        private String ReplacePlayerInformation(String template, APlayer aPlayer)
        {
            try
            {
                if (String.IsNullOrEmpty(template) || aPlayer == null)
                {
                    return template;
                }
                template = template.Replace("%player_name%", aPlayer.player_name ?? "Unknown");
                template = template.Replace("%player_id%", aPlayer.player_id.ToString());
                template = template.Replace("%player_guid%", aPlayer.player_guid ?? "Unknown");
                template = template.Replace("%player_pbguid%", aPlayer.player_pbguid ?? "Unknown");
                template = template.Replace("%player_ip%", aPlayer.player_ip ?? "Unknown");
                template = template.Replace("%player_clan%", aPlayer.player_clanTag ?? "");
                if (aPlayer.location != null)
                {
                    template = template.Replace("%player_country%", aPlayer.location.CountryName ?? "Unknown");
                    template = template.Replace("%player_countrycode%", aPlayer.location.CountryCode ?? "Unknown");
                    template = template.Replace("%player_city%", aPlayer.location.City ?? "Unknown");
                }
                else
                {
                    template = template.Replace("%player_country%", "Unknown");
                    template = template.Replace("%player_countrycode%", "Unknown");
                    template = template.Replace("%player_city%", "Unknown");
                }
                template = template.Replace("%player_reputation%", Math.Round(aPlayer.player_reputation, 2).ToString());
                template = template.Replace("%player_infpoints%", aPlayer.player_infractionPoints.ToString());
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error replacing player information in template.", e));
            }
            return template;
        }

        /// <summary>
        /// Checks if a database connection can be established. Used for connection recovery.
        /// </summary>
        private Boolean DebugDatabaseConnectionActive()
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(_dbCommStringBuilder.ConnectionString))
                {
                    connection.Open();
                    using (MySqlCommand command = new MySqlCommand("SELECT UTC_TIMESTAMP()", connection))
                    {
                        command.ExecuteScalar();
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Debug(() => "Database connection check failed: " + e.Message, 4);
                return false;
            }
        }

        /// <summary>
        /// Gets the current ping limit based on population and time-of-day modifiers.
        /// </summary>
        private Double GetPingLimit()
        {
            try
            {
                Int32 currentHour = DateTime.UtcNow.Hour;
                Double baseTrigger;
                Int32 timeModifier;

                switch (_populationStatus)
                {
                    case PopulationState.Low:
                        baseTrigger = _pingEnforcerLowTriggerMS;
                        timeModifier = _pingEnforcerLowTimeModifier[currentHour];
                        break;
                    case PopulationState.Medium:
                        baseTrigger = _pingEnforcerMedTriggerMS;
                        timeModifier = _pingEnforcerMedTimeModifier[currentHour];
                        break;
                    case PopulationState.High:
                        baseTrigger = _pingEnforcerHighTriggerMS;
                        timeModifier = _pingEnforcerHighTimeModifier[currentHour];
                        break;
                    default:
                        baseTrigger = _pingEnforcerFullTriggerMS;
                        timeModifier = _pingEnforcerFullTimeModifier[currentHour];
                        break;
                }

                return baseTrigger + timeModifier;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error calculating ping limit.", e));
                return 300;
            }
        }

        /// <summary>
        /// Gets a display string for the current ping limit status.
        /// </summary>
        private String GetPingLimitStatus()
        {
            try
            {
                Double limit = GetPingLimit();
                return limit + "ms (" + _populationStatus + " pop, hour " + DateTime.UtcNow.Hour + " UTC)";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// IP API communication thread loop.
        /// In v2, IP checking is handled via the OnIPChecked event callback from PRoCon's
        /// built-in IPCheckService. This thread is kept as a no-op for compatibility.
        /// </summary>
        public void IPAPICommThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting IP API Comm Thread", 1);
                Thread.CurrentThread.Name = "IPAPIComm";
                while (true)
                {
                    try
                    {
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        // In v2, IP checking is event-based via OnIPChecked.
                        // This thread sleeps and periodically cleans up stale pending checks.
                        Threading.Wait(10000);

                        if (!_firstPlayerListComplete)
                        {
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("IP API comm thread aborted.", e));
                            break;
                        }
                        Log.HandleException(new AException("Error in IP API comm thread loop.", e));
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error in IP API comm thread.", e));
            }
        }

    }
}
