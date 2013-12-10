/* 
 * AdKats is an Advanced In-Game Admin and Ban Enforcer for Procon Frostbite.
 * 
 * Copyright 2013 A Different Kind, LLC
 * 
 * AdKats was inspired by the gaming community A Different Kind (ADK), with help from the BF3 Admins within the 
 * community. Visit http://www.ADKGamers.com/ for more information.
 *
 * The AdKats Frostbite Plugin is free software: you can redistribute it and/or modify it under the terms of the
 * GNU General Public License as published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version. AdKats is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See the GNU General Public License for more details. To view this license, visit http://www.gnu.org/licenses/.
 * 
 * Code Credit:
 * Modded Levenshtein Distance algorithm from Micovery's InsaneLimits
 * Twitter Interaction System from Micovery's InsaneLimits
 * Email System from MorpheusX(AUT)'s "Notify Me!"
 * 
 * Development by ColColonCleaner
 * 
 * AdKats.cs
 * Beta Version 3.9.9.2
 */

using System;
using System.Globalization;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Collections;
using System.Net;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Diagnostics;
using MySql.Data.MySqlClient;
using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.HttpServer;


namespace PRoConEvents {
    //Aliases

    public class AdKats : PRoConPluginAPI, IPRoConPluginInterface {
        #region Variables

        //Current version of the plugin
        private const String PluginVersion = "3.9.9.2";
        //When slowmo is enabled, there will be a 1 second pause between each print to console or in-game say
        //This will slow the program as a whole whenever the console is printed to
        private const Boolean Slowmo = false;

        //Match command showing whether AdKats is installed and running
        private readonly MatchCommand _AdKatsAvailableIndicator;

        //Messaging
        public enum ConsoleMessageType {
            Warning,
            Error,
            Exception,
            Normal,
            Success
        };

        //Plugin Info
        private volatile Boolean _FetchedPluginInformation = false;
        private volatile String _PluginVersionStatus = null;
        private volatile String _PluginDescription = null;
        private volatile String _PluginChangelog = null;

        //Constants
        //IDs of the two teams as the server understands it
        private const Int32 UsTeamID = 1;
        private const Int32 RuTeamID = 2;

        //General Plugin Settings/Status
        private volatile Boolean _IsEnabled = false;
        private volatile Boolean _ThreadsReady = false;
        private volatile Boolean _UseKeepAlive = false;

        //Player Lists
        private readonly Dictionary<String, AdKats_Player> _PlayerDictionary = new Dictionary<String, AdKats_Player>();
        private DateTime _LastSuccessfulPlayerList = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        //player counts per team
        private Int32 _UsPlayerCount = 0;
        private Int32 _RuPlayerCount = 0;

        //User Settings
        private Dictionary<long, AdKatsUser> _UserCache = new Dictionary<long, AdKatsUser>();
        private DateTime _LastUserFetch = DateTime.UtcNow;
        //Frequency in seconds to fetch user changes at
        private const Int32 DbUserFetchFrequency = 300;

        //Database Settings
        private String _MySqlHostname = "";
        private String _MySqlPort = "";
        private String _MySqlDatabaseName = "";
        private String _MySqlUsername = "";
        private String _MySqlPassword = "";
        private readonly MySqlConnectionStringBuilder _DbCommStringBuilder = new MySqlConnectionStringBuilder();
        private const Boolean UseConnectionPooling = true;
        private const Int32 MinConnectionPoolSize = 0;
        private const Int32 MaxConnectionPoolSize = 20;
        private const Boolean UseCompressedConnection = false;
        private volatile Boolean _DbSettingsChanged = true;
        //Action fetching from database
        private Boolean _FetchActionsFromDb = false;
        private DateTime _LastDbActionFetch = DateTime.UtcNow;
        //frequency in seconds to fetch actions at
        private const Int32 DbActionFetchFrequency = 10;
        //Error Catching
        private Boolean _DatabaseConnectionCriticalState = false;
        private const Int32 DatabaseTimeoutThreshold = 3;
        private Int32 _DatabaseTimeouts = 0;
        private DateTime _LastDatabaseTimeout = DateTime.UtcNow;

        //Server Settings
        private Int64 _ServerID = -1;
        private String _ServerName = null;
        private String _ServerIP = null;
        private CServerInfo _ServerInfo = null;

        public enum GameVersion {
            BF3,
            BF4
        };

        private GameVersion _GameVersion = GameVersion.BF3;
        private Int32 _GameID = -1;
        //Assume the BF3 version unless universal is detected
        private String _StatLoggerVersion = "BF3";
        private String _GamePatchVersion = "UNKNOWN";
        private String _ServerType = "UNKNOWN";
        private Boolean _FairFightEnabled = false;
        private Boolean _HitIndicatorEnabled = false;
        private Boolean _CommanderEnabled = false;
        private Boolean _ForceReloadWholeMags = false;
        private Int32 _MaxSpectators = -1;

        //Setting Import
        private Int64 _SettingImportID = -1;
        private DateTime _LastDbSettingFetch = DateTime.UtcNow;
        private const Int32 DbSettingFetchFrequency = 300;

        //Round Settings
        private Boolean _UseRoundTimer = false;
        private Boolean _RoundEnded = false;
        private Double _RoundTimeMinutes = 30;

        //ADK Settings
        //This will automatically change to true on ADK servers
        private Boolean _IsTestingAuthorized = false;

        //Experimental Tools Settings
        private Boolean _UseExperimentalTools = false;
        //NO EX Limiter
        private Boolean _UseWeaponLimiter = false;
        private String _WeaponLimiterString = "M320|RPG|SMAW|C4|M67|Claymore|MAV|FGM-148|FIM92|ROADKILL|Death|_LVG|_HE|_Frag|_XM25|_FLASH|_V40|_M34|_Flashbang|_SMK|_Smoke|_FGM148|_Grenade|_SLAM|_NLAW|_RPG7|_C4|_Claymore|_FIM92|_M67|_SMAW|_SRAW|_Sa18IGLA|_Tomahawk";
        private String _WeaponLimiterExceptionString = "_Flechette|_Slug";
        //Grenade Cook Catcher
        private Boolean _UseGrenadeCookCatcher = false;
        private Dictionary<String, AdKats_Player> _RoundCookers = null;
        //Hacker Checker
        private Boolean _UseHackerChecker = false;
        private String[] _HackerCheckerWhitelist = {"ColColonCleaner"};
        private Boolean _UseDpsChecker = false;
        private Double _DpsTriggerLevel = 50.0;
        private Boolean _UseHskChecker = false;
        private Double _HskTriggerLevel = 70.0;

        //Infraction Management Settings
        //Whether to combine server punishments
        private Boolean _CombineServerPunishments = false;
        //IRO punishment setting
        private Boolean _IROActive = true;
        private Int32 _IROTimeout = 10;
        private Boolean _IROOverridesLowPop = false;
        //Default hierarchy of punishments
        private String[] _PunishmentHierarchy = {"kill", "kill", "kick", "tban60", "tban120", "tbanday", "tbanweek", "tban2weeks", "tbanmonth", "ban"};
        private readonly List<String> _PunishmentSeverityIndex = null;
        //When punishing, only kill players when server is in low population
        private Boolean _OnlyKillOnLowPop = true;
        //Default for low populations
        private Int32 _LowPopPlayerCount = 20;

        //Ban Management Settings
        //General
        private Boolean _UseBanAppend = false;
        private String _BanAppend = "Appeal at your_site.com";
        //Ban Enforcer
        private Boolean _UseBanEnforcer = false;
        private String _CBanAdminName = "BanEnforcer";
        private Boolean _UseBanEnforcerPreviousState = false;
        private Boolean _DefaultEnforceName = false;
        private Boolean _DefaultEnforceGUID = true;
        private Boolean _DefaultEnforceIP = false;
        private Boolean _BansFirstListed = false;
        private DateTime _LastBanListCall = DateTime.UtcNow;
        private DateTime _LastSuccessfulBanList = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _LastDbBanFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private const Int32 DbBanFetchFrequency = 60;
        private DateTime _PermaBanEndTime = DateTime.UtcNow.AddYears(20);
        private Int64 _NameBanCount = -1;
        private Int64 _GUIDBanCount = -1;
        private Int64 _IPBanCount = -1;
        private DateTime _LastNameBanCountFetch = DateTime.UtcNow;
        private DateTime _LastGUIDBanCountFetch = DateTime.UtcNow;
        private DateTime _LastIPBanCountFetch = DateTime.UtcNow;

        //AdKats WebAdmin Settings
        //This is currently a constant
        private Boolean _UsingAwa = false;

        //Debug Settings
        private volatile Int32 _DebugLevel;
        private Boolean _ToldCol = false;
        //Debug Soldier (Used for displaying command action duration in-game)
        private String _DebugSoldierName = "ColColonCleaner";
        private DateTime _CommandStartTime = DateTime.UtcNow;

        //Orchestration Settings
        private Boolean _FeedMultiBalancerWhitelist = false;
        private Boolean _FeedServerReservedSlots = false;
        private Boolean _FeedStatLoggerSettings = false;
        private List<String> _CurrentReservedSlotPlayers = null;
        private Hashtable _LastStatLoggerStatusUpdate = null;
        private DateTime _LastStatLoggerStatusUpdateTime = DateTime.MinValue;

        //Messaging Settings
        //Pre-Message Settings
        private List<String> _PreMessageList;
        private Boolean _RequirePreMessageUse = false;
        //Yell Settings
        private Int32 _YellDuration = 5;

        //Role Settings
        //Dictionary of roles store by role ID (ID parsing)
        private readonly Dictionary<long, AdKats_Role> _RoleIDDictionary = new Dictionary<long, AdKats_Role>();
        //Dictionary of roles stored by role key (Raw key parsing)
        private readonly Dictionary<String, AdKats_Role> _RoleKeyDictionary = new Dictionary<String, AdKats_Role>();
        //Dictionary of roles stored by role name (Setting parsing)
        private readonly Dictionary<String, AdKats_Role> _RoleNameDictionary = new Dictionary<String, AdKats_Role>();
        //In-Game Command Settings
        //Dictionary of commands store by command ID (database parsing)
        private readonly Dictionary<long, AdKatsCommand> _CommandIDDictionary = new Dictionary<long, AdKatsCommand>();
        //Dictionary of commands stored by command key (Source parsing)
        private readonly Dictionary<String, AdKatsCommand> _CommandKeyDictionary = new Dictionary<String, AdKatsCommand>();
        //Dictionary of commands stored by command name (Name parsing)
        private readonly Dictionary<String, AdKatsCommand> _CommandNameDictionary = new Dictionary<String, AdKatsCommand>();
        //Dictionary of commands stored by command text (In-game parsing)
        private readonly Dictionary<String, AdKatsCommand> _CommandTextDictionary = new Dictionary<String, AdKatsCommand>();
        //Default required reason length for admins
        private Int32 _RequiredReasonLength = 5;

        //External Access Settings
        private String _ExternalCommandAccessKey = "NoPasswordSet";

        //TeamSwap Settings
        //The list of players on RU wishing to move to US (This list takes first priority)
        private Queue<CPlayerInfo> _UsMoveQueue = new Queue<CPlayerInfo>();
        //the list of players on US wishing to move to RU (This list takes secondary)
        private Queue<CPlayerInfo> _RuMoveQueue = new Queue<CPlayerInfo>();
        //whether to allow all players, or just players in the whitelist
        private Boolean _RequireTeamswapWhitelist = true;
        //the lowest ticket count of either team
        private volatile Int32 _LowestTicketCount = 500000;
        //the highest ticket count of either team
        private volatile Int32 _HighestTicketCount = 0;
        private volatile Int32 _UsTicketCount = 0;
        private volatile Int32 _RuTicketCount = 0;
        //the highest ticket count of either team to allow self move
        private Int32 _TeamSwapTicketWindowHigh = 500000;
        //the lowest ticket count of either team to allow self move
        private Int32 _TeamSwapTicketWindowLow = 0;
        //Round only whitelist
        private Dictionary<String, bool> _TeamswapRoundWhitelist = new Dictionary<String, bool>();
        //Number of random players to whitelist at the beginning of the round
        private Int32 _PlayersToAutoWhitelist = 2;
        //Force move action queue
        private readonly Queue<CPlayerInfo> _TeamswapForceMoveQueue = new Queue<CPlayerInfo>();
        //Delayed move list
        private Dictionary<String, CPlayerInfo> _TeamswapOnDeathMoveDic = new Dictionary<String, CPlayerInfo>();
        //Delayed move checking queue
        private readonly Queue<CPlayerInfo> _TeamswapOnDeathCheckingQueue = new Queue<CPlayerInfo>();

        //Report/Admin Call Settings
        //Reports for the current round
        private Dictionary<String, AdKatsRecord> _RoundReports = new Dictionary<String, AdKatsRecord>();

        //Player Mute Settings
        private String _MutedPlayerMuteMessage = "You have been muted by an admin, talking will cause punishment. You can speak again next round.";
        private String _MutedPlayerKillMessage = "Do not talk while muted. You can speak again next round.";
        private String _MutedPlayerKickMessage = "Talking excessively while muted.";
        private Int32 _MutedPlayerChances = 5;
        private Dictionary<String, Int32> _RoundMutedPlayers = new Dictionary<String, Int32>();

        //Admin Assistant Settings
        private Boolean _EnableAdminAssistantPerk = true;
        private Int32 _MinimumRequiredMonthlyReports = 10;

        //Twitter Settings
/*
        private Boolean _UseTwitter = false;
*/
/*
        private TwitterHandler _TwitterHandler = null;
*/

        //Email Settings
        private Boolean _UseEmail = false;
        private readonly EmailHandler _EmailHandler = null;

        //Multi-Threading Settings
        //Threads
        private Thread _PlayerListingThread;
        private Thread _KillProcessingThread;
        private Thread _MessageProcessingThread;
        private Thread _CommandParsingThread;
        private Thread _DatabaseCommunicationThread;
        private Thread _ActionHandlingThread;
        private Thread _TeamSwapThread;
        private Thread _BanEnforcerThread;
        private Thread _HackerCheckerThread;
        private Thread _Activator;
        private Thread _Finalizer;
        private Thread _DisconnectHandlingThread;
        private Thread _RoundTimerThread;
        //Mutexes
        private readonly Object _PlayersMutex = new Object();
/*
        private Object _KillProcessingMutex = new Object();
*/
/*
        private Object _BanListMutex = new Object();
*/
        private readonly Object _ReportsMutex = new Object();
        private readonly Object _ActionConfirmMutex = new Object();
        private readonly Object _UserMutex = new Object();
        private readonly Object _TeamswapMutex = new Object();
        private readonly Object _ServerInfoMutex = new Object();
        private readonly Object _UnparsedMessageMutex = new Object();
        private readonly Object _UnparsedCommandMutex = new Object();
        private readonly Object _UnprocessedRecordMutex = new Object();
        private readonly Object _UnprocessedActionMutex = new Object();
        private readonly Object _BanEnforcerMutex = new Object();
        private readonly Object _HackerCheckerMutex = new Object();
        private readonly Object _RoundEndingMutex = new Object();
        //Handles
        private EventWaitHandle _TeamswapWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PlayerListProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _KillProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PlayerListUpdateWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _MessageParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _CommandParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _DbCommunicationWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _ActionHandlingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _BanEnforcerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _ServerInfoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _StatLoggerStatusWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _HackerCheckerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _HttpFetchWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _RoundEndingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        //Threading Queues
        private readonly Queue<List<CPlayerInfo>> _PlayerListProcessingQueue = new Queue<List<CPlayerInfo>>();
        private readonly Queue<Kill> _KillProcessingQueue = new Queue<Kill>();
        private readonly Queue<KeyValuePair<String, String>> _UnparsedMessageQueue = new Queue<KeyValuePair<String, String>>();
        private readonly Queue<KeyValuePair<String, String>> _UnparsedCommandQueue = new Queue<KeyValuePair<String, String>>();
        private readonly Queue<AdKatsRecord> _UnprocessedRecordQueue = new Queue<AdKatsRecord>();
        private readonly Queue<AdKatsRecord> _UnprocessedActionQueue = new Queue<AdKatsRecord>();
        private readonly Queue<AdKatsUser> _UserUploadQueue = new Queue<AdKatsUser>();
        private readonly Queue<AdKatsUser> _UserRemovalQueue = new Queue<AdKatsUser>();
        private readonly Queue<AdKats_Player> _BanEnforcerCheckingQueue = new Queue<AdKats_Player>();
        private readonly Queue<AdKats_Player> _HackerCheckerQueue = new Queue<AdKats_Player>();
        private readonly Queue<AdKatsBan> _BanEnforcerProcessingQueue = new Queue<AdKatsBan>();
        private readonly Queue<CBanInfo> _CBanProcessingQueue = new Queue<CBanInfo>();
        private readonly Queue<CPluginVariable> _SettingUploadQueue = new Queue<CPluginVariable>();
        private readonly Queue<AdKatsCommand> _CommandUploadQueue = new Queue<AdKatsCommand>();
        private readonly Queue<AdKatsCommand> _CommandRemovalQueue = new Queue<AdKatsCommand>();
        private readonly Queue<AdKats_Role> _RoleUploadQueue = new Queue<AdKats_Role>();
        private readonly Queue<AdKats_Role> _RoleRemovalQueue = new Queue<AdKats_Role>();

        //MISC Settings
        private Boolean _ShowAdminNameInSay = false;
        //When an action requires confirmation, this dictionary holds those actions until player confirms action
        private Dictionary<String, AdKatsRecord> _ActionConfirmDic = new Dictionary<String, AdKatsRecord>();
        //Action will be taken when the player next spawns
        private Dictionary<String, AdKatsRecord> _ActOnSpawnDictionary = new Dictionary<String, AdKatsRecord>();
        //VOIP information
        private String _ServerVoipAddress = "(TS3) TS.ADKGamers.com:3796";
        //Rules information
        private Double _ServerRulesDelay = 0.5;
        private Double _ServerRulesInterval = 5;
        private String[] _ServerRulesList = {"This server has not set rules yet.", "This server has not set rules yet.", "This server has not set rules yet."};

        //Hacker Checker stat library
        private StatLibrary _StatLibrary = null;

        #endregion

        public AdKats() {
            //Set defaults for webclient
            System.Net.ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            this._IsEnabled = false;
            this._ThreadsReady = false;
            //Assign the match command
            this._AdKatsAvailableIndicator = new MatchCommand("AdKats", "NoCallableMethod", new List<String>(), "AdKats_NoCallableMethod", new List<MatchArgumentFormat>(), new ExecutionRequirements(ExecutionScope.None), "Useable by other plugins to determine whether this one is installed and enabled.");
            //Debug level is 0 by default
            this._DebugLevel = 0;
            //Randomize the external access key
            this._ExternalCommandAccessKey = AdKats.GetRandom32BitHashCode();

            //Initialize email handler
            this._EmailHandler = new EmailHandler(this);

            //Init the punishment severity index
            this._PunishmentSeverityIndex = new List<String> {
                                                                 "kill",
                                                                 "kick",
                                                                 "tban60",
                                                                 "tban120",
                                                                 "tbanday",
                                                                 "tbanweek",
                                                                 "tban2weeks",
                                                                 "tbanmonth",
                                                                 "ban"
                                                             };

            //Init the pre-message list
            this._PreMessageList = new List<String> {
                                                        "US TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.",
                                                        "RU TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.",
                                                        "US TEAM: DO NOT ENTER THE STREETS BEYOND 'A', YOU WILL BE PUNISHED.",
                                                        "RU TEAM: DO NOT GO BEYOND THE BLACK LINE ON CEILING BY 'C' FLAG, YOU WILL BE PUNISHED.",
                                                        "THIS SERVER IS NO EXPLOSIVES, YOU WILL BE PUNISHED FOR INFRACTIONS.",
                                                        "JOIN OUR TEAMSPEAK AT TS.ADKGAMERS.COM:3796"
                                                    };

            //Fetch the plugin description and changelog
            this.FetchPluginDescAndChangelog();

            //Prepare the keep-alive
            this.SetupKeepAlive();
        }

        #region Plugin details

        public String GetPluginName() {
            return "AdKats - Advanced In-Game Admin";
        }

        public String GetPluginVersion() {
            return PluginVersion;
        }

        public String GetPluginAuthor() {
            return "[ADK]ColColonCleaner";
        }

        public String GetPluginWebsite() {
            return "https://github.com/ColColonCleaner/AdKats/";
        }

        public String GetPluginDescription() {
            if (!this._FetchedPluginInformation) {
                //Wait up to 10 seconds for the description to fetch
                this.DebugWrite("Waiting for plugin description...", 1);
                if (!this._HttpFetchWaitHandle.WaitOne(10000)) {
                    this.ConsoleError("Unable to fetch plugin description.");
                }
            }

            //Parse out the descriptions
            String concat = String.Empty;
            if (!String.IsNullOrEmpty(this._PluginVersionStatus)) {
                concat += this._PluginVersionStatus;
            }
            if (!String.IsNullOrEmpty(this._PluginDescription)) {
                concat += this._PluginDescription;
            }
            if (!String.IsNullOrEmpty(this._PluginChangelog)) {
                concat += this._PluginChangelog;
            }

            //Check if the description fetched
            if (String.IsNullOrEmpty(concat)) {
                concat = "Plugin description failed to download. Please visit AdKats on github to view the plugin description.";
            }

            return concat;
        }

        private void FetchPluginDescAndChangelog() {
            this._HttpFetchWaitHandle.Reset();
            //Create a new thread to fetch the plugin description and changelog
            Thread httpFetcher = new Thread(new ThreadStart(delegate {
                try {
                    //Create web client
                    WebClient client = new WebClient();
                    //Download the readme and changelog
                    this.DebugWrite("Fetching plugin readme...", 2);
                    try {
                        this._PluginDescription = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/README.md");
                        this.DebugWrite("Plugin description fetched.", 1);
                    }
                    catch (Exception) {
                        this.ConsoleError("Failed to fetch plugin description.");
                    }
                    this.DebugWrite("Fetching plugin changelog...", 2);
                    try {
                        this._PluginChangelog = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/CHANGELOG.md");
                        this.DebugWrite("Plugin changelog fetched.", 1);
                    }
                    catch (Exception) {
                        this.ConsoleError("Failed to fetch plugin changelog.");
                    }
                    if (this._PluginDescription != "DESCRIPTION FETCH FAILED|") {
                        //Extract the latest stable version
                        String latestStableVersion = this.ExtractString(this._PluginDescription, "latest_stable_release");
                        if (!String.IsNullOrEmpty(latestStableVersion)) {
                            //Convert it to an integer
                            String trimmedLatestStableVersion = latestStableVersion.Replace(".", "");
                            Int32 latestStableVersionInt = Int32.Parse(trimmedLatestStableVersion);
                            //Get current plugin version
                            Int32 currentVersionInt = Int32.Parse(PluginVersion.Replace(".", ""));

                            String versionStatus = String.Empty;
                            //Add the appropriate message to plugin description
                            if (latestStableVersionInt > currentVersionInt) {
                                versionStatus = @"
                                <h2 style='color:#DF0101;'>
                                    You are running an outdated build of AdKats! Version " + latestStableVersion + @" is available for download!
                                </h2>
                                <a href='https://github.com/ColColonCleaner/AdKats/blob/master/CHANGELOG.md' target='_blank'>
                                    New in Version " + latestStableVersion + @"!
                                </a><br/>
                                Download link below.";
                            }
                            else if (latestStableVersionInt == currentVersionInt) {
                                versionStatus = @"
                                <h2 style='color:#01DF01;'>
                                    Congrats! You are running the latest stable build of AdKats!
                                </h2>";
                            }
                            else if (latestStableVersionInt < currentVersionInt) {
                                versionStatus = @"
                                <h2 style='color:#FF8000;'>
                                    CAUTION! You are running a BETA or TEST build of AdKats! Functionality might be untested.
                                </h2> Below documentation is for stable build " + latestStableVersion + ".";
                            }
                            //Prepend the message
                            this._PluginVersionStatus = versionStatus;
                        }
                    }
                    this.DebugWrite("Setting desc fetch handle.", 1);
                    this._FetchedPluginInformation = true;
                    this._HttpFetchWaitHandle.Set();
                }
                catch (Exception e) {
                    this.HandleException(new AdKatsException("Error while fetching plugin description and changelog.", e));
                }
            }));
            //Start the thread
            httpFetcher.Start();
        }

        private void SetupKeepAlive() {
            //Create a new thread to handle keep-alive
            //This thread will remain running for the duration the layer is online
            Thread keepAliveThread = new Thread(new ThreadStart(delegate {
                try {
                    while (true) {
                        Thread.Sleep(TimeSpan.FromSeconds(60));
                        if (this._UseKeepAlive) {
                            this.Enable();
                        }
                    }
                }
                catch (Exception e) {
                    this.HandleException(new AdKatsException("Error while running keep-alive.", e));
                }
            }));
            //Start the thread
            keepAliveThread.Start();
        }

        #endregion

        #region Plugin settings

        public List<CPluginVariable> GetDisplayPluginVariables() {
            List<CPluginVariable> lstReturn;
            const string separator = " | ";

            //Only fetch the following settings when plugin disabled
            if (!this._ThreadsReady) {
                lstReturn = new List<CPluginVariable>();

                if (this._UseKeepAlive) {
                    lstReturn.Add(new CPluginVariable("0. Instance Settings|Auto-Enable/Keep-Alive", typeof (Boolean), true));
                }

                lstReturn.Add(new CPluginVariable("Complete these settings before enabling.", typeof (String), "Once enabled, more settings will appear."));
                //SQL Settings
                lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Hostname", typeof (String), this._MySqlHostname));
                lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Port", typeof (String), this._MySqlPort));
                lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Database", typeof (String), this._MySqlDatabaseName));
                lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Username", typeof (String), this._MySqlUsername));
                lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Password", typeof (String), this._MySqlPassword));

                //Debugging Settings
                lstReturn.Add(new CPluginVariable("2. Debugging|Debug level", typeof (Int32), this._DebugLevel));
            }
            else {
                //If plugin is enabled, return the full storage list
                lstReturn = this.GetPluginVariables();
                //Add display variables

                //Server Settings
                lstReturn.Add(new CPluginVariable("1. Server Settings|Server ID (Display)", typeof (int), this._ServerID));
                lstReturn.Add(new CPluginVariable("1. Server Settings|Server IP (Display)", typeof (String), this._ServerIP));
                lstReturn.Add(new CPluginVariable("1. Server Settings|Setting Import", typeof (String), this._ServerID));
                if (!this._UsingAwa) {
                    const string userSettingsPrefix = "3. User Settings|";
                    //User Settings
                    lstReturn.Add(new CPluginVariable(userSettingsPrefix + "Add User", typeof (String), ""));
                    if (this._UserCache.Count > 0) {
                        //Sort access list by access level, then by name
                        List<AdKatsUser> tempAccess = this._UserCache.Values.ToList();
                        tempAccess.Sort((a1, a2) => (a1.user_role.role_id == a2.user_role.role_id) ? (System.String.CompareOrdinal(a1.user_name, a2.user_name)) : ((a1.user_role.role_id < a2.user_role.role_id) ? (-1) : (1)));
                        String roleEnum = String.Empty;
                        if (this._RoleKeyDictionary.Count > 0) {
                            Random random = new Random();
                            foreach (AdKats_Role role in this._RoleKeyDictionary.Values) {
                                if (String.IsNullOrEmpty(roleEnum)) {
                                    roleEnum += "enum.RoleEnum_" + random.Next(100000, 999999) + "(";
                                }
                                else {
                                    roleEnum += "|";
                                }
                                roleEnum += role.role_name;
                            }
                            roleEnum += ")";
                        }
                        foreach (AdKatsUser user in tempAccess) {
                            String userPrefix = userSettingsPrefix + "USR" + user.user_id + separator + user.user_name + separator;
                            if (this._UseEmail) {
                                lstReturn.Add(new CPluginVariable(userPrefix + "User Email", typeof (String), user.user_email));
                            }
                            //Do not display phone input until that operation is available for use
                            //lstReturn.Add(new CPluginVariable(userPrefix + "User Phone", typeof(String), user.user_phone));
                            lstReturn.Add(new CPluginVariable(userPrefix + "User Role", roleEnum, user.user_role.role_name));
                            lstReturn.Add(new CPluginVariable(userPrefix + "Delete User?", typeof (String), ""));
                            lstReturn.Add(new CPluginVariable(userPrefix + "Add Soldier?", typeof (String), ""));
                            String soldierPrefix = userPrefix + "Soldiers" + separator;
                            lstReturn.AddRange(user.soldierDictionary.Values.Select(aPlayer => new CPluginVariable(soldierPrefix + aPlayer.player_id + separator + aPlayer.player_name + separator + "Delete Soldier?", typeof (String), "")));
                        }
                    }
                    else {
                        lstReturn.Add(new CPluginVariable(userSettingsPrefix + "No Users in User List", typeof (String), "Add Users with 'Add User'."));
                    }


                    lstReturn.Add(new CPluginVariable("5. Command Settings|Minimum Required Reason Length", typeof (int), this._RequiredReasonLength));

                    //Role Settings
                    const string roleListPrefix = "4. Role Settings|";
                    lstReturn.Add(new CPluginVariable(roleListPrefix + "Add Role", typeof (String), ""));
                    if (this._RoleKeyDictionary.Count > 0) {
                        lock (this._RoleKeyDictionary) {
                            foreach (AdKats_Role aRole in this._RoleKeyDictionary.Values) {
                                lock (this._CommandNameDictionary) {
                                    String rolePrefix = roleListPrefix + "RLE" + aRole.role_id + separator + aRole.role_name + separator;
                                    lstReturn.AddRange(from aCommand in this._CommandNameDictionary.Values where aCommand.command_active == AdKatsCommand.CommandActive.Active select new CPluginVariable(rolePrefix + "CDE" + aCommand.command_id + separator + aCommand.command_name, "enum.roleAllowCommandEnum(Allow|Deny)", aRole.allowedCommands.ContainsKey(aCommand.command_key) ? ("Allow") : ("Deny")));
                                    //Do not display the delete option for default guest
                                    if (aRole.role_key != "guest_default") {
                                        lstReturn.Add(new CPluginVariable(rolePrefix + "Delete Role? (All assignments will be removed)", typeof (String), ""));
                                    }
                                }
                            }
                        }
                    }
                    else {
                        lstReturn.Add(new CPluginVariable(roleListPrefix + "Role List Empty", typeof (String), "No valid roles found in database."));
                    }

                    //Command Settings
                    const string commandListPrefix = "6. Command List|";
                    if (this._CommandNameDictionary.Count > 0) {
                        foreach (AdKatsCommand command in this._CommandNameDictionary.Values) {
                            if (command.command_active != AdKatsCommand.CommandActive.Invisible) {
                                String commandPrefix = commandListPrefix + "CDE" + command.command_id + separator + command.command_name + separator;
                                lstReturn.Add(new CPluginVariable(commandPrefix + "Active", "enum.commandActiveEnum(Active|Disabled)", command.command_active.ToString()));
                                if (command.command_logging != AdKatsCommand.CommandLogging.Mandatory && command.command_logging != AdKatsCommand.CommandLogging.Unable) {
                                    lstReturn.Add(new CPluginVariable(commandPrefix + "Logging", "enum.commandLoggingEnum(Log|Ignore)", command.command_logging.ToString()));
                                }
                                lstReturn.Add(new CPluginVariable(commandPrefix + "Text", typeof (String), command.command_text));
                            }
                        }
                    }
                    else {
                        lstReturn.Add(new CPluginVariable(commandListPrefix + "Command List Empty", typeof (String), "No valid commands found in database."));
                    }
                }
                else {
                    lstReturn.Add(new CPluginVariable("3. Player Access Settings|You are using AdKats WebAdmin", typeof (String), "Manage admin settings there."));
                }
            }
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables() {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            try {
                //Auto-Enable Settings
                lstReturn.Add(new CPluginVariable("0. Instance Settings|Auto-Enable/Keep-Alive", typeof (Boolean), this._UseKeepAlive));

                //SQL Settings
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Hostname", typeof (String), _MySqlHostname));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Port", typeof (String), _MySqlPort));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Database", typeof (String), _MySqlDatabaseName));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Username", typeof (String), _MySqlUsername));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Password", typeof (String), _MySqlPassword));

                //Punishment Settings
                lstReturn.Add(new CPluginVariable("7. Punishment Settings|Punishment Hierarchy", typeof (String[]), this._PunishmentHierarchy));
                lstReturn.Add(new CPluginVariable("7. Punishment Settings|Combine Server Punishments", typeof (Boolean), this._CombineServerPunishments));
                lstReturn.Add(new CPluginVariable("7. Punishment Settings|Only Kill Players when Server in low population", typeof (Boolean), this._OnlyKillOnLowPop));
                if (this._OnlyKillOnLowPop) {
                    lstReturn.Add(new CPluginVariable("7. Punishment Settings|Low Population Value", typeof (int), this._LowPopPlayerCount));
                }
                lstReturn.Add(new CPluginVariable("7. Punishment Settings|Use IRO Punishment", typeof (Boolean), this._IROActive));
                if (this._IROActive) {
                    lstReturn.Add(new CPluginVariable("7. Punishment Settings|IRO Timeout Minutes", typeof (Int32), this._IROTimeout));
                    lstReturn.Add(new CPluginVariable("7. Punishment Settings|IRO Punishment Overrides Low Pop", typeof (Boolean), this._IROOverridesLowPop));
                }

                //Email Settings
                lstReturn.Add(new CPluginVariable("8. Email Settings|Send Emails", typeof (bool), this._UseEmail));
                if (this._UseEmail) {
                    lstReturn.Add(new CPluginVariable("8. Email Settings|Email: Use SSL?", typeof (Boolean), this._EmailHandler.UseSSL));
                    lstReturn.Add(new CPluginVariable("8. Email Settings|SMTP-Server address", typeof (String), this._EmailHandler.SMTPServer));
                    lstReturn.Add(new CPluginVariable("8. Email Settings|SMTP-Server port", typeof (int), this._EmailHandler.SMTPPort));
                    lstReturn.Add(new CPluginVariable("8. Email Settings|Sender address", typeof (String), this._EmailHandler.SenderEmail));
                    lstReturn.Add(new CPluginVariable("8. Email Settings|SMTP-Server username", typeof (String), this._EmailHandler.SMTPUser));
                    lstReturn.Add(new CPluginVariable("8. Email Settings|SMTP-Server password", typeof (String), this._EmailHandler.SMTPPassword));
                }

                //TeamSwap Settings
                lstReturn.Add(new CPluginVariable("9. TeamSwap Settings|Require Whitelist for Access", typeof (Boolean), this._RequireTeamswapWhitelist));
                if (this._RequireTeamswapWhitelist) {
                    lstReturn.Add(new CPluginVariable("9. TeamSwap Settings|Auto-Whitelist Count", typeof (String), this._PlayersToAutoWhitelist));
                }
                lstReturn.Add(new CPluginVariable("9. TeamSwap Settings|Ticket Window High", typeof (int), this._TeamSwapTicketWindowHigh));
                lstReturn.Add(new CPluginVariable("9. TeamSwap Settings|Ticket Window Low", typeof (int), this._TeamSwapTicketWindowLow));

                //Admin Assistant Settings
                lstReturn.Add(new CPluginVariable("A10. Admin Assistant Settings|Enable Admin Assistant Perk", typeof (Boolean), this._EnableAdminAssistantPerk));
                lstReturn.Add(new CPluginVariable("A10. Admin Assistant Settings|Minimum Confirmed Reports Per Month", typeof (int), this._MinimumRequiredMonthlyReports));

                //Muting Settings
                lstReturn.Add(new CPluginVariable("A11. Player Mute Settings|On-Player-Muted Message", typeof (String), this._MutedPlayerMuteMessage));
                lstReturn.Add(new CPluginVariable("A11. Player Mute Settings|On-Player-Killed Message", typeof (String), this._MutedPlayerKillMessage));
                lstReturn.Add(new CPluginVariable("A11. Player Mute Settings|On-Player-Kicked Message", typeof (String), this._MutedPlayerKickMessage));
                lstReturn.Add(new CPluginVariable("A11. Player Mute Settings|# Chances to give player before kicking", typeof (int), this._MutedPlayerChances));

                //Message Settings
                lstReturn.Add(new CPluginVariable("A12. Messaging Settings|Display Admin Name in Kick and Ban Announcement", typeof (Boolean), this._ShowAdminNameInSay));
                lstReturn.Add(new CPluginVariable("A12. Messaging Settings|Yell display time seconds", typeof (int), this._YellDuration));
                lstReturn.Add(new CPluginVariable("A12. Messaging Settings|Pre-Message List", typeof (String[]), this._PreMessageList.ToArray()));
                lstReturn.Add(new CPluginVariable("A12. Messaging Settings|Require Use of Pre-Messages", typeof (Boolean), this._RequirePreMessageUse));

                //Ban Settings
                lstReturn.Add(new CPluginVariable("A13. Banning Settings|Use Additional Ban Message", typeof (Boolean), this._UseBanAppend));
                if (this._UseBanAppend) {
                    lstReturn.Add(new CPluginVariable("A13. Banning Settings|Additional Ban Message", typeof (String), this._BanAppend));
                }
                lstReturn.Add(new CPluginVariable("A13. Banning Settings|Procon Ban Admin Name", typeof (String), this._CBanAdminName));
                if (!this._UsingAwa) {
                    lstReturn.Add(new CPluginVariable("A13. Banning Settings|Use Ban Enforcer", typeof (Boolean), this._UseBanEnforcer));
                }
                if (this._UseBanEnforcer) {
                    lstReturn.Add(new CPluginVariable("A13. Banning Settings|Enforce New Bans by NAME", typeof (Boolean), this._DefaultEnforceName));
                    lstReturn.Add(new CPluginVariable("A13. Banning Settings|Enforce New Bans by GUID", typeof (Boolean), this._DefaultEnforceGUID));
                    lstReturn.Add(new CPluginVariable("A13. Banning Settings|Enforce New Bans by IP", typeof (Boolean), this._DefaultEnforceIP));
                    if (this._ThreadsReady) {
                        lstReturn.Add(new CPluginVariable("A13. Banning Settings|NAME Ban Count", typeof (int), (int) this.FetchNameBanCount()));
                        lstReturn.Add(new CPluginVariable("A13. Banning Settings|GUID Ban Count", typeof (int), (int) this.FetchGUIDBanCount()));
                        lstReturn.Add(new CPluginVariable("A13. Banning Settings|IP Ban Count", typeof (int), (int) this.FetchIPBanCount()));
                    }
                }

                //External Command Settings
                lstReturn.Add(new CPluginVariable("A14. External Command Settings|HTTP External Access Key", typeof (String), this._ExternalCommandAccessKey));
                if (!this._UseBanEnforcer && !this._UsingAwa) {
                    lstReturn.Add(new CPluginVariable("A14. External Command Settings|Fetch Actions from Database", typeof (Boolean), this._FetchActionsFromDb));
                }

                lstReturn.Add(new CPluginVariable("A15. VOIP Settings|Server VOIP Address", typeof (String), this._ServerVoipAddress));

                lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Feed MULTIBalancer Whitelist", typeof (Boolean), this._FeedMultiBalancerWhitelist));
                lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Feed Server Reserved Slots", typeof (Boolean), this._FeedServerReservedSlots));
                lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Feed Stat Logger Settings", typeof (Boolean), this._FeedStatLoggerSettings));

                lstReturn.Add(new CPluginVariable("A17. Round Settings|Round Timer: Enable", typeof (Boolean), this._UseRoundTimer));
                if (this._UseRoundTimer) {
                    lstReturn.Add(new CPluginVariable("A17. Round Settings|Round Timer: Round Duration Minutes", typeof (Double), this._RoundTimeMinutes));
                }

                if (this._IsTestingAuthorized) {
                    lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: Enable", typeof (Boolean), this._UseHackerChecker));
                    if (this._UseHackerChecker) {
                        if (this._IsTestingAuthorized) {
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|Hacker-Check Player", typeof (String), ""));
                        }
                        lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: Whitelist", typeof (String[]), this._HackerCheckerWhitelist));
                        lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: DPS Checker: Enable", typeof (Boolean), this._UseDpsChecker));
                        if (this._UseDpsChecker) {
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: DPS Checker: Trigger Level", typeof (Double), this._DpsTriggerLevel));
                        }
                        lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: HSK Checker: Enable", typeof (Boolean), this._UseHskChecker));
                        if (this._UseHskChecker) {
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: HSK Checker: Trigger Level", typeof (Double), this._HskTriggerLevel));
                        }
                    }
                }

                //Server rules Settings
                lstReturn.Add(new CPluginVariable("A19. Server Rules Settings|Rule Print Delay", typeof (Double), this._ServerRulesDelay));
                lstReturn.Add(new CPluginVariable("A19. Server Rules Settings|Rule Print Interval", typeof (Double), this._ServerRulesInterval));
                lstReturn.Add(new CPluginVariable("A19. Server Rules Settings|Server Rule List", typeof (String[]), this._ServerRulesList));

                //Debug settings
                lstReturn.Add(new CPluginVariable("Z99. Debugging|Debug level", typeof (int), this._DebugLevel));
                lstReturn.Add(new CPluginVariable("Z99. Debugging|Debug Soldier Name", typeof (String), this._DebugSoldierName));
                lstReturn.Add(new CPluginVariable("Z99. Debugging|Command Entry", typeof (String), ""));

                //Experimental tools
                if (this._IsTestingAuthorized) {
                    lstReturn.Add(new CPluginVariable("X99. Experimental|Use Experimental Tools", typeof (Boolean), this._UseExperimentalTools));
                    if (this._UseExperimentalTools) {
                        lstReturn.Add(new CPluginVariable("X99. Experimental|Send Query", typeof (String), ""));
                        lstReturn.Add(new CPluginVariable("X99. Experimental|Send Non-Query", typeof (String), ""));
                        lstReturn.Add(new CPluginVariable("X99. Experimental|Use NO EXPLOSIVES Limiter", typeof (Boolean), this._UseWeaponLimiter));
                        if (this._UseWeaponLimiter) {
                            lstReturn.Add(new CPluginVariable("X99. Experimental|NO EXPLOSIVES Weapon String", typeof (String), this._WeaponLimiterString));
                            lstReturn.Add(new CPluginVariable("X99. Experimental|NO EXPLOSIVES Exception String", typeof (String), this._WeaponLimiterExceptionString));
                        }
                        lstReturn.Add(new CPluginVariable("X99. Experimental|Use Grenade Cook Catcher", typeof (Boolean), this._UseGrenadeCookCatcher));
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error While Fetching Settings", e));
            }
            return lstReturn;
        }

        public void SetPluginVariable(String strVariable, String strValue) {
            try {
                //this.ConsoleWrite("'" + strVariable + "' -> '" + strValue + "'");

                if (strVariable == "UpdateSettings") {
                    //Do nothing. Settings page will be updated after return.
                }
                else if (Regex.Match(strVariable, @"Auto-Enable/Keep-Alive").Success) {
                    Boolean autoEnable = Boolean.Parse(strValue);
                    if (autoEnable != this._UseKeepAlive) {
                        if(autoEnable)
                            this.Enable();
                        this._UseKeepAlive = autoEnable;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Auto-Enable/Keep-Alive", typeof (Boolean), this._UseKeepAlive));
                    }
                }
                else if (Regex.Match(strVariable, @"Send Query").Success) {
                    this.SendQuery(strValue, true);
                }
                else if (Regex.Match(strVariable, @"Send Non-Query").Success) {
                    this.SendNonQuery("Experimental Query", strValue, true);
                }
                else if (Regex.Match(strVariable, @"Hacker-Check Player").Success) {
                    if (String.IsNullOrEmpty(strValue) || !this._ThreadsReady) {
                        return;
                    }
                    this.ConsoleWarn("Preparing to hacker check " + strValue);
                    AdKats_Player aPlayer = this.FetchPlayer(false, -1, strValue, null, null);
                    this.FetchPlayerStats(aPlayer);
                    if (aPlayer.stats != null) {
                        this.RunStatSiteHackCheck(aPlayer, true);
                    }
                    else {
                        this.ConsoleError("Stats not found for " + strValue);
                    }
                }
                else if (Regex.Match(strVariable, @"Setting Import").Success) {
                    Int32 tmp = -1;
                    if (int.TryParse(strValue, out tmp)) {
                        if (tmp != -1)
                            this.QueueSettingImport(tmp);
                    }
                    else {
                        this.ConsoleError("Invalid Input for Setting Import");
                    }
                }
                else if (Regex.Match(strVariable, @"Using AdKats WebAdmin").Success) {
                    Boolean tmp = false;
                    if (Boolean.TryParse(strValue, out tmp)) {
                        this._UsingAwa = tmp;

                        //Update necessary settings for AWA use
                        if (this._UsingAwa) {
                            this._UseBanEnforcer = true;
                            this._FetchActionsFromDb = true;
                            this._DbCommunicationWaitHandle.Set();
                        }
                    }
                    else {
                        this.ConsoleError("Invalid Input for Using AdKats WebAdmin");
                    }
                }
                    #region debugging

                else if (Regex.Match(strVariable, @"Command Entry").Success) {
                    if (String.IsNullOrEmpty(strValue)) {
                        return;
                    }
                    //Check if the message is a command
                    if (strValue.StartsWith("@") || strValue.StartsWith("!")) {
                        strValue = strValue.Substring(1);
                    }
                    else if (strValue.StartsWith("/@") || strValue.StartsWith("/!")) {
                        strValue = strValue.Substring(2);
                    }
                    else if (strValue.StartsWith("/")) {
                        strValue = strValue.Substring(1);
                    }
                    AdKatsRecord recordItem = new AdKatsRecord {
                                                                   record_source = AdKatsRecord.Sources.Settings,
                                                                   source_name = "SettingsAdmin"
                                                               };
                    this.CompleteRecord(recordItem, strValue);
                }
                else if (Regex.Match(strVariable, @"Debug level").Success) {
                    Int32 tmp = 2;
                    if (int.TryParse(strValue, out tmp)) {
                        if (tmp != this._DebugLevel) {
                            this._DebugLevel = tmp;
                            //Once setting has been changed, upload the change to database
                            this.QueueSettingForUpload(new CPluginVariable(@"Debug level", typeof (int), this._DebugLevel));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Debug Soldier Name").Success) {
                    if (this.SoldierNameValid(strValue)) {
                        if (strValue != this._DebugSoldierName) {
                            this._DebugSoldierName = strValue;
                            //Once setting has been changed, upload the change to database
                            this.QueueSettingForUpload(new CPluginVariable(@"Debug Soldier Name", typeof (String), this._DebugSoldierName));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Server VOIP Address").Success) {
                    if (strValue != this._ServerVoipAddress) {
                        this._ServerVoipAddress = strValue;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Server VOIP Address", typeof (String), this._ServerVoipAddress));
                    }
                }
                else if (Regex.Match(strVariable, @"Rule Print Delay").Success) {
                    Double delay = Double.Parse(strValue);
                    if (this._ServerRulesDelay != delay) {
                        if (delay <= 0) {
                            this.ConsoleError("Delay cannot be negative.");
                            delay = 1.0;
                        }
                        this._ServerRulesDelay = delay;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Rule Print Delay", typeof (Double), this._ServerRulesDelay));
                    }
                }
                else if (Regex.Match(strVariable, @"Rule Print Interval").Success) {
                    Double interval = Double.Parse(strValue);
                    if (this._ServerRulesInterval != interval) {
                        if (interval <= 0) {
                            this.ConsoleError("Interval cannot be negative.");
                            interval = 5.0;
                        }
                        this._ServerRulesInterval = interval;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Rule Print Interval", typeof (Double), this._ServerRulesInterval));
                    }
                }
                else if (Regex.Match(strVariable, @"Server Rule List").Success) {
                    this._ServerRulesList = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    this.QueueSettingForUpload(new CPluginVariable(@"Server Rule List", typeof (String), CPluginVariable.EncodeStringArray(this._ServerRulesList)));
                }
                else if (Regex.Match(strVariable, @"Feed MULTIBalancer Whitelist").Success) {
                    Boolean feedMTB = Boolean.Parse(strValue);
                    if (feedMTB != this._FeedMultiBalancerWhitelist) {
                        this._FeedMultiBalancerWhitelist = feedMTB;
                        this.FetchUserList();
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Whitelist", typeof (Boolean), this._FeedMultiBalancerWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Server Reserved Slots").Success) {
                    Boolean feedSRS = Boolean.Parse(strValue);
                    if (feedSRS != this._FeedServerReservedSlots) {
                        this._FeedServerReservedSlots = feedSRS;
                        this.FetchUserList();
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Feed Server Reserved Slots", typeof (Boolean), this._FeedServerReservedSlots));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Stat Logger Settings").Success) {
                    Boolean feedSLS = Boolean.Parse(strValue);
                    if (feedSLS != this._FeedStatLoggerSettings) {
                        this._FeedStatLoggerSettings = feedSLS;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Feed Stat Logger Settings", typeof (Boolean), this._FeedStatLoggerSettings));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Experimental Tools").Success) {
                    Boolean useEXP = Boolean.Parse(strValue);
                    if (useEXP != this._UseExperimentalTools) {
                        this._UseExperimentalTools = useEXP;
                        if (this._UseExperimentalTools) {
                            if (this._ThreadsReady) {
                                this.ConsoleWarn("Using experimental tools. Take caution.");
                            }
                        }
                        else {
                            this.ConsoleWarn("Experimental tools disabled.");
                            this._UseWeaponLimiter = false;
                            this._UseGrenadeCookCatcher = false;
                            this._UseHackerChecker = false;
                            this._UseDpsChecker = false;
                            this._UseHskChecker = false;
                        }
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use Experimental Tools", typeof (Boolean), this._UseExperimentalTools));
                        this.QueueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof (Boolean), this._UseWeaponLimiter));
                        this.QueueSettingForUpload(new CPluginVariable(@"Use Hacker Checker", typeof (Boolean), this._UseHackerChecker));
                        this.QueueSettingForUpload(new CPluginVariable(@"Use DPS Checker", typeof (Boolean), this._UseDpsChecker));
                        this.QueueSettingForUpload(new CPluginVariable(@"Use HSK Checker", typeof (Boolean), this._UseHskChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"Round Timer: Enable").Success) {
                    Boolean useTimer = Boolean.Parse(strValue);
                    if (useTimer != this._UseRoundTimer) {
                        this._UseRoundTimer = useTimer;
                        if (this._UseRoundTimer) {
                            if (this._ThreadsReady) {
                                this.ConsoleWarn("Internal Round Timer activated, will enable on next round.");
                            }
                        }
                        else {
                            this.ConsoleWarn("Internal Round Timer disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Round Timer: Enable", typeof (Boolean), this._UseRoundTimer));
                    }
                }
                else if (Regex.Match(strVariable, @"Round Timer: Round Duration Minutes").Success) {
                    Double duration = Double.Parse(strValue);
                    if (this._RoundTimeMinutes != duration) {
                        if (duration <= 0) {
                            duration = 30.0;
                        }
                        this._RoundTimeMinutes = duration;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Round Timer: Round Duration Minutes", typeof (Double), this._RoundTimeMinutes));
                    }
                }
                else if (Regex.Match(strVariable, @"Use NO EXPLOSIVES Limiter").Success) {
                    Boolean useLimiter = Boolean.Parse(strValue);
                    if (useLimiter != this._UseWeaponLimiter) {
                        this._UseWeaponLimiter = useLimiter;
                        if (this._UseWeaponLimiter) {
                            if (this._ThreadsReady) {
                                this.ConsoleWarn("Internal NO EXPLOSIVES punish limit activated.");
                            }
                        }
                        else {
                            this.ConsoleWarn("Internal NO EXPLOSIVES punish limit disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof (Boolean), this._UseWeaponLimiter));
                    }
                }
                else if (Regex.Match(strVariable, @"NO EXPLOSIVES Weapon String").Success) {
                    if (this._WeaponLimiterString != strValue) {
                        if (!String.IsNullOrEmpty(strValue)) {
                            this._WeaponLimiterString = strValue;
                            //Once setting has been changed, upload the change to database
                            this.QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Weapon String", typeof (String), this._WeaponLimiterString));
                        }
                        else {
                            this.ConsoleError("Weapon String cannot be empty.");
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"NO EXPLOSIVES Exception String").Success) {
                    if (this._WeaponLimiterExceptionString != strValue) {
                        if (!String.IsNullOrEmpty(strValue)) {
                            this._WeaponLimiterExceptionString = strValue;
                            //Once setting has been changed, upload the change to database
                            this.QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Exception String", typeof (String), this._WeaponLimiterExceptionString));
                        }
                        else {
                            this.ConsoleError("Weapon exception String cannot be empty.");
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Use Grenade Cook Catcher").Success) {
                    Boolean useCookCatcher = Boolean.Parse(strValue);
                    if (useCookCatcher != this._UseGrenadeCookCatcher) {
                        this._UseGrenadeCookCatcher = useCookCatcher;
                        if (this._UseGrenadeCookCatcher) {
                            if (this._ThreadsReady) {
                                this.ConsoleWarn("Internal Grenade Cook Catcher activated.");
                            }
                        }
                        else {
                            this.ConsoleWarn("Internal Grenade Cook Catcher disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use Grenade Cook Catcher", typeof (Boolean), this._UseGrenadeCookCatcher));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: Enable").Success) {
                    Boolean useHackChecker = Boolean.Parse(strValue);
                    if (useHackChecker != this._UseHackerChecker) {
                        this._UseHackerChecker = useHackChecker;
                        if (this._UseHackerChecker) {
                            if (this._ThreadsReady) {
                                this.ConsoleWarn("Internal Hacker Checker activated.");
                            }
                        }
                        else {
                            this.ConsoleWarn("Internal Hacker Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use Hacker Checker", typeof (Boolean), this._UseHackerChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: Whitelist").Success) {
                    this._HackerCheckerWhitelist = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    this.QueueSettingForUpload(new CPluginVariable(@"Use Hacker Checker", typeof (Boolean), this._UseHackerChecker));
                }
                else if (Regex.Match(strVariable, @"DPS Checker: Enable").Success) {
                    Boolean useDamageChecker = Boolean.Parse(strValue);
                    if (useDamageChecker != this._UseDpsChecker) {
                        this._UseDpsChecker = useDamageChecker;
                        if (this._UseDpsChecker) {
                            if (this._ThreadsReady) {
                                this.ConsoleWarn("Internal Damage Mod Checker activated.");
                            }
                        }
                        else {
                            this.ConsoleWarn("Internal Damage Mod Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use DPS Checker", typeof (Boolean), this._UseDpsChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: DPS Checker: Trigger Level").Success) {
                    Double triggerLevel = Double.Parse(strValue);
                    if (this._DpsTriggerLevel != triggerLevel) {
                        if (triggerLevel <= 0) {
                            triggerLevel = 100.0;
                        }
                        this._DpsTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Trigger Level", typeof (Double), this._DpsTriggerLevel));
                    }
                }
                else if (Regex.Match(strVariable, @"HSK Checker: Enable").Success) {
                    Boolean useAimbotChecker = Boolean.Parse(strValue);
                    if (useAimbotChecker != this._UseHskChecker) {
                        this._UseHskChecker = useAimbotChecker;
                        if (this._UseHskChecker) {
                            if (this._ThreadsReady) {
                                this.ConsoleWarn("Internal Aimbot Checker activated.");
                            }
                        }
                        else {
                            this.ConsoleWarn("Internal Aimbot Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use HSK Checker", typeof (Boolean), this._UseHskChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: HSK Checker: Trigger Level").Success) {
                    Double triggerLevel = Double.Parse(strValue);
                    if (this._HskTriggerLevel != triggerLevel) {
                        if (triggerLevel <= 0) {
                            triggerLevel = 100.0;
                        }
                        this._HskTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Trigger Level", typeof (Double), this._HskTriggerLevel));
                    }
                }
                    #endregion
                    #region HTTP settings

                else if (Regex.Match(strVariable, @"External Access Key").Success) {
                    if (strValue != this._ExternalCommandAccessKey) {
                        this._ExternalCommandAccessKey = strValue;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"External Access Key", typeof (String), this._ExternalCommandAccessKey));
                    }
                }
                else if (Regex.Match(strVariable, @"Fetch Actions from Database").Success) {
                    Boolean fetch;
                    if (fetch = Boolean.Parse(strValue)) {
                        if (fetch != this._FetchActionsFromDb) {
                            this._FetchActionsFromDb = fetch;
                            this._DbCommunicationWaitHandle.Set();
                            //Once setting has been changed, upload the change to database
                            this.QueueSettingForUpload(new CPluginVariable(@"Fetch Actions from Database", typeof (Boolean), this._FetchActionsFromDb));
                        }
                    }
                }
                    #endregion
                    #region ban settings

                else if (Regex.Match(strVariable, @"Use Additional Ban Message").Success) {
                    Boolean use = Boolean.Parse(strValue);
                    if (this._UseBanAppend != use) {
                        this._UseBanAppend = use;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use Additional Ban Message", typeof (Boolean), this._UseBanAppend));
                    }
                }
                else if (Regex.Match(strVariable, @"Additional Ban Message").Success) {
                    if (strValue.Length > 30) {
                        strValue = strValue.Substring(0, 30);
                        this.ConsoleError("Ban append cannot be more than 30 characters.");
                    }
                    if (this._BanAppend != strValue) {
                        this._BanAppend = strValue;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Additional Ban Message", typeof (String), this._BanAppend));
                    }
                }
                else if (Regex.Match(strVariable, @"Procon Ban Admin Name").Success) {
                    if (strValue.Length > 16) {
                        strValue = strValue.Substring(0, 16);
                        this.ConsoleError("Procon ban admin name cannot be more than 16 characters.");
                    }
                    if (this._CBanAdminName != strValue) {
                        this._CBanAdminName = strValue;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Procon Ban Admin Name", typeof (String), this._CBanAdminName));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Ban Enforcer").Success) {
                    Boolean use = Boolean.Parse(strValue);
                    if (this._UseBanEnforcer != use) {
                        this._UseBanEnforcer = use;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use Ban Enforcer", typeof (Boolean), this._UseBanEnforcer));
                        if (this._UseBanEnforcer) {
                            this._FetchActionsFromDb = true;
                            this._DbCommunicationWaitHandle.Set();
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by NAME").Success) {
                    Boolean enforceName = Boolean.Parse(strValue);
                    if (this._DefaultEnforceName != enforceName) {
                        this._DefaultEnforceName = enforceName;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by NAME", typeof (Boolean), this._DefaultEnforceName));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by GUID").Success) {
                    Boolean enforceGUID = Boolean.Parse(strValue);
                    if (this._DefaultEnforceGUID != enforceGUID) {
                        this._DefaultEnforceGUID = enforceGUID;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by GUID", typeof (Boolean), this._DefaultEnforceGUID));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by IP").Success) {
                    Boolean enforceIP = Boolean.Parse(strValue);
                    if (this._DefaultEnforceIP != enforceIP) {
                        this._DefaultEnforceIP = enforceIP;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by IP", typeof (Boolean), this._DefaultEnforceIP));
                    }
                }
                    #endregion
                    #region In-Game Command Settings

                else if (Regex.Match(strVariable, @"Minimum Required Reason Length").Success) {
                    Int32 required = Int32.Parse(strValue);
                    if (this._RequiredReasonLength != required) {
                        this._RequiredReasonLength = required;
                        if (this._RequiredReasonLength < 1) {
                            this._RequiredReasonLength = 1;
                        }
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Minimum Required Reason Length", typeof (Int32), this._RequiredReasonLength));
                    }
                }
                else if (strVariable.StartsWith("USR")) {
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

                    AdKatsUser aUser = null;
                    if (this._UserCache.TryGetValue(user_id, out aUser)) {
                        switch (section) {
                            case "User Email":
                                if (String.IsNullOrEmpty(strValue) || Regex.IsMatch(strValue, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$")) {
                                    aUser.user_email = strValue;
                                }
                                else {
                                    this.ConsoleError(strValue + " is an invalid email address.");
                                    return;
                                }
                                //Reupload the user
                                this.QueueUserForUpload(aUser);
                                break;
                            case "User Phone":
                                aUser.user_phone = strValue;
                                //Reupload the user
                                this.QueueUserForUpload(aUser);
                                break;
                            case "User Role":
                                AdKats_Role aRole = null;
                                if (this._RoleNameDictionary.TryGetValue(strValue, out aRole)) {
                                    aUser.user_role = aRole;
                                }
                                else {
                                    this.ConsoleError("Role " + strValue + " not found.");
                                    return;
                                }
                                //Reupload the user
                                this.QueueUserForUpload(aUser);
                                break;
                            case "Delete User?":
                                if (strValue.ToLower() == "delete") {
                                    this.QueueUserForRemoval(aUser);
                                }
                                break;
                            case "Add Soldier?":
                                //Attempt to fetch the soldier
                                if (!String.IsNullOrEmpty(strValue) && this.SoldierNameValid(strValue)) {
                                    AdKats_Player aPlayer = this.FetchPlayer(true, -1, strValue, null, null);
                                    if (aPlayer != null) {
                                        Boolean playerDuplicate = false;
                                        //Make sure the player is not already assigned to another user
                                        lock (this._UserCache) {
                                            foreach (AdKatsUser innerUser in this._UserCache.Values) {
                                                if (innerUser.soldierDictionary.ContainsKey(aPlayer.player_id)) {
                                                    playerDuplicate = true;
                                                    break;
                                                }
                                            }
                                        }
                                        if (!playerDuplicate) {
                                            if (aUser.soldierDictionary.ContainsKey(aPlayer.player_id)) {
                                                aUser.soldierDictionary.Remove(aPlayer.player_id);
                                            }
                                            aUser.soldierDictionary.Add(aPlayer.player_id, aPlayer);
                                        }
                                        else {
                                            this.ConsoleError("Player already assigned to another user, unable to assign to user.");
                                        }
                                    }
                                    else {
                                        this.ConsoleError("Player not found in database, unable to assign to user.");
                                    }
                                }
                                //Reupload the user
                                this.QueueUserForUpload(aUser);
                                break;
                            case "Soldiers":
                                if (strVariable.Contains("Delete Soldier?") && strValue.ToLower() == "delete") {
                                    String player_id_str = commandSplit[3].Trim();
                                    Int64 player_id = Int64.Parse(player_id_str);
                                    aUser.soldierDictionary.Remove(player_id);
                                    //Reupload the user
                                    this.QueueUserForUpload(aUser);
                                }
                                break;
                            default:
                                this.ConsoleError("Section " + section + " not found.");
                                break;
                        }
                    }
                }
                else if (strVariable.StartsWith("CDE")) {
                    //Trim off all but the command ID and section
                    //5. Command List|CDE1 | Kill Player | Active
                    //5. Command List|CDE1 | Kill Player | Logging
                    //5. Command List|CDE1 | Kill Player | Text

                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String command_id_str = commandSplit[0].TrimStart("CDE".ToCharArray()).Trim();
                    Int32 command_id = Int32.Parse(command_id_str);
                    String section = commandSplit[2].Trim();

                    AdKatsCommand command = null;
                    if (this._CommandIDDictionary.TryGetValue(command_id, out command)) {
                        if (section == "Active") {
                            //Check for valid value
                            if (strValue == "Active") {
                                command.command_active = AdKatsCommand.CommandActive.Active;
                            }
                            else if (strValue == "Disabled") {
                                command.command_active = AdKatsCommand.CommandActive.Disabled;
                            }
                            else if (strValue == "Invisible") {
                                command.command_active = AdKatsCommand.CommandActive.Invisible;
                            }
                            else {
                                this.ConsoleError("Activity setting " + strValue + " was invalid.");
                                return;
                            }
                        }
                        else if (section == "Logging") {
                            //Check for valid value
                            switch (strValue) {
                                case "Log":
                                    command.command_logging = AdKatsCommand.CommandLogging.Log;
                                    break;
                                case "Mandatory":
                                    command.command_logging = AdKatsCommand.CommandLogging.Mandatory;
                                    break;
                                case "Ignore":
                                    command.command_logging = AdKatsCommand.CommandLogging.Ignore;
                                    break;
                                case "Unable":
                                    command.command_logging = AdKatsCommand.CommandLogging.Unable;
                                    break;
                                default:
                                    this.ConsoleError("Logging setting " + strValue + " was invalid.");
                                    return;
                            }
                        }
                        else if (section == "Text") {
                            if (String.IsNullOrEmpty(strValue)) {
                                this.ConsoleError("Command text cannot be blank.");
                                return;
                            }
                            //Make sure command text only contains alphanumeric chars, underscores, and dashes
                            Regex rgx = new Regex("[^a-zA-Z0-9_-]");
                            strValue = rgx.Replace(strValue, "");
                            //Check to make sure text is not a duplicate
                            foreach (AdKatsCommand testCommand in this._CommandNameDictionary.Values) {
                                if (testCommand.command_text == strValue) {
                                    this.ConsoleError("Command text cannot be the same as another command.");
                                    return;
                                }
                            }
                            //Assign the command text
                            command.command_text = strValue;
                        }
                        else {
                            this.ConsoleError("Section " + section + " not understood.");
                            return;
                        }
                        //Upload the command changes
                        this.QueueCommandForUpload(command);
                    }
                    else {
                        this.ConsoleError("Command " + command_id + " not found in command dictionary.");
                    }
                }
                else if (strVariable.StartsWith("RLE")) {
                    //Trim off all but the command ID and section
                    //RLE1 | Default Guest | CDE3 | Kill Player

                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String roleIDStr = commandSplit[0].TrimStart("RLE".ToCharArray()).Trim();
                    Int32 roleID = Int32.Parse(roleIDStr);

                    //If second section is a command prefix, this is the allow/deny clause
                    if (commandSplit[2].Trim().StartsWith("CDE")) {
                        String commandIDStr = commandSplit[2].Trim().TrimStart("CDE".ToCharArray());
                        Int32 commandID = Int32.Parse(commandIDStr);

                        //Fetch needed role
                        AdKats_Role aRole = null;
                        if (this._RoleIDDictionary.TryGetValue(roleID, out aRole)) {
                            //Fetch needed command
                            AdKatsCommand aCommand = null;
                            if (this._CommandIDDictionary.TryGetValue(commandID, out aCommand)) {
                                switch (strValue.ToLower()) {
                                    case "allow":
                                        lock (aRole.allowedCommands) {
                                            if (!aRole.allowedCommands.ContainsKey(aCommand.command_key)) {
                                                aRole.allowedCommands.Add(aCommand.command_key, aCommand);
                                            }
                                        }
                                        this.QueueRoleForUpload(aRole);
                                        break;
                                    case "deny":
                                        lock (aRole.allowedCommands) {
                                            aRole.allowedCommands.Remove(aCommand.command_key);
                                        }
                                        this.QueueRoleForUpload(aRole);
                                        break;
                                    default:
                                        this.ConsoleError("Unknown setting when assigning command allowance.");
                                        return;
                                }
                            }
                            else {
                                this.ConsoleError("Command " + commandID + " not found in command dictionary.");
                            }
                        }
                        else {
                            this.ConsoleError("Role " + roleID + " not found in role dictionary.");
                        }
                    }
                    else if (commandSplit[2].Contains("Delete Role?") && strValue.ToLower() == "delete") {
                        //Fetch needed role
                        AdKats_Role aRole = null;
                        if (this._RoleIDDictionary.TryGetValue(roleID, out aRole)) {
                            this.QueueRoleForRemoval(aRole);
                        }
                        else {
                            this.ConsoleError("Unable to fetch role for deletion.");
                        }
                    }
                }
                    #endregion
                    #region punishment settings

                else if (Regex.Match(strVariable, @"Punishment Hierarchy").Success) {
                    this._PunishmentHierarchy = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    this.QueueSettingForUpload(new CPluginVariable(@"Punishment Hierarchy", typeof (String), CPluginVariable.EncodeStringArray(this._PunishmentHierarchy)));
                }
                else if (Regex.Match(strVariable, @"Combine Server Punishments").Success) {
                    Boolean combine = Boolean.Parse(strValue);
                    if (this._CombineServerPunishments != combine) {
                        this._CombineServerPunishments = combine;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Combine Server Punishments", typeof (Boolean), this._CombineServerPunishments));
                    }
                }
                else if (Regex.Match(strVariable, @"Only Kill Players when Server in low population").Success) {
                    Boolean onlyKill = Boolean.Parse(strValue);
                    if (onlyKill != this._OnlyKillOnLowPop) {
                        this._OnlyKillOnLowPop = onlyKill;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Only Kill Players when Server in low population", typeof (Boolean), this._OnlyKillOnLowPop));
                    }
                }
                else if (Regex.Match(strVariable, @"Low Population Value").Success) {
                    Int32 lowPop = Int32.Parse(strValue);
                    if (lowPop != this._LowPopPlayerCount) {
                        this._LowPopPlayerCount = lowPop;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Low Population Value", typeof (Int32), this._LowPopPlayerCount));
                    }
                }
                else if (Regex.Match(strVariable, @"Use IRO Punishment").Success) {
                    Boolean iro = Boolean.Parse(strValue);
                    if (iro != this._IROActive) {
                        this._IROActive = iro;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Use IRO Punishment", typeof (Boolean), this._IROActive));
                    }
                }
                else if (Regex.Match(strVariable, @"IRO Punishment Overrides Low Pop").Success) {
                    Boolean overrideIRO = Boolean.Parse(strValue);
                    if (overrideIRO != this._IROOverridesLowPop) {
                        this._IROOverridesLowPop = overrideIRO;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"IRO Punishment Overrides Low Pop", typeof (Boolean), this._IROOverridesLowPop));
                    }
                }
                else if (Regex.Match(strVariable, @"IRO Timeout Minutes").Success) {
                    Int32 timeout = Int32.Parse(strValue);
                    if (timeout != this._IROTimeout) {
                        this._IROTimeout = timeout;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"IRO Timeout (Minutes)", typeof (Int32), this._IROTimeout));
                    }
                }
                    #endregion
                    #region sql settings

                else if (Regex.Match(strVariable, @"MySQL Hostname").Success) {
                    _MySqlHostname = strValue;
                    this._DbSettingsChanged = true;
                    this._DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Port").Success) {
                    Int32 tmp = 3306;
                    int.TryParse(strValue, out tmp);
                    if (tmp > 0 && tmp < 65536) {
                        _MySqlPort = strValue;
                        this._DbSettingsChanged = true;
                        this._DbCommunicationWaitHandle.Set();
                    }
                    else {
                        this.ConsoleError("Invalid value for MySQL Port: '" + strValue + "'. Must be number between 1 and 65535!");
                    }
                }
                else if (Regex.Match(strVariable, @"MySQL Database").Success) {
                    this._MySqlDatabaseName = strValue;
                    this._DbSettingsChanged = true;
                    this._DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Username").Success) {
                    _MySqlUsername = strValue;
                    this._DbSettingsChanged = true;
                    this._DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Password").Success) {
                    _MySqlPassword = strValue;
                    this._DbSettingsChanged = true;
                    this._DbCommunicationWaitHandle.Set();
                }
                    #endregion
                    #region email settings

                else if (Regex.Match(strVariable, @"Send Emails").Success) {
                    //Disabled
                    this._UseEmail = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    this.QueueSettingForUpload(new CPluginVariable("Send Emails", typeof (Boolean), this._UseEmail));
                }
                else if (Regex.Match(strVariable, @"Admin Request Email?").Success) {
                    //this.blNotifyEmail = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    //this.queueSettingForUpload(new CPluginVariable("Admin Request Email?", typeof(String), strValue));
                }
                else if (Regex.Match(strVariable, @"Use SSL?").Success) {
                    this._EmailHandler.UseSSL = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    //this.queueSettingForUpload(new CPluginVariable("Use SSL?", typeof(Boolean), this.emailHandler.UseSSL));
                }
                else if (Regex.Match(strVariable, @"SMTP-Server address").Success) {
                    this._EmailHandler.SMTPServer = strValue;
                    //Once setting has been changed, upload the change to database
                    //this.queueSettingForUpload(new CPluginVariable("SMTP-Server address", typeof(String), strValue));
                }
                else if (Regex.Match(strVariable, @"SMTP-Server port").Success) {
                    Int32 iPort = Int32.Parse(strValue);
                    if (iPort > 0) {
                        this._EmailHandler.SMTPPort = iPort;
                        //Once setting has been changed, upload the change to database
                        //this.queueSettingForUpload(new CPluginVariable("SMTP-Server port", typeof(Int32), iPort));
                    }
                }
                else if (Regex.Match(strVariable, @"Sender address").Success) {
                    if (string.IsNullOrEmpty(strValue)) {
                        this._EmailHandler.SenderEmail = "SENDER_CANNOT_BE_EMPTY";
                        this.ConsoleError("No sender for email was given! Canceling Operation.");
                    }
                    else {
                        this._EmailHandler.SenderEmail = strValue;
                        //Once setting has been changed, upload the change to database
                        //this.queueSettingForUpload(new CPluginVariable("Sender address", typeof(String), strValue));
                    }
                }
                else if (Regex.Match(strVariable, @"SMTP-Server username").Success) {
                    if (string.IsNullOrEmpty(strValue)) {
                        this._EmailHandler.SMTPUser = "SMTP_USERNAME_CANNOT_BE_EMPTY";
                        this.ConsoleError("No username for SMTP was given! Canceling Operation.");
                    }
                    else {
                        this._EmailHandler.SMTPUser = strValue;
                        //Once setting has been changed, upload the change to database
                        //this.queueSettingForUpload(new CPluginVariable("SMTP-Server username", typeof(String), strValue));
                    }
                }
                else if (Regex.Match(strVariable, @"SMTP-Server password").Success) {
                    if (string.IsNullOrEmpty(strValue)) {
                        this._EmailHandler.SMTPPassword = "SMTP_PASSWORD_CANNOT_BE_EMPTY";
                        this.ConsoleError("No password for SMTP was given! Canceling Operation.");
                    }
                    else {
                        this._EmailHandler.SMTPPassword = strValue;
                        //Once setting has been changed, upload the change to database
                        //this.queueSettingForUpload(new CPluginVariable("SMTP-Server password", typeof(String), strValue));
                    }
                }
                    #endregion
                    #region mute settings

                else if (Regex.Match(strVariable, @"On-Player-Muted Message").Success) {
                    if (this._MutedPlayerMuteMessage != strValue) {
                        this._MutedPlayerMuteMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"On-Player-Muted Message", typeof (String), this._MutedPlayerMuteMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Killed Message").Success) {
                    if (this._MutedPlayerKillMessage != strValue) {
                        this._MutedPlayerKillMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"On-Player-Killed Message", typeof (String), this._MutedPlayerKillMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Kicked Message").Success) {
                    if (this._MutedPlayerKickMessage != strValue) {
                        this._MutedPlayerKickMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"On-Player-Kicked Message", typeof (String), this._MutedPlayerKickMessage));
                    }
                }
                if (Regex.Match(strVariable, @"# Chances to give player before kicking").Success) {
                    Int32 tmp = 5;
                    int.TryParse(strValue, out tmp);
                    if (this._MutedPlayerChances != tmp) {
                        this._MutedPlayerChances = tmp;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"# Chances to give player before kicking", typeof (Int32), this._MutedPlayerChances));
                    }
                }
                    #endregion
                    #region TeamSwap settings

                else if (Regex.Match(strVariable, @"Require Whitelist for Access").Success) {
                    Boolean require = Boolean.Parse(strValue);
                    if (this._RequireTeamswapWhitelist != require) {
                        this._RequireTeamswapWhitelist = require;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Require Whitelist for Access", typeof (Boolean), this._RequireTeamswapWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Whitelist Count").Success) {
                    Int32 tmp = 1;
                    int.TryParse(strValue, out tmp);
                    if (tmp != this._PlayersToAutoWhitelist) {
                        if (tmp < 0)
                            tmp = 0;
                        this._PlayersToAutoWhitelist = tmp;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Auto-Whitelist Count", typeof (Int32), this._PlayersToAutoWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Ticket Window High").Success) {
                    Int32 tmp = 2;
                    int.TryParse(strValue, out tmp);
                    if (tmp != this._TeamSwapTicketWindowHigh) {
                        this._TeamSwapTicketWindowHigh = tmp;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Ticket Window High", typeof (Int32), this._TeamSwapTicketWindowHigh));
                    }
                }
                else if (Regex.Match(strVariable, @"Ticket Window Low").Success) {
                    Int32 tmp = 2;
                    int.TryParse(strValue, out tmp);
                    if (tmp != this._TeamSwapTicketWindowLow) {
                        this._TeamSwapTicketWindowLow = tmp;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Ticket Window Low", typeof (Int32), this._TeamSwapTicketWindowLow));
                    }
                }
                    #endregion
                    #region Admin Assistants

                else if (Regex.Match(strVariable, @"Enable Admin Assistant Perk").Success) {
                    Boolean enableAA = Boolean.Parse(strValue);
                    if (this._EnableAdminAssistantPerk != enableAA) {
                        this._EnableAdminAssistantPerk = enableAA;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistant Perk", typeof (Boolean), this._EnableAdminAssistantPerk));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Confirmed Reports Per Month").Success) {
                    Int32 monthlyReports = Int32.Parse(strValue);
                    if (this._MinimumRequiredMonthlyReports != monthlyReports) {
                        this._MinimumRequiredMonthlyReports = monthlyReports;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Minimum Confirmed Reports Per Month", typeof (Int32), this._MinimumRequiredMonthlyReports));
                    }
                }
                    #endregion
                    #region Messaging Settings

                else if (Regex.Match(strVariable, @"Yell display time seconds").Success) {
                    Int32 yellTime = Int32.Parse(strValue);
                    if (this._YellDuration != yellTime) {
                        this._YellDuration = yellTime;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Yell display time seconds", typeof (Int32), this._YellDuration));
                    }
                }
                else if (Regex.Match(strVariable, @"Pre-Message List").Success) {
                    this._PreMessageList = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    //Once setting has been changed, upload the change to database
                    this.QueueSettingForUpload(new CPluginVariable(@"Pre-Message List", typeof (String), CPluginVariable.EncodeStringArray(this._PreMessageList.ToArray())));
                }
                else if (Regex.Match(strVariable, @"Require Use of Pre-Messages").Success) {
                    Boolean require = Boolean.Parse(strValue);
                    if (require != this._RequirePreMessageUse) {
                        this._RequirePreMessageUse = require;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Require Use of Pre-Messages", typeof (Boolean), this._RequirePreMessageUse));
                    }
                }
                else if (Regex.Match(strVariable, @"Display Admin Name in Kick and Ban Announcement").Success) {
                    Boolean display = Boolean.Parse(strValue);
                    if (display != this._ShowAdminNameInSay) {
                        this._ShowAdminNameInSay = display;
                        //Once setting has been changed, upload the change to database
                        this.QueueSettingForUpload(new CPluginVariable(@"Display Admin Name in Kick and Ban Announcement", typeof (Boolean), this._ShowAdminNameInSay));
                    }
                }
                    #endregion
                    #region access settings

                else if (Regex.Match(strVariable, @"Add User").Success) {
                    if (this.SoldierNameValid(strValue)) {
                        //Create the access objectdd
                        AdKatsUser user = new AdKatsUser {
                                                             user_name = strValue
                                                         };
                        //Queue it for processing
                        this.QueueUserForUpload(user);
                    }
                    else {
                        this.ConsoleError("User name had invalid formatting, please try again.");
                    }
                }
                else if (Regex.Match(strVariable, @"Add Role").Success) {
                    if (!String.IsNullOrEmpty(strValue)) {
                        String roleName = new Regex("[^a-zA-Z0-9 _-]").Replace(strValue, "");
                        String roleKey = roleName.Replace(' ', '_');
                        if (!String.IsNullOrEmpty(roleName) && !String.IsNullOrEmpty(roleKey)) {
                            AdKats_Role aRole = new AdKats_Role {
                                                                    role_key = roleKey,
                                                                    role_name = roleName
                                                                };
                            //By default we should include all commands as allowed
                            lock (this._CommandNameDictionary) {
                                foreach (AdKatsCommand aCommand in this._CommandNameDictionary.Values) {
                                    aRole.allowedCommands.Add(aCommand.command_key, aCommand);
                                }
                            }
                            //Queue it for upload
                            this.QueueRoleForUpload(aRole);
                        }
                        else {
                            this.ConsoleError("Role name had invalid characters, please try again.");
                        }
                    }
                }

                #endregion
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error occured while updating AdKats settings.", e));
            }
        }

        #endregion

        #region Threading

        public void InitWaitHandles() {
            //Initializes all wait handles 
            this._TeamswapWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._PlayerListProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._KillProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._PlayerListUpdateWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._MessageParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._CommandParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._DbCommunicationWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._ActionHandlingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._BanEnforcerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._HackerCheckerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._ServerInfoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._StatLoggerStatusWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this._HttpFetchWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void SetAllHandles() {
            //Opens all handles to make sure all threads complete one loop
            this._TeamswapWaitHandle.Set();
            this._PlayerListProcessingWaitHandle.Set();
            this._PlayerListUpdateWaitHandle.Set();
            this._MessageParsingWaitHandle.Set();
            this._CommandParsingWaitHandle.Set();
            this._DbCommunicationWaitHandle.Set();
            this._ActionHandlingWaitHandle.Set();
            this._BanEnforcerWaitHandle.Set();
            this._HackerCheckerWaitHandle.Set();
            this._ServerInfoWaitHandle.Set();
            this._StatLoggerStatusWaitHandle.Set();
        }

        public void InitThreads() {
            try {
                //Creats all threads with their starting methods and set to run in the background
                this._PlayerListingThread = new Thread(new ThreadStart(PlayerListingThreadLoop)) {
                                                                                                     IsBackground = true
                                                                                                 };

                this._KillProcessingThread = new Thread(new ThreadStart(KillProcessingThreadLoop)) {
                                                                                                       IsBackground = true
                                                                                                   };

                this._MessageProcessingThread = new Thread(new ThreadStart(MessagingThreadLoop)) {
                                                                                                     IsBackground = true
                                                                                                 };

                this._CommandParsingThread = new Thread(new ThreadStart(CommandParsingThreadLoop)) {
                                                                                                       IsBackground = true
                                                                                                   };

                this._DatabaseCommunicationThread = new Thread(new ThreadStart(DatabaseCommThreadLoop)) {
                                                                                                            IsBackground = true
                                                                                                        };

                this._ActionHandlingThread = new Thread(new ThreadStart(ActionHandlingThreadLoop)) {
                                                                                                       IsBackground = true
                                                                                                   };

                this._TeamSwapThread = new Thread(new ThreadStart(TeamswapThreadLoop)) {
                                                                                           IsBackground = true
                                                                                       };

                this._BanEnforcerThread = new Thread(new ThreadStart(BanEnforcerThreadLoop)) {
                                                                                                 IsBackground = true
                                                                                             };

                this._HackerCheckerThread = new Thread(new ThreadStart(HackerCheckerThreadLoop)) {
                                                                                                     IsBackground = true
                                                                                                 };
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error occured while initializing threads.", e));
            }
        }

        public void StartThreads() {
            this.DebugWrite("Entering StartThreads", 7);
            try {
                //Start the main thread
                //DB Comm is the heart of AdKats, everything revolves around that thread
                this._DatabaseCommunicationThread.Start();
                //Other threads are started within the db comm thread
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while starting processing threads.", e));
            }
            this.DebugWrite("Exiting StartThreads", 7);
        }

        public Boolean AllThreadsReady() {
            this.DebugWrite("Entering allThreadsReady", 7);
            Boolean ready = true;
            try {
                if (this._TeamswapWaitHandle.WaitOne(0)) {
                    this.DebugWrite("TeamSwap not ready.", 7);
                    ready = false;
                }
                if (this._MessageParsingWaitHandle.WaitOne(0)) {
                    this.DebugWrite("messaging not ready.", 7);
                    ready = false;
                }
                if (this._CommandParsingWaitHandle.WaitOne(0)) {
                    this.DebugWrite("command parsing not ready.", 7);
                    ready = false;
                }
                if (this._DbCommunicationWaitHandle.WaitOne(0)) {
                    this.DebugWrite("db comm not ready.", 7);
                    ready = false;
                }
                if (this._ActionHandlingWaitHandle.WaitOne(0)) {
                    this.DebugWrite("action handling not ready.", 7);
                    ready = false;
                }
                if (this._BanEnforcerWaitHandle.WaitOne(0)) {
                    this.DebugWrite("ban enforcer not ready.", 7);
                    ready = false;
                }
                if (this._HackerCheckerWaitHandle.WaitOne(0)) {
                    this.DebugWrite("hacker checker not ready.", 7);
                    ready = false;
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while checking if all threads ready.", e));
            }
            this.DebugWrite("Exiting allThreadsReady", 7);
            return ready;
        }

        #endregion

        #region Procon Events

        private void Disable() {
            //Call Disable
            this.ExecuteCommand("procon.protected.plugins.enable", "AdKats", "False");
            //Set enable false
            this._IsEnabled = false;
            this._ThreadsReady = false;
        }

        private void Enable() {
            //Call Enable
            this.ExecuteCommand("procon.protected.plugins.enable", "AdKats", "True");
        }

        public void OnPluginLoadingEnv(List<String> lstPluginEnv) {
            foreach (String env in lstPluginEnv) {
                this.DebugWrite("^9OnPluginLoadingEnv: " + env, 7);
            }
            switch (lstPluginEnv[1]) {
                case "BF3":
                    _GameVersion = GameVersion.BF3;
                    break;
                case "BF4":
                    _GameVersion = GameVersion.BF4;
                    break;
            }
            this.DebugWrite("^1Game Version: " + _GameVersion, 1);
        }

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
            this.DebugWrite("Entering OnPluginLoaded", 7);
            try {
                //Set the server IP
                this._ServerIP = strHostName + ":" + strPort;
                //Register all events
                this.RegisterEvents(this.GetType().Name, "OnVersion", "OnServerInfo", "OnListPlayers", "OnPunkbusterPlayerInfo", "OnReservedSlotsList", "OnPlayerKilled", "OnPlayerSpawned", "OnPlayerTeamChange", "OnPlayerJoin", "OnPlayerLeft", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnLevelLoaded", "OnBanAdded", "OnBanRemoved", "OnBanListClear", "OnBanListSave", "OnBanListLoad", "OnBanList", "OnEndRound", "OnSpectatorListLoad", "OnSpectatorListSave", "OnSpectatorListPlayerAdded", "OnSpectatorListPlayerRemoved", "OnSpectatorListCleared", "OnSpectatorListList", "OnGameAdminLoad", "OnGameAdminSave", "OnGameAdminPlayerAdded", "OnGameAdminPlayerRemoved", "OnGameAdminCleared", "OnGameAdminList", "OnFairFight", "OnIsHitIndicator", "OnCommander", "OnForceReloadWholeMags", "OnServerType", "OnMaxSpectators");
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("FATAL ERROR on plugin load.", e));
            }
            this.DebugWrite("Exiting OnPluginLoaded", 7);
        }

        public override void OnVersion(String serverType, String version) {
            this._GamePatchVersion = version;
        }

        public override void OnFairFight(bool isEnabled) {
            this._FairFightEnabled = isEnabled;
        }

        public override void OnIsHitIndicator(bool isEnabled) {
            this._HitIndicatorEnabled = isEnabled;
        }

        public override void OnCommander(bool isEnabled) {
            this._CommanderEnabled = isEnabled;
        }

        public override void OnForceReloadWholeMags(bool isEnabled) {
            this._ForceReloadWholeMags = isEnabled;
        }

        public override void OnServerType(String value) {
            this._ServerType = value;
        }

        public override void OnMaxSpectators(Int32 limit) {
            this._MaxSpectators = limit;
        }

        public override void OnSpectatorListLoad() {
        }

        public override void OnSpectatorListSave() {
        }

        public override void OnSpectatorListPlayerAdded(String soldierName) {
        }

        public override void OnSpectatorListPlayerRemoved(String soldierName) {
        }

        public override void OnSpectatorListCleared() {
        }

        public override void OnSpectatorListList(List<String> soldierNames) {
        }

        public override void OnGameAdminLoad() {
        }

        public override void OnGameAdminSave() {
        }

        public override void OnGameAdminPlayerAdded(String soldierName) {
        }

        public override void OnGameAdminPlayerRemoved(String soldierName) {
        }

        public override void OnGameAdminCleared() {
        }

        public override void OnGameAdminList(List<String> soldierNames) {
        }

        //DONE
        public void OnPluginEnable() {
            try {
                //If the finalizer is still alive, inform the user and disable
                if (this._Finalizer != null && this._Finalizer.IsAlive) {
                    ConsoleError("Cannot enable plugin while it is shutting down. Please Wait.");
                    //Disable the plugin
                    this.Disable();
                    return;
                }
                //Create a new thread to activate the plugin
                this._Activator = new Thread(new ThreadStart(delegate {
                    try {
                        //Initialize the stat library
                        this._StatLibrary = new StatLibrary(this);

                        this.ConsoleWrite("Waiting a few seconds for requirements and other plugins to initialize, please wait...");
                        //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                        for (Int32 index = 5; index > 0; index--) {
                            this.DebugWrite(index + "...", 1);
                            Thread.Sleep(1000);
                        }

                        //Don't directly depend on stat logger being controllable at this time, connection is unstable
                        /*if (this.useDatabase)
                        {
                            //Confirm Stat Logger active and properly configured
                            this.ConsoleWrite("Confirming proper setup for CChatGUIDStatsLoggerBF3, please wait...");

                            if (this.gameVersion == GameVersion.BF3)
                            {
                                if (this.confirmStatLoggerSetup())
                                {
                                    this.ConsoleSuccess("^bCChatGUIDStatsLoggerBF3^n enabled and active!");
                                }
                                else
                                {
                                    //Stat logger could not be enabled or managed
                                    this.ConsoleWarn("The stat logger plugin could not be found or controlled. Running AdKats in backup mode.");
                                    return;
                                }
                            }
                        }*/

                        //Inform of IP
                        this.ConsoleSuccess("Server IP is " + this._ServerIP + "!");

                        //Set the enabled variable
                        this._IsEnabled = true;

                        //Init and start all the threads
                        this.InitWaitHandles();
                        this.SetAllHandles();
                        this.InitThreads();
                        this.StartThreads();
                    }
                    catch (Exception e) {
                        this.HandleException(new AdKatsException("Error while enabling AdKats.", e));
                    }
                }));

                this.ConsoleWrite("^b^2Enabled!^n^0 Beginning startup sequence...");
                //Start the thread
                this._Activator.Start();
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while initializing activator thread.", e));
            }
        }

        //DONE
        public void OnPluginDisable() {
            //If the plugin is already disabling then cancel
            if (this._Finalizer != null && this._Finalizer.IsAlive)
                return;
            try {
                //Create a new thread to disabled the plugin
                this._Finalizer = new Thread(new ThreadStart(delegate {
                    try {
                        //Disable settings
                        this._IsEnabled = false;
                        this._ThreadsReady = false;
                        //Remove the available indicator
                        this.UnregisterCommand(_AdKatsAvailableIndicator);
                        //Open all handles. Threads will finish on their own.
                        this.SetAllHandles();

                        //Reset all caches and storage
                        if (this._UserRemovalQueue != null)
                            this._UserRemovalQueue.Clear();
                        if (this._UserUploadQueue != null)
                            this._UserUploadQueue.Clear();
                        if (this._TeamswapForceMoveQueue != null)
                            this._TeamswapForceMoveQueue.Clear();
                        if (this._TeamswapOnDeathCheckingQueue != null)
                            this._TeamswapOnDeathCheckingQueue.Clear();
                        if (this._TeamswapOnDeathMoveDic != null)
                            this._TeamswapOnDeathMoveDic.Clear();
                        if (this._UnparsedCommandQueue != null)
                            this._UnparsedCommandQueue.Clear();
                        if (this._UnparsedMessageQueue != null)
                            this._UnparsedMessageQueue.Clear();
                        if (this._UnprocessedActionQueue != null)
                            this._UnprocessedActionQueue.Clear();
                        if (this._UnprocessedRecordQueue != null)
                            this._UnprocessedRecordQueue.Clear();
                        if (this._BanEnforcerCheckingQueue != null)
                            this._BanEnforcerCheckingQueue.Clear();
                        this._ServerID = -1;
                        this._ToldCol = false;
                        this._RuPlayerCount = 0;
                        if (this._RuMoveQueue != null)
                            this._RuMoveQueue.Clear();
                        this._UsPlayerCount = 0;
                        if (this._UsMoveQueue != null)
                            this._UsMoveQueue.Clear();
                        if (this._RoundCookers != null)
                            this._RoundCookers.Clear();
                        if (this._RoundReports != null)
                            this._RoundReports.Clear();
                        if (this._RoundMutedPlayers != null)
                            this._RoundMutedPlayers.Clear();
                        if (this._PlayerDictionary != null)
                            this._PlayerDictionary.Clear();
                        if (this._UserCache != null)
                            this._UserCache.Clear();
                        if (this.FrostbitePlayerInfoList != null)
                            this.FrostbitePlayerInfoList.Clear();
                        if (this._CBanProcessingQueue != null)
                            this._CBanProcessingQueue.Clear();
                        if (this._BanEnforcerProcessingQueue != null)
                            this._BanEnforcerProcessingQueue.Clear();
                        if (this._ActOnSpawnDictionary != null)
                            this._ActOnSpawnDictionary.Clear();
                        if (this._ActionConfirmDic != null)
                            this._ActionConfirmDic.Clear();
                        if (this._TeamswapRoundWhitelist != null)
                            this._TeamswapRoundWhitelist.Clear();
                        //Now that plugin is disabled, update the settings page to reflect
                        this.UpdateSettingPage();
                        ConsoleWrite("^b^1AdKats " + this.GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e) {
                        this.HandleException(new AdKatsException("Error occured while disabling Adkats.", e));
                    }
                }));

                //Start the finalizer thread
                this._Finalizer.Start();
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error occured while initializing AdKats disable thread.", e));
            }
        }

        public override void OnPlayerTeamChange(String soldierName, Int32 teamId, Int32 squadId) {
            this.DebugWrite("Entering OnPlayerTeamChange", 7);
            try {
                //When a player changes team, tell teamswap to recheck queues
                this._TeamswapWaitHandle.Set();
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while handling player team change.", e));
            }
            this.DebugWrite("Exiting OnPlayerTeamChange", 7);
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset cpsSubset) {
            try {
                //Only handle the list if it is an "All players" list
                if (cpsSubset.Subset == CPlayerSubset.PlayerSubsetType.All) {
                    //Return if small duration (5 seconds) since last player list
                    if ((DateTime.UtcNow - this._LastSuccessfulPlayerList) < TimeSpan.FromSeconds(5)) {
                        //Avoid "over-handling" player listing
                    }
                    else {
                        //Only perform the following if all threads are ready
                        if (this._ThreadsReady) {
                            this.QueuePlayerListForProcessing(players);
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error occured while listing players.", e));
            }
        }

        private void QueuePlayerListForProcessing(List<CPlayerInfo> players) {
            this.DebugWrite("Entering queuePlayerListForProcessing", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue player list for processing", 6);
                    lock (this._PlayerListProcessingQueue) {
                        //Empty the queue before sending the new player list. Only the most recent information should be processed.
                        this._PlayerListProcessingQueue.Clear();
                        this._PlayerListProcessingQueue.Enqueue(players);
                        this.DebugWrite("Player list queued for processing", 6);
                        this._PlayerListProcessingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing player list for processing.", e));
            }
            this.DebugWrite("Exiting queuePlayerListForProcessing", 7);
        }

        public void PlayerListingThreadLoop() {
            try {
                this.DebugWrite("PLIST: Starting Player Listing Thread", 2);
                Thread.CurrentThread.Name = "playerlisting";
                while (true) {
                    this.DebugWrite("PLIST: Entering Player Listing Thread Loop", 7);
                    if (!this._IsEnabled) {
                        this.DebugWrite("PLIST: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Get all unparsed inbound lists
                    Queue<List<CPlayerInfo>> inboundPlayerLists;
                    if (this._PlayerListProcessingQueue.Count > 0) {
                        this.DebugWrite("PLIST: Preparing to lock player list queues to retrive new player lists", 7);
                        lock (this._PlayerListProcessingQueue) {
                            this.DebugWrite("PLIST: Inbound player lists found. Grabbing.", 6);
                            //Grab all lists in the queue
                            inboundPlayerLists = new Queue<List<CPlayerInfo>>(this._PlayerListProcessingQueue.ToArray());
                            //Clear the queue for next run
                            this._PlayerListProcessingQueue.Clear();
                        }
                    }
                    else {
                        this.DebugWrite("PLIST: No inbound player lists. Waiting for Input.", 4);
                        //Wait for input
                        this._PlayerListProcessingWaitHandle.Reset();
                        this._PlayerListProcessingWaitHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }

                    //Loop through all messages in order that they came in
                    while (inboundPlayerLists.Count > 0) {
                        this.DebugWrite("PLIST: begin reading player lists", 6);
                        //Dequeue the first/next message
                        List<CPlayerInfo> players = inboundPlayerLists.Dequeue();

                        this.DebugWrite("Listing Players", 5);
                        //Player list and ban list need to be locked for this operation
                        lock (this._PlayersMutex) {
                            List<String> playerNames = new List<String>();
                            //Reset the player counts of both sides and recount everything
                            this._UsPlayerCount = 0;
                            this._RuPlayerCount = 0;
                            //Loop over all players in the list
                            foreach (CPlayerInfo player in players) {
                                playerNames.Add(player.SoldierName);
                                AdKats_Player aPlayer = null;
                                //Check if the player is already in the player dictionary
                                if (this._PlayerDictionary.TryGetValue(player.SoldierName, out aPlayer)) {
                                    //If they are update the internal frostbite player info
                                    this._PlayerDictionary[player.SoldierName].frostbitePlayerInfo = player;
                                }
                                else {
                                    //If they aren't in the list, fetch their profile from the database
                                    aPlayer = this.FetchPlayer(false, -1, player.SoldierName, player.GUID, null);
                                    //Add the frostbite player info
                                    aPlayer.frostbitePlayerInfo = player;
                                    //Set their last death/spawn times
                                    aPlayer.lastDeath = DateTime.UtcNow;
                                    aPlayer.lastSpawn = DateTime.UtcNow;
                                    //Add them to the dictionary
                                    this._PlayerDictionary.Add(player.SoldierName, aPlayer);
                                    //If using ban enforcer, check the player's ban status
                                    if (this._UseBanEnforcer) {
                                        this.QueuePlayerForBanCheck(aPlayer);
                                    }
                                    else if (this._UseHackerChecker) {
                                        //Queue the player for a hacker check
                                        this.QueuePlayerForHackerCheck(aPlayer);
                                    }
                                }

                                if (player.TeamID == UsTeamID) {
                                    this._UsPlayerCount++;
                                }
                                else if (player.TeamID == RuTeamID) {
                                    this._RuPlayerCount++;
                                }
                            }
                            //Make sure the player dictionary is clean of any straglers
                            List<String> dicPlayerNames = this._PlayerDictionary.Keys.ToList();
                            Int32 straglerCount = 0;
                            Int32 dicCount = this._PlayerDictionary.Count;
                            foreach (String playerName in dicPlayerNames) {
                                if (!playerNames.Contains(playerName)) {
                                    straglerCount++;
                                    this.DebugWrite("PLIST: Removing " + playerName + " from current player list (VIA CLEANUP).", 4);
                                    this._PlayerDictionary.Remove(playerName);
                                }
                            }
                            //Inform the admins of disconnect
                            if (straglerCount > (dicCount / 2)) {
                                //Create the report record
                                AdKatsRecord record = new AdKatsRecord {
                                                                           record_source = AdKatsRecord.Sources.Automated,
                                                                           isDebug = true,
                                                                           server_id = this._ServerID,
                                                                           command_type = this._CommandKeyDictionary["player_calladmin"],
                                                                           command_numeric = 0,
                                                                           target_name = "Server",
                                                                           target_player = null,
                                                                           source_name = "AdKats",
                                                                           record_message = "Server Crashed / Blaze Disconnected (" + dicCount + " Players Lost)"
                                                                       };
                                //Process the record
                                this.QueueRecordForProcessing(record);
                                this.ConsoleError(record.record_message);

                                //Set round ended
                                if (!this._RoundEnded) {
                                    this._RoundEnded = true;
                                    Thread.Sleep(3000);
                                    this._RoundEnded = false;
                                }
                            }
                        }

                        //Update last successful player list time
                        this._LastSuccessfulPlayerList = DateTime.UtcNow;
                        //Set the handle for TeamSwap 
                        this._PlayerListUpdateWaitHandle.Set();
                    }
                }
                this.DebugWrite("PLIST: Ending Player Listing Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Player Listing thread aborted. Attempting to restart.", e));
                    this.DebugWrite("Thread Exception", 4);
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Error occured in player listing thread.", e));
                }
            }
        }

        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo cpbiPlayer) {
            try {
                this.DebugWrite("OPPI: OnPunkbusterPlayerInfo fired!", 7);
                lock (this._PlayersMutex) {
                    AdKats_Player targetPlayer = null;
                    if (this._PlayerDictionary.TryGetValue(cpbiPlayer.SoldierName, out targetPlayer)) {
                        this.DebugWrite("OPPI: PB player already in the player list.", 7);
                        Boolean updatePlayer = (targetPlayer.player_ip == null);
                        //Update the player with pb info
                        targetPlayer.PBPlayerInfo = cpbiPlayer;
                        targetPlayer.player_pbguid = cpbiPlayer.GUID;
                        targetPlayer.player_slot = cpbiPlayer.SlotID;
                        targetPlayer.player_ip = cpbiPlayer.Ip.Split(':')[0];
                        if (updatePlayer) {
                            this.DebugWrite("OPPI: Queueing existing player " + targetPlayer.player_name + " for update.", 4);
                            this.UpdatePlayer(targetPlayer);
                            //If using ban enforcer, queue player for update
                            if (this._UseBanEnforcer) {
                                this.QueuePlayerForBanCheck(targetPlayer);
                            }
                        }
                    }
                    this.DebugWrite("OPPI: Player slot: " + cpbiPlayer.SlotID, 7);
                }
                this.DebugWrite("OPPI: OnPunkbusterPlayerInfo finished!", 7);
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error occured while processing punkbuster info.", e));
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo) {
            this.DebugWrite("Entering OnServerInfo", 7);
            try {
                if (this._IsEnabled) {
                    this._UsTicketCount = -1;
                    this._RuTicketCount = -1;
                    if (serverInfo != null) {
                        //Get the team scores
                        this.SetServerInfo(serverInfo);
                        if (serverInfo.TeamScores != null) {
                            List<TeamScore> listCurrTeamScore = serverInfo.TeamScores;
                            //During round change, teams don't exist
                            if (listCurrTeamScore.Count > 0) {
                                foreach (TeamScore score in listCurrTeamScore) {
                                    if (score.TeamID == AdKats.UsTeamID) {
                                        this._UsTicketCount = score.Score;
                                    }
                                    else if (score.TeamID == AdKats.RuTeamID) {
                                        this._RuTicketCount = score.Score;
                                    }
                                    else {
                                        this.DebugWrite("Score for team " + score.TeamID + " not parsable.", 2);
                                    }
                                }
                            }
                            else {
                                this.DebugWrite("Server info fired while changing rounds, no teams to parse.", 5);
                            }

                            if (this._UsTicketCount >= 0 && this._RuTicketCount >= 0) {
                                this._LowestTicketCount = (this._UsTicketCount < this._RuTicketCount) ? (this._UsTicketCount) : (this._RuTicketCount);
                                this._HighestTicketCount = (this._UsTicketCount > this._RuTicketCount) ? (this._UsTicketCount) : (this._RuTicketCount);
                            }
                        }

                        this._ServerName = serverInfo.ServerName;
                        //Only activate the following on ADK servers.
                        Boolean wasADK = this._IsTestingAuthorized;
                        //TODO remove bsw
                        this._IsTestingAuthorized = serverInfo.ServerName.Contains("=ADK=") || serverInfo.ServerName.Contains("[BSW]");
                        if (!wasADK && this._IsTestingAuthorized) {
                            this.ConsoleWrite("Server is priviledged for testing.");
                            this.UpdateSettingPage();
                        }
                    }
                    else {
                        this.HandleException(new AdKatsException("Server info was null"));
                    }
                    this._ServerInfoWaitHandle.Set();
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while processing server info.", e));
            }
            this.DebugWrite("Exiting OnServerInfo", 7);
        }

        public override void OnLevelLoaded(String strMapFileName, String strMapMode, Int32 roundsPlayed, Int32 roundsTotal) {
            this.DebugWrite("Entering OnLevelLoaded", 7);
            try {
                if (this._IsEnabled) {
                    //Completely clear all round-specific data
                    this._RoundReports = new Dictionary<String, AdKatsRecord>();
                    this._RoundMutedPlayers = new Dictionary<String, int>();
                    this._TeamswapRoundWhitelist = new Dictionary<String, Boolean>();
                    this._ActionConfirmDic = new Dictionary<String, AdKatsRecord>();
                    this._ActOnSpawnDictionary = new Dictionary<String, AdKatsRecord>();
                    this._TeamswapOnDeathMoveDic = new Dictionary<String, CPlayerInfo>();
                    this.AutoWhitelistPlayers();
                    this._UsMoveQueue = new Queue<CPlayerInfo>();
                    this._RuMoveQueue = new Queue<CPlayerInfo>();
                    this._RoundCookers = new Dictionary<String, AdKats_Player>();
                    //Enable round timer
                    if (this._UseRoundTimer) {
                        this.StartRoundTimer();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while handling level load.", e));
            }
            this.DebugWrite("Exiting OnLevelLoaded", 7);
        }

        //Round ended stuff
        public override void OnEndRound(Int32 iWinningTeamID) {
            if (!this._RoundEnded) {
                this._RoundEnded = true;
                Thread.Sleep(3000);
                this._RoundEnded = false;
            }
        }

        public override void OnRunNextLevel() {
            if (!this._RoundEnded) {
                this._RoundEnded = true;
                Thread.Sleep(3000);
                this._RoundEnded = false;
            }
        }

        //Move delayed players when they are killed
        public override void OnPlayerKilled(Kill kKillerVictimDetails) {
            this.DebugWrite("Entering OnPlayerKilled", 7);
            try {
                //If the plugin is not enabled just return
                if (!this._IsEnabled || !this._ThreadsReady) {
                    return;
                }
                //Otherwise, queue the kill for processing
                this.QueueKillForProcessing(kKillerVictimDetails);
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while handling onPlayerKilled.", e));
            }
        }

        private void QueueKillForProcessing(Kill kKillerVictimDetails) {
            this.DebugWrite("Entering queueKillForProcessing", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue kill for processing", 6);
                    lock (this._KillProcessingQueue) {
                        this._KillProcessingQueue.Enqueue(kKillerVictimDetails);
                        this.DebugWrite("Kill queued for processing", 6);
                        this._KillProcessingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing kill for processing.", e));
            }
            this.DebugWrite("Exiting queueKillForProcessing", 7);
        }

        public void KillProcessingThreadLoop() {
            try {
                this.DebugWrite("KILLPROC: Starting Kill Processing Thread", 2);
                Thread.CurrentThread.Name = "killprocessing";
                while (true) {
                    this.DebugWrite("KILLPROC: Entering Kill Processing Thread Loop", 7);
                    if (!this._IsEnabled) {
                        this.DebugWrite("KILLPROC: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Get all unprocessed inbound kills
                    Queue<Kill> inboundPlayerKills;
                    if (this._KillProcessingQueue.Count > 0) {
                        this.DebugWrite("KILLPROC: Preparing to lock inbound kill queue to retrive new player kills", 7);
                        lock (this._KillProcessingQueue) {
                            this.DebugWrite("KILLPROC: Inbound kills found. Grabbing.", 6);
                            //Grab all kills in the queue
                            inboundPlayerKills = new Queue<Kill>(this._KillProcessingQueue.ToArray());
                            //Clear the queue for next run
                            this._KillProcessingQueue.Clear();
                        }
                    }
                    else {
                        this.DebugWrite("KILLPROC: No inbound player kills. Waiting for Input.", 4);
                        //Wait for input
                        this._KillProcessingWaitHandle.Reset();
                        this._KillProcessingWaitHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }

                    //Loop through all kils in order that they came in
                    while (inboundPlayerKills.Count > 0) {
                        this.DebugWrite("KILLPROC: begin reading player kills", 6);
                        //Dequeue the first/next kill
                        Kill playerKill = inboundPlayerKills.Dequeue();

                        //Call processing on the player kill
                        this.ProcessPlayerKill(playerKill);
                    }
                }
                this.DebugWrite("KILLPROC: Ending Kill Processing Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Kill processing thread aborted. Attempting to restart.", e));
                    this.DebugWrite("Thread Exception", 4);
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Error occured in kill processing thread.", e));
                }
            }
        }

        private void ProcessPlayerKill(Kill kKillerVictimDetails) {
            try {
                //Used for delayed player moving
                if (this._TeamswapOnDeathMoveDic.Count > 0) {
                    lock (this._TeamswapMutex) {
                        this._TeamswapOnDeathCheckingQueue.Enqueue(kKillerVictimDetails.Victim);
                        this._TeamswapWaitHandle.Set();
                    }
                }

                Boolean gKillHandled = false;
                //Update player death information
                if (this._PlayerDictionary.ContainsKey(kKillerVictimDetails.Victim.SoldierName)) {
                    lock (this._PlayersMutex) {
                        AdKats_Player victim = this._PlayerDictionary[kKillerVictimDetails.Victim.SoldierName];
                        this.DebugWrite("Setting " + victim.player_name + " time of death to " + kKillerVictimDetails.TimeOfDeath, 7);
                        victim.lastDeath = kKillerVictimDetails.TimeOfDeath;
                        //Only add the last death if it's not a death by admin
                        if (!String.IsNullOrEmpty(kKillerVictimDetails.Killer.SoldierName)) {
                            try {
                                //ADK grenade cooking catcher
                                if (this._UseExperimentalTools && this._UseGrenadeCookCatcher) {
                                    if (this._RoundCookers == null) {
                                        this._RoundCookers = new Dictionary<String, AdKats_Player>();
                                    }
                                    Double fuseTime = 0;
                                    if (this._GameVersion == GameVersion.BF3) {
                                        fuseTime = 3735.00;
                                    }
                                    else if (this._GameVersion == GameVersion.BF4) {
                                        fuseTime = 3132.00;
                                    }
                                    //2865 MINI
                                    const double possibleRange = 750.00;
                                    //Update killer information
                                    AdKats_Player killer = null;
                                    if (this._PlayerDictionary.TryGetValue(kKillerVictimDetails.Killer.SoldierName, out killer)) {
                                        //Initialize / clean up the recent kills queue
                                        if (killer.recentKills == null) {
                                            killer.recentKills = new Queue<KeyValuePair<AdKats_Player, DateTime>>();
                                        }
                                        //Only keep the last 6 kills in memory
                                        while (killer.recentKills.Count > 1) {
                                            killer.recentKills.Dequeue();
                                        }
                                        //Add the player
                                        killer.recentKills.Enqueue(new KeyValuePair<AdKats_Player, DateTime>(victim, kKillerVictimDetails.TimeOfDeath));
                                        //Check for cooked grenade and non-suicide
                                        if (kKillerVictimDetails.DamageType.Contains("M67")) {
                                            if (kKillerVictimDetails.Killer.SoldierName != kKillerVictimDetails.Victim.SoldierName) {
                                                Boolean told = false;
                                                List<KeyValuePair<AdKats_Player, String>> possible = new List<KeyValuePair<AdKats_Player, String>>();
                                                List<KeyValuePair<AdKats_Player, String>> sure = new List<KeyValuePair<AdKats_Player, String>>();
                                                foreach (KeyValuePair<AdKats_Player, DateTime> cooker in killer.recentKills) {
                                                    //Get the actual time since cooker value
                                                    Double milli = kKillerVictimDetails.TimeOfDeath.Subtract(cooker.Value).TotalMilliseconds;

                                                    //Calculate the percentage probability
                                                    Double probability = 0.00;
                                                    if (Math.Abs(milli - fuseTime) < possibleRange) {
                                                        probability = (1 - Math.Abs((milli - fuseTime) / possibleRange)) * 100;
                                                        this.DebugWrite(cooker.Key.player_name + " cooking probability: " + probability + "%", 2);
                                                    }
                                                    else {
                                                        probability = 0.00;
                                                    }

                                                    //If probability > 60% report the player and add them to the round cookers list
                                                    if (probability > 60.00) {
                                                        this.DebugWrite(cooker.Key.player_name + " in " + killer.player_name + " recent kills has a " + probability + "% cooking probability.", 2);
                                                        gKillHandled = true;
                                                        //Code to avoid spam
                                                        if (cooker.Key.lastAction.AddSeconds(2) < DateTime.UtcNow) {
                                                            cooker.Key.lastAction = DateTime.UtcNow;
                                                        }
                                                        else {
                                                            this.DebugWrite("Skipping additional auto-actions for multi-kill event.", 2);
                                                            continue;
                                                        }

                                                        if (!told) {
                                                            //Inform the victim player that they will not be punished
                                                            this.PlayerSayMessage(kKillerVictimDetails.Killer.SoldierName, "You appear to be a victim of grenade cooking and will NOT be punished.");
                                                            told = true;
                                                        }

                                                        //Create the probability String
                                                        String probString = ((int) probability) + "-" + ((int) milli);

                                                        //If the player is already on the round cooker list, ban them
                                                        if (this._RoundCookers.ContainsKey(cooker.Key.player_name)) {
                                                            //Create the ban record
                                                            AdKatsRecord record = new AdKatsRecord {
                                                                                                       record_source = AdKatsRecord.Sources.Automated,
                                                                                                       server_id = this._ServerID,
                                                                                                       command_type = this._CommandKeyDictionary["player_punish"],
                                                                                                       command_numeric = 0,
                                                                                                       target_name = cooker.Key.player_name,
                                                                                                       target_player = cooker.Key,
                                                                                                       source_name = "AutoAdmin",
                                                                                                       record_message = "Rules: Cooking Grenades [" + probString + "-X] [Victim " + killer.player_name + " Protected]"
                                                                                                   };
                                                            //Process the record
                                                            this.QueueRecordForProcessing(record);
                                                            //this.adminSay("Punishing " + killer.player_name + " for " + record.record_message);
                                                            this.DebugWrite(record.target_player.player_name + " punished for " + record.record_message, 2);
                                                            return;
                                                        }
                                                        //else if probability > 92.5% add them to the SURE list, and round cooker list
                                                        if (probability > 92.5) {
                                                            this._RoundCookers.Add(cooker.Key.player_name, cooker.Key);
                                                            this.DebugWrite(cooker.Key.player_name + " added to round cooker list.", 2);
                                                            //Add to SURE
                                                            sure.Add(new KeyValuePair<AdKats_Player, String>(cooker.Key, probString));
                                                        }
                                                            //Otherwise add them to the round cooker list, and add to POSSIBLE list
                                                        else {
                                                            this._RoundCookers.Add(cooker.Key.player_name, cooker.Key);
                                                            this.DebugWrite(cooker.Key.player_name + " added to round cooker list.", 2);
                                                            //Add to POSSIBLE
                                                            possible.Add(new KeyValuePair<AdKats_Player, String>(cooker.Key, probString));
                                                        }
                                                    }
                                                }
                                                //This method used for dealing with multiple kills at the same instant i.e twin/triple headshots
                                                if (sure.Count == 1 && possible.Count == 0) {
                                                    AdKats_Player player = sure[0].Key;
                                                    String probString = sure[0].Value;
                                                    //Create the ban record
                                                    AdKatsRecord record = new AdKatsRecord {
                                                                                               record_source = AdKatsRecord.Sources.Automated,
                                                                                               server_id = this._ServerID,
                                                                                               command_type = this._CommandKeyDictionary["player_punish"],
                                                                                               command_numeric = 0,
                                                                                               target_name = player.player_name,
                                                                                               target_player = player,
                                                                                               source_name = "AutoAdmin",
                                                                                               record_message = "Rules: Cooking Grenades [" + probString + "] [Victim " + killer.player_name + " Protected]"
                                                                                           };
                                                    //Process the record
                                                    this.QueueRecordForProcessing(record);
                                                    //this.adminSay("Punishing " + killer.player_name + " for " + record.record_message);
                                                    this.DebugWrite(record.target_player.player_name + " punished for " + record.record_message, 2);
                                                }
                                                else {
                                                    AdKats_Player player = null;
                                                    String probString = null;
                                                    foreach (KeyValuePair<AdKats_Player, String> playerPair in sure) {
                                                        player = playerPair.Key;
                                                        probString = playerPair.Value;
                                                        //Create the report record
                                                        AdKatsRecord record = new AdKatsRecord {
                                                                                                   record_source = AdKatsRecord.Sources.Automated,
                                                                                                   server_id = this._ServerID,
                                                                                                   command_type = this._CommandKeyDictionary["player_report"],
                                                                                                   command_numeric = 0,
                                                                                                   target_name = player.player_name,
                                                                                                   target_player = player,
                                                                                                   source_name = "AutoAdmin",
                                                                                                   record_message = "Possible Grenade Cooker [" + probString + "] [Victim " + killer.player_name + " Protected]"
                                                                                               };
                                                        //Process the record
                                                        this.QueueRecordForProcessing(record);
                                                        this.DebugWrite(record.target_player.player_name + " reported for " + record.record_message, 2);
                                                    }
                                                    foreach (KeyValuePair<AdKats_Player, String> playerPair in possible) {
                                                        player = playerPair.Key;
                                                        probString = playerPair.Value;
                                                        //Create the report record
                                                        AdKatsRecord record = new AdKatsRecord {
                                                                                                   record_source = AdKatsRecord.Sources.Automated,
                                                                                                   server_id = this._ServerID,
                                                                                                   command_type = this._CommandKeyDictionary["player_report"],
                                                                                                   command_numeric = 0,
                                                                                                   target_name = player.player_name,
                                                                                                   target_player = player,
                                                                                                   source_name = "AutoAdmin",
                                                                                                   record_message = "Possible Grenade Cooker [" + probString + "] [Victim " + killer.player_name + " Protected]"
                                                                                               };
                                                        //Process the record
                                                        this.QueueRecordForProcessing(record);
                                                        this.DebugWrite(record.target_player.player_name + " reported for " + record.record_message, 2);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e) {
                                this.HandleException(new AdKatsException("Error in grenade cook catcher.", e));
                            }
                        }
                    }
                }

                try {
                    if (this._UseExperimentalTools && this._UseWeaponLimiter && !gKillHandled) {
                        //Check for restricted weapon
                        if (Regex.Match(kKillerVictimDetails.DamageType, @"(?:" + this._WeaponLimiterString + ")", RegexOptions.IgnoreCase).Success) {
                            //Check for exception type
                            if (!Regex.Match(kKillerVictimDetails.DamageType, @"(?:" + this._WeaponLimiterExceptionString + ")", RegexOptions.IgnoreCase).Success) {
                                //Check if suicide
                                if (kKillerVictimDetails.Killer.SoldierName != kKillerVictimDetails.Victim.SoldierName) {
                                    //Get player from the dictionary
                                    AdKats_Player killer = null;
                                    if (this._PlayerDictionary.TryGetValue(kKillerVictimDetails.Killer.SoldierName, out killer)) {
                                        //Code to avoid spam
                                        if (killer.lastAction.AddSeconds(2) < DateTime.UtcNow) {
                                            killer.lastAction = DateTime.UtcNow;

                                            //Create the punish record
                                            AdKatsRecord record = new AdKatsRecord {
                                                                                       record_source = AdKatsRecord.Sources.Automated,
                                                                                       server_id = this._ServerID,
                                                                                       command_type = this._CommandKeyDictionary["player_punish"],
                                                                                       command_numeric = 0,
                                                                                       target_name = killer.player_name,
                                                                                       target_player = killer,
                                                                                       source_name = "AutoAdmin"
                                                                                   };
                                            const string removeWeapon = "Weapons/";
                                            const string removeGadgets = "Gadgets/";
                                            const string removePrefix = "U_";
                                            String weapon = kKillerVictimDetails.DamageType;
                                            Int32 index = weapon.IndexOf(removeWeapon, System.StringComparison.Ordinal);
                                            weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removeWeapon.Length));
                                            index = weapon.IndexOf(removeGadgets, System.StringComparison.Ordinal);
                                            weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removeGadgets.Length));
                                            index = weapon.IndexOf(removePrefix, System.StringComparison.Ordinal);
                                            weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removePrefix.Length));
                                            if (weapon == "RoadKill") {
                                                record.record_message = "Rules: Roadkilling with EOD or MAV";
                                            }
                                            else if (weapon == "Death") {
                                                record.record_message = "Rules: Using Mortar";
                                            }
                                            else {
                                                record.record_message = "Rules: Using Explosives [" + weapon + "]";
                                            }
                                            //Process the record
                                            this.QueueRecordForProcessing(record);
                                        }
                                        else {
                                            this.DebugWrite("Skipping additional auto-actions for multi-kill event.", 2);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e) {
                    this.HandleException(new AdKatsException("Error in no explosives auto-admin.", e));
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while processing player kill.", e));
            }
            this.DebugWrite("Exiting OnPlayerKilled", 7);
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) {
            this.DebugWrite("Entering OnPlayerSpawned", 7);
            try {
                if (this._IsEnabled) {
                    if (this._CommandNameDictionary.Count > 0) {
                        //Handle TeamSwap notifications
                        String command = this._CommandKeyDictionary["self_teamswap"].command_text;
                        AdKats_Player aPlayer = null;
                        if (this._PlayerDictionary.TryGetValue(soldierName, out aPlayer)) {
                            if (aPlayer.player_aa && !aPlayer.player_aa_told) {
                                this.ExecuteCommand("procon.protected.send", "admin.yell", "For your consistent player reporting you can now use @" + command + ". Type @" + command + " to move yourself between teams.", "10", "player", soldierName);
                                aPlayer.player_aa_told = true;
                            }
                            else {
                                Boolean informed = true;
                                if (this._TeamswapRoundWhitelist.Count > 0 && this._TeamswapRoundWhitelist.TryGetValue(soldierName, out informed)) {
                                    if (informed == false) {
                                        this.ExecuteCommand("procon.protected.send", "admin.yell", "By random selection you can use @" + command + " for this round. Type @" + command + " to move yourself between teams.", "10", "player", soldierName);
                                        this._TeamswapRoundWhitelist[soldierName] = true;
                                    }
                                }
                            }
                        }
                    }

                    //Handle Dev Notifications
                    if (soldierName == "ColColonCleaner" && !_ToldCol) {
                        this.ExecuteCommand("procon.protected.send", "admin.yell", "CONGRATS! This server is running version " + PluginVersion + " of AdKats!", "20", "player", "ColColonCleaner");
                        this._ToldCol = true;
                    }

                    //Update player spawn information
                    if (this._PlayerDictionary.ContainsKey(soldierName)) {
                        lock (this._PlayersMutex) {
                            this._PlayerDictionary[soldierName].lastSpawn = DateTime.UtcNow;
                        }
                    }

                    if (this._ActOnSpawnDictionary.Count > 0) {
                        lock (this._ActOnSpawnDictionary) {
                            AdKatsRecord record = null;
                            if (this._ActOnSpawnDictionary.TryGetValue(soldierName, out record)) {
                                //Remove it from the dic
                                this._ActOnSpawnDictionary.Remove(soldierName);
                                //Wait 1.5 seconds to kill them again
                                Thread.Sleep(1500);
                                //Queue the action
                                this.QueueRecordForActionHandling(record);
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while handling player spawn.", e));
            }
            this.DebugWrite("Exiting OnPlayerSpawned", 7);
        }

        //DONE
        public override void OnPlayerLeft(CPlayerInfo playerInfo) {
            this.DebugWrite("Entering OnPlayerLeft", 7);
            try {
                this.RemovePlayerFromDictionary(playerInfo.SoldierName);
                this._TeamswapWaitHandle.Set();
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while handling player exit.", e));
            }
            this.DebugWrite("Exiting OnPlayerLeft", 7);
        }

        #endregion

        #region Ban Enforcer

        private void QueuePlayerForBanCheck(AdKats_Player player) {
            this.DebugWrite("Entering queuePlayerForBanCheck", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue player for ban check", 6);
                    lock (_BanEnforcerMutex) {
                        this._BanEnforcerCheckingQueue.Enqueue(player);
                        this.DebugWrite("Player queued for checking", 6);
                        this._BanEnforcerWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing player for ban check.", e));
            }
            this.DebugWrite("Exiting queuePlayerForBanCheck", 7);
        }

        private void QueueSettingImport(Int32 serverID) {
            this.DebugWrite("Entering queueSettingImport", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue server ID for setting import", 6);
                    this._SettingImportID = serverID;
                    this._DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while preparing to import settings.", e));
            }
            this.DebugWrite("Exiting queueSettingImport", 7);
        }

        private void QueueSettingForUpload(CPluginVariable setting) {
            this.DebugWrite("Entering queueSettingForUpload", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue setting " + setting.Name + " for upload", 6);
                    lock (this._SettingUploadQueue) {
                        this._SettingUploadQueue.Enqueue(setting);
                        this._DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing setting for upload.", e));
            }
            this.DebugWrite("Exiting queueSettingForUpload", 7);
        }

        private void QueueCommandForUpload(AdKatsCommand command) {
            this.DebugWrite("Entering queueCommandForUpload", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue command " + command.command_key + " for upload", 6);
                    lock (this._CommandUploadQueue) {
                        this._CommandUploadQueue.Enqueue(command);
                        this._DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing command for upload.", e));
            }
            this.DebugWrite("Exiting queueCommandForUpload", 7);
        }

        private void QueueRoleForUpload(AdKats_Role aRole) {
            this.DebugWrite("Entering queueRoleForUpload", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue role " + aRole.role_key + " for upload", 6);
                    lock (this._RoleUploadQueue) {
                        this._RoleUploadQueue.Enqueue(aRole);
                        this._DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing role for upload.", e));
            }
            this.DebugWrite("Exiting queueRoleForUpload", 7);
        }

        private void QueueRoleForRemoval(AdKats_Role aRole) {
            this.DebugWrite("Entering queueRoleForRemoval", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue role " + aRole.role_key + " for removal", 6);
                    lock (this._RoleRemovalQueue) {
                        this._RoleRemovalQueue.Enqueue(aRole);
                        this._DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing role for removal.", e));
            }
            this.DebugWrite("Exiting queueRoleForRemoval", 7);
        }

        private void QueueBanForProcessing(AdKatsBan aBan) {
            this.DebugWrite("Entering queueBanForProcessing", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue ban for processing", 6);
                    lock (_BanEnforcerMutex) {
                        this._BanEnforcerProcessingQueue.Enqueue(aBan);
                        this.DebugWrite("Ban queued for processing", 6);
                        this._DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing ban for processing.", e));
            }
            this.DebugWrite("Exiting queueBanForProcessing", 7);
        }

        private void BanEnforcerThreadLoop() {
            try {
                this.DebugWrite("BANENF: Starting Ban Enforcer Thread", 2);
                Thread.CurrentThread.Name = "BanEnforcer";

                while (true) {
                    this.DebugWrite("BANENF: Entering Ban Enforcer Thread Loop", 7);
                    if (!this._IsEnabled) {
                        this.DebugWrite("BANENF: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Get all unchecked players
                    Queue<AdKats_Player> playerCheckingQueue = new Queue<AdKats_Player>();
                    if (this._BanEnforcerCheckingQueue.Count > 0 && this._UseBanEnforcer) {
                        this.DebugWrite("BANENF: Preparing to lock banEnforcerMutex to retrive new players", 6);
                        lock (_BanEnforcerMutex) {
                            this.DebugWrite("BANENF: Inbound players found. Grabbing.", 5);
                            //Grab all players in the queue
                            playerCheckingQueue = new Queue<AdKats_Player>(this._BanEnforcerCheckingQueue.ToArray());
                            //Clear the queue for next run
                            this._BanEnforcerCheckingQueue.Clear();
                        }
                    }
                    else {
                        this.DebugWrite("BANENF: No inbound ban checks. Waiting for Input.", 4);
                        //Wait for input
                        this._BanEnforcerWaitHandle.Reset();
                        this._BanEnforcerWaitHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }

                    //Get all checks in order that they came in
                    while (playerCheckingQueue.Count > 0) {
                        //Grab first/next player
                        AdKats_Player aPlayer = playerCheckingQueue.Dequeue();
                        this.DebugWrite("BANENF: begin reading player", 5);

                        //this.DebugWrite("Checking " + aPlayer.player_name + " Against " + this.adkats_bans_Name.Count + " Name Bans. " + this.adkats_bans_GUID.Count + " GUID Bans. And " + this.adkats_bans_IP.Count + " IP Bans.", 5);

                        if (this._PlayerDictionary.ContainsKey(aPlayer.player_name)) {
                            AdKatsBan aBan = this.FetchPlayerBan(aPlayer);

                            if (aBan != null) {
                                this.DebugWrite("BANENF: BAN ENFORCED", 3);
                                //Create the new record
                                AdKatsRecord record = new AdKatsRecord {
                                                                           record_source = AdKatsRecord.Sources.Automated,
                                                                           source_name = "BanEnforcer",
                                                                           isIRO = false,
                                                                           server_id = this._ServerID,
                                                                           target_name = aPlayer.player_name,
                                                                           target_player = aPlayer,
                                                                           command_type = this._CommandKeyDictionary["banenforcer_enforce"],
                                                                           command_numeric = (int) aBan.ban_id,
                                                                           record_message = aBan.ban_record.record_message
                                                                       };
                                //Queue record for upload
                                this.QueueRecordForProcessing(record);
                                //Enforce the ban
                                this.EnforceBan(aBan, true);
                            }
                            else {
                                this.DebugWrite("BANENF: No ban found for player", 5);
                                //Only call a ban check if the player does not already have a ban
                                if (this._UseHackerChecker) {
                                    this.QueuePlayerForHackerCheck(aPlayer);
                                }
                            }
                        }
                    }
                }
                this.DebugWrite("BANENF: Ending Ban Enforcer Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Ban enforcer thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Error occured in ban enforcer thread.", e));
                }
            }
        }

        public override void OnBanAdded(CBanInfo ban) {
            if (!this._IsEnabled || !this._UseBanEnforcer)
                return;
            //this.DebugWrite("OnBanAdded fired", 6);
            //this.ExecuteCommand("procon.protected.send", "banList.list");
        }

        public override void OnBanList(List<CBanInfo> banList) {
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                //Return if small duration (10 seconds) since last ban list
                if ((DateTime.UtcNow - this._LastSuccessfulBanList) < TimeSpan.FromSeconds(10)) {
                    this.DebugWrite("Banlist being called quickly.", 4);
                    return;
                }
                DateTime startTime = DateTime.UtcNow;
                this._LastSuccessfulBanList = startTime;
                if (!this._IsEnabled)
                    return;
                this.DebugWrite("OnBanList fired", 3);
                if (this._UseBanEnforcer) {
                    if (banList.Count > 0) {
                        this.DebugWrite("Bans found", 3);
                        lock (this._CBanProcessingQueue) {
                            //Only allow one banlist to happen during initial startup
                            if (this._UseBanEnforcerPreviousState || !this._BansFirstListed) {
                                foreach (CBanInfo cBan in banList) {
                                    this.DebugWrite("Queuing Ban.", 7);
                                    this._CBanProcessingQueue.Enqueue(cBan);
                                    if (DateTime.UtcNow - startTime > TimeSpan.FromSeconds(50)) {
                                        this.HandleException(new AdKatsException("OnBanList took longer than 50 seconds, exiting so procon doesn't panic."));
                                        return;
                                    }
                                }
                                this._BansFirstListed = true;
                            }
                        }
                    }
                }
                this._DbCommunicationWaitHandle.Set();
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error occured while listing procon bans.", e));
            }
        }

        public override void OnBanListClear() {
            this.DebugWrite("Ban list cleared", 5);
        }

        public override void OnBanListSave() {
            this.DebugWrite("Ban list saved", 5);
        }

        public override void OnBanListLoad() {
            this.DebugWrite("Ban list loaded", 5);
        }

        #endregion

        #region Hacker Checker

        private void QueuePlayerForHackerCheck(AdKats_Player player) {
            this.DebugWrite("Entering queuePlayerForHackerCheck", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue player for hacker check", 6);
                    lock (this._HackerCheckerMutex) {
                        this._HackerCheckerQueue.Enqueue(player);
                        this.DebugWrite("Player queued for checking", 6);
                        this._HackerCheckerWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing player for hacker check.", e));
            }
            this.DebugWrite("Exiting queuePlayerForHackerCheck", 7);
        }

        public Boolean PlayerProtected(AdKats_Player aPlayer) {
            Boolean protect = false;
            foreach (String whitelistedValue in this._HackerCheckerWhitelist) {
                if (aPlayer.player_name == whitelistedValue) {
                    this.DebugWrite(aPlayer.player_name + " protected from hacker checker by name.", 2);
                    protect = true;
                    break;
                }
                if (aPlayer.player_guid == whitelistedValue) {
                    this.DebugWrite(aPlayer.player_name + " protected from hacker checker by guid.", 2);
                    protect = true;
                    break;
                }
                if (aPlayer.player_ip == whitelistedValue) {
                    this.DebugWrite(aPlayer.player_name + " protected from hacker checker by IP.", 2);
                    protect = true;
                    break;
                }
            }
            return protect;
        }

        public void HackerCheckerThreadLoop() {
            //Hacker Checker thread for ADK servers
            double checkedPlayers = 0;
            Double playersWithStats = 0;
            try {
                this.DebugWrite("HCKCHK: Starting Hacker Checker Thread", 2);
                Thread.CurrentThread.Name = "HackerChecker";

                Queue<AdKats_Player> playerCheckingQueue = new Queue<AdKats_Player>();
                Queue<AdKats_Player> repeatCheckingQueue = new Queue<AdKats_Player>();
                while (true) {
                    this.DebugWrite("HCKCHK: Entering Hacker Checker Thread Loop", 7);
                    if (!this._IsEnabled) {
                        playerCheckingQueue.Clear();
                        repeatCheckingQueue.Clear();
                        this.DebugWrite("HCKCHK: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    try {
                        //Get all unchecked players
                        if (this._HackerCheckerQueue.Count > 0) {
                            this.DebugWrite("HCKCHK: Preparing to lock hackerCheckerMutex to retrive new players", 6);
                            lock (this._HackerCheckerMutex) {
                                this.DebugWrite("HCKCHK: Inbound players found. Grabbing.", 5);
                                //Grab all players in the queue
                                playerCheckingQueue = new Queue<AdKats_Player>(this._HackerCheckerQueue.ToArray());
                                //Clear the queue for next run
                                this._HackerCheckerQueue.Clear();
                            }
                        }
                        else {
                            this.DebugWrite("HCKCHK: No inbound hacker checks. Waiting 10 seconds or for input.", 4);
                            //Wait for input
                            this._HackerCheckerWaitHandle.Reset();
                            //Either loop when handle is set, or after 3 minutes
                            this._HackerCheckerWaitHandle.WaitOne(180000 / ((repeatCheckingQueue.Count > 0) ? (repeatCheckingQueue.Count) : (1)));
                        }
                    }
                    catch (Exception e) {
                        this.HandleException(new AdKatsException("Error while fetching new players to check.", e));
                    }

                    //Current player being checked
                    AdKats_Player aPlayer = null;
                    try {
                        //Check one player from the repeat checking queue
                        if (repeatCheckingQueue.Count > 0) {
                            //Only keep players still in the server in the repeat checking list
                            Boolean stillInServer = true;
                            do {
                                aPlayer = repeatCheckingQueue.Dequeue();
                                if (!this._PlayerDictionary.ContainsKey(aPlayer.player_name)) {
                                    stillInServer = false;
                                }
                            } while (!stillInServer && repeatCheckingQueue.Count > 0);
                            //Fetch their stats from appropriate source
                            this.FetchPlayerStats(aPlayer);
                            //check for dmg mod if stats available
                            if (aPlayer.stats != null) {
                                playersWithStats++;
                                this.ConsoleSuccess(aPlayer.player_name + " now has stats. Checking.");

                                if (this._UseHackerChecker) {
                                    if (!this.PlayerProtected(aPlayer)) {
                                        this.RunStatSiteHackCheck(aPlayer, false);
                                    }
                                }
                                else {
                                    this.DebugWrite("Player removed from check list after disabling hacker checker.", 2);
                                }
                                this.ConsoleWrite("Players with " + this._GameVersion + "Stats: " + String.Format("{0:0.00}", (playersWithStats / checkedPlayers) * 100) + "%");
                            }
                            else {
                                //If they still dont have stats, add them back to the queue
                                repeatCheckingQueue.Enqueue(aPlayer);
                            }
                        }
                    }
                    catch (Exception e) {
                        this.HandleException(new AdKatsException("Error while in repeat checking queue handler", e));
                    }

                    //Get all checks in order that they came in
                    while (playerCheckingQueue.Count > 0) {
                        //Grab first/next player
                        aPlayer = playerCheckingQueue.Dequeue();
                        if (aPlayer != null) {
                            this.DebugWrite("HCKCHK: begin reading player", 4);

                            if (!this.PlayerProtected(aPlayer)) {
                                this.FetchPlayerStats(aPlayer);
                                checkedPlayers++;
                                //check for dmg mod if stats available
                                if (aPlayer.stats != null) {
                                    playersWithStats++;
                                    if (this._UseHackerChecker) {
                                        this.RunStatSiteHackCheck(aPlayer, false);
                                    }
                                    else {
                                        this.DebugWrite("Player skipped after disabling hacker checker.", 2);
                                    }
                                }
                                else {
                                    //this.ConsoleError(aPlayer.player_name + " doesn't have stats.");
                                    repeatCheckingQueue.Enqueue(aPlayer);
                                }
                                this.ConsoleWrite("Players with " + this._GameVersion + "Stats: " + String.Format("{0:0.00}", (playersWithStats / checkedPlayers) * 100) + "%");
                            }
                        }
                    }
                }
                this.DebugWrite("HCKCHK: Ending Hacker Checker Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Hacker Checker thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Error occured in Hacker Checker thread.", e));
                }
            }
        }

        private void RunStatSiteHackCheck(AdKats_Player aPlayer, Boolean debug) {
            this.DebugWrite("HackerChecker running on " + aPlayer.player_name, 5);
            Boolean acted = false;
            if (this._UseDpsChecker) {
                this.DebugWrite("Preparing to DPS check " + aPlayer.player_name, 5);
                acted = this.DamageHackCheck(aPlayer, debug);
            }
            if (this._UseHskChecker && !acted) {
                this.DebugWrite("Preparing to HSK check " + aPlayer.player_name, 5);
                acted = this.AimbotHackCheck(aPlayer, debug);
            }
            if (!acted && debug) {
                this.ConsoleSuccess(aPlayer.player_name + " is clean.");
            }
        }

        private void RunGCPHackCheck(AdKats_Player aPlayer, Boolean verbose) {
            //Fetch hack status from GCP servers
            Hashtable hcResponse = this.FetchGCPHackCheck(aPlayer.player_name);
            String status = (String) hcResponse["status"];
            if (status == "success") {
                Hashtable response = (Hashtable) hcResponse["response"];
                String result = (String) response["result"];
                if (result == "dirty") {
                    this.ConsoleWarn(aPlayer.player_name + " is hacking.");
                    try {
                        List<AdKatsWeaponStats> weaponList = (from DictionaryEntry pair in (Hashtable) response["weapons"]
                            let weaponStats = (Hashtable) pair.Value
                            select new AdKatsWeaponStats {
                                                             id = (String) pair.Key,
                                                             kills = (Double) weaponStats["kills"],
                                                             headshots = (Double) weaponStats["headshots"],
                                                             dps = (Double) weaponStats["DPS"],
                                                             hskr = (Double) weaponStats["HKR"],
                                                             hits = (Double) weaponStats["hit"],
                                                             shots = (Double) weaponStats["shot"]
                                                         }).ToList();
                        AdKatsWeaponStats actedWeapon = null;
                        Double actedDPS = 0;
                        Double actedHSKR = 0;
                        foreach (AdKatsWeaponStats weapon in weaponList) {
                            if (weapon.dps > actedDPS) {
                                actedWeapon = weapon;
                                actedDPS = weapon.dps;
                            }
                            else if (weapon.hskr > actedHSKR) {
                                actedWeapon = weapon;
                                actedHSKR = weapon.hskr;
                            }
                        }
                        //Create record for player in actual player list
                        if (actedWeapon != null) {
                            AdKatsRecord record = new AdKatsRecord {
                                                                       record_source = AdKatsRecord.Sources.Automated,
                                                                       server_id = this._ServerID,
                                                                       command_type = this._CommandKeyDictionary["player_ban_perm"],
                                                                       command_numeric = 0,
                                                                       target_name = aPlayer.player_name,
                                                                       target_player = aPlayer,
                                                                       source_name = "AutoAdmin",
                                                                       record_message = "Hacking/Cheating Automatic Ban [" + actedWeapon.id.Replace("-", "").Replace(" ", "").ToUpper() + "-" + (int) actedWeapon.dps + "DPS-" + (int) (actedWeapon.hskr * 100) + "HSKR-" + (int) actedWeapon.kills + "]"
                                                                   };
                            //Process the record
                            this.QueueRecordForProcessing(record);
                            this.ConsoleWarn(aPlayer.player_name + " auto-banned for hacking. [" + actedWeapon.id.Replace("-", "").Replace(" ", "").ToUpper() + "-" + (int) actedWeapon.dps + "DPS-" + (int) (actedWeapon.hskr * 100) + "HSKR-" + (int) actedWeapon.kills + "]");
                        }
                        else {
                            this.ConsoleError("actedWeapon was null in hackerchecker.");
                        }
                    }
                    catch (Exception e) {
                        this.HandleException(new AdKatsException("Unable to parse player hack information", e));
                    }
                }
                else if (result == "clean") {
                    if (verbose) {
                        this.ConsoleSuccess(aPlayer.player_name + " is clean.");
                    }
                    else {
                        this.DebugWrite(aPlayer.player_name + " is clean.", 2);
                    }
                }
                else {
                    this.ConsoleError("Unknown hacker result '" + result + "'.");
                }
            }
            else {
                this.ConsoleError(aPlayer.player_name + " not found or could not be hacker-checked.");
            }
        }

        private Boolean DamageHackCheck(AdKats_Player player, Boolean debugMode) {
            List<String> allowedCategories = null;
            switch (this._GameVersion) {
                case GameVersion.BF3:
                    allowedCategories = new List<string>() {
                                                               "Sub machine guns",
                                                               "Assault rifles",
                                                               "Carbines",
                                                               "Machine guns",
                                                               "Handheld weapons"
                                                           };
                    break;
                case GameVersion.BF4:
                    allowedCategories = new List<string>() {
                                                               "PDW",
                                                               "ASSAULT RIFLE",
                                                               "CARBINE",
                                                               "LMG",
                                                               "SIDEARM"
                                                           };
                    break;
                default:
                    return false;
            }
            Boolean acted = false;
            List<AdKatsWeaponStats> topWeapons = player.stats.weaponStats.Values.ToList();
            topWeapons.Sort(delegate(AdKatsWeaponStats a1, AdKatsWeaponStats a2) {
                                if (a1.kills == a2.kills) {
                                    return 0;
                                }
                                return (a1.kills < a2.kills) ? (1) : (-1);
                            });

            AdKatsWeaponStats actedWeapon = null;
            Double actedPerc = -1;
            Int32 index = 0;
            foreach (AdKatsWeaponStats weaponStat in topWeapons) {
                //Break after 15th top weapon
                if (index++ > 15) {
                    break;
                }
                //Only count certain weapon categories
                if (allowedCategories.Contains(weaponStat.category)) {
                    StatLibraryWeapon weapon = null;
                    if (this._StatLibrary.weapons[this._GameVersion].TryGetValue(weaponStat.id, out weapon)) {
                        //Only handle weapons that do < 50 max dps
                        if (weapon.damage_max < 50) {
                            //Only take weapons with more than 50 kills
                            if (weaponStat.kills > 50) {
                                //Check for damage hack
                                if (weaponStat.dps > weapon.damage_max) {
                                    //Get the percentage over normal
                                    Double percDiff = (weaponStat.dps - weapon.damage_max) / weaponStat.dps;
                                    if (percDiff > (this._DpsTriggerLevel / 100)) {
                                        if (percDiff > actedPerc) {
                                            actedPerc = percDiff;
                                            actedWeapon = weaponStat;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else {
                        this.ConsoleWarn("Could not find " + weaponStat.id + " in " + this._GameVersion + " stat library.");
                    }
                }
            }
            if (actedWeapon != null) {
                acted = true;
                String formattedName = actedWeapon.id.Replace("-", "").Replace(" ", "").ToUpper();
                this.ConsoleError(player.player_name + " auto-banned for damage mod. [" + formattedName + "-" + (int) actedWeapon.dps + "-" + (int) actedWeapon.kills + "-" + (int) actedWeapon.headshots + "]");
                if (!debugMode) {
                    //Create the ban record
                    AdKatsRecord record = new AdKatsRecord {
                                                               record_source = AdKatsRecord.Sources.Automated,
                                                               server_id = this._ServerID,
                                                               command_type = this._CommandKeyDictionary["player_ban_perm"],
                                                               command_numeric = 0,
                                                               target_name = player.player_name,
                                                               target_player = player,
                                                               source_name = "AutoAdmin",
                                                               record_message = "Hacking/Cheating DPS Automatic Ban [" + formattedName + "-" + (int) actedWeapon.dps + "-" + (int) actedWeapon.kills + "-" + (int) actedWeapon.headshots + "]"
                                                           };
                    //Process the record
                    this.QueueRecordForProcessing(record);
                    //this.AdminSayMessage(player.player_name + " auto-banned for damage mod. [" + actedWeapon.id + "-" + (int) actedWeapon.dps + "-" + (int) actedWeapon.kills + "-" + (int) actedWeapon.headshots + "]");
                }
            }
            return acted;
        }

        private Boolean AimbotHackCheck(AdKats_Player player, Boolean debugMode) {
            List<String> allowedCategories;
            switch (this._GameVersion) {
                case GameVersion.BF3:
                    allowedCategories = new List<string>() {
                                                               "Sub machine guns",
                                                               "Assault rifles",
                                                               "Carbines",
                                                               "Machine guns"
                                                           };
                    break;
                case GameVersion.BF4:
                    allowedCategories = new List<string>() {
                                                               "PDW",
                                                               "ASSAULT RIFLE",
                                                               "CARBINE",
                                                               "LMG"
                                                           };
                    break;
                default:
                    return false;
            }
            Boolean acted = false;
            List<AdKatsWeaponStats> topWeapons = player.stats.weaponStats.Values.ToList();
            topWeapons.Sort(delegate(AdKatsWeaponStats a1, AdKatsWeaponStats a2) {
                                if (a1.kills == a2.kills) {
                                    return 0;
                                }
                                return (a1.kills < a2.kills) ? (1) : (-1);
                            });

            AdKatsWeaponStats actedWeapon = null;
            Double actedHskr = -1;
            Int32 index = 0;
            foreach (AdKatsWeaponStats weaponStat in topWeapons) {
                //Break after 15th top weapon
                if (index++ > 15) {
                    break;
                }
                //Only count certain weapon categories
                if (allowedCategories.Contains(weaponStat.category)) {
                    StatLibraryWeapon weapon = null;
                    if (this._StatLibrary.weapons[this._GameVersion].TryGetValue(weaponStat.id, out weapon)) {
                        //Only handle weapons that do < 50 max dps
                        if (weapon.damage_max < 50) {
                            //Only take weapons with more than 100 kills
                            if (weaponStat.kills > 100) {
                                //Check for aimbot hack
                                this.DebugWrite("Checking " + weaponStat.id + " HSKR (" + weaponStat.hskr + " >? " + (this._HskTriggerLevel / 100) + ")", 6);
                                if (weaponStat.hskr > (this._HskTriggerLevel / 100)) {
                                    if (weaponStat.hskr > actedHskr) {
                                        actedHskr = weaponStat.hskr;
                                        actedWeapon = weaponStat;
                                    }
                                }
                            }
                        }
                    }
                    else {
                        this.ConsoleWarn("Could not find " + weaponStat.id + " in " + this._GameVersion + " stat library.");
                    }
                }
            }
            if (actedWeapon != null) {
                acted = true;
                String formattedName = actedWeapon.id.Replace("-", "").Replace(" ", "").ToUpper();
                this.ConsoleError(player.player_name + " auto-banned for aimbot. [" + formattedName + "-" + (int) (actedWeapon.hskr * 100) + "-" + (int) actedWeapon.kills + "-" + (int) actedWeapon.headshots + "]");
                if (!debugMode) {
                    //Create the ban record
                    AdKatsRecord record = new AdKatsRecord {
                                                               record_source = AdKatsRecord.Sources.Automated,
                                                               server_id = this._ServerID,
                                                               command_type = this._CommandKeyDictionary["player_ban_perm"],
                                                               command_numeric = 0,
                                                               target_name = player.player_name,
                                                               target_player = player,
                                                               source_name = "AutoAdmin",
                                                               record_message = "Hacking/Cheating HSK Automatic Ban [" + formattedName + "-" + (int) (actedWeapon.hskr * 100) + "-" + (int) actedWeapon.kills + "-" + (int) actedWeapon.headshots + "]"
                                                           };
                    //Process the record
                    this.QueueRecordForProcessing(record);
                    //this.AdminSayMessage(player.player_name + " auto-banned for aimbot. [" + actedWeapon.id + "-" + (int) actedWeapon.hskr + "-" + (int) actedWeapon.kills + "-" + (int) actedWeapon.headshots + "]");
                }
            }
            return acted;
        }

        #endregion

        #region Messaging

        //all messaging is redirected to global chat for analysis
        public override void OnGlobalChat(String speaker, String message) {
            //this.uploadChatLog(speaker, "Global", message);
            this.HandleChat(speaker, message);
        }

        public override void OnTeamChat(String speaker, String message, Int32 teamId) {
            //this.uploadChatLog(speaker, "Team", message);
            this.HandleChat(speaker, message);
        }

        public override void OnSquadChat(String speaker, String message, Int32 teamId, Int32 squadId) {
            //this.uploadChatLog(speaker, "Squad", message);
            this.HandleChat(speaker, message);
        }

        private void HandleChat(String speaker, String message) {
            this.DebugWrite("Entering handleChat", 7);
            try {
                if (_IsEnabled) {
                    //Performance testing area
                    if (speaker == this._DebugSoldierName) {
                        this._CommandStartTime = DateTime.UtcNow;
                    }
                    //If message contains comorose just return and ignore
                    if (message.Contains("ComoRose:")) {
                        return;
                    }
                    this.QueueMessageForParsing(speaker, message);
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while processing inbound chat messages.", e));
            }
            this.DebugWrite("Exiting handleChat", 7);
        }

        public String SendMessageToSource(AdKatsRecord record, String message) {
            this.DebugWrite("Entering sendMessageToSource", 7);
            String returnMessage = null;
            try {
                switch (record.record_source) {
                    case AdKatsRecord.Sources.InGame:
                        if (!String.IsNullOrEmpty(message)) {
                            this.PlayerSayMessage(record.source_name, message);
                        }
                        else {
                            this.ConsoleError("message null in sendMessageToSource");
                        }
                        break;
                    case AdKatsRecord.Sources.ProconChat:
                        this.ProconChatWrite(message);
                        break;
                    case AdKatsRecord.Sources.Settings:
                        this.ConsoleWrite(message);
                        break;
                    case AdKatsRecord.Sources.Database:
                        //Do nothing, no way to communicate to source when database
                        break;
                    case AdKatsRecord.Sources.Automated:
                        //Do nothing, no source to communicate with
                        break;
                    case AdKatsRecord.Sources.External:
                        returnMessage = message;
                        break;
                    case AdKatsRecord.Sources.HTTP:
                        returnMessage = message;
                        break;
                    default:
                        this.ConsoleWarn("Command source not set, or not recognized.");
                        break;
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while sending message to record source.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting sendMessageToSource", 7);
            return returnMessage;
        }

        public void AdminSayMessage(String message) {
            this.DebugWrite("Entering adminSay", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    this.ConsoleError("message null in adminSay");
                    return;
                }
                this.ProconChatWrite("Say > All > " + message);
                this.ExecuteCommand("procon.protected.send", "admin.say", message, "all");
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while sending admin say.", e));
            }
            this.DebugWrite("Exiting adminSay", 7);
        }

        public void PlayerSayMessage(String target, String message) {
            this.DebugWrite("Entering playerSayMessage", 7);
            try {
                if (String.IsNullOrEmpty(target) || String.IsNullOrEmpty(message)) {
                    this.ConsoleError("target or message null in playerSayMessage");
                    return;
                }
                this.ProconChatWrite("Say > " + target + " > " + message);
                ExecuteCommand("procon.protected.send", "admin.say", message, "player", target);
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while sending message to player.", e));
            }
            this.DebugWrite("Exiting playerSayMessage", 7);
        }

        public void AdminYellMessage(String message) {
            this.DebugWrite("Entering adminYell", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    this.ConsoleError("message null in adminYell");
                    return;
                }
                this.ProconChatWrite("Yell > All > " + message);
                this.ExecuteCommand("procon.protected.send", "admin.yell", message.ToUpper(), this._YellDuration + "", "all");
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            this.DebugWrite("Exiting adminYell", 7);
        }

        public void PlayerYellMessage(String target, String message) {
            this.DebugWrite("Entering adminYell", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    this.ConsoleError("message null in adminYell");
                    return;
                }
                this.ProconChatWrite("Yell > " + target + " > " + message);
                this.ExecuteCommand("procon.protected.send", "admin.yell", message.ToUpper(), this._YellDuration + "", "player", target);
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            this.DebugWrite("Exiting adminYell", 7);
        }

        public void AdminTellMessage(String message) {
            this.AdminSayMessage(message);
            this.AdminYellMessage(message);
        }

        public void PlayerTellMessage(String target, String message) {
            this.PlayerSayMessage(target, message);
            this.PlayerYellMessage(target, message);
        }

        private void QueueMessageForParsing(String speaker, String message) {
            this.DebugWrite("Entering queueMessageForParsing", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue message for parsing", 6);
                    lock (_UnparsedMessageMutex) {
                        this._UnparsedMessageQueue.Enqueue(new KeyValuePair<String, String>(speaker, message));
                        this.DebugWrite("Message queued for parsing.", 6);
                        this._MessageParsingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing chat message for parsing.", e));
            }
            this.DebugWrite("Exiting queueMessageForParsing", 7);
        }

        private void QueueCommandForParsing(String speaker, String command) {
            this.DebugWrite("Entering queueCommandForParsing", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue command for parsing", 6);
                    lock (_UnparsedCommandMutex) {
                        this._UnparsedCommandQueue.Enqueue(new KeyValuePair<String, String>(speaker, command));
                        this.DebugWrite("Command sent to unparsed commands.", 6);
                        this._CommandParsingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing command for parsing.", e));
            }
            this.DebugWrite("Exiting queueCommandForParsing", 7);
        }

        private void MessagingThreadLoop() {
            try {
                this.DebugWrite("MESSAGE: Starting Messaging Thread", 2);
                Thread.CurrentThread.Name = "messaging";
                while (true) {
                    this.DebugWrite("MESSAGE: Entering Messaging Thread Loop", 7);
                    if (!this._IsEnabled) {
                        this.DebugWrite("MESSAGE: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Get all unparsed inbound messages
                    Queue<KeyValuePair<String, String>> inboundMessages;
                    if (this._UnparsedMessageQueue.Count > 0) {
                        this.DebugWrite("MESSAGE: Preparing to lock messaging to retrive new messages", 7);
                        lock (_UnparsedMessageMutex) {
                            this.DebugWrite("MESSAGE: Inbound messages found. Grabbing.", 6);
                            //Grab all messages in the queue
                            inboundMessages = new Queue<KeyValuePair<String, String>>(this._UnparsedMessageQueue.ToArray());
                            //Clear the queue for next run
                            this._UnparsedMessageQueue.Clear();
                        }
                    }
                    else {
                        this.DebugWrite("MESSAGE: No inbound messages. Waiting for Input.", 4);
                        //Wait for input
                        this._MessageParsingWaitHandle.Reset();
                        this._MessageParsingWaitHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }

                    //Loop through all messages in order that they came in
                    while (inboundMessages.Count > 0) {
                        this.DebugWrite("MESSAGE: begin reading message", 6);
                        //Dequeue the first/next message
                        KeyValuePair<String, String> messagePair = inboundMessages.Dequeue();
                        String speaker = messagePair.Key;
                        String message = messagePair.Value;

                        //check for player mute case
                        //ignore if it's a server call
                        if (speaker != "Server") {
                            lock (_PlayersMutex) {
                                //Check if the player is muted
                                this.DebugWrite("MESSAGE: Checking for mute case.", 7);
                                if (this._RoundMutedPlayers.ContainsKey(speaker)) {
                                    this.DebugWrite("MESSAGE: Player is muted. Acting.", 7);
                                    //Increment the muted chat count
                                    this._RoundMutedPlayers[speaker] = this._RoundMutedPlayers[speaker] + 1;
                                    //Create record
                                    AdKatsRecord record = new AdKatsRecord();
                                    record.record_source = AdKatsRecord.Sources.Automated;
                                    record.server_id = this._ServerID;
                                    record.source_name = "PlayerMuteSystem";
                                    record.target_player = this._PlayerDictionary[speaker];
                                    record.target_name = record.target_player.player_name;
                                    if (this._RoundMutedPlayers[speaker] > this._MutedPlayerChances) {
                                        record.record_message = this._MutedPlayerKickMessage;
                                        record.command_type = this._CommandKeyDictionary["player_kick"];
                                        record.command_action = this._CommandKeyDictionary["player_kick"];
                                    }
                                    else {
                                        record.record_message = _MutedPlayerKillMessage;
                                        record.command_type = this._CommandKeyDictionary["player_kill"];
                                        record.command_action = this._CommandKeyDictionary["player_kill"];
                                    }

                                    this.QueueRecordForProcessing(record);
                                    continue;
                                }
                            }
                        }

                        //Check if the message is a command
                        if (message.StartsWith("@") || message.StartsWith("!")) {
                            message = message.Substring(1);
                        }
                        else if (message.StartsWith("/@") || message.StartsWith("/!")) {
                            message = message.Substring(2);
                        }
                        else if (message.StartsWith("/")) {
                            message = message.Substring(1);
                        }
                        else {
                            //If the message does not cause either of the above clauses, then ignore it.
                            this.DebugWrite("MESSAGE: Message is regular chat. Ignoring.", 7);
                            continue;
                        }
                        this.QueueCommandForParsing(speaker, message);
                    }
                }
                this.DebugWrite("MESSAGE: Ending Messaging Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Messaging thread aborted. Attempting to restart.", e));
                    this.DebugWrite("Thread Exception", 4);
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Error occured in messaging thread.", e));
                }
            }
        }

        #endregion

        #region Teamswap Methods

        private void QueuePlayerForForceMove(CPlayerInfo player) {
            this.DebugWrite("Entering queuePlayerForForceMove", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to queue player for TeamSwap ", 6);
                    lock (_TeamswapMutex) {
                        this._TeamswapForceMoveQueue.Enqueue(player);
                        this._TeamswapWaitHandle.Set();
                        this.DebugWrite("Player queued for TeamSwap", 6);
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing player for force-move.", e));
            }
            this.DebugWrite("Exiting queuePlayerForForceMove", 7);
        }

        private void QueuePlayerForMove(CPlayerInfo player) {
            this.DebugWrite("Entering queuePlayerForMove", 7);
            try {
                if (this._IsEnabled) {
                    this.DebugWrite("Preparing to add player to 'on-death' move dictionary.", 6);
                    lock (_TeamswapMutex) {
                        if (!this._TeamswapOnDeathMoveDic.ContainsKey(player.SoldierName)) {
                            this._TeamswapOnDeathMoveDic.Add(player.SoldierName, player);
                            this._TeamswapWaitHandle.Set();
                            this.DebugWrite("Player added to 'on-death' move dictionary.", 6);
                        }
                        else {
                            this.DebugWrite("Player already in 'on-death' move dictionary.", 6);
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing player for move.", e));
            }
            this.DebugWrite("Exiting queuePlayerForMove", 7);
        }

        //runs through both team swap queues and performs the swapping
        public void TeamswapThreadLoop() {
            //assume the max player count per team is 32 if no server info has been provided
            Int32 maxPlayerCount = 32;
            try {
                this.DebugWrite("TSWAP: Starting TeamSwap Thread", 2);
                Thread.CurrentThread.Name = "teamswap";
                while (true) {
                    this.DebugWrite("TSWAP: Entering TeamSwap Thread Loop", 7);
                    if (!this._IsEnabled) {
                        this.DebugWrite("TSWAP: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Call List Players
                    this._PlayerListUpdateWaitHandle.Reset();
                    this.ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
                    //Wait for listPlayers to finish
                    if (!this._PlayerListUpdateWaitHandle.WaitOne(2000)) {
                        this.DebugWrite("ListPlayers ran out of time for TeamSwap. 2 sec.", 1);
                    }

                    //Refresh Max Player Count, needed for responsive server size
                    CServerInfo info = this.GetServerInfo();
                    if (info != null && info.MaxPlayerCount != maxPlayerCount) {
                        maxPlayerCount = info.MaxPlayerCount / 2;
                    }

                    //Get players who died that need moving
                    if ((this._TeamswapOnDeathMoveDic.Count > 0 && this._TeamswapOnDeathCheckingQueue.Count > 0) || this._TeamswapForceMoveQueue.Count > 0) {
                        this.DebugWrite("TSWAP: Preparing to lock TeamSwap queues", 4);
                        lock (_TeamswapMutex) {
                            this.DebugWrite("TSWAP: Players in ready for TeamSwap. Grabbing.", 6);
                            //Grab all messages in the queue
                            Queue<CPlayerInfo> movingQueue = new Queue<CPlayerInfo>(this._TeamswapForceMoveQueue.ToArray());
                            Queue<CPlayerInfo> checkingQueue = new Queue<CPlayerInfo>(this._TeamswapOnDeathCheckingQueue.ToArray());
                            //Clear the queue for next run
                            this._TeamswapOnDeathCheckingQueue.Clear();
                            this._TeamswapForceMoveQueue.Clear();

                            //Check for "on-death" move players
                            while (this._TeamswapOnDeathMoveDic.Count > 0 && checkingQueue.Count > 0) {
                                //Dequeue the first/next player
                                String playerName = checkingQueue.Dequeue().SoldierName;
                                CPlayerInfo player;
                                //If they are 
                                if (this._TeamswapOnDeathMoveDic.TryGetValue(playerName, out player)) {
                                    //Player has died, remove from the dictionary
                                    this._TeamswapOnDeathMoveDic.Remove(playerName);
                                    //Add to move queue
                                    movingQueue.Enqueue(player);
                                }
                            }

                            while (movingQueue.Count > 0) {
                                CPlayerInfo player = movingQueue.Dequeue();
                                if (player.TeamID == UsTeamID) {
                                    if (!this.ContainsCPlayerInfo(this._UsMoveQueue, player.SoldierName)) {
                                        this._UsMoveQueue.Enqueue(player);
                                        this.PlayerSayMessage(player.SoldierName, "You have been added to the (US -> RU) TeamSwap queue in position " + (this.IndexOfCPlayerInfo(this._UsMoveQueue, player.SoldierName) + 1) + ".");
                                    }
                                    else {
                                        this.PlayerSayMessage(player.SoldierName, "RU Team Full (" + this._RuPlayerCount + "/" + maxPlayerCount + "). You are in queue position " + (this.IndexOfCPlayerInfo(this._UsMoveQueue, player.SoldierName) + 1));
                                    }
                                }
                                else {
                                    if (!this.ContainsCPlayerInfo(this._RuMoveQueue, player.SoldierName)) {
                                        this._RuMoveQueue.Enqueue(player);
                                        this.PlayerSayMessage(player.SoldierName, "You have been added to the (RU -> US) TeamSwap queue in position " + (this.IndexOfCPlayerInfo(this._RuMoveQueue, player.SoldierName) + 1) + ".");
                                    }
                                    else {
                                        this.PlayerSayMessage(player.SoldierName, "US Team Full (" + this._UsPlayerCount + "/" + maxPlayerCount + "). You are in queue position " + (this.IndexOfCPlayerInfo(this._RuMoveQueue, player.SoldierName) + 1));
                                    }
                                }
                            }
                        }
                    }
                    this.DebugWrite("Team Info: US:" + this._UsPlayerCount + "/" + maxPlayerCount + " RU:" + this._RuPlayerCount + "/" + maxPlayerCount, 5);
                    if (this._RuMoveQueue.Count > 0 || this._UsMoveQueue.Count > 0) {
                        //Perform player moving
                        Boolean movedPlayer;
                        do {
                            movedPlayer = false;
                            if (this._RuMoveQueue.Count > 0) {
                                if (this._UsPlayerCount < maxPlayerCount) {
                                    CPlayerInfo player = this._RuMoveQueue.Dequeue();
                                    AdKats_Player dicPlayer = null;
                                    if (this._PlayerDictionary.TryGetValue(player.SoldierName, out dicPlayer)) {
                                        if (dicPlayer.frostbitePlayerInfo.TeamID == UsTeamID) {
                                            //Skip the kill/swap if they are already on the goal team by some other means
                                            continue;
                                        }
                                    }
                                    if (String.IsNullOrEmpty(player.SoldierName) || String.IsNullOrEmpty(UsTeamID.ToString(CultureInfo.InvariantCulture))) {
                                        this.ConsoleError("soldiername null in US teamswap");
                                    }
                                    else {
                                        ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, UsTeamID.ToString(CultureInfo.InvariantCulture), "1", "true");
                                    }
                                    this.PlayerSayMessage(player.SoldierName, "Swapping you from team RU to team US");
                                    movedPlayer = true;
                                }
                            }
                            if (this._UsMoveQueue.Count > 0) {
                                if (this._RuPlayerCount < maxPlayerCount) {
                                    CPlayerInfo player = this._UsMoveQueue.Dequeue();
                                    AdKats_Player dicPlayer = null;
                                    if (this._PlayerDictionary.TryGetValue(player.SoldierName, out dicPlayer)) {
                                        if (dicPlayer.frostbitePlayerInfo.TeamID == RuTeamID) {
                                            //Skip the kill/swap if they are already on the goal team by some other means
                                            continue;
                                        }
                                    }
                                    if (String.IsNullOrEmpty(player.SoldierName) || String.IsNullOrEmpty(RuTeamID.ToString(CultureInfo.InvariantCulture))) {
                                        this.ConsoleError("soldiername null in RU teamswap");
                                    }
                                    else {
                                        ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, RuTeamID.ToString(CultureInfo.InvariantCulture), "1", "true");
                                    }
                                    this.PlayerSayMessage(player.SoldierName, "Swapping you from team US to team RU");
                                    movedPlayer = true;
                                }
                            }
                        } while (movedPlayer);

                        //Sleep for 5 seconds
                        Thread.Sleep(5000);
                    }
                    else {
                        this.DebugWrite("TSWAP: No players to swap. Waiting for input.", 4);
                        //There are no players to swap, wait.
                        this._TeamswapWaitHandle.Reset();
                        this._TeamswapWaitHandle.WaitOne(Timeout.Infinite);
                    }
                }
                this.DebugWrite("TSWAP: Ending TeamSwap Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Teamswap thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Error occured in teamswap thread.", e));
                }
            }
        }

        //Whether a move queue contains a given player
        private bool ContainsCPlayerInfo(Queue<CPlayerInfo> queueList, String player) {
            this.DebugWrite("Entering containsCPlayerInfo", 7);
            try {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 index = 0; index < queueList.Count; index++) {
                    if (playerArray[index].SoldierName == player) {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while checking for player in teamswap queue.", e));
            }
            this.DebugWrite("Exiting containsCPlayerInfo", 7);
            return false;
        }

        //Helper method to find a player's information in the move queue
        private CPlayerInfo GetCPlayerInfo(Queue<CPlayerInfo> queueList, String player) {
            this.DebugWrite("Entering getCPlayerInfo", 7);
            CPlayerInfo playerInfo = null;
            try {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 index = 0; index < queueList.Count; index++) {
                    if (playerArray[index].SoldierName == player) {
                        playerInfo = playerArray[index];
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while getting player info from teamswap queue.", e));
            }
            this.DebugWrite("Exiting getCPlayerInfo", 7);
            return playerInfo;
        }

        //The index of a player in the move queue
        private Int32 IndexOfCPlayerInfo(Queue<CPlayerInfo> queueList, String player) {
            this.DebugWrite("Entering getCPlayerInfo", 7);
            try {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 i = 0; i < queueList.Count; i++) {
                    if (playerArray[i].SoldierName == player) {
                        return i;
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while getting index of player in teamswap queue.", e));
            }
            this.DebugWrite("Exiting getCPlayerInfo", 7);
            return -1;
        }

        #endregion

        #region Record Creation and Processing

        private void QueueRecordForProcessing(AdKatsRecord record) {
            this.DebugWrite("Entering queueRecordForProcessing", 7);
            try {
                this.DebugWrite("Preparing to queue record for processing", 6);
                lock (_UnprocessedRecordMutex) {
                    //Queue the record for processing
                    this._UnprocessedRecordQueue.Enqueue(record);
                    this.DebugWrite("Record queued for processing", 6);
                    this._DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while queueing record for processing.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting queueRecordForProcessing", 7);
        }

        private void CommandParsingThreadLoop() {
            try {
                this.DebugWrite("COMMAND: Starting Command Parsing Thread", 2);
                Thread.CurrentThread.Name = "Command";
                while (true) {
                    this.DebugWrite("COMMAND: Entering Command Parsing Thread Loop", 7);
                    if (!this._IsEnabled) {
                        this.DebugWrite("COMMAND: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Get all unparsed inbound messages
                    if (this._UnparsedCommandQueue.Count > 0) {
                        this.DebugWrite("COMMAND: Preparing to lock command queue to retrive new commands", 7);
                        Queue<KeyValuePair<String, String>> unparsedCommands;
                        lock (_UnparsedCommandMutex) {
                            this.DebugWrite("COMMAND: Inbound commands found. Grabbing.", 6);
                            //Grab all messages in the queue
                            unparsedCommands = new Queue<KeyValuePair<String, String>>(this._UnparsedCommandQueue.ToArray());
                            //Clear the queue for next run
                            this._UnparsedCommandQueue.Clear();
                        }

                        //Loop through all commands in order that they came in
                        while (unparsedCommands.Count > 0) {
                            this.DebugWrite("COMMAND: begin reading command", 6);
                            //Dequeue the first/next command
                            KeyValuePair<String, String> commandPair = unparsedCommands.Dequeue();
                            String speaker = commandPair.Key;
                            String command = commandPair.Value;

                            AdKatsRecord record = new AdKatsRecord {
                                                                       record_source = speaker == "Server" ? AdKatsRecord.Sources.ProconChat : AdKatsRecord.Sources.InGame,
                                                                       source_name = speaker
                                                                   };
                            //Complete the record creation
                            this.CompleteRecord(record, command);
                        }
                    }
                    else {
                        this.DebugWrite("COMMAND: No inbound commands, ready.", 7);
                        //No commands to parse, ready.
                        this._CommandParsingWaitHandle.Reset();
                        this._CommandParsingWaitHandle.WaitOne(Timeout.Infinite);
                    }
                }
                this.DebugWrite("COMMAND: Ending Command Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Command parsing thread aborted. Attempting to restart.", e));
                    this.DebugWrite("COMMAND: Thread Exception", 4);
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Error occured in command parsing thread.", e));
                }
            }
        }

        //Before calling this, the record is initialized, and command_source/source_name are filled
        public void CompleteRecord(AdKatsRecord record, String message) {
            try {
                //Initial split of command by whitespace
                String[] splitMessage = message.Split(' ');
                if (splitMessage.Length < 1) {
                    this.DebugWrite("Completely blank command entered", 5);
                    this.SendMessageToSource(record, "You entered a completely blank command.");
                    return;
                }
                String commandString = splitMessage[0].ToLower();
                DebugWrite("Raw Command: " + commandString, 6);
                String remainingMessage = message.TrimStart(splitMessage[0].ToCharArray()).Trim();

                //GATE 1: Add general data
                record.server_id = this._ServerID;
                record.record_time = DateTime.UtcNow;

                //GATE 2: Add Command
                AdKatsCommand commandType = null;
                if (this._CommandTextDictionary.TryGetValue(commandString, out commandType)) {
                    record.command_type = commandType;
                    record.command_action = commandType;
                    this.DebugWrite("Command parsed. Command is " + commandType.command_key + ".", 5);
                }
                else {
                    //If command not parsable, return without creating
                    DebugWrite("Command not parsable", 6);
                    return;
                }

                //GATE 3: Check Access Rights
                //Check for server command case
                if (record.source_name == "Server") {
                    record.source_name = "PRoConAdmin";
                }
                    //Check if player has the right to perform what he's asking, only perform for InGame actions
                else if (record.record_source == AdKatsRecord.Sources.InGame) {
                    //Attempt to fetch the source player
                    if (!this._PlayerDictionary.TryGetValue(record.source_name, out record.source_player)) {
                        this.ConsoleError("Source player not found in server for in-game command, unable to complete command.");
                        return;
                    }
                    if (!this.HasAccess(record.source_player, record.command_type)) {
                        DebugWrite("No rights to call command", 6);
                        this.SendMessageToSource(record, "Your user role " + record.source_player.player_role.role_name + " does not have access to " + record.command_type.command_name + ".");
                        //Return without creating if player doesn't have rights to do it
                        return;
                    }
                }

                //GATE 4: Add specific data based on command type.
                switch (record.command_type.command_key) {
                        #region MovePlayer

                    case "player_move": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.record_message = "MovePlayer";
                                record.target_name = parameters[0];
                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    this.CompleteTargetInformation(record, false);
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region ForceMovePlayer

                    case "player_fmove": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.record_message = "ForceMovePlayer";
                                record.target_name = parameters[0];
                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    this.CompleteTargetInformation(record, false);
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region Teamswap

                    case "self_teamswap": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //May only call this command from in-game
                        if (record.record_source != AdKatsRecord.Sources.InGame) {
                            this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                            break;
                        }
                        record.record_message = "TeamSwap";
                        record.target_name = record.source_name;
                        this.CompleteTargetInformation(record, false);
                    }
                        break;

                        #endregion

                        #region KillSelf

                    case "self_kill": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //May only call this command from in-game
                        if (record.record_source != AdKatsRecord.Sources.InGame) {
                            this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                            break;
                        }
                        record.record_message = "Self-Inflicted";
                        record.target_name = record.source_name;
                        this.CompleteTargetInformation(record, false);
                    }
                        break;

                        #endregion

                        #region KillPlayer

                    case "player_kill": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, false);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!this.HandleRoundReport(record)) {
                                    this.SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], this._RequirePreMessageUse);
                                if (record.record_message == null) {
                                    this.SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this._PreMessageList.Count);
                                    break;
                                }

                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    if (record.record_message.Length >= this._RequiredReasonLength) {
                                        this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region KickPlayer

                    case "player_kick": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!this.HandleRoundReport(record)) {
                                    this.SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], this._RequirePreMessageUse);
                                if (record.record_message == null) {
                                    this.SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this._PreMessageList.Count);
                                    break;
                                }

                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    if (record.record_message.Length >= this._RequiredReasonLength) {
                                        this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region TempBanPlayer

                    case "player_ban_temp": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 3);

                        //Default is minutes
                        Double recordDuration = 0.0;
                        Double durationMultiplier = 1.0;
                        if (parameters.Length > 0) {
                            String stringDuration = parameters[0].ToLower();
                            DebugWrite("Raw Duration: " + stringDuration, 6);
                            if (stringDuration.EndsWith("s")) {
                                stringDuration = stringDuration.TrimEnd('s');
                                durationMultiplier = (1.0 / 60.0);
                            }
                            else if (stringDuration.EndsWith("m")) {
                                stringDuration = stringDuration.TrimEnd('m');
                                durationMultiplier = 1.0;
                            }
                            else if (stringDuration.EndsWith("h")) {
                                stringDuration = stringDuration.TrimEnd('h');
                                durationMultiplier = 60.0;
                            }
                            else if (stringDuration.EndsWith("d")) {
                                stringDuration = stringDuration.TrimEnd('d');
                                durationMultiplier = 1440.0;
                            }
                            else if (stringDuration.EndsWith("w")) {
                                stringDuration = stringDuration.TrimEnd('w');
                                durationMultiplier = 10080.0;
                            }
                            if (!Double.TryParse(stringDuration, out recordDuration)) {
                                this.SendMessageToSource(record, "Invalid time given, unable to submit.");
                                return;
                            }
                            record.command_numeric = (int) (recordDuration * durationMultiplier);
                        }

                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                break;
                            case 1:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.command_numeric = (int) (recordDuration * durationMultiplier);
                                //Target is source
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 2:
                                record.command_numeric = (int) (recordDuration * durationMultiplier);

                                record.target_name = parameters[1];
                                DebugWrite("target: " + record.target_name, 6);

                                //Handle based on report ID as only option
                                if (!this.HandleRoundReport(record)) {
                                    this.SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                break;
                            case 3:
                                record.command_numeric = (int) (recordDuration * durationMultiplier);

                                record.target_name = parameters[1];
                                DebugWrite("target: " + record.target_name, 6);

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[2], this._RequirePreMessageUse);
                                if (record.record_message == null) {
                                    this.SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this._PreMessageList.Count);
                                    break;
                                }

                                DebugWrite("reason: " + record.record_message, 6);

                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    if (record.record_message.Length >= this._RequiredReasonLength) {
                                        this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region PermabanPlayer

                    case "player_ban_perm": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!this.HandleRoundReport(record)) {
                                    this.SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], this._RequirePreMessageUse);
                                if (record.record_message == null) {
                                    this.SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this._PreMessageList.Count);
                                    break;
                                }

                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    if (record.record_message.Length >= this._RequiredReasonLength) {
                                        this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region PunishPlayer

                    case "player_punish": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!this.HandleRoundReport(record)) {
                                    this.SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], this._RequirePreMessageUse);
                                if (record.record_message == null) {
                                    this.SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this._PreMessageList.Count);
                                    break;
                                }

                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    if (record.record_message.Length >= this._RequiredReasonLength) {
                                        this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region ForgivePlayer

                    case "player_forgive": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!this.HandleRoundReport(record)) {
                                    this.SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], this._RequirePreMessageUse);
                                if (record.record_message == null) {
                                    this.SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this._PreMessageList.Count);
                                    break;
                                }

                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    if (record.record_message.Length >= this._RequiredReasonLength) {
                                        this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region MutePlayer

                    case "player_mute": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!this.HandleRoundReport(record)) {
                                    this.SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], this._RequirePreMessageUse);
                                if (record.record_message == null) {
                                    this.SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this._PreMessageList.Count);
                                    break;
                                }

                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    if (record.record_message.Length >= this._RequiredReasonLength) {
                                        this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region JoinPlayer

                    case "player_join": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "foreveralone.jpg");
                                return;
                            case 1:
                                record.target_name = parameters[0];
                                record.record_message = "Joining Player";
                                if (!this.HandleRoundReport(record)) {
                                    this.CompleteTargetInformation(record, false);
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region RoundWhitelistPlayer

                    case "player_roundwhitelist": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    this.SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                this.CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!this.HandleRoundReport(record)) {
                                    this.SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], false);

                                //Handle based on report ID if possible
                                if (!this.HandleRoundReport(record)) {
                                    if (record.record_message.Length >= this._RequiredReasonLength) {
                                        this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                    }
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region ReportPlayer

                    case "player_report": {
                        //Get the command text for report
                        String command = this._CommandKeyDictionary["player_report"].command_text;

                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                break;
                            case 1:
                                this.SendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                break;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], false);

                                DebugWrite("reason: " + record.record_message, 6);

                                //Only 1 character reasons are required for reports and admin calls
                                if (record.record_message.Length >= 1) {
                                    this.CompleteTargetInformation(record, false);
                                }
                                else {
                                    DebugWrite("reason too short", 6);
                                    this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region CallAdmin

                    case "player_calladmin": {
                        //Get the command text for call admin
                        String command = this._CommandKeyDictionary["player_calladmin"].command_text;

                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                break;
                            case 1:
                                this.SendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                break;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                //attempt to handle via pre-message ID
                                record.record_message = this.GetPreMessage(parameters[1], false);

                                DebugWrite("reason: " + record.record_message, 6);
                                //Only 1 character reasons are required for reports and admin calls
                                if (record.record_message.Length >= 1) {
                                    this.CompleteTargetInformation(record, false);
                                }
                                else {
                                    DebugWrite("reason too short", 6);
                                    this.SendMessageToSource(record, "Reason too short, unable to submit.");
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region NukeServer

                    case "server_nuke": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                break;
                            case 1:
                                String targetTeam = parameters[0];
                                record.record_message = "Nuke Server";
                                DebugWrite("target: " + targetTeam, 6);
                                if (targetTeam.ToLower().Contains("us")) {
                                    record.target_name = "US Team";
                                    record.record_message += " (US Team)";
                                }
                                else if (targetTeam.ToLower().Contains("ru")) {
                                    record.target_name = "RU Team";
                                    record.record_message += " (RU Team)";
                                }
                                else if (targetTeam.ToLower().Contains("all")) {
                                    record.target_name = "Everyone";
                                    record.record_message += " (Everyone)";
                                }
                                else {
                                    this.SendMessageToSource(record, "Use 'US', 'RU', or 'ALL' as targets.");
                                }
                                //Have the admin confirm the action
                                this.ConfirmActionWithSource(record);
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region KickAll

                    case "server_kickall":
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        record.target_name = "Non-Admins";
                        record.record_message = "Kick All Players";
                        this.ConfirmActionWithSource(record);
                        break;

                        #endregion

                        #region EndLevel

                    case "round_end": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                return;
                            case 1:
                                String targetTeam = parameters[0];
                                DebugWrite("target team: " + targetTeam, 6);
                                record.record_message = "End Round";
                                if (targetTeam.ToLower().Contains("us")) {
                                    record.target_name = "US Team";
                                    record.command_numeric = AdKats.UsTeamID;
                                    record.record_message += " (US Win)";
                                }
                                else if (targetTeam.ToLower().Contains("ru")) {
                                    record.target_name = "RU Team";
                                    record.command_numeric = AdKats.RuTeamID;
                                    record.record_message += " (RU Win)";
                                }
                                else {
                                    this.SendMessageToSource(record, "Use 'US' or 'RU' as team names to end round");
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                        //Have the admin confirm the action
                        this.ConfirmActionWithSource(record);
                    }
                        break;

                        #endregion

                        #region RestartLevel

                    case "round_restart":
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        record.target_name = "Server";
                        record.record_message = "Restart Round";
                        this.ConfirmActionWithSource(record);
                        break;

                        #endregion

                        #region NextLevel

                    case "round_next":
                        this.CancelSourcePendingAction(record);

                        if (this._ServerType == "OFFICIAL") {
                            this.SendMessageToSource(record, record.command_type + " cannot be performed on official servers.");
                            return;
                        }

                        record.target_name = "Server";
                        record.record_message = "Run Next Map";
                        this.ConfirmActionWithSource(record);
                        break;

                        #endregion

                        #region WhatIs

                    case "self_whatis": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                return;
                            case 1:
                                record.record_message = this.GetPreMessage(parameters[0], true);
                                if (record.record_message == null) {
                                    this.SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this._PreMessageList.Count);
                                }
                                else {
                                    this.SendMessageToSource(record, record.record_message);
                                }
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                        //This type is not processed
                    }
                        break;

                        #endregion

                        #region RequestVoip

                    case "self_voip": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Send them voip information
                        this.SendMessageToSource(record, this._ServerVoipAddress);
                    }
                        break;

                        #endregion

                        #region RequestRules

                    case "self_rules": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        record.record_message = "Player Requested Rules";
                        record.target_name = record.source_name;
                        this.CompleteTargetInformation(record, false);
                    }
                        break;

                        #endregion

                        #region AdminSay

                    case "admin_say": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                return;
                            case 1:
                                record.record_message = this.GetPreMessage(parameters[0], false);
                                DebugWrite("message: " + record.record_message, 6);
                                record.target_name = "Server";
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                        this.QueueRecordForProcessing(record);
                    }
                        break;

                        #endregion

                        #region PlayerSay

                    case "player_say": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                return;
                            case 1:
                                this.SendMessageToSource(record, "No message given, unable to submit.");
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = this.GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                this.CompleteTargetInformation(record, false);
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region AdminYell

                    case "admin_yell": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                return;
                            case 1:
                                record.record_message = this.GetPreMessage(parameters[0], false);
                                DebugWrite("message: " + record.record_message, 6);
                                record.target_name = "Server";
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                        this.QueueRecordForProcessing(record);
                    }
                        break;

                        #endregion

                        #region PlayerYell

                    case "player_yell": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                return;
                            case 1:
                                this.SendMessageToSource(record, "No message given, unable to submit.");
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = this.GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                this.CompleteTargetInformation(record, false);
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region AdminTell

                    case "admin_tell": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                return;
                            case 1:
                                record.record_message = this.GetPreMessage(parameters[0], false);
                                DebugWrite("message: " + record.record_message, 6);
                                record.target_name = "Server";
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                        this.QueueRecordForProcessing(record);
                    }
                        break;

                        #endregion

                        #region PlayerTell

                    case "player_tell": {
                        //Remove previous commands awaiting confirmation
                        this.CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = this.ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                this.SendMessageToSource(record, "No parameters given, unable to submit.");
                                return;
                            case 1:
                                this.SendMessageToSource(record, "No message given, unable to submit.");
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = this.GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                this.CompleteTargetInformation(record, false);
                                break;
                            default:
                                this.SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;

                        #endregion

                        #region ConfirmCommand

                    case "command_confirm":
                        this.DebugWrite("attempting to confirm command", 6);
                        AdKatsRecord recordAttempt = null;
                        this._ActionConfirmDic.TryGetValue(record.source_name, out recordAttempt);
                        if (recordAttempt != null) {
                            this.DebugWrite("command found, calling processing", 6);
                            this._ActionConfirmDic.Remove(record.source_name);
                            this.QueueRecordForProcessing(recordAttempt);
                        }
                        else {
                            this.DebugWrite("no command to confirm", 6);
                            this.SendMessageToSource(record, "No command to confirm.");
                        }
                        //This type is not processed
                        break;

                        #endregion

                        #region CancelCommand

                    case "command_cancel":
                        this.DebugWrite("attempting to cancel command", 6);
                        if (!this._ActionConfirmDic.Remove(record.source_name)) {
                            this.DebugWrite("no command to cancel", 6);
                            this.SendMessageToSource(record, "No command to cancel.");
                        }
                        //This type is not processed
                        break;

                        #endregion

                    default:
                        this.ConsoleError("Unable to complete record for " + record.command_type.command_key + ", handler not found.");
                        break;
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error occured while completing record information.", e));
            }
        }

        public String CompleteTargetInformation(AdKatsRecord record, Boolean requireConfirm) {
            try {
                lock (_PlayersMutex) {
                    //Check for an exact match
                    if (_PlayerDictionary.ContainsKey(record.target_name)) {
                        //Exact player match, call processing without confirmation
                        record.target_player = this._PlayerDictionary[record.target_name];
                        record.target_name = record.target_player.player_name;
                        if (!requireConfirm) {
                            //Process record right away
                            this.QueueRecordForProcessing(record);
                        }
                        else {
                            this.ConfirmActionWithSource(record);
                        }
                    }
                    else {
                        //Get all subString matches
                        Converter<String, List<AdKats_Player>> exactNameMatches = delegate(String sub) {
                                                                                      List<AdKats_Player> matches = new List<AdKats_Player>();
                                                                                      if (String.IsNullOrEmpty(sub))
                                                                                          return matches;
                                                                                      matches.AddRange(this._PlayerDictionary.Values.Where(player => Regex.Match(player.player_name, sub, RegexOptions.IgnoreCase).Success));
                                                                                      return matches;
                                                                                  };
                        List<AdKats_Player> subStringMatches = exactNameMatches(record.target_name);
                        if (subStringMatches.Count == 1) {
                            //Only one subString match, call processing without confirmation if able
                            record.target_player = subStringMatches[0];
                            record.target_name = record.target_player.player_name;
                            if (!requireConfirm) {
                                //Process record right away
                                this.QueueRecordForProcessing(record);
                            }
                            else {
                                this.ConfirmActionWithSource(record);
                            }
                        }
                        else if (subStringMatches.Count > 1) {
                            //Multiple players matched the query, choose correct one
                            String msg = "'" + record.target_name + "' matches multiple players: ";
                            bool first = true;
                            AdKats_Player suggestion = null;
                            foreach (AdKats_Player player in subStringMatches) {
                                if (first) {
                                    msg = msg + player.player_name;
                                    first = false;
                                }
                                else {
                                    msg = msg + ", " + player.player_name;
                                }
                                //Suggest player names that start with the text admins entered over others
                                if (player.player_name.ToLower().StartsWith(record.target_name.ToLower())) {
                                    suggestion = player;
                                }
                            }
                            if (suggestion == null) {
                                //If no player name starts with what admins typed, suggest subString name with lowest Levenshtein distance
                                Int32 bestDistance = Int32.MaxValue;
                                foreach (AdKats_Player player in subStringMatches) {
                                    Int32 distance = LevenshteinDistance(record.target_name, player.player_name);
                                    if (distance < bestDistance) {
                                        bestDistance = distance;
                                        suggestion = player;
                                    }
                                }
                            }
                            //If the suggestion is still null, something has failed
                            if (suggestion == null) {
                                this.DebugWrite("name suggestion system failed subString match", 5);
                                return "ERROR";
                            }

                            //Inform admin of multiple players found
                            this.SendMessageToSource(record, msg);

                            //Use suggestion for target
                            record.target_player = suggestion;
                            record.target_name = suggestion.player_name;
                            //Send record to attempt list for confirmation
                            return this.ConfirmActionWithSource(record);
                        }
                        else {
                            //There were no players found, run a fuzzy search using Levenshtein Distance on all players in server
                            AdKats_Player fuzzyMatch = null;
                            Int32 bestDistance = Int32.MaxValue;
                            foreach (AdKats_Player player in this._PlayerDictionary.Values) {
                                Int32 distance = LevenshteinDistance(record.target_name, player.player_name);
                                if (distance < bestDistance) {
                                    bestDistance = distance;
                                    fuzzyMatch = player;
                                }
                            }
                            //If the suggestion is still null, something has failed
                            if (fuzzyMatch == null) {
                                this.DebugWrite("name suggestion system failed fuzzy match", 5);
                                return "ERROR";
                            }

                            //Use suggestion for target
                            record.target_player = fuzzyMatch;
                            record.target_name = fuzzyMatch.player_name;
                            //Send record to attempt list for confirmation
                            return this.ConfirmActionWithSource(record);
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while completing target information.", e));
            }
            return "END OF FUNCTION";
        }

        public String ConfirmActionWithSource(AdKatsRecord record) {
            this.DebugWrite("Entering confirmActionWithSource", 7);
            try {
                lock (_ActionConfirmMutex) {
                    //Cancel any source pending action
                    this.CancelSourcePendingAction(record);
                    //Send record to attempt list
                    this._ActionConfirmDic.Add(record.source_name, record);
                    return this.SendMessageToSource(record, record.command_type.command_name + "->" + record.target_name + " for " + record.record_message + "?");
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while confirming action with record source.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting confirmActionWithSource", 7);
            return null;
        }

        public void CancelSourcePendingAction(AdKatsRecord record) {
            this.DebugWrite("Entering cancelSourcePendingAction", 7);
            try {
                this.DebugWrite("attempting to cancel command", 6);
                lock (_ActionConfirmMutex) {
                    if (!this._ActionConfirmDic.Remove(record.source_name)) {
                        //this.sendMessageToSource(record, "No command to cancel.");
                    }
                    else {
                        this.SendMessageToSource(record, "Command Canceled.");
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while canceling source pending action.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting cancelSourcePendingAction", 7);
        }

        public void AutoWhitelistPlayers() {
            try {
                lock (_PlayersMutex) {
                    if (this._PlayersToAutoWhitelist > 0) {
                        Random random = new Random();
                        List<String> playerListCopy = new List<String>();
                        foreach (AdKats_Player player in this._PlayerDictionary.Values) {
                            this.DebugWrite("Checking for TeamSwap access on " + player.player_name, 6);
                            if (!this.HasAccess(player, this._CommandKeyDictionary["self_teamswap"])) {
                                this.DebugWrite("player doesnt have access, adding them to chance list", 6);
                                if (!playerListCopy.Contains(player.player_name)) {
                                    playerListCopy.Add(player.player_name);
                                }
                            }
                        }
                        if (playerListCopy.Count > 0) {
                            Int32 maxIndex = (playerListCopy.Count < this._PlayersToAutoWhitelist) ? (playerListCopy.Count) : (this._PlayersToAutoWhitelist);
                            this.DebugWrite("MaxIndex: " + maxIndex, 6);
                            for (Int32 index = 0; index < maxIndex; index++) {
                                String playerName = null;
                                Int32 iterations = 0;
                                do {
                                    playerName = playerListCopy[random.Next(0, playerListCopy.Count - 1)];
                                } while (this._TeamswapRoundWhitelist.ContainsKey(playerName) && (iterations++ < 100));
                                if (!this._TeamswapRoundWhitelist.ContainsKey(playerName)) {
                                    AdKats_Player aPlayer = null;
                                    if (this._PlayerDictionary.TryGetValue(playerName, out aPlayer)) {
                                        //Create the Exception record
                                        AdKatsRecord record = new AdKatsRecord {
                                                                                   record_source = AdKatsRecord.Sources.Automated,
                                                                                   server_id = this._ServerID,
                                                                                   command_type = this._CommandKeyDictionary["player_roundwhitelist"],
                                                                                   command_numeric = 0,
                                                                                   target_name = aPlayer.player_name,
                                                                                   target_player = aPlayer,
                                                                                   source_name = "AdKats",
                                                                                   record_message = "Round-Whitelisting Player"
                                                                               };
                                        //Process the record
                                        this.QueueRecordForProcessing(record);
                                    }
                                    else {
                                        this.ConsoleError("Player was not in player dictionary when calling auto-whitelist.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while auto-whitelisting players.", e));
            }
        }

        public Boolean HandleRoundReport(AdKatsRecord record) {
            Boolean acted = false;
            try {
                lock (_ReportsMutex) {
                    //report ID will be housed in target_name
                    this.DebugWrite("Attempting to handle based on round report.", 6);
                    if (this._RoundReports.ContainsKey(record.target_name)) {
                        this.DebugWrite("Handling round report.", 5);
                        //Get the reported record
                        AdKatsRecord reportedRecord = this._RoundReports[record.target_name];
                        //Remove it from the reports for this round
                        this._RoundReports.Remove(record.target_name);
                        //Update it in the database
                        reportedRecord.command_action = this._CommandKeyDictionary["player_report_confirm"];
                        this.UpdateRecord(reportedRecord, false);
                        //Get target information
                        record.target_name = reportedRecord.target_name;
                        record.target_player = reportedRecord.target_player;
                        //Update record message if needed
                        //attempt to handle via pre-message ID
                        //record.record_message = this.getPreMessage(record.record_message, this.requirePreMessageUse);
                        this.DebugWrite("MESS: " + record.record_message, 5);
                        if (record.record_message == null || record.record_message.Length < this._RequiredReasonLength) {
                            record.record_message = reportedRecord.record_message;
                        }
                        //Inform the reporter that they helped the admins
                        this.SendMessageToSource(reportedRecord, "Your report has been acted on. Thank you.");
                        //Queue for processing right away
                        this.QueueRecordForProcessing(record);
                        acted = true;
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while handling round report.", e);
                this.HandleException(record.record_exception);
            }
            return acted;
        }

        //replaces the message with a pre-message
        public String GetPreMessage(String message, Boolean required) {
            this.DebugWrite("Entering getPreMessage", 7);
            try {
                if (!string.IsNullOrEmpty(message)) {
                    //Attempt to fill the message via pre-message ID
                    Int32 preMessageID = 0;
                    DebugWrite("Raw preMessageID: " + message, 6);
                    Boolean valid = Int32.TryParse(message, out preMessageID);
                    if (valid && (preMessageID > 0) && (preMessageID <= this._PreMessageList.Count)) {
                        message = this._PreMessageList[preMessageID - 1];
                    }
                    else if (required) {
                        return null;
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while getting pre-message.", e));
            }
            this.DebugWrite("Exiting getPreMessage", 7);
            return message;
        }

        #endregion

        #region Action Methods

        private void QueueRecordForActionHandling(AdKatsRecord record) {
            this.DebugWrite("Entering queueRecordForActionHandling", 6);
            try {
                this.DebugWrite("Preparing to queue record for action handling", 6);
                lock (_UnprocessedActionMutex) {
                    this._UnprocessedActionQueue.Enqueue(record);
                    this.DebugWrite("Record queued for action handling", 6);
                    this._ActionHandlingWaitHandle.Set();
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while queuing record for action handling.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting queueRecordForActionHandling", 6);
        }

        private void ActionHandlingThreadLoop() {
            try {
                this.DebugWrite("ACTION: Starting Action Thread", 2);
                Thread.CurrentThread.Name = "action";
                while (true) {
                    this.DebugWrite("ACTION: Entering Action Thread Loop", 7);
                    if (!this._IsEnabled) {
                        this.DebugWrite("ACTION: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }
                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Handle Inbound Actions
                    if (this._UnprocessedActionQueue.Count > 0) {
                        Queue<AdKatsRecord> unprocessedActions;
                        lock (_UnprocessedActionMutex) {
                            this.DebugWrite("ACTION: Inbound actions found. Grabbing.", 6);
                            //Grab all messages in the queue
                            unprocessedActions = new Queue<AdKatsRecord>(this._UnprocessedActionQueue.ToArray());
                            //Clear the queue for next run
                            this._UnprocessedActionQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (unprocessedActions.Count > 0) {
                            this.DebugWrite("ACTION: Preparing to Run Actions for record", 6);
                            //Dequeue the record
                            AdKatsRecord record = unprocessedActions.Dequeue();

                            //Run the appropriate action
                            this.RunAction(record);
                            //If more processing is needed, then perform it
                            //If any errors exist in the record, do not re-queue
                            if (record.record_exception == null) {
                                this.QueueRecordForProcessing(record);
                            }
                            else {
                                this.DebugWrite("ACTION: Record has errors, not re-queueing after action.", 3);
                            }
                        }
                    }
                    else {
                        this.DebugWrite("ACTION: No inbound actions. Waiting.", 6);
                        //Wait for new actions
                        this._ActionHandlingWaitHandle.Reset();
                        this._ActionHandlingWaitHandle.WaitOne(Timeout.Infinite);
                    }
                }
                this.DebugWrite("ACTION: Ending Action Handling Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Action handling thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Fatal occured in action handling thread. Unable to restart.", e));
                }
            }
        }

        private String RunAction(AdKatsRecord record) {
            this.DebugWrite("Entering runAction", 6);
            String response = "ERROR";
            try {
                //Make sure record has an action
                if (record.command_action == null) {
                    record.command_action = record.command_type;
                }
                //Perform Actions
                switch (record.command_action.command_key) {
                    case "player_move":
                        response = this.MoveTarget(record);
                        break;
                    case "player_fmove":
                        response = this.ForceMoveTarget(record);
                        break;
                    case "self_teamswap":
                        response = this.ForceMoveTarget(record);
                        break;
                    case "self_kill":
                        response = this.KillTarget(record, null);
                        break;
                    case "player_kill":
                        response = this.KillTarget(record, null);
                        break;
                    case "player_kill_lowpop":
                        response = this.KillTarget(record, null);
                        break;
                    case "player_kill_repeat":
                        response = this.KillTarget(record, null);
                        break;
                    case "player_kick":
                        response = this.KickTarget(record, null);
                        break;
                    case "player_ban_temp":
                        response = this.TempBanTarget(record, null);
                        break;
                    case "player_ban_perm":
                        response = this.PermaBanTarget(record, null);
                        break;
                    case "player_punish":
                        response = this.PunishTarget(record);
                        break;
                    case "player_forgive":
                        response = this.ForgiveTarget(record);
                        break;
                    case "player_mute":
                        response = this.MuteTarget(record);
                        break;
                    case "player_join":
                        response = this.JoinTarget(record);
                        break;
                    case "player_roundwhitelist":
                        response = this.RoundWhitelistTarget(record);
                        break;
                    case "player_report":
                        response = this.ReportTarget(record);
                        break;
                    case "player_calladmin":
                        response = this.CallAdminOnTarget(record);
                        break;
                    case "round_restart":
                        response = this.RestartLevel(record);
                        break;
                    case "round_next":
                        response = this.NextLevel(record);
                        break;
                    case "round_end":
                        response = this.EndLevel(record);
                        break;
                    case "server_nuke":
                        response = this.NukeTarget(record);
                        break;
                    case "server_kickall":
                        response = this.KickAllPlayers(record);
                        break;
                    case "admin_say":
                        response = this.AdminSay(record);
                        break;
                    case "player_say":
                        response = this.PlayerSay(record);
                        break;
                    case "admin_yell":
                        response = this.AdminYell(record);
                        break;
                    case "player_yell":
                        response = this.PlayerYell(record);
                        break;
                    case "admin_tell":
                        response = this.AdminTell(record);
                        break;
                    case "player_tell":
                        response = this.PlayerTell(record);
                        break;
                    case "self_rules":
                        response = this.SendServerRules(record);
                        break;
                    case "banenforcer_enforce":
                        //Don't do anything here, ban enforcer handles this
                        break;
                    case "adkats_exception":
                        record.record_action_executed = true;
                        break;
                    default:
                        record.record_action_executed = true;
                        response = "Command not recognized when running action.";
                        record.record_exception = this.HandleException(new AdKatsException("Command " + record.command_action + " not found in runAction"));
                        break;
                }
            }
            catch (Exception e) {
                record.record_exception = this.HandleException(new AdKatsException("Error while choosing action for record.", e));
            }
            this.DebugWrite("Exiting runAction", 6);
            return response;
        }

        public String MoveTarget(AdKatsRecord record) {
            this.DebugWrite("Entering moveTarget", 6);
            String message = null;
            try {
                this.QueuePlayerForMove(record.target_player.frostbitePlayerInfo);
                this.PlayerSayMessage(record.target_name, "On your next death you will be moved to the opposing team.");
                message = this.SendMessageToSource(record, record.target_name + " will be sent to TeamSwap on their next death.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for move record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting moveTarget", 6);
            return message;
        }

        public String ForceMoveTarget(AdKatsRecord record) {
            this.DebugWrite("Entering forceMoveTarget", 6);
            String message = null;
            try {
                if (record.command_type == this._CommandKeyDictionary["self_teamswap"]) {
                    if (this.HasAccess(record.source_player, this._CommandKeyDictionary["self_teamswap"]) || ((this._TeamSwapTicketWindowHigh >= this._HighestTicketCount) && (this._TeamSwapTicketWindowLow <= this._LowestTicketCount))) {
                        message = "Calling Teamswap on self";
                        this.DebugWrite(message, 6);
                        this.QueuePlayerForForceMove(record.target_player.frostbitePlayerInfo);
                    }
                    else {
                        message = "Player unable to TeamSwap";
                        this.DebugWrite(message, 6);
                        this.SendMessageToSource(record, "You cannot TeamSwap at this time. Game outside ticket window [" + this._TeamSwapTicketWindowLow + ", " + this._TeamSwapTicketWindowHigh + "].");
                    }
                }
                else {
                    message = "TeamSwap called on " + record.target_name;
                    this.DebugWrite("Calling Teamswap on target", 6);
                    this.SendMessageToSource(record, "" + record.target_name + " sent to TeamSwap.");
                    this.QueuePlayerForForceMove(record.target_player.frostbitePlayerInfo);
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for force-move/teamswap record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forceMoveTarget", 6);
            return message;
        }

        public String KillTarget(AdKatsRecord record, String additionalMessage) {
            this.DebugWrite("Entering killTarget", 6);
            String message = null;
            try {
                additionalMessage = !string.IsNullOrEmpty(additionalMessage) ? (" " + additionalMessage) : ("");
                this.PlayerSayMessage(record.target_name, "Killed by admin for " + record.record_message + " " + additionalMessage);
                Int32 seconds = (int) DateTime.UtcNow.Subtract(record.target_player.lastDeath).TotalSeconds;
                this.DebugWrite("Killing player. Player last died " + seconds + " seconds ago.", 3);
                if (seconds < 5 && record.command_action.command_key != "player_kill_repeat") {
                    this.DebugWrite("Queueing player for kill on spawn. (" + seconds + ")&(" + record.command_action + ")", 3);
                    if (!this._ActOnSpawnDictionary.ContainsKey(record.target_player.player_name)) {
                        lock (this._ActOnSpawnDictionary) {
                            record.command_action = this._CommandKeyDictionary["player_kill_repeat"];
                            this._ActOnSpawnDictionary.Add(record.target_player.player_name, record);
                        }
                    }
                }
                //Perform actions
                if (String.IsNullOrEmpty(record.target_player.player_name)) {
                    this.ConsoleError("playername null in 5437");
                }
                else {
                    ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_player.player_name);
                    this.SendMessageToSource(record, "You KILLED " + record.target_name + " for " + record.record_message + additionalMessage);
                }
                if (record.source_name == "AutoAdmin" && record.command_type.command_key == "player_punish") {
                    this.AdminSayMessage("Punishing " + record.target_name + " for " + record.record_message);
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for kill record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting killTarget", 6);
            return message;
        }

        public String KickTarget(AdKatsRecord record, String additionalMessage) {
            this.DebugWrite("Entering kickTarget", 6);
            String message = null;
            try {
                String kickReason = this.GenerateKickReason(record);
                //Perform Actions
                this.DebugWrite("Kick Message: '" + kickReason + "'", 3);
                if (String.IsNullOrEmpty(record.target_player.player_name) || String.IsNullOrEmpty(kickReason)) {
                    this.ConsoleError("Item null in 5464");
                }
                else {
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", record.target_player.player_name, kickReason);
                    this.RemovePlayerFromDictionary(record.target_player.player_name);
                    if (record.target_name != record.source_name) {
                        this.AdminSayMessage("Player " + record.target_name + " was KICKED by " + ((this._ShowAdminNameInSay) ? (record.source_name) : ("admin")) + " for " + record.record_message + " " + additionalMessage);
                    }
                    message = this.SendMessageToSource(record, "You KICKED " + record.target_name + " for " + record.record_message + ". " + additionalMessage);
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for kick record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting kickTarget", 6);
            return message;
        }

        public String TempBanTarget(AdKatsRecord record, String additionalMessage) {
            this.DebugWrite("Entering tempBanTarget", 6);
            String message = null;
            try {
                //Subtract 1 second for visual effect
                Int32 seconds = (record.command_numeric * 60) - 1;

                //Perform Actions
                //Only post to ban enforcer if there are no exceptions
                if (this._UseBanEnforcer && record.record_exception == null) {
                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                    //Create the ban
                    AdKatsBan aBan = new AdKatsBan {
                                                       ban_record = record,
                                                       ban_enforceName = nameAvailable && (this._DefaultEnforceName || (!guidAvailable && !ipAvailable)),
                                                       ban_enforceGUID = guidAvailable && (this._DefaultEnforceGUID || (!nameAvailable && !ipAvailable)),
                                                       ban_enforceIP = ipAvailable && (this._DefaultEnforceIP || (!nameAvailable && !guidAvailable))
                                                   };

                    //Queue the ban for upload
                    this.QueueBanForProcessing(aBan);
                }
                else {
                    if (record.record_exception != null) {
                        this.HandleException(new AdKatsException("Defaulting to procon banlist usage since exceptions existed in record"));
                    }
                    //Trim the ban message if necessary
                    String banMessage = record.record_message;
                    Int32 cutLength = banMessage.Length - 80;
                    if (cutLength > 0) {
                        banMessage = banMessage.Substring(0, banMessage.Length - cutLength);
                    }
                    this.DebugWrite("Ban Message: '" + banMessage + "'", 3);
                    if (!String.IsNullOrEmpty(record.target_player.player_guid)) {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_player.player_guid, "seconds", seconds + "", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_ip)) {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "ip", record.target_player.player_ip, "seconds", seconds + "", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_name)) {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "name", record.target_player.player_name, "seconds", seconds + "", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else {
                        this.ConsoleError("Player has no information to ban with.");
                        message = "ERROR";
                    }
                    this.RemovePlayerFromDictionary(record.target_player.player_name);
                }
                if (record.target_name != record.source_name) {
                    this.AdminSayMessage("Player " + record.target_name + " was BANNED by " + ((this._ShowAdminNameInSay) ? (record.source_name) : ("admin")) + " for " + record.record_message + " " + additionalMessage);
                }
                message = this.SendMessageToSource(record, "You TEMP BANNED " + record.target_name + " for " + this.FormatTimeString(TimeSpan.FromMinutes(record.command_numeric)) + "." + additionalMessage);
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for TempBan record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting tempBanTarget", 6);
            return message;
        }

        public String PermaBanTarget(AdKatsRecord record, String additionalMessage) {
            this.DebugWrite("Entering permaBanTarget", 6);
            String message = null;
            try {
                //Perform Actions
                //Only post to ban enforcer if there are no exceptions
                if (this._UseBanEnforcer && record.record_exception == null) {
                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                    //Create the ban
                    AdKatsBan aBan = new AdKatsBan {
                                                       ban_record = record,
                                                       ban_enforceName = nameAvailable && (this._DefaultEnforceName || (!guidAvailable && !ipAvailable)),
                                                       ban_enforceGUID = guidAvailable && (this._DefaultEnforceGUID || (!nameAvailable && !ipAvailable)),
                                                       ban_enforceIP = ipAvailable && (this._DefaultEnforceIP || (!nameAvailable && !guidAvailable))
                                                   };

                    //Queue the ban for upload
                    this.QueueBanForProcessing(aBan);
                }
                else {
                    if (record.record_exception != null) {
                        this.HandleException(new AdKatsException("Defaulting to procon banlist usage since exceptions existed in record"));
                    }
                    //Trim the ban message if necessary
                    String banMessage = record.record_message;
                    Int32 cutLength = banMessage.Length - 80;
                    if (cutLength > 0) {
                        banMessage = banMessage.Substring(0, banMessage.Length - cutLength);
                    }
                    this.DebugWrite("Ban Message: '" + banMessage + "'", 3);
                    if (!String.IsNullOrEmpty(record.target_player.player_guid)) {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_player.player_guid, "perm", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_ip)) {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "ip", record.target_player.player_ip, "perm", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_name)) {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "name", record.target_player.player_name, "perm", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else {
                        this.ConsoleError("Player has no information to ban with.");
                        message = "ERROR";
                    }
                    this.RemovePlayerFromDictionary(record.target_player.player_name);
                }
                if (record.target_name != record.source_name) {
                    this.AdminSayMessage("Player " + record.target_name + " was BANNED by " + ((this._ShowAdminNameInSay) ? (record.source_name) : ("admin")) + " for " + record.record_message + additionalMessage);
                }
                message = this.SendMessageToSource(record, "You PERMA BANNED " + record.target_name + "! Get a vet admin NOW!" + additionalMessage);
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for PermaBan record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting permaBanTarget", 6);
            return message;
        }

        public String EnforceBan(AdKatsBan aBan, Boolean verbose) {
            this.DebugWrite("Entering enforceBan", 6);
            String message = null;
            try {
                //Create the total kick message
                String generatedBanReason = this.GenerateBanReason(aBan);
                this.DebugWrite("Ban Enforce Message: '" + generatedBanReason + "'", 3);

                //Perform Actions
                if (this._PlayerDictionary.ContainsKey(aBan.ban_record.target_player.player_name)) {
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", aBan.ban_record.target_player.player_name, generatedBanReason);
                    this.RemovePlayerFromDictionary(aBan.ban_record.target_player.player_name);
                    //Inform the server of the enforced ban
                    if (verbose) {
                        this.AdminSayMessage("Enforcing ban on " + aBan.ban_record.target_player.player_name + " for " + aBan.ban_record.record_message);
                    }
                    message = "Ban Enforced";
                }
            }
            catch (Exception e) {
                aBan.ban_exception = new AdKatsException("Error while enforcing ban.", e);
                this.HandleException(aBan.ban_exception);
            }
            this.DebugWrite("Exiting enforceBan", 6);
            return message;
        }

        public String PunishTarget(AdKatsRecord record) {
            this.DebugWrite("Entering punishTarget", 6);
            String message = null;
            try {
                //If the record has any exceptions, skip everything else and just kill the player
                if (record.record_exception == null) {
                    //Get number of points the player from server
                    Int32 points = this.FetchPoints(record.target_player);
                    this.DebugWrite(record.target_player.player_name + " has " + points + " points.", 5);
                    //Get the proper action to take for player punishment
                    String action = "noaction";
                    String skippedAction = null;
                    if (points > (this._PunishmentHierarchy.Length - 1)) {
                        action = this._PunishmentHierarchy[this._PunishmentHierarchy.Length - 1];
                    }
                    else if (points > 0) {
                        action = this._PunishmentHierarchy[points - 1];
                        if (record.isIRO) {
                            skippedAction = this._PunishmentHierarchy[points - 2];
                        }
                    }
                    else {
                        action = this._PunishmentHierarchy[0];
                    }

                    //Handle the case where and IRO punish skips higher level punishment for a lower one, use the higher one
                    if (skippedAction != null && this._PunishmentSeverityIndex.IndexOf(skippedAction) > this._PunishmentSeverityIndex.IndexOf(action)) {
                        action = skippedAction;
                    }

                    //Set additional message
                    String pointMessage = " [" + ((record.isIRO) ? ("IRO ") : ("")) + points + "pts]";
                    if (!record.record_message.Contains(pointMessage)) {
                        record.record_message += pointMessage;
                    }
                    const string additionalMessage = "";

                    Boolean isLowPop = this._OnlyKillOnLowPop && (this._PlayerDictionary.Count < this._LowPopPlayerCount);
                    Boolean iroOverride = record.isIRO && this._IROOverridesLowPop;

                    this.DebugWrite("Server low population: " + isLowPop + " (" + this._PlayerDictionary.Count + " <? " + this._LowPopPlayerCount + ") | Override: " + iroOverride, 5);

                    //Call correct action
                    if ((action == "kill" || (isLowPop && !iroOverride)) && !action.Equals("ban")) {
                        record.command_action = (isLowPop) ? (this._CommandKeyDictionary["player_kill_lowpop"]) : (this._CommandKeyDictionary["player_kill"]);
                        message = this.KillTarget(record, additionalMessage);
                    }
                    else if (action == "kick") {
                        record.command_action = this._CommandKeyDictionary["player_kick"];
                        message = this.KickTarget(record, additionalMessage);
                    }
                    else if (action == "tban60") {
                        record.command_numeric = 60;
                        record.command_action = this._CommandKeyDictionary["player_ban_temp"];
                        message = this.TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tban120") {
                        record.command_numeric = 120;
                        record.command_action = this._CommandKeyDictionary["player_ban_temp"];
                        message = this.TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tbanday") {
                        record.command_numeric = 1440;
                        record.command_action = this._CommandKeyDictionary["player_ban_temp"];
                        message = this.TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tbanweek") {
                        record.command_numeric = 10080;
                        record.command_action = this._CommandKeyDictionary["player_ban_temp"];
                        message = this.TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tban2weeks") {
                        record.command_numeric = 20160;
                        record.command_action = this._CommandKeyDictionary["player_ban_temp"];
                        message = this.TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tbanmonth") {
                        record.command_numeric = 43200;
                        record.command_action = this._CommandKeyDictionary["player_ban_temp"];
                        message = this.TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "ban") {
                        record.command_action = this._CommandKeyDictionary["player_ban_perm"];
                        message = this.PermaBanTarget(record, additionalMessage);
                    }
                    else {
                        record.command_action = this._CommandKeyDictionary["player_kill"];
                        this.KillTarget(record, additionalMessage);
                        record.record_exception = new AdKatsException("Punish options are set incorrectly. '" + action + "' not found. Inform plugin setting manager.");
                        this.HandleException(record.record_exception);
                    }
                }
                else {
                    //Exception found, just kill the player
                    record.command_action = this._CommandKeyDictionary["player_kill"];
                    this.KillTarget(record, null);
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Punish record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting punishTarget", 6);
            return message;
        }

        public String ForgiveTarget(AdKatsRecord record) {
            this.DebugWrite("Entering forgiveTarget", 6);
            String message = null;
            try {
                //If the record has any exceptions, skip everything
                if (record.record_exception == null) {
                    Int32 points = this.FetchPoints(record.target_player);
                    this.PlayerSayMessage(record.target_name, "Forgiven 1 infraction point. You now have " + points + " point(s) against you.");
                    message = this.SendMessageToSource(record, "Forgive Logged for " + record.target_name + ". They now have " + points + " infraction points.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Forgive record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forgiveTarget", 6);
            return message;
        }

        public String MuteTarget(AdKatsRecord record) {
            this.DebugWrite("Entering muteTarget", 6);
            String message = null;
            try {
                if (!this.HasAccess(record.target_player, this._CommandKeyDictionary["player_mute"])) {
                    if (!this._RoundMutedPlayers.ContainsKey(record.target_name)) {
                        this._RoundMutedPlayers.Add(record.target_name, 0);
                        this.PlayerSayMessage(record.target_name, this._MutedPlayerMuteMessage);
                        message = this.SendMessageToSource(record, record.target_name + " has been muted for this round.");
                    }
                    else {
                        message = this.SendMessageToSource(record, record.target_name + " already muted for this round.");
                    }
                }
                else {
                    message = this.SendMessageToSource(record, "You can't mute an admin, dimwit.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Mute record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting muteTarget", 6);
            return message;
        }

        public String JoinTarget(AdKatsRecord record) {
            this.DebugWrite("Entering joinTarget", 6);
            String message = null;
            try {
                //Get source player
                AdKats_Player sourcePlayer = null;
                if (this._PlayerDictionary.TryGetValue(record.source_name, out sourcePlayer)) {
                    //If the source has access to move players, then the squad will be unlocked for their entry
                    if (this.HasAccess(record.source_player, this._CommandKeyDictionary["player_move"])) {
                        //Unlock target squad
                        this.SendMessageToSource(record, "Unlocking target squad if needed, please wait.");
                        this.ExecuteCommand("procon.protected.send", "squad.private", record.target_player.frostbitePlayerInfo.TeamID + "", record.target_player.frostbitePlayerInfo.SquadID + "", "false");
                        //If anything longer is needed...tisk tisk
                        Thread.Sleep(500);
                    }
                    //Check for player access to change teams
                    if (record.target_player.frostbitePlayerInfo.TeamID != sourcePlayer.frostbitePlayerInfo.TeamID && !this.HasAccess(record.source_player, this._CommandKeyDictionary["self_teamswap"])) {
                        message = this.SendMessageToSource(record, "Target player is not on your team, you need @" + this._CommandKeyDictionary["self_teamswap"].command_text + "/TeamSwap access to join them.");
                    }
                    else {
                        //Move to specific squad
                        ExecuteCommand("procon.protected.send", "admin.movePlayer", record.source_name, record.target_player.frostbitePlayerInfo.TeamID + "", record.target_player.frostbitePlayerInfo.SquadID + "", "true");
                        message = this.SendMessageToSource(record, "Attempting to join " + record.target_name);
                    }
                }
                else {
                    message = this.SendMessageToSource(record, "Unable to find you in the player list, please try again.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Join record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting joinTarget", 6);
            return message;
        }

        public String RoundWhitelistTarget(AdKatsRecord record) {
            this.DebugWrite("Entering roundWhitelistTarget", 6);
            String message = null;
            try {
                if (!this._TeamswapRoundWhitelist.ContainsKey(record.target_name)) {
                    if (this._TeamswapRoundWhitelist.Count < this._PlayersToAutoWhitelist + 2) {
                        this._TeamswapRoundWhitelist.Add(record.target_name, false);
                        String command = this._CommandKeyDictionary["self_teamswap"].command_text;
                        message = record.target_name + " can now use @" + command + " for this round.";
                    }
                    else {
                        message = "Cannot whitelist more than two extra people per round.";
                    }
                }
                else {
                    message = record.target_name + " is already in this round's TeamSwap whitelist.";
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for RoundWhitelist record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting roundWhitelistTarget", 6);
            return message;
        }

        public String ReportTarget(AdKatsRecord record) {
            this.DebugWrite("Entering reportTarget", 6);
            String message = null;
            try {
                Random random = new Random();
                Int32 reportID;
                do {
                    reportID = random.Next(100, 999);
                } while (_RoundReports.ContainsKey(reportID + ""));
                record.command_numeric = reportID;
                this._RoundReports.Add(reportID + "", record);
                String adminAssistantIdentifier = record.source_player.player_aa ? ("[AA]") : ("");
                //Send to all online players with access to player interaction commands
                lock (this._PlayersMutex) {
                    foreach (AdKats_Player player in this._PlayerDictionary.Values.Where(player => this.RoleIsInteractionAble(player.player_role))) {
                        //If any allowed command is a player interaction command, send the report
                        this.PlayerSayMessage(player.player_name, "REPORT " + adminAssistantIdentifier + "[" + reportID + "]: " + record.source_name + " reported " + record.target_name + " for " + record.record_message);
                    }
                }
                if (this._UseEmail) {
                    this._EmailHandler.SendReport(record);
                }
                message = this.SendMessageToSource(record, "REPORT [" + reportID + "] sent. " + record.target_name + " for " + record.record_message);
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Report record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting reportTarget", 6);
            return message;
        }

        public String CallAdminOnTarget(AdKatsRecord record) {
            this.DebugWrite("Entering callAdminOnTarget", 6);
            String message = null;
            try {
                Random random = new Random();
                Int32 reportID;
                do {
                    reportID = random.Next(100, 999);
                } while (_RoundReports.ContainsKey(reportID + ""));
                this._RoundReports.Add(reportID + "", record);
                String adminAssistantIdentifier = record.source_player.player_aa ? ("[AA]") : ("");
                //Send to all online players with access to player interaction commands
                lock (this._PlayersMutex) {
                    foreach (AdKats_Player player in this._PlayerDictionary.Values.Where(player => this.RoleIsInteractionAble(player.player_role))) {
                        //If any allowed command is a player interaction command, send the report
                        this.PlayerSayMessage(player.player_name, "ADMIN CALL " + adminAssistantIdentifier + "[" + reportID + "]: " + record.source_name + " called admin on " + record.target_name + " for " + record.record_message);
                    }
                }
                message = this.SendMessageToSource(record, "ADMIN CALL [" + reportID + "] sent. " + record.target_name + " for " + record.record_message);
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for CallAdmin record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting callAdminOnTarget", 6);
            return message;
        }

        public String RestartLevel(AdKatsRecord record) {
            this.DebugWrite("Entering restartLevel", 6);
            String message = null;
            try {
                this.ExecuteCommand("procon.protected.send", "mapList.restartRound");
                message = this.SendMessageToSource(record, "Round Restarted.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for RestartLevel record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting restartLevel", 6);
            return message;
        }

        public String NextLevel(AdKatsRecord record) {
            this.DebugWrite("Entering nextLevel", 6);
            String message = null;
            try {
                this.ExecuteCommand("procon.protected.send", "mapList.runNextRound");
                message = this.SendMessageToSource(record, "Next round has been run.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for NextLevel record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting nextLevel", 6);
            return message;
        }

        public String EndLevel(AdKatsRecord record) {
            this.DebugWrite("Entering forgiveTarget", 6);
            String message = null;
            try {
                this.ExecuteCommand("procon.protected.send", "mapList.endRound", record.command_numeric + "");
                message = this.SendMessageToSource(record, "Ended round with " + record.target_name + " as winner.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for EndLevel record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forgiveTarget", 6);
            return message;
        }

        public String NukeTarget(AdKatsRecord record) {
            this.DebugWrite("Entering nukeTarget", 6);
            String message = null;
            try {
                foreach (AdKats_Player player in this._PlayerDictionary.Values) {
                    if ((record.target_name == "US Team" && player.frostbitePlayerInfo.TeamID == AdKats.UsTeamID) || (record.target_name == "RU Team" && player.frostbitePlayerInfo.TeamID == AdKats.RuTeamID) || (record.target_name == "Server")) {
                        Thread.Sleep(50);
                        ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                        this.PlayerSayMessage(record.target_name, "Killed by admin for: " + record.record_message);
                    }
                }
                message = this.SendMessageToSource(record, "You NUKED " + record.target_name + " for " + record.record_message + ".");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for NukeServer record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting nukeTarget", 6);
            return message;
        }

        public String KickAllPlayers(AdKatsRecord record) {
            this.DebugWrite("Entering kickAllPlayers", 6);
            String message = null;
            try {
                foreach (AdKats_Player player in this._PlayerDictionary.Values) {
                    if (player.player_role.role_key == "guest_default") {
                        Thread.Sleep(50);
                        ExecuteCommand("procon.protected.send", "admin.kickPlayer", player.player_name, "(" + record.source_name + ") " + record.record_message);
                    }
                }
                this.AdminSayMessage("All guest players have been kicked.");
                message = "You KICKED EVERYONE for " + record.record_message + ". ";
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for KickAll record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting kickAllPlayers", 6);
            return message;
        }

        public String AdminSay(AdKatsRecord record) {
            this.DebugWrite("Entering adminSay", 6);
            String message = null;
            try {
                this.AdminSayMessage(record.record_message);
                message = "Server has been told '" + record.record_message + " by SAY'";
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for AdminSay record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting adminSay", 6);
            return message;
        }

        public String PlayerSay(AdKatsRecord record) {
            this.DebugWrite("Entering playerSay", 6);
            String message = null;
            try {
                this.PlayerSayMessage(record.target_name, record.record_message);
                message = record.target_name + " has been told '" + record.record_message + "' by SAY.";
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for playerSay record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting playerSay", 6);
            return message;
        }

        public String AdminYell(AdKatsRecord record) {
            this.DebugWrite("Entering adminYell", 6);
            String message = null;
            try {
                this.AdminYellMessage(record.record_message);
                message = "Server has been told '" + record.record_message + " by YELL'";
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for AdminYell record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting adminYell", 6);
            return message;
        }

        public String PlayerYell(AdKatsRecord record) {
            this.DebugWrite("Entering playerYell", 6);
            String message = null;
            try {
                this.PlayerYellMessage(record.target_name, record.record_message);
                message = record.target_name + " has been told '" + record.record_message + "' by YELL.";
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for playerYell record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting playerYell", 6);
            return message;
        }

        public String AdminTell(AdKatsRecord record) {
            this.DebugWrite("Entering adminTell", 6);
            String message = null;
            try {
                this.AdminTellMessage(record.record_message);
                message = "Server has been told '" + record.record_message + " by TELL'";
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for AdminYell record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting adminTell", 6);
            return message;
        }

        public String PlayerTell(AdKatsRecord record) {
            this.DebugWrite("Entering playerTell", 6);
            String message = null;
            try {
                this.PlayerTellMessage(record.target_name, record.record_message);
                message = record.target_name + " has been told '" + record.record_message + "' by TELL.";
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for playerTell record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting playerTell", 6);
            return message;
        }

        public String SendServerRules(AdKatsRecord record) {
            this.DebugWrite("Entering sendServerRules", 6);
            String message = null;
            try {
                //Send the server rules
                message = this.SendServerRules(record.target_name);
                //Set the executed bool
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while sending server rules (record).", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting sendServerRules", 6);
            return message;
        }

        public String SendServerRules(String targetName) {
            this.DebugWrite("Entering sendServerRules", 6);
            String message = "Rules sent to " + targetName;
            try {
                //Create a new thread to handle the disconnect orchestration
                Thread rulePrinter = new Thread(new ThreadStart(delegate {
                    this.DebugWrite("Starting a rule printer thread.", 5);
                    try {
                        //Special Case for no rules
                        if (this._ServerRulesList.Length == 0) {
                            this.PlayerSayMessage(targetName, "This server has no rules.");
                        }
                        else {
                            //Wait the rule delay duration
                            Thread.Sleep(TimeSpan.FromSeconds(this._ServerRulesDelay));
                            foreach (String rule in this._ServerRulesList) {
                                this.PlayerSayMessage(targetName, this.GetPreMessage(rule, false));
                                Thread.Sleep(TimeSpan.FromSeconds(this._ServerRulesInterval));
                            }
                        }
                    }
                    catch (Exception) {
                        this.HandleException(new AdKatsException("Error while printing server rules"));
                    }
                    this.DebugWrite("Exiting a rule printer.", 5);
                }));

                //Start the thread
                rulePrinter.Start();
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while sending server rules (String).", e));
            }
            this.DebugWrite("Exiting sendServerRules", 6);
            return message;
        }

        #endregion

        #region User Access

        private void QueueUserForUpload(AdKatsUser user) {
            try {
                this.DebugWrite("Preparing to queue user for access upload.", 6);
                lock (_UserMutex) {
                    this._UserUploadQueue.Enqueue(user);
                    this.DebugWrite("User queued for access upload", 6);
                    this._DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queuing user upload.", e));
            }
        }

        private void QueueUserForRemoval(AdKatsUser user) {
            try {
                this.DebugWrite("Preparing to queue user for access removal", 6);
                lock (_UserMutex) {
                    this._UserRemovalQueue.Enqueue(user);
                    this.DebugWrite("User queued for access removal", 6);
                    this._DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queuing access removal.", e));
            }
        }

        private Boolean HasAccess(AdKats_Player aPlayer, AdKatsCommand command) {
            try {
                if (aPlayer == null) {
                    this.ConsoleError("player was null in hasAccess.");
                    return false;
                }
                if (aPlayer.player_role == null) {
                    this.ConsoleError("player role was null in hasAccess.");
                    return false;
                }
                if (command == null) {
                    this.ConsoleError("Command was null in hasAccess.");
                    return false;
                }
                lock (aPlayer.player_role) {
                    lock (aPlayer.player_role.allowedCommands) {
                        if (aPlayer.player_role.allowedCommands.Values.Any(innerCommand => command.command_id == innerCommand.command_id)) {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while checking command access on player.", e));
                return false;
            }
        }

        #endregion

        #region Database Methods

        private void DatabaseCommThreadLoop() {
            try {
                this.DebugWrite("DBCOMM: Starting Database Comm Thread", 2);
                Thread.CurrentThread.Name = "databasecomm";
                Boolean firstRun = true;
                while (true) {
                    this.DebugWrite("DBCOMM: Entering Database Comm Thread Loop", 7);
                    if (!this._IsEnabled) {
                        this.DebugWrite("DBCOMM: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Check if database connection settings have changed
                    if (this._DbSettingsChanged) {
                        this.DebugWrite("DBCOMM: DB Settings have changed, calling test.", 6);
                        if (this.TestDatabaseConnection()) {
                            this.DebugWrite("DBCOMM: Database Connection Good. Continuing Thread.", 6);
                        }
                        else {
                            this._DbSettingsChanged = true;
                            continue;
                        }
                    }

                    //On first run, pull all roles and commands
                    if (firstRun) {
                        this.FetchCommands();
                        this.FetchRoles();
                    }

                    //Every 60 seconds feed stat logger settings
                    if (this._LastStatLoggerStatusUpdateTime.AddSeconds(60) < DateTime.UtcNow) {
                        this._LastStatLoggerStatusUpdateTime = DateTime.UtcNow;
                        if (this._StatLoggerVersion == "BF3") {
                            //this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Chatlogging?", "No");
                            //this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Log ServerSPAM?", "No");
                            //this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Instant Logging of Chat Messages?", "No");
                            //this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable chatlog filter(Regex)?", "No");
                            if (this._FeedStatLoggerSettings) {
                                //Due to narwhals, Stat logger time offset is in the opposite direction of Adkats time offset
                                Double slOffset = DateTime.UtcNow.Subtract(DateTime.Now).TotalHours;
                                this.SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Servertime Offset", slOffset + "");
                            }
                        }
                        else if (this._StatLoggerVersion == "UNIVERSAL") {
                            //this.setExternalPluginSetting("CChatGUIDStatsLogger", "Enable Chatlogging?", "No");
                            //this.setExternalPluginSetting("CChatGUIDStatsLogger", "Log ServerSPAM?", "No");
                            //this.setExternalPluginSetting("CChatGUIDStatsLogger", "Instant Logging of Chat Messages?", "No");
                            //this.setExternalPluginSetting("CChatGUIDStatsLogger", "Enable chatlog filter(Regex)?", "No");
                            if (this._FeedStatLoggerSettings) {
                                //Due to narwhals, Stat logger time offset is in the opposite direction of Adkats time offset
                                Double slOffset = DateTime.UtcNow.Subtract(DateTime.Now).TotalHours;
                                this.SetExternalPluginSetting("CChatGUIDStatsLogger", "Servertime Offset", slOffset + "");
                            }
                        }
                        else {
                            this.ConsoleError("Stat logger version is unknown, unable to feed stat logger settings.");
                        }
                        //TODO put back in the future
                        //this.confirmStatLoggerSetup();
                    }

                    //Update server ID
                    if (this._ServerID < 0) {
                        //Checking for database server info
                        if (this.FetchServerID() >= 0) {
                            this.ConsoleSuccess("Database Server Info Fetched. Server ID is " + this._ServerID + "!");

                            //Push all settings for this instance to the database
                            this.UploadAllSettings();
                        }
                        else {
                            //Inform the user
                            this.ConsoleError("Database Server info could not be fetched! Make sure XpKiller's Stat Logger is running on this server!");
                            //Disable the plugin
                            this.Disable();
                            break;
                        }
                    }
                    else {
                        this.DebugWrite("Skipping server ID fetch. Server ID: " + this._ServerID, 7);
                    }

                    //Check if settings need sync
                    if (this._SettingImportID != this._ServerID || this._LastDbSettingFetch.AddSeconds(DbSettingFetchFrequency) < DateTime.UtcNow) {
                        this.DebugWrite("Preparing to fetch settings from server " + _ServerID, 6);
                        //Fetch new settings from the database
                        this.FetchSettings(this._SettingImportID, this._SettingImportID != this._ServerID);
                    }

                    //Handle Inbound Setting Uploads
                    if (this._SettingUploadQueue.Count > 0) {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound setting queue to get new settings", 7);
                        Queue<CPluginVariable> inboundSettingUpload;
                        lock (this._SettingUploadQueue) {
                            this.DebugWrite("DBCOMM: Inbound settings found. Grabbing.", 6);
                            //Grab all settings in the queue
                            inboundSettingUpload = new Queue<CPluginVariable>(this._SettingUploadQueue.ToArray());
                            //Clear the queue for next run
                            this._SettingUploadQueue.Clear();
                        }
                        //Loop through all settings in order that they came in
                        while (inboundSettingUpload.Count > 0) {
                            CPluginVariable setting = inboundSettingUpload.Dequeue();

                            this.UploadSetting(setting);
                        }
                    }

                    //Handle Inbound Command Uploads
                    if (this._CommandUploadQueue.Count > 0) {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound command queue to get new commands", 7);
                        Queue<AdKatsCommand> inboundCommandUpload;
                        lock (this._CommandUploadQueue) {
                            this.DebugWrite("DBCOMM: Inbound commands found. Grabbing.", 6);
                            //Grab all commands in the queue
                            inboundCommandUpload = new Queue<AdKatsCommand>(this._CommandUploadQueue.ToArray());
                            //Clear the queue for next run
                            this._CommandUploadQueue.Clear();
                        }
                        //Loop through all commands in order that they came in
                        while (inboundCommandUpload.Count > 0) {
                            AdKatsCommand command = inboundCommandUpload.Dequeue();

                            this.UploadCommand(command);

                            this.FetchCommands();
                            this.FetchRoles();
                            this.FetchUserList();
                            this.UpdateSettingPage();
                        }
                    }

                    //Handle Inbound Role Uploads
                    if (this._RoleUploadQueue.Count > 0) {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound role queue to get new roles", 7);
                        Queue<AdKats_Role> inboundRoleUpload;
                        lock (this._RoleUploadQueue) {
                            this.DebugWrite("DBCOMM: Inbound roles found. Grabbing.", 6);
                            //Grab all roles in the queue
                            inboundRoleUpload = new Queue<AdKats_Role>(this._RoleUploadQueue.ToArray());
                            //Clear the queue for next run
                            this._RoleUploadQueue.Clear();
                        }
                        //Loop through all roles in order that they came in
                        while (inboundRoleUpload.Count > 0) {
                            AdKats_Role aRole = inboundRoleUpload.Dequeue();

                            this.UploadRole(aRole);

                            this.FetchCommands();
                            this.FetchRoles();
                            this.FetchUserList();
                            this.UpdateSettingPage();
                        }
                    }

                    //Handle Inbound Role Removal
                    if (this._RoleRemovalQueue.Count > 0) {
                        this.DebugWrite("DBCOMM: Preparing to lock removal role queue to get new roles", 7);
                        Queue<AdKats_Role> inboundRoleRemoval;
                        lock (this._RoleRemovalQueue) {
                            this.DebugWrite("DBCOMM: Inbound roles found. Grabbing.", 6);
                            //Grab all roles in the queue
                            inboundRoleRemoval = new Queue<AdKats_Role>(this._RoleRemovalQueue.ToArray());
                            //Clear the queue for next run
                            this._RoleRemovalQueue.Clear();
                        }
                        //Loop through all commands in order that they came in
                        while (inboundRoleRemoval.Count > 0) {
                            AdKats_Role aRole = inboundRoleRemoval.Dequeue();

                            this.RemoveRole(aRole);

                            this.FetchCommands();
                            this.FetchRoles();
                            this.FetchUserList();
                            this.UpdateSettingPage();
                        }
                    }

                    //Check for new actions from the database at given interval
                    if (this._FetchActionsFromDb && (DateTime.UtcNow > this._LastDbActionFetch.AddSeconds(DbActionFetchFrequency))) {
                        this.RunActionsFromDB();
                    }
                    else {
                        this.DebugWrite("DBCOMM: Skipping DB action fetch", 7);
                    }

                    //Call banlist at set interval (20 seconds)
                    if (this._UseBanEnforcerPreviousState && (DateTime.UtcNow > this._LastBanListCall.AddSeconds(20))) {
                        this._LastBanListCall = DateTime.UtcNow;
                        this.DebugWrite("banlist.list called at interval.", 6);
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }

                    //Handle access updates
                    if (this._UserUploadQueue.Count > 0 || this._UserRemovalQueue.Count > 0) {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound access queues to retrive access changes", 7);
                        Queue<AdKatsUser> inboundUserRemoval;
                        Queue<AdKatsUser> inboundUserUploads;
                        lock (_UserMutex) {
                            this.DebugWrite("DBCOMM: Inbound access changes found. Grabbing.", 6);
                            //Grab all in the queue
                            inboundUserUploads = new Queue<AdKatsUser>(this._UserUploadQueue.ToArray());
                            inboundUserRemoval = new Queue<AdKatsUser>(this._UserRemovalQueue.ToArray());
                            //Clear the queue for next run
                            this._UserUploadQueue.Clear();
                            this._UserRemovalQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (inboundUserUploads.Count > 0) {
                            AdKatsUser user = inboundUserUploads.Dequeue();
                            this.UploadUser(user);
                        }
                        //Loop through all records in order that they came in
                        while (inboundUserRemoval.Count > 0) {
                            AdKatsUser user = inboundUserRemoval.Dequeue();
                            this.RemoveUser(user);
                        }
                        this.FetchUserList();
                        //Update the setting page with new information
                        this.UpdateSettingPage();
                    }
                    else if (DateTime.UtcNow > this._LastUserFetch.AddSeconds(DbUserFetchFrequency)) {
                        //Handle user updates directly from the database
                        this.FetchUserList();
                        //If there are now users in the user list, update the UI
                        if (this._UserCache.Count > 0) {
                            //Update the setting page with new information
                            this.UpdateSettingPage();
                        }
                    }
                    else if (this._UserCache.Count == 0) {
                        //Handle access updates directly from the database
                        this.FetchUserList();
                        //If there are now people in the access list, update the UI
                        if (this._UserCache.Count > 0) {
                            //Update the setting page with new information
                            this.UpdateSettingPage();
                        }
                    }
                    else {
                        this.DebugWrite("DBCOMM: No inbound user changes.", 7);
                    }

                    //Start the other threads
                    if (firstRun) {
                        //Start other threads
                        this._PlayerListingThread.Start();
                        this._KillProcessingThread.Start();
                        this._MessageProcessingThread.Start();
                        this._CommandParsingThread.Start();
                        this._ActionHandlingThread.Start();
                        this._TeamSwapThread.Start();
                        this._BanEnforcerThread.Start();
                        this._HackerCheckerThread.Start();
                        firstRun = false;

                        this._ThreadsReady = true;
                        this.UpdateSettingPage();

                        //Register a command to indicate availibility to other plugins
                        this.RegisterCommand(_AdKatsAvailableIndicator);

                        this.ConsoleWrite("^b^2Running!^n^0 Version: " + this.GetPluginVersion());
                    }

                    //Ban Enforcer
                    if (this._UseBanEnforcer) {
                        if (!this._UseBanEnforcerPreviousState || (DateTime.UtcNow > this._LastDbBanFetch.AddSeconds(DbBanFetchFrequency))) {
                            //Load all bans on startup
                            if (!this._UseBanEnforcerPreviousState) {
                                //Get all bans from procon
                                this.ConsoleWarn("Preparing to queue procon bans for import. Please wait.");
                                this._DbCommunicationWaitHandle.Reset();
                                this.ExecuteCommand("procon.protected.send", "banList.list");
                                this._DbCommunicationWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
                                if (this._CBanProcessingQueue.Count > 0) {
                                    this.ConsoleWrite(this._CBanProcessingQueue.Count + " procon bans queued for import. Import might take several minutes if you have many bans!");
                                }
                                else {
                                    this.ConsoleWrite("No procon bans to import into Ban Enforcer.");
                                }
                            }
                        }
                        else {
                            this.DebugWrite("DBCOMM: Skipping DB ban fetch", 7);
                        }

                        //Handle Inbound Ban Comms
                        if (this._BanEnforcerProcessingQueue.Count > 0) {
                            this.DebugWrite("DBCOMM: Preparing to lock inbound ban enforcer queue to retrive new bans", 7);
                            Queue<AdKatsBan> inboundBans;
                            lock (this._BanEnforcerMutex) {
                                this.DebugWrite("DBCOMM: Inbound bans found. Grabbing.", 6);
                                //Grab all messages in the queue
                                inboundBans = new Queue<AdKatsBan>(this._BanEnforcerProcessingQueue.ToArray());
                                //Clear the queue for next run
                                this._BanEnforcerProcessingQueue.Clear();
                            }
                            Int32 index = 1;
                            //Loop through all bans in order that they came in
                            while (inboundBans.Count > 0) {
                                if (!this._IsEnabled || !this._UseBanEnforcer) {
                                    this.ConsoleWarn("Canceling ban import mid-operation.");
                                    break;
                                }
                                //Grab the ban
                                AdKatsBan aBan = inboundBans.Dequeue();

                                this.DebugWrite("DBCOMM: Processing Frostbite Ban: " + index++, 6);

                                //Upload the ban
                                this.UploadBan(aBan);

                                //Only perform special action when ban is direct
                                //Indirect bans are through the procon banlist, so the player has already been kicked
                                if (aBan.ban_record.source_name != "BanEnforcer") {
                                    //Enforce the ban
                                    this.EnforceBan(aBan, false);
                                }
                                else {
                                    this.RemovePlayerFromDictionary(aBan.ban_record.target_player.player_name);
                                }
                            }
                        }

                        //Handle BF3 Ban Manager imports
                        if (!this._UseBanEnforcerPreviousState) {
                            //Import all bans from BF3 Ban Manager
                            this.ImportBansFromBBM5108();
                        }

                        //Handle Inbound CBan Uploads
                        if (this._CBanProcessingQueue.Count > 0) {
                            if (!this._UseBanEnforcerPreviousState) {
                                this.ConsoleWarn("Do not disable AdKats or change any settings until upload is complete!");
                            }
                            this.DebugWrite("DBCOMM: Preparing to lock inbound cBan queue to retrive new cBans", 7);
                            Double totalCBans = 0;
                            Double bansImported = 0;
                            Boolean earlyExit = false;
                            DateTime startTime = DateTime.UtcNow;
                            Queue<CBanInfo> inboundCBans;
                            lock (this._CBanProcessingQueue) {
                                this.DebugWrite("DBCOMM: Inbound cBans found. Grabbing.", 6);
                                //Grab all cBans in the queue
                                inboundCBans = new Queue<CBanInfo>(this._CBanProcessingQueue.ToArray());
                                totalCBans = inboundCBans.Count;
                                //Clear the queue for next run
                                this._CBanProcessingQueue.Clear();
                            }
                            //Loop through all cBans in order that they came in
                            Boolean bansFound = false;
                            while (inboundCBans.Count > 0) {
                                //Break from the loop if the plugin is disabled or the setting is reverted.
                                if (!this._IsEnabled || !this._UseBanEnforcer) {
                                    this.ConsoleWarn("You exited the ban upload process early, the process was not completed.");
                                    earlyExit = true;
                                    break;
                                }

                                bansFound = true;

                                CBanInfo cBan = inboundCBans.Dequeue();

                                //Create the record
                                AdKatsRecord record = new AdKatsRecord();
                                record.record_source = AdKatsRecord.Sources.Automated;
                                //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                if (cBan.BanLength.Seconds > 0 && cBan.BanLength.Seconds < 31536000) {
                                    record.command_type = this._CommandKeyDictionary["player_ban_temp"];
                                    record.command_action = this._CommandKeyDictionary["player_ban_temp"];
                                    record.command_numeric = cBan.BanLength.Seconds / 60;
                                }
                                else {
                                    record.command_type = this._CommandKeyDictionary["player_ban_perm"];
                                    record.command_action = this._CommandKeyDictionary["player_ban_perm"];
                                    record.command_numeric = 0;
                                }
                                record.source_name = this._CBanAdminName;
                                record.server_id = this._ServerID;
                                record.target_player = this.FetchPlayer(false, -1, cBan.SoldierName, cBan.Guid, cBan.IpAddress);
                                if (!String.IsNullOrEmpty(record.target_player.player_name)) {
                                    record.target_name = record.target_player.player_name;
                                }
                                record.isIRO = false;
                                record.record_message = cBan.Reason;

                                //Update the ban enforcement depending on available information
                                Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                                Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                                Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                                //Create the ban
                                AdKatsBan aBan = new AdKatsBan {
                                                                   ban_record = record,
                                                                   ban_enforceName = nameAvailable && (this._DefaultEnforceName || (!guidAvailable && !ipAvailable) || !String.IsNullOrEmpty(cBan.SoldierName)),
                                                                   ban_enforceGUID = guidAvailable && (this._DefaultEnforceGUID || (!nameAvailable && !ipAvailable) || !String.IsNullOrEmpty(cBan.Guid)),
                                                                   ban_enforceIP = ipAvailable && (this._DefaultEnforceIP || (!nameAvailable && !guidAvailable) || !String.IsNullOrEmpty(cBan.IpAddress))
                                                               };
                                if (!aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP) {
                                    this.ConsoleError("Unable to create ban, no proper player information");
                                    continue;
                                }

                                //Upload the ban
                                this.DebugWrite("Uploading ban from procon.", 5);
                                this.UploadBan(aBan);

                                if (!this._UseBanEnforcerPreviousState && (++bansImported % 25 == 0)) {
                                    this.ConsoleWrite(Math.Round(100 * bansImported / totalCBans, 2) + "% of bans uploaded. AVG " + Math.Round(bansImported / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " uploads/sec.");
                                }
                            }
                            if (bansFound && !earlyExit) {
                                //If all bans have been queued for processing, clear the ban list
                                this.ExecuteCommand("procon.protected.send", "banList.clear");
                                this.ExecuteCommand("procon.protected.send", "banList.save");
                                this.ExecuteCommand("procon.protected.send", "banList.list");
                                if (!this._UseBanEnforcerPreviousState) {
                                    this.ConsoleSuccess("All bans uploaded into AdKats database.");
                                }
                            }
                        }

                        this._UseBanEnforcerPreviousState = true;
                    }
                    else {
                        //If the ban enforcer was previously enabled, and the user disabled it, repopulate procon's ban list
                        if (this._UseBanEnforcerPreviousState) {
                            this.RepopulateProconBanList();
                            this._UseBanEnforcerPreviousState = false;
                            this._BansFirstListed = false;
                        }
                        //If not, completely ignore all ban enforcer code
                    }

                    //Handle Inbound Records
                    if (this._UnprocessedRecordQueue.Count > 0) {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound record queue to retrive new records", 7);
                        Queue<AdKatsRecord> inboundRecords;
                        lock (this._UnprocessedRecordMutex) {
                            this.DebugWrite("DBCOMM: Inbound records found. Grabbing.", 6);
                            //Grab all messages in the queue
                            inboundRecords = new Queue<AdKatsRecord>(this._UnprocessedRecordQueue.ToArray());
                            //Clear the queue for next run
                            this._UnprocessedRecordQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (inboundRecords.Count > 0) {
                            //Pull the next record
                            AdKatsRecord record = inboundRecords.Dequeue();
                            //Upload the record
                            this.HandleRecordUpload(record);
                            //Check for action handling needs
                            if (!record.record_action_executed) {
                                //Action is only called after initial upload, not after update
                                this.DebugWrite("DBCOMM: Upload success. Attempting to add to action queue.", 6);

                                //Only queue the record for action handling if it's not an enforced ban
                                if (record.command_type.command_key != "banenforcer_enforce") {
                                    this.QueueRecordForActionHandling(record);
                                }
                            }
                            else {
                                //Performance testing area
                                if (record.source_name == this._DebugSoldierName) {
                                    this.SendMessageToSource(record, "Duration: " + ((int) DateTime.UtcNow.Subtract(this._CommandStartTime).TotalMilliseconds) + "ms");
                                }
                                this.DebugWrite("DBCOMM: Update success. Record does not need action handling.", 6);
                            }
                        }
                    }
                    else {
                        this.DebugWrite("DBCOMM: No unprocessed records. Waiting for input", 7);
                        this._DbCommunicationWaitHandle.Reset();

                        if (this._FetchActionsFromDb || this._UseBanEnforcer || this._UsingAwa) {
                            //If waiting on DB input, the maximum time we can wait is "db action frequency"
                            this._DbCommunicationWaitHandle.WaitOne(DbActionFetchFrequency * 1000);
                        }
                        else {
                            //Maximum wait time is DB access fetch time
                            this._DbCommunicationWaitHandle.WaitOne(DbUserFetchFrequency * 1000);
                        }
                    }
                }
                this.DebugWrite("DBCOMM: Ending Database Comm Thread", 2);
            }
            catch (Exception e) {
                if (e is ThreadAbortException) {
                    this.HandleException(new AdKatsException("Database comm thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                }
                else {
                    this.HandleException(new AdKatsException("Error occured in database comm thread.", e));
                }
            }
        }

        #region Connection and Setup

        private Boolean ConnectionCapable() {
            if (!string.IsNullOrEmpty(this._MySqlDatabaseName) && !string.IsNullOrEmpty(this._MySqlHostname) && !string.IsNullOrEmpty(this._MySqlPassword) && !string.IsNullOrEmpty(this._MySqlPort) && !string.IsNullOrEmpty(this._MySqlUsername)) {
                this.DebugWrite("MySql Connection capable. All variables in place.", 7);
                return true;
            }
            return false;
        }

        private MySqlConnection GetDatabaseConnection() {
            if (this.ConnectionCapable()) {
                MySqlConnection conn = new MySqlConnection(this.PrepareMySqlConnectionString());
                conn.Open();
                return conn;
            }
            this.ConsoleError("Attempted to connect to database without all variables in place.");
            return null;
        }

        private Boolean TestDatabaseConnection() {
            Boolean databaseValid = false;
            DebugWrite("testDatabaseConnection starting!", 6);
            if (this.ConnectionCapable()) {
                Boolean success = false;
                Int32 attempt = 0;
                do {
                    if (!this._IsEnabled) {
                        return false;
                    }
                    attempt++;
                    try {
                        //Prepare the connection String and create the connection object
                        using (MySqlConnection connection = this.GetDatabaseConnection()) {
                            this.ConsoleWrite("Attempting database connection. Attempt " + attempt + " of 5.");
                            //Attempt a ping through the connection
                            if (connection.Ping()) {
                                //Connection good
                                this.ConsoleSuccess("Database connection open.");
                                success = true;
                            }
                            else {
                                //Connection poor
                                this.ConsoleError("Database connection FAILED ping test.");
                            }
                        } //databaseConnection gets closed here
                        if (success) {
                            //Make sure database structure is good
                            if (this.ConfirmDatabaseSetup()) {
                                //Confirm the database is valid
                                databaseValid = true;
                                //clear setting change monitor
                                this._DbSettingsChanged = false;
                            }
                            else {
                                this.Disable();
                                break;
                            }
                        }
                    }
                    catch (Exception e) {
                        //Only perform retries if the error was a timeout
                        if (e.ToString().Contains("Unable to connect")) {
                            this.ConsoleError("Connection failed on attempt " + attempt + ". " + ((attempt <= 5) ? ("Retrying in 5 seconds. ") : ("")));
                            Thread.Sleep(5000);
                        }
                        else {
                            break;
                        }
                    }
                } while (!success && attempt < 5);
                if (!success) {
                    //Invalid credentials or no connection to database
                    this.ConsoleError("Database connection FAILED with EXCEPTION. Bad credentials, invalid hostname, or invalid port.");
                    this.Disable();
                }
            }
            else {
                this.ConsoleError("Not DB connection capable yet, complete SQL connection variables.");
            }
            DebugWrite("testDatabaseConnection finished!", 6);

            return databaseValid;
        }

        private Boolean ConfirmDatabaseSetup() {
            this.DebugWrite("Confirming Database Structure.", 3);
            try {
                if (!this.ConfirmStatLoggerTables()) {
                    this.ConsoleError("Tables from XPKiller's Stat Logger not present in the database. Enable that plugin then re-run AdKats!");
                    return false;
                }
                //Detect AdKats tables
                if (!this.ConfirmAdKatsTables()) {
                    this.ConsoleError("AdKats tables not present in the database. For this release the adkats database setup script must be run manually. Run the script then restart AdKats.");
                    return false;
                }
                this.ConsoleSuccess("Database confirmed functional for AdKats use.");
                return true;
            }
            catch (Exception e) {
                this.ConsoleError("ERROR in helper_confirmDatabaseSetup: " + e);
                return false;
            }
        }

        private void runDBSetupScript() {
            try {
                ConsoleWrite("Running database setup script. You will not lose any data.");
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        WebClient downloader = new WebClient();
                        //Set the insert command structure
                        command.CommandText = downloader.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/adkats.sql");
                        try {
                            //Attempt to execute the query
                            Int32 rowsAffected = command.ExecuteNonQuery();
                            this.ConsoleWrite("Setup script successful, your database is now prepared for use by AdKats " + this.GetPluginVersion());
                        }
                        catch (Exception e) {
                            this.ConsoleError("Your database did not accept the script. Does your account have access to table creation? AdKats will not function properly. Exception: " + e);
                        }
                    }
                }
            }
            catch (Exception e) {
                this.ConsoleError("ERROR when setting up DB, you might not have connection to github: " + e);
            }
        }

        private Boolean ConfirmAdKatsTables() {
            //Detect AdKats tables
            return this.ConfirmTable("adkats_bans") && this.ConfirmTable("adkats_commands") && this.ConfirmTable("adkats_infractions_global") && this.ConfirmTable("adkats_infractions_server") && this.ConfirmTable("adkats_records_debug") && this.ConfirmTable("adkats_records_main") && this.ConfirmTable("adkats_rolecommands") && this.ConfirmTable("adkats_roles") && this.ConfirmTable("adkats_settings") && this.ConfirmTable("adkats_users") && this.ConfirmTable("adkats_usersoldiers");
        }

        private Boolean ConfirmStatLoggerTables() {
            Boolean confirmed = true;
            //All versions of stat logger should have these tables
            if (this.ConfirmTable("tbl_playerdata") && this.ConfirmTable("tbl_server") && this.ConfirmTable("tbl_chatlog")) {
                //The universal version has a tbl_games table, detect that
                if (this.ConfirmTable("tbl_games")) {
                    this._StatLoggerVersion = "UNIVERSAL";
                    Boolean gameIDFound = false;
                    using (MySqlConnection connection = this.GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            //Attempt to execute the query
                            command.CommandText = "SELECT `GameID` AS `game_id`, `Name` AS `game_name` FROM `tbl_games` WHERE `Name` = '" + this._GameVersion + "'";
                            using (MySqlDataReader reader = command.ExecuteReader()) {
                                if (reader.Read()) {
                                    gameIDFound = true;
                                    this._GameID = reader.GetInt32("game_id");
                                }
                            }
                        }
                        if (!gameIDFound) {
                            using (MySqlCommand command = connection.CreateCommand()) {
                                //Attempt to execute the query
                                command.CommandText = "INSERT INTO `tbl_games` (`Name`) VALUES ('" + this._GameVersion + "')";
                                if (command.ExecuteNonQuery() > 0) {
                                    gameIDFound = true;
                                    this._GameID = (int) command.LastInsertedId;
                                }
                                else {
                                    this.ConsoleError("Unable to insert game ID into database.");
                                }
                            }
                        }
                        confirmed = gameIDFound;
                    }
                }
                //TODO remove when confirmed not needed
                /*
                Boolean chatSourceAlterSuccess = false;
                if (!this.SendQuery("SELECT * FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND COLUMN_NAME='logPlayerID' AND TABLE_NAME='tbl_chatlog'", false))
                {
                    this.ConsoleWarn("Updating your chat log table with player IDs. This may take some time if you have many records! Be patient!");
                    chatSourceAlterSuccess = this.SendNonQuery("Adding logPlayerID Column", "ALTER TABLE `tbl_chatlog` ADD COLUMN `logPlayerID` INT(10) UNSIGNED DEFAULT NULL", false);
                    chatSourceAlterSuccess = this.SendNonQuery("Adding logPlayerID Index", "ALTER TABLE `tbl_chatlog` ADD INDEX (`logPlayerID`)", false);
                    if (chatSourceAlterSuccess)
                    {
                        chatSourceAlterSuccess = this.SendNonQuery("Adding logPlayerID Key", "ALTER TABLE `tbl_chatlog` ADD CONSTRAINT `tbl_chatlog_ibfk_player_id` FOREIGN KEY (`logPlayerID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE CASCADE", false);
                        if (chatSourceAlterSuccess)
                        {
                            //All previous chat logs must be updated with source_ids
                            chatSourceAlterSuccess = this.SendNonQuery("Updating all previous chat logs with IDs", @"
                            UPDATE 
                                `tbl_chatlog`
                            INNER JOIN 
                                `tbl_playerdata`
                            ON 
                                `tbl_chatlog`.`logSoldierName` = `tbl_playerdata`.`SoldierName` 
                            SET 
                                `tbl_chatlog`.`logPlayerID` = `tbl_playerdata`.`PlayerID`
                            WHERE 
                                `tbl_playerdata`.`SoldierName` <> 'AutoAdmin' 
                            AND 
                                `tbl_playerdata`.`SoldierName` <> 'AdKats' 
                            AND 
                                `tbl_playerdata`.`SoldierName` <> 'Server' 
                            AND 
                                `tbl_playerdata`.`SoldierName` <> 'BanEnforcer'
                            AND 
                                `tbl_chatlog`.`logPlayerID` IS NULL
                            ", false);
                            if (chatSourceAlterSuccess)
                            {
                                this.ConsoleSuccess("Chat log player IDs added.");
                            }
                        }
                    }
                }
                else
                {
                    //Column already exists
                    chatSourceAlterSuccess = true;
                }
                if (!chatSourceAlterSuccess)
                {
                    this.ConsoleError("Unable to add logPlayerID column to chat log table.");
                    confirmed = false;
                }*/
            }
            else {
                confirmed = false;
            }
            return confirmed;
        }

        private Boolean ConfirmTable(String tablename) {
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '" + this._MySqlDatabaseName + "' AND TABLE_NAME= '" + tablename + "'";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            return reader.Read();
                        }
                    }
                }
            }
            catch (Exception e) {
                this.ConsoleError("ERROR in helper_confirmTable: " + e);
                return false;
            }
        }

        private String PrepareMySqlConnectionString() {
            //Create new String for connection
            String conString = String.Empty;
            lock (this._DbCommStringBuilder) {
                //Default to port 3306 and attempt to parse correct port
                UInt32 uintport = 3306;
                UInt32.TryParse(this._MySqlPort, out uintport);
                //Add connection variables
                this._DbCommStringBuilder.Port = uintport;
                this._DbCommStringBuilder.Server = this._MySqlHostname;
                this._DbCommStringBuilder.UserID = this._MySqlUsername;
                this._DbCommStringBuilder.Password = this._MySqlPassword;
                this._DbCommStringBuilder.Database = this._MySqlDatabaseName;
                //Set up connection pooling
                if (UseConnectionPooling) {
                    this._DbCommStringBuilder.Pooling = true;
                    this._DbCommStringBuilder.MinimumPoolSize = Convert.ToUInt32(MinConnectionPoolSize);
                    this._DbCommStringBuilder.MaximumPoolSize = Convert.ToUInt32(MaxConnectionPoolSize);
                    this._DbCommStringBuilder.ConnectionLifeTime = 600;
                }
                else {
                    this._DbCommStringBuilder.Pooling = false;
                }
                //Set Compression
                this._DbCommStringBuilder.UseCompression = UseCompressedConnection;
                //Allow User Settings
                this._DbCommStringBuilder.AllowUserVariables = true;
                //Set Timeout Settings
                this._DbCommStringBuilder.DefaultCommandTimeout = 3600;
                this._DbCommStringBuilder.ConnectionTimeout = 50;

                //Build the final connection String
                conString = this._DbCommStringBuilder.ConnectionString;
            }
            return conString;
            //Old Code
            //return "Server=" + mySqlHostname + ";Port=" + mySqlPort + ";Database=" + this.mySqlDatabaseName + ";Uid=" + mySqlUsername + ";Pwd=" + mySqlPassword + ";";
        }

        #endregion

        #region Queries

        private void UploadAllSettings() {
            if (!this._IsEnabled) {
                return;
            }
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                DebugWrite("uploadAllSettings starting!", 6);
                this.QueueSettingForUpload(new CPluginVariable(@"Plugin Version", typeof (String), this.GetPluginVersion()));
                this.QueueSettingForUpload(new CPluginVariable(@"Debug level", typeof (Int32), this._DebugLevel));
                this.QueueSettingForUpload(new CPluginVariable(@"Debug Soldier Name", typeof (String), this._DebugSoldierName));
                this.QueueSettingForUpload(new CPluginVariable(@"Server VOIP Address", typeof (String), this._ServerVoipAddress));
                this.QueueSettingForUpload(new CPluginVariable(@"External Access Key", typeof (String), this._ExternalCommandAccessKey));
                this.QueueSettingForUpload(new CPluginVariable(@"Fetch Actions from Database", typeof (Boolean), this._FetchActionsFromDb));
                this.QueueSettingForUpload(new CPluginVariable(@"Use Additional Ban Message", typeof (Boolean), this._UseBanAppend));
                this.QueueSettingForUpload(new CPluginVariable(@"Additional Ban Message", typeof (String), this._BanAppend));
                this.QueueSettingForUpload(new CPluginVariable(@"Use Ban Enforcer", typeof (Boolean), this._UseBanEnforcer));
                this.QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by NAME", typeof (Boolean), this._DefaultEnforceName));
                this.QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by GUID", typeof (Boolean), this._DefaultEnforceGUID));
                this.QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by IP", typeof (Boolean), this._DefaultEnforceIP));
                this.QueueSettingForUpload(new CPluginVariable(@"Minimum Required Reason Length", typeof (Int32), this._RequiredReasonLength));
                this.QueueSettingForUpload(new CPluginVariable(@"Punishment Hierarchy", typeof (String), CPluginVariable.EncodeStringArray(this._PunishmentHierarchy)));
                this.QueueSettingForUpload(new CPluginVariable(@"Combine Server Punishments", typeof (Boolean), this._CombineServerPunishments));
                this.QueueSettingForUpload(new CPluginVariable(@"Only Kill Players when Server in low population", typeof (Boolean), this._OnlyKillOnLowPop));
                this.QueueSettingForUpload(new CPluginVariable(@"Low Population Value", typeof (Int32), this._LowPopPlayerCount));
                this.QueueSettingForUpload(new CPluginVariable(@"IRO Punishment Overrides Low Pop", typeof (Boolean), this._IROOverridesLowPop));
                this.QueueSettingForUpload(new CPluginVariable(@"Send Emails", typeof (Boolean), this._UseEmail));
                this.QueueSettingForUpload(new CPluginVariable(@"On-Player-Muted Message", typeof (String), this._MutedPlayerMuteMessage));
                this.QueueSettingForUpload(new CPluginVariable(@"On-Player-Killed Message", typeof (String), this._MutedPlayerKillMessage));
                this.QueueSettingForUpload(new CPluginVariable(@"On-Player-Kicked Message", typeof (String), this._MutedPlayerKickMessage));
                this.QueueSettingForUpload(new CPluginVariable(@"# Chances to give player before kicking", typeof (Int32), this._MutedPlayerChances));
                this.QueueSettingForUpload(new CPluginVariable(@"Require Whitelist for Access", typeof (Boolean), this._RequireTeamswapWhitelist));
                this.QueueSettingForUpload(new CPluginVariable(@"Auto-Whitelist Count", typeof (Int32), this._PlayersToAutoWhitelist));
                this.QueueSettingForUpload(new CPluginVariable(@"Ticket Window High", typeof (Int32), this._TeamSwapTicketWindowHigh));
                this.QueueSettingForUpload(new CPluginVariable(@"Ticket Window Low", typeof (Int32), this._TeamSwapTicketWindowLow));
                this.QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistant Perk", typeof (Boolean), this._EnableAdminAssistantPerk));
                this.QueueSettingForUpload(new CPluginVariable(@"Minimum Confirmed Reports Per Month", typeof (Int32), this._MinimumRequiredMonthlyReports));
                this.QueueSettingForUpload(new CPluginVariable(@"Yell display time seconds", typeof (Int32), this._YellDuration));
                this.QueueSettingForUpload(new CPluginVariable(@"Pre-Message List", typeof (String), CPluginVariable.EncodeStringArray(this._PreMessageList.ToArray())));
                this.QueueSettingForUpload(new CPluginVariable(@"Require Use of Pre-Messages", typeof (Boolean), this._RequirePreMessageUse));
                DebugWrite("uploadAllSettings finished!", 6);
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing all settings for upload.", e));
            }
        }

        private void UploadSetting(CPluginVariable var) {
            this.DebugWrite("uploadSetting starting!", 7);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        String value = var.Value.Replace("'", "*").Replace('"', '*');
                        //Check for length too great
                        if (value.Length > 1499) {
                            this.ConsoleError("Unable to upload setting. Length of setting too great. Really dude? It's 1500 chars. This is battlefield, not a book club.");
                            return;
                        }
                        this.DebugWrite(value, 7);
                        //Set the insert command structure
                        command.CommandText = @"
                        INSERT INTO `" + this._MySqlDatabaseName + @"`.`adkats_settings` 
                        (
                            `server_id`, 
                            `setting_name`, 
                            `setting_type`, 
                            `setting_value`
                        ) 
                        VALUES 
                        ( 
                            " + this._ServerID + @", 
                            '" + var.Name + @"', 
                            '" + var.Type + @"', 
                            '" + value + @"'
                        ) 
                        ON DUPLICATE KEY 
                        UPDATE 
                            `setting_value` = '" + value + @"'";
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0) {
                            this.DebugWrite("Setting " + var.Name + " pushed to database", 7);
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while uploading setting to database.", e));
            }

            DebugWrite("uploadSetting finished!", 7);
        }

        private void FetchSettings(long serverID, Boolean verbose) {
            this.DebugWrite("fetchSettings starting!", 6);
            Boolean success = false;
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                //Success fetching settings
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        String sql = @"
                        SELECT  
                            `setting_name`, 
                            `setting_type`, 
                            `setting_value`
                        FROM 
                            `" + this._MySqlDatabaseName + @"`.`adkats_settings` 
                        WHERE 
                            `server_id` = " + serverID;
                        command.CommandText = sql;
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            //Grab the settings
                            while (reader.Read()) {
                                success = true;
                                //Create as variable in case needed later
                                CPluginVariable var = new CPluginVariable(reader.GetString("setting_name"), reader.GetString("setting_type"), reader.GetString("setting_value"));
                                this.SetPluginVariable(var.Name, var.Value);
                            }
                            if (success) {
                                this._LastDbSettingFetch = DateTime.UtcNow;
                                this.UpdateSettingPage();
                            }
                            else if(verbose){
                                this.ConsoleError("Settings could not be loaded. Server " + serverID + " invalid.");
                            }
                            this._SettingImportID = this._ServerID;
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching settings from database.", e));
            }
            this.DebugWrite("fetchSettings finished!", 6);
        }

        private void FetchRoles() {
            this.DebugWrite("fetchRoles starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                //Lock all command dictionaries for this operation
                lock (this._RoleKeyDictionary) {
                    lock (this._RoleIDDictionary) {
                        lock (this._RoleNameDictionary) {
                            //Create the connection
                            using (MySqlConnection connection = this.GetDatabaseConnection()) {
                                //Create the command
                                Dictionary<long, AdKats_Role> roleList = new Dictionary<long, AdKats_Role>();
                                using (MySqlCommand sqlcommand = connection.CreateCommand()) {
                                    //Query to fetch all roles
                                    const string sql = @"
                                    SELECT 
	                                    `role_id`,
	                                    `role_key`,
	                                    `role_name`
                                    FROM 
	                                    `adkats_roles`";
                                    sqlcommand.CommandText = sql;
                                    using (MySqlDataReader reader = sqlcommand.ExecuteReader()) {
                                        //Grab the commands
                                        while (reader.Read()) {
                                            //Create as new AdKats_Command
                                            AdKats_Role role = new AdKats_Role {
                                                                                   role_id = reader.GetInt64("role_id"),
                                                                                   role_key = reader.GetString("role_key"),
                                                                                   role_name = reader.GetString("role_name")
                                                                               };
                                            //Add the command to temp list
                                            roleList.Add(role.role_id, role);
                                        }
                                    }
                                }
                                using (MySqlCommand sqlcommand = connection.CreateCommand()) {
                                    //Query to fetch all command assignments
                                    const string sql = @"
                                    SELECT 
	                                    `role_id`,
	                                    `command_id`
                                    FROM 
	                                    `adkats_rolecommands`
                                    ORDER BY
                                        `role_id`
                                    ASC";
                                    sqlcommand.CommandText = sql;
                                    using (MySqlDataReader reader = sqlcommand.ExecuteReader()) {
                                        //Grab the command assignments
                                        AdKats_Role currentRole = null;
                                        while (reader.Read()) {
                                            Int32 roleID = reader.GetInt32("role_id");
                                            Int32 commandID = reader.GetInt32("command_id");
                                            //Is the fetched role different than the current operating role?
                                            if (currentRole == null || roleID != currentRole.role_id) {
                                                //Yes it's different, grab it
                                                if (roleList.TryGetValue(roleID, out currentRole)) {
                                                    currentRole.allowedCommands.Clear();
                                                }
                                                else {
                                                    //Failed to grab the role
                                                    this.ConsoleError("Failed to grab current role for " + roleID + " when fetching command assignments.");
                                                    return;
                                                }
                                            }
                                            AdKatsCommand currentCommand = null;
                                            if (this._CommandIDDictionary.TryGetValue(commandID, out currentCommand)) {
                                                currentRole.allowedCommands.Add(currentCommand.command_key, currentCommand);
                                            }
                                            else {
                                                this.ConsoleError("Could not assign command " + commandID + " to role " + roleID + " when fetching command assignments.");
                                                //return false;
                                            }
                                        }
                                    }
                                }
                                if (roleList.Count > 0) {
                                    //Empty all the role dictionaries
                                    this._RoleKeyDictionary.Clear();
                                    this._RoleNameDictionary.Clear();
                                    this._RoleIDDictionary.Clear();
                                    //Loop over each role found and add them into the dictionaries
                                    foreach (AdKats_Role role in roleList.Values) {
                                        this._RoleKeyDictionary.Add(role.role_key, role);
                                        this._RoleNameDictionary.Add(role.role_name, role);
                                        this._RoleIDDictionary.Add(role.role_id, role);
                                    }
                                    //Successful
                                    return;
                                }
                                this.ConsoleError("Roles could not be fetched.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching roles from database.", e));
            }
            this.DebugWrite("fetchRoles finished!", 6);
        }

        private void FetchCommands() {
            this.DebugWrite("fetchCommands starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                //Lock all command dictionaries for this operation
                lock (this._CommandIDDictionary) {
                    lock (this._CommandKeyDictionary) {
                        lock (this._CommandNameDictionary) {
                            lock (this._CommandTextDictionary) {
                                //Create the connection
                                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                                    //Create the command
                                    using (MySqlCommand sqlcommand = connection.CreateCommand()) {
                                        //Query to fetch all commands
                                        const string sql = @"
                                        SELECT 
	                                        `command_id`,
	                                        `command_active`,
	                                        `command_key`,
	                                        `command_logging`,
	                                        `command_name`,
	                                        `command_text`,
                                            `command_playerInteraction`
                                        FROM 
	                                        `adkats_commands`";
                                        sqlcommand.CommandText = sql;
                                        List<AdKatsCommand> commandList = new List<AdKatsCommand>();
                                        using (MySqlDataReader reader = sqlcommand.ExecuteReader()) {
                                            //Grab the commands
                                            while (reader.Read()) {
                                                //Create as new AdKats_Command
                                                AdKatsCommand command = new AdKatsCommand {
                                                                                              command_id = reader.GetInt32("command_id"),
                                                                                              command_active = (AdKatsCommand.CommandActive) Enum.Parse(typeof (AdKatsCommand.CommandActive), reader.GetString("command_active")),
                                                                                              command_key = reader.GetString("command_key"),
                                                                                              command_logging = (AdKatsCommand.CommandLogging) Enum.Parse(typeof (AdKatsCommand.CommandLogging), reader.GetString("command_logging")),
                                                                                              command_name = reader.GetString("command_name"),
                                                                                              command_text = reader.GetString("command_text"),
                                                                                              command_playerInteraction = reader.GetBoolean("command_playerInteraction")
                                                                                          };
                                                //Add the command to temp list
                                                commandList.Add(command);
                                            }
                                        }
                                        if (commandList.Count > 0) {
                                            //Empty all the command dictionaries
                                            this._CommandIDDictionary.Clear();
                                            this._CommandKeyDictionary.Clear();
                                            this._CommandNameDictionary.Clear();
                                            this._CommandTextDictionary.Clear();
                                            //Loop over each command found and add it into the dictionaries
                                            foreach (AdKatsCommand command in commandList) {
                                                this._CommandIDDictionary.Add(command.command_id, command);
                                                this._CommandKeyDictionary.Add(command.command_key, command);
                                                this._CommandNameDictionary.Add(command.command_name, command);
                                                this._CommandTextDictionary.Add(command.command_text, command);
                                            }
                                            //Successful
                                            return;
                                        }
                                        this.ConsoleError("Commands could not be fetched.");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching commands from database.", e));
            }
            this.DebugWrite("fetchCommands finished!", 6);
        }

        private void UploadCommand(AdKatsCommand command) {
            DebugWrite("uploadCommand starting!", 6);

            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand sqlcommand = connection.CreateCommand()) {
                        //Set the insert command structure
                        sqlcommand.CommandText = @"
                        INSERT INTO 
                        `" + this._MySqlDatabaseName + @"`.`adkats_commands` 
                        (
	                        `command_id`,
	                        `command_active`,
	                        `command_key`,
	                        `command_logging`,
	                        `command_name`,
	                        `command_text`,
                            `command_playerInteraction`
                        ) 
                        VALUES 
                        (
	                        @command_id,
	                        @command_active,
	                        @command_key,
	                        @command_logging,
	                        @command_name,
	                        @command_text,
                            @command_playerInteraction
                        ) 
                        ON DUPLICATE KEY 
                        UPDATE 
	                        `command_active` = @command_active, 
	                        `command_logging` = @command_logging, 
	                        `command_name` = @command_name, 
	                        `command_text` = @command_text,
                            `command_playerInteraction` = @command_playerInteraction";

                        //Fill the command
                        sqlcommand.Parameters.AddWithValue("@command_id", command.command_id);
                        sqlcommand.Parameters.AddWithValue("@command_active", command.command_active.ToString());
                        sqlcommand.Parameters.AddWithValue("@command_key", command.command_key);
                        sqlcommand.Parameters.AddWithValue("@command_logging", command.command_logging.ToString());
                        sqlcommand.Parameters.AddWithValue("@command_name", command.command_name);
                        sqlcommand.Parameters.AddWithValue("@command_text", command.command_text);
                        sqlcommand.Parameters.AddWithValue("@command_playerInteraction", command.command_playerInteraction);

                        //Get reference to the command in case of error
                        //Attempt to execute the query
                        if (sqlcommand.ExecuteNonQuery() > 0) {
                        }
                    }
                }

                DebugWrite("uploadCommand finished!", 6);
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Unexpected error uploading command.", e));
            }
        }

        private void HandleRecordUpload(AdKatsRecord record) {
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return;
            }
            try {
                this.DebugWrite("DBCOMM: Entering handle record upload", 5);
                if (record.record_id != -1 || record.record_action_executed) {
                    //Record already has a record ID, or action has already been taken, it can only be updated
                    if (record.command_type.command_logging != AdKatsCommand.CommandLogging.Ignore) {
                        if (record.record_exception == null) {
                            //Only call update if the record contained no errors
                            this.DebugWrite("DBCOMM: UPDATING record for " + record.command_type, 5);
                            //Update Record
                            this.UpdateRecord(record, false);
                        }
                        else {
                            this.DebugWrite("DBCOMM: " + record.command_type + " record contained errors, skipping UPDATE", 4);
                        }
                    }
                    else {
                        this.DebugWrite("DBCOMM: Skipping record UPDATE for " + record.command_type, 5);
                    }
                }
                else {
                    this.DebugWrite("DBCOMM: Record needs full upload, checking.", 5);
                    //No record ID. Perform full upload
                    switch (record.command_type.command_key) {
                        case "player_punish":
                            //Upload for punish is required

                            //TODO confirm this stops the action if cant punish
                            if (this.CanPunish(record)) {
                                //Check if the punish will be Double counted
                                Boolean iroStatus = this._IROActive && this.FetchIROStatus(record);
                                if (iroStatus) {
                                    record.isIRO = true;
                                    //Upload record twice
                                    this.DebugWrite("DBCOMM: UPLOADING IRO Punish", 5); //IRO - Immediate Repeat Offence
                                    this.UploadRecord(record);
                                    this.UploadRecord(record);
                                }
                                else {
                                    //Upload record once
                                    this.DebugWrite("DBCOMM: UPLOADING Punish", 5);
                                    this.UploadRecord(record);
                                }
                            }
                            break;
                        case "player_forgive":
                            //Upload for forgive is required
                            //No restriction on forgives/minute
                            this.DebugWrite("DBCOMM: UPLOADING Forgive", 5);
                            this.UploadRecord(record);
                            break;
                        default:
                            //Case for any other command
                            //Check logging setting for record command type
                            if (record.command_type.command_logging != AdKatsCommand.CommandLogging.Ignore) {
                                this.DebugWrite("UPLOADING record for " + record.command_type, 5);
                                //Upload Record
                                this.UploadRecord(record);
                            }
                            else {
                                this.DebugWrite("Skipping record UPLOAD for " + record.command_type, 6);
                            }
                            break;
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = this.HandleException(new AdKatsException("Error while handling record upload.", e));
            }
        }

        private Boolean UploadRecord(AdKatsRecord record) {
            DebugWrite("uploadRecord starting!", 6);

            Boolean success = false;
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return false;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Decide which table the record should be added to
                        String tablename = (record.isDebug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");
                        //Set the insert command structure
                        command.CommandText = @"INSERT INTO " + tablename + @"
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
                            'Y'
                        )";

                        //Fill the command
                        command.Parameters.AddWithValue("@server_id", record.server_id);
                        if (record.command_type == null) {
                            this.ConsoleError("Command type was null in uploadRecord, unable to continue.");
                            return false;
                        }
                        command.Parameters.AddWithValue("@command_type", record.command_type.command_id);
                        if (record.command_action == null) {
                            record.command_action = record.command_type;
                        }
                        command.Parameters.AddWithValue("@command_action", record.command_action.command_id);
                        command.Parameters.AddWithValue("@command_numeric", record.command_numeric);
                        String tName = "NoNameTarget";
                        if (!String.IsNullOrEmpty(record.target_name)) {
                            tName = record.target_name;
                        }
                        if (record.target_player != null) {
                            if (!String.IsNullOrEmpty(record.target_player.player_name)) {
                                tName = record.target_player.player_name;
                            }
                            command.Parameters.AddWithValue("@target_id", record.target_player.player_id);
                        }
                        else {
                            command.Parameters.AddWithValue("@target_id", null);
                        }
                        command.Parameters.AddWithValue("@target_name", tName);
                        command.Parameters.AddWithValue("@source_name", record.source_name);
                        //Add the source ID if available
                        if (record.source_player != null) {
                            command.Parameters.AddWithValue("@source_id", record.source_player.player_id);
                        }
                        else {
                            command.Parameters.AddWithValue("@source_id", null);
                        }
                        String messageIRO = record.record_message + ((record.isIRO) ? (" [IRO]") : (""));
                        //Trim to 500 characters (Should only hit this limit when processing error messages)
                        messageIRO = messageIRO.Length <= 500 ? messageIRO : messageIRO.Substring(0, 500);
                        command.Parameters.AddWithValue("@record_message", messageIRO);

                        //Get reference to the command in case of error
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0) {
                            success = true;
                            record.record_id = command.LastInsertedId;
                        }
                    }
                }

                if (success) {
                    DebugWrite(record.command_action.command_key + " upload for player " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
                }
                else {
                    record.record_exception = new AdKatsException("Unknown error uploading record.");
                    this.HandleException(record.record_exception);
                }

                DebugWrite("uploadRecord finished!", 6);
                return success;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Unexpected error uploading Record.", e);
                this.HandleException(record.record_exception);
                return false;
            }
        }

        private Boolean SendQuery(String query, Boolean verbose) {
            if (String.IsNullOrEmpty(query)) {
                return false;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Attempt to execute the query
                        command.CommandText = query;
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                if (verbose) {
                                    this.ConsoleSuccess("Query returned values.");
                                }
                                return true;
                            }
                            if (verbose) {
                                this.ConsoleError("Query returned no results.");
                            }
                            return false;
                        }
                    }
                }
            }
            catch (Exception e) {
                if (verbose) {
                    this.ConsoleError(e.ToString());
                }
                return false;
            }
        }

        private Boolean SendNonQuery(String desc, String nonQuery, Boolean verbose) {
            if (String.IsNullOrEmpty(nonQuery)) {
                return false;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = nonQuery;
                        //Attempt to execute the non query
                        Int32 rowsAffected = command.ExecuteNonQuery();
                        if (verbose) {
                            this.ConsoleSuccess("Non-Query success. " + rowsAffected + " rows affected. [" + desc + "]");
                        }
                        return true;
                    }
                }
            }
            catch (Exception e) {
                if (verbose) {
                    this.ConsoleError("Non-Query failed. [" + desc + "]: " + e);
                }
                return false;
            }
        }

        private void UpdateRecord(AdKatsRecord record, bool debug) {
            DebugWrite("updateRecord starting!", 6);

            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return;
            }
            try {
                Int32 attempts = 0;
                Boolean success = false;
                do {
                    try {
                        using (MySqlConnection connection = this.GetDatabaseConnection()) {
                            using (MySqlCommand command = connection.CreateCommand()) {
                                String tablename = (debug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");
                                //Set the insert command structure
                                command.CommandText = "UPDATE " + tablename + @" 
                            SET 
                                `command_action` = @command_action, 
                                `command_numeric` = @command_numeric, 
                                `record_message` = @record_message, 
                                `adkats_read` = 'Y' 
                            WHERE 
                                `record_id` = @record_id";
                                //Fill the command
                                command.Parameters.AddWithValue("@record_id", record.record_id);
                                command.Parameters.AddWithValue("@command_numeric", record.command_numeric);
                                //Trim to 500 characters
                                record.record_message = record.record_message.Length <= 500 ? record.record_message : record.record_message.Substring(0, 500);
                                command.Parameters.AddWithValue("@record_message", record.record_message);
                                command.Parameters.AddWithValue("@command_action", record.command_action.command_id);

                                //Attempt to execute the query
                                Int32 rowsAffected = command.ExecuteNonQuery();
                                success = true;
                            }
                        }
                    }
                    catch (Exception e) {
                        this.ConsoleError(e.ToString());
                        success = false;
                    }
                } while (!success && attempts++ < 5);

                if (success) {
                    DebugWrite(record.command_action.command_key + " update for player " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
                }
                else {
                    ConsoleError(record.command_action.command_key + " update for player " + record.target_name + " by " + record.source_name + " FAILED!");
                }

                DebugWrite("updateRecord finished!", 6);
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while updating record", e));
            }
        }

        //DONE
        private AdKatsRecord FetchRecordByID(Int64 recordID, Boolean debug) {
            DebugWrite("fetchRecordByID starting!", 6);
            AdKatsRecord record = null;
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return null;
            }
            try {
                //Success fetching record
                Boolean success = false;
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        String tablename = (debug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");
                        String sql = @"
                        SELECT 
                            `record_id`, 
                            `server_id`, 
                            `command_type`, 
                            `command_action`, 
                            `command_numeric`, 
                            `target_name`, 
                            `target_id`, 
                            `source_name`, 
                            `record_message`, 
                            `record_time` 
                        FROM 
                            " + tablename + @" 
                        WHERE 
                            `record_id` = " + recordID;
                        command.CommandText = sql;
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            //Grab the record
                            if (reader.Read()) {
                                success = true;

                                record = new AdKatsRecord();
                                record.record_source = AdKatsRecord.Sources.Database;
                                record.record_id = reader.GetInt64("record_id");
                                record.server_id = reader.GetInt64("server_id");
                                Int32 commandTypeInt = reader.GetInt32("command_type");
                                if (!this._CommandIDDictionary.TryGetValue(commandTypeInt, out record.command_type)) {
                                    this.ConsoleError("Unable to parse command type " + commandTypeInt + " when fetching record by ID.");
                                }
                                Int32 commandActionInt = reader.GetInt32("command_action");
                                if (!this._CommandIDDictionary.TryGetValue(commandActionInt, out record.command_action)) {
                                    this.ConsoleError("Unable to parse command action " + commandActionInt + " when fetching record by ID.");
                                }
                                record.command_numeric = reader.GetInt32("command_numeric");
                                record.target_name = reader.GetString("target_name");
                                if (!reader.IsDBNull(6)) {
                                    record.target_player = new AdKats_Player {
                                                                                 player_id = reader.GetInt64(6)
                                                                             };
                                }
                                record.source_name = reader.GetString("source_name");
                                record.record_message = reader.GetString("record_message");
                                record.record_time = reader.GetDateTime("record_time");
                            }
                            if (success) {
                                this.DebugWrite("Record found for ID " + recordID, 5);
                            }
                            else {
                                this.DebugWrite("No record found for ID " + recordID, 5);
                            }
                        }
                        if (success && record.target_player != null) {
                            record.target_player = this.FetchPlayer(true, record.target_player.player_id, null, null, null);
                            record.target_name = record.target_player.player_name;
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching record by ID", e));
            }

            DebugWrite("fetchRecordByID finished!", 6);
            return record;
        }

        //DONE
        private IEnumerable<AdKatsRecord> FetchUnreadRecords() {
            DebugWrite("fetchUnreadRecords starting!", 6);
            //Create return list
            List<AdKatsRecord> records = new List<AdKatsRecord>();
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return records;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        String sql = @"
                        SELECT 
                            `record_id`, 
                            `server_id`, 
                            `command_type`, 
                            `command_action`, 
                            `command_numeric`, 
                            `target_name`, 
                            `target_id`, 
                            `source_name`, 
                            `record_message`, 
                            `record_time` 
                        FROM 
                            `" + this._MySqlDatabaseName + @"`.`adkats_records_main` 
                        WHERE 
                            `adkats_read` = 'N' 
                        AND 
                            `server_id` = " + this._ServerID;
                        command.CommandText = sql;
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            //Grab the record
                            while (reader.Read()) {
                                AdKatsRecord record = new AdKatsRecord();
                                record.record_source = AdKatsRecord.Sources.Database;
                                record.record_id = reader.GetInt64("record_id");
                                record.server_id = reader.GetInt64("server_id");
                                Int32 commandTypeInt = reader.GetInt32("command_type");
                                if (!this._CommandIDDictionary.TryGetValue(commandTypeInt, out record.command_type)) {
                                    this.ConsoleError("Unable to parse command type " + commandTypeInt + " when fetching record by ID.");
                                }
                                Int32 commandActionInt = reader.GetInt32("command_action");
                                if (!this._CommandIDDictionary.TryGetValue(commandActionInt, out record.command_action)) {
                                    this.ConsoleError("Unable to parse command action " + commandActionInt + " when fetching record by ID.");
                                }
                                record.command_numeric = reader.GetInt32("command_numeric");
                                record.target_name = reader.GetString("target_name");
                                object value = reader.GetValue(6);
                                Int64 targetIDParse = -1;
                                DebugWrite("id fetched!", 6);
                                if (Int64.TryParse(value.ToString(), out targetIDParse)) {
                                    DebugWrite("id parsed! " + targetIDParse, 6);
                                    //Check if the player needs to be imported, or if they are already in the server
                                    AdKats_Player importedPlayer = this.FetchPlayer(true, targetIDParse, null, null, null);
                                    AdKats_Player currentPlayer = null;
                                    if (!String.IsNullOrEmpty(importedPlayer.player_name) && this._PlayerDictionary.TryGetValue(importedPlayer.player_name, out currentPlayer)) {
                                        this.DebugWrite("External player is currently in the server, using existing data.", 5);
                                        record.target_player = currentPlayer;
                                    }
                                    else {
                                        this.DebugWrite("External player is not in the server, fetching from database.", 5);
                                        record.target_player = importedPlayer;
                                    }
                                    record.target_name = record.target_player.player_name;
                                }
                                else {
                                    DebugWrite("id parse failed!", 6);
                                }
                                record.source_name = reader.GetString("source_name");
                                record.record_message = reader.GetString("record_message");
                                record.record_time = reader.GetDateTime("record_time");

                                records.Add(record);
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching unread records from database.", e));
            }

            DebugWrite("fetchUnreadRecords finished!", 6);
            return records;
        }

        //DONE
        private Int64 FetchNameBanCount() {
            DebugWrite("fetchNameBanCount starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return 0;
            }
            if (this._NameBanCount >= 0 && (DateTime.UtcNow - this._LastNameBanCountFetch).TotalSeconds < 60) {
                return this._NameBanCount;
            }
            this._LastNameBanCountFetch = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            COUNT(ban_id) AS `ban_count`
                        FROM 
	                        `adkats_bans` 
                        WHERE 
                            `adkats_bans`.`ban_enforceName` = 'Y' 
                        AND 
                            `ban_status` = 'Active'";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                this._NameBanCount = reader.GetInt64("ban_count");
                                return this._NameBanCount;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching number of name bans.", e));
            }

            DebugWrite("fetchNameBanCount finished!", 6);
            return -1;
        }

        //DONE
        private Int64 FetchGUIDBanCount() {
            DebugWrite("fetchGUIDBanCount starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return 0;
            }
            if (this._GUIDBanCount >= 0 && (DateTime.UtcNow - this._LastGUIDBanCountFetch).TotalSeconds < 60) {
                return this._GUIDBanCount;
            }
            this._LastGUIDBanCountFetch = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            COUNT(ban_id) AS `ban_count`
                        FROM 
	                        `adkats_bans` 
                        WHERE 
                            `adkats_bans`.`ban_enforceGUID` = 'Y' 
                        AND 
                            `ban_status` = 'Active'";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                this._GUIDBanCount = reader.GetInt64("ban_count");
                                return this._GUIDBanCount;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching number of GUID bans.", e));
            }

            DebugWrite("fetchGUIDBanCount finished!", 6);
            return -1;
        }

        //DONE
        private Int64 FetchIPBanCount() {
            DebugWrite("fetchIPBanCount starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return 0;
            }
            if (this._IPBanCount >= 0 && (DateTime.UtcNow - this._LastIPBanCountFetch).TotalSeconds < 60) {
                return this._IPBanCount;
            }
            this._LastIPBanCountFetch = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            COUNT(ban_id) AS `ban_count` 
                        FROM 
	                        `adkats_bans` 
                        WHERE 
                            `adkats_bans`.`ban_enforceIP` = 'Y' 
                        AND 
                            `ban_status` = 'Active'";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                this._IPBanCount = reader.GetInt64("ban_count");
                                return this._IPBanCount;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching number of IP bans.", e));
            }

            DebugWrite("fetchIPBanCount finished!", 6);
            return -1;
        }

        //DONE
        private void RemoveUser(AdKatsUser user) {
            DebugWrite("removeUser starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Set the insert command structure
                        command.CommandText = "DELETE FROM `" + this._MySqlDatabaseName + "`.`adkats_users` WHERE `user_id` = @user_id";
                        //Set values
                        command.Parameters.AddWithValue("@user_id", user.user_id);
                        //Attempt to execute the query
                        Int32 rowsAffected = command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while removing user.", e));
            }
            DebugWrite("removeUser finished!", 6);
        }

        //DONE
        private void RemoveRole(AdKats_Role aRole) {
            DebugWrite("removeRole starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                //Assign "Default Guest" to all users currently on this role
                AdKats_Role guestRole = null;
                if (this._RoleKeyDictionary.TryGetValue("guest_default", out guestRole)) {
                    foreach (AdKatsUser aUser in this._UserCache.Values) {
                        if (aUser.user_role.role_key == aRole.role_key) {
                            aUser.user_role = guestRole;
                        }
                        this.UploadUser(aUser);
                    }
                }
                else {
                    this.ConsoleError("Could not fetch default guest user role. Unsafe to remove requested user role.");
                    return;
                }
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    //Delete all role commands for this role
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Set the insert command structure
                        command.CommandText = "DELETE FROM `" + this._MySqlDatabaseName + "`.`adkats_rolecommands` WHERE `role_id` = @role_id";
                        //Set values
                        command.Parameters.AddWithValue("@role_id", aRole.role_id);
                        //Attempt to execute the query
                        Int32 rowsAffected = command.ExecuteNonQuery();
                    }
                    //Finally delete the role
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Set the insert command structure
                        command.CommandText = "DELETE FROM `" + this._MySqlDatabaseName + "`.`adkats_roles` WHERE `role_id` = @role_id";
                        //Set values
                        command.Parameters.AddWithValue("@role_id", aRole.role_id);
                        //Attempt to execute the query
                        Int32 rowsAffected = command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while removing user.", e));
            }
            DebugWrite("removeRole finished!", 6);
        }

        //DONE
        private void UploadUser(AdKatsUser aUser) {
            DebugWrite("uploadUser starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                this.DebugWrite("Uploading user: " + aUser.user_name, 5);

                //Open db connection
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    //Upload/Update the main user object
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //If the user does not have a current role, we need to assign them one
                        if (aUser.user_role == null) {
                            //Make sure we have a role to give them
                            AdKats_Role aRole = null;
                            if (this._RoleKeyDictionary.TryGetValue("guest_default", out aRole)) {
                                aUser.user_role = aRole;
                            }
                            else {
                                this.ConsoleError("Unable to assign default guest role to user " + aUser.user_name + ". Unable to upload user.");
                                return;
                            }
                        }
                        //Set the insert command structure
                        command.CommandText = @"
                        INSERT INTO 
	                        `adkats_users`
                        (
	                        " + ((aUser.user_id > 0) ? ("`user_id`,") : ("")) + @"
	                        `user_name`,
	                        `user_email`,
	                        `user_phone`,
	                        `user_role`
                        )
                        VALUES
                        (
	                        " + ((aUser.user_id > 0) ? ("@user_id,") : ("")) + @"
	                        @user_name,
	                        @user_email,
	                        @user_phone,
	                        @user_role
                        )
                        ON DUPLICATE KEY UPDATE
	                        `user_name` = @user_name,
	                        `user_email` = @user_email,
	                        `user_phone` = @user_phone,
	                        `user_role` = @user_role";
                        //Set values
                        if (aUser.user_id > 0) {
                            command.Parameters.AddWithValue("@user_id", aUser.user_id);
                        }
                        command.Parameters.AddWithValue("@user_name", aUser.user_name);
                        command.Parameters.AddWithValue("@user_email", aUser.user_email);
                        command.Parameters.AddWithValue("@user_phone", aUser.user_phone);
                        command.Parameters.AddWithValue("@user_role", aUser.user_role.role_id);

                        //Attempt to execute the query
                        Int32 rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0) {
                            //Set the user's new ID if new
                            if (aUser.user_id < 0) {
                                aUser.user_id = command.LastInsertedId;
                            }
                            this.DebugWrite("User uploaded to database SUCCESSFULY.", 5);
                        }
                        else {
                            this.ConsoleError("Unable to upload user " + aUser.user_name + " to database.");
                            return;
                        }
                    }
                    //Run command to delete all current soldiers
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"DELETE FROM `adkats_usersoldiers` where `user_id` = " + aUser.user_id;
                        //Attempt to execute the query
                        Int32 rowsAffected = command.ExecuteNonQuery();
                    }
                    //Upload/Update the user's soldier list
                    if (aUser.soldierDictionary.Count > 0) {
                        //Refill user with current soldiers
                        foreach (AdKats_Player aPlayer in aUser.soldierDictionary.Values) {
                            using (MySqlCommand command = connection.CreateCommand()) {
                                //Set the insert command structure
                                command.CommandText = @"
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
	                                `player_id` = @player_id";
                                //Set values
                                command.Parameters.AddWithValue("@user_id", aUser.user_id);
                                command.Parameters.AddWithValue("@player_id", aPlayer.player_id);

                                //Attempt to execute the query
                                Int32 rowsAffected = command.ExecuteNonQuery();
                                if (rowsAffected > 0) {
                                    this.DebugWrite("Soldier link " + aUser.user_id + "->" + aPlayer.player_id + " uploaded to database SUCCESSFULY.", 5);
                                }
                                else {
                                    this.ConsoleError("Unable to upload soldier link for " + aUser.user_name + " to database.");
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while updating player access.", e));
            }

            DebugWrite("uploadUser finished!", 6);
        }

        //DONE
        private void UploadRole(AdKats_Role aRole) {
            DebugWrite("uploadRole starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                lock (aRole) {
                    lock (aRole.allowedCommands) {
                        this.DebugWrite("Uploading role: " + aRole.role_name, 5);

                        //Open db connection
                        using (MySqlConnection connection = this.GetDatabaseConnection()) {
                            //Upload/Update the main role object
                            using (MySqlCommand command = connection.CreateCommand()) {
                                //Set the insert command structure
                                command.CommandText = @"
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
                                //Set values
                                command.Parameters.AddWithValue("@role_key", aRole.role_key);
                                command.Parameters.AddWithValue("@role_name", aRole.role_name);

                                //Attempt to execute the query
                                Int32 rowsAffected = command.ExecuteNonQuery();
                                if (rowsAffected > 0) {
                                    //Set the user's new ID if new
                                    if (aRole.role_id < 0) {
                                        aRole.role_id = command.LastInsertedId;
                                    }
                                    this.DebugWrite("Role uploaded to database SUCCESSFULY.", 5);
                                }
                                else {
                                    this.ConsoleError("Unable to upload role " + aRole.role_name + " to database.");
                                    return;
                                }
                            }
                            //Delete all current allowed commands
                            using (MySqlCommand command = connection.CreateCommand()) {
                                command.CommandText = @"DELETE FROM `adkats_rolecommands` where `role_id` = " + aRole.role_id;
                                //Attempt to execute the query
                                Int32 rowsAffected = command.ExecuteNonQuery();
                            }
                            foreach (AdKatsCommand aCommand in aRole.allowedCommands.Values) {
                                //Upload the role's allowed commands
                                using (MySqlCommand command = connection.CreateCommand()) {
                                    //Set the insert command structure
                                    command.CommandText = @"
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
	                                    `command_id` = @command_id";
                                    //Set values
                                    command.Parameters.AddWithValue("@role_id", aRole.role_id);
                                    command.Parameters.AddWithValue("@command_id", aCommand.command_id);

                                    //Attempt to execute the query
                                    Int32 rowsAffected = command.ExecuteNonQuery();
                                    if (rowsAffected > 0) {
                                        //Set the user's new ID if new
                                        if (aRole.role_id < 0) {
                                            aRole.role_id = command.LastInsertedId;
                                        }
                                        this.DebugWrite("Role-command uploaded to database SUCCESSFULY.", 5);
                                    }
                                    else {
                                        this.ConsoleError("Unable to upload role-command for " + aRole.role_name + ".");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while uploading role.", e));
            }

            DebugWrite("uploadRole finished!", 6);
        }

        //DONE
        private void UploadBan(AdKatsBan aBan) {
            DebugWrite("uploadBan starting!", 6);

            Boolean success = false;
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            if (aBan == null) {
                this.ConsoleError("Ban invalid in uploadBan.");
            }
            else {
                try {
                    //Upload the inner record if needed
                    if (aBan.ban_record.record_id < 0) {
                        if (!this.UploadRecord(aBan.ban_record)) {
                            return;
                        }
                    }

                    using (MySqlConnection connection = this.GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            command.CommandText = @"
                            INSERT INTO 
                            `" + this._MySqlDatabaseName + @"`.`adkats_bans` 
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
	                            UTC_TIMESTAMP(), 
	                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @ban_durationMinutes MINUTE), 
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
	                            `ban_startTime` = UTC_TIMESTAMP(), 
	                            `ban_endTime` = DATE_ADD(UTC_TIMESTAMP(), INTERVAL @ban_durationMinutes MINUTE), 
	                            `ban_enforceName` = @ban_enforceName, 
	                            `ban_enforceGUID` = @ban_enforceGUID, 
	                            `ban_enforceIP` = @ban_enforceIP, 
	                            `ban_sync` = @ban_sync";

                            command.Parameters.AddWithValue("@player_id", aBan.ban_record.target_player.player_id);
                            command.Parameters.AddWithValue("@latest_record_id", aBan.ban_record.record_id);
                            command.Parameters.AddWithValue("@ban_status", "Active");
                            if (String.IsNullOrEmpty(aBan.ban_notes))
                                aBan.ban_notes = "NoNotes";
                            command.Parameters.AddWithValue("@ban_notes", aBan.ban_notes);
                            command.Parameters.AddWithValue("@ban_enforceName", aBan.ban_enforceName ? ('Y') : ('N'));
                            command.Parameters.AddWithValue("@ban_enforceGUID", aBan.ban_enforceGUID ? ('Y') : ('N'));
                            command.Parameters.AddWithValue("@ban_enforceIP", aBan.ban_enforceIP ? ('Y') : ('N'));
                            command.Parameters.AddWithValue("@ban_sync", "*" + this._ServerID + "*");
                            //Handle permaban case
                            if (aBan.ban_record.command_action.command_key == "player_ban_perm") {
                                aBan.ban_record.command_numeric = (Int32) this._PermaBanEndTime.Subtract(DateTime.UtcNow).TotalMinutes;
                            }
                            command.Parameters.AddWithValue("@ban_durationMinutes", aBan.ban_record.command_numeric);
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() >= 0) {
                                //Rows affected should be > 0
                                this.DebugWrite("Success Uploading Ban on player " + aBan.ban_record.target_player.player_id, 5);
                                success = true;
                            }
                        }
                        if (success) {
                            using (MySqlCommand command = connection.CreateCommand()) {
                                command.CommandText = @"
                                SELECT 
                                    `ban_id`,
                                    `ban_startTime`, 
                                    `ban_endTime` 
                                FROM 
                                    `adkats_bans` 
                                WHERE 
                                    `player_id` = @player_id";

                                command.Parameters.AddWithValue("@player_id", aBan.ban_record.target_player.player_id);
                                //Attempt to execute the query
                                using (MySqlDataReader reader = command.ExecuteReader()) {
                                    //Grab the ban ID
                                    if (reader.Read()) {
                                        aBan.ban_id = reader.GetInt64("ban_id");
                                        aBan.ban_startTime = reader.GetDateTime("ban_startTime");
                                        aBan.ban_endTime = reader.GetDateTime("ban_endTime");

                                        //Update setting page to reflect the ban count
                                        this.UpdateSettingPage();
                                        this.DebugWrite("Ban ID: " + aBan.ban_id, 5);
                                    }
                                    else {
                                        this.ConsoleError("Could not fetch ban information after upload");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e) {
                    this.HandleException(new AdKatsException("Error while uploading new ban.", e));
                }
            }
            DebugWrite("uploadBan finished!", 6);
        }

        private Boolean AssignPlayerRole(AdKats_Player aPlayer) {
            AdKats_Role aRole = null;
            Boolean authorized = false;
            lock (this._UserCache) {
                foreach (AdKatsUser aUser in this._UserCache.Values) {
                    foreach (Int64 playerID in aUser.soldierDictionary.Keys) {
                        if (playerID == aPlayer.player_id) {
                            authorized = true;
                            aRole = this._RoleKeyDictionary[aUser.user_role.role_key];
                        }
                    }
                }
            }
            if (aRole == null) {
                aRole = this._RoleKeyDictionary["guest_default"];
            }
            if (aPlayer.player_role == null) {
                if (authorized) {
                    this.DebugWrite("Player " + aPlayer.player_name + " has been assigned authorized role " + aRole.role_name + ".", 4);
                }
                else {
                    this.DebugWrite("Player " + aPlayer.player_name + " has been assigned the guest role.", 4);
                }
            }
            else {
                if (aPlayer.player_role.role_key != aRole.role_key) {
                    if (authorized) {
                        this.DebugWrite("Role for authorized player " + aPlayer.player_name + " has been CHANGED to " + aRole.role_name + ".", 4);
                        //Tell the player about the access update?
                        this.PlayerSayMessage(aPlayer.player_name, "You have been assigned the authorized role " + aRole.role_name + ".");
                    }
                    else {
                        this.DebugWrite("Player " + aPlayer.player_name + " has been assigned the guest role.", 4);
                        //Tell the player about the access update?
                        this.PlayerSayMessage(aPlayer.player_name, "You have been assigned the guest role.");
                    }
                }
            }
            aPlayer.player_role = aRole;
            return authorized;
        }

        //DONE
        private AdKats_Player FetchPlayer(Boolean requireExisting, Int64 playerID, String playerName, String playerGUID, String playerIP) {
            DebugWrite("fetchPlayer starting!", 6);
            //Create return list
            AdKats_Player aPlayer = null;
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                //If AdKats is disconnected from the database, return the player as-is
                aPlayer = new AdKats_Player {
                                                player_name = playerName,
                                                player_guid = playerGUID
                                            };
                return aPlayer;
            }
            if (playerID < 0 && String.IsNullOrEmpty(playerName) && String.IsNullOrEmpty(playerGUID) && String.IsNullOrEmpty(playerIP)) {
                this.ConsoleError("Attempted to fetch player with no information.");
            }
            else {
                try {
                    using (MySqlConnection connection = this.GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            String sql = @"
                            SELECT 
                                `PlayerID` as `player_id`, 
                                `SoldierName` as `player_name`, 
                                `EAGUID` as `player_guid`, 
                                `PBGUID` as `player_pbguid`, 
                                `IP_Address` as `player_ip` 
                            FROM `" + this._MySqlDatabaseName + @"`.`tbl_playerdata` ";
                            bool sqlEnder = true;
                            if (playerID >= 0) {
                                sql += " WHERE (";
                                sqlEnder = false;
                                sql += " `PlayerID` = " + playerID + "";
                            }
                            if (!String.IsNullOrEmpty(playerGUID)) {
                                if (sqlEnder) {
                                    sql += " WHERE (";
                                    sqlEnder = false;
                                }
                                else {
                                    sql += " OR ";
                                }
                                sql += " `EAGUID` LIKE '" + playerGUID + "'";
                            }
                            if (String.IsNullOrEmpty(playerGUID) && !String.IsNullOrEmpty(playerName)) {
                                if (sqlEnder) {
                                    sql += " WHERE (";
                                    sqlEnder = false;
                                }
                                else {
                                    sql += " OR ";
                                }
                                sql += " `SoldierName` LIKE '" + playerName + "'";
                            }
                            if (String.IsNullOrEmpty(playerGUID) && !String.IsNullOrEmpty(playerIP)) {
                                if (sqlEnder) {
                                    sql += " WHERE (";
                                    sqlEnder = false;
                                }
                                else {
                                    sql += " OR ";
                                }
                                sql += " `IP_Address` LIKE '" + playerIP + "'";
                            }
                            if (this._GameID > 0)
                            {
                                if (sqlEnder)
                                {
                                    sql += " WHERE (";
                                    sqlEnder = false;
                                }
                                else
                                {
                                    sql += " AND ";
                                }
                                sql += " `GameID` = " + this._GameID + "";
                            }
                            if (!sqlEnder) {
                                sql += ")";
                            }
                            command.CommandText = sql;
                            using (MySqlDataReader reader = command.ExecuteReader()) {
                                if (reader.Read()) {
                                    aPlayer = new AdKats_Player();
                                    //Player ID will never be null
                                    aPlayer.player_id = reader.GetInt64("player_id");
                                    if (!reader.IsDBNull(1))
                                        aPlayer.player_name = reader.GetString("player_name");
                                    if (!reader.IsDBNull(2))
                                        aPlayer.player_guid = reader.GetString("player_guid");
                                    if (!reader.IsDBNull(3))
                                        aPlayer.player_pbguid = reader.GetString("player_pbguid");
                                    if (!reader.IsDBNull(4))
                                        aPlayer.player_ip = reader.GetString("player_ip");
                                }
                                else {
                                    this.DebugWrite("No player matching search information.", 5);
                                }
                            }
                        }
                        if (aPlayer == null) {
                            if (requireExisting) {
                                return null;
                            }
                            this.DebugWrite("Adding player to database.", 5);
                            using (MySqlCommand command = connection.CreateCommand()) {
                                //Set the insert command structure
                                command.CommandText = @"
                                INSERT INTO `" + this._MySqlDatabaseName + @"`.`tbl_playerdata` 
                                (
                                    " + ((this._GameID > 0) ? ("`GameID`") : ("")) + @"
                                    " + ((!String.IsNullOrEmpty(playerName)) ? (((this._GameID > 0) ? (",") : ("")) + "`SoldierName`") : ("")) + @"
                                    " + ((!String.IsNullOrEmpty(playerGUID)) ? (((!String.IsNullOrEmpty(playerName)) ? (",") : ("")) + "`EAGUID`") : ("")) + @"
                                    " + ((!String.IsNullOrEmpty(playerIP)) ? (((!String.IsNullOrEmpty(playerGUID)) ? (",") : ("")) + "`IP_Address`") : ("")) + @"
                                ) 
                                VALUES 
                                (
                                    " + ((this._GameID > 0) ? (this._GameID + "") : ("")) + @"
                                    " + ((!String.IsNullOrEmpty(playerName)) ? (((this._GameID > 0) ? (",") : ("")) + "'" + playerName + "'") : ("")) + @"
                                    " + ((!String.IsNullOrEmpty(playerGUID)) ? (((!String.IsNullOrEmpty(playerName)) ? (",") : ("")) + "'" + playerGUID + "'") : ("")) + @"
                                    " + ((!String.IsNullOrEmpty(playerIP)) ? (((!String.IsNullOrEmpty(playerGUID)) ? (",") : ("")) + "'" + playerIP + "'") : ("")) + @"
                                )
                                ON DUPLICATE KEY 
                                UPDATE 
                                    `PlayerID` = LAST_INSERT_ID(`PlayerID`),
                                    `SoldierName` = '" + playerName + @"',
                                    `EAGUID` = '" + playerGUID + @"',
                                    `IP_Address` = '" + playerIP + "'";

                                //Attempt to execute the query
                                if (command.ExecuteNonQuery() > 0) {
                                    //Rows affected should be > 0
                                    aPlayer = new AdKats_Player {
                                                                    player_id = command.LastInsertedId,
                                                                    player_name = playerName,
                                                                    player_guid = playerGUID,
                                                                    player_ip = playerIP
                                                                };
                                }
                                else {
                                    this.ConsoleError("Unable to add player to database.");
                                    return null;
                                }
                            }
                        }
                        if (!String.IsNullOrEmpty(playerName) && !String.IsNullOrEmpty(aPlayer.player_guid) && playerName != aPlayer.player_name) {
                            this.DebugWrite(aPlayer.player_name + " changed their name to " + playerName + ". Updating the database.", 2);
                            using (MySqlCommand command = connection.CreateCommand()) {
                                //Set the insert command structure
                                command.CommandText = @"UPDATE `" + this._MySqlDatabaseName + @"`.`tbl_playerdata` SET `SoldierName` = '" + playerName + "' WHERE `EAGUID` = '" + aPlayer.player_guid + "'";
                                //Attempt to execute the query
                                if (command.ExecuteNonQuery() <= 0) {
                                    this.ConsoleError("Could not update " + aPlayer.player_name + "'s name-change to " + playerName + " in the database.");
                                }
                                //Update the player name in the player object
                                aPlayer.player_name = playerName;
                            }
                        }
                        //Load admin assistat status
                        aPlayer.player_aa = this.IsAdminAssistant(aPlayer);
                        if (aPlayer.player_aa) {
                            this.DebugWrite(aPlayer.player_name + " IS an Admin Assistant.", 3);
                        }
                        else {
                            this.DebugWrite(aPlayer.player_name + " is NOT an Admin Assistant.", 4);
                        }
                        //Assign player role
                        this.AssignPlayerRole(aPlayer);
                    }
                }
                catch (Exception e) {
                    this.HandleException(new AdKatsException("Error while fetching player.", e));
                }
            }
            DebugWrite("fetchPlayer finished!", 6);
            return aPlayer;
        }

        private AdKats_Player UpdatePlayer(AdKats_Player player) {
            this.DebugWrite("updatePlayer starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return player;
            }
            if (player == null || player.player_id < 0 || (String.IsNullOrEmpty(player.player_name) && String.IsNullOrEmpty(player.player_guid) & String.IsNullOrEmpty(player.player_ip))) {
                this.ConsoleError("Attempted to update player without required information.");
            }
            else {
                try {
                    using (MySqlConnection connection = this.GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            //Set the insert command structure
                            command.CommandText = @"
                            UPDATE `" + this._MySqlDatabaseName + @"`.`tbl_playerdata` SET 
                                `SoldierName` = '" + player.player_name + @"',
                                `EAGUID` = '" + player.player_guid + @"',
                                `IP_Address` = '" + player.player_ip + @"'
                            WHERE `tbl_playerdata`.`PlayerID` = " + player.player_id;
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() > 0) {
                                this.DebugWrite("Update player info success.", 5);
                            }
                        }
                    }
                }
                catch (Exception e) {
                    this.HandleException(new AdKatsException("Error while updating player.", e));
                }
            }
            DebugWrite("updatePlayer finished!", 6);
            return player;
        }

        //DONE
        private AdKatsBan FetchPlayerBan(AdKats_Player player) {
            DebugWrite("fetchPlayerBan starting!", 6);

            AdKatsBan aBan = null;
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return null;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
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
                            `adkats_bans`.`ban_status` = 'Active' 
                        AND 
                        (";
                        Boolean started = false;
                        if (!String.IsNullOrEmpty(player.player_name)) {
                            started = true;
                            query += "(`tbl_playerdata`.`SoldierName` = '" + player.player_name + @"' AND `adkats_bans`.`ban_enforceName` = 'Y')";
                        }
                        if (!String.IsNullOrEmpty(player.player_guid)) {
                            if (started) {
                                query += " OR ";
                            }
                            started = true;
                            query += "(`tbl_playerdata`.`EAGUID` = '" + player.player_guid + "' AND `adkats_bans`.`ban_enforceGUID` = 'Y')";
                        }
                        if (!String.IsNullOrEmpty(player.player_ip)) {
                            if (started) {
                                query += " OR ";
                            }
                            started = true;
                            query += "(`tbl_playerdata`.`IP_Address` = '" + player.player_ip + "' AND `adkats_bans`.`ban_enforceIP` = 'Y')";
                        }
                        if (!started) {
                            this.HandleException(new AdKatsException("No data to fetch ban with. This should never happen."));
                            return null;
                        }
                        query += ")";

                        //Assign the query
                        command.CommandText = query;

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            Boolean fetchedFirstBan = false;
                            if (reader.Read()) {
                                fetchedFirstBan = true;
                                //Create the ban element
                                aBan = new AdKatsBan {
                                                         ban_id = reader.GetInt64("ban_id"),
                                                         ban_status = reader.GetString("ban_status"),
                                                         ban_notes = reader.GetString("ban_notes"),
                                                         ban_sync = reader.GetString("ban_sync"),
                                                         ban_startTime = reader.GetDateTime("ban_startTime"),
                                                         ban_endTime = reader.GetDateTime("ban_endTime"),
                                                         ban_enforceName = (reader.GetString("ban_enforceName") == "Y"),
                                                         ban_enforceGUID = (reader.GetString("ban_enforceGUID") == "Y"),
                                                         ban_enforceIP = (reader.GetString("ban_enforceIP") == "Y"),
                                                         ban_record = this.FetchRecordByID(reader.GetInt64("latest_record_id"), false)
                                                     };

                                //Get the record information
                            }
                            if (reader.Read() && fetchedFirstBan) {
                                this.ConsoleWarn("Multiple banned players matched ban information, possible duplicate account");
                            }
                        }
                    }
                    //If bans were fetched successfully, update the ban lists and sync back
                    if (aBan != null) {
                        Int64 totalSeconds = (long) aBan.ban_endTime.Subtract(DateTime.UtcNow).TotalSeconds;
                        if (totalSeconds < 0) {
                            aBan.ban_status = "Expired";
                            //Update the sync for this ban
                            this.UpdateBanStatus(aBan);
                            return null;
                        }
                        //Update the sync for this ban
                        this.UpdateBanStatus(aBan);
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching player ban.", e));
            }
            return aBan;
        }

        //DONE
        private void RepopulateProconBanList() {
            DebugWrite("repopulateProconBanList starting!", 6);
            this.ConsoleWarn("Downloading bans from database, please wait. This might take several minutes depending on your ban count!");

            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            Double totalBans = 0;
            Double bansRepopulated = 0;
            Boolean earlyExit = false;
            DateTime startTime = DateTime.UtcNow;

            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            COUNT(*) AS `ban_count`
                        FROM 
	                        `adkats_bans`";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                totalBans = reader.GetInt64("ban_count");
                            }
                        }
                    }
                    if (totalBans < 1) {
                        return;
                    }
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
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
	                        `adkats_bans`";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            //Loop through all incoming bans
                            while (reader.Read()) {
                                //Break from the loop if the plugin is disabled or the setting is reverted.
                                if (!this._IsEnabled || this._UseBanEnforcer) {
                                    this.ConsoleWarn("You exited the ban download process early, the process was not completed.");
                                    earlyExit = true;
                                    break;
                                }

                                //Bans have been found

                                //Create the ban element
                                AdKatsBan aBan = new AdKatsBan {
                                                                   ban_id = reader.GetInt64("ban_id"),
                                                                   ban_status = reader.GetString("ban_status"),
                                                                   ban_notes = reader.GetString("ban_notes"),
                                                                   ban_sync = reader.GetString("ban_sync"),
                                                                   ban_startTime = reader.GetDateTime("ban_startTime"),
                                                                   ban_endTime = reader.GetDateTime("ban_endTime"),
                                                                   ban_record = this.FetchRecordByID(reader.GetInt64("latest_record_id"), false)
                                                               };

                                //Get the record information

                                Int64 totalBanSeconds = (long) aBan.ban_endTime.Subtract(DateTime.UtcNow).TotalSeconds;
                                if (totalBanSeconds > 0) {
                                    this.DebugWrite("Re-ProconBanning: " + aBan.ban_record.target_player.player_name + " for " + totalBanSeconds + "sec for " + aBan.ban_record.record_message, 4);

                                    //Push the name ban
                                    if (reader.GetString("ban_enforceName") == "Y") {
                                        Thread.Sleep(75);
                                        //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                        if (totalBanSeconds > 0 && totalBanSeconds < 31536000) {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "name", aBan.ban_record.target_player.player_name, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                        }
                                        else {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "name", aBan.ban_record.target_player.player_name, "perm", aBan.ban_record.record_message);
                                        }
                                    }

                                    //Push the guid ban
                                    if (reader.GetString("ban_enforceGUID") == "Y") {
                                        Thread.Sleep(75);
                                        //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                        if (totalBanSeconds > 0 && totalBanSeconds < 31536000) {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "guid", aBan.ban_record.target_player.player_guid, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                        }
                                        else {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "guid", aBan.ban_record.target_player.player_guid, "perm", aBan.ban_record.record_message);
                                        }
                                    }

                                    //Push the IP ban
                                    if (reader.GetString("ban_enforceIP") == "Y") {
                                        Thread.Sleep(75);
                                        //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                        if (totalBanSeconds > 0 && totalBanSeconds < 31536000) {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "ip", aBan.ban_record.target_player.player_ip, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                        }
                                        else {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "ip", aBan.ban_record.target_player.player_ip, "perm", aBan.ban_record.record_message);
                                        }
                                    }
                                }

                                if (++bansRepopulated % 15 == 0) {
                                    this.ConsoleWrite(Math.Round(100 * bansRepopulated / totalBans, 2) + "% of bans repopulated. AVG " + Math.Round(bansRepopulated / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " downloads/sec.");
                                }
                            }
                            this.ExecuteCommand("procon.protected.send", "banList.save");
                            this.ExecuteCommand("procon.protected.send", "banList.list");
                            if (!earlyExit) {
                                this.ConsoleSuccess("All AdKats Enforced bans repopulated to procon's ban list.");
                            }

                            //Update the last db ban fetch time
                            this._LastDbBanFetch = DateTime.UtcNow;
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while repopulating procon banlist.", e));
            }
        }

        //DONE
        private Boolean UpdateBanStatus(AdKatsBan aBan) {
            DebugWrite("updateBanStatus starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return false;
            }

            Boolean success = false;
            if (aBan == null) {
                this.ConsoleError("Ban invalid in updateBanStatus.");
            }
            else {
                try {
                    //Conditionally modify the ban_sync for this server
                    if (!aBan.ban_sync.Contains("*" + this._ServerID + "*")) {
                        aBan.ban_sync += ("*" + this._ServerID + "*");
                    }

                    using (MySqlConnection connection = this.GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            String query = @"
                            UPDATE 
                            `" + this._MySqlDatabaseName + @"`.`adkats_bans` 
                            SET 
                            `ban_sync` = '" + aBan.ban_sync + @"', 
                            `ban_status` = '" + aBan.ban_status + @"'
                            WHERE 
                            `ban_id` = " + aBan.ban_id;
                            command.CommandText = query;
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() > 0) {
                                success = true;
                            }
                        }
                    }
                }
                catch (Exception e) {
                    this.HandleException(new AdKatsException("Error while updating status of ban.", e));
                }
            }

            DebugWrite("updateBanStatus finished!", 6);
            return success;
        }

        //DONE
        private void ImportBansFromBBM5108() {
            //Check if tables exist from BF3 Ban Manager
            if (!this.ConfirmTable("bm_banlist")) {
                return;
            }
            this.ConsoleWarn("BF3 Ban Manager tables detected. Checking validity....");

            //Check if any BBM5108 bans exist in the AdKats Banlist
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            * 
                        FROM 
                            `" + this._MySqlDatabaseName + @"`.`adkats_bans` 
                        WHERE 
                            `adkats_bans`.`ban_notes` = 'BBM5108' 
                        LIMIT 1";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                this.ConsoleWarn("BF3 Ban Manager bans already imported, canceling import.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while checking for BBM Bans.", e));
                return;
            }

            this.ConsoleWarn("Validity confirmed. Preparing to fetch all BF3 Ban Manager Bans...");
            Double totalBans = 0;
            Double bansImported = 0;
            Queue<BBM5108Ban> inboundBBMBans = new Queue<BBM5108Ban>();
            DateTime startTime = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        this.DebugWrite("Creating query to import BBM5108", 3);
                        command.CommandText = @"
                        SELECT 
                            soldiername, eaguid, ban_length, ban_duration, ban_reason 
                        FROM 
                            bm_banlist 
                        INNER JOIN 
                            bm_soldiers 
                        ON 
                            bm_banlist.soldierID = bm_soldiers.soldierID";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            Boolean told = false;
                            while (reader.Read()) {
                                if (!told) {
                                    this.DebugWrite("BBM5108 bans found, grabbing.", 3);
                                    told = true;
                                }
                                BBM5108Ban bbmBan = new BBM5108Ban {
                                                                       soldiername = reader.IsDBNull(reader.GetOrdinal("soldiername")) ? null : reader.GetString("soldiername"),
                                                                       eaguid = reader.IsDBNull(reader.GetOrdinal("eaguid")) ? null : reader.GetString("eaguid"),
                                                                       ban_length = reader.GetString("ban_length"),
                                                                       ban_duration = reader.GetDateTime("ban_duration"),
                                                                       ban_reason = reader.IsDBNull(reader.GetOrdinal("ban_reason")) ? null : reader.GetString("ban_reason")
                                                                   };
                                inboundBBMBans.Enqueue(bbmBan);
                                totalBans++;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching BBM Bans.", e));
                return;
            }
            this.ConsoleWarn(totalBans + " Ban Manager bans fetched, starting import to AdKats Ban Enforcer...");

            try {
                //Loop through all BBMBans in order that they came in
                while (inboundBBMBans.Count > 0) {
                    //Break from the loop if the plugin is disabled or the setting is reverted.
                    if (!this._IsEnabled || !this._UseBanEnforcer) {
                        this.ConsoleError("You exited the ban import process process early, the process was not completed and cannot recover without manual override. Talk to ColColonCleaner.");
                        break;
                    }

                    BBM5108Ban bbmBan = inboundBBMBans.Dequeue();

                    //Create the record
                    AdKatsRecord record = new AdKatsRecord();
                    //Fetch the player
                    record.target_player = this.FetchPlayer(false, -1, bbmBan.soldiername, bbmBan.eaguid, null);

                    record.record_source = AdKatsRecord.Sources.Automated;
                    if (bbmBan.ban_length == "permanent") {
                        this.DebugWrite("Ban is permanent", 4);
                        record.command_type = this._CommandKeyDictionary["player_ban_perm"];
                        record.command_action = this._CommandKeyDictionary["player_ban_perm"];
                        record.command_numeric = 0;
                    }
                    else if (bbmBan.ban_length == "seconds") {
                        this.DebugWrite("Ban is temporary", 4);
                        record.command_type = this._CommandKeyDictionary["player_ban_temp"];
                        record.command_action = this._CommandKeyDictionary["player_ban_temp"];
                        record.command_numeric = (Int32) (bbmBan.ban_duration - DateTime.UtcNow).TotalMinutes;
                    }
                    else {
                        //Ignore all other cases e.g. round bans
                        this.DebugWrite("Ban type '" + bbmBan.ban_length + "' not usable", 3);
                        continue;
                    }

                    record.source_name = "BanEnforcer";
                    record.server_id = this._ServerID;
                    if (!String.IsNullOrEmpty(record.target_player.player_name)) {
                        record.target_name = record.target_player.player_name;
                    }
                    record.isIRO = false;
                    record.record_message = bbmBan.ban_reason;

                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                    //Create the ban
                    AdKatsBan aBan = new AdKatsBan {
                                                       ban_record = record,
                                                       ban_notes = "BBM5108",
                                                       ban_enforceName = nameAvailable && (this._DefaultEnforceName || (!guidAvailable && !ipAvailable) || !String.IsNullOrEmpty(bbmBan.soldiername)),
                                                       ban_enforceGUID = guidAvailable && (this._DefaultEnforceGUID || (!nameAvailable && !ipAvailable) || !String.IsNullOrEmpty(bbmBan.eaguid)),
                                                       ban_enforceIP = ipAvailable && this._DefaultEnforceIP
                                                   };
                    if (!aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP) {
                        this.ConsoleError("Unable to create ban, no proper player information");
                        continue;
                    }

                    //Upload the ban
                    this.DebugWrite("Uploading Ban Manager ban.", 5);
                    this.UploadBan(aBan);

                    if (++bansImported % 25 == 0) {
                        this.ConsoleWrite(Math.Round(100 * bansImported / totalBans, 2) + "% of Ban Manager bans uploaded. AVG " + Math.Round(bansImported / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " uploads/sec.");
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while processing imported BBM Bans to AdKats banlist.", e));
                return;
            }
            if (inboundBBMBans.Count == 0) {
                this.ConsoleSuccess("All Ban Manager bans imported into AdKats Ban Enforcer!");
            }
        }

        //Done
        private Boolean CanPunish(AdKatsRecord record) {
            DebugWrite("canPunish starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return false;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            `record_time` AS `latest_time` 
                        FROM 
                            `adkats_records_main` 
                        WHERE 
                            `adkats_records_main`.`command_type` = 'Punish' 
                        AND 
                            `adkats_records_main`.`target_id` = " + record.target_player.player_id + @" 
                        AND 
                            DATE_ADD(`record_time`, INTERVAL 20 SECOND) > UTC_TIMESTAMP() 
                        ORDER BY 
                            `record_time` 
                        DESC LIMIT 1";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                this.DebugWrite("can't upload punish", 6);
                                this.SendMessageToSource(record, record.target_name + " already punished in the last 20 seconds.");
                                return false;
                            }
                            return true;
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while checking if player can be punished.", e));
                //Assume false if any errors
                return false;
            }
        }

        //DONE
        private Boolean FetchIROStatus(AdKatsRecord record) {
            DebugWrite("FetchIROStatus starting!", 6);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return false;
            }

            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            `record_time` AS `latest_time` 
                        FROM 
                            `adkats_records_main` 
                        WHERE 
                            `adkats_records_main`.`command_type` = 'Punish' 
                        AND 
                            `adkats_records_main`.`target_id` = " + record.target_player.player_id + @" 
                        AND 
                            DATE_ADD(`record_time`, INTERVAL " + this._IROTimeout + @" MINUTE) > UTC_TIMESTAMP() 
                        ORDER BY 
                            `record_time` 
                        DESC LIMIT 1";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                this.DebugWrite("Punish is Double counted", 6);
                                return true;
                            }
                            return false;
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while checking if punish will be IRO.", e));
                //Assume false if any errors
                return false;
            }
        }

        //DONE
        private void RunActionsFromDB() {
            DebugWrite("runActionsFromDB starting!", 7);
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            try {
                foreach (AdKatsRecord record in this.FetchUnreadRecords()) {
                    this.QueueRecordForActionHandling(record);
                }
                //Update the last time this was fetched
                this._LastDbActionFetch = DateTime.UtcNow;
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while queueing unread records for action handling.", e));
            }
        }

        //DONE
        private Int32 FetchPoints(AdKats_Player player) {
            DebugWrite("fetchPoints starting!", 6);

            Int32 returnVal = -1;
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return returnVal;
            }

            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        if (this._CombineServerPunishments) {
                            command.CommandText = @"SELECT `total_points` FROM `" + this._MySqlDatabaseName + @"`.`adkats_infractions_global` WHERE `player_id` = @player_id";
                            command.Parameters.AddWithValue("@player_id", player.player_id);
                        }
                        else {
                            command.CommandText = @"SELECT `total_points` FROM `" + this._MySqlDatabaseName + @"`.`adkats_infractions_server` WHERE `player_id` = @player_id and `server_id` = @server_id";
                            command.Parameters.AddWithValue("@player_id", player.player_id);
                            command.Parameters.AddWithValue("@server_id", this._ServerID);
                        }
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            returnVal = reader.Read() ? reader.GetInt32("total_points") : 0;
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while getting infraction points for player.", e));
            }
            DebugWrite("fetchPoints finished!", 6);
            return returnVal;
        }

        //IN Progress
        private void FetchUserList() {
            DebugWrite("fetchUserList starting!", 6);

            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return;
            }
            //Make sure roles and commands are loaded before performing this
            if (this._CommandNameDictionary.Count == 0 || this._RoleKeyDictionary.Count == 0) {
                this.FetchCommands();
                this.FetchRoles();
                return;
            }
            Dictionary<long, AdKatsUser> tempUserCache = new Dictionary<long, AdKatsUser>();
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"SELECT 
	                        `adkats_users`.`user_id`,
	                        `adkats_users`.`user_name`,
	                        `adkats_users`.`user_email`,
	                        `adkats_users`.`user_phone`,
	                        `adkats_users`.`user_role`
                        FROM 
	                        `adkats_users`";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                //Create the user object
                                AdKatsUser user = new AdKatsUser();
                                user.user_id = reader.GetInt32("user_id"); //0
                                user.user_name = reader.GetString("user_name"); //1
                                if (!reader.IsDBNull(2))
                                    user.user_email = reader.GetString("user_email"); //2
                                if (!reader.IsDBNull(3))
                                    user.user_phone = reader.GetString("user_phone"); //3
                                if (!this._RoleIDDictionary.TryGetValue(reader.GetInt32("user_role"), out user.user_role)) {
                                    this.ConsoleError("Unable to find user role for role_id " + reader.GetInt32("user_role"));
                                    return;
                                }
                                //Add the user to temp list
                                tempUserCache.Add(user.user_id, user);
                            }
                        }
                    }
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
	                        `adkats_users`.`user_id`,
	                        `adkats_usersoldiers`.`player_id`,
	                        `tbl_playerdata`.`GameID` AS `game_id`,
	                        `tbl_playerdata`.`ClanTag` AS `clan_tag`,
	                        `tbl_playerdata`.`SoldierName` AS `player_name`,
	                        `tbl_playerdata`.`EAGUID` AS `player_guid`,
	                        `tbl_playerdata`.`IP_Address` AS `player_ip`
                        FROM 
	                        `adkats_users`
                        INNER JOIN
	                        `adkats_usersoldiers`
                        ON 
	                        `adkats_users`.`user_id` = `adkats_usersoldiers`.`user_id`
                        INNER JOIN
	                        `tbl_playerdata`
                        ON
	                        `adkats_usersoldiers`.`player_id` = `tbl_playerdata`.`PlayerID`
                        ORDER BY 
                            `user_id`
                        ASC";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                //Create the new player object
                                AdKats_Player aPlayer = new AdKats_Player();

                                //Import the information
                                Int32 userID = reader.GetInt32("user_id"); //0
                                aPlayer.player_id = reader.GetInt32("player_id"); //1
                                aPlayer.game_id = reader.GetInt32("game_id"); //2
                                if (!reader.IsDBNull(3))
                                    aPlayer.clan_tag = reader.GetString("clan_tag"); //3
                                if (!reader.IsDBNull(4))
                                    aPlayer.player_name = reader.GetString("player_name"); //4
                                if (!reader.IsDBNull(5))
                                    aPlayer.player_guid = reader.GetString("player_guid"); //5
                                if (!reader.IsDBNull(6))
                                    aPlayer.player_ip = reader.GetString("player_ip"); //6

                                //Add soldier to user
                                AdKatsUser aUser = null;
                                if (tempUserCache.TryGetValue(userID, out aUser)) {
                                    if (aUser.soldierDictionary.ContainsKey(aPlayer.player_id)) {
                                        aUser.soldierDictionary.Remove(aPlayer.player_id);
                                    }
                                    aUser.soldierDictionary.Add(aPlayer.player_id, aPlayer);
                                }
                                else {
                                    this.ConsoleError("Unable to add soldier " + aPlayer.player_name + " to user " + userID + " when fetching user list.");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching access list.", e));
            }

            //Update the user cache
            this._UserCache = tempUserCache;

            //Update all currently online players
            lock (this._PlayersMutex) {
                foreach (AdKats_Player aPlayer in this._PlayerDictionary.Values) {
                    this.AssignPlayerRole(aPlayer);
                }
            }

            //Update the last update time
            this._LastUserFetch = DateTime.UtcNow;
            if (this._UserCache.Count > 0) {
                this.DebugWrite("User List Fetched from Database. User Count: " + this._UserCache.Count, 1);
                //Update MULTIBalancer Whitelists
                this.UpdateMULTIBalancerWhitelist();
                //Update Server Reserved Slots
                this.UpdateReservedSlots();
            }
            else {
                this.ConsoleWarn("No users in the user table. Add a new user with 'Add User'.");
            }

            DebugWrite("fetchUserList finished!", 6);
        }

        //DONE
        private Boolean IsAdminAssistant(AdKats_Player player) {
            DebugWrite("fetchAdminAssistants starting!", 6);

            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return false;
            }
            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT
                            'isAdminAssistant' 
                        FROM 
                            `adkats_records_main`
                        WHERE (
	                        SELECT count(`command_action`) 
	                        FROM `adkats_records_main` 
	                        WHERE `command_action` = 'ConfirmReport' 
	                        AND `source_name` = '" + player.player_name + @"' 
	                        AND (`adkats_records_main`.`record_time` BETWEEN date_sub(UTC_TIMESTAMP(),INTERVAL 30 DAY) AND UTC_TIMESTAMP())
                        ) >= " + this._MinimumRequiredMonthlyReports + " LIMIT 1";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            return reader.Read();
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while checking if player is an admin assistant.", e));
            }
            DebugWrite("fetchAdminAssistants finished!", 6);
            return false;
        }

        //DONE
        private Int64 FetchServerID() {
            DebugWrite("fetchServerID starting!", 6);

            Int64 returnVal = -1;
            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return returnVal;
            }

            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"SELECT `ServerID` as `server_id` FROM `tbl_server` WHERE IP_Address = @IP_Address";
                        command.Parameters.AddWithValue("@IP_Address", this._ServerIP);
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                returnVal = reader.GetInt64("server_id");
                                if (this._ServerID != -1) {
                                    this.DebugWrite("Attempted server ID update after ID already chosen.", 5);
                                }
                                else {
                                    this._ServerID = returnVal;
                                    this._SettingImportID = this._ServerID;
                                    this.DebugWrite("Server ID fetched: " + this._ServerID, 1);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while fetching server ID from database.", e));
            }

            DebugWrite("fetchServerID finished!", 6);

            return returnVal;
        }

        private Boolean HandlePossibleDisconnect() {
            Boolean returnVal = false;
            //Make sure database connection active
            if (this._DatabaseConnectionCriticalState || !this.DebugDatabaseConnectionActive()) {
                if (!this._DatabaseConnectionCriticalState) {
                    this.HandleDatabaseConnectionInteruption();
                }
                returnVal = true;
            }
            return returnVal;
        }

        private Boolean DebugDatabaseConnectionActive() {
            DebugWrite("databaseConnectionActive starting!", 8);

            Boolean active = false;

            try {
                using (MySqlConnection connection = this.GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"SELECT UTC_TIMESTAMP() AS `current_time`";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                active = true;
                            }
                        }
                    }
                }
            }
            catch (Exception) {
                //there was an error, database connection not established
            }
            DebugWrite("databaseConnectionActive finished!", 8);
            return active;
        }

        #endregion

        #region MULTIBalancer Whitelisting

        private void UpdateMULTIBalancerWhitelist() {
            try {
                if (this._FeedMultiBalancerWhitelist) {
                    List<String> accessMoveme = new List<String>();
                    //Loop over the user list
                    lock (this._UserCache) {
                        foreach (AdKatsUser user in this._UserCache.Values) {
                            lock (user) {
                                lock (user.user_role) {
                                    lock (user.user_role.allowedCommands) {
                                        //Check each user's allowed commands
                                        foreach (AdKatsCommand command in user.user_role.allowedCommands.Values) {
                                            //If the teamswap command is allowed add each of the user's soldiers to the whitelist
                                            if (command.command_key == "self_teamswap") {
                                                foreach (AdKats_Player soldier in user.soldierDictionary.Values) {
                                                    accessMoveme.Add(soldier.player_name);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    this.SetExternalPluginSetting("MULTIbalancer", "1 - Settings|Whitelist", CPluginVariable.EncodeStringArray(accessMoveme.ToArray()));
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while updating MULTIBalancer whitelist.", e));
            }
        }

        #endregion

        #region Server Reserved Slots

        private void UpdateReservedSlots() {
            try {
                if (this._FeedServerReservedSlots && this._UserCache.Count > 0 && this._CurrentReservedSlotPlayers != null) {
                    this.DebugWrite("Checking validity of reserve slotted players.", 6);

                    //Loop over the user list and grab all current soldiers
                    List<String> allUserSoldiers = new List<String>();
                    foreach (AdKatsUser user in this._UserCache.Values) {
                        foreach (AdKats_Player soldier in user.soldierDictionary.Values) {
                            allUserSoldiers.Add(soldier.player_name);
                            //Add needed soldiers to the reserved slot list
                            if (!this._CurrentReservedSlotPlayers.Contains(soldier.player_name)) {
                                this.DebugWrite(soldier.player_name + " in AdKats access, but not in reserved slots. Adding.", 3);
                                this.ExecuteCommand("procon.protected.send", "reservedSlotsList.add", soldier.player_name);
                                Thread.Sleep(50);
                            }
                        }
                    }

                    //Remove soldiers from the list where needed
                    foreach (String playerName in this._CurrentReservedSlotPlayers) {
                        if (!allUserSoldiers.Contains(playerName)) {
                            this.DebugWrite(playerName + " in reserved slots, but not AdKats Access List. Removing.", 3);
                            this.ExecuteCommand("procon.protected.send", "reservedSlotsList.remove", playerName);
                            Thread.Sleep(50);
                        }
                    }

                    //Save the list
                    this.ExecuteCommand("procon.protected.send", "reservedSlotsList.save");
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while updating server reserved slots.", e));
            }
        }

        public override void OnReservedSlotsList(List<String> soldierNames) {
            try {
                this.DebugWrite("Reserved slots listed.", 5);
                this._CurrentReservedSlotPlayers = soldierNames;
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while handling reserved slot list.", e));
            }
        }

        #endregion

        #endregion

        #region External Commands

        public void PerformExternalPluginCommand(params String[] commandParams) {
            this.DebugWrite("PerformExternalPluginCommand starting!", 6);
            if (commandParams.Length < 1) {
                this.ConsoleError("External action handling canceled. No parameters were provided.");
                return;
            }

            new Thread(new ParameterizedThreadStart(ParseExternalPluginCommand)).Start(commandParams[0]);
            this.DebugWrite("PerformExternalPluginCommand finished!", 6);
        }

        private void ParseExternalPluginCommand(Object clientInformation) {
            this.DebugWrite("ParseExternalPluginCommand starting!", 6);
            try {
                //Set current thread name
                Thread.CurrentThread.Name = "ParseExternalPluginCommand";

                //Parse Command Information into a record
                Hashtable parsedClientInformation = (Hashtable) JSON.JsonDecode((String) clientInformation);
                AdKatsCommand commandType = null;
                Int32 commandNumeric = 0;
                String sourceName = String.Empty;
                String targetName = String.Empty;
                String recordMessage = String.Empty;
                //Import the command type
                if (!parsedClientInformation.ContainsKey("command_type")) {
                    this.ConsoleError("Parsed command didn't contain a command_type!");
                    return;
                }
                String unparsedCommandType = (String) parsedClientInformation["command_type"];
                if (String.IsNullOrEmpty(unparsedCommandType)) {
                    this.ConsoleError("command_type was empty. Unable to parse external command.");
                    return;
                }
                if (!this._CommandKeyDictionary.TryGetValue(unparsedCommandType, out commandType)) {
                    this.ConsoleError("command_type was invalid, command not found in definition.");
                    return;
                }
                //Import the command numeric
                if (!parsedClientInformation.ContainsKey("command_numeric")) {
                    //Only required for temp ban
                    if (commandType.command_key == "player_ban_temp") {
                        this.ConsoleError("Parsed command didn't contain a command_numeric! Unable to parse command.");
                        return;
                    }
                }
                else {
                    commandNumeric = (Int32) parsedClientInformation["command_numeric"];
                }
                //Import the source name
                if (!parsedClientInformation.ContainsKey("source_name")) {
                    this.ConsoleError("Parsed command didn't contain a source_name!");
                    return;
                }
                if (String.IsNullOrEmpty(sourceName)) {
                    this.ConsoleError("source_name was empty. Unable to parse external command.");
                    return;
                }
                sourceName = (String) parsedClientInformation["source_name"];
                //Import the target name
                if (!parsedClientInformation.ContainsKey("target_name")) {
                    this.ConsoleError("Parsed command didn't contain a target_name!");
                    return;
                }
                if (String.IsNullOrEmpty(targetName)) {
                    this.ConsoleError("source_name was empty. Unable to parse external command.");
                    return;
                }
                targetName = (String) parsedClientInformation["target_name"];
                //Import the record message
                if (!parsedClientInformation.ContainsKey("record_message")) {
                    this.ConsoleError("Parsed command didn't contain a record_message!");
                    return;
                }
                if (String.IsNullOrEmpty(recordMessage)) {
                    this.ConsoleError("record_message was empty. Unable to parse external command.");
                    return;
                }
                recordMessage = (String) parsedClientInformation["record_message"];

                //Create the record
                AdKatsRecord record = new AdKatsRecord {
                                                           record_source = AdKatsRecord.Sources.External,
                                                           command_type = commandType,
                                                           command_numeric = commandNumeric,
                                                           source_name = sourceName,
                                                           target_name = targetName,
                                                           record_message = recordMessage
                                                       };

                //Queue the record for processing
                this.QueueRecordForProcessing(record);
            }
            catch (Exception e) {
                //Log the error in console
                this.HandleException(new AdKatsException("Unable to process external command.", e));
            }
            this.DebugWrite("ParseExternalPluginCommand finished!", 6);
        }

        #endregion

        #region HTTP Server Handling

        public override HttpWebServerResponseData OnHttpRequest(HttpWebServerRequestData data) {
            String responseString = "AdKats Remote: ";
            try {
                /*foreach (String key in data.POSTData.AllKeys)
                {
                    this.DebugWrite("POST Key: " + key + " val: " + data.Headers[key], 6);
                }*/
                foreach (String key in data.Query.AllKeys) {
                    this.DebugWrite("Query Key: " + key + " val: " + data.Query[key], 6);
                }
                this.DebugWrite("method: " + data.Method, 6);
                //this.DebugWrite("doc: " + data.Document, 6);
                AdKatsRecord record = new AdKatsRecord();
                record.record_source = AdKatsRecord.Sources.HTTP;

                NameValueCollection dataCollection = null;
                if (System.String.Compare(data.Method, "GET", System.StringComparison.OrdinalIgnoreCase) == 0) {
                    dataCollection = data.Query;
                }
                else if (System.String.Compare(data.Method, "POST", System.StringComparison.OrdinalIgnoreCase) == 0) {
                    return null; //dataCollection = data.POSTData;
                }
                if (dataCollection != null) {
                    String commandString = dataCollection["command_type"];
                    record.command_type = this._CommandKeyDictionary[commandString];

                    if (dataCollection["access_key"] != null && dataCollection["access_key"] == this._ExternalCommandAccessKey) {
                        //If command not parsable, return without creating
                        if (record.command_type != null) {
                            //Set the command action
                            record.command_action = record.command_type;

                            //Set the source
                            String sourceName = dataCollection["source_name"];
                            record.source_name = !String.IsNullOrEmpty(sourceName) ? sourceName : "HTTPAdmin";

                            String duration = dataCollection["record_durationMinutes"];
                            record.command_numeric = !string.IsNullOrEmpty(duration) ? Int32.Parse(duration) : 0;

                            String message = dataCollection["record_message"];
                            if (!String.IsNullOrEmpty(message)) {
                                if (message.Length >= this._RequiredReasonLength) {
                                    record.record_message = message;

                                    //Check the target
                                    String targetName = dataCollection["target_name"];
                                    //Check for an exact match
                                    if (!String.IsNullOrEmpty(targetName)) {
                                        record.target_name = targetName;
                                        responseString += this.CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        responseString += "target_name cannot be null";
                                    }
                                }
                                else {
                                    responseString += "Reason too short. Needs to be at least " + this._RequiredReasonLength + " chars.";
                                }
                            }
                            else {
                                responseString += "record_message cannot be null.";
                            }
                        }
                        else {
                            responseString += "Command '" + commandString + "' Not Parsable. Check AdKats doc for valid DB commands.";
                        }
                    }
                    else {
                        responseString += "access_key either not given or incorrect.";
                    }
                }
            }
            catch (Exception e) {
                responseString += e.ToString();
            }
            return new HttpWebServerResponseData(responseString);
        }

        #endregion

        #region Mailing Functions

        public class EmailHandler {
            public AdKats Plugin;
            public String SenderEmail = "adkatsbattlefield@gmail.com";
            public String SMTPServer = "smtp.gmail.com";
            public Int32 SMTPPort = 587;
            public String SMTPUser = "adkatsbattlefield@gmail.com";
            public String SMTPPassword = "paqwjboqkbfywapu";
            public Boolean UseSSL = true;

            public EmailHandler(AdKats plugin) {
                this.Plugin = plugin;
            }

            public void SendReport(AdKatsRecord record) {
                try {
                    //Create a new thread to handle keep-alive
                    //This thread will remain running for the duration the layer is online
                    Thread emailSendingThread = new Thread(new ThreadStart(delegate {
                        try {
                            String subject = String.Empty;
                            String body = String.Empty;

                            StringBuilder sb = new StringBuilder();
                            if (String.IsNullOrEmpty(Plugin._ServerName)) {
                                //Unable to send report email, server name unknown
                                return;
                            }
                            subject = record.target_name + " reported in [" + Plugin._GameVersion.ToString() + "] " + Plugin._ServerName;
                            sb.Append("<h1>AdKats " + Plugin._GameVersion.ToString() + " Player Report [" + record.command_numeric + "]</h1>");
                            sb.Append("<h2>" + Plugin._ServerName + "</h2>");
                            sb.Append("<h3>" + DateTime.Now + " ProCon Time</h3>");
                            sb.Append("<h3>" + record.source_name + " has reported " + record.target_name + " for " + record.record_message + "</h3>");
                            sb.Append("<p>");
                            CPlayerInfo playerInfo = record.target_player.frostbitePlayerInfo;
                            //sb.Append("<h4>Current Information on " + record.target_name + ":</h4>");
                            int numReports = Plugin._RoundReports.Values.Count(aRecord => aRecord.target_name == record.target_name);
                            sb.Append("Reported " + numReports + " times during the current round.<br/>");
                            sb.Append("Has " + Plugin.FetchPoints(record.target_player) + " infraction points on your servers.<br/>");
                            sb.Append("Score: " + playerInfo.Score + "<br/>");
                            sb.Append("Kills: " + playerInfo.Kills + "<br/>");
                            sb.Append("Deaths: " + playerInfo.Deaths + "<br/>");
                            sb.Append("Kdr: " + playerInfo.Kdr + "<br/>");
                            sb.Append("Ping: " + playerInfo.Ping + "<br/>");
                            sb.Append("</p>");
                            sb.Append("<p>");
                            sb.Append("SoldierName: " + playerInfo.SoldierName + "<br/>");
                            sb.Append("EA GUID: " + playerInfo.GUID + "<br/>");
                            if (record.target_player.PBPlayerInfo != null) {
                                sb.Append("PB GUID: " + record.target_player.PBPlayerInfo.GUID + "<br/>");
                                sb.Append("IP: " + record.target_player.PBPlayerInfo.Ip.Split(':')[0] + "<br/>");
                                sb.Append("Country: " + record.target_player.PBPlayerInfo.PlayerCountry + "<br/>");
                            }
                            sb.Append("</p>");

                            body = sb.ToString();

                            this.EmailWrite(subject, body);
                        }
                        catch (Exception e) {
                            Plugin.HandleException(new AdKatsException("Error in email sending thread.", e));
                        }
                    }));
                    //Start the thread
                    emailSendingThread.Start();
                }
                catch (Exception e) {
                    Plugin.HandleException(new AdKatsException("Error when sending email.", e));
                }
            }

            private void EmailWrite(String subject, String body) {
                try {
                    MailMessage email = new MailMessage();

                    email.From = new MailAddress(this.SenderEmail);

                    lock (Plugin._UserCache) {
                        foreach (AdKatsUser aUser in Plugin._UserCache.Values) {
                            //Check for not null and default values
                            if (Plugin.RoleIsInteractionAble(aUser.user_role) && !String.IsNullOrEmpty(aUser.user_email)) {
                                if (Regex.IsMatch(aUser.user_email, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$")) {
                                    email.To.Add(new MailAddress(aUser.user_email));
                                }
                                else {
                                    Plugin.ConsoleError("Error in receiver email address: " + aUser.user_email);
                                }
                            }
                        }
                    }

                    email.Subject = subject;
                    email.Body = body;
                    email.IsBodyHtml = true;
                    email.BodyEncoding = Encoding.UTF8;

                    SmtpClient smtp = new SmtpClient(this.SMTPServer, this.SMTPPort) {
                                                                                         EnableSsl = this.UseSSL,
                                                                                         Timeout = 10000,
                                                                                         DeliveryMethod = SmtpDeliveryMethod.Network,
                                                                                         UseDefaultCredentials = false,
                                                                                         Credentials = new NetworkCredential(this.SMTPUser, this.SMTPPassword)
                                                                                     };
                    smtp.Send(email);

                    Plugin.DebugWrite("A notification email has been sent.", 1);
                }
                catch (Exception e) {
                    Plugin.ConsoleError("Error while sending email: " + e);
                }
            }
        }

        #endregion

        #region Twitter Functions

        /////////////////////////CODE CREDIT////////////////////////
        //All below twitter related functions are credited to Micovery's Insane Limits and PapaCharlie9
        ////////////////////////////////////////////////////////////
        public class TwitterHandler {
            public AdKats plugin = null;

            private String oauth_token = String.Empty;
            private String oauth_token_secret = String.Empty;

            //Public keys to connect to AdKats twitter account (Posting only)
            public String twitter_PIN = "2916484";
            public String twitter_consumer_key = "3rkSNbotknUEMstELBNnQg";
            public String twitter_consumer_secret = "vRijlzIyJO8uXcoRM6ikis298sJJcxFkP3sf4hrL7A";
            public String twitter_access_token = "1468907792-UcOkpQhqFXdJM1rsYFq4XHYz9RPIjIW0PYDRfsB";
            public String twitter_access_token_secret = "VzqhUNthdTadAthiX7CqXU62VP7eRXAaw3Jfc1j0";
            public String twitter_user_id = "1468907792";
            public String twitter_screen_name = "AdKats Tool";

            public TwitterHandler(AdKats plugin) {
                this.plugin = plugin;
                this.SetupTwitter();
            }

            public bool sendTweet(String status) {
                return sendCustomTweet(status, twitter_access_token, twitter_access_token_secret, twitter_consumer_key, twitter_consumer_secret, true);
            }

            public bool sendCustomTweet(String status, String access_token, String access_token_secret, String consumer_key, String consumer_secret, bool quiet) {
                try {
                    if (String.IsNullOrEmpty(status))
                        throw new TwitterException("Cannot update Twitter status, invalid ^bstatus^n value");


                    if (String.IsNullOrEmpty(access_token) || String.IsNullOrEmpty(access_token_secret) || String.IsNullOrEmpty(consumer_key) || String.IsNullOrEmpty(consumer_secret))
                        throw new TwitterException("Cannot update Twitter status, looks like you have not run Twitter setup");

                    /* Create the Status Update Request */
                    OAuthRequest orequest = TwitterStatusUpdateRequest(status, access_token, access_token_secret, consumer_key, consumer_secret);

                    HttpWebResponse oresponse = (HttpWebResponse) orequest.request.GetResponse();

                    String protcol = "HTTP/" + oresponse.ProtocolVersion + " " + (Int32) oresponse.StatusCode;

                    if (!oresponse.StatusCode.Equals(HttpStatusCode.OK))
                        throw new TwitterException("Twitter UpdateStatus Request failed, " + protcol);

                    if (oresponse.ContentLength == 0)
                        throw new TwitterException("Twitter UpdateStatus Request failed, ContentLength=0");

                    StreamReader sin = new StreamReader(oresponse.GetResponseStream());
                    String response = sin.ReadToEnd();
                    sin.Close();

                    Hashtable data = (Hashtable) JSON.JsonDecode(response);

                    if (data == null || !data.ContainsKey("id_str"))
                        throw new TwitterException("Twitter UpdateStatus Request failed, response missing ^bid^n field");

                    String id = (String) (data["id_str"].ToString());

                    plugin.DebugWrite("Tweet Successful, id=^b" + id + "^n, Status: " + status, 4);

                    return true;
                }
                catch (TwitterException e) {
                    if (!quiet)
                        plugin.ConsoleError(e.Message);
                }
                catch (WebException e) {
                    if (!quiet)
                        HandleTwitterWebException(e, "UpdateStatus");
                }
                catch (Exception e) {
                    plugin.ConsoleError(e.ToString());
                }

                return false;
            }

            public void VerifyTwitterPin(String PIN) {
                try {
                    if (String.IsNullOrEmpty(PIN)) {
                        plugin.ConsoleError("Cannot verify Twitter PIN, value(^b" + PIN + "^n) is invalid");
                        return;
                    }

                    plugin.DebugWrite("VERIFIER_PIN: " + PIN, 5);

                    if (String.IsNullOrEmpty(oauth_token) || String.IsNullOrEmpty(oauth_token_secret))
                        throw new TwitterException("Cannot verify Twitter PIN, There is no ^boauth_token^n or ^boauth_token_secret^n in memory");

                    OAuthRequest orequest = TwitterAccessTokenRequest(PIN, oauth_token, oauth_token_secret);

                    HttpWebResponse oresponse = (HttpWebResponse) orequest.request.GetResponse();

                    String protcol = "HTTP/" + oresponse.ProtocolVersion + " " + (Int32) oresponse.StatusCode;

                    if (!oresponse.StatusCode.Equals(HttpStatusCode.OK))
                        throw new TwitterException("Twitter AccessToken Request failed, " + protcol);

                    if (oresponse.ContentLength == 0)
                        throw new TwitterException("Twitter AccessToken Request failed, ContentLength=0");

                    StreamReader sin = new StreamReader(oresponse.GetResponseStream());
                    String response = sin.ReadToEnd();

                    plugin.DebugWrite("ACCESS_TOKEN_RESPONSE: " + response, 5);

                    Dictionary<String, String> pairs = ParseQueryString(response);

                    /* Sanity check the results */
                    if (pairs.Count == 0)
                        throw new TwitterException("Twitter AccessToken Request failed, missing fields");

                    /* Get the ReuestToken */
                    if (!pairs.ContainsKey("oauth_token"))
                        throw new TwitterException("Twitter AccessToken Request failed, missing ^boauth_token^n field");
                    oauth_token = pairs["oauth_token"];

                    /* Get the RequestTokenSecret */
                    if (!pairs.ContainsKey("oauth_token_secret"))
                        throw new TwitterException("Twitter AccessToken Request failed, missing ^boauth_token_secret^n field");
                    oauth_token_secret = pairs["oauth_token_secret"];

                    /* Get the User-Id  (Optional) */
                    String user_id = String.Empty;
                    if (pairs.ContainsKey("user_id"))
                        user_id = pairs["user_id"];

                    /* Get the Screen-Name (Optional) */
                    String screen_name = String.Empty;
                    if (pairs.ContainsKey("screen_name"))
                        screen_name = pairs["screen_name"];

                    plugin.ConsoleWrite("Access token, and secret obtained. Twitter setup is now complete.");
                    if (!String.IsNullOrEmpty(user_id))
                        plugin.ConsoleWrite("Twitter User-Id: ^b" + user_id + "^n");
                    if (!String.IsNullOrEmpty(screen_name))
                        plugin.ConsoleWrite("Twitter Screen-Name: ^b" + screen_name + "^n");

                    plugin.DebugWrite("access_token=" + oauth_token, 4);
                    plugin.DebugWrite("access_token_secret=" + oauth_token_secret, 4);

                    this.twitter_access_token = oauth_token;
                    this.twitter_access_token_secret = oauth_token_secret;
                    this.twitter_user_id = user_id;
                    this.twitter_screen_name = screen_name;
                }
                catch (TwitterException e) {
                    plugin.ConsoleError(e.Message);
                    return;
                }
                catch (WebException e) {
                    HandleTwitterWebException(e, "AccessToken");
                }
                catch (Exception e) {
                    plugin.ConsoleError(e.ToString());
                }
            }

            public void SetupTwitter() {
                try {
                    oauth_token = String.Empty;
                    oauth_token_secret = String.Empty;

                    OAuthRequest orequest = TwitterRequestTokenRequest();

                    HttpWebResponse oresponse = (HttpWebResponse) orequest.request.GetResponse();
                    String protcol = "HTTP/" + oresponse.ProtocolVersion + " " + (Int32) oresponse.StatusCode;

                    if (!oresponse.StatusCode.Equals(HttpStatusCode.OK))
                        throw new TwitterException("Twitter RequestToken Request failed, " + protcol);

                    if (oresponse.ContentLength == 0)
                        throw new TwitterException("Twitter RequestToken Request failed, ContentLength=0");

                    StreamReader sin = new StreamReader(oresponse.GetResponseStream());
                    String response = sin.ReadToEnd();

                    Dictionary<String, String> pairs = ParseQueryString(response);

                    if (pairs.Count == 0 || !pairs.ContainsKey("oauth_callback_confirmed"))
                        throw new TwitterException("Twitter RequestToken Request failed, missing ^boauth_callback_confirmed^n field");

                    String oauth_callback_confirmed = pairs["oauth_callback_confirmed"];

                    if (!oauth_callback_confirmed.ToLower().Equals("true"))
                        throw new TwitterException("Twitter RequestToken Request failed, ^boauth_callback_confirmed^n=^b" + oauth_callback_confirmed + "^n");

                    /* Get the ReuestToken */
                    if (!pairs.ContainsKey("oauth_token"))
                        throw new TwitterException("Twitter RequestToken Request failed, missing ^boauth_token^n field");
                    oauth_token = pairs["oauth_token"];

                    /* Get the RequestTokenSecret */
                    if (!pairs.ContainsKey("oauth_token_secret"))
                        throw new TwitterException("Twitter RequestToken Request failed, missing ^boauth_token_secret^n field");
                    oauth_token_secret = pairs["oauth_token_secret"];

                    plugin.DebugWrite("REQUEST_TOKEN_RESPONSE: " + response, 5);
                    plugin.DebugWrite("oauth_callback_confirmed=" + oauth_callback_confirmed, 4);
                    plugin.DebugWrite("oauth_token=" + oauth_token, 4);
                    plugin.DebugWrite("oauth_token_secret=" + oauth_token_secret, 4);

                    //Confirm PIN right away
                    this.VerifyTwitterPin(this.twitter_PIN);
                }
                catch (TwitterException e) {
                    plugin.ConsoleError(e.Message);
                    return;
                }
                catch (WebException e) {
                    HandleTwitterWebException(e, "RequestToken");
                }
                catch (Exception e) {
                    plugin.ConsoleError(e.ToString());
                }
            }

            public void HandleTwitterWebException(WebException e, String prefix) {
                HttpWebResponse response = (HttpWebResponse) e.Response;
                String protcol = (response == null) ? "" : "HTTP/" + response.ProtocolVersion;

                String error = String.Empty;
                //try reading JSON response
                if (response != null && response.ContentType != null && response.ContentType.ToLower().Contains("json")) {
                    try {
                        StreamReader sin = new StreamReader(response.GetResponseStream());
                        String data = sin.ReadToEnd();
                        sin.Close();

                        Hashtable jdata = (Hashtable) JSON.JsonDecode(data);
                        if (jdata == null || !jdata.ContainsKey("error") || jdata["error"] == null || !jdata["error"].GetType().Equals(typeof (String)))
                            throw new Exception();

                        error = "Twitter Error: " + (String) jdata["error"] + ", ";
                    }
                    catch (Exception ex) {
                    }
                }

                /* Handle Time-Out Gracefully */
                if (e.Status.Equals(WebExceptionStatus.Timeout)) {
                    plugin.ConsoleError("Twitter " + prefix + " Request(" + protcol + ") timed-out");
                    return;
                }
                else if (e.Status.Equals(WebExceptionStatus.ProtocolError)) {
                    plugin.ConsoleError("Twitter " + prefix + " Request(" + protcol + ") failed, " + error + " " + e.GetType() + ": " + e.Message);
                    return;
                }
                else
                    throw e;
            }

            public Dictionary<String, String> ParseQueryString(String text) {
                MatchCollection matches = Regex.Matches(text, @"([^=]+)=([^&]+)&?", RegexOptions.IgnoreCase);

                Dictionary<String, String> pairs = new Dictionary<String, String>();

                foreach (Match match in matches)
                    if (match.Success && !pairs.ContainsKey(match.Groups[1].Value))
                        pairs.Add(match.Groups[1].Value, match.Groups[2].Value);

                return pairs;
            }

            public static Int32 MAX_STATUS_LENGTH = 140;

            public OAuthRequest TwitterStatusUpdateRequest(String status, String access_token, String access_token_secret, String consumer_key, String consumer_secret) {
                System.Net.ServicePointManager.Expect100Continue = false;

                if (String.IsNullOrEmpty(status))
                    return null;

                String suffix = "...";
                if (status.Length > MAX_STATUS_LENGTH)
                    status = status.Substring(0, MAX_STATUS_LENGTH - suffix.Length) + suffix;

                OAuthRequest orequest = new OAuthRequest(plugin, "http://api.twitter.com/1/statuses/update.json");
                orequest.Method = HTTPMethod.POST;
                orequest.request.ContentType = "application/x-www-form-urlencoded";

                /* Set the Post Data */

                byte[] data = Encoding.UTF8.GetBytes("status=" + OAuthRequest.UrlEncode(Encoding.UTF8.GetBytes(status)));

                // Parameters required by the Twitter OAuth Protocol
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_consumer_key", consumer_key));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_nonce", Guid.NewGuid().ToString("N")));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_signature_method", "HMAC-SHA1"));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_token", access_token));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_timestamp", ((long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString()));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_version", "1.0"));
                orequest.parameters.Add(new KeyValuePair<String, String>("status", OAuthRequest.UrlEncode(Encoding.UTF8.GetBytes(status))));

                // Compute and add the signature
                String signature = orequest.Signature(consumer_secret, access_token_secret);
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_signature", OAuthRequest.UrlEncode(signature)));

                // Add the OAuth authentication header
                String OAuthHeader = orequest.Header();
                orequest.request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequired;
                orequest.request.Headers["Authorization"] = OAuthHeader;

                // Add the POST body
                orequest.request.ContentLength = data.Length;
                Stream sout = orequest.request.GetRequestStream();
                sout.Write(data, 0, data.Length);
                sout.Close();

                return orequest;
            }

            public OAuthRequest TwitterAccessTokenRequest(String verifier, String token, String secret) {
                OAuthRequest orequest = new OAuthRequest(plugin, "http://api.twitter.com/oauth/access_token");
                orequest.Method = HTTPMethod.POST;
                orequest.request.ContentLength = 0;

                // Parameters required by the Twitter OAuth Protocol
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_consumer_key", twitter_consumer_key));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_nonce", Guid.NewGuid().ToString("N")));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_timestamp", ((long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString()));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_signature_method", "HMAC-SHA1"));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_version", "1.0"));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_token", token));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_verifier", verifier));

                // Compute and add the signature
                String signature = orequest.Signature(twitter_consumer_secret, secret);
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_signature", OAuthRequest.UrlEncode(signature)));

                // Add the OAuth authentication header
                String OAuthHeader = orequest.Header();
                orequest.request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequired;
                orequest.request.Headers["Authorization"] = OAuthHeader;
                return orequest;
            }

            public OAuthRequest TwitterRequestTokenRequest() {
                OAuthRequest orequest = new OAuthRequest(plugin, "http://api.twitter.com/oauth/request_token");
                orequest.Method = HTTPMethod.POST;
                orequest.request.ContentLength = 0;

                // Parameters required by the Twitter OAuth Protocol
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_callback", OAuthRequest.UrlEncode("oob")));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_consumer_key", twitter_consumer_key));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_nonce", Guid.NewGuid().ToString("N")));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_timestamp", ((long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString()));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_signature_method", "HMAC-SHA1"));
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_version", "1.0"));

                // Compute and add the signature
                String signature = orequest.Signature(twitter_consumer_secret, null);
                orequest.parameters.Add(new KeyValuePair<String, String>("oauth_signature", OAuthRequest.UrlEncode(signature)));

                // Add the OAuth authentication header
                String OAuthHeader = orequest.Header();
                orequest.request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequired;
                orequest.request.Headers["Authorization"] = OAuthHeader;

                return orequest;
            }

            public enum HTTPMethod {
                POST = 0x01,
                GET = 0x02,
                PUT = 0x04
            };

            public class TwitterException : Exception {
                public Int32 code = 0;

                public TwitterException(String message) : base(message) {
                }

                public TwitterException(String message, Int32 code) : base(message) {
                    this.code = code;
                }
            }

            public class OAuthRequest {
                private AdKats plugin = null;
                public HttpWebRequest request = null;
                private HMACSHA1 SHA1 = null;
                public List<KeyValuePair<String, String>> parameters = new List<KeyValuePair<String, String>>();

                public HTTPMethod Method {
                    set { request.Method = value.ToString(); }
                    get { return (HTTPMethod) Enum.Parse(typeof (HTTPMethod), request.Method); }
                }

                public OAuthRequest(AdKats plugin, String URL) {
                    this.plugin = plugin;
                    this.request = (HttpWebRequest) HttpWebRequest.Create(URL);
                    this.request.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US; rv:1.9.1.3) Gecko/20090824 Firefox/3.5.3 (.NET CLR 4.0.20506)";
                }

                public void Sort() {
                    // sort the query parameters
                    parameters.Sort(delegate(KeyValuePair<String, String> left, KeyValuePair<String, String> right) {
                                        if (left.Key.Equals(right.Key))
                                            return left.Value.CompareTo(right.Value);
                                        else
                                            return left.Key.CompareTo(right.Key);
                                    });
                }

                public String Header() {
                    String header = "OAuth ";
                    List<String> pairs = new List<String>();

                    Sort();

                    for (Int32 i = 0; i < parameters.Count; i++) {
                        KeyValuePair<String, String> pair = parameters[i];
                        if (pair.Key.Equals("status"))
                            continue;

                        pairs.Add(pair.Key + "=\"" + pair.Value + "\"");
                    }

                    header += String.Join(", ", pairs.ToArray());

                    plugin.DebugWrite("OAUTH_HEADER: " + header, 7);

                    return header;
                }

                public String Signature(String ConsumerSecret, String AccessTokenSecret) {
                    String base_url = request.Address.Scheme + "://" + request.Address.Host + request.Address.AbsolutePath;
                    String encoded_base_url = UrlEncode(base_url);

                    String http_method = request.Method;

                    Sort();

                    List<String> encoded_parameters = new List<String>();
                    List<String> raw_parameters = new List<String>();

                    // encode and concatenate the query parameters
                    for (Int32 i = 0; i < parameters.Count; i++) {
                        KeyValuePair<String, String> pair = parameters[i];

                        // ignore signature if present
                        if (pair.Key.Equals("oauth_signature"))
                            continue;

                        raw_parameters.Add(pair.Key + "=" + pair.Value);
                        encoded_parameters.Add(UrlEncode(pair.Key) + "%3D" + UrlEncode(pair.Value));
                    }

                    String encoded_query = String.Join("%26", encoded_parameters.ToArray());
                    String raw_query = String.Join("&", raw_parameters.ToArray());

                    plugin.DebugWrite("HTTP_METHOD: " + http_method, 8);
                    plugin.DebugWrite("BASE_URI: " + base_url, 8);
                    plugin.DebugWrite("ENCODED_BASE_URI: " + encoded_base_url, 8);
                    //plugin.DebugWrite("RAW_QUERY: " + raw_query, 8);
                    //plugin.DebugWrite("ENCODED_QUERY: " + encoded_query, 8);

                    String base_signature = http_method + "&" + encoded_base_url + "&" + encoded_query;

                    plugin.DebugWrite("BASE_SIGNATURE: " + base_signature, 7);


                    String HMACSHA1_signature = HMACSHA1_HASH(base_signature, ConsumerSecret, AccessTokenSecret);

                    plugin.DebugWrite("HMACSHA1_SIGNATURE: " + HMACSHA1_signature, 7);

                    return HMACSHA1_signature;
                }

                public String HMACSHA1_HASH(String text, String ConsumerSecret, String AccessTokenSecret) {
                    if (SHA1 == null) {
                        /* Initialize the SHA1 */
                        String HMACSHA1_KEY = String.IsNullOrEmpty(ConsumerSecret) ? "" : UrlEncode(ConsumerSecret) + "&" + (String.IsNullOrEmpty(AccessTokenSecret) ? "" : UrlEncode(AccessTokenSecret));
                        plugin.DebugWrite("HMACSHA1_KEY: " + HMACSHA1_KEY, 7);
                        SHA1 = new HMACSHA1(Encoding.ASCII.GetBytes(HMACSHA1_KEY));
                    }

                    return Convert.ToBase64String(SHA1.ComputeHash(System.Text.Encoding.ASCII.GetBytes(text)));
                }

                public static String UnreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

                public static String UrlEncode(String Input) {
                    StringBuilder Result = new StringBuilder();

                    for (Int32 x = 0; x < Input.Length; ++x) {
                        if (UnreservedChars.IndexOf(Input[x]) != -1)
                            Result.Append(Input[x]);
                        else
                            Result.Append("%").Append(String.Format("{0:X2}", (Int32) Input[x]));
                    }

                    return Result.ToString();
                }

                public static String UrlEncode(byte[] Input) {
                    StringBuilder Result = new StringBuilder();

                    for (Int32 x = 0; x < Input.Length; ++x) {
                        if (UnreservedChars.IndexOf((char) Input[x]) != -1)
                            Result.Append((char) Input[x]);
                        else
                            Result.Append("%").Append(String.Format("{0:X2}", (Int32) Input[x]));
                    }

                    return Result.ToString();
                }
            }
        }

        #endregion

        #region BF3Stats Fetch

        public AdKatsPlayerStats FetchPlayerStats(AdKats_Player aPlayer) {
            this.DebugWrite("entering getPlayerStats", 7);
            //Create return value
            AdKatsPlayerStats stats = new AdKatsPlayerStats();
            try {
                //Fetch from BF3Stats
                Hashtable responseData = null;
                if (this._GameVersion == GameVersion.BF3) {
                    responseData = this.fetchBF3StatsPlayer(aPlayer.player_name);
                    if (responseData != null) {
                        String dataStatus = (String) responseData["status"];
                        if (dataStatus == "error") {
                            stats.stats_exception = new AdKatsException("BF3 Stats reported error.");
                        }
                        else if (dataStatus == "notfound") {
                            stats.stats_exception = new AdKatsException(aPlayer.player_name + " not found");
                        }
                        else {
                            //Pull the global stats
                            stats.Platform = (String) responseData["plat"];
                            stats.ClanTag = (String) responseData["tag"];
                            stats.language = (String) responseData["language"];
                            stats.country = (String) responseData["country"];
                            stats.country_name = (String) responseData["country_name"];
                            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                            stats.firstSeen = dtDateTime.AddSeconds((Double) responseData["date_insert"]);
                            stats.lastPlayerUpdate = dtDateTime.AddSeconds((Double) responseData["date_update"]);

                            //Get internal stats
                            if (dataStatus == "data") {
                                Hashtable statsList = (Hashtable) responseData["stats"];
                                stats.lastStatUpdate = dtDateTime.AddSeconds((Double) statsList["date_update"]);
                                //Get rank
                                Hashtable rankTable = (Hashtable) statsList["rank"];
                                stats.rank = (Double) rankTable["nr"];
                                //Get overall
                                Hashtable global = (Hashtable) statsList["global"];
                                stats.kills = (Double) global["kills"];
                                stats.deaths = (Double) global["deaths"];
                                stats.wins = (Double) global["wins"];
                                stats.losses = (Double) global["losses"];
                                stats.shots = (Double) global["shots"];
                                stats.hits = (Double) global["hits"];
                                stats.headshots = (Double) global["headshots"];
                                stats.time = TimeSpan.FromSeconds((Double) global["time"]);
                                //Get weapons
                                stats.weaponStats = new Dictionary<String, AdKatsWeaponStats>();
                                Hashtable weaponStats = (Hashtable) statsList["weapons"];
                                foreach (String weaponKey in weaponStats.Keys) {
                                    //Create new construct
                                    AdKatsWeaponStats weapon = new AdKatsWeaponStats();
                                    //Grab data
                                    Hashtable currentWeapon = (Hashtable) weaponStats[weaponKey];
                                    //Parse into construct
                                    weapon.id = (String) currentWeapon["name"];
                                    weapon.shots = (Double) currentWeapon["shots"];
                                    weapon.hits = (Double) currentWeapon["hits"];
                                    weapon.kills = (Double) currentWeapon["kills"];
                                    weapon.headshots = (Double) currentWeapon["headshots"];
                                    weapon.category = (String) currentWeapon["category"];
                                    weapon.kit = (String) currentWeapon["kit"];
                                    weapon.range = (String) currentWeapon["range"];
                                    weapon.time = TimeSpan.FromSeconds((Double) currentWeapon["time"]);
                                    //Calculate values
                                    weapon.hskr = weapon.headshots / weapon.kills;
                                    weapon.kpm = weapon.kills / weapon.time.TotalMinutes;
                                    weapon.dps = weapon.kills / weapon.hits * 100;
                                    //Assign the construct
                                    stats.weaponStats.Add(weapon.id, weapon);
                                }
                            }
                            else {
                                stats.stats_exception = new AdKatsException(aPlayer.player_name + " did not have stats");
                            }
                        }
                    }
                }
                else if (this._GameVersion == GameVersion.BF4) {
                    responseData = this.FetchBF4StatsPlayer(aPlayer.player_name);
                    if (responseData != null) {
                        if (responseData.ContainsKey("error")) {
                            stats.stats_exception = new AdKatsException("BF4 stats returned error '" + ((String) responseData["error"]) + "' when querying for player '" + aPlayer.player_name + "'.");
                            this.ConsoleError(stats.stats_exception.ToString());
                            return null;
                        }
                        else if (!responseData.ContainsKey("player") || !responseData.ContainsKey("stats") || !responseData.ContainsKey("weapons")) {
                            stats.stats_exception = new AdKatsException("BF4 stats response for player '" + aPlayer.player_name + "' was invalid.");
                            this.ConsoleError(stats.stats_exception.ToString());
                            return null;
                        }
                        else {
                            try {
                                //Player section
                                Hashtable playerData = (Hashtable) responseData["player"];
                                if (playerData != null && playerData.Count > 0) {
                                    stats.Platform = (String) playerData["plat"];
                                    stats.ClanTag = (String) playerData["tag"];
                                    stats.country = (String) playerData["country"];
                                    stats.country_name = (String) playerData["countryName"];
                                    DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                                    Double createMilli = (Double) playerData["dateCreate"];
                                    stats.firstSeen = dtDateTime.AddMilliseconds(createMilli);
                                    Double updateMilli = (Double) playerData["dateUpdate"];
                                    stats.lastPlayerUpdate = dtDateTime.AddMilliseconds(updateMilli);
                                    //Get rank
                                    Hashtable rankData = (Hashtable) playerData["rank"];
                                    stats.rank = (Double) rankData["nr"];
                                }
                                else {
                                    stats.stats_exception = new AdKatsException(aPlayer.player_name + " did not have global info.");
                                }

                                //Get Stats
                                Hashtable statsData = (Hashtable) responseData["stats"];
                                if (statsData != null && statsData.Count > 0) {
                                    //Stat last update is the same as last player update in BF4
                                    stats.lastStatUpdate = stats.lastPlayerUpdate;
                                    stats.kills = (Double) statsData["kills"];
                                    stats.deaths = (Double) statsData["deaths"];
                                    stats.wins = (Double) statsData["numWins"];
                                    stats.losses = (Double) statsData["numLosses"];
                                    stats.shots = (Double) statsData["shotsFired"];
                                    stats.hits = (Double) statsData["shotsHit"];
                                    stats.headshots = (Double) statsData["headshots"];
                                    stats.time = TimeSpan.FromSeconds((Double) statsData["timePlayed"]);
                                }
                                else {
                                    stats.stats_exception = new AdKatsException(aPlayer.player_name + " did not have global stats.");
                                }

                                //Get Weapons
                                ArrayList weaponData = (ArrayList) responseData["weapons"];
                                if (weaponData != null && weaponData.Count > 0) {
                                    stats.weaponStats = new Dictionary<String, AdKatsWeaponStats>();
                                    foreach (Hashtable currentWeapon in weaponData) {
                                        //Create new construct
                                        AdKatsWeaponStats weapon = new AdKatsWeaponStats();
                                        //Grab stat data
                                        Hashtable currentWeaponStats = (Hashtable) currentWeapon["stat"];
                                        weapon.id = (String) currentWeaponStats["id"];
                                        weapon.time = TimeSpan.FromSeconds((Double) currentWeaponStats["time"]);
                                        weapon.shots = (Double) currentWeaponStats["shots"];
                                        weapon.hits = (Double) currentWeaponStats["hits"];
                                        weapon.kills = (Double) currentWeaponStats["kills"];
                                        weapon.headshots = (Double) currentWeaponStats["hs"];

                                        //Grab detail data
                                        Hashtable currentWeaponDetail = (Hashtable) currentWeapon["detail"];
                                        weapon.category = (String) currentWeaponDetail["category"];
                                        //leave kit alone
                                        //leave range alone
                                        //Calculate values
                                        weapon.hskr = weapon.headshots / weapon.kills;
                                        weapon.kpm = weapon.kills / weapon.time.TotalMinutes;
                                        weapon.dps = weapon.kills / weapon.hits * 100;
                                        //Assign the construct
                                        stats.weaponStats.Add(weapon.id, weapon);
                                    }
                                }
                                else {
                                    stats.stats_exception = new AdKatsException(aPlayer.player_name + " did not have weapon stats.");
                                }
                            }
                            catch (Exception e) {
                                this.ConsoleError(e.ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                stats.stats_exception = new AdKatsException("Server error fetching stats.", e);
            }
            //Assign the stats if no errors
            if (stats.stats_exception == null) {
                aPlayer.stats = stats;
            }
            this.DebugWrite("exiting getPlayerStats", 7);
            return stats;
        }

        private Hashtable fetchBF3StatsPlayer(String playerName) {
            Hashtable playerData = null;
            using (WebClient client = new WebClient()) {
                NameValueCollection data = new NameValueCollection {
                                                                       {"player", playerName},
                                                                       {"opt", "all"}
                                                                   };
                byte[] response = client.UploadValues("http://api.bf3stats.com/pc/player/", data);
                if (response != null) {
                    String textResponse = System.Text.Encoding.Default.GetString(response);
                    playerData = (Hashtable) JSON.JsonDecode(textResponse);
                }
            }
            return playerData;
        }

        private Hashtable FetchBF4StatsPlayer(String playerName) {
            using (WebClient client = new WebClient()) {
                NameValueCollection data = new NameValueCollection {
                                                                       {"plat", "pc"},
                                                                       {"name", playerName}
                                                                   };
                byte[] response = client.UploadValues("http://api.bf4stats.com/api/playerInfo", data);
                if (response != null) {
                    String textResponse = System.Text.Encoding.Default.GetString(response);
                    return (Hashtable) JSON.JsonDecode(textResponse);
                }
                return null;
            }
        }

        private Hashtable FetchGCPHackCheck(String playerName) {
            Hashtable playerData = null;
            using (WebClient client = new WebClient()) {
                try {
                    String url = "http://bf4cheat.psychedelic-host.info/api/bf4/checkplayer/" + playerName;
                    String textResponse = client.DownloadString(url);
                    playerData = (Hashtable) JSON.JsonDecode(textResponse);
                }
                catch (Exception e) {
                    this.ConsoleError(e.ToString());
                }
            }
            return playerData;
        }

        /*private Hashtable updateBF3StatsPlayer(String player_name)
        {
            Hashtable playerData = null;
            try
            {
                using (WebClient client = new WebClient())
                {
                    System.Security.Cryptography.HMACSHA256 hmac256 = new HMACSHA256(this.GetBytes("0wPt049KGUTESASnNdi6gnMLht3KdV20"));
                    Hashtable hashData = new Hashtable();
                    hashData.Add("time", this.ConvertToTimestamp(DateTime.UtcNow) + "");
                    hashData.Add("ident", "pUNykTul3R");
                    hashData.Add("player", player_name);
                    hashData.Add("type", "cronjob");
                    String dataString = JSON.JsonEncode(hashData);
                    this.ConsoleError("DATA:" + dataString);
                    //url encode the data
                    dataString = System.Web.HttpUtility.UrlEncode(dataString);
                    this.ConsoleError("E DATA: " + dataString);
                    //Compute the sig
                    String sig = System.Web.HttpUtility.UrlEncode(this.GetString(hmac256.ComputeHash(this.GetBytes(dataString))));
                    this.ConsoleError("SIG: " + sig);
                    NameValueCollection data = new NameValueCollection();
                    data.Add("data", dataString);
                    data.Add("sig", sig);
                    byte[] response = client.UploadValues("http://api.bf3stats.com/pc/playerupdate/", data);
                    if (response != null)
                    {
                        String textResponse = System.Text.Encoding.Default.GetString(response);
                        this.ConsoleSuccess(textResponse);
                        playerData = (Hashtable)JSON.JsonDecode(textResponse);
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKats_Exception("Error updating BF3Stats player.", e));
            }
            return playerData;
        }

        private Hashtable lookupBF3StatsPlayer(String player_name)
        {
            Hashtable playerData = null;
            using (WebClient client = new WebClient())
            {
                NameValueCollection data = new NameValueCollection();
                data.Add("player", player_name);
                data.Add("ident", "pUNykTul3R");
                data.Add("opt", "all");
                byte[] response = client.UploadValues("http://api.bf3stats.com/pc/player/", data);
                if (response != null)
                {
                    String textResponse = System.Text.Encoding.Default.GetString(response);
                    playerData = (Hashtable)JSON.JsonDecode(textResponse);
                }
            }
            return playerData;
        }*/

        #endregion

        #region Helper Methods and Classes

        public Boolean RoleIsInteractionAble(AdKats_Role aRole) {
            lock (aRole) {
                lock (aRole.allowedCommands) {
                    if (aRole.allowedCommands.Values.Any(command => command.command_playerInteraction)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public AdKatsCommand GetCommandByKey(String commandKey) {
            AdKatsCommand command = null;
            this._CommandKeyDictionary.TryGetValue(commandKey, out command);
            return command;
        }

        public String ExtractString(String s, String tag) {
            if (String.IsNullOrEmpty(s) || String.IsNullOrEmpty(tag)) {
                this.ConsoleError("Unable to extract String. Invalid inputs.");
                return null;
            }
            String startTag = "<" + tag + ">";
            Int32 startIndex = s.IndexOf(startTag, System.StringComparison.Ordinal) + startTag.Length;
            if (startIndex == -1) {
                this.ConsoleError("Unable to extract String. Tag not found.");
            }
            Int32 endIndex = s.IndexOf("</" + tag + ">", startIndex, System.StringComparison.Ordinal);
            return s.Substring(startIndex, endIndex - startIndex);
        }

        public Boolean SoldierNameValid(String input) {
            try {
                this.DebugWrite("Checking player '" + input + "' for validity.", 7);
                if (String.IsNullOrEmpty(input)) {
                    this.ConsoleError("Soldier Name empty or null.");
                    return false;
                }
                if (input.Length > 16) {
                    this.ConsoleError("Soldier Name '" + input + "' too long, maximum length is 16 characters.");
                    return false;
                }
                if (new Regex("[^a-zA-Z0-9_-]").Replace(input, "").Length != input.Length) {
                    this.ConsoleError("Soldier Name '" + input + "' contained invalid characters.");
                    return false;
                }
                return true;
            }
            catch (Exception) {
                //Soldier name caused exception in the regex, definitely not valid
                this.ConsoleError("Soldier Name '" + input + "' contained invalid characters.");
                return false;
            }
        }

        public String FormatTimeString(TimeSpan timeSpan) {
            this.DebugWrite("Entering formatTimeString", 7);
            String returnMessage = null;
            try {
                String formattedTime = (timeSpan.TotalMilliseconds >= 0) ? ("") : ("-");
                Int32 days = Math.Abs(timeSpan.Days);
                Int32 hours = Math.Abs(timeSpan.Hours);
                Int32 minutes = Math.Abs(timeSpan.Minutes);
                Int32 seconds = Math.Abs(timeSpan.Seconds);
                //Only show day if greater than 1 day
                if (days > 0) {
                    //Show day count
                    formattedTime += days + "d";
                }
                //Only show more information if less than 35 days
                if (days < 35) {
                    //Only show hours if days exist, or hours > 0
                    if (hours > 0 || days > 0) {
                        //Show hour count
                        formattedTime += hours + "h";
                    }
                    //Only show more infomation if less than 1 day
                    if (days < 1) {
                        //Only show minutes if Hours exist, or minutes > 0
                        if (minutes > 0 || hours > 0) {
                            //Show hour count
                            formattedTime += minutes + "m";
                        }
                        //Only show more infomation if less than 1 hour
                        if (hours < 1) {
                            //Only show seconds if minutes exist, or seconds > 0
                            if (seconds > 0 || minutes > 0) {
                                //Show hour count
                                formattedTime += seconds + "s";
                            }
                        }
                    }
                }
                returnMessage = formattedTime;
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while formatting time String.", e));
            }
            this.DebugWrite("Exiting formatTimeString", 7);
            return returnMessage;
        }

        private Double ConvertToTimestamp(DateTime value) {
            //create Timespan by subtracting the value provided from
            //the Unix Epoch
            TimeSpan span = (value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());

            //return the total seconds (which is a UNIX timestamp)
            return span.TotalSeconds;
        }

        private void RemovePlayerFromDictionary(String playerName) {
            this.DebugWrite("Entering removePlayerFromDictionary", 7);
            try {
                //If the player is currently in the player list, remove them
                if (!String.IsNullOrEmpty(playerName)) {
                    if (this._PlayerDictionary.ContainsKey(playerName)) {
                        lock (this._PlayersMutex) {
                            this.DebugWrite("Removing " + playerName + " from current player list.", 4);
                            this._PlayerDictionary.Remove(playerName);
                        }
                    }
                }
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while removing player from player dictionary.", e));
            }
            this.DebugWrite("Exiting removePlayerFromDictionary", 7);
        }

        public CPlayerInfo BuildCPlayerInfo(String playerName, String playerGUID) {
            this.DebugWrite("Entering ", 7);
            CPlayerInfo playerInfo = null;
            try {
                IList<String> lstParameters = new List<String>();
                IList<String> lstVariables = new List<String>();
                lstParameters.Add("name");
                lstVariables.Add(playerName);
                lstParameters.Add("guid");
                lstVariables.Add(playerGUID);
                playerInfo = new CPlayerInfo(lstParameters, lstVariables);
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while creating CPlayerInfo object.", e));
            }
            this.DebugWrite("Exiting ", 7);
            return playerInfo;
        }

        private TimeSpan getRemainingBanTime(AdKatsBan aBan) {
            return aBan.ban_endTime.Subtract(DateTime.UtcNow);
        }

        public Boolean ConfirmStatLoggerSetup() {
            //This function has been disabled for now

            //Make sure database connection active
            if (this.HandlePossibleDisconnect()) {
                return false;
            }
            try {
                List<MatchCommand> registered = this.GetRegisteredCommands();
                MatchCommand loggerStatusCommand = null;
                foreach (MatchCommand command in registered) {
                    if (System.String.Compare(command.RegisteredClassname, "CChatGUIDStatsLoggerBF3", System.StringComparison.Ordinal) == 0 && System.String.Compare(command.RegisteredMethodName, "GetStatus", System.StringComparison.Ordinal) == 0) {
                        loggerStatusCommand = command;
                        this.DebugWrite("Found command for BF3 stat logger.", 5);
                        break;
                    }
                    if (System.String.Compare(command.RegisteredClassname, "CChatGUIDStatsLogger", System.StringComparison.Ordinal) == 0 && System.String.Compare(command.RegisteredMethodName, "GetStatus", System.StringComparison.Ordinal) == 0) {
                        loggerStatusCommand = command;
                        this.DebugWrite("Found command for Universal stat logger.", 5);
                        break;
                    }
                }
                if (loggerStatusCommand != null) {
                    //Stat logger is installed, fetch its status
                    Hashtable statLoggerStatus = this.GetStatLoggerStatus();

                    //Only continue if response value
                    if (statLoggerStatus == null) {
                        return false;
                    }
                    foreach (String key in statLoggerStatus.Keys) {
                        this.DebugWrite("Logger response: (" + key + "): " + statLoggerStatus[key], 5);
                    }
                    if (((String) statLoggerStatus["pluginVersion"]) != "1.1.0.2") {
                        this.ConsoleError("Invalid version of CChatGUIDStatsLoggerBF3 installed. Version 1.1.0.2 is required. If there is a new version, inform ColColonCleaner.");
                        return false;
                    }

                    if (!Regex.Match((String) statLoggerStatus["DBHost"], this._MySqlHostname, RegexOptions.IgnoreCase).Success || !Regex.Match((String) statLoggerStatus["DBPort"], this._MySqlPort, RegexOptions.IgnoreCase).Success || !Regex.Match((String) statLoggerStatus["DBName"], this._MySqlDatabaseName, RegexOptions.IgnoreCase).Success) {
                        //Are db settings set for AdKats? If not, import them from stat logger.
                        if (String.IsNullOrEmpty(this._MySqlHostname) || String.IsNullOrEmpty(this._MySqlPort) || String.IsNullOrEmpty(this._MySqlDatabaseName)) {
                            this._MySqlHostname = (String) statLoggerStatus["DBHost"];
                            this._MySqlPort = (String) statLoggerStatus["DBPort"];
                            this._MySqlDatabaseName = (String) statLoggerStatus["DBName"];
                            this.UpdateSettingPage();
                        }
                        //Are DB Settings set for stat logger? If not, set them
                        if (String.IsNullOrEmpty((String) statLoggerStatus["DBHost"]) || String.IsNullOrEmpty((String) statLoggerStatus["DBPort"]) || String.IsNullOrEmpty((String) statLoggerStatus["DBName"])) {
                            this.SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Host", this._MySqlHostname);
                            this.SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Port", this._MySqlPort);
                            this.SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Database Name", this._MySqlDatabaseName);
                            this.SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "UserName", this._MySqlUsername);
                            this.SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Password", this._MySqlPassword);

                            this.ConsoleError("CChatGUIDStatsLoggerBF3 database connection was not configured. It has been set up to use the same database and credentials as AdKats.");
                            //Update the logger status
                            statLoggerStatus = this.GetStatLoggerStatus();
                        }
                        else {
                            this.ConsoleError("CChatGUIDStatsLoggerBF3 is not set up to use the same database as AdKats. Modify settings so they both use the same database.");
                            return false;
                        }
                    }
                    if (((String) statLoggerStatus["DBConnectionActive"]) != "True") {
                        this.ConsoleError("CChatGUIDStatsLoggerBF3's connection to the database is not active. Backup mode Enabled...");
                    }
                    return true;
                }
                this.ConsoleError("^1^bCChatGUIDStatsLoggerBF3^n plugin not found. Installing special release version 1.1.0.2 of that plugin is required for AdKats!");
                return false;
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while confirming stat logger setup.", e));
                return false;
            }
        }

        public Hashtable GetStatLoggerStatus() {
            //Disabled

            //Make sure AdKats database connection active
            if (this.HandlePossibleDisconnect()) {
                return null;
            }
            try {
                //Check if enabled
                if (!this._IsEnabled) {
                    this.DebugWrite("Attempted to fetch stat logger status while plugin disabled.", 4);
                    return null;
                }
                //Build request
                Hashtable request = new Hashtable();
                request["pluginName"] = "AdKats";
                request["pluginMethod"] = "HandleStatLoggerStatusResponse";
                // Send request
                this._StatLoggerStatusWaitHandle.Reset();
                this.ExecuteCommand("procon.protected.plugins.call", "CChatGUIDStatsLoggerBF3", "GetStatus", JSON.JsonEncode(request));
                //Wait a maximum of 5 seconds for stat logger response
                if (!this._StatLoggerStatusWaitHandle.WaitOne(5000)) {
                    this.ConsoleWarn("^bCChatGUIDStatsLoggerBF3^n is not enabled or is lagging! Attempting to enable, please wait...");
                    Int32 attempts = 0;
                    Boolean success = false;
                    do {
                        attempts++;
                        this.DebugWrite("Stat Logger Enable Attempt " + attempts, 2);
                        //Issue the command to enable stat logger
                        this.ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "True");
                        //Wait 5 seconds for enable and initial connect
                        Thread.Sleep(5000);
                        //Refetch the status
                        this._StatLoggerStatusWaitHandle.Reset();
                        this.ExecuteCommand("procon.protected.plugins.call", "CChatGUIDStatsLoggerBF3", "GetStatus", JSON.JsonEncode(request));
                        if (this._StatLoggerStatusWaitHandle.WaitOne(5000)) {
                            success = true;
                        }
                    } while (!success && attempts < 10);
                    if (!success) {
                        this.ConsoleError("CChatGUIDStatsLoggerBF3 could not be enabled automatically. Please enable manually.");
                        return null;
                    }
                }
                if (this._LastStatLoggerStatusUpdate != null && this._LastStatLoggerStatusUpdateTime != DateTime.MinValue && this._LastStatLoggerStatusUpdate.ContainsKey("pluginVersion") && this._LastStatLoggerStatusUpdate.ContainsKey("pluginEnabled") && this._LastStatLoggerStatusUpdate.ContainsKey("DBHost") && this._LastStatLoggerStatusUpdate.ContainsKey("DBPort") && this._LastStatLoggerStatusUpdate.ContainsKey("DBName") && this._LastStatLoggerStatusUpdate.ContainsKey("DBTimeOffset") && this._LastStatLoggerStatusUpdate.ContainsKey("DBConnectionActive") && this._LastStatLoggerStatusUpdate.ContainsKey("ChatloggingEnabled") && this._LastStatLoggerStatusUpdate.ContainsKey("InstantChatLoggingEnabled") && this._LastStatLoggerStatusUpdate.ContainsKey("StatsLoggingEnabled") && this._LastStatLoggerStatusUpdate.ContainsKey("DBliveScoreboardEnabled") && this._LastStatLoggerStatusUpdate.ContainsKey("DebugMode") && this._LastStatLoggerStatusUpdate.ContainsKey("Error")) {
                    //Response appears to be valid, return it
                    return this._LastStatLoggerStatusUpdate;
                }
                //Response is invalid, throw error and return null
                this.ConsoleError("Status response from CChatGUIDStatsLoggerBF3 was not valid.");
                return null;
            }
            catch (Exception) {
                this.HandleException(new AdKatsException("Error while getting stat logger status."));
                return null;
            }
        }

        public void HandleStatLoggerStatusResponse(params String[] commands) {
            this.DebugWrite("Entering HandleStatLoggerStatusResponse", 7);
            try {
                if (commands.Length < 1) {
                    this.ConsoleError("Status fetch response handle canceled, no parameters provided.");
                    return;
                }
                this._LastStatLoggerStatusUpdate = (Hashtable) JSON.JsonDecode(commands[0]);
                this._LastStatLoggerStatusUpdateTime = DateTime.UtcNow;
                this._StatLoggerStatusWaitHandle.Set();
            }
            catch (Exception e) {
                this.HandleException(new AdKatsException("Error while handling stat logger status response.", e));
            }
            this.DebugWrite("Exiting HandleStatLoggerStatusResponse", 7);
        }

        public static String GetRandom32BitHashCode() {
            String randomString = "";
            Random random = new Random();

            for (Int32 i = 0; i < 32; i++) {
                randomString += Convert.ToChar(Convert.ToInt32(Math.Floor(91 * random.NextDouble()))).ToString(CultureInfo.InvariantCulture);
            }

            return Encode(randomString);
        }

        public static String Encode(String str) {
            byte[] encbuff = System.Text.Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(encbuff);
        }

        public static String Decode(String str) {
            byte[] decbuff = Convert.FromBase64String(str.Replace(" ", "+"));
            return System.Text.Encoding.UTF8.GetString(decbuff);
        }

        public static String EncodeStringArray(String[] strValue) {
            StringBuilder encodedString = new StringBuilder();

            for (Int32 i = 0; i < strValue.Length; i++) {
                if (i > 0) {
                    encodedString.Append("|");
                    //strReturn += "|";
                }
                encodedString.Append(Encode(strValue[i]));
                //strReturn += Encode(strValue[i]);
            }

            return encodedString.ToString();
        }

        public byte[] GetBytes(String str) {
            byte[] bytes = new byte[str.Length * sizeof (char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public String GetString(byte[] bytes) {
            char[] chars = new char[bytes.Length / sizeof (char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new String(chars);
        }

        public void SetServerInfo(CServerInfo info) {
            lock (this._ServerInfoMutex) {
                this._ServerInfo = info;
            }
        }

        public CServerInfo GetServerInfo() {
            lock (this._ServerInfoMutex) {
                return this._ServerInfo;
            }
        }

        //Calling this method will make the settings window refresh with new data
        public void UpdateSettingPage() {
            this.SetExternalPluginSetting("AdKats", "UpdateSettings", "Update");
        }

        //Calls setVariable with the given parameters
        public void SetExternalPluginSetting(String pluginName, String settingName, String settingValue) {
            if (String.IsNullOrEmpty(pluginName) || String.IsNullOrEmpty(settingName) || String.IsNullOrEmpty(settingValue)) {
                this.ConsoleError("Required inputs null in setExternalPluginSetting");
                return;
            }
            this.ExecuteCommand("procon.protected.plugins.setVariable", pluginName, settingName, settingValue);
        }

        //Credit to Micovery and PapaCharlie9 for modified Levenshtein Distance algorithm 
        public static Int32 LevenshteinDistance(String s, String t) {
            s = s.ToLower();
            t = t.ToLower();
            Int32 n = s.Length;
            Int32 m = t.Length;
            Int32[,] d = new Int32[n + 1, m + 1];
            if (n == 0)
                return m;
            if (m == 0)
                return n;
            for (Int32 i = 0; i <= n; d[i, 0] = i++)
                ;
            for (Int32 j = 0; j <= m; d[0, j] = j++)
                ;
            for (Int32 i = 1; i <= n; i++)
                for (Int32 j = 1; j <= m; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 0), d[i - 1, j - 1] + ((t[j - 1] == s[i - 1]) ? 0 : 1));
            return d[n, m];
        }

        //parses single word or number parameters out of a String until param count is reached
        private String[] ParseParameters(String message, Int32 maxParamCount) {
            //create list for parameters
            List<String> parameters = new List<String>();
            if (message.Length > 0) {
                //Add all single word/number parameters
                String[] paramSplit = message.Split(' ');
                Int32 maxLoop = (paramSplit.Length < maxParamCount) ? (paramSplit.Length) : (maxParamCount);
                for (Int32 i = 0; i < maxLoop - 1; i++) {
                    this.DebugWrite("Param " + i + ": " + paramSplit[i], 6);
                    parameters.Add(paramSplit[i]);
                    message = message.TrimStart(paramSplit[i].ToCharArray()).Trim();
                }
                //Add final multi-word parameter
                parameters.Add(message);
            }
            this.DebugWrite("Num params: " + parameters.Count, 6);
            return parameters.ToArray();
        }

        public void JoinWith(Thread thread) {
            if (thread == null || !thread.IsAlive) {
                DebugWrite("Thread already finished.", 3);
                return;
            }
            DebugWrite("Waiting for ^b" + thread.Name + "^n to finish", 3);
            thread.Join();
        }

        public class AdKatsUser {
            //No reference to player table made here, plain String name access
            public Int64 user_id = -1;
            public String user_name = null;
            public String user_email = null;
            public String user_phone = null;
            public Dictionary<long, AdKats_Player> soldierDictionary = null;
            public AdKats_Role user_role = null;

            public AdKatsUser() {
                this.soldierDictionary = new Dictionary<long, AdKats_Player>();
            }
        }

        public class AdKatsCommand {
            //Active option
            public enum CommandActive {
                Active,
                Disabled,
                Invisible
            }

            //Logging option
            public enum CommandLogging {
                Log,
                Ignore,
                Mandatory,
                Unable
            }

            public Int64 command_id = -1;
            public CommandActive command_active = CommandActive.Active;
            public String command_key = null;
            public CommandLogging command_logging = CommandLogging.Log;
            public String command_name = null;
            public String command_text = null;
            public Boolean command_playerInteraction = true;

            public AdKatsCommand() {
            }
        }

        public class AdKats_Role {
            public Int64 role_id = -1;
            public String role_key = null;
            public String role_name = null;
            public Dictionary<String, AdKatsCommand> allowedCommands = null;

            public AdKats_Role() {
                this.allowedCommands = new Dictionary<String, AdKatsCommand>();
            }
        }

        public class AdKats_Player {
            public Int64 player_id = -1;
            public Int64 game_id = -1;
            public String clan_tag = null;
            public String player_name = null;
            public String player_guid = null;
            public String player_pbguid = null;
            public String player_ip = null;
            public String player_slot = null;
            public AdKats_Role player_role = null;
            public Boolean player_aa = false;
            public Boolean player_aa_told = false;
            public Boolean player_online = false;

            public CPlayerInfo frostbitePlayerInfo = null;
            public CPunkbusterInfo PBPlayerInfo = null;

            public DateTime lastSpawn = DateTime.UtcNow;
            public DateTime lastDeath = DateTime.UtcNow;
            public DateTime lastAction = DateTime.UtcNow;

            public Queue<KeyValuePair<AdKats_Player, DateTime>> recentKills = null;

            public AdKatsPlayerStats stats = null;

            public AdKats_Player() {
                this.recentKills = new Queue<KeyValuePair<AdKats_Player, DateTime>>();
            }
        }

        public class AdKatsPlayerStats {
            public String Platform = null;
            public String ClanTag = null;
            public String language = null;
            public String country = null;
            public String country_name = null;
            public DateTime firstSeen = DateTime.MaxValue;
            public DateTime lastPlayerUpdate = DateTime.MaxValue;
            public DateTime lastStatUpdate = DateTime.MaxValue;

            public Double rank = -1;
            public TimeSpan time = TimeSpan.FromSeconds(0);
            public Int32 score = -1;
            public Double kills = -1;
            public Double assists = -1;
            public Double deaths = -1;
            public Double shots = -1;
            public Double hits = -1;
            public Double headshots = -1;

            public Double wins = -1;
            public Double losses = -1;

            public Dictionary<String, AdKatsWeaponStats> weaponStats = null;

            public AdKatsException stats_exception = null;

            public AdKatsPlayerStats() {
            }
        }

        public class AdKatsWeaponStats {
            public String id = null;
            public String category = null;
            public String kit = null;
            public String range = null;

            public TimeSpan time = TimeSpan.FromSeconds(0);
            public Double kills = -1;
            public Double headshots = -1;
            public Double hits = -1;
            public Double shots = -1;

            //Calculated values
            public Double hskr = -1;
            public Double dps = -1;
            public Double kpm = -1;

            public AdKatsWeaponStats() {
            }
        }

        #region stat library

        public class StatLibrary {
            public Dictionary<GameVersion, Dictionary<String, StatLibraryWeapon>> weapons = null;

            public StatLibrary(AdKats plugin) {
                try {
                    //Create the weapon library
                    this.weapons = new Dictionary<GameVersion, Dictionary<String, StatLibraryWeapon>>();

                    //Create the game specific libraries
                    Dictionary<String, StatLibraryWeapon> bf3Weapons = new Dictionary<String, StatLibraryWeapon>();
                    Dictionary<String, StatLibraryWeapon> bf4Weapons = new Dictionary<String, StatLibraryWeapon>();

                    //Add the weapons
                    StatLibraryWeapon weapon;
                    weapon = new StatLibraryWeapon {
                                                       name = "G17C",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "G17C SUPP.",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "G17C TACT.",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = ".44 MAGNUM",
                                                       damage_max = 60,
                                                       damage_min = 30
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = ".44 SCOPED",
                                                       damage_max = 60,
                                                       damage_min = 30
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "93R",
                                                       damage_max = 20,
                                                       damage_min = 12.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "G18",
                                                       damage_max = 20,
                                                       damage_min = 12.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "G18 SUPP.",
                                                       damage_max = 20,
                                                       damage_min = 12.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "G18 TACT.",
                                                       damage_max = 20,
                                                       damage_min = 12.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M9",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M9 TACT.",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M9 SUPP.",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M1911",
                                                       damage_max = 34,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M1911 TACT.",
                                                       damage_max = 34,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M1911 SUPP.",
                                                       damage_max = 34,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M1911 S-TAC",
                                                       damage_max = 34,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "MP412 REX",
                                                       damage_max = 50,
                                                       damage_min = 28
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "MP443",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "MP443 TACT.",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "MP443 SUPP.",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "AS VAL",
                                                       damage_max = 20,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M5K",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "MP7",
                                                       damage_max = 20,
                                                       damage_min = 11.2
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "P90",
                                                       damage_max = 20,
                                                       damage_min = 11.2
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "PDW-R",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "PP-19",
                                                       damage_max = 16.7,
                                                       damage_min = 12.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "PP-2000",
                                                       damage_max = 25,
                                                       damage_min = 13.75
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "UMP-45",
                                                       damage_max = 34,
                                                       damage_min = 12.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "JNG-90",
                                                       damage_max = 80,
                                                       damage_min = 59
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "L96",
                                                       damage_max = 80,
                                                       damage_min = 59
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M39 EMR",
                                                       damage_max = 50,
                                                       damage_min = 37.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M40A5",
                                                       damage_max = 80,
                                                       damage_min = 59
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M98B",
                                                       damage_max = 95,
                                                       damage_min = 59
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M417",
                                                       damage_max = 50,
                                                       damage_min = 37.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "MK11",
                                                       damage_max = 50,
                                                       damage_min = 37.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "QBU-88",
                                                       damage_max = 50,
                                                       damage_min = 37.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "SKS",
                                                       damage_max = 43,
                                                       damage_min = 27
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "SV98",
                                                       damage_max = 80,
                                                       damage_min = 50
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "SVD",
                                                       damage_max = 50,
                                                       damage_min = 37.5
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M27 IAR",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "RPK-74M",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "L86A2",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "LSAT",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M60E4",
                                                       damage_max = 34,
                                                       damage_min = 22
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M240B",
                                                       damage_max = 34,
                                                       damage_min = 22
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M249",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "MG36",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "PKP PECHENEG",
                                                       damage_max = 34,
                                                       damage_min = 22
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "QBB-95",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "TYPE 88 LMG",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "A-91",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "ACW-R",
                                                       damage_max = 20,
                                                       damage_min = 16.7
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "AKS-74u",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "G36C",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "G53",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M4A1",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "MTAR-21",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "QBZ-95B",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "SCAR-H",
                                                       damage_max = 30,
                                                       damage_min = 20
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "SG553",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M4",
                                                       damage_max = 25,
                                                       damage_min = 14.3
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "AEK-971",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "AK-74M",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "AN-94",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "AUG A3",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "F2000",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "FAMAS",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "G3A3",
                                                       damage_max = 34,
                                                       damage_min = 22
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "KH2002",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "L85A2",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M16A3",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M416",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "SCAR-L",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M16A4",
                                                       damage_max = 25,
                                                       damage_min = 18.4
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "XBOW",
                                                       damage_max = 100,
                                                       damage_min = 10
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "XBOW SCOPED",
                                                       damage_max = 100,
                                                       damage_min = 10
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M320",
                                                       damage_max = 100,
                                                       damage_min = 1
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M26",
                                                       damage_max = 100,
                                                       damage_min = 1
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M26 MASS",
                                                       damage_max = 100,
                                                       damage_min = 1
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M26 SLUG",
                                                       damage_max = 100,
                                                       damage_min = 1
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "M26 FRAG",
                                                       damage_max = 100,
                                                       damage_min = 1
                                                   };
                    bf3Weapons.Add(weapon.name, weapon);

                    //Add BF4 Weapons
                    //Carbines
                    weapon = new StatLibraryWeapon {
                                                       name = "ak-5c",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "aku-12",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "sg553",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "acw-r",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "g36c",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "type-95b-1",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "a-91",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "ace-21-cqb",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "ace-52-cqb",
                                                       damage_max = 34,
                                                       damage_min = 20
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m4",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    //DMRs
                    weapon = new StatLibraryWeapon {
                                                       name = "ace-53-sv",
                                                       damage_max = 43,
                                                       damage_min = 30
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m39-emr",
                                                       damage_max = 43,
                                                       damage_min = 30
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "qbu-88",
                                                       damage_max = 40,
                                                       damage_min = 28
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "mk11-mod-0",
                                                       damage_max = 43,
                                                       damage_min = 30
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "scar-h-sv",
                                                       damage_max = 43,
                                                       damage_min = 30
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "sks",
                                                       damage_max = 40,
                                                       damage_min = 28
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "rfb",
                                                       damage_max = 43,
                                                       damage_min = 30
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "svd-12",
                                                       damage_max = 43,
                                                       damage_min = 30
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    //Handguns
                    weapon = new StatLibraryWeapon {
                                                       name = "p226",
                                                       damage_max = 27,
                                                       damage_min = 12.1
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m412-rex",
                                                       damage_max = 56,
                                                       damage_min = 28
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "compact-45",
                                                       damage_max = 34,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m1911",
                                                       damage_max = 34,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "44-magnum",
                                                       damage_max = 56,
                                                       damage_min = 37.5
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "93r",
                                                       damage_max = 18,
                                                       damage_min = 10.8
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "mp443",
                                                       damage_max = 27,
                                                       damage_min = 12.1
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "g18",
                                                       damage_max = 18,
                                                       damage_min = 10.8
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "cz-75",
                                                       damage_max = 27,
                                                       damage_min = 13.5
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "qsz-92",
                                                       damage_max = 20,
                                                       damage_min = 11.2
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m9",
                                                       damage_max = 27,
                                                       damage_min = 12.1
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "fn57",
                                                       damage_max = 20,
                                                       damage_min = 11.2
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    //Assault
                    weapon = new StatLibraryWeapon {
                                                       name = "aek-971",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m416",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "scar-h",
                                                       damage_max = 34,
                                                       damage_min = 28
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "sar-21",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "ak-12",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "aug-a3",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "qbz-95-1",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m16a4",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "cz-805",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "famas",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "ace-23",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    //PDWs
                    weapon = new StatLibraryWeapon {
                                                       name = "mx4",
                                                       damage_max = 25,
                                                       damage_min = 12.1
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "pp-2000",
                                                       damage_max = 25,
                                                       damage_min = 12.1
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "cbj-ms",
                                                       damage_max = 22,
                                                       damage_min = 12.1
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "ump-45",
                                                       damage_max = 34,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "cz-3a1",
                                                       damage_max = 25,
                                                       damage_min = 12.1
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "p90",
                                                       damage_max = 21,
                                                       damage_min = 11.2
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "pdw-r",
                                                       damage_max = 25,
                                                       damage_min = 15.4
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "ump-9",
                                                       damage_max = 25,
                                                       damage_min = 12.1
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "js2",
                                                       damage_max = 20,
                                                       damage_min = 11.2
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    //LMGs
                    weapon = new StatLibraryWeapon {
                                                       name = "mg4",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "lsat",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m240b",
                                                       damage_max = 34,
                                                       damage_min = 25
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "qbb-95-1",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "type-88-lmg",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "u-100-mk5",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "rpk-12",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "pkp-pecheneg",
                                                       damage_max = 34,
                                                       damage_min = 25
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);
                    weapon = new StatLibraryWeapon {
                                                       name = "m249",
                                                       damage_max = 25,
                                                       damage_min = 18
                                                   };
                    bf4Weapons.Add(weapon.name, weapon);

                    //Add the dictionaries
                    this.weapons.Add(GameVersion.BF3, bf3Weapons);
                    this.weapons.Add(GameVersion.BF4, bf4Weapons);
                }
                catch (Exception e) {
                    plugin.ConsoleError(e.ToString());
                }
            }
        }

        #endregion

        public class StatLibraryWeapon {
            public String name = null;
            public Double damage_max = -1;
            public Double damage_min = -1;

            public StatLibraryWeapon() {
            }
        }

        public class AdKatsException {
            public String Method = String.Empty;
            public String message = String.Empty;
            public System.Exception InternalException = null;
            //Param Constructors
            public AdKatsException(String message, System.Exception internalException) {
                this.Method = new StackFrame(1).GetMethod().Name;
                this.message = message;
                this.InternalException = internalException;
            }

            public AdKatsException(String message) {
                this.Method = new StackFrame(1).GetMethod().Name;
                this.message = message;
            }

            //Override toString
            public override String ToString() {
                return "[" + this.Method + "][" + this.message + "]" + ((this.InternalException != null) ? (": " + InternalException) : (""));
            }
        }

        public class AdKatsRecord {
            //Source of this record
            public enum Sources {
                Default,
                Automated,
                External,
                InGame,
                Settings,
                ProconChat,
                Database,
                HTTP
            }

            //Attributes for the record
            public Int64 record_id = -1;
            public Int64 server_id = -1;
            public AdKatsCommand command_type = null;
            public AdKatsCommand command_action = null;
            public Int32 command_numeric = 0;
            public String source_name = null;
            public AdKats_Player source_player = null;
            public String target_name = null;
            public AdKats_Player target_player = null;
            public String record_message = null;
            public DateTime record_time = DateTime.MinValue;

            //Not stored separately in the database
            public Sources record_source = Sources.Default;
            public Boolean isDebug = false;
            public Boolean isIRO = false;

            //Current exception state of the record
            public AdKatsException record_exception = null;

            //If record action was take
            public Boolean record_action_executed = false;

            //Default Constructor
            public AdKatsRecord() {
            }
        }

        public class AdKatsBan {
            //Current exception state of the ban
            public AdKatsException ban_exception = null;

            public Int64 ban_id = -1;
            public AdKatsRecord ban_record = null;
            public String ban_status = "Enabled";
            public String ban_notes = null;
            public String ban_sync = null;
            //startTime and endTime are not set by AdKats, they are set in the database.
            //startTime and endTime will be valid only when bans are fetched from the database
            public DateTime ban_startTime;
            public DateTime ban_endTime;
            public Boolean ban_enforceName = false;
            public Boolean ban_enforceGUID = true;
            public Boolean ban_enforceIP = false;

            public AdKatsBan() {
            }
        }

        public class BBM5108Ban {
            public String soldiername = null;
            public String eaguid = null;
            public String ban_length = null;
            public DateTime ban_duration;
            public String ban_reason = null;
        }

        #endregion

        #region Logging

        public String FormatMessage(String msg, ConsoleMessageType type) {
            String prefix = "[^bAdKats^n] ";

            if (type.Equals(ConsoleMessageType.Warning)) {
                prefix += "^1^bWARNING^0^n: ";
            }
            else if (type.Equals(ConsoleMessageType.Error)) {
                prefix += "^1^bERROR^0^n: ";
            }
            else if (type.Equals(ConsoleMessageType.Success)) {
                prefix += "^b^2SUCCESS^n^0: ";
            }
            else if (type.Equals(ConsoleMessageType.Exception)) {
                prefix += "^1^bEXCEPTION^0^n: ";
            }

            return prefix + msg;
        }

        public String GenerateKickReason(AdKatsRecord record) {
            String sourceNameString = "[" + record.source_name + "]";

            //Create the full message
            String fullMessage = record.record_message + " " + sourceNameString;

            //Trim the kick message if necessary
            Int32 cutLength = fullMessage.Length - 80;
            if (cutLength > 0) {
                String cutReason = record.record_message.Substring(0, record.record_message.Length - cutLength);
                fullMessage = cutReason + " " + sourceNameString;
            }
            return fullMessage;
        }

        public String GenerateBanReason(AdKatsBan aBan) {
            String banDurationString;
            //If ban time > 1000 days just say perm
            TimeSpan remainingTime = this.getRemainingBanTime(aBan);
            if (remainingTime.TotalDays > 1000) {
                banDurationString = "[perm]";
            }
            else {
                banDurationString = "[" + this.FormatTimeString(remainingTime) + "]";
            }
            String sourceNameString = "[" + aBan.ban_record.source_name + "]";
            String banAppendString = ((this._UseBanAppend) ? ("[" + this._BanAppend + "]") : (""));

            //Create the full message
            String fullMessage = aBan.ban_record.record_message + " " + banDurationString + sourceNameString + banAppendString;

            //Trim the kick message if necessary
            Int32 cutLength = fullMessage.Length - 80;
            if (cutLength > 0) {
                String cutReason = aBan.ban_record.record_message.Substring(0, aBan.ban_record.record_message.Length - cutLength);
                fullMessage = cutReason + " " + banDurationString + sourceNameString + banAppendString;
            }
            return fullMessage;
        }

        public void LogWrite(String msg) {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
            if (Slowmo) {
                Thread.Sleep(1000);
            }
        }

        public void ProconChatWrite(String msg) {
            this.ExecuteCommand("procon.protected.chat.write", "AdKats > " + msg);
            if (Slowmo) {
                Thread.Sleep(1000);
            }
        }

        public void ConsoleWrite(String msg, ConsoleMessageType type) {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Normal);
        }

        public void ConsoleWarn(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Warning);
        }

        public void ConsoleError(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Error);
        }

        public void ConsoleSuccess(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Success);
        }

        public void DebugWrite(String msg, Int32 level) {
            if (_DebugLevel >= level) {
                ConsoleWrite(msg, ConsoleMessageType.Normal);
            }
        }

        public void PrintPreparedCommand(MySqlCommand cmd) {
            String query = cmd.Parameters.Cast<MySqlParameter>().Aggregate(cmd.CommandText, (current, p) => current.Replace(p.ParameterName, p.Value.ToString()));

            this.ConsoleWrite(query);
        }

        #endregion

        #region exception handling

        public AdKatsException HandleException(AdKatsException aException) {
            //If it's null, just return
            if (aException == null) {
                this.ConsoleError("Attempted to handle exception when none was given.");
                return null;
            }
            //Always write the exception to console
            this.ConsoleWrite(aException.ToString(), ConsoleMessageType.Exception);
            //Check if the exception attributes to the database
            if (aException.InternalException.GetType() == typeof (System.TimeoutException) || aException.InternalException.ToString().Contains("Unable to connect to any of the specified MySQL hosts")) {
                this.HandleDatabaseConnectionInteruption();
            }
            else {
                //Create the Exception record
                AdKatsRecord record = new AdKatsRecord {
                                                           record_source = AdKatsRecord.Sources.Automated,
                                                           isDebug = true,
                                                           server_id = this._ServerID,
                                                           command_type = this._CommandKeyDictionary["adkats_exception"],
                                                           command_numeric = 0,
                                                           target_name = "AdKats",
                                                           target_player = null,
                                                           source_name = "AdKats",
                                                           record_message = aException.ToString()
                                                       };
                //Process the record
                this.QueueRecordForProcessing(record);
            }
            return aException;
        }

        #endregion

        #region database connection interuption handler

        public void HandleDatabaseConnectionInteruption() {
            //Only handle these errors if all threads are already functioning normally
            if (this._ThreadsReady) {
                this.ConsoleError("Database timeout detected. This is timeout " + (++this._DatabaseTimeouts) + ". Critical disconnect at " + DatabaseTimeoutThreshold + ".");
                //Check for critical state (timeouts > threshold, and last timeout less than 1 minute ago)
                if (this._DatabaseTimeouts >= DatabaseTimeoutThreshold && ((DateTime.UtcNow - this._LastDatabaseTimeout).TotalSeconds < 60)) {
                    try {
                        //If the handler is already alive, return
                        if (this._DisconnectHandlingThread != null && this._DisconnectHandlingThread.IsAlive) {
                            this.DebugWrite("Attempted to start disconnect handling thread when it was already running.", 2);
                            return;
                        }
                        //Create a new thread to handle the disconnect orchestration
                        this._DisconnectHandlingThread = new Thread(new ThreadStart(delegate {
                            try {
                                //Log the time of critical disconnect
                                DateTime criticalDisconnectTime = DateTime.UtcNow;
                                //Immediately disable Stat Logger
                                this.ConsoleError("Database connection in critial failure state. Disabling Stat Logger and putting AdKats in Backup Mode.");
                                this._DatabaseConnectionCriticalState = true;
                                this.ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "False");
                                this.ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLogger", "False");
                                //Sleep 10 seconds
                                Thread.Sleep(10000);
                                //Set resolved
                                Boolean restored = false;
                                //Enter loop to check for database reconnection
                                do {
                                    //If someone manually disables AdKats, exit everything
                                    if (!this._IsEnabled) {
                                        return;
                                    }
                                    //Check if the connection has been restored
                                    restored = this.DebugDatabaseConnectionActive();
                                    if (!restored) {
                                        //Inform the user database still not connectable
                                        this.ConsoleError("Database still not accessible. (" + (DateTime.UtcNow - criticalDisconnectTime).TotalSeconds + " seconds since critical disconnect at " + criticalDisconnectTime.ToShortTimeString() + ".");
                                        //Wait 30 seconds to retry
                                        Thread.Sleep(30000);
                                    }
                                } while (!restored);
                                //Connection has been restored, inform the user
                                this.ConsoleSuccess("Database connection restored, re-enabling Stat Logger and returning AdKats to Normal Mode.");
                                //Reset timeout count
                                this._DatabaseTimeouts = 0;
                                //re-enable AdKats and Stat Logger
                                this._DatabaseConnectionCriticalState = false;
                                this.ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "True");
                                this.ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLogger", "True");

                                lock (this._PlayersMutex) {
                                    //Clear the player dinctionary, causing all players to be fetched from the database again
                                    this._PlayerDictionary.Clear();
                                }

                                //Create the Exception record
                                AdKatsRecord record = new AdKatsRecord {
                                                                            record_source = AdKatsRecord.Sources.Automated,
                                                                            isDebug = true,
                                                                            server_id = this._ServerID,
                                                                            command_type = this._CommandKeyDictionary["adkats_exception"],
                                                                            command_numeric = 0,
                                                                            target_name = "Database",
                                                                            target_player = null,
                                                                            source_name = "AdKats",
                                                                            record_message = "Critical Database Disconnect Handled (" + (DateTime.UtcNow - criticalDisconnectTime).TotalSeconds + " seconds). AdKats on server " + this._ServerID + " functioning normally again."
                                                                        };
                                //Process the record
                                this.QueueRecordForProcessing(record);
                            }
                            catch (Exception) {
                                this.ConsoleError("Error handling database disconnect.");
                            }
                            this.ConsoleSuccess("Exiting Critical Disconnect Handler.");
                        }));

                        //Start the thread
                        this._DisconnectHandlingThread.Start();
                    }
                    catch (Exception) {
                        this.ConsoleError("Error while initializing disconnect handling thread.");
                    }
                }
                else {
                    this._LastDatabaseTimeout = DateTime.UtcNow;
                }
            }
            else {
                this.DebugWrite("Attempted to handle database timeout when threads not running.", 2);
            }
        }

        #endregion

        #region round timer

        public void StartRoundTimer() {
            //Only handle these errors if all threads are already functioning normally
            if (this._IsEnabled && this._ThreadsReady) {
                try {
                    //If the thread is still alive, inform the user and return
                    if (this._RoundTimerThread != null && this._RoundTimerThread.IsAlive) {
                        ConsoleError("Tried to enable a round timer while one was still active.");
                        return;
                    }
                    //Create a new thread to handle the disconnect orchestration
                    this._RoundTimerThread = new Thread(new ThreadStart(delegate {
                        try {
                            this.DebugWrite("starting round timer", 2);
                            Thread.Sleep(3000);
                            Int32 roundTimeSeconds = (Int32) (this._RoundTimeMinutes * 60);
                            for (Int32 secondsRemaining = roundTimeSeconds; secondsRemaining > 0; secondsRemaining--) {
                                if (this._RoundEnded || !this._IsEnabled || !this._ThreadsReady) {
                                    this.ConsoleError("Exiting round timer.");
                                    return;
                                }
                                if (secondsRemaining == roundTimeSeconds - 60 && secondsRemaining > 60) {
                                    this.AdminTellMessage("Round will end automatically in ~" + (Int32) (secondsRemaining / 60.0) + " minutes.");
                                    this.DebugWrite("Round will end automatically in ~" + (Int32) (secondsRemaining / 60.0) + " minutes.", 3);
                                }
                                else if (secondsRemaining == (roundTimeSeconds / 2) && secondsRemaining > 60) {
                                    this.AdminTellMessage("Round will end automatically in ~" + (Int32) (secondsRemaining / 60.0) + " minutes.");
                                    this.DebugWrite("Round will end automatically in ~" + (Int32) (secondsRemaining / 60.0) + " minutes.", 3);
                                }
                                else if (secondsRemaining == 30) {
                                    this.AdminTellMessage("Round ends in 30 seconds. (Current winning team will win)");
                                    this.DebugWrite("Round ends in 30 seconds. (Current winning team will win)", 3);
                                }
                                else if (secondsRemaining == 20) {
                                    this.AdminTellMessage("Round ends in 20 seconds. (Current winning team will win)");
                                    this.DebugWrite("Round ends in 20 seconds. (Current winning team will win)", 3);
                                }
                                else if (secondsRemaining <= 10) {
                                    this.AdminSayMessage("Round ends in..." + secondsRemaining);
                                    this.DebugWrite("Round ends in..." + secondsRemaining, 3);
                                }
                                //Sleep for 1 second
                                Thread.Sleep(1000);
                            }
                            if (this._UsTicketCount < this._RuTicketCount) {
                                this.ExecuteCommand("procon.protected.send", "mapList.endRound", AdKats.RuTeamID + "");
                                this.DebugWrite("Ended Round (" + AdKats.RuTeamID + ")", 4);
                            }
                            else {
                                this.ExecuteCommand("procon.protected.send", "mapList.endRound", AdKats.UsTeamID + "");
                                this.DebugWrite("Ended Round (" + AdKats.UsTeamID + ")", 4);
                            }
                        }
                        catch (Exception e) {
                            this.HandleException(new AdKatsException("Error in round timer thread.", e));
                        }
                        this.DebugWrite("Exiting round timer.", 2);
                    }));

                    //Start the thread
                    this._RoundTimerThread.Start();
                }
                catch (Exception e) {
                    this.HandleException(new AdKatsException("Error starting round timer thread.", e));
                }
            }
        }

        #endregion
    } // end AdKats
} // end namespace PRoConEvents
