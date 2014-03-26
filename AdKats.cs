/* 
 * AdKats - Advanced In-Game Admin and Ban Enforcer for Procon Frostbite.
 * 
 * Copyright 2014 A Different Kind, LLC
 * 
 * AdKats was inspired by the gaming community A Different Kind (ADK). Visit http://www.ADKGamers.com/ for more information.
 *
 * The AdKats Frostbite Plugin is free software: You can redistribute it and/or modify it under the terms of the
 * GNU General Public License as published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version. AdKats is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See the GNU General Public License for more details. To view this license, visit http://www.gnu.org/licenses/.
 * 
 * Code Credit:
 * Modded Levenshtein Distance algorithm from Micovery's InsaneLimits
 * Email System adapted from MorpheusX(AUT)'s "Notify Me!"
 * 
 * Development by ColColonCleaner
 * 
 * AdKats.cs
 * Version 4.2.0.8
 * 26-MAR-2014
 */

using System;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
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
using PRoCon.Core.Utils;


namespace PRoConEvents {
    public class AdKats : PRoConPluginAPI, IPRoConPluginInterface {
        public enum ConsoleMessageType {
            Warning,
            Error,
            Exception,
            Normal,
            Success
        };

        public enum GameVersion {
            BF3,
            BF4
        };

        public enum RoundState {
            Loaded,
            Playing,
            Ended
        }

        private const String PluginVersion = "4.2.0.8";
        private const Boolean FullDebug = false;
        private const Boolean SlowMoOnException = false;
        private const Int32 DbUserFetchFrequency = 300;

        private const Boolean UseConnectionPooling = true;
        private const Int32 MinConnectionPoolSize = 0;
        private const Int32 MaxConnectionPoolSize = 20;
        private const Boolean UseCompressedConnection = false;
        private const Int32 DbActionFetchFrequency = 10;
        private const Int32 DatabaseTimeoutThreshold = 10;
        private const Int32 DatabaseSuccessThreshold = 5;
        private const Int32 DbSettingFetchFrequency = 300;
        private const Int32 DbBanFetchFrequency = 60;
        private readonly Dictionary<String, AdKatsRecord> _ActOnIsAliveDictionary = new Dictionary<String, AdKatsRecord>();
        private readonly Dictionary<String, AdKatsRecord> _ActOnSpawnDictionary = new Dictionary<String, AdKatsRecord>();
        private readonly Dictionary<String, AdKatsRecord> _ActionConfirmDic = new Dictionary<String, AdKatsRecord>();
        private readonly Queue<AdKatsPlayer> _BanEnforcerCheckingQueue = new Queue<AdKatsPlayer>();
        private readonly Queue<AdKatsBan> _BanEnforcerProcessingQueue = new Queue<AdKatsBan>();
        private readonly Queue<CBanInfo> _CBanProcessingQueue = new Queue<CBanInfo>();
        private readonly Dictionary<long, AdKatsCommand> _CommandIDDictionary = new Dictionary<long, AdKatsCommand>();
        private readonly Dictionary<String, AdKatsCommand> _CommandKeyDictionary = new Dictionary<String, AdKatsCommand>();
        private readonly Dictionary<String, AdKatsCommand> _CommandNameDictionary = new Dictionary<String, AdKatsCommand>();
        private readonly Dictionary<String, DateTime> _commandUsageTimes = new Dictionary<string, DateTime>(); 
        private readonly Queue<AdKatsCommand> _CommandRemovalQueue = new Queue<AdKatsCommand>();
        private readonly Dictionary<String, AdKatsCommand> _CommandTextDictionary = new Dictionary<String, AdKatsCommand>();
        private readonly Queue<AdKatsCommand> _CommandUploadQueue = new Queue<AdKatsCommand>();
        private readonly Queue<AdKatsPlayer> _HackerCheckerQueue = new Queue<AdKatsPlayer>();
        private readonly Queue<Kill> _KillProcessingQueue = new Queue<Kill>();
        private readonly Queue<List<CPlayerInfo>> _PlayerListProcessingQueue = new Queue<List<CPlayerInfo>>();
        private readonly Queue<CPlayerInfo> _PlayerRemovalProcessingQueue = new Queue<CPlayerInfo>();
        private readonly DateTime _ProconStartTime = DateTime.UtcNow;

        private readonly List<String> _PunishmentSeverityIndex;
        private readonly Dictionary<long, AdKatsRole> _RoleIDDictionary = new Dictionary<long, AdKatsRole>();
        private readonly Dictionary<String, AdKatsRole> _RoleKeyDictionary = new Dictionary<String, AdKatsRole>();
        private readonly Dictionary<String, AdKatsRole> _RoleNameDictionary = new Dictionary<String, AdKatsRole>();
        private readonly Queue<AdKatsRole> _RoleRemovalQueue = new Queue<AdKatsRole>();
        private readonly Queue<AdKatsRole> _RoleUploadQueue = new Queue<AdKatsRole>();
        private readonly Dictionary<String, Int32> _RoundMutedPlayers = new Dictionary<String, Int32>();
        private readonly Dictionary<String, AdKatsRecord> _RoundReports = new Dictionary<String, AdKatsRecord>();
        private readonly Queue<CPluginVariable> _SettingUploadQueue = new Queue<CPluginVariable>();
        private readonly Queue<CPlayerInfo> _Team1MoveQueue = new Queue<CPlayerInfo>();
        private readonly Queue<CPlayerInfo> _Team2MoveQueue = new Queue<CPlayerInfo>();
        private readonly Queue<CPlayerInfo> _TeamswapForceMoveQueue = new Queue<CPlayerInfo>();
        private readonly Queue<CPlayerInfo> _TeamswapOnDeathCheckingQueue = new Queue<CPlayerInfo>();
        private readonly Dictionary<String, CPlayerInfo> _TeamswapOnDeathMoveDic = new Dictionary<String, CPlayerInfo>();
        private readonly Dictionary<String, bool> _TeamswapRoundWhitelist = new Dictionary<String, bool>();
        private readonly Queue<KeyValuePair<String, String>> _UnparsedCommandQueue = new Queue<KeyValuePair<String, String>>();
        private readonly Queue<KeyValuePair<String, String>> _UnparsedMessageQueue = new Queue<KeyValuePair<String, String>>();
        private readonly Queue<AdKatsRecord> _UnprocessedActionQueue = new Queue<AdKatsRecord>();
        private readonly Queue<AdKatsRecord> _UnprocessedRecordQueue = new Queue<AdKatsRecord>();
        private readonly Queue<AdKatsUser> _UserRemovalQueue = new Queue<AdKatsUser>();
        private readonly Queue<AdKatsUser> _UserUploadQueue = new Queue<AdKatsUser>();
        private readonly EventWaitHandle _WeaponStatsWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private readonly Dictionary<Int32, Thread> _aliveThreads = new Dictionary<Int32, Thread>();
        private readonly MySqlConnectionStringBuilder _dbCommStringBuilder = new MySqlConnectionStringBuilder();
        private readonly MatchCommand _fetchAuthorizedSoldiersMatchCommand;
        private readonly Dictionary<Int64, GameVersion> _gameIDDictionary = new Dictionary<Int64, GameVersion>();
        private readonly MatchCommand _issueCommandMatchCommand;
        private readonly Dictionary<String, AdKatsPlayer> _playerDictionary = new Dictionary<String, AdKatsPlayer>();
        private readonly Dictionary<String, List<AdKatsSpecialPlayer>> _specialPlayerGroupCache = new Dictionary<String, List<AdKatsSpecialPlayer>>();
        private readonly Dictionary<Int32, AdKatsTeam> _teamDictionary = new Dictionary<Int32, AdKatsTeam>();
        private readonly Dictionary<long, AdKatsUser> _userCache = new Dictionary<long, AdKatsUser>();
        public Func<AdKats, AdKatsPlayer, Boolean> AAPerkFunc = ((plugin, aPlayer) => (plugin._EnableAdminAssistantPerk && aPlayer.player_aa));
        private EventWaitHandle _AccessFetchWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _AccessFetchingThread;
        private Thread _ActionHandlingThread;
        private EventWaitHandle _ActionHandlingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _Activator;
        private DateTime _AdKatsStartTime = DateTime.UtcNow;
        private Boolean _AllowAdminSayCommands = true;
        public String[] _AutoReportHandleStrings = {};
        private String _BanAppend = "Appeal at your_site.com";
        private List<AdKatsBan> _BanEnforcerSearchResults = new List<AdKatsBan>();
        private Thread _BanEnforcerThread;
        private EventWaitHandle _BanEnforcerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Boolean _BansQueuing;
        private String _CBanAdminName = "BanEnforcer";
        private Boolean _CombineServerPunishments;
        private Thread _CommandParsingThread;
        private EventWaitHandle _CommandParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private DateTime _CommandStartTime = DateTime.UtcNow;
        private List<String> _CurrentReservedSlotPlayers;
        private RoundState _CurrentRoundState = RoundState.Loaded;
        private List<String> _CurrentSpectatorListPlayers;
        private Thread _DatabaseCommunicationThread;
        private EventWaitHandle _DbCommunicationWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        private volatile Int32 _DebugLevel;
        private String _DebugSoldierName = "ColColonCleaner";
        private Boolean _DefaultEnforceGUID = true;
        private Boolean _DefaultEnforceIP;
        private Boolean _DefaultEnforceName;
        private Thread _DisconnectHandlingThread;
        private Double _DpsTriggerLevel = 50.0;
        private EmailHandler _EmailHandler;
        public Boolean _EnableAdminAssistantPerk = true;
        public Boolean _EnableAdminAssistants = false;
        private String _ExternalCommandAccessKey = "NoPasswordSet";
        private Boolean _FeedMultiBalancerDisperseList;

        private Boolean _FeedMultiBalancerWhitelist;
        private Boolean _FeedMultiBalancerWhitelist_UserCache = true;
        private Boolean _FeedServerReservedSlots;
        private Boolean _FeedServerReservedSlots_UserCache = true;
        private Boolean _FeedServerSpectatorList;
        private Boolean _FeedServerSpectatorList_UserCache = true;
        private Boolean _FeedStatLoggerSettings;
        private Thread _Finalizer;
        private Int64 _GUIDBanCount = -1;
        private String _HackerCheckerDPSBanMessage = "DPS Automatic Ban";
        private String _HackerCheckerHSKBanMessage = "HSK Automatic Ban";
        private String _HackerCheckerKPMBanMessage = "KPM Automatic Ban";
        private Thread _HackerCheckerThread;
        private EventWaitHandle _HackerCheckerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private volatile Int32 _HighestTicketCount;
        private Double _HskTriggerLevel = 60.0;
        private Int64 _IPBanCount = -1;
        private Boolean _IROActive = true;
        private Boolean _IROOverridesLowPop;
        private Int32 _IROTimeout = 10;
        private Thread _KillProcessingThread;
        private EventWaitHandle _KillProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Double _KpmTriggerLevel = 5.0;
        private DateTime _LastBanListCall = DateTime.UtcNow;
        private DateTime _LastDbBanFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _LastGUIDBanCountFetch = DateTime.UtcNow;
        private DateTime _LastIPBanCountFetch = DateTime.UtcNow;
        private DateTime _LastNameBanCountFetch = DateTime.UtcNow;
        private Hashtable _LastStatLoggerStatusUpdate;
        private DateTime _LastStatLoggerStatusUpdateTime = DateTime.UtcNow;
        private DateTime _LastSuccessfulBanList = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private Int32 _LowPopPlayerCount = 20;

        private volatile Int32 _LowestTicketCount = 500000;
        private EventWaitHandle _MessageParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _MessageProcessingThread;
        public Int32 _MinimumRequiredMonthlyReports = 10;
        private Int32 _MutedPlayerChances = 5;
        private String _MutedPlayerKickMessage = "Talking excessively while muted.";
        private String _MutedPlayerKillMessage = "Do not talk while muted. You can speak again next round.";
        private String _MutedPlayerMuteMessage = "You have been muted by an admin, talking will cause punishment. You can speak again next round.";
        private Int64 _NameBanCount = -1;
        private Boolean _OnlyKillOnLowPop = true;
        private DateTime _PermaBanEndTime = DateTime.UtcNow.AddYears(20);
        private EventWaitHandle _PlayerListUpdateWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _PlayerListingThread;
        private EventWaitHandle _PlayerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Boolean _PlayerRoleRefetch;
        private Int32 _PlayersToAutoWhitelist = 2;
        private EventWaitHandle _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private List<String> _PreMessageList;
        private String[] _PunishmentHierarchy = {"kill", "kill", "kick", "tban60", "tban120", "tbanday", "tbanweek", "tban2weeks", "tbanmonth", "ban"};
        private Boolean _RequirePreMessageUse;
        private Int32 _RequiredReasonLength = 4;
        private Dictionary<String, AdKatsPlayer> _RoundCookers = new Dictionary<String, AdKatsPlayer>();
        private EventWaitHandle _RoundEndingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _RoundTimerThread;
        private EventWaitHandle _ServerInfoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        private Double _ServerRulesDelay = 0.5;
        private Double _ServerRulesInterval = 5;
        private String[] _ServerRulesList = {"This server has not set rules yet."};
        private String _ServerVoipAddress = "(TS3) TS.ADKGamers.com:3796";
        private Boolean _ShowAdminNameInSay;
        private StatLibrary _StatLibrary;
        private EventWaitHandle _StatLoggerStatusWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _TeamSwapThread;
        private Int32 _TeamSwapTicketWindowHigh = 500000;
        private Int32 _TeamSwapTicketWindowLow;
        private EventWaitHandle _TeamswapWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Boolean _ToldCol;
        public Boolean _UseAAReportAutoHandler = false;
        private Boolean _UseBanAppend;
        private Boolean _UseBanEnforcer;
        private Boolean _UseBanEnforcerPreviousState;
        private Boolean _UseDpsChecker;
        private Boolean _UseEmail;
        private Boolean _UseGrenadeCookCatcher;
        private Boolean _UseHackerChecker;
        private Boolean _UseHskChecker;
        private Boolean _UseKpmChecker;
        private Boolean _UseWeaponLimiter;
        private Boolean _UsingAwa;
        private Boolean _WeaponCodesTableConfirmed;
        private Boolean _WeaponCodesTableTested;
        private String _WeaponLimiterExceptionString = "_Flechette|_Slug";
        private String _WeaponLimiterString = "M320|RPG|SMAW|C4|M67|Claymore|FGM-148|FIM92|ROADKILL|Death|_LVG|_HE|_Frag|_XM25|_FLASH|_V40|_M34|_Flashbang|_SMK|_Smoke|_FGM148|_Grenade|_SLAM|_NLAW|_RPG7|_C4|_Claymore|_FIM92|_M67|_SMAW|_SRAW|_Sa18IGLA|_Tomahawk";
        private Int32 _YellDuration = 5;
        private Dictionary<String, Double> _commandSourceReputationDictionary;
        private Dictionary<String, Double> _commandTargetReputationDictionary;
        private Boolean _commanderEnabled;
        private Boolean _databaseConnectionCriticalState;
        private Int32 _databaseSuccess;
        private Int32 _databaseTimeouts;
        private volatile Boolean _dbSettingsChanged = true;
        private Boolean _fairFightEnabled;
        private Boolean _fetchActionsFromDb;
        private volatile Boolean _fetchedPluginInformation;
        private Boolean _firstPlayerListComplete;
        private Boolean _forceReloadWholeMags;
        private Int32 _gameID = -1;
        private String _gamePatchVersion = "UNKNOWN";
        private GameVersion _gameVersion = GameVersion.BF3;
        private Boolean _hitIndicatorEnabled;
        private Boolean _isTestingAuthorized;
        private DateTime _lastDatabaseTimeout = DateTime.UtcNow;
        private DateTime _lastDbActionFetch = DateTime.UtcNow;
        private DateTime _lastDbSettingFetch = DateTime.UtcNow;
        private DateTime _lastSuccessfulPlayerList = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastUpdateSettingRequest = DateTime.UtcNow;
        private DateTime _lastUserFetch = DateTime.UtcNow;
        private Int32 _maxSpectators = -1;
        private String _mySqlDatabaseName = "";
        private String _mySqlHostname = "";
        private String _mySqlPassword = "";
        private String _mySqlPort = "";
        private String _mySqlUsername = "";
        private volatile String _pluginChangelog;
        private volatile String _pluginDescription;
        private volatile Boolean _pluginEnabled;
        private volatile String _pluginVersionStatus;
        private Double _roundTimeMinutes = 30;
        private Int64 _serverGroup = -1;
        private Int64 _serverID = -1;
        private String _serverIP;
        private CServerInfo _serverInfo = new CServerInfo();
        private String _serverName;
        private String _serverType = "UNKNOWN";
        private Int64 _settingImportID = -1;
        private Boolean _settingsFetched;
        private Boolean _settingsLocked;
        private String _settingsPassword;
        private Boolean _slowmo;
        private String _statLoggerVersion = "BF3";
        private volatile Boolean _threadsReady;
        private Boolean _useExperimentalTools;
        private volatile Boolean _useKeepAlive;
        private Boolean _useRoundTimer;


        public AdKats() {
            //Set defaults for webclient
            System.Net.ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            _pluginEnabled = false;
            _threadsReady = false;
            //Assign the match commands
            _issueCommandMatchCommand = new MatchCommand("AdKats", "IssueCommand", new List<String>(), "AdKats_IssueCommand", new List<MatchArgumentFormat>(), new ExecutionRequirements(ExecutionScope.None), "Useable by other plugins to call AdKats commands.");
            _fetchAuthorizedSoldiersMatchCommand = new MatchCommand("AdKats", "FetchAuthorizedSoldiers", new List<String>(), "AdKats_FetchAuthorizedSoldiers", new List<MatchArgumentFormat>(), new ExecutionRequirements(ExecutionScope.None), "Useable by other plugins to fetch authorized soldiers.");
            //Debug level is 0 by default
            _DebugLevel = 0;
            //Randomize the external access key
            _ExternalCommandAccessKey = AdKats.GetRandom32BitHashCode();

            //Init the punishment severity index
            _PunishmentSeverityIndex = new List<String> {
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
            _PreMessageList = new List<String> {
                "US TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.",
                "RU TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.",
                "US TEAM: DO NOT ENTER THE STREETS BEYOND 'A', YOU WILL BE PUNISHED.",
                "RU TEAM: DO NOT GO BEYOND THE BLACK LINE ON CEILING BY 'C' FLAG, YOU WILL BE PUNISHED.",
                "THIS SERVER IS NO EXPLOSIVES, YOU WILL BE PUNISHED FOR INFRACTIONS.",
                "JOIN OUR TEAMSPEAK AT TS.ADKGAMERS.COM:3796"
            };

            //Fetch the plugin description and changelog
            FetchPluginDescAndChangelog();

            //Prepare the keep-alive
            SetupKeepAlive();
        }

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
            if (!_fetchedPluginInformation) {
                //Wait up to 10 seconds for the description to fetch
                DebugWrite("Waiting for plugin description...", 1);
                if (!_PluginDescriptionWaitHandle.WaitOne(10000)) {
                    ConsoleError("Unable to fetch plugin description.");
                }
            }

            //Parse out the descriptions
            String concat = String.Empty;
            if (!String.IsNullOrEmpty(_pluginVersionStatus)) {
                concat += _pluginVersionStatus;
            }
            if (!String.IsNullOrEmpty(_pluginDescription)) {
                concat += _pluginDescription;
            }
            if (!String.IsNullOrEmpty(_pluginChangelog)) {
                concat += _pluginChangelog;
            }

            //Check if the description fetched
            if (String.IsNullOrEmpty(concat)) {
                concat = "Plugin description failed to download. Please visit AdKats on github to view the plugin description.";
            }

            return concat;
        }

        public List<CPluginVariable> GetDisplayPluginVariables() {
            try {
                var lstReturn = new List<CPluginVariable>();
                const string separator = " | ";

                if (_settingsLocked) {
                    lstReturn.Add(new CPluginVariable("1. Server Settings|Unlock Settings", typeof (String), ""));
                }
                else {
                    lstReturn.Add(new CPluginVariable(String.IsNullOrEmpty(_settingsPassword) ? ("1. Server Settings|Lock Settings - Create Password") : ("1. Server Settings|Lock Settings"), typeof (String), ""));
                }

                //Only fetch the following settings when plugin disabled
                if (!_threadsReady) {
                    if (!_settingsLocked) {
                        if (_useKeepAlive) {
                            lstReturn.Add(new CPluginVariable("0. Instance Settings|Auto-Enable/Keep-Alive", typeof (Boolean), true));
                        }

                        lstReturn.Add(new CPluginVariable("Complete these settings before enabling.", typeof (String), "Once enabled, more settings will appear."));
                        //SQL Settings
                        lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Hostname", typeof (String), _mySqlHostname));
                        lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Port", typeof (String), _mySqlPort));
                        lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Database", typeof (String), _mySqlDatabaseName));
                        lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Username", typeof (String), _mySqlUsername));
                        lstReturn.Add(new CPluginVariable("1. MySQL Settings|MySQL Password", typeof (String), _mySqlPassword));
                    }
                    //Debugging Settings
                    lstReturn.Add(new CPluginVariable("2. Debugging|Debug level", typeof (Int32), _DebugLevel));
                }
                else {
                    if (_settingsLocked) {
                        lstReturn.Add(new CPluginVariable("1. Server Settings|Server ID (Display)", typeof (int), _serverID));
                        lstReturn.Add(new CPluginVariable("1. Server Settings|Server IP (Display)", typeof (String), _serverIP));
                        if (_UseBanEnforcer) {
                            const string banManagementPrefix = "A13-3. Mini Ban Management|";
                            lstReturn.Add(new CPluginVariable(banManagementPrefix + "NAME Ban Count", typeof (int), _NameBanCount));
                            lstReturn.Add(new CPluginVariable(banManagementPrefix + "GUID Ban Count", typeof (int), _GUIDBanCount));
                            lstReturn.Add(new CPluginVariable(banManagementPrefix + "IP Ban Count", typeof (int), _IPBanCount));
                            lstReturn.Add(new CPluginVariable(banManagementPrefix + "Ban Search", typeof (String), ""));
                            lstReturn.AddRange(_BanEnforcerSearchResults.Select(aBan => new CPluginVariable(banManagementPrefix + "BAN" + aBan.ban_id + separator + aBan.ban_record.target_player.player_name + separator + aBan.ban_record.source_name + separator + aBan.ban_record.record_message, "enum.commandActiveEnum(Active|Disabled|Expired)", aBan.ban_status)));
                        }
                        lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof (int), _DebugLevel));
                        lstReturn.Add(new CPluginVariable("D99. Debugging|Debug Soldier Name", typeof (String), _DebugSoldierName));
                        lstReturn.Add(new CPluginVariable("D99. Debugging|Command Entry", typeof (String), ""));
                        return lstReturn;
                    }
                    //Auto-Enable Settings
                    lstReturn.Add(new CPluginVariable("0. Instance Settings|Auto-Enable/Keep-Alive", typeof (Boolean), _useKeepAlive));

                    //SQL Settings
                    lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Hostname", typeof (String), _mySqlHostname));
                    lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Port", typeof (String), _mySqlPort));
                    lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Database", typeof (String), _mySqlDatabaseName));
                    lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Username", typeof (String), _mySqlUsername));
                    lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Password", typeof (String), _mySqlPassword));

                    //Punishment Settings
                    lstReturn.Add(new CPluginVariable("7. Punishment Settings|Punishment Hierarchy", typeof (String[]), _PunishmentHierarchy));
                    lstReturn.Add(new CPluginVariable("7. Punishment Settings|Combine Server Punishments", typeof (Boolean), _CombineServerPunishments));
                    lstReturn.Add(new CPluginVariable("7. Punishment Settings|Only Kill Players when Server in low population", typeof (Boolean), _OnlyKillOnLowPop));
                    if (_OnlyKillOnLowPop) {
                        lstReturn.Add(new CPluginVariable("7. Punishment Settings|Low Population Value", typeof (int), _LowPopPlayerCount));
                    }
                    lstReturn.Add(new CPluginVariable("7. Punishment Settings|Use IRO Punishment", typeof (Boolean), _IROActive));
                    if (_IROActive) {
                        lstReturn.Add(new CPluginVariable("7. Punishment Settings|IRO Timeout Minutes", typeof (Int32), _IROTimeout));
                        lstReturn.Add(new CPluginVariable("7. Punishment Settings|IRO Punishment Overrides Low Pop", typeof (Boolean), _IROOverridesLowPop));
                    }

                    //Email Settings
                    lstReturn.Add(new CPluginVariable("8. Email Settings|Send Emails", typeof (bool), _UseEmail));
                    if (_UseEmail) {
                        lstReturn.Add(new CPluginVariable("8. Email Settings|Use SSL?", typeof (Boolean), _EmailHandler.UseSSL));
                        lstReturn.Add(new CPluginVariable("8. Email Settings|SMTP-Server address", typeof (String), _EmailHandler.SMTPServer));
                        lstReturn.Add(new CPluginVariable("8. Email Settings|SMTP-Server port", typeof (int), _EmailHandler.SMTPPort));
                        lstReturn.Add(new CPluginVariable("8. Email Settings|Sender address", typeof (String), _EmailHandler.SenderEmail));
                        lstReturn.Add(new CPluginVariable("8. Email Settings|SMTP-Server username", typeof (String), _EmailHandler.SMTPUser));
                        lstReturn.Add(new CPluginVariable("8. Email Settings|SMTP-Server password", typeof (String), _EmailHandler.SMTPPassword));
                        lstReturn.Add(new CPluginVariable("8. Email Settings|Custom HTML Addition", typeof (String), _EmailHandler.CustomHTMLAddition));
                        lstReturn.Add(new CPluginVariable("8. Email Settings|Extra Recipient Email Addresses", typeof (String[]), _EmailHandler.RecipientEmails.ToArray()));
                    }

                    //TeamSwap Settings
                    lstReturn.Add(new CPluginVariable("9. TeamSwap Settings|Ticket Window High", typeof (int), _TeamSwapTicketWindowHigh));
                    lstReturn.Add(new CPluginVariable("9. TeamSwap Settings|Ticket Window Low", typeof (int), _TeamSwapTicketWindowLow));

                    //Admin Assistant Settings
                    lstReturn.Add(new CPluginVariable("A10. Admin Assistant Settings|Enable Admin Assistants", typeof (Boolean), _EnableAdminAssistants));
                    if (_EnableAdminAssistants) {
                        lstReturn.Add(new CPluginVariable("A10. Admin Assistant Settings|Minimum Confirmed Reports Per Month", typeof (int), _MinimumRequiredMonthlyReports));
                        lstReturn.Add(new CPluginVariable("A10. Admin Assistant Settings|Enable Admin Assistant Perk", typeof (Boolean), _EnableAdminAssistantPerk));
                        lstReturn.Add(new CPluginVariable("A10. Admin Assistant Settings|Use AA Report Auto Handler", typeof (Boolean), _UseAAReportAutoHandler));
                        if (_UseAAReportAutoHandler) {
                            lstReturn.Add(new CPluginVariable("A10. Admin Assistant Settings|Auto-Report-Handler Strings", typeof (String[]), _AutoReportHandleStrings));
                        }
                    }

                    //Muting Settings
                    lstReturn.Add(new CPluginVariable("A11. Player Mute Settings|On-Player-Muted Message", typeof (String), _MutedPlayerMuteMessage));
                    lstReturn.Add(new CPluginVariable("A11. Player Mute Settings|On-Player-Killed Message", typeof (String), _MutedPlayerKillMessage));
                    lstReturn.Add(new CPluginVariable("A11. Player Mute Settings|On-Player-Kicked Message", typeof (String), _MutedPlayerKickMessage));
                    lstReturn.Add(new CPluginVariable("A11. Player Mute Settings|# Chances to give player before kicking", typeof (int), _MutedPlayerChances));

                    //Message Settings
                    lstReturn.Add(new CPluginVariable("A12. Messaging Settings|Display Admin Name in Kick and Ban Announcement", typeof (Boolean), _ShowAdminNameInSay));
                    lstReturn.Add(new CPluginVariable("A12. Messaging Settings|Yell display time seconds", typeof (int), _YellDuration));
                    lstReturn.Add(new CPluginVariable("A12. Messaging Settings|Pre-Message List", typeof (String[]), _PreMessageList.ToArray()));
                    lstReturn.Add(new CPluginVariable("A12. Messaging Settings|Require Use of Pre-Messages", typeof (Boolean), _RequirePreMessageUse));

                    //Ban Settings
                    lstReturn.Add(new CPluginVariable("A13. Banning Settings|Use Additional Ban Message", typeof (Boolean), _UseBanAppend));
                    if (_UseBanAppend) {
                        lstReturn.Add(new CPluginVariable("A13. Banning Settings|Additional Ban Message", typeof (String), _BanAppend));
                    }
                    lstReturn.Add(new CPluginVariable("A13. Banning Settings|Procon Ban Admin Name", typeof (String), _CBanAdminName));
                    const string banEnforcerPrefix = "A13-2. Ban Enforcer Settings|";
                    if (!_UsingAwa) {
                        lstReturn.Add(new CPluginVariable(banEnforcerPrefix + "Use Ban Enforcer", typeof (Boolean), _UseBanEnforcer));
                    }
                    if (_UseBanEnforcer) {
                        lstReturn.Add(new CPluginVariable(banEnforcerPrefix + "Enforce New Bans by NAME", typeof (Boolean), _DefaultEnforceName));
                        lstReturn.Add(new CPluginVariable(banEnforcerPrefix + "Enforce New Bans by GUID", typeof (Boolean), _DefaultEnforceGUID));
                        lstReturn.Add(new CPluginVariable(banEnforcerPrefix + "Enforce New Bans by IP", typeof (Boolean), _DefaultEnforceIP));
                    }

                    //External Command Settings
                    lstReturn.Add(new CPluginVariable("A14. External Command Settings|HTTP External Access Key", typeof (String), _ExternalCommandAccessKey));
                    if (!_UseBanEnforcer && !_UsingAwa) {
                        lstReturn.Add(new CPluginVariable("A14. External Command Settings|Fetch Actions from Database", typeof (Boolean), _fetchActionsFromDb));
                    }

                    lstReturn.Add(new CPluginVariable("A15. VOIP Settings|Server VOIP Address", typeof (String), _ServerVoipAddress));

                    lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Feed MULTIBalancer Whitelist", typeof (Boolean), _FeedMultiBalancerWhitelist));
                    if (_FeedMultiBalancerWhitelist) {
                        lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Automatic MULTIBalancer Whitelist for Admins", typeof (Boolean), _FeedMultiBalancerWhitelist_UserCache));
                    }
                    lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Feed MULTIBalancer Even Dispersion List", typeof (Boolean), _FeedMultiBalancerDisperseList));
                    lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Feed Server Reserved Slots", typeof (Boolean), _FeedServerReservedSlots));
                    if (_FeedServerReservedSlots) {
                        lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Automatic Reserved Slot for User Cache", typeof (Boolean), _FeedServerReservedSlots_UserCache));
                    }
                    lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Feed Server Spectator List", typeof (Boolean), _FeedServerSpectatorList));
                    if (_FeedServerSpectatorList) {
                        lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Automatic Spectator Slot for User Cache", typeof (Boolean), _FeedServerReservedSlots_UserCache));
                    }
                    lstReturn.Add(new CPluginVariable("A16. Orchestration Settings|Feed Stat Logger Settings", typeof (Boolean), _FeedStatLoggerSettings));

                    lstReturn.Add(new CPluginVariable("A17. Round Settings|Round Timer: Enable", typeof (Boolean), _useRoundTimer));
                    if (_useRoundTimer) {
                        lstReturn.Add(new CPluginVariable("A17. Round Settings|Round Timer: Round Duration Minutes", typeof (Double), _roundTimeMinutes));
                    }

                    lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: Enable", typeof (Boolean), _UseHackerChecker));
                    if (_UseHackerChecker) {
                        if (_isTestingAuthorized) {
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|Hacker-Check Player", typeof (String), ""));
                        }
                        //lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: Whitelist", typeof (String[]), _HackerCheckerWhitelist));
                        lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: DPS Checker: Enable", typeof (Boolean), _UseDpsChecker));
                        if (_UseDpsChecker) {
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: DPS Checker: Trigger Level", typeof (Double), _DpsTriggerLevel));
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: DPS Checker: Ban Message", typeof (String), _HackerCheckerDPSBanMessage));
                        }
                        lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: HSK Checker: Enable", typeof (Boolean), _UseHskChecker));
                        if (_UseHskChecker) {
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: HSK Checker: Trigger Level", typeof (Double), _HskTriggerLevel));
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: HSK Checker: Ban Message", typeof (String), _HackerCheckerHSKBanMessage));
                        }
                        lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: KPM Checker: Enable", typeof (Boolean), _UseKpmChecker));
                        if (_UseKpmChecker) {
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: KPM Checker: Trigger Level", typeof (Double), _KpmTriggerLevel));
                            lstReturn.Add(new CPluginVariable("A18. Internal Hacker-Checker Settings|HackerChecker: KPM Checker: Ban Message", typeof (String), _HackerCheckerKPMBanMessage));
                        }
                    }

                    //Server rules Settings
                    lstReturn.Add(new CPluginVariable("A19. Server Rules Settings|Rule Print Delay", typeof (Double), _ServerRulesDelay));
                    lstReturn.Add(new CPluginVariable("A19. Server Rules Settings|Rule Print Interval", typeof (Double), _ServerRulesInterval));
                    lstReturn.Add(new CPluginVariable("A19. Server Rules Settings|Server Rule List", typeof (String[]), _ServerRulesList));

                    //Debug settings
                    lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof (int), _DebugLevel));
                    lstReturn.Add(new CPluginVariable("D99. Debugging|Debug Soldier Name", typeof (String), _DebugSoldierName));
                    lstReturn.Add(new CPluginVariable("D99. Debugging|Command Entry", typeof (String), ""));

                    //Experimental tools
                    if (_isTestingAuthorized) {
                        lstReturn.Add(new CPluginVariable("X99. Experimental|Use Experimental Tools", typeof (Boolean), _useExperimentalTools));
                        if (_useExperimentalTools) {
                            lstReturn.Add(new CPluginVariable("X99. Experimental|Send Query", typeof (String), ""));
                            lstReturn.Add(new CPluginVariable("X99. Experimental|Send Non-Query", typeof (String), ""));
                            lstReturn.Add(new CPluginVariable("X99. Experimental|Use NO EXPLOSIVES Limiter", typeof (Boolean), _UseWeaponLimiter));
                            if (_UseWeaponLimiter) {
                                lstReturn.Add(new CPluginVariable("X99. Experimental|NO EXPLOSIVES Weapon String", typeof (String), _WeaponLimiterString));
                                lstReturn.Add(new CPluginVariable("X99. Experimental|NO EXPLOSIVES Exception String", typeof (String), _WeaponLimiterExceptionString));
                            }
                            lstReturn.Add(new CPluginVariable("X99. Experimental|Use Grenade Cook Catcher", typeof (Boolean), _UseGrenadeCookCatcher));
                        }
                    }

                    //Server Settings
                    lstReturn.Add(new CPluginVariable("1. Server Settings|Setting Import", typeof (String), _serverID));
                    lstReturn.Add(new CPluginVariable("1. Server Settings|Server ID (Display)", typeof (int), _serverID));
                    lstReturn.Add(new CPluginVariable("1. Server Settings|Server IP (Display)", typeof (String), _serverIP));
                    if (!_UsingAwa) {
                        const string userSettingsPrefix = "3. User Settings|";
                        //User Settings
                        lstReturn.Add(new CPluginVariable(userSettingsPrefix + "Add User", typeof (String), ""));
                        if (_userCache.Count > 0) {
                            //Sort access list by access level, then by id
                            List<AdKatsUser> tempAccess = _userCache.Values.ToList();
                            tempAccess.Sort((a1, a2) => (a1.user_role.role_id == a2.user_role.role_id) ? (System.String.CompareOrdinal(a1.user_name, a2.user_name)) : ((a1.user_role.role_id < a2.user_role.role_id) ? (-1) : (1)));
                            String roleEnum = String.Empty;
                            if (_RoleKeyDictionary.Count > 0) {
                                var random = new Random();
                                foreach (AdKatsRole role in _RoleKeyDictionary.Values) {
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
                                if (_UseEmail) {
                                    lstReturn.Add(new CPluginVariable(userPrefix + "User Email", typeof (String), user.user_email));
                                }
                                //Do not display phone input until that operation is available for use
                                //lstReturn.Add(new CPluginVariable(userPrefix + "User Phone", typeof(String), user.user_phone));
                                lstReturn.Add(new CPluginVariable(userPrefix + "User Role", roleEnum, user.user_role.role_name));
                                lstReturn.Add(new CPluginVariable(userPrefix + "Delete User?", typeof (String), ""));
                                lstReturn.Add(new CPluginVariable(userPrefix + "Add Soldier?", typeof (String), ""));
                                String soldierPrefix = userPrefix + "Soldiers" + separator;
                                lstReturn.AddRange(user.soldierDictionary.Values.Select(aPlayer => new CPluginVariable(soldierPrefix + aPlayer.player_id + separator + _gameIDDictionary[aPlayer.game_id] + separator + aPlayer.player_name + separator + "Delete Soldier?", typeof (String), "")));
                            }
                        }
                        else {
                            lstReturn.Add(new CPluginVariable(userSettingsPrefix + "No Users in User List", typeof (String), "Add Users with 'Add User'."));
                        }


                        lstReturn.Add(new CPluginVariable("5. Command Settings|Minimum Required Reason Length", typeof (int), _RequiredReasonLength));
                        lstReturn.Add(new CPluginVariable("5. Command Settings|Allow Commands from Admin Say", typeof (Boolean), _AllowAdminSayCommands));

                        //Role Settings
                        const string roleListPrefix = "4. Role Settings|";
                        lstReturn.Add(new CPluginVariable(roleListPrefix + "Add Role", typeof (String), ""));
                        if (_RoleKeyDictionary.Count > 0) {
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_RoleIDDictionary) {
                                foreach (AdKatsRole aRole in _RoleKeyDictionary.Values) {
                                    if (_isTestingAuthorized)
                                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                    lock (_CommandNameDictionary) {
                                        String rolePrefix = roleListPrefix + "RLE" + aRole.role_id + separator + aRole.role_name + separator;
                                        lstReturn.AddRange(from aCommand in _CommandNameDictionary.Values where aCommand.command_active == AdKatsCommand.CommandActive.Active select new CPluginVariable(rolePrefix + "CDE" + aCommand.command_id + separator + aCommand.command_name, "enum.roleAllowCommandEnum(Allow|Deny)", aRole.RoleAllowedCommands.ContainsKey(aCommand.command_key) ? ("Allow") : ("Deny")));
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
                        if (_CommandNameDictionary.Count > 0) {
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_CommandIDDictionary) {
                                foreach (AdKatsCommand command in _CommandIDDictionary.Values) {
                                    if (command.command_active != AdKatsCommand.CommandActive.Invisible) {
                                        String commandPrefix = commandListPrefix + "CDE" + command.command_id + separator + command.command_name + separator;
                                        lstReturn.Add(new CPluginVariable(commandPrefix + "Active", "enum.commandActiveEnum(Active|Disabled)", command.command_active.ToString()));
                                        if (command.command_active != AdKatsCommand.CommandActive.Disabled) {
                                            if (command.command_logging != AdKatsCommand.CommandLogging.Mandatory && command.command_logging != AdKatsCommand.CommandLogging.Unable) {
                                                lstReturn.Add(new CPluginVariable(commandPrefix + "Logging", "enum.commandLoggingEnum(Log|Ignore)", command.command_logging.ToString()));
                                            }
                                            lstReturn.Add(new CPluginVariable(commandPrefix + "Text", typeof (String), command.command_text));
                                        }
                                    }
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

                    if (_UseBanEnforcer) {
                        const string banManagementPrefix = "A13-3. Mini Ban Management|";
                        lstReturn.Add(new CPluginVariable(banManagementPrefix + "NAME Ban Count", typeof (int), _NameBanCount));
                        lstReturn.Add(new CPluginVariable(banManagementPrefix + "GUID Ban Count", typeof (int), _GUIDBanCount));
                        lstReturn.Add(new CPluginVariable(banManagementPrefix + "IP Ban Count", typeof (int), _IPBanCount));
                        lstReturn.Add(new CPluginVariable(banManagementPrefix + "Ban Search", typeof (String), ""));
                        lstReturn.AddRange(_BanEnforcerSearchResults.Select(aBan => new CPluginVariable(banManagementPrefix + "BAN" + aBan.ban_id + separator + aBan.ban_record.target_player.player_name + separator + aBan.ban_record.source_name + separator + aBan.ban_record.record_message, "enum.commandActiveEnum(Active|Disabled|Expired)", aBan.ban_status)));
                    }
                }
                return lstReturn;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching display vars.", e));
                return new List<CPluginVariable>();
            }
        }

        public List<CPluginVariable> GetPluginVariables() {
            var lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("1. Server Settings|Settings Locked", typeof (Boolean), _settingsLocked, true));
            lstReturn.Add(new CPluginVariable("2. Server Settings|Settings Password", typeof (String), _settingsPassword));

            lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Hostname", typeof (String), _mySqlHostname));
            lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Port", typeof (String), _mySqlPort));
            lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Database", typeof (String), _mySqlDatabaseName));
            lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Username", typeof (String), _mySqlUsername));
            lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Password", typeof (String), _mySqlPassword));

            lstReturn.Add(new CPluginVariable("3. Debugging|Debug level", typeof (Int32), _DebugLevel));
            return lstReturn;
        }

        public void SetPluginVariable(String strVariable, String strValue) {
            if (strValue == null) {
                return;
            }
            try {
                if (strVariable == "UpdateSettings") {
                    //Do nothing. Settings page will be updated after return.
                }
                else if (Regex.Match(strVariable, @"Auto-Enable/Keep-Alive").Success) {
                    Boolean autoEnable = Boolean.Parse(strValue);
                    if (autoEnable != _useKeepAlive) {
                        if (autoEnable)
                            Enable();
                        _useKeepAlive = autoEnable;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Enable/Keep-Alive", typeof (Boolean), _useKeepAlive));
                    }
                }
                else if (Regex.Match(strVariable, @"Unlock Settings").Success) {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 5) {
                        return;
                    }
                    if (strValue != _settingsPassword) {
                        ConsoleError("Password incorrect.");
                        return;
                    }
                    _settingsLocked = false;
                    ConsoleSuccess("Settings unlocked.");
                    QueueSettingForUpload(new CPluginVariable(@"Settings Locked", typeof (Boolean), _settingsLocked));
                }
                else if (Regex.Match(strVariable, @"Lock Settings - Create Password").Success) {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 5) {
                        ConsoleError("Password had invalid format/length, unable to submit.");
                        return;
                    }
                    _settingsPassword = strValue;
                    _settingsLocked = true;
                    ConsoleSuccess("Password created. Settings Locked.");
                    QueueSettingForUpload(new CPluginVariable(@"Settings Password", typeof (String), _settingsPassword));
                    QueueSettingForUpload(new CPluginVariable(@"Settings Locked", typeof (Boolean), _settingsLocked));
                }
                else if (Regex.Match(strVariable, @"Lock Settings").Success) {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 5) {
                        return;
                    }
                    if (strValue != _settingsPassword) {
                        ConsoleError("Password incorrect.");
                        return;
                    }
                    _settingsLocked = true;
                    ConsoleSuccess("Settings locked.");
                    QueueSettingForUpload(new CPluginVariable(@"Settings Locked", typeof (Boolean), _settingsLocked));
                }
                else if (Regex.Match(strVariable, @"Settings Password").Success) {
                    if (String.IsNullOrEmpty(strValue) || strValue.Length < 5) {
                        return;
                    }
                    _settingsPassword = strValue;
                }
                else if (Regex.Match(strVariable, @"Settings Locked").Success) {
                    _settingsLocked = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Send Query").Success) {
                    SendQuery(strValue, true);
                }
                else if (Regex.Match(strVariable, @"Send Non-Query").Success) {
                    SendNonQuery("Experimental Query", strValue, true);
                }
                else if (Regex.Match(strVariable, @"Hacker-Check Player").Success) {
                    //Create new thread to run hack check
                    var statCheckingThread = new Thread(new ThreadStart(delegate {
                        try {
                            Thread.CurrentThread.Name = "SpecialHackerCheck";
                            if (String.IsNullOrEmpty(strValue) || !_threadsReady) {
                                return;
                            }
                            ConsoleWarn("Preparing to hacker check " + strValue);
                            if (String.IsNullOrEmpty(strValue) || strValue.Length < 3) {
                                ConsoleError("Player name must be at least 3 characters long.");
                                return;
                            }
                            if (!SoldierNameValid(strValue)) {
                                ConsoleError("Player name contained invalid characters.");
                                return;
                            }
                            var aPlayer = new AdKatsPlayer {
                                player_name = strValue
                            };
                            FetchPlayerStats(aPlayer);
                            if (aPlayer.stats != null) {
                                RunStatSiteHackCheck(aPlayer, true);
                            }
                            else {
                                ConsoleError("Stats not found for " + strValue);
                            }
                        }
                        catch (Exception e) {
                            HandleException(new AdKatsException("Error while manual stat checking player.", e));
                        }
                        LogThreadExit();
                    }));
                    //Start the thread
                    StartAndLogThread(statCheckingThread);
                }
                else if (Regex.Match(strVariable, @"Setting Import").Success) {
                    Int32 tmp = -1;
                    if (int.TryParse(strValue, out tmp)) {
                        if (tmp != -1)
                            QueueSettingImport(tmp);
                    }
                    else {
                        ConsoleError("Invalid Input for Setting Import");
                    }
                }
                else if (Regex.Match(strVariable, @"Using AdKats WebAdmin").Success) {
                    Boolean tmp = false;
                    if (Boolean.TryParse(strValue, out tmp)) {
                        _UsingAwa = tmp;

                        //Update necessary settings for AWA use
                        if (_UsingAwa) {
                            _UseBanEnforcer = true;
                            _fetchActionsFromDb = true;
                            _DbCommunicationWaitHandle.Set();
                        }
                    }
                    else {
                        ConsoleError("Invalid Input for Using AdKats WebAdmin");
                    }
                }

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
                    else {
                        ConsoleError("Invalid command format.");
                        return;
                    }
                    var record = new AdKatsRecord {
                        record_source = AdKatsRecord.Sources.Settings,
                        source_name = "SettingsAdmin"
                    };
                    CompleteRecordInformation(record, strValue);
                }
                else if (Regex.Match(strVariable, @"Debug level").Success) {
                    Int32 tmp;
                    if (int.TryParse(strValue, out tmp)) {
                        if (tmp == 23548) {
                            Boolean wasAuth = _isTestingAuthorized;
                            _isTestingAuthorized = true;
                            if (!wasAuth) {
                                ConsoleWrite("Server is priviledged for testing during this instance.");
                            }
                        }
                        else if (tmp != _DebugLevel) {
                            _DebugLevel = tmp;
                            //Once setting has been changed, upload the change to database
                            QueueSettingForUpload(new CPluginVariable(@"Debug level", typeof (int), _DebugLevel));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Debug Soldier Name").Success) {
                    if (SoldierNameValid(strValue)) {
                        if (strValue != _DebugSoldierName) {
                            _DebugSoldierName = strValue;
                            //Once setting has been changed, upload the change to database
                            QueueSettingForUpload(new CPluginVariable(@"Debug Soldier Name", typeof (String), _DebugSoldierName));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Server VOIP Address").Success) {
                    if (strValue != _ServerVoipAddress) {
                        _ServerVoipAddress = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Server VOIP Address", typeof (String), _ServerVoipAddress));
                    }
                }
                else if (Regex.Match(strVariable, @"Rule Print Delay").Success) {
                    Double delay = Double.Parse(strValue);
                    if (_ServerRulesDelay != delay) {
                        if (delay <= 0) {
                            ConsoleError("Delay cannot be negative.");
                            delay = 1.0;
                        }
                        _ServerRulesDelay = delay;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Rule Print Delay", typeof (Double), _ServerRulesDelay));
                    }
                }
                else if (Regex.Match(strVariable, @"Rule Print Interval").Success) {
                    Double interval = Double.Parse(strValue);
                    if (_ServerRulesInterval != interval) {
                        if (interval <= 0) {
                            ConsoleError("Interval cannot be negative.");
                            interval = 5.0;
                        }
                        _ServerRulesInterval = interval;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Rule Print Interval", typeof (Double), _ServerRulesInterval));
                    }
                }
                else if (Regex.Match(strVariable, @"Server Rule List").Success) {
                    _ServerRulesList = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Server Rule List", typeof (String), CPluginVariable.EncodeStringArray(_ServerRulesList)));
                }
                else if (Regex.Match(strVariable, @"Feed MULTIBalancer Whitelist").Success) {
                    Boolean feedMTBWhite = Boolean.Parse(strValue);
                    if (feedMTBWhite != _FeedMultiBalancerWhitelist) {
                        _FeedMultiBalancerWhitelist = feedMTBWhite;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Whitelist", typeof (Boolean), _FeedMultiBalancerWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic MULTIBalancer Whitelist for Admins").Success) {
                    Boolean feedMTBWhiteUser = Boolean.Parse(strValue);
                    if (feedMTBWhiteUser != _FeedMultiBalancerWhitelist_UserCache) {
                        _FeedMultiBalancerWhitelist_UserCache = feedMTBWhiteUser;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic MULTIBalancer Whitelist for Admins", typeof (Boolean), _FeedMultiBalancerWhitelist_UserCache));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed MULTIBalancer Even Dispersion List").Success) {
                    Boolean feedMTBBlack = Boolean.Parse(strValue);
                    if (feedMTBBlack != _FeedMultiBalancerDisperseList) {
                        _FeedMultiBalancerDisperseList = feedMTBBlack;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Even Dispersion List", typeof (Boolean), _FeedMultiBalancerDisperseList));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Server Reserved Slots").Success) {
                    Boolean feedSRS = Boolean.Parse(strValue);
                    if (feedSRS != _FeedServerReservedSlots) {
                        _FeedServerReservedSlots = feedSRS;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed Server Reserved Slots", typeof (Boolean), _FeedServerReservedSlots));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Reserved Slot for User Cache").Success) {
                    Boolean feedSRSUser = Boolean.Parse(strValue);
                    if (feedSRSUser != _FeedServerReservedSlots_UserCache) {
                        _FeedServerReservedSlots_UserCache = feedSRSUser;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Reserved Slot for User Cache", typeof (Boolean), _FeedServerReservedSlots_UserCache));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Server Spectator List").Success) {
                    Boolean feedSSL = Boolean.Parse(strValue);
                    if (feedSSL != _FeedServerSpectatorList) {
                        if (_gameVersion != GameVersion.BF4) {
                            ConsoleError("This feature can only be enabled on BF4 servers.");
                            return;
                        }
                        _FeedServerSpectatorList = feedSSL;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed Server Spectator List", typeof (Boolean), _FeedServerSpectatorList));
                    }
                }
                else if (Regex.Match(strVariable, @"Automatic Spectator Slot for User Cache").Success) {
                    Boolean feedSSLUser = Boolean.Parse(strValue);
                    if (feedSSLUser != _FeedServerSpectatorList_UserCache) {
                        _FeedServerSpectatorList_UserCache = feedSSLUser;
                        FetchAllAccess(true);
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Automatic Spectator Slot for User Cache", typeof (Boolean), _FeedServerSpectatorList_UserCache));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Stat Logger Settings").Success) {
                    Boolean feedSLS = Boolean.Parse(strValue);
                    if (feedSLS != _FeedStatLoggerSettings) {
                        _FeedStatLoggerSettings = feedSLS;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Feed Stat Logger Settings", typeof (Boolean), _FeedStatLoggerSettings));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Experimental Tools").Success) {
                    Boolean useEXP = Boolean.Parse(strValue);
                    if (useEXP != _useExperimentalTools) {
                        _useExperimentalTools = useEXP;
                        if (_useExperimentalTools) {
                            if (_threadsReady) {
                                ConsoleWarn("Using experimental tools. Take caution.");
                            }
                        }
                        else {
                            ConsoleWarn("Experimental tools disabled.");
                            _UseWeaponLimiter = false;
                            _UseGrenadeCookCatcher = false;
                            _UseHackerChecker = false;
                            _UseDpsChecker = false;
                            _UseHskChecker = false;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Experimental Tools", typeof (Boolean), _useExperimentalTools));
                        QueueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof (Boolean), _UseWeaponLimiter));
                    }
                }
                else if (Regex.Match(strVariable, @"Round Timer: Enable").Success) {
                    Boolean useTimer = Boolean.Parse(strValue);
                    if (useTimer != _useRoundTimer) {
                        _useRoundTimer = useTimer;
                        if (_useRoundTimer) {
                            if (_threadsReady) {
                                ConsoleWarn("Internal Round Timer activated, will enable on next round.");
                            }
                        }
                        else {
                            ConsoleWarn("Internal Round Timer disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Round Timer: Enable", typeof (Boolean), _useRoundTimer));
                    }
                }
                else if (Regex.Match(strVariable, @"Round Timer: Round Duration Minutes").Success) {
                    Double duration = Double.Parse(strValue);
                    if (_roundTimeMinutes != duration) {
                        if (duration <= 0) {
                            duration = 30.0;
                        }
                        _roundTimeMinutes = duration;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Round Timer: Round Duration Minutes", typeof (Double), _roundTimeMinutes));
                    }
                }
                else if (Regex.Match(strVariable, @"Use NO EXPLOSIVES Limiter").Success) {
                    Boolean useLimiter = Boolean.Parse(strValue);
                    if (useLimiter != _UseWeaponLimiter) {
                        _UseWeaponLimiter = useLimiter;
                        if (_UseWeaponLimiter) {
                            if (_threadsReady) {
                                ConsoleWarn("Internal NO EXPLOSIVES punish limit activated.");
                            }
                        }
                        else {
                            ConsoleWarn("Internal NO EXPLOSIVES punish limit disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof (Boolean), _UseWeaponLimiter));
                    }
                }
                else if (Regex.Match(strVariable, @"NO EXPLOSIVES Weapon String").Success) {
                    if (_WeaponLimiterString != strValue) {
                        if (!String.IsNullOrEmpty(strValue)) {
                            _WeaponLimiterString = strValue;
                        }
                        else {
                            ConsoleError("Weapon String cannot be empty.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Weapon String", typeof (String), _WeaponLimiterString));
                    }
                }
                else if (Regex.Match(strVariable, @"NO EXPLOSIVES Exception String").Success) {
                    if (_WeaponLimiterExceptionString != strValue) {
                        if (!String.IsNullOrEmpty(strValue)) {
                            _WeaponLimiterExceptionString = strValue;
                        }
                        else {
                            ConsoleError("Weapon exception String cannot be empty.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Exception String", typeof (String), _WeaponLimiterExceptionString));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Grenade Cook Catcher").Success) {
                    Boolean useCookCatcher = Boolean.Parse(strValue);
                    if (useCookCatcher != _UseGrenadeCookCatcher) {
                        _UseGrenadeCookCatcher = useCookCatcher;
                        if (_UseGrenadeCookCatcher) {
                            if (_threadsReady) {
                                ConsoleWarn("Internal Grenade Cook Catcher activated.");
                            }
                        }
                        else {
                            ConsoleWarn("Internal Grenade Cook Catcher disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Grenade Cook Catcher", typeof (Boolean), _UseGrenadeCookCatcher));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: Enable").Success) {
                    Boolean useHackChecker = Boolean.Parse(strValue);
                    if (useHackChecker != _UseHackerChecker) {
                        _UseHackerChecker = useHackChecker;
                        if (_UseHackerChecker) {
                            if (_threadsReady) {
                                ConsoleWarn("Internal Hacker Checker activated.");
                            }
                        }
                        else {
                            ConsoleWarn("Internal Hacker Checker disabled.");
                            _UseDpsChecker = false;
                            QueueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Enable", typeof (Boolean), _UseDpsChecker));
                            _UseHskChecker = false;
                            QueueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Enable", typeof (Boolean), _UseHskChecker));
                            _UseKpmChecker = false;
                            QueueSettingForUpload(new CPluginVariable(@"HackerChecker: KPM Checker: Enable", typeof (Boolean), _UseKpmChecker));
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: Enable", typeof (Boolean), _UseHackerChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: Whitelist").Success) {
                    //_HackerCheckerWhitelist = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    //QueueSettingForUpload(new CPluginVariable(@"HackerChecker: Whitelist", typeof (String), CPluginVariable.EncodeStringArray(_HackerCheckerWhitelist)));
                }
                else if (Regex.Match(strVariable, @"HackerChecker: DPS Checker: Enable").Success) {
                    Boolean useDamageChecker = Boolean.Parse(strValue);
                    if (useDamageChecker != _UseDpsChecker) {
                        _UseDpsChecker = useDamageChecker;
                        if (_UseDpsChecker) {
                            if (_threadsReady) {
                                ConsoleWarn("Internal Damage Mod Checker activated.");
                            }
                        }
                        else {
                            ConsoleWarn("Internal Damage Mod Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Enable", typeof (Boolean), _UseDpsChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: DPS Checker: Trigger Level").Success) {
                    Double triggerLevel = Double.Parse(strValue);
                    if (_DpsTriggerLevel != triggerLevel) {
                        if (triggerLevel <= 0) {
                            triggerLevel = 100.0;
                        }
                        _DpsTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Trigger Level", typeof (Double), _DpsTriggerLevel));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: DPS Checker: Ban Message").Success) {
                    if (_HackerCheckerDPSBanMessage != strValue) {
                        _HackerCheckerDPSBanMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Ban Message", typeof (String), _HackerCheckerDPSBanMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: HSK Checker: Enable").Success) {
                    Boolean useAimbotChecker = Boolean.Parse(strValue);
                    if (useAimbotChecker != _UseHskChecker) {
                        _UseHskChecker = useAimbotChecker;
                        if (_UseHskChecker) {
                            if (_threadsReady) {
                                ConsoleWarn("Internal Aimbot Checker activated.");
                            }
                        }
                        else {
                            ConsoleWarn("Internal Aimbot Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Enable", typeof (Boolean), _UseHskChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: HSK Checker: Trigger Level").Success) {
                    Double triggerLevel = Double.Parse(strValue);
                    if (_HskTriggerLevel != triggerLevel) {
                        if (triggerLevel <= 0) {
                            triggerLevel = 100.0;
                        }
                        _HskTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Trigger Level", typeof (Double), _HskTriggerLevel));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: HSK Checker: Ban Message").Success) {
                    if (_HackerCheckerHSKBanMessage != strValue) {
                        _HackerCheckerHSKBanMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Ban Message", typeof (String), _HackerCheckerHSKBanMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: KPM Checker: Enable").Success) {
                    Boolean useKPMChecker = Boolean.Parse(strValue);
                    if (useKPMChecker != _UseKpmChecker) {
                        _UseKpmChecker = useKPMChecker;
                        if (_UseKpmChecker) {
                            if (_threadsReady) {
                                ConsoleWarn("Internal KPM Checker activated.");
                            }
                        }
                        else {
                            ConsoleWarn("Internal KPM Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: KPM Checker: Enable", typeof (Boolean), _UseKpmChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: KPM Checker: Trigger Level").Success) {
                    Double triggerLevel = Double.Parse(strValue);
                    if (_KpmTriggerLevel != triggerLevel) {
                        if (triggerLevel <= 0) {
                            triggerLevel = 100.0;
                        }
                        _KpmTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: KPM Checker: Trigger Level", typeof (Double), _KpmTriggerLevel));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: KPM Checker: Ban Message").Success) {
                    if (_HackerCheckerKPMBanMessage != strValue) {
                        _HackerCheckerKPMBanMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"HackerChecker: KPM Checker: Ban Message", typeof (String), _HackerCheckerKPMBanMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"External Access Key").Success) {
                    if (strValue != _ExternalCommandAccessKey) {
                        _ExternalCommandAccessKey = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"External Access Key", typeof (String), _ExternalCommandAccessKey));
                    }
                }
                else if (Regex.Match(strVariable, @"Fetch Actions from Database").Success) {
                    Boolean fetch = Boolean.Parse(strValue);
                    if (fetch != _fetchActionsFromDb) {
                        _fetchActionsFromDb = fetch;
                        _DbCommunicationWaitHandle.Set();
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Fetch Actions from Database", typeof (Boolean), _fetchActionsFromDb));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Additional Ban Message").Success) {
                    Boolean use = Boolean.Parse(strValue);
                    if (_UseBanAppend != use) {
                        _UseBanAppend = use;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Additional Ban Message", typeof (Boolean), _UseBanAppend));
                    }
                }
                else if (Regex.Match(strVariable, @"Additional Ban Message").Success) {
                    if (strValue.Length > 30) {
                        strValue = strValue.Substring(0, 30);
                        ConsoleError("Ban append cannot be more than 30 characters.");
                    }
                    if (_BanAppend != strValue) {
                        _BanAppend = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Additional Ban Message", typeof (String), _BanAppend));
                    }
                }
                else if (Regex.Match(strVariable, @"Procon Ban Admin Name").Success) {
                    if (strValue.Length > 16) {
                        strValue = strValue.Substring(0, 16);
                        ConsoleError("Procon ban admin id cannot be more than 16 characters.");
                    }
                    if (_CBanAdminName != strValue) {
                        _CBanAdminName = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Procon Ban Admin Name", typeof (String), _CBanAdminName));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Ban Enforcer").Success) {
                    Boolean use = Boolean.Parse(strValue);
                    if (_UseBanEnforcer != use) {
                        _UseBanEnforcer = use;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use Ban Enforcer", typeof (Boolean), _UseBanEnforcer));
                        if (_UseBanEnforcer) {
                            _fetchActionsFromDb = true;
                            _DbCommunicationWaitHandle.Set();
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by NAME").Success) {
                    Boolean enforceName = Boolean.Parse(strValue);
                    if (_DefaultEnforceName != enforceName) {
                        _DefaultEnforceName = enforceName;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by NAME", typeof (Boolean), _DefaultEnforceName));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by GUID").Success) {
                    Boolean enforceGUID = Boolean.Parse(strValue);
                    if (_DefaultEnforceGUID != enforceGUID) {
                        _DefaultEnforceGUID = enforceGUID;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by GUID", typeof (Boolean), _DefaultEnforceGUID));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by IP").Success) {
                    Boolean enforceIP = Boolean.Parse(strValue);
                    if (_DefaultEnforceIP != enforceIP) {
                        _DefaultEnforceIP = enforceIP;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by IP", typeof (Boolean), _DefaultEnforceIP));
                    }
                }
                else if (Regex.Match(strVariable, @"Ban Search").Success) {
                    //Create new thread to run ban search
                    var banSearchThread = new Thread(new ThreadStart(delegate {
                        try {
                            Thread.CurrentThread.Name = "bansearch";
                            if (String.IsNullOrEmpty(strValue) || strValue.Length < 3) {
                                ConsoleError("Search query must be 3 or more characters.");
                                return;
                            }
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_BanEnforcerSearchResults) {
                                _BanEnforcerSearchResults = new List<AdKatsBan>();
                                List<AdKatsPlayer> matchingPlayers;
                                if (FetchMatchingPlayers(strValue, out matchingPlayers, false)) {
                                    foreach (AdKatsPlayer aPlayer in matchingPlayers) {
                                        foreach (AdKatsBan aBan in FetchPlayerBans(aPlayer)) {
                                            _BanEnforcerSearchResults.Add(aBan);
                                        }
                                    }
                                }
                                if (_BanEnforcerSearchResults.Count == 0) {
                                    ConsoleError("No players matching '" + strValue + "' have active bans.");
                                }
                            }
                        }
                        catch (Exception e) {
                            HandleException(new AdKatsException("Error while running ban search.", e));
                        }
                        LogThreadExit();
                    }));
                    //Start the thread
                    StartAndLogThread(banSearchThread);
                }
                else if (Regex.Match(strVariable, @"Minimum Required Reason Length").Success) {
                    Int32 required = Int32.Parse(strValue);
                    if (_RequiredReasonLength != required) {
                        _RequiredReasonLength = required;
                        if (_RequiredReasonLength < 1) {
                            _RequiredReasonLength = 1;
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Required Reason Length", typeof (Int32), _RequiredReasonLength));
                    }
                }
                else if (Regex.Match(strVariable, @"Allow Commands from Admin Say").Success) {
                    Boolean allowSayCommands = Boolean.Parse(strValue);
                    if (_AllowAdminSayCommands != allowSayCommands) {
                        _AllowAdminSayCommands = allowSayCommands;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Allow Commands from Admin Say", typeof (Boolean), _AllowAdminSayCommands));
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
                    if (_userCache.TryGetValue(user_id, out aUser)) {
                        switch (section) {
                            case "User Email":
                                if (String.IsNullOrEmpty(strValue) || Regex.IsMatch(strValue, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$")) {
                                    aUser.user_email = strValue;
                                }
                                else {
                                    ConsoleError(strValue + " is an invalid email address.");
                                    return;
                                }
                                //Reupload the user
                                QueueUserForUpload(aUser);
                                break;
                            case "User Phone":
                                aUser.user_phone = strValue;
                                //Reupload the user
                                QueueUserForUpload(aUser);
                                break;
                            case "User Role":
                                AdKatsRole aRole = null;
                                if (_RoleNameDictionary.TryGetValue(strValue, out aRole)) {
                                    aUser.user_role = aRole;
                                }
                                else {
                                    ConsoleError("Role " + strValue + " not found.");
                                    return;
                                }
                                //Reupload the user
                                QueueUserForUpload(aUser);
                                break;
                            case "Delete User?":
                                if (strValue.ToLower() == "delete") {
                                    QueueUserForRemoval(aUser);
                                }
                                break;
                            case "Add Soldier?":
                                var addSoldierThread = new Thread(new ThreadStart(delegate {
                                    Thread.CurrentThread.Name = "addsoldier";
                                    DebugWrite("Starting a user change thread.", 2);
                                    TryAddUserSoldier(aUser, strValue);
                                    QueueUserForUpload(aUser);
                                    DebugWrite("Exiting a user change thread.", 2);
                                    LogThreadExit();
                                }));
                                StartAndLogThread(addSoldierThread);
                                break;
                            case "Soldiers":
                                if (strVariable.Contains("Delete Soldier?") && strValue.ToLower() == "delete") {
                                    String player_id_str = commandSplit[3].Trim();
                                    Int64 player_id = Int64.Parse(player_id_str);
                                    aUser.soldierDictionary.Remove(player_id);
                                    //Reupload the user
                                    QueueUserForUpload(aUser);
                                }
                                break;
                            default:
                                ConsoleError("Section " + section + " not found.");
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
                    if (_CommandIDDictionary.TryGetValue(command_id, out command)) {
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
                                ConsoleError("Activity setting " + strValue + " was invalid.");
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
                                    ConsoleError("Logging setting " + strValue + " was invalid.");
                                    return;
                            }
                        }
                        else if (section == "Text") {
                            if (String.IsNullOrEmpty(strValue)) {
                                ConsoleError("Command text cannot be blank.");
                                return;
                            }
                            //Make sure command text only contains alphanumeric chars, underscores, and dashes
                            var rgx = new Regex("[^a-zA-Z0-9_-]");
                            strValue = rgx.Replace(strValue, "").ToLower();
                            //Check to make sure text is not a duplicate
                            foreach (AdKatsCommand testCommand in _CommandNameDictionary.Values) {
                                if (testCommand.command_text == strValue) {
                                    ConsoleError("Command text cannot be the same as another command.");
                                    return;
                                }
                            }
                            //Assign the command text
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_CommandTextDictionary) {
                                _CommandTextDictionary.Remove(command.command_text);
                                command.command_text = strValue;
                                _CommandTextDictionary.Add(command.command_text, command);
                            }
                        }
                        else {
                            ConsoleError("Section " + section + " not understood.");
                            return;
                        }
                        //Upload the command changes
                        QueueCommandForUpload(command);
                    }
                    else {
                        ConsoleError("Command " + command_id + " not found in command dictionary.");
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
                        AdKatsRole aRole = null;
                        if (_RoleIDDictionary.TryGetValue(roleID, out aRole)) {
                            //Fetch needed command
                            AdKatsCommand aCommand = null;
                            if (_CommandIDDictionary.TryGetValue(commandID, out aCommand)) {
                                switch (strValue.ToLower()) {
                                    case "allow":
                                        if (_isTestingAuthorized)
                                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                        lock (aRole.RoleAllowedCommands) {
                                            if (!aRole.RoleAllowedCommands.ContainsKey(aCommand.command_key)) {
                                                aRole.RoleAllowedCommands.Add(aCommand.command_key, aCommand);
                                            }
                                        }
                                        QueueRoleForUpload(aRole);
                                        break;
                                    case "deny":
                                        if (_isTestingAuthorized)
                                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                        lock (aRole.RoleAllowedCommands) {
                                            aRole.RoleAllowedCommands.Remove(aCommand.command_key);
                                        }
                                        QueueRoleForUpload(aRole);
                                        break;
                                    default:
                                        ConsoleError("Unknown setting when assigning command allowance.");
                                        return;
                                }
                            }
                            else {
                                ConsoleError("Command " + commandID + " not found in command dictionary.");
                            }
                        }
                        else {
                            ConsoleError("Role " + roleID + " not found in role dictionary.");
                        }
                    }
                    else if (commandSplit[2].Contains("Delete Role?") && strValue.ToLower() == "delete") {
                        //Fetch needed role
                        AdKatsRole aRole = null;
                        if (_RoleIDDictionary.TryGetValue(roleID, out aRole)) {
                            QueueRoleForRemoval(aRole);
                        }
                        else {
                            ConsoleError("Unable to fetch role for deletion.");
                        }
                    }
                }
                else if (strVariable.StartsWith("BAN")) {
                    //Trim off all but the command ID and section
                    //BAN1 | ColColonCleaner | Some Reason

                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String banIDStr = commandSplit[0].TrimStart("BAN".ToCharArray()).Trim();
                    Int32 banID = Int32.Parse(banIDStr);

                    AdKatsBan aBan = null;
                    foreach (AdKatsBan innerBan in _BanEnforcerSearchResults) {
                        if (innerBan.ban_id == banID) {
                            aBan = innerBan;
                            break;
                        }
                    }
                    if (aBan != null) {
                        switch (strValue) {
                            case "Active":
                                aBan.ban_status = strValue;
                                break;
                            case "Disabled":
                                aBan.ban_status = strValue;
                                break;
                            default:
                                ConsoleError("Unknown setting when assigning ban status.");
                                return;
                        }
                        UpdateBanStatus(aBan);
                        ConsoleSuccess("Ban " + aBan.ban_id + " is now " + strValue);
                    }
                    else {
                        ConsoleError("Unable to update ban. This should not happen.");
                    }
                }
                else if (Regex.Match(strVariable, @"Punishment Hierarchy").Success) {
                    _PunishmentHierarchy = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Punishment Hierarchy", typeof (String), CPluginVariable.EncodeStringArray(_PunishmentHierarchy)));
                }
                else if (Regex.Match(strVariable, @"Combine Server Punishments").Success) {
                    Boolean combine = Boolean.Parse(strValue);
                    if (_CombineServerPunishments != combine) {
                        _CombineServerPunishments = combine;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Combine Server Punishments", typeof (Boolean), _CombineServerPunishments));
                    }
                }
                else if (Regex.Match(strVariable, @"Only Kill Players when Server in low population").Success) {
                    Boolean onlyKill = Boolean.Parse(strValue);
                    if (onlyKill != _OnlyKillOnLowPop) {
                        _OnlyKillOnLowPop = onlyKill;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Only Kill Players when Server in low population", typeof (Boolean), _OnlyKillOnLowPop));
                    }
                }
                else if (Regex.Match(strVariable, @"Low Population Value").Success) {
                    Int32 lowPop = Int32.Parse(strValue);
                    if (lowPop != _LowPopPlayerCount) {
                        _LowPopPlayerCount = lowPop;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Low Population Value", typeof (Int32), _LowPopPlayerCount));
                    }
                }
                else if (Regex.Match(strVariable, @"Use IRO Punishment").Success) {
                    Boolean iro = Boolean.Parse(strValue);
                    if (iro != _IROActive) {
                        _IROActive = iro;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use IRO Punishment", typeof (Boolean), _IROActive));
                    }
                }
                else if (Regex.Match(strVariable, @"IRO Punishment Overrides Low Pop").Success) {
                    Boolean overrideIRO = Boolean.Parse(strValue);
                    if (overrideIRO != _IROOverridesLowPop) {
                        _IROOverridesLowPop = overrideIRO;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"IRO Punishment Overrides Low Pop", typeof (Boolean), _IROOverridesLowPop));
                    }
                }
                else if (Regex.Match(strVariable, @"IRO Timeout Minutes").Success) {
                    Int32 timeout = Int32.Parse(strValue);
                    if (timeout != _IROTimeout) {
                        _IROTimeout = timeout;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"IRO Timeout (Minutes)", typeof (Int32), _IROTimeout));
                    }
                }
                else if (Regex.Match(strVariable, @"MySQL Hostname").Success) {
                    _mySqlHostname = strValue;
                    _dbSettingsChanged = true;
                    _DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Port").Success) {
                    Int32 tmp = 3306;
                    int.TryParse(strValue, out tmp);
                    if (tmp > 0 && tmp < 65536) {
                        _mySqlPort = strValue;
                        _dbSettingsChanged = true;
                        _DbCommunicationWaitHandle.Set();
                    }
                    else {
                        ConsoleError("Invalid value for MySQL Port: '" + strValue + "'. Must be number between 1 and 65535!");
                    }
                }
                else if (Regex.Match(strVariable, @"MySQL Database").Success) {
                    _mySqlDatabaseName = strValue;
                    _dbSettingsChanged = true;
                    _DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Username").Success) {
                    _mySqlUsername = strValue;
                    _dbSettingsChanged = true;
                    _DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Password").Success) {
                    _mySqlPassword = strValue;
                    _dbSettingsChanged = true;
                    _DbCommunicationWaitHandle.Set();
                }
                else if (Regex.Match(strVariable, @"Send Emails").Success) {
                    //Disabled
                    _UseEmail = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable("Send Emails", typeof (Boolean), _UseEmail));
                }
                else if (Regex.Match(strVariable, @"Use SSL?").Success) {
                    _EmailHandler.UseSSL = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable("Use SSL?", typeof (Boolean), _EmailHandler.UseSSL));
                }
                else if (Regex.Match(strVariable, @"SMTP-Server address").Success) {
                    if (!String.IsNullOrEmpty(strValue)) {
                        _EmailHandler.SMTPServer = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("SMTP-Server address", typeof (String), _EmailHandler.SMTPServer));
                    }
                }
                else if (Regex.Match(strVariable, @"SMTP-Server port").Success) {
                    Int32 iPort = Int32.Parse(strValue);
                    if (iPort > 0) {
                        _EmailHandler.SMTPPort = iPort;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("SMTP-Server port", typeof (Int32), _EmailHandler.SMTPPort));
                    }
                }
                else if (Regex.Match(strVariable, @"Sender address").Success) {
                    if (string.IsNullOrEmpty(strValue)) {
                        _EmailHandler.SenderEmail = "SENDER_CANNOT_BE_EMPTY";
                        ConsoleError("No sender for email was given! Canceling Operation.");
                    }
                    else {
                        _EmailHandler.SenderEmail = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("Sender address", typeof (String), _EmailHandler.SenderEmail));
                    }
                }
                else if (Regex.Match(strVariable, @"SMTP-Server username").Success) {
                    if (string.IsNullOrEmpty(strValue)) {
                        _EmailHandler.SMTPUser = "SMTP_USERNAME_CANNOT_BE_EMPTY";
                        ConsoleError("No username for SMTP was given! Canceling Operation.");
                    }
                    else {
                        _EmailHandler.SMTPUser = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("SMTP-Server username", typeof (String), _EmailHandler.SMTPUser));
                    }
                }
                else if (Regex.Match(strVariable, @"SMTP-Server password").Success) {
                    if (string.IsNullOrEmpty(strValue)) {
                        _EmailHandler.SMTPPassword = "SMTP_PASSWORD_CANNOT_BE_EMPTY";
                        ConsoleError("No password for SMTP was given! Canceling Operation.");
                    }
                    else {
                        _EmailHandler.SMTPPassword = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable("SMTP-Server password", typeof (String), _EmailHandler.SMTPPassword));
                    }
                }
                else if (Regex.Match(strVariable, @"Custom HTML Addition").Success) {
                    _EmailHandler.CustomHTMLAddition = strValue;
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable("Custom HTML Addition", typeof (String), _EmailHandler.CustomHTMLAddition));
                }
                else if (Regex.Match(strVariable, @"Extra Recipient Email Addresses").Success) {
                    _EmailHandler.RecipientEmails = CPluginVariable.DecodeStringArray(strValue).ToList();
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Extra Recipient Email Addresses", typeof (String), strValue));
                }
                else if (Regex.Match(strVariable, @"On-Player-Muted Message").Success) {
                    if (_MutedPlayerMuteMessage != strValue) {
                        _MutedPlayerMuteMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"On-Player-Muted Message", typeof (String), _MutedPlayerMuteMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Killed Message").Success) {
                    if (_MutedPlayerKillMessage != strValue) {
                        _MutedPlayerKillMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"On-Player-Killed Message", typeof (String), _MutedPlayerKillMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Kicked Message").Success) {
                    if (_MutedPlayerKickMessage != strValue) {
                        _MutedPlayerKickMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"On-Player-Kicked Message", typeof (String), _MutedPlayerKickMessage));
                    }
                }
                if (Regex.Match(strVariable, @"# Chances to give player before kicking").Success) {
                    Int32 tmp = 5;
                    int.TryParse(strValue, out tmp);
                    if (_MutedPlayerChances != tmp) {
                        _MutedPlayerChances = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"# Chances to give player before kicking", typeof (Int32), _MutedPlayerChances));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Whitelist Count").Success) {
                    Int32 tmp = 1;
                    int.TryParse(strValue, out tmp);
                    if (tmp != _PlayersToAutoWhitelist) {
                        if (tmp < 0)
                            tmp = 0;
                        _PlayersToAutoWhitelist = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Auto-Whitelist Count", typeof (Int32), _PlayersToAutoWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Ticket Window High").Success) {
                    Int32 tmp = 2;
                    int.TryParse(strValue, out tmp);
                    if (tmp != _TeamSwapTicketWindowHigh) {
                        _TeamSwapTicketWindowHigh = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ticket Window High", typeof (Int32), _TeamSwapTicketWindowHigh));
                    }
                }
                else if (Regex.Match(strVariable, @"Ticket Window Low").Success) {
                    Int32 tmp = 2;
                    int.TryParse(strValue, out tmp);
                    if (tmp != _TeamSwapTicketWindowLow) {
                        _TeamSwapTicketWindowLow = tmp;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Ticket Window Low", typeof (Int32), _TeamSwapTicketWindowLow));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Admin Assistants").Success) {
                    Boolean enableAA = Boolean.Parse(strValue);
                    if (_EnableAdminAssistants != enableAA) {
                        _EnableAdminAssistants = enableAA;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistants", typeof (Boolean), _EnableAdminAssistants));
                    }
                }
                else if (Regex.Match(strVariable, @"Enable Admin Assistant Perk").Success) {
                    Boolean enableAA = Boolean.Parse(strValue);
                    if (_EnableAdminAssistantPerk != enableAA) {
                        _EnableAdminAssistantPerk = enableAA;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistant Perk", typeof (Boolean), _EnableAdminAssistantPerk));
                    }
                }
                else if (Regex.Match(strVariable, @"Use AA Report Auto Handler").Success) {
                    Boolean useAAHandler = Boolean.Parse(strValue);
                    if (useAAHandler != _UseAAReportAutoHandler) {
                        _UseAAReportAutoHandler = useAAHandler;
                        if (_UseAAReportAutoHandler) {
                            if (_threadsReady) {
                                ConsoleWarn("Internal Automatic Report Handler activated.");
                            }
                        }
                        else {
                            ConsoleWarn("Internal Automatic Report Handler disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Use AA Report Auto Handler", typeof (Boolean), _UseAAReportAutoHandler));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Report-Handler Strings").Success) {
                    _AutoReportHandleStrings = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Auto-Report-Handler Strings", typeof (String), CPluginVariable.EncodeStringArray(_AutoReportHandleStrings)));
                }
                else if (Regex.Match(strVariable, @"Minimum Confirmed Reports Per Month").Success) {
                    Int32 monthlyReports = Int32.Parse(strValue);
                    if (_MinimumRequiredMonthlyReports != monthlyReports) {
                        _MinimumRequiredMonthlyReports = monthlyReports;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Minimum Confirmed Reports Per Month", typeof (Int32), _MinimumRequiredMonthlyReports));
                    }
                }
                else if (Regex.Match(strVariable, @"Yell display time seconds").Success) {
                    Int32 yellTime = Int32.Parse(strValue);
                    if (_YellDuration != yellTime) {
                        _YellDuration = yellTime;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Yell display time seconds", typeof (Int32), _YellDuration));
                    }
                }
                else if (Regex.Match(strVariable, @"Pre-Message List").Success) {
                    _PreMessageList = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                    //Once setting has been changed, upload the change to database
                    QueueSettingForUpload(new CPluginVariable(@"Pre-Message List", typeof (String), CPluginVariable.EncodeStringArray(_PreMessageList.ToArray())));
                }
                else if (Regex.Match(strVariable, @"Require Use of Pre-Messages").Success) {
                    Boolean require = Boolean.Parse(strValue);
                    if (require != _RequirePreMessageUse) {
                        _RequirePreMessageUse = require;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Require Use of Pre-Messages", typeof (Boolean), _RequirePreMessageUse));
                    }
                }
                else if (Regex.Match(strVariable, @"Display Admin Name in Kick and Ban Announcement").Success) {
                    Boolean display = Boolean.Parse(strValue);
                    if (display != _ShowAdminNameInSay) {
                        _ShowAdminNameInSay = display;
                        //Once setting has been changed, upload the change to database
                        QueueSettingForUpload(new CPluginVariable(@"Display Admin Name in Kick and Ban Announcement", typeof (Boolean), _ShowAdminNameInSay));
                    }
                }
                else if (Regex.Match(strVariable, @"Add User").Success) {
                    if (SoldierNameValid(strValue)) {
                        var aUser = new AdKatsUser {
                            user_name = strValue
                        };
                        Boolean valid = true;
                        if (_isTestingAuthorized)
                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                        lock (_userCache) {
                            valid = _userCache.Values.All(iUser => aUser.user_name != iUser.user_name);
                        }
                        if (!valid) {
                            ConsoleError("Unable to add " + aUser.user_name + ", a user with that user id already exists.");
                            return;
                        }
                        var addUserThread = new Thread(new ThreadStart(delegate {
                            Thread.CurrentThread.Name = "userchange";
                            DebugWrite("Starting a user change thread.", 2);
                            //Attempt to add soldiers matching the user's name
                            TryAddUserSoldier(aUser, aUser.user_name);
                            QueueUserForUpload(aUser);
                            DebugWrite("Exiting a user change thread.", 2);
                            LogThreadExit();
                        }));
                        StartAndLogThread(addUserThread);
                    }
                    else {
                        ConsoleError("User id had invalid formatting, please try again.");
                    }
                }
                else if (Regex.Match(strVariable, @"Add Role").Success) {
                    if (!String.IsNullOrEmpty(strValue)) {
                        String roleName = new Regex("[^a-zA-Z0-9 _-]").Replace(strValue, "");
                        String roleKey = roleName.Replace(' ', '_');
                        if (!String.IsNullOrEmpty(roleName) && !String.IsNullOrEmpty(roleKey)) {
                            var aRole = new AdKatsRole {
                                role_key = roleKey,
                                role_name = roleName
                            };
                            //By default we should include all commands as allowed
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_CommandNameDictionary) {
                                foreach (AdKatsCommand aCommand in _CommandNameDictionary.Values) {
                                    aRole.RoleAllowedCommands.Add(aCommand.command_key, aCommand);
                                }
                            }
                            //Queue it for upload
                            QueueRoleForUpload(aRole);
                        }
                        else {
                            ConsoleError("Role had invalid characters, please try again.");
                        }
                    }
                }
                if (FullDebug) {
                    ConsoleWrite("updating settings page");
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while updating AdKats settings.", e));
            }
        }

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
            DebugWrite("Entering OnPluginLoaded", 7);
            try {
                //Set the server IP
                _serverIP = strHostName + ":" + strPort;
                //Register all events
                RegisterEvents(GetType().Name, "OnVersion", "OnServerInfo", "OnListPlayers", "OnPunkbusterPlayerInfo", "OnReservedSlotsList", "OnPlayerKilled", "OnPlayerIsAlive", "OnPlayerSpawned", "OnPlayerTeamChange", "OnPlayerJoin", "OnPlayerLeft", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnLevelLoaded", "OnBanAdded", "OnBanRemoved", "OnBanListClear", "OnBanListSave", "OnBanListLoad", "OnBanList", "OnRoundOverTeamScores", "OnSpectatorListLoad", "OnSpectatorListSave", "OnSpectatorListPlayerAdded", "OnSpectatorListPlayerRemoved", "OnSpectatorListCleared", "OnSpectatorListList", "OnGameAdminLoad", "OnGameAdminSave", "OnGameAdminPlayerAdded", "OnGameAdminPlayerRemoved", "OnGameAdminCleared", "OnGameAdminList", "OnFairFight", "OnIsHitIndicator", "OnCommander", "OnForceReloadWholeMags", "OnServerType", "OnMaxSpectators", "OnTeamFactionOverride");
            }
            catch (Exception e) {
                HandleException(new AdKatsException("FATAL ERROR on plugin load.", e));
            }
            DebugWrite("Exiting OnPluginLoaded", 7);
        }

        public void OnPluginEnable() {
            try {
                //If the finalizer is still alive, inform the user and disable
                if (_Finalizer != null && _Finalizer.IsAlive) {
                    ConsoleError("Cannot enable plugin while it is shutting down. Please Wait.");
                    //Disable the plugin
                    Disable();
                    return;
                }
                //Create a new thread to activate the plugin
                _Activator = new Thread(new ThreadStart(delegate {
                    try {
                        Thread.CurrentThread.Name = "enabler";

                        if ((DateTime.UtcNow - _ProconStartTime).TotalSeconds < 10) {
                            ConsoleWrite("Waiting a few seconds for requirements and other plugins to initialize, please wait...");
                            //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                            for (Int32 index = 5; index > 0; index--) {
                                DebugWrite(index + "...", 1);
                                Thread.Sleep(1000);
                            }
                        }

                        //Make sure the default in-game admin is disabled
                        ExecuteCommand("procon.protected.plugins.enable", "CInGameAdmin", "False");

                        //Initialize the stat library
                        _StatLibrary = new StatLibrary(this);

                        //Fetch all reputation information
                        PopulateCommandReputationDictionaries();

                        //Don't directly depend on stat logger being controllable at this time, connection is unstable
                        /*if (useDatabase)
                        {
                            //Confirm Stat Logger active and properly configured
                            ConsoleWrite("Confirming proper setup for CChatGUIDStatsLoggerBF3, please wait...");

                            if (gameVersion == GameVersion.BF3)
                            {
                                if (confirmStatLoggerSetup())
                                {
                                    ConsoleSuccess("^bCChatGUIDStatsLoggerBF3^n enabled and active!");
                                }
                                else
                                {
                                    //Stat logger could not be enabled or managed
                                    ConsoleWarn("The stat logger plugin could not be found or controlled. Running AdKats in backup mode.");
                                    return;
                                }
                            }
                        }*/

                        //Inform of IP
                        ConsoleSuccess("Server IP is " + _serverIP + "!");

                        //Set the enabled variable
                        _pluginEnabled = true;

                        //Init and start all the threads
                        InitWaitHandles();
                        SetAllHandles();
                        InitThreads();
                        StartThreads();
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error while enabling AdKats.", e));
                    }
                    LogThreadExit();
                }));

                ConsoleWrite("^b^2Enabled!^n^0 Beginning startup sequence...");
                //Start the thread
                StartAndLogThread(_Activator);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while initializing activator thread.", e));
            }
        }

        public void OnPluginDisable() {
            //If the plugin is already disabling then cancel
            if (_Finalizer != null && _Finalizer.IsAlive)
                return;
            try {
                //Create a new thread to disabled the plugin
                _Finalizer = new Thread(new ThreadStart(delegate {
                    try {
                        Thread.CurrentThread.Name = "finalizer";
                        ConsoleWarn("Shutting down AdKats.");
                        //Disable settings
                        _pluginEnabled = false;
                        _threadsReady = false;
                        //Remove all match commands
                        UnregisterCommand(_issueCommandMatchCommand);
                        UnregisterCommand(_fetchAuthorizedSoldiersMatchCommand);
                        //Open all handles. Threads will finish on their own.
                        SetAllHandles();

                        //Check to make sure all threads have completed and stopped
                        Int32 attempts = 0;
                        Boolean alive = false;
                        do {
                            attempts++;
                            Thread.Sleep(1000);
                            alive = false;
                            String aliveThreads = "";
                            lock (_aliveThreads) {
                                foreach (Thread aliveThread in _aliveThreads.Values) {
                                    alive = true;
                                    aliveThreads += (aliveThread.Name + "[" + aliveThread.ManagedThreadId + "] ");
                                }
                            }
                            if (aliveThreads.Length > 0) {
                                if (attempts > 10) {
                                    ConsoleWarn("Threads still exiting: " + aliveThreads);
                                }
                                else {
                                    DebugWrite("Threads still exiting: " + aliveThreads, 2);
                                }
                            }
                        } while (alive);

                        //Reset all caches and storage
                        if (_UserRemovalQueue != null)
                            _UserRemovalQueue.Clear();
                        if (_UserUploadQueue != null)
                            _UserUploadQueue.Clear();
                        if (_TeamswapForceMoveQueue != null)
                            _TeamswapForceMoveQueue.Clear();
                        if (_TeamswapOnDeathCheckingQueue != null)
                            _TeamswapOnDeathCheckingQueue.Clear();
                        if (_TeamswapOnDeathMoveDic != null)
                            _TeamswapOnDeathMoveDic.Clear();
                        if (_UnparsedCommandQueue != null)
                            _UnparsedCommandQueue.Clear();
                        if (_UnparsedMessageQueue != null)
                            _UnparsedMessageQueue.Clear();
                        if (_UnprocessedActionQueue != null)
                            _UnprocessedActionQueue.Clear();
                        if (_UnprocessedRecordQueue != null)
                            _UnprocessedRecordQueue.Clear();
                        if (_BanEnforcerCheckingQueue != null)
                            _BanEnforcerCheckingQueue.Clear();
                        _ToldCol = false;
                        if (_Team2MoveQueue != null)
                            _Team2MoveQueue.Clear();
                        if (_Team1MoveQueue != null)
                            _Team1MoveQueue.Clear();
                        if (_RoundCookers != null)
                            _RoundCookers.Clear();
                        if (_RoundReports != null)
                            _RoundReports.Clear();
                        if (_RoundMutedPlayers != null)
                            _RoundMutedPlayers.Clear();
                        if (_playerDictionary != null)
                            _playerDictionary.Clear();
                        _firstPlayerListComplete = false;
                        if (_userCache != null)
                            _userCache.Clear();
                        if (FrostbitePlayerInfoList != null)
                            FrostbitePlayerInfoList.Clear();
                        if (_CBanProcessingQueue != null)
                            _CBanProcessingQueue.Clear();
                        if (_BanEnforcerProcessingQueue != null)
                            _BanEnforcerProcessingQueue.Clear();
                        if (_ActOnSpawnDictionary != null)
                            _ActOnSpawnDictionary.Clear();
                        if (_ActOnIsAliveDictionary != null)
                            _ActOnIsAliveDictionary.Clear();
                        if (_ActionConfirmDic != null)
                            _ActionConfirmDic.Clear();
                        if (_TeamswapRoundWhitelist != null)
                            _TeamswapRoundWhitelist.Clear();
                        _slowmo = false;
                        //Now that plugin is disabled, update the settings page to reflect
                        UpdateSettingPage();
                        ConsoleWrite("^b^1AdKats " + GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error occured while disabling Adkats.", e));
                    }
                }));

                //Start the finalizer thread
                _Finalizer.Start();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while initializing AdKats disable thread.", e));
            }
        }

        private void FetchPluginDescAndChangelog() {
            _PluginDescriptionWaitHandle.Reset();
            //Create a new thread to fetch the plugin description and changelog
            var descFetcher = new Thread(new ThreadStart(delegate {
                try {
                    Thread.CurrentThread.Name = "descfetching";
                    //Create web client
                    var client = new WebClient();
                    //Download the readme and changelog
                    DebugWrite("Fetching plugin readme...", 2);
                    try {
                        _pluginDescription = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/README.md");
                        DebugWrite("Plugin description fetched.", 1);
                    }
                    catch (Exception) {
                        ConsoleError("Failed to fetch plugin description.");
                    }
                    DebugWrite("Fetching plugin changelog...", 2);
                    try {
                        _pluginChangelog = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/CHANGELOG.md");
                        DebugWrite("Plugin changelog fetched.", 1);
                    }
                    catch (Exception) {
                        ConsoleError("Failed to fetch plugin changelog.");
                    }
                    if (_pluginDescription != "DESCRIPTION FETCH FAILED|") {
                        //Extract the latest stable version
                        String latestStableVersion = ExtractString(_pluginDescription, "latest_stable_release");
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
                            _pluginVersionStatus = versionStatus;
                        }
                    }
                    DebugWrite("Setting desc fetch handle.", 1);
                    _fetchedPluginInformation = true;
                    _PluginDescriptionWaitHandle.Set();
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error while fetching plugin description and changelog.", e));
                }
                LogThreadExit();
            }));
            //Start the thread
            StartAndLogThread(descFetcher);
        }

        private void FetchWeaponStats() {
            _WeaponStatsWaitHandle.Reset();
            //Create a new thread to fetch the plugin description and changelog
            var descFetcher = new Thread(new ThreadStart(delegate {
                try {
                    Thread.CurrentThread.Name = "statsfetching";
                    //Create web client
                    var client = new WebClient();
                    //Download the readme and changelog
                    DebugWrite("Fetching weapon stats...", 2);
                    try {
                        _pluginDescription = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/adkatsweaponstats.json");
                        DebugWrite("Weapon stats fetched.", 1);
                    }
                    catch (Exception) {
                        ConsoleError("Failed to fetch weapon stats.");
                    }
                    if (_pluginDescription != "DESCRIPTION FETCH FAILED|") {
                    }
                    DebugWrite("Setting weapon stat fetch handle.", 1);
                    _WeaponStatsWaitHandle.Set();
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error while fetching plugin description and changelog.", e));
                }
                LogThreadExit();
            }));
            //Start the thread
            StartAndLogThread(descFetcher);
        }

        private void SetupKeepAlive() {
            //Create a new thread to handle keep-alive
            //This thread will remain running for the duration the layer is online
            var keepAliveThread = new Thread(new ThreadStart(delegate {
                try {
                    Thread.CurrentThread.Name = "keepalive";
                    DateTime lastKeepAliveCheck = DateTime.UtcNow;
                    DateTime lastDatabaseConnectionCheck = DateTime.UtcNow;
                    while (true) {
                        //Check for keep alive every 60 seconds
                        if (_useKeepAlive && (DateTime.UtcNow - lastKeepAliveCheck).TotalSeconds > 60) {
                            Enable();
                            lastKeepAliveCheck = DateTime.UtcNow;

                            if (_aliveThreads.Count() >= 20) {
                                String aliveThreads = "";
                                lock (_aliveThreads) {
                                    aliveThreads = _aliveThreads.Values.Aggregate(aliveThreads, (current, aliveThread) => current + (aliveThread.Name + "[" + aliveThread.ManagedThreadId + "] "));
                                }
                                ConsoleWarn("Thread warning: " + aliveThreads);
                            }

                            if ((DateTime.UtcNow - _lastSuccessfulPlayerList).TotalSeconds > 120 && _isTestingAuthorized && _firstPlayerListComplete) {
                                //Create the report record
                                var record = new AdKatsRecord {
                                    record_source = AdKatsRecord.Sources.InternalAutomated,
                                    server_id = _serverID,
                                    command_type = _CommandKeyDictionary["player_calladmin"],
                                    command_numeric = 0,
                                    target_name = "AdKats",
                                    target_player = null,
                                    source_name = "AdKats",
                                    record_message = "Player Listing Offline"
                                };
                                //Process the record
                                QueueRecordForProcessing(record);
                            }
                        }
                        //Check for possible connection interuption every 10 seconds
                        if (_threadsReady && (DateTime.UtcNow - lastDatabaseConnectionCheck).TotalSeconds > 10) {
                            HandlePossibleDisconnect();
                        }
                        //Sleep 1 second between loops
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error while running keep-alive.", e));
                }
            }));
            //Start the thread
            keepAliveThread.Start();
        }

        public void InitWaitHandles() {
            //Initializes all wait handles 
            _TeamswapWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PlayerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _AccessFetchWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _KillProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PlayerListUpdateWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _MessageParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _CommandParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _DbCommunicationWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _ActionHandlingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _BanEnforcerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _HackerCheckerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _ServerInfoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _StatLoggerStatusWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void SetAllHandles() {
            //Opens all handles to make sure all threads complete one loop
            _TeamswapWaitHandle.Set();
            _PlayerProcessingWaitHandle.Set();
            _AccessFetchWaitHandle.Set();
            _KillProcessingWaitHandle.Set();
            _PlayerListUpdateWaitHandle.Set();
            _MessageParsingWaitHandle.Set();
            _CommandParsingWaitHandle.Set();
            _DbCommunicationWaitHandle.Set();
            _ActionHandlingWaitHandle.Set();
            _BanEnforcerWaitHandle.Set();
            _HackerCheckerWaitHandle.Set();
            _ServerInfoWaitHandle.Set();
            _StatLoggerStatusWaitHandle.Set();
            _EmailHandler._EmailProcessingWaitHandle.Set();
        }

        public void InitThreads() {
            try {
                //Creats all threads with their starting methods and set to run in the background
                _PlayerListingThread = new Thread(PlayerListingThreadLoop) {
                    IsBackground = true
                };

                _AccessFetchingThread = new Thread(AccessFetchingThreadLoop) {
                    IsBackground = true
                };

                _KillProcessingThread = new Thread(KillProcessingThreadLoop) {
                    IsBackground = true
                };

                _MessageProcessingThread = new Thread(MessagingThreadLoop) {
                    IsBackground = true
                };

                _CommandParsingThread = new Thread(CommandParsingThreadLoop) {
                    IsBackground = true
                };

                _DatabaseCommunicationThread = new Thread(DatabaseCommunicationThreadLoop) {
                    IsBackground = true
                };

                _ActionHandlingThread = new Thread(ActionHandlingThreadLoop) {
                    IsBackground = true
                };

                _TeamSwapThread = new Thread(TeamswapThreadLoop) {
                    IsBackground = true
                };

                _BanEnforcerThread = new Thread(BanEnforcerThreadLoop) {
                    IsBackground = true
                };

                _HackerCheckerThread = new Thread(HackerCheckerThreadLoop) {
                    IsBackground = true
                };
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while initializing threads.", e));
            }
        }

        public void StartThreads() {
            DebugWrite("Entering StartThreads", 7);
            try {
                //Start the main thread
                //DB Comm is the heart of AdKats, everything revolves around that thread
                StartAndLogThread(_DatabaseCommunicationThread);
                //Other threads are started within the db comm thread
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while starting processing threads.", e));
            }
            DebugWrite("Exiting StartThreads", 7);
        }

        private void Disable() {
            //Call Disable
            ExecuteCommand("procon.protected.plugins.enable", "AdKats", "False");
            //Set enabled false so threads begin exiting
            _pluginEnabled = false;
            _threadsReady = false;
        }

        private void Enable() {
            //Call Enable
            ExecuteCommand("procon.protected.plugins.enable", "AdKats", "True");
        }

        public void OnPluginLoadingEnv(List<String> lstPluginEnv) {
            foreach (String env in lstPluginEnv) {
                DebugWrite("^9OnPluginLoadingEnv: " + env, 7);
            }
            switch (lstPluginEnv[1]) {
                case "BF3":
                    _gameVersion = GameVersion.BF3;
                    break;
                case "BF4":
                    _gameVersion = GameVersion.BF4;
                    break;
            }
            DebugWrite("^1Game Version: " + _gameVersion, 1);
            //Initialize the email handler
            _EmailHandler = new EmailHandler(this);
            //Update faction info
            //Load initial factions
            OnTeamFactionOverride(1, 0);
            OnTeamFactionOverride(2, 1);
            OnTeamFactionOverride(3, 0);
            OnTeamFactionOverride(4, 1);
            UpdateFactions();
        }

        public override void OnVersion(String serverType, String version) {
            _gamePatchVersion = version;
        }

        public override void OnTeamFactionOverride(Int32 targetTeamID, Int32 overrideTeamId) {
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (_teamDictionary) {
                if (_teamDictionary.ContainsKey(targetTeamID)) {
                    _teamDictionary.Remove(targetTeamID);
                }
                switch (overrideTeamId) {
                    case 0:
                        _teamDictionary.Add(targetTeamID, new AdKatsTeam(targetTeamID, "US", "US Army", "United States Army"));
                        DebugWrite("Assigning team ID " + targetTeamID + " to US", 4);
                        break;
                    case 1:
                        _teamDictionary.Add(targetTeamID, new AdKatsTeam(targetTeamID, "RU", "Russian Army", "Russian Federation Army"));
                        DebugWrite("Assigning team ID " + targetTeamID + " to RU", 4);
                        break;
                    case 2:
                        _teamDictionary.Add(targetTeamID, new AdKatsTeam(targetTeamID, "CN", "Chinese Army", "Chinese People's Liberation Army"));
                        DebugWrite("Assigning team ID " + targetTeamID + " to CN", 4);
                        break;
                    default:
                        ConsoleError("Team ID " + overrideTeamId + " was not understood.");
                        break;
                }
            }
        }

        public override void OnFairFight(bool isEnabled) {
            _fairFightEnabled = isEnabled;
        }

        public override void OnIsHitIndicator(bool isEnabled) {
            _hitIndicatorEnabled = isEnabled;
        }

        public override void OnCommander(bool isEnabled) {
            _commanderEnabled = isEnabled;
        }

        public override void OnForceReloadWholeMags(bool isEnabled) {
            _forceReloadWholeMags = isEnabled;
        }

        public override void OnServerType(String value) {
            _serverType = value;
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

        public void UpdateFactions() {
            if (_gameVersion == GameVersion.BF3) {
                OnTeamFactionOverride(1, 0);
                OnTeamFactionOverride(2, 1);
                OnTeamFactionOverride(3, 0);
                OnTeamFactionOverride(4, 1);
            }
            else if (_gameVersion == GameVersion.BF4) {
                ExecuteCommand("procon.protected.send", "vars.teamFactionOverride");
            }
        }

        //DONE

        public override void OnPlayerTeamChange(String soldierName, Int32 teamId, Int32 squadId) {
            DebugWrite("Entering OnPlayerTeamChange", 7);
            try {
                //When a player changes team, tell teamswap to recheck queues
                _TeamswapWaitHandle.Set();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling player team change.", e));
            }
            DebugWrite("Exiting OnPlayerTeamChange", 7);
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset cpsSubset) {
            DebugWrite("Entering OnListPlayers", 7);
            try {
                //Only handle the list if it is an "All players" list
                if (cpsSubset.Subset == CPlayerSubset.PlayerSubsetType.All) {
                    //Return if small duration (5 seconds) since last player list
                    if ((DateTime.UtcNow - _lastSuccessfulPlayerList) < TimeSpan.FromSeconds(5)) {
                        return;
                    }
                    //Only perform the following if all threads are ready
                    if (_threadsReady) {
                        QueuePlayerListForProcessing(players);
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while listing players.", e));
            }
            DebugWrite("Exiting OnListPlayers", 7);
        }

        private void QueuePlayerListForProcessing(List<CPlayerInfo> players) {
            DebugWrite("Entering QueuePlayerListForProcessing", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue player list for processing", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_PlayerListProcessingQueue) {
                        _PlayerListProcessingQueue.Enqueue(players);
                        DebugWrite("Player list queued for processing", 6);
                        _PlayerProcessingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing player list for processing.", e));
            }
            DebugWrite("Exiting QueuePlayerListForProcessing", 7);
        }

        private void QueuePlayerForRemoval(CPlayerInfo player) {
            DebugWrite("Entering QueuePlayerForRemoval", 7);
            try {
                if (_pluginEnabled && _firstPlayerListComplete) {
                    DebugWrite("Preparing to queue player list for processing", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_PlayerRemovalProcessingQueue) {
                        _PlayerRemovalProcessingQueue.Enqueue(player);
                        DebugWrite("Player removal queued for processing", 6);
                        _PlayerProcessingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing player for removal.", e));
            }
            DebugWrite("Exiting QueuePlayerForRemoval", 7);
        }

        public void PlayerListingThreadLoop() {
            try {
                DebugWrite("PLIST: Starting Player Listing Thread", 1);
                Thread.CurrentThread.Name = "playerlisting";
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("PLIST: Entering Player Listing Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("PLIST: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Get all unparsed inbound lists
                        List<CPlayerInfo> inboundPlayerList = null;
                        if (_PlayerListProcessingQueue.Count > 0) {
                            DebugWrite("PLIST: Preparing to lock player list queues to retrive new player lists", 7);
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_PlayerListProcessingQueue) {
                                DebugWrite("PLIST: Inbound player lists found. Grabbing.", 6);
                                while (_PlayerListProcessingQueue.Any()) {
                                    inboundPlayerList = _PlayerListProcessingQueue.Dequeue();
                                }
                                //Clear the queue for next run
                                _PlayerListProcessingQueue.Clear();
                            }
                        }
                        else {
                            inboundPlayerList = new List<CPlayerInfo>();
                        }

                        //Get all unparsed inbound player removals
                        Queue<CPlayerInfo> inboundPlayerRemoval = null;
                        if (_PlayerRemovalProcessingQueue.Count > 0) {
                            DebugWrite("PLIST: Preparing to lock player removal queue to retrive new player removals", 7);
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_PlayerRemovalProcessingQueue) {
                                DebugWrite("PLIST: Inbound player removals found. Grabbing.", 6);
                                if (_PlayerRemovalProcessingQueue.Any()) {
                                    inboundPlayerRemoval = new Queue<CPlayerInfo>(_PlayerRemovalProcessingQueue.ToArray());
                                }
                                //Clear the queue for next run
                                _PlayerRemovalProcessingQueue.Clear();
                            }
                        }
                        else {
                            inboundPlayerRemoval = new Queue<CPlayerInfo>();
                        }

                        if (!inboundPlayerList.Any() && !inboundPlayerRemoval.Any() && !_PlayerRoleRefetch) {
                            DebugWrite("PLIST: No inbound player lists or removals found. Waiting for Input.", 5);
                            //Wait for input
                            if (!_firstPlayerListComplete) {
                                OnlineAdminSayMessage("Startup sequence has begun. Commands will be online shortly.");
                                ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
                            }
                            _PlayerProcessingWaitHandle.Reset();
                            _PlayerProcessingWaitHandle.WaitOne(Timeout.Infinite);
                            continue;
                        }

                        if (_isTestingAuthorized)
                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                        lock (_playerDictionary) {
                            //Firstly, go through removal queue, remove all names, and log them.
                            var removedPlayers = new List<string>();
                            while (inboundPlayerRemoval.Any()) {
                                CPlayerInfo playerInfo = inboundPlayerRemoval.Dequeue();
                                AdKatsPlayer aPlayer;
                                if (_playerDictionary.TryGetValue(playerInfo.SoldierName, out aPlayer)) {
                                    if (aPlayer.TargetedRecords.Count > 0 && !aPlayer.TargetedRecords.Any(aRecord => aRecord.command_action.command_key == "player_kick" || aRecord.command_action.command_key == "player_ban_temp" || aRecord.command_action.command_key == "player_ban_perm" || aRecord.command_action.command_key == "banenforcer_enforce" || aRecord.command_action.command_key == "player_changeip" || aRecord.command_action.command_key == "player_changename" || aRecord.command_action.command_key.Contains("self_"))) {
                                        OnlineAdminSayMessage("Targeted player " + aPlayer.player_name + " has left the server.");
                                    }
                                }
                                RemovePlayerFromDictionary(playerInfo.SoldierName, false);
                                removedPlayers.Add(playerInfo.SoldierName);
                            }
                            var validPlayers = new List<String>();
                            if (inboundPlayerList.Count > 0) {
                                DebugWrite("Listing Players", 5);
                                //Reset the player counts of both sides and recount everything
                                //Loop over all players in the list
                                Int32 team1PC = 0;
                                Int32 team2PC = 0;
                                Int32 team3PC = 0;
                                Int32 team4PC = 0;

                                foreach (CPlayerInfo playerInfo in inboundPlayerList.Where(player => !removedPlayers.Contains(player.SoldierName))) {
                                    if (!_pluginEnabled) {
                                        break;
                                    }
                                    validPlayers.Add(playerInfo.SoldierName);
                                    //Check if the player is already in the player dictionary
                                    AdKatsPlayer aPlayer = null;
                                    if (_playerDictionary.TryGetValue(playerInfo.SoldierName, out aPlayer)) {
                                        //If they are, only update the internal frostbite player info
                                        _playerDictionary[playerInfo.SoldierName].frostbitePlayerInfo = playerInfo;
                                    }
                                    else {
                                        //If they aren't in the list, fetch their profile from the database
                                        aPlayer = FetchPlayer(true, false, null, -1, playerInfo.SoldierName, playerInfo.GUID, null);
                                        if (aPlayer == null) {
                                            //Do not handle the player if not returned
                                            continue;
                                        }
                                        //Add the frostbite player info
                                        aPlayer.frostbitePlayerInfo = playerInfo;
                                        //Set their last death/spawn times
                                        aPlayer.lastDeath = DateTime.UtcNow;
                                        aPlayer.lastSpawn = DateTime.UtcNow;
                                        //Add them to the dictionary
                                        _playerDictionary.Add(playerInfo.SoldierName, aPlayer);
                                        UpdatePlayerReputation(aPlayer);
                                        //If using ban enforcer, check the player's ban status
                                        if (_UseBanEnforcer) {
                                            QueuePlayerForBanCheck(aPlayer);
                                        }
                                        else if (_UseHackerChecker) {
                                            //Queue the player for a hacker check
                                            QueuePlayerForHackerCheck(aPlayer);
                                        }
                                    }
                                    switch (playerInfo.TeamID) {
                                        case 0:
                                            //Do nothing, team 0 is the joining team
                                            break;
                                        case 1:
                                            team1PC++;
                                            break;
                                        case 2:
                                            team2PC++;
                                            break;
                                        case 3:
                                            team3PC++;
                                            break;
                                        case 4:
                                            team4PC++;
                                            break;
                                        default:
                                            ConsoleError("Team ID " + playerInfo.TeamID + " for player " + playerInfo.SoldierName + " was invalid.");
                                            break;
                                    }
                                }
                                _teamDictionary[1].UpdatePlayerCount(team1PC);
                                _teamDictionary[2].UpdatePlayerCount(team2PC);
                                _teamDictionary[3].UpdatePlayerCount(team3PC);
                                _teamDictionary[4].UpdatePlayerCount(team4PC);
                                //Make sure the player dictionary is clean of any straglers
                                Int32 straglerCount = 0;
                                Int32 dicCount = _playerDictionary.Count;
                                foreach (string playerName in _playerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList()) {
                                    straglerCount++;
                                    DebugWrite("PLIST: Removing " + playerName + " from current player list (VIA CLEANUP).", 4);
                                    _playerDictionary.Remove(playerName);
                                }
                                if (straglerCount > 1 && straglerCount > (dicCount / 2)) {
                                    var record = new AdKatsRecord {
                                        record_source = AdKatsRecord.Sources.InternalAutomated,
                                        isDebug = true,
                                        server_id = _serverID,
                                        command_type = _CommandKeyDictionary["player_calladmin"],
                                        command_numeric = 0,
                                        target_name = "Server",
                                        target_player = null,
                                        source_name = "AdKats",
                                        record_message = "Server Crashed (" + dicCount + " Players Lost)"
                                    };
                                    //Process the record
                                    QueueRecordForProcessing(record);
                                    ConsoleError(record.record_message);
                                    //Set round ended
                                    _CurrentRoundState = RoundState.Ended;
                                }
                            }
                            if (_PlayerRoleRefetch) {
                                //Update roles for all online players
                                foreach (AdKatsPlayer aPlayer in _playerDictionary.Values) {
                                    AssignPlayerRole(aPlayer);
                                }
                                _PlayerRoleRefetch = false;
                            }
                        }

                        //Update last successful player list time
                        _lastSuccessfulPlayerList = DateTime.UtcNow;
                        //Set required handles 
                        _PlayerListUpdateWaitHandle.Set();
                        _TeamswapWaitHandle.Set();
                        if (!_firstPlayerListComplete) {
                            _firstPlayerListComplete = true;
                            OnlineAdminSayMessage("Startup processing complete [" + FormatTimeString(DateTime.UtcNow - _AdKatsStartTime, 3) + "]. Commands are now online.");
                            DebugWrite("Startup processing complete [" + FormatTimeString(DateTime.UtcNow - _AdKatsStartTime, 3) + "]. Commands are now online.", 1);
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            ConsoleWarn("player listing thread was force aborted. Exiting.");
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in player listing thread. Skipping loop.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                }
                DebugWrite("PLIST: Ending Player Listing Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in player listing thread.", e));
            }
        }

        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo cpbiPlayer) {
            try {
                DebugWrite("OPPI: OnPunkbusterPlayerInfo fired!", 7);
                AdKatsPlayer aPlayer;
                if (_playerDictionary.TryGetValue(cpbiPlayer.SoldierName, out aPlayer)) {
                    DebugWrite("OPPI: PB player already in the player list.", 7);
                    Boolean updatePlayer = false;
                    //Update the player with pb info
                    aPlayer.PBPlayerInfo = cpbiPlayer;
                    aPlayer.player_pbguid = cpbiPlayer.GUID;
                    aPlayer.player_slot = cpbiPlayer.SlotID;
                    String player_ip = cpbiPlayer.Ip.Split(':')[0];
                    if (player_ip != aPlayer.player_ip) {
                        updatePlayer = true;
                        DebugWrite(aPlayer.player_name + " changed their IP from " + (String.IsNullOrEmpty(aPlayer.player_ip) ? ("No previous IP on record") : (aPlayer.player_ip)) + " to " + player_ip + ". Updating the database.", 2);
                        var record = new AdKatsRecord {
                            record_source = AdKatsRecord.Sources.InternalAutomated,
                            server_id = _serverID,
                            command_type = _CommandKeyDictionary["player_changeip"],
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "AdKats",
                            record_message = (String.IsNullOrEmpty(aPlayer.player_ip) ? ("No previous IP on record") : (aPlayer.player_ip))
                        };
                        QueueRecordForProcessing(record);
                    }
                    aPlayer.player_ip = player_ip;
                    if (updatePlayer) {
                        DebugWrite("OPPI: Queueing existing player " + aPlayer.player_name + " for update.", 4);
                        UpdatePlayer(aPlayer);
                        //If using ban enforcer, queue player for update
                        if (_UseBanEnforcer) {
                            QueuePlayerForBanCheck(aPlayer);
                        }
                    }
                }
                DebugWrite("OPPI: Player slot: " + cpbiPlayer.SlotID, 7);
                DebugWrite("OPPI: OnPunkbusterPlayerInfo finished!", 7);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while processing punkbuster info.", e));
            }
        }

        private void FetchAllAccess(Boolean async) {
            if (async) {
                _AccessFetchWaitHandle.Set();
            }
            else if (_threadsReady) {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_userCache) {
                    FetchCommands();
                    FetchRoles();
                    FetchUserList();
                }
            }
        }

        private void AccessFetchingThreadLoop() {
            try {
                DebugWrite("FACC: Starting Access Fetching Thread", 1);
                Thread.CurrentThread.Name = "AccessFetching";
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("FACC: Entering Access Fetching Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("FACC: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        if (_isTestingAuthorized)
                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                        lock (_userCache) {
                            FetchCommands();
                            FetchRoles();
                            FetchUserList();
                        }

                        DebugWrite("FACC: Access fetch waiting for Input.", 5);
                        _AccessFetchWaitHandle.Reset();
                        _AccessFetchWaitHandle.WaitOne(Timeout.Infinite);
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            ConsoleWarn("Access Fetching thread was force aborted. Exiting.");
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in Access Fetching thread. Skipping loop.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                }
                DebugWrite("FACC: Ending Access Fetching Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in Access Fetching thread.", e));
            }
        }

        private void StartRoundTicketLogger() {
            try {
                if (!_pluginEnabled || !_threadsReady || !_isTestingAuthorized) {
                    return;
                }
                var roundLoggerThread = new Thread(new ThreadStart(delegate {
                    try {
                        Thread.CurrentThread.Name = "roundticketlogger";
                        Int32 TPCSCounter = 0;
                        Boolean TPCSActionTaken = false;
                        Int32 roundTimeSeconds = 0;
                        Int32 currentRoundID = 0;
                        using (MySqlConnection connection = GetDatabaseConnection()) {
                            using (MySqlCommand command = connection.CreateCommand()) {
                                command.CommandText = @"
                                SELECT
	                                MAX(`round_id`) AS `max_round_id`
                                FROM
	                                `tbl_extendedroundstats`
                                WHERE 
                                    `server_id` = @server_id";
                                command.Parameters.AddWithValue("server_id", _serverID);
                                using (MySqlDataReader reader = command.ExecuteReader()) {
                                    if (reader.Read()) {
                                        Int32 oldRoundID = reader.GetInt32("max_round_id");
                                        ConsoleSuccess("Old round ID: " + oldRoundID);
                                        currentRoundID = oldRoundID + 1;
                                        ConsoleSuccess("New round ID: " + currentRoundID);
                                    }
                                    else {
                                        currentRoundID = 1;
                                    }
                                }
                            }
                        }

                        var watch = new Stopwatch();
                        AdKatsTeam team1 = _teamDictionary[1];
                        AdKatsTeam team2 = _teamDictionary[2];
                        while (true) {
                            watch.Reset();
                            watch.Start();
                            //Check for exit and entry cases
                            if (_CurrentRoundState == RoundState.Ended || !_pluginEnabled) {
                                break;
                            }
                            if (_CurrentRoundState == RoundState.Loaded) {
                                Thread.Sleep(TimeSpan.FromSeconds(2));
                                continue;
                            }
                            using (MySqlConnection connection = GetDatabaseConnection()) {
                                using (MySqlCommand command = connection.CreateCommand()) {
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
                                        `team2_tpm`
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
                                        @team2_tpm
                                    )";
                                    command.Parameters.AddWithValue("@server_id", _serverID);
                                    command.Parameters.AddWithValue("@round_id", currentRoundID);
                                    command.Parameters.AddWithValue("@round_elapsedTimeSec", roundTimeSeconds);
                                    command.Parameters.AddWithValue("@team1_count", team1.TeamPlayerCount);
                                    command.Parameters.AddWithValue("@team2_count", team2.TeamPlayerCount);
                                    command.Parameters.AddWithValue("@team1_score", team1.TeamTotalScore);
                                    command.Parameters.AddWithValue("@team2_score", team2.TeamTotalScore);
                                    command.Parameters.AddWithValue("@team1_spm", team1.TeamScoreDifferenceRate);
                                    command.Parameters.AddWithValue("@team2_spm", team2.TeamScoreDifferenceRate);
                                    command.Parameters.AddWithValue("@team1_tickets", team1.TeamTicketCount);
                                    command.Parameters.AddWithValue("@team2_tickets", team2.TeamTicketCount);
                                    command.Parameters.AddWithValue("@team1_tpm", team1.TeamTicketDifferenceRate);
                                    command.Parameters.AddWithValue("@team2_tpm", team2.TeamTicketDifferenceRate);
                                    //Attempt to execute the query
                                    if (command.ExecuteNonQuery() > 0) {
                                        DebugWrite("round stat pushed to database", 5);
                                    }
                                }
                            }

                            if (_serverInfo.Map == "MP_Prison" && false) {
                                AdKatsTeam winningTeam, losingTeam;
                                if (team1.TeamTicketCount > team2.TeamTicketCount) {
                                    winningTeam = team1;
                                    losingTeam = team2;
                                }
                                else {
                                    winningTeam = team2;
                                    losingTeam = team1;
                                }
                                Double ticketDifference = Math.Abs(team1.TeamTicketCount - team2.TeamTicketCount);
                                Double winningPower = Math.Abs(losingTeam.TeamTicketDifferenceRate);
                                Double losingPower = Math.Abs(winningTeam.TeamTicketDifferenceRate);
                                Double winningTicketPower = (ticketDifference * winningPower);
                                ConsoleWrite("TicketPower: " + String.Format("{0:0.00}", winningTicketPower));
                                /*if (winningTicketPower > 20000 && !TPCSActionTaken)
                                {
                                    this.ConsoleWarn("ISSUING TICKET POWER NUKE ON " + winningTeam.TeamName.ToUpper());
                                    TPCSActionTaken = true;
                                }*/
                                if (winningPower > losingPower) {
                                    if (winningPower >= 60 && ticketDifference > 200) {
                                        ConsoleWarn(winningTeam.TeamName + " is winning, and baseraping.");
                                        if (roundTimeSeconds > 400 && winningTeam.TeamPlayerCount > 20 && losingTeam.TeamPlayerCount > 20 && winningTeam.TeamTicketCount > 150 && losingTeam.TeamTicketCount > 150) {
                                            ConsoleWarn("ISSUING BASERAPE NUKE ON " + winningTeam.TeamName.ToUpper() + " (" + winningTeam.TeamTicketCount + ":" + losingTeam.TeamTicketCount + ")");
                                            TPCSActionTaken = true;
                                            AdminTellMessage(winningTeam.TeamName + " is being nuked for baserape! Advance! Advance!");
                                            //Create the nuke record
                                            var record = new AdKatsRecord {
                                                record_source = AdKatsRecord.Sources.InternalAutomated,
                                                server_id = _serverID,
                                                command_type = _CommandKeyDictionary["server_nuke"],
                                                command_numeric = winningTeam.TeamID,
                                                target_name = winningTeam.TeamName,
                                                source_name = "AutoAdmin",
                                                record_message = "Baserape"
                                            };
                                            //Process the record
                                            QueueRecordForProcessing(record);
                                            TPCSCounter = 0;
                                        }
                                    }
                                    else if (winningPower >= 55) {
                                        ConsoleWarn(winningTeam.TeamName + " is winning, and has 4+ flags.");
                                        TPCSCounter = 0;
                                    }
                                    else if (winningPower >= 45) {
                                        ConsoleWarn(winningTeam.TeamName + " is winning, and has 3+ flags.");
                                        TPCSCounter = 0;
                                    }
                                    else {
                                        ConsoleSuccess("Teams are equal. With " + winningTeam.TeamName + " currently winning.");
                                        TPCSCounter = 0;
                                    }
                                }
                                else {
                                    if (losingPower >= 45) {
                                        ConsoleWarn(losingTeam.TeamName + " is making a comeback.");
                                        AdminSayMessage(losingTeam.TeamName + " is making a comeback! Keep pushing!");
                                        TPCSCounter = 0;
                                    }
                                    else {
                                        ConsoleSuccess("Teams are equal. With " + winningTeam.TeamName + " currently winning.");
                                        TPCSCounter = 0;
                                    }
                                }
                            }

                            watch.Stop();
                            if (watch.Elapsed.TotalSeconds < 30) {
                                Thread.Sleep(TimeSpan.FromSeconds(30) - watch.Elapsed);
                            }
                            roundTimeSeconds += 30;
                        }
                    }
                    catch (Exception e) {
                        //HandleException(new AdKatsException("Error in round stat logger thread", e));
                    }
                    LogThreadExit();
                }));

                //Start the thread
                StartAndLogThread(roundLoggerThread);
            }
            catch (Exception e) {
                //HandleException(new AdKatsException("Error while starting round ticket logger", e));
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo) {
            DebugWrite("Entering OnServerInfo", 7);
            try {
                if (_pluginEnabled) {
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_serverInfo) {
                        if (serverInfo != null) {
                            //Get the server info
                            _serverInfo = serverInfo;
                            if (serverInfo.TeamScores != null) {
                                List<TeamScore> listCurrTeamScore = serverInfo.TeamScores;
                                //During round change, teams don't exist
                                if (listCurrTeamScore.Count > 0) {
                                    foreach (TeamScore score in listCurrTeamScore) {
                                        AdKatsTeam currentTeam;
                                        if (_teamDictionary.TryGetValue(score.TeamID, out currentTeam)) {
                                            currentTeam.UpdateTicketCount(score.Score);
                                            currentTeam.UpdateTotalScore(_playerDictionary.Values.Where(aPlayer => aPlayer.frostbitePlayerInfo.TeamID == score.TeamID).Aggregate<AdKatsPlayer, double>(0, (current, aPlayer) => current + aPlayer.frostbitePlayerInfo.Score));
                                        }
                                        else {
                                            ConsoleError("Team ID " + score.TeamID + " could not be recognized.");
                                        }
                                    }
                                }
                                else {
                                    DebugWrite("Server info fired while changing rounds, no teams to parse.", 5);
                                }
                            }

                            AdKatsTeam team1 = _teamDictionary[1];
                            AdKatsTeam team2 = _teamDictionary[2];
                            if (team1.TeamTicketCount >= 0 && team2.TeamTicketCount >= 0) {
                                _LowestTicketCount = (team1.TeamTicketCount < team2.TeamTicketCount) ? (team1.TeamTicketCount) : (team2.TeamTicketCount);
                                _HighestTicketCount = (team1.TeamTicketCount > team2.TeamTicketCount) ? (team1.TeamTicketCount) : (team2.TeamTicketCount);
                            }

                            _serverName = serverInfo.ServerName;
                            //Only activate the following on ADK servers.
                            Boolean wasADK = _isTestingAuthorized;
                            _isTestingAuthorized = serverInfo.ServerName.Contains("=ADK=");
                            if (!wasADK && _isTestingAuthorized) {
                                ConsoleWrite("Server is priviledged for testing.");
                                UpdateSettingPage();
                            }
                        }
                        else {
                            HandleException(new AdKatsException("Server info was null"));
                        }
                        _ServerInfoWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while processing server info.", e));
            }
            DebugWrite("Exiting OnServerInfo", 7);
        }

        public override void OnLevelLoaded(String strMapFileName, String strMapMode, Int32 roundsPlayed, Int32 roundsTotal) {
            DebugWrite("Entering OnLevelLoaded", 7);
            try {
                if (_pluginEnabled) {
                    _CurrentRoundState = RoundState.Loaded;
                    //Completely clear all round-specific data
                    _RoundReports.Clear();
                    _RoundMutedPlayers.Clear();
                    _TeamswapRoundWhitelist.Clear();
                    _ActionConfirmDic.Clear();
                    _ActOnSpawnDictionary.Clear();
                    _ActOnIsAliveDictionary.Clear();
                    _TeamswapOnDeathMoveDic.Clear();
                    //AutoWhitelistPlayers();
                    _Team1MoveQueue.Clear();
                    _Team2MoveQueue.Clear();
                    _RoundCookers.Clear();
                    //Update the factions 
                    UpdateFactions();
                    //Enable round timer
                    if (_useRoundTimer) {
                        StartRoundTimer();
                    }
                    StartRoundTicketLogger();
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling level load.", e));
            }
            DebugWrite("Exiting OnLevelLoaded", 7);
        }

        //Round ended stuff
        public override void OnRoundOverTeamScores(List<TeamScore> teamScores) {
            _CurrentRoundState = RoundState.Ended;
        }

        public override void OnRunNextLevel() {
            _CurrentRoundState = RoundState.Ended;
        }

        //Move delayed players when they are killed
        public override void OnPlayerKilled(Kill kKillerVictimDetails) {
            DebugWrite("Entering OnPlayerKilled", 7);
            try {
                //If the plugin is not enabled just return
                if (!_pluginEnabled || !_threadsReady) {
                    return;
                }
                //Otherwise, queue the kill for processing
                QueueKillForProcessing(kKillerVictimDetails);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling onPlayerKilled.", e));
            }
            DebugWrite("Exiting OnPlayerKilled", 7);
        }

        public override void OnPlayerIsAlive(string soldierName, bool isAlive) {
            DebugWrite("Entering OnPlayerIsAlive", 7);
            try {
                if (!_pluginEnabled)
                    return;
                if (!_ActOnIsAliveDictionary.ContainsKey(soldierName)) {
                    return;
                }
                AdKatsRecord aRecord;
                lock (_ActOnIsAliveDictionary) {
                    if (_ActOnIsAliveDictionary.TryGetValue(soldierName, out aRecord)) {
                        _ActOnIsAliveDictionary.Remove(aRecord.target_player.player_name);
                        aRecord.isAliveChecked = true;
                        switch (aRecord.command_action.command_key) {
                            case "player_kill":
                            case "player_kill_lowpop":
                                if (isAlive) {
                                    QueueRecordForActionHandling(aRecord);
                                }
                                else {
                                    DebugWrite(aRecord.target_player.player_name + " is dead. Queueing them for kill on-spawn.", 3);
                                    SendMessageToSource(aRecord, aRecord.target_player.player_name + " is dead. Queueing them for kill on-spawn.");
                                    DebugWrite("Queueing player for kill on spawn.", 3);
                                    if (!_ActOnSpawnDictionary.ContainsKey(aRecord.target_player.player_name)) {
                                        if (_isTestingAuthorized)
                                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                        lock (_ActOnSpawnDictionary) {
                                            aRecord.command_action = _CommandKeyDictionary["player_kill_repeat"];
                                            _ActOnSpawnDictionary.Add(aRecord.target_player.player_name, aRecord);
                                        }
                                    }
                                }
                                break;
                            default:
                                ConsoleError("Command " + aRecord.command_action.command_key + " not useable in OnPlayerIsAlive");
                                break;
                        }
                    }
                    else {
                        ConsoleWarn(soldierName + " not not fetchable from the isalive dictionary.");
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling OnPlayerIsAlive.", e));
            }
            DebugWrite("Exiting OnPlayerIsAlive", 7);
        }

        private void QueueKillForProcessing(Kill kKillerVictimDetails) {
            DebugWrite("Entering queueKillForProcessing", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue kill for processing", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_KillProcessingQueue) {
                        _KillProcessingQueue.Enqueue(kKillerVictimDetails);
                        DebugWrite("Kill queued for processing", 6);
                        _KillProcessingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing kill for processing.", e));
            }
            DebugWrite("Exiting queueKillForProcessing", 7);
        }

        public void KillProcessingThreadLoop() {
            try {
                DebugWrite("KILLPROC: Starting Kill Processing Thread", 1);
                Thread.CurrentThread.Name = "killprocessing";
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("KILLPROC: Entering Kill Processing Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("KILLPROC: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Get all unprocessed inbound kills
                        Queue<Kill> inboundPlayerKills;
                        if (_KillProcessingQueue.Count > 0) {
                            DebugWrite("KILLPROC: Preparing to lock inbound kill queue to retrive new player kills", 7);
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_KillProcessingQueue) {
                                DebugWrite("KILLPROC: Inbound kills found. Grabbing.", 6);
                                //Grab all kills in the queue
                                inboundPlayerKills = new Queue<Kill>(_KillProcessingQueue.ToArray());
                                //Clear the queue for next run
                                _KillProcessingQueue.Clear();
                            }
                        }
                        else {
                            DebugWrite("KILLPROC: No inbound player kills. Waiting for Input.", 4);
                            //Wait for input
                            _KillProcessingWaitHandle.Reset();
                            _KillProcessingWaitHandle.WaitOne(Timeout.Infinite);
                            continue;
                        }

                        //Loop through all kils in order that they came in
                        while (inboundPlayerKills.Count > 0) {
                            if (!_pluginEnabled) {
                                break;
                            }
                            DebugWrite("KILLPROC: begin reading player kills", 6);
                            //Dequeue the first/next kill
                            Kill playerKill = inboundPlayerKills.Dequeue();

                            //Call processing on the player kill
                            ProcessPlayerKill(playerKill);
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("kill processing thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in kill processing thread.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                }
                DebugWrite("KILLPROC: Ending Kill Processing Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in kill processing thread.", e));
            }
        }

        private void ProcessPlayerKill(Kill kKillerVictimDetails) {
            try
            {
                UploadWeaponCode(kKillerVictimDetails.DamageType);
                if (!_firstPlayerListComplete) {
                    return;
                }
                AdKatsPlayer victim = null;
                _playerDictionary.TryGetValue(kKillerVictimDetails.Victim.SoldierName, out victim);
                AdKatsPlayer killer = null;
                _playerDictionary.TryGetValue(kKillerVictimDetails.Killer.SoldierName, out killer);
                if (victim == null || killer == null) {
                    return;
                }
                //Used for delayed player moving
                if (_TeamswapOnDeathMoveDic.Count > 0) {
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_TeamswapOnDeathCheckingQueue) {
                        _TeamswapOnDeathCheckingQueue.Enqueue(kKillerVictimDetails.Victim);
                        _TeamswapWaitHandle.Set();
                    }
                }

                Boolean gKillHandled = false;
                //Update player death information
                if (_playerDictionary.ContainsKey(kKillerVictimDetails.Victim.SoldierName)) {
                    if (victim != null) {
                        DebugWrite("Setting " + victim.player_name + " time of death to " + kKillerVictimDetails.TimeOfDeath, 7);
                        victim.lastDeath = DateTime.UtcNow;
                        //Only add the last death if it's not a death by admin
                        if (!String.IsNullOrEmpty(kKillerVictimDetails.Killer.SoldierName)) {
                            try {
                                //ADK grenade cooking catcher
                                if (_useExperimentalTools && _UseGrenadeCookCatcher) {
                                    if (_RoundCookers == null) {
                                        _RoundCookers = new Dictionary<String, AdKatsPlayer>();
                                    }
                                    const double possibleRange = 750.00;
                                    //Update killer information
                                    if (killer != null) {
                                        //Initialize / clean up the recent kills queue
                                        if (killer.RecentKills == null) {
                                            killer.RecentKills = new Queue<KeyValuePair<AdKatsPlayer, DateTime>>();
                                        }
                                        //Only keep the last 6 kills in memory
                                        while (killer.RecentKills.Count > 1) {
                                            killer.RecentKills.Dequeue();
                                        }
                                        //Add the player
                                        killer.RecentKills.Enqueue(new KeyValuePair<AdKatsPlayer, DateTime>(victim, kKillerVictimDetails.TimeOfDeath));
                                        //Check for cooked grenade and non-suicide
                                        if (kKillerVictimDetails.DamageType.Contains("M67") || kKillerVictimDetails.DamageType.Contains("V40")) {
                                            if (kKillerVictimDetails.Killer.SoldierName != kKillerVictimDetails.Victim.SoldierName) {
                                                Double fuseTime = 0;
                                                if (kKillerVictimDetails.DamageType.Contains("M67")) {
                                                    if (_gameVersion == GameVersion.BF3) {
                                                        fuseTime = 3735.00;
                                                    }
                                                    else if (_gameVersion == GameVersion.BF4) {
                                                        fuseTime = 3132.00;
                                                    }
                                                }
                                                else if (kKillerVictimDetails.DamageType.Contains("V40")) {
                                                    fuseTime = 2865.00;
                                                }
                                                Boolean told = false;
                                                var possible = new List<KeyValuePair<AdKatsPlayer, String>>();
                                                var sure = new List<KeyValuePair<AdKatsPlayer, String>>();
                                                foreach (var cooker in killer.RecentKills) {
                                                    //Get the actual time since cooker value
                                                    Double milli = kKillerVictimDetails.TimeOfDeath.Subtract(cooker.Value).TotalMilliseconds;

                                                    //Calculate the percentage probability
                                                    Double probability;
                                                    if (Math.Abs(milli - fuseTime) < possibleRange) {
                                                        probability = (1 - Math.Abs((milli - fuseTime) / possibleRange)) * 100;
                                                        DebugWrite(cooker.Key.player_name + " cooking probability: " + probability + "%", 2);
                                                    }
                                                    else {
                                                        probability = 0.00;
                                                    }

                                                    //If probability > 60% report the player and add them to the round cookers list
                                                    if (probability > 60.00) {
                                                        DebugWrite(cooker.Key.player_name + " in " + killer.player_name + "'s recent kills has a " + probability + "% cooking probability.", 2);
                                                        gKillHandled = true;
                                                        //Code to avoid spam
                                                        if (killer.lastAction.AddSeconds(2) < DateTime.UtcNow) {
                                                            killer.lastAction = DateTime.UtcNow;
                                                        }
                                                        else {
                                                            DebugWrite("Skipping additional auto-actions for multi-kill event.", 2);
                                                            continue;
                                                        }

                                                        if (!told) {
                                                            //Inform the victim player that they will not be punished
                                                            PlayerSayMessage(kKillerVictimDetails.Killer.SoldierName, "You appear to be a victim of grenade cooking and will NOT be punished.");
                                                            told = true;
                                                        }

                                                        //Create the probability String
                                                        String probString = ((int) probability) + "-" + ((int) milli);

                                                        //If the player is already on the round cooker list, ban them
                                                        if (_RoundCookers.ContainsKey(cooker.Key.player_name)) {
                                                            //Create the ban record
                                                            var record = new AdKatsRecord {
                                                                record_source = AdKatsRecord.Sources.InternalAutomated,
                                                                server_id = _serverID,
                                                                command_type = _CommandKeyDictionary["player_punish"],
                                                                command_numeric = 0,
                                                                target_name = cooker.Key.player_name,
                                                                target_player = cooker.Key,
                                                                source_name = "AutoAdmin",
                                                                record_message = "Rules: Cooking Grenades [" + probString + "-X] [Victim " + killer.player_name + " Protected]"
                                                            };
                                                            //Process the record
                                                            QueueRecordForProcessing(record);
                                                            //adminSay("Punishing " + killer.player_name + " for " + record.record_message);
                                                            DebugWrite(record.target_player.player_name + " punished for " + record.record_message, 2);
                                                            return;
                                                        }
                                                        //else if probability > 92.5% add them to the SURE list, and round cooker list
                                                        if (probability > 92.5) {
                                                            _RoundCookers.Add(cooker.Key.player_name, cooker.Key);
                                                            DebugWrite(cooker.Key.player_name + " added to round cooker list.", 2);
                                                            //Add to SURE
                                                            sure.Add(new KeyValuePair<AdKatsPlayer, String>(cooker.Key, probString));
                                                        }
                                                            //Otherwise add them to the round cooker list, and add to POSSIBLE list
                                                        else {
                                                            _RoundCookers.Add(cooker.Key.player_name, cooker.Key);
                                                            DebugWrite(cooker.Key.player_name + " added to round cooker list.", 2);
                                                            //Add to POSSIBLE
                                                            possible.Add(new KeyValuePair<AdKatsPlayer, String>(cooker.Key, probString));
                                                        }
                                                    }
                                                }
                                                //This method used for dealing with multiple kills at the same instant i.e twin/triple headshots
                                                if (sure.Count == 1 && possible.Count == 0) {
                                                    AdKatsPlayer player = sure[0].Key;
                                                    String probString = sure[0].Value;
                                                    //Create the ban record
                                                    var record = new AdKatsRecord {
                                                        record_source = AdKatsRecord.Sources.InternalAutomated,
                                                        server_id = _serverID,
                                                        command_type = _CommandKeyDictionary["player_punish"],
                                                        command_numeric = 0,
                                                        target_name = player.player_name,
                                                        target_player = player,
                                                        source_name = "AutoAdmin",
                                                        record_message = "Rules: Cooking Grenades [" + probString + "] [Victim " + killer.player_name + " Protected]"
                                                    };
                                                    //Process the record
                                                    QueueRecordForProcessing(record);
                                                    //adminSay("Punishing " + killer.player_name + " for " + record.record_message);
                                                    DebugWrite(record.target_player.player_name + " punished for " + record.record_message, 2);
                                                }
                                                else {
                                                    AdKatsPlayer player;
                                                    String probString;
                                                    foreach (var playerPair in sure) {
                                                        player = playerPair.Key;
                                                        probString = playerPair.Value;
                                                        //Create the report record
                                                        var record = new AdKatsRecord {
                                                            record_source = AdKatsRecord.Sources.InternalAutomated,
                                                            server_id = _serverID,
                                                            command_type = _CommandKeyDictionary["player_report"],
                                                            command_numeric = 0,
                                                            target_name = player.player_name,
                                                            target_player = player,
                                                            source_name = "AutoAdmin",
                                                            record_message = "Possible Grenade Cooker [" + probString + "] [Victim " + killer.player_name + " Protected]"
                                                        };
                                                        //Process the record
                                                        QueueRecordForProcessing(record);
                                                        DebugWrite(record.target_player.player_name + " reported for " + record.record_message, 2);
                                                    }
                                                    foreach (var playerPair in possible) {
                                                        player = playerPair.Key;
                                                        probString = playerPair.Value;
                                                        //Create the report record
                                                        var record = new AdKatsRecord {
                                                            record_source = AdKatsRecord.Sources.InternalAutomated,
                                                            server_id = _serverID,
                                                            command_type = _CommandKeyDictionary["player_report"],
                                                            command_numeric = 0,
                                                            target_name = player.player_name,
                                                            target_player = player,
                                                            source_name = "AutoAdmin",
                                                            record_message = "Possible Grenade Cooker [" + probString + "] [Victim " + killer.player_name + " Protected]"
                                                        };
                                                        //Process the record
                                                        QueueRecordForProcessing(record);
                                                        DebugWrite(record.target_player.player_name + " reported for " + record.record_message, 2);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e) {
                                HandleException(new AdKatsException("Error in grenade cook catcher.", e));
                            }
                        }
                    }
                }

                try {
                    if (_UseWeaponLimiter && !gKillHandled) {
                        //Check for restricted weapon
                        if (Regex.Match(kKillerVictimDetails.DamageType, @"(?:" + _WeaponLimiterString + ")", RegexOptions.IgnoreCase).Success) {
                            //Check for exception type
                            if (!Regex.Match(kKillerVictimDetails.DamageType, @"(?:" + _WeaponLimiterExceptionString + ")", RegexOptions.IgnoreCase).Success) {
                                //Check if suicide
                                if (kKillerVictimDetails.Killer.SoldierName != kKillerVictimDetails.Victim.SoldierName) {
                                    //Get player from the dictionary
                                    if (killer != null) {
                                        //Code to avoid spam
                                        if (killer.lastAction.AddSeconds(2) < DateTime.UtcNow) {
                                            killer.lastAction = DateTime.UtcNow;
                                            //Create the punish record
                                            var record = new AdKatsRecord {
                                                record_source = AdKatsRecord.Sources.InternalAutomated,
                                                server_id = _serverID,
                                                command_type = _CommandKeyDictionary["player_punish"],
                                                command_numeric = 0,
                                                target_name = killer.player_name,
                                                target_player = killer,
                                                source_name = "AutoAdmin"
                                            };
                                            const string removeWeapon = "Weapons/";
                                            const string removeGadgets = "Gadgets/";
                                            const string removePrefix = "U_";
                                            String weapon = kKillerVictimDetails.DamageType;
                                            Int32 index = weapon.IndexOf(removeWeapon, StringComparison.Ordinal);
                                            weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removeWeapon.Length));
                                            index = weapon.IndexOf(removeGadgets, StringComparison.Ordinal);
                                            weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removeGadgets.Length));
                                            index = weapon.IndexOf(removePrefix, StringComparison.Ordinal);
                                            weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removePrefix.Length));
                                            if (weapon == "RoadKill") {
                                                record.record_message = "Rules: Roadkilling with EOD or MAV";
                                            }
                                            else if (weapon == "Death") {
                                                if (_gameVersion == GameVersion.BF3) {
                                                    record.record_message = "Rules: Using Mortar";
                                                }
                                                else if (_gameVersion == GameVersion.BF4) {
                                                    record.record_message = "Rules: Using EOD Bot";
                                                }
                                            }
                                            else {
                                                record.record_message = "Rules: Using Explosives [" + weapon + "]";
                                            }
                                            PlayerYellMessage(victim.player_name, killer.player_name + " was punished for killing you with " + weapon);
                                            //Process the record
                                            QueueRecordForProcessing(record);
                                        }
                                        else {
                                            DebugWrite("Skipping additional auto-actions for multi-kill event.", 2);
                                        }
                                    }
                                    else {
                                        ConsoleError("Killer was null when processing kill");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error in no explosives auto-admin.", e));
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while processing player kill.", e));
            }
            DebugWrite("Exiting OnPlayerKilled", 7);
        }

        private void UploadWeaponCode(String weaponCode) {
            DebugWrite("uploadWeaponCode starting!", 7);

            //Make sure database connection active
            if (HandlePossibleDisconnect() || !_isTestingAuthorized) {
                return;
            }
            try {
                Boolean confirmed = _WeaponCodesTableConfirmed;
                if (!_WeaponCodesTableTested) {
                    _WeaponCodesTableTested = true;
                    _WeaponCodesTableConfirmed = ConfirmTable("tbl_weaponcodes");
                }
                if (!_WeaponCodesTableConfirmed) {
                    return;
                }
                if (!confirmed) {
                    ConsoleSuccess("Weapon code table found.");
                }
                //Check for length too great
                if (weaponCode.Length > 100) {
                    ConsoleError("Weapon name '" + weaponCode + "' too long!!!");
                    return;
                }

                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Set the insert command structure
                        command.CommandText = @"
                        INSERT INTO `tbl_weaponcodes` 
                        (
                            `weapon_code`
                        ) 
                        VALUES 
                        (  
                            '" + weaponCode + @"'
                        ) 
                        ON DUPLICATE KEY 
                        UPDATE 
                            `weapon_usage_count` = `weapon_usage_count` + 1";
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0) {
                            DebugWrite("Weapon pushed to database", 7);
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while uploading weapon to database.", e));
            }

            DebugWrite("uploadWeaponCode finished!", 7);
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) {
            DebugWrite("Entering OnPlayerSpawned", 7);
            try {
                if (_pluginEnabled) {
                    if (_CurrentRoundState == RoundState.Loaded) {
                        _CurrentRoundState = RoundState.Playing;
                    }
                    if (_CommandNameDictionary.Count > 0) {
                        //Handle TeamSwap notifications
                        String command = _CommandKeyDictionary["self_teamswap"].command_text;
                        AdKatsPlayer aPlayer;
                        if (_playerDictionary.TryGetValue(soldierName, out aPlayer)) {
                            aPlayer.lastSpawn = DateTime.UtcNow;
                            if (aPlayer.player_aa && !aPlayer.player_aa_told) {
                                String adminAssistantMessage = "You are now considered an Admin Assistant. ";
                                if (!_UseAAReportAutoHandler && !_EnableAdminAssistantPerk) {
                                    adminAssistantMessage += "Thank you for your consistent reporting.";
                                }
                                else {
                                    adminAssistantMessage += "Perks: ";
                                    if (_UseAAReportAutoHandler) {
                                        adminAssistantMessage += "AutoAdmin can handle some of your reports. ";
                                    }
                                    if (_EnableAdminAssistantPerk) {
                                        adminAssistantMessage += "You can use the @" + command + " command.";
                                    }
                                }
                                PlayerSayMessage(soldierName, adminAssistantMessage);
                                aPlayer.player_aa_told = true;
                            }
                            else {
                                Boolean informed;
                                if (_TeamswapRoundWhitelist.Count > 0 && _TeamswapRoundWhitelist.TryGetValue(soldierName, out informed)) {
                                    if (!informed) {
                                        PlayerTellMessage(soldierName, "By random selection you can use @" + command + " for this round. Type @" + command + " to move yourself between teams.");
                                        _TeamswapRoundWhitelist[soldierName] = true;
                                    }
                                }
                            }
                        }
                    }

                    //Handle Dev Notifications
                    if (soldierName == "ColColonCleaner" && !_ToldCol) {
                        PlayerTellMessage("ColColonCleaner", "CONGRATS! This server is running AdKats " + PluginVersion + "!");
                        _ToldCol = true;
                    }

                    if (_ActOnSpawnDictionary.Count > 0) {
                        if (_isTestingAuthorized)
                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                        lock (_ActOnSpawnDictionary) {
                            AdKatsRecord record;
                            if (_ActOnSpawnDictionary.TryGetValue(soldierName, out record)) {
                                //Remove it from the dic
                                _ActOnSpawnDictionary.Remove(soldierName);
                                //Wait 1.5 seconds to kill them again
                                Thread.Sleep(1500);
                                //Queue the action
                                QueueRecordForActionHandling(record);
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling player spawn.", e));
            }
            DebugWrite("Exiting OnPlayerSpawned", 7);
        }

        //DONE
        public override void OnPlayerLeft(CPlayerInfo playerInfo) {
            DebugWrite("Entering OnPlayerLeft", 7);
            try {
                QueuePlayerForRemoval(playerInfo);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling player left.", e));
            }
            DebugWrite("Exiting OnPlayerLeft", 7);
        }

        private void QueueSettingImport(Int32 serverID) {
            DebugWrite("Entering queueSettingImport", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue server ID for setting import", 6);
                    _settingImportID = serverID;
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while preparing to import settings.", e));
            }
            DebugWrite("Exiting queueSettingImport", 7);
        }

        private void QueueSettingForUpload(CPluginVariable setting) {
            DebugWrite("Entering queueSettingForUpload", 7);
            if (!_settingsFetched) {
                return;
            }
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue setting " + setting.Name + " for upload", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_SettingUploadQueue) {
                        _SettingUploadQueue.Enqueue(setting);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing setting for upload.", e));
            }
            DebugWrite("Exiting queueSettingForUpload", 7);
        }

        private void QueueCommandForUpload(AdKatsCommand command) {
            DebugWrite("Entering queueCommandForUpload", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue command " + command.command_key + " for upload", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_CommandUploadQueue) {
                        _CommandUploadQueue.Enqueue(command);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing command for upload.", e));
            }
            DebugWrite("Exiting queueCommandForUpload", 7);
        }

        private void QueueRoleForUpload(AdKatsRole aRole) {
            DebugWrite("Entering queueRoleForUpload", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue role " + aRole.role_key + " for upload", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_RoleUploadQueue) {
                        _RoleUploadQueue.Enqueue(aRole);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing role for upload.", e));
            }
            DebugWrite("Exiting queueRoleForUpload", 7);
        }

        private void QueueRoleForRemoval(AdKatsRole aRole) {
            DebugWrite("Entering queueRoleForRemoval", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue role " + aRole.role_key + " for removal", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_RoleRemovalQueue) {
                        _RoleRemovalQueue.Enqueue(aRole);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing role for removal.", e));
            }
            DebugWrite("Exiting queueRoleForRemoval", 7);
        }

        private void QueuePlayerForBanCheck(AdKatsPlayer player) {
            DebugWrite("Entering queuePlayerForBanCheck", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue player for ban check", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_BanEnforcerCheckingQueue) {
                        _BanEnforcerCheckingQueue.Enqueue(player);
                        DebugWrite("Player queued for checking", 6);
                        _BanEnforcerWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing player for ban check.", e));
            }
            DebugWrite("Exiting queuePlayerForBanCheck", 7);
        }

        private void QueueBanForProcessing(AdKatsBan aBan) {
            DebugWrite("Entering queueBanForProcessing", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue ban for processing", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_BanEnforcerProcessingQueue) {
                        _BanEnforcerProcessingQueue.Enqueue(aBan);
                        DebugWrite("Ban queued for processing", 6);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing ban for processing.", e));
            }
            DebugWrite("Exiting queueBanForProcessing", 7);
        }

        private void BanEnforcerThreadLoop() {
            try {
                DebugWrite("BANENF: Starting Ban Enforcer Thread", 1);
                Thread.CurrentThread.Name = "BanEnforcer";
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("BANENF: Entering Ban Enforcer Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("BANENF: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Get all unchecked players
                        Queue<AdKatsPlayer> playerCheckingQueue;
                        if (_BanEnforcerCheckingQueue.Count > 0 && _UseBanEnforcer) {
                            DebugWrite("BANENF: Preparing to lock banEnforcerMutex to retrive new players", 6);
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_BanEnforcerCheckingQueue) {
                                DebugWrite("BANENF: Inbound players found. Grabbing.", 5);
                                //Grab all players in the queue
                                playerCheckingQueue = new Queue<AdKatsPlayer>(_BanEnforcerCheckingQueue.ToArray());
                                //Clear the queue for next run
                                _BanEnforcerCheckingQueue.Clear();
                                if (_databaseConnectionCriticalState) {
                                    continue;
                                }
                            }
                        }
                        else {
                            DebugWrite("BANENF: No inbound ban checks. Waiting for Input.", 4);
                            //Wait for input
                            _BanEnforcerWaitHandle.Reset();
                            _BanEnforcerWaitHandle.WaitOne(Timeout.Infinite);
                            continue;
                        }

                        //Get all checks in order that they came in
                        while (playerCheckingQueue.Count > 0) {
                            //Grab first/next player
                            AdKatsPlayer aPlayer = playerCheckingQueue.Dequeue();
                            DebugWrite("BANENF: begin reading player", 5);
                            if (_playerDictionary.ContainsKey(aPlayer.player_name)) {
                                List<AdKatsBan> aBanList = FetchPlayerBans(aPlayer);
                                if (aBanList.Count > 0) {
                                    foreach (AdKatsBan aBan in aBanList) {
                                        DebugWrite("BANENF: BAN ENFORCED on " + aPlayer.player_name, 3);

                                        //Create the new record
                                        var aRecord = new AdKatsRecord {
                                            record_source = AdKatsRecord.Sources.InternalAutomated,
                                            source_name = "BanEnforcer",
                                            isIRO = false,
                                            server_id = _serverID,
                                            target_name = aPlayer.player_name,
                                            target_player = aPlayer,
                                            command_type = _CommandKeyDictionary["banenforcer_enforce"],
                                            command_numeric = (int) aBan.ban_id,
                                            record_message = aBan.ban_record.record_message + ((aBan.ban_record.target_player.player_id != aPlayer.player_id) ? (" [LINKED ACCOUNT " + aBan.ban_record.target_player.player_id + "]") : (""))
                                        };
                                        //Queue record for upload
                                        QueueRecordForProcessing(aRecord);
                                        //Ensure the ban record has correct player information
                                        aBan.ban_record.target_player = aPlayer;
                                        //Enforce the ban
                                        EnforceBan(aBan, true);
                                    }
                                }
                                else {
                                    DebugWrite("BANENF: No ban found for player", 5);
                                    //Only call a hack check if the player does not already have a ban
                                    if (_UseHackerChecker) {
                                        QueuePlayerForHackerCheck(aPlayer);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("ban enforcer thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in ban enforcer thread. Skipping current loop.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                }
                DebugWrite("BANENF: Ending Ban Enforcer Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in ban enforcer thread.", e));
            }
        }

        public override void OnBanAdded(CBanInfo ban) {
            if (!_pluginEnabled || !_UseBanEnforcer)
                return;
            //DebugWrite("OnBanAdded fired", 6);
            ExecuteCommand("procon.protected.send", "banList.list");
        }

        public override void OnBanList(List<CBanInfo> banList) {
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                //Return if small duration (0.5 seconds) since last ban list, or if there is already a ban list going on
                if ((DateTime.UtcNow - _LastSuccessfulBanList) < TimeSpan.FromSeconds(0.5)) {
                    DebugWrite("Banlist being called quickly.", 4);
                    return;
                }
                if (_BansQueuing) {
                    ConsoleError("Attempted banlist call rejected. Processing already in progress.");
                    return;
                }
                DateTime startTime = DateTime.UtcNow;
                _LastSuccessfulBanList = startTime;
                if (!_pluginEnabled)
                    return;
                DebugWrite("OnBanList fired", 5);
                if (_UseBanEnforcer) {
                    if (banList.Count > 0) {
                        DebugWrite("Bans found", 3);
                        if (_isTestingAuthorized)
                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                        lock (_CBanProcessingQueue) {
                            //Only allow queueing of new bans if the processing queue is currently empty
                            if (_CBanProcessingQueue.Count == 0) {
                                foreach (CBanInfo cBan in banList) {
                                    DebugWrite("Queuing Ban.", 7);
                                    _CBanProcessingQueue.Enqueue(cBan);
                                    _BansQueuing = true;
                                    if (DateTime.UtcNow - startTime > TimeSpan.FromSeconds(50)) {
                                        HandleException(new AdKatsException("OnBanList took longer than 50 seconds, exiting so procon doesn't panic."));
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
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while listing procon bans.", e));
                _BansQueuing = false;
            }
        }

        public override void OnBanListClear() {
            DebugWrite("Ban list cleared", 5);
        }

        public override void OnBanListSave() {
            DebugWrite("Ban list saved", 5);
        }

        public override void OnBanListLoad() {
            DebugWrite("Ban list loaded", 5);
        }

        private void QueuePlayerForHackerCheck(AdKatsPlayer player) {
            DebugWrite("Entering queuePlayerForHackerCheck", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue player for hacker check", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_HackerCheckerQueue) {
                        _HackerCheckerQueue.Enqueue(player);
                        DebugWrite("Player queued for checking", 6);
                        _HackerCheckerWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing player for hacker check.", e));
            }
            DebugWrite("Exiting queuePlayerForHackerCheck", 7);
        }

        public Boolean PlayerProtected(AdKatsPlayer aPlayer) {
            //Pull players from special player cache
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (_specialPlayerGroupCache) {
                List<AdKatsSpecialPlayer> protectedList;
                if (_specialPlayerGroupCache.TryGetValue("whitelist_hackerchecker", out protectedList)) {
                    foreach (AdKatsSpecialPlayer asPlayer in protectedList) {
                        if (asPlayer.player_object != null && asPlayer.player_object.player_id == aPlayer.player_id) {
                            DebugWrite(aPlayer.player_name + " protected from hacker checker by database ID.", 2);
                            return true;
                        }
                        if (!String.IsNullOrEmpty(asPlayer.player_identifier)) {
                            if (aPlayer.player_name == asPlayer.player_identifier) {
                                DebugWrite(aPlayer.player_name + " protected from hacker checker by NAME.", 2);
                                return true;
                            }
                            if (aPlayer.player_guid == asPlayer.player_identifier) {
                                DebugWrite(aPlayer.player_name + " protected from hacker checker by GUID.", 2);
                                return true;
                            }
                            if (aPlayer.player_ip == asPlayer.player_identifier) {
                                DebugWrite(aPlayer.player_name + " protected from hacker checker by IP.", 2);
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public void HackerCheckerThreadLoop() {
            Double checkedPlayers = 0;
            Double playersWithStats = 0;
            try {
                DebugWrite("HCKCHK: Starting Hacker Checker Thread", 1);
                Thread.CurrentThread.Name = "HackerChecker";

                var playerCheckingQueue = new Queue<AdKatsPlayer>();
                var repeatCheckingQueue = new Queue<AdKatsPlayer>();
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("HCKCHK: Entering Hacker Checker Thread Loop", 7);
                        if (!_pluginEnabled) {
                            playerCheckingQueue.Clear();
                            repeatCheckingQueue.Clear();
                            DebugWrite("HCKCHK: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        try {
                            //Get all unchecked players
                            if (_HackerCheckerQueue.Count > 0) {
                                DebugWrite("HCKCHK: Preparing to lock hackerCheckerMutex to retrive new players", 6);
                                if (_isTestingAuthorized)
                                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                lock (_HackerCheckerQueue) {
                                    DebugWrite("HCKCHK: Inbound players found. Grabbing.", 5);
                                    //Grab all players in the queue
                                    playerCheckingQueue = new Queue<AdKatsPlayer>(_HackerCheckerQueue.ToArray());
                                    //Clear the queue for next run  
                                    _HackerCheckerQueue.Clear();
                                }
                            }
                            else {
                                DebugWrite("HCKCHK: No inbound hacker checks. Waiting 10 seconds or for input.", 4);
                                //Wait for input
                                _HackerCheckerWaitHandle.Reset();
                                //Either loop when handle is set, or after 3 minutes
                                _HackerCheckerWaitHandle.WaitOne(180000 / ((repeatCheckingQueue.Count > 0) ? (repeatCheckingQueue.Count) : (1)));
                            }
                        }
                        catch (Exception e) {
                            HandleException(new AdKatsException("Error while fetching new players to check.", e));
                        }

                        //Current player being checked
                        AdKatsPlayer aPlayer = null;
                        try {
                            if (!_UseHackerChecker) {
                                repeatCheckingQueue.Clear();
                            }
                            //Check one player from the repeat checking queue
                            if (repeatCheckingQueue.Count > 0) {
                                //Only keep players still in the server in the repeat checking list
                                Boolean stillInServer = true;
                                do {
                                    if (!_pluginEnabled) {
                                        break;
                                    }
                                    aPlayer = repeatCheckingQueue.Dequeue();
                                    if (!_playerDictionary.ContainsKey(aPlayer.player_name)) {
                                        stillInServer = false;
                                    }
                                } while (!stillInServer && repeatCheckingQueue.Count > 0);
                                if (aPlayer != null) {
                                    //Fetch their stats from appropriate source
                                    FetchPlayerStats(aPlayer);
                                    if (aPlayer.stats != null && aPlayer.stats.StatsException == null) {
                                        playersWithStats++;
                                        ConsoleSuccess(aPlayer.player_name + " now has stats. Checking.");
                                        if (!PlayerProtected(aPlayer)) {
                                            RunStatSiteHackCheck(aPlayer, false);
                                        }
                                        DebugWrite("Players with " + _gameVersion + "Stats: " + String.Format("{0:0.00}", (playersWithStats / checkedPlayers) * 100) + "%", 3);
                                    }
                                    else {
                                        aPlayer.stats = null;
                                        //If they still dont have stats, add them back to the queue
                                        repeatCheckingQueue.Enqueue(aPlayer);
                                    }
                                }
                            }
                        }
                        catch (Exception e) {
                            HandleException(new AdKatsException("Error while in repeat checking queue handler", e));
                        }

                        //Get all checks in order that they came in
                        while (playerCheckingQueue.Count > 0) {
                            if (!_pluginEnabled) {
                                break;
                            }
                            //Grab first/next player
                            aPlayer = playerCheckingQueue.Dequeue();
                            if (aPlayer != null) {
                                DebugWrite("HCKCHK: begin reading player", 4);

                                if (!PlayerProtected(aPlayer)) {
                                    FetchPlayerStats(aPlayer);
                                    checkedPlayers++;
                                    if (aPlayer.stats != null && aPlayer.stats.StatsException == null) {
                                        playersWithStats++;
                                        if (_UseHackerChecker) {
                                            RunStatSiteHackCheck(aPlayer, false);
                                        }
                                        else {
                                            DebugWrite("Player skipped after disabling hacker checker.", 2);
                                        }
                                    }
                                    else {
                                        //ConsoleError(aPlayer.player_name + " doesn't have stats.");
                                        repeatCheckingQueue.Enqueue(aPlayer);
                                    }
                                    DebugWrite("Players with " + _gameVersion + "Stats: " + String.Format("{0:0.00}", (playersWithStats / checkedPlayers) * 100) + "%", 3);
                                }
                            }
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("Hacker Checker thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in Hacker Checker thread. Skipping current loop.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                }
                DebugWrite("HCKCHK: Ending Hacker Checker Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in Hacker Checker thread.", e));
            }
        }

        private void RunStatSiteHackCheck(AdKatsPlayer aPlayer, Boolean debug) {
            DebugWrite("HackerChecker running on " + aPlayer.player_name, 5);
            Boolean acted = false;
            if (_UseDpsChecker) {
                DebugWrite("Preparing to DPS check " + aPlayer.player_name, 5);
                acted = DamageHackCheck(aPlayer, debug);
            }
            if (_UseHskChecker && !acted) {
                DebugWrite("Preparing to HSK check " + aPlayer.player_name, 5);
                acted = AimbotHackCheck(aPlayer, debug);
            }
            if (_UseKpmChecker && !acted) {
                DebugWrite("Preparing to KPM check " + aPlayer.player_name, 5);
                acted = KPMHackCheck(aPlayer, debug);
            }
            if (!acted && debug) {
                ConsoleSuccess(aPlayer.player_name + " is clean.");
            }
        }

        private Boolean DamageHackCheck(AdKatsPlayer aPlayer, Boolean debugMode) {
            Boolean acted = false;
            try {
                if (aPlayer == null || aPlayer.stats == null || aPlayer.stats.WeaponStats == null) {
                    return false;
                }
                List<String> allowedCategories;
                switch (_gameVersion) {
                    case GameVersion.BF3:
                        allowedCategories = new List<string> {
                            "Sub machine guns",
                            "Assault rifles",
                            "Carbines",
                            "Machine guns",
                            "Handheld weapons"
                        };
                        break;
                    case GameVersion.BF4:
                        allowedCategories = new List<string> {
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
                List<AdKatsWeaponStats> topWeapons = aPlayer.stats.WeaponStats.Values.ToList();
                topWeapons.Sort(delegate(AdKatsWeaponStats a1, AdKatsWeaponStats a2) {
                    if (Math.Abs(a1.Kills - a2.Kills) < 0.001) {
                        return 0;
                    }
                    return (a1.Kills < a2.Kills) ? (1) : (-1);
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
                    if (allowedCategories.Contains(weaponStat.Category)) {
                        StatLibraryWeapon weapon;
                        if (_StatLibrary.Weapons.TryGetValue(weaponStat.ID, out weapon)) {
                            //Only handle weapons that do < 50 max dps
                            if (weapon.damage_max < 50) {
                                //Only take weapons with more than 50 kills
                                if (weaponStat.Kills > 50) {
                                    //Check for damage hack
                                    if (weaponStat.DPS > weapon.damage_max) {
                                        //Get the percentage over normal
                                        Double percDiff = (weaponStat.DPS - weapon.damage_max) / weaponStat.DPS;
                                        if (percDiff > (_DpsTriggerLevel / 100)) {
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
                            ConsoleWarn("Could not find " + weaponStat.ID + " in " + _gameVersion + " library of " + _StatLibrary.Weapons.Count + " weapons.");
                        }
                    }
                }
                if (actedWeapon != null) {
                    acted = true;
                    String formattedName = actedWeapon.ID.Replace("-", "").Replace(" ", "").ToUpper();
                    ConsoleWarn(aPlayer.player_name + " auto-banned for damage mod. [" + formattedName + "-" + (int) actedWeapon.DPS + "-" + (int) actedWeapon.Kills + "-" + (int) actedWeapon.Headshots + "]");
                    if (!debugMode) {
                        //Create the ban record
                        var record = new AdKatsRecord {
                            record_source = AdKatsRecord.Sources.InternalAutomated,
                            server_id = _serverID,
                            command_type = _CommandKeyDictionary["player_ban_perm"],
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "AutoAdmin",
                            record_message = _HackerCheckerDPSBanMessage + " [" + formattedName + "-" + (int) actedWeapon.DPS + "-" + (int) actedWeapon.Kills + "-" + (int) actedWeapon.Headshots + "]"
                        };
                        //Process the record
                        QueueRecordForProcessing(record);
                        //AdminSayMessage(player.player_name + " auto-banned for damage mod. [" + actedWeapon.id + "-" + (int) actedWeapon.dps + "-" + (int) actedWeapon.kills + "-" + (int) actedWeapon.headshots + "]");
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error running DPS hack check", e));
            }
            return acted;
        }

        private Boolean AimbotHackCheck(AdKatsPlayer aPlayer, Boolean debugMode) {
            Boolean acted = false;
            try {
                if (aPlayer == null || aPlayer.stats == null || aPlayer.stats.WeaponStats == null) {
                    return false;
                }
                List<String> allowedCategories;
                switch (_gameVersion) {
                    case GameVersion.BF3:
                        allowedCategories = new List<string> {
                            "Sub machine guns",
                            "Assault rifles",
                            "Carbines",
                            "Machine guns"
                        };
                        break;
                    case GameVersion.BF4:
                        allowedCategories = new List<string> {
                            "PDW",
                            "ASSAULT RIFLE",
                            "CARBINE",
                            "LMG"
                        };
                        break;
                    default:
                        return false;
                }
                List<AdKatsWeaponStats> topWeapons = aPlayer.stats.WeaponStats.Values.ToList();
                topWeapons.Sort(delegate(AdKatsWeaponStats a1, AdKatsWeaponStats a2) {
                    if (Math.Abs(a1.Kills - a2.Kills) < 0.001) {
                        return 0;
                    }
                    return (a1.Kills < a2.Kills) ? (1) : (-1);
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
                    if (allowedCategories.Contains(weaponStat.Category)) {
                        //Only take weapons with more than 100 kills
                        if (weaponStat.Kills > 100) {
                            //Check for aimbot hack
                            DebugWrite("Checking " + weaponStat.ID + " HSKR (" + weaponStat.HSKR + " >? " + (_HskTriggerLevel / 100) + ")", 6);
                            if (weaponStat.HSKR > (_HskTriggerLevel / 100)) {
                                if (weaponStat.HSKR > actedHskr) {
                                    actedHskr = weaponStat.HSKR;
                                    actedWeapon = weaponStat;
                                }
                            }
                        }
                    }
                }
                if (actedWeapon != null) {
                    acted = true;
                    String formattedName = actedWeapon.ID.Replace("-", "").Replace(" ", "").ToUpper();
                    ConsoleWarn(aPlayer.player_name + " auto-banned for aimbot. [" + formattedName + "-" + (int) (actedWeapon.HSKR * 100) + "-" + (int) actedWeapon.Kills + "-" + (int) actedWeapon.Headshots + "]");
                    if (!debugMode) {
                        //Create the ban record
                        var record = new AdKatsRecord {
                            record_source = AdKatsRecord.Sources.InternalAutomated,
                            server_id = _serverID,
                            command_type = _CommandKeyDictionary["player_ban_perm"],
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "AutoAdmin",
                            record_message = _HackerCheckerHSKBanMessage + " [" + formattedName + "-" + (int) (actedWeapon.HSKR * 100) + "-" + (int) actedWeapon.Kills + "-" + (int) actedWeapon.Headshots + "]"
                        };
                        //Process the record
                        QueueRecordForProcessing(record);
                        //AdminSayMessage(player.player_name + " auto-banned for aimbot. [" + actedWeapon.id + "-" + (int) actedWeapon.hskr + "-" + (int) actedWeapon.kills + "-" + (int) actedWeapon.headshots + "]");
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error running HSK hack check.", e));
            }
            return acted;
        }

        private Boolean KPMHackCheck(AdKatsPlayer aPlayer, Boolean debugMode) {
            Boolean acted = false;
            try {
                if (aPlayer == null || aPlayer.stats == null || aPlayer.stats.WeaponStats == null) {
                    return false;
                }
                List<String> allowedCategories;
                switch (_gameVersion) {
                    case GameVersion.BF3:
                        allowedCategories = new List<string> {
                            "Sub machine guns",
                            "Assault rifles",
                            "Carbines",
                            "Machine guns",
                            "Sniper rifles",
                            "Shotguns"
                        };
                        break;
                    case GameVersion.BF4:
                        allowedCategories = new List<string> {
                            "PDW",
                            "ASSAULT RIFLE",
                            "CARBINE",
                            "LMG",
                            "SNIPER RIFLE",
                            "DMR",
                            "SHOTGUN"
                        };
                        break;
                    default:
                        return false;
                }
                List<AdKatsWeaponStats> topWeapons = aPlayer.stats.WeaponStats.Values.ToList();
                topWeapons.Sort(delegate(AdKatsWeaponStats a1, AdKatsWeaponStats a2) {
                    if (a1.Kills == a2.Kills) {
                        return 0;
                    }
                    return (a1.Kills < a2.Kills) ? (1) : (-1);
                });

                AdKatsWeaponStats actedWeapon = null;
                Double actedKpm = -1;
                Int32 index = 0;
                foreach (AdKatsWeaponStats weaponStat in topWeapons) {
                    //Break after 15th top weapon
                    if (index++ > 15) {
                        break;
                    }
                    //Only count certain weapon categories
                    if (allowedCategories.Contains(weaponStat.Category)) {
                        //Only take weapons with more than 100 kills
                        if (weaponStat.Kills > 100) {
                            //Check for KPM limit
                            DebugWrite("Checking " + weaponStat.ID + " KPM (" + String.Format("{0:0.00}", weaponStat.KPM) + " >? " + (_KpmTriggerLevel) + ")", 6);
                            if (weaponStat.KPM > (_KpmTriggerLevel)) {
                                if (weaponStat.KPM > actedKpm) {
                                    actedKpm = weaponStat.KPM;
                                    actedWeapon = weaponStat;
                                }
                            }
                        }
                    }
                }
                if (actedWeapon != null) {
                    acted = true;
                    String formattedName = actedWeapon.ID.Replace("-", "").Replace(" ", "").ToUpper();
                    ConsoleWarn(aPlayer.player_name + ((debugMode) ? (" debug") : (" auto")) + "-banned for KPM. [" + formattedName + "-" + String.Format("{0:0.00}", actedWeapon.KPM) + "-" + (int) actedWeapon.Kills + "-" + (int) actedWeapon.Headshots + "]");
                    if (!debugMode) {
                        //Create the ban record
                        var record = new AdKatsRecord {
                            record_source = AdKatsRecord.Sources.InternalAutomated,
                            server_id = _serverID,
                            command_type = _CommandKeyDictionary["player_ban_perm"],
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "AutoAdmin",
                            record_message = _HackerCheckerKPMBanMessage + " [" + formattedName + "-" + String.Format("{0:0.00}", actedWeapon.KPM) + "-" + (int) actedWeapon.Kills + "-" + (int) actedWeapon.Headshots + "]"
                        };
                        //Process the record
                        QueueRecordForProcessing(record);
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error running KPM hack check.", e));
            }
            return acted;
        }

        //all messaging is redirected to global chat for analysis
        public override void OnGlobalChat(String speaker, String message) {
            //uploadChatLog(speaker, "Global", message);
            HandleChat(speaker, message);
        }

        public override void OnTeamChat(String speaker, String message, Int32 teamId) {
            //uploadChatLog(speaker, "Team", message);
            HandleChat(speaker, message);
        }

        public override void OnSquadChat(String speaker, String message, Int32 teamId, Int32 squadId) {
            //uploadChatLog(speaker, "Squad", message);
            HandleChat(speaker, message);
        }

        private void HandleChat(String speaker, String message) {
            DebugWrite("Entering handleChat", 7);
            try {
                if (_pluginEnabled) {
                    //Performance testing area
                    if (speaker == _DebugSoldierName) {
                        _CommandStartTime = DateTime.UtcNow;
                    }
                    //If message contains comorose just return and ignore
                    if (message.Contains("ComoRose:")) {
                        return;
                    }
                    QueueMessageForParsing(speaker, message.Trim());
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while processing inbound chat messages.", e));
            }
            DebugWrite("Exiting handleChat", 7);
        }

        public void SendMessageToSource(AdKatsRecord record, String message) {
            DebugWrite("Entering sendMessageToSource", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    ConsoleError("message null or empty in sendMessageToSource");
                    return;
                }
                switch (record.record_source) {
                    case AdKatsRecord.Sources.InGame:
                        PlayerSayMessage(record.source_name, message);
                        break;
                    case AdKatsRecord.Sources.ServerCommand:
                        ProconChatWrite(message);
                        break;
                    case AdKatsRecord.Sources.Settings:
                        ConsoleWrite(message);
                        break;
                    case AdKatsRecord.Sources.Database:
                        //Do nothing, no way to communicate to source when database
                        break;
                    case AdKatsRecord.Sources.InternalAutomated:
                        //Do nothing, no source to communicate with
                        break;
                    case AdKatsRecord.Sources.ExternalPlugin:
                        record.debugMessages.Add(message);
                        break;
                    case AdKatsRecord.Sources.HTTP:
                        record.debugMessages.Add(message);
                        break;
                    default:
                        ConsoleWarn("Command source not set, or not recognized.");
                        break;
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while sending message to record source.", e);
                HandleException(record.record_exception);
            }
            DebugWrite("Exiting sendMessageToSource", 7);
        }

        public void AdminSayMessage(String message) {
            DebugWrite("Entering adminSay", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    ConsoleError("message null in adminSay");
                    return;
                }
                ProconChatWrite("Say > All > " + message);
                ExecuteCommand("procon.protected.send", "admin.say", message, "all");
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while sending admin say.", e));
            }
            DebugWrite("Exiting adminSay", 7);
        }

        public Boolean OnlineAdminSayMessage(String message) {
            Boolean adminsTold = false;
            foreach (AdKatsPlayer player in FetchOnlineAdminSoldiers()) {
                adminsTold = true;
                PlayerSayMessage(player.player_name, message);
            }
            if (!adminsTold) {
                ProconChatWrite(ColorMessageRed(BoldMessage(message)));
            }
            return adminsTold;
        }

        public void PlayerSayMessage(String target, String message) {
            DebugWrite("Entering playerSayMessage", 7);
            try {
                if (String.IsNullOrEmpty(target) || String.IsNullOrEmpty(message)) {
                    ConsoleError("target or message null in playerSayMessage");
                    return;
                }
                ProconChatWrite("Say > " + target + " > " + message);
                ExecuteCommand("procon.protected.send", "admin.say", message, "player", target);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while sending message to player.", e));
            }
            DebugWrite("Exiting playerSayMessage", 7);
        }

        public void AdminYellMessage(String message) {
            DebugWrite("Entering adminYell", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    ConsoleError("message null in adminYell");
                    return;
                }
                ProconChatWrite("Yell[" + _YellDuration + "] > All > " + message);
                ExecuteCommand("procon.protected.send", "admin.yell", message.ToUpper(), _YellDuration + "", "all");
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            DebugWrite("Exiting adminYell", 7);
        }

        public void PlayerYellMessage(String target, String message) {
            DebugWrite("Entering adminYell", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    ConsoleError("message null in adminYell");
                    return;
                }
                ProconChatWrite("Yell[" + _YellDuration + "] > " + target + " > " + message);
                ExecuteCommand("procon.protected.send", "admin.yell", message.ToUpper(), _YellDuration + "", "player", target);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            DebugWrite("Exiting adminYell", 7);
        }

        public void AdminTellMessage(String message) {
            AdminSayMessage(message);
            AdminYellMessage(message);
        }

        public void PlayerTellMessage(String target, String message) {
            PlayerSayMessage(target, message);
            PlayerYellMessage(target, message);
        }

        private void QueueMessageForParsing(String speaker, String message) {
            DebugWrite("Entering queueMessageForParsing", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue message for parsing", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_UnparsedMessageQueue) {
                        _UnparsedMessageQueue.Enqueue(new KeyValuePair<String, String>(speaker, message));
                        DebugWrite("Message queued for parsing.", 6);
                        _MessageParsingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing chat message for parsing.", e));
            }
            DebugWrite("Exiting queueMessageForParsing", 7);
        }

        private void QueueCommandForParsing(String speaker, String command) {
            DebugWrite("Entering queueCommandForParsing", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue command for parsing", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_UnparsedCommandQueue) {
                        _UnparsedCommandQueue.Enqueue(new KeyValuePair<String, String>(speaker, command));
                        DebugWrite("Command sent to unparsed commands.", 6);
                        _CommandParsingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing command for parsing.", e));
            }
            DebugWrite("Exiting queueCommandForParsing", 7);
        }

        private void MessagingThreadLoop() {
            try {
                DebugWrite("MESSAGE: Starting Messaging Thread", 1);
                Thread.CurrentThread.Name = "messaging";
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("MESSAGE: Entering Messaging Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("MESSAGE: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Get all unparsed inbound messages
                        Queue<KeyValuePair<String, String>> inboundMessages;
                        if (_UnparsedMessageQueue.Count > 0) {
                            DebugWrite("MESSAGE: Preparing to lock messaging to retrive new messages", 7);
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_UnparsedMessageQueue) {
                                DebugWrite("MESSAGE: Inbound messages found. Grabbing.", 6);
                                //Grab all messages in the queue
                                inboundMessages = new Queue<KeyValuePair<String, String>>(_UnparsedMessageQueue.ToArray());
                                //Clear the queue for next run
                                _UnparsedMessageQueue.Clear();
                            }
                        }
                        else {
                            DebugWrite("MESSAGE: No inbound messages. Waiting for Input.", 4);
                            //Wait for input
                            _MessageParsingWaitHandle.Reset();
                            _MessageParsingWaitHandle.WaitOne(Timeout.Infinite);
                            continue;
                        }

                        //Loop through all messages in order that they came in
                        while (inboundMessages.Count > 0) {
                            DebugWrite("MESSAGE: begin reading message", 6);
                            //Dequeue the first/next message
                            KeyValuePair<String, String> messagePair = inboundMessages.Dequeue();
                            String speaker = messagePair.Key;
                            String message = messagePair.Value;

                            //check for player mute case
                            //ignore if it's a server call
                            if (speaker != "Server") {
                                if (_isTestingAuthorized)
                                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                lock (_RoundMutedPlayers) {
                                    //Check if the player is muted
                                    DebugWrite("MESSAGE: Checking for mute case.", 7);
                                    if (_RoundMutedPlayers.ContainsKey(speaker)) {
                                        DebugWrite("MESSAGE: Player is muted. Acting.", 7);
                                        //Increment the muted chat count
                                        _RoundMutedPlayers[speaker] = _RoundMutedPlayers[speaker] + 1;
                                        //Create record
                                        var record = new AdKatsRecord();
                                        record.record_source = AdKatsRecord.Sources.InternalAutomated;
                                        record.server_id = _serverID;
                                        record.source_name = "PlayerMuteSystem";
                                        _playerDictionary.TryGetValue(speaker, out record.target_player);
                                        record.target_name = speaker;
                                        if (_RoundMutedPlayers[speaker] > _MutedPlayerChances) {
                                            record.record_message = _MutedPlayerKickMessage;
                                            record.command_type = _CommandKeyDictionary["player_kick"];
                                            record.command_action = _CommandKeyDictionary["player_kick"];
                                        }
                                        else {
                                            record.record_message = _MutedPlayerKillMessage;
                                            record.command_type = _CommandKeyDictionary["player_kill"];
                                            record.command_action = _CommandKeyDictionary["player_kill"];
                                        }

                                        QueueRecordForProcessing(record);
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
                                DebugWrite("MESSAGE: Message is regular chat. Ignoring.", 7);
                                continue;
                            }
                            QueueCommandForParsing(speaker, message.Trim());
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("Messaging thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in Messaging thread. Skipping current loop.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                }
                DebugWrite("MESSAGE: Ending Messaging Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in messaging thread.", e));
            }
        }

        private void QueuePlayerForForceMove(CPlayerInfo player) {
            DebugWrite("Entering queuePlayerForForceMove", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to queue player for TeamSwap ", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_TeamswapForceMoveQueue) {
                        _TeamswapForceMoveQueue.Enqueue(player);
                        _TeamswapWaitHandle.Set();
                        DebugWrite("Player queued for TeamSwap", 6);
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing player for force-move.", e));
            }
            DebugWrite("Exiting queuePlayerForForceMove", 7);
        }

        private void QueuePlayerForMove(CPlayerInfo player) {
            DebugWrite("Entering queuePlayerForMove", 7);
            try {
                if (_pluginEnabled) {
                    DebugWrite("Preparing to add player to 'on-death' move dictionary.", 6);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_TeamswapOnDeathCheckingQueue) {
                        if (!_TeamswapOnDeathMoveDic.ContainsKey(player.SoldierName)) {
                            _TeamswapOnDeathMoveDic.Add(player.SoldierName, player);
                            _TeamswapWaitHandle.Set();
                            DebugWrite("Player added to 'on-death' move dictionary.", 6);
                        }
                        else {
                            DebugWrite("Player already in 'on-death' move dictionary.", 6);
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing player for move.", e));
            }
            DebugWrite("Exiting queuePlayerForMove", 7);
        }

        //runs through both team swap queues and performs the swapping
        public void TeamswapThreadLoop() {
            //assume the max player count per team is 32 if no server info has been provided
            Int32 maxTeamPlayerCount = 32;
            try {
                DebugWrite("TSWAP: Starting TeamSwap Thread", 1);
                Thread.CurrentThread.Name = "TeamSwap";
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("TSWAP: Entering TeamSwap Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("TSWAP: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        AdKatsTeam team1;
                        AdKatsTeam team2;
                        if (_isTestingAuthorized)
                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                        lock (_teamDictionary) {
                            if (!_teamDictionary.TryGetValue(1, out team1)) {
                                DebugWrite("Team 1 description was not found. Unable to continue.", 1);
                                Thread.Sleep(5000);
                                continue;
                            }
                            if (!_teamDictionary.TryGetValue(2, out team2)) {
                                DebugWrite("Team 2 description was not found. Unable to continue.", 1);
                                Thread.Sleep(5000);
                                continue;
                            }
                        }

                        //Refresh Max Player Count, needed for responsive server size
                        if (_serverInfo != null && _serverInfo.MaxPlayerCount != maxTeamPlayerCount) {
                            maxTeamPlayerCount = _serverInfo.MaxPlayerCount / 2;
                        }

                        //Get players who died that need moving
                        if ((_TeamswapOnDeathMoveDic.Count > 0 && _TeamswapOnDeathCheckingQueue.Count > 0) || _TeamswapForceMoveQueue.Count > 0) {
                            DebugWrite("TSWAP: Preparing to lock TeamSwap queues", 4);

                            //Call List Players
                            _PlayerListUpdateWaitHandle.Reset();
                            ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
                            //Wait for listPlayers to finish, max 5 seconds
                            if (!_PlayerListUpdateWaitHandle.WaitOne(TimeSpan.FromSeconds(10))) {
                                DebugWrite("ListPlayers ran out of time for TeamSwap. 5 sec.", 4);
                            }

                            Queue<CPlayerInfo> movingQueue;
                            Queue<CPlayerInfo> checkingQueue;
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_TeamswapForceMoveQueue) {
                                movingQueue = new Queue<CPlayerInfo>(_TeamswapForceMoveQueue.ToArray());
                                _TeamswapForceMoveQueue.Clear();
                            }
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_TeamswapOnDeathCheckingQueue) {
                                checkingQueue = new Queue<CPlayerInfo>(_TeamswapOnDeathCheckingQueue.ToArray());
                                _TeamswapOnDeathCheckingQueue.Clear();
                            }

                            //Check for "on-death" move players
                            while (_TeamswapOnDeathMoveDic.Count > 0 && checkingQueue.Count > 0) {
                                if (!_pluginEnabled) {
                                    break;
                                }
                                //Dequeue the first/next player
                                String playerName = checkingQueue.Dequeue().SoldierName;
                                CPlayerInfo player;
                                //If they are 
                                if (_TeamswapOnDeathMoveDic.TryGetValue(playerName, out player)) {
                                    //Player has died, remove from the dictionary
                                    _TeamswapOnDeathMoveDic.Remove(playerName);
                                    //Add to move queue
                                    movingQueue.Enqueue(player);
                                }
                            }

                            while (movingQueue.Count > 0) {
                                if (!_pluginEnabled) {
                                    break;
                                }
                                CPlayerInfo player = movingQueue.Dequeue();
                                switch (player.TeamID) {
                                    case 1:
                                        if (!ContainsCPlayerInfo(_Team1MoveQueue, player.SoldierName)) {
                                            _Team1MoveQueue.Enqueue(player);
                                            PlayerSayMessage(player.SoldierName, "You have been added to the (" + team1.TeamName + " -> " + team2.TeamName + ") TeamSwap queue in position " + (IndexOfCPlayerInfo(_Team1MoveQueue, player.SoldierName) + 1) + ".");
                                        }
                                        else {
                                            PlayerSayMessage(player.SoldierName, team2.TeamName + " Team Full (" + team2.TeamPlayerCount + "/" + maxTeamPlayerCount + "). You are in queue position " + (IndexOfCPlayerInfo(_Team1MoveQueue, player.SoldierName) + 1));
                                        }
                                        break;
                                    case 2:
                                        if (!ContainsCPlayerInfo(_Team2MoveQueue, player.SoldierName)) {
                                            _Team2MoveQueue.Enqueue(player);
                                            PlayerSayMessage(player.SoldierName, "You have been added to the (" + team2.TeamName + " -> " + team1.TeamName + ") TeamSwap queue in position " + (IndexOfCPlayerInfo(_Team2MoveQueue, player.SoldierName) + 1) + ".");
                                        }
                                        else {
                                            PlayerSayMessage(player.SoldierName, team1.TeamName + " Team Full (" + team1.TeamPlayerCount + "/" + maxTeamPlayerCount + "). You are in queue position " + (IndexOfCPlayerInfo(_Team2MoveQueue, player.SoldierName) + 1));
                                        }
                                        break;
                                }
                            }
                        }
                        DebugWrite("Team Info: " + team1.TeamName + ": " + team1.TeamPlayerCount + "/" + maxTeamPlayerCount + " " + team2.TeamName + ": " + team2.TeamPlayerCount + "/" + maxTeamPlayerCount, 5);
                        if (_Team2MoveQueue.Count > 0 || _Team1MoveQueue.Count > 0) {
                            //Perform player moving
                            Boolean movedPlayer;
                            do {
                                if (!_pluginEnabled) {
                                    break;
                                }
                                movedPlayer = false;
                                if (_Team2MoveQueue.Count > 0) {
                                    if (team1.TeamPlayerCount < maxTeamPlayerCount) {
                                        CPlayerInfo player = _Team2MoveQueue.Dequeue();
                                        AdKatsPlayer dicPlayer;
                                        if (_playerDictionary.TryGetValue(player.SoldierName, out dicPlayer)) {
                                            if (dicPlayer.frostbitePlayerInfo.TeamID == 1) {
                                                //Skip the kill/swap if they are already on the goal team by some other means
                                                continue;
                                            }
                                        }
                                        if (String.IsNullOrEmpty(player.SoldierName)) {
                                            ConsoleError("soldiername null in team 2 -> 1 teamswap");
                                        }
                                        else {
                                            ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, "1", "1", "true");
                                            team1.TeamPlayerCount++;
                                            team2.TeamPlayerCount--;
                                        }
                                        PlayerSayMessage(player.SoldierName, "Swapping you from team " + team2.TeamName + " to team " + team1.TeamName);
                                        movedPlayer = true;
                                        Thread.Sleep(1000);
                                    }
                                }
                                if (_Team1MoveQueue.Count > 0) {
                                    if (team2.TeamPlayerCount < maxTeamPlayerCount) {
                                        CPlayerInfo player = _Team1MoveQueue.Dequeue();
                                        AdKatsPlayer dicPlayer;
                                        if (_playerDictionary.TryGetValue(player.SoldierName, out dicPlayer)) {
                                            if (dicPlayer.frostbitePlayerInfo.TeamID == 2) {
                                                //Skip the kill/swap if they are already on the goal team by some other means
                                                continue;
                                            }
                                        }
                                        if (String.IsNullOrEmpty(player.SoldierName)) {
                                            ConsoleError("soldiername null in team 1 -> 2 teamswap");
                                        }
                                        else {
                                            ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, "2", "1", "true");
                                            team2.TeamPlayerCount++;
                                            team1.TeamPlayerCount--;
                                        }
                                        PlayerSayMessage(player.SoldierName, "Swapping you from team " + team1.TeamName + "to team " + team2.TeamName);
                                        movedPlayer = true;
                                        Thread.Sleep(1000);
                                    }
                                }
                            } while (movedPlayer);
                        }
                        else {
                            DebugWrite("TSWAP: No players to swap. Waiting for input.", 4);
                            //There are no players to swap, wait.
                            _TeamswapWaitHandle.Reset();
                            _TeamswapWaitHandle.WaitOne(Timeout.Infinite);
                            continue;
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("TeamSwap thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in TeamSwap thread. Skipping current loop.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                    _TeamswapWaitHandle.Reset();
                    _TeamswapWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                }
                DebugWrite("TSWAP: Ending TeamSwap Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in teamswap thread.", e));
            }
        }

        //Whether a move queue contains a given player
        private bool ContainsCPlayerInfo(Queue<CPlayerInfo> queueList, String player) {
            DebugWrite("Entering containsCPlayerInfo", 7);
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
                HandleException(new AdKatsException("Error while checking for player in teamswap queue.", e));
            }
            DebugWrite("Exiting containsCPlayerInfo", 7);
            return false;
        }

        //The index of a player in the move queue
        private Int32 IndexOfCPlayerInfo(Queue<CPlayerInfo> queueList, String player) {
            DebugWrite("Entering getCPlayerInfo", 7);
            try {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 i = 0; i < queueList.Count; i++) {
                    if (playerArray[i].SoldierName == player) {
                        return i;
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while getting index of player in teamswap queue.", e));
            }
            DebugWrite("Exiting getCPlayerInfo", 7);
            return -1;
        }

        private void QueueRecordForProcessing(AdKatsRecord record) {
            DebugWrite("Entering queueRecordForProcessing", 7);
            try {
                DebugWrite("Preparing to queue record for processing", 6);
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_UnprocessedRecordQueue) {
                    //Queue the record for processing
                    _UnprocessedRecordQueue.Enqueue(record);
                    DebugWrite("Record queued for processing", 6);
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while queueing record for processing.", e);
                HandleException(record.record_exception);
            }
            DebugWrite("Exiting queueRecordForProcessing", 7);
        }

        private void CommandParsingThreadLoop() {
            try {
                DebugWrite("COMMAND: Starting Command Parsing Thread", 1);
                Thread.CurrentThread.Name = "Command";
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("COMMAND: Entering Command Parsing Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("COMMAND: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Sleep for 10ms
                        Thread.Sleep(10);

                        //Get all unparsed inbound messages
                        if (_UnparsedCommandQueue.Count > 0) {
                            DebugWrite("COMMAND: Preparing to lock command queue to retrive new commands", 7);
                            Queue<KeyValuePair<String, String>> unparsedCommands;
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_UnparsedCommandQueue) {
                                DebugWrite("COMMAND: Inbound commands found. Grabbing.", 6);
                                //Grab all messages in the queue
                                unparsedCommands = new Queue<KeyValuePair<String, String>>(_UnparsedCommandQueue.ToArray());
                                //Clear the queue for next run
                                _UnparsedCommandQueue.Clear();
                            }

                            //Loop through all commands in order that they came in
                            while (unparsedCommands.Count > 0) {
                                DebugWrite("COMMAND: begin reading command", 6);
                                //Dequeue the first/next command
                                KeyValuePair<String, String> commandPair = unparsedCommands.Dequeue();
                                String speaker = commandPair.Key;
                                String command = commandPair.Value;

                                AdKatsRecord record;
                                if (speaker == "Server") {
                                    record = new AdKatsRecord {
                                        record_source = AdKatsRecord.Sources.ServerCommand,
                                        source_name = "ProconAdmin"
                                    };
                                }
                                else {
                                    record = new AdKatsRecord {
                                        record_source = AdKatsRecord.Sources.InGame,
                                        source_name = speaker
                                    };
                                }

                                //Complete the record creation
                                CompleteRecordInformation(record, command);
                            }
                        }
                        else {
                            DebugWrite("COMMAND: No inbound commands, ready.", 7);
                            //No commands to parse, ready.
                            _CommandParsingWaitHandle.Reset();
                            _CommandParsingWaitHandle.WaitOne(Timeout.Infinite);
                            continue;
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("Command thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in Command thread. Skipping current loop.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                }
                DebugWrite("COMMAND: Ending Command Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in command parsing thread.", e));
            }
        }

        //Before calling this, the record is initialized, and command_source/source_name are filled
        public void CompleteRecordInformation(AdKatsRecord record, String message) {
            try {
                //Initial split of command by whitespace
                String[] splitMessage = message.Split(' ');
                if (splitMessage.Length < 1) {
                    DebugWrite("Completely blank command entered", 5);
                    SendMessageToSource(record, "You entered a completely blank command.");
                    FinalizeRecord(record);
                    return;
                }
                String commandString = splitMessage[0].ToLower();
                DebugWrite("Raw Command: " + commandString, 6);
                String remainingMessage = message.TrimStart(splitMessage[0].ToCharArray()).Trim();

                record.server_id = _serverID;
                record.record_time = DateTime.UtcNow;

                //GATE 1: Add Command
                AdKatsCommand commandType = null;
                if (_CommandTextDictionary.TryGetValue(commandString, out commandType)) {
                    record.command_type = commandType;
                    record.command_action = commandType;
                    DebugWrite("Command parsed. Command is " + commandType.command_key + ".", 5);
                }
                else {
                    //If command not parsable, return without creating
                    DebugWrite("Command not parsable", 6);
                    if (record.record_source == AdKatsRecord.Sources.ExternalPlugin) {
                        SendMessageToSource(record, "Command not parsable.");
                        FinalizeRecord(record);
                    }
                    return;
                }

                //GATE 2: Check Access Rights
                if (record.record_source == AdKatsRecord.Sources.ServerCommand && !_AllowAdminSayCommands) {
                    SendMessageToSource(record, "Access to commands using that method has been disabled in AdKats settings.");
                    FinalizeRecord(record);
                    return;
                }
                if (!_firstPlayerListComplete) {
                    SendMessageToSource(record, "Command startup sequence in progress, please wait.");
                    FinalizeRecord(record);
                    return;
                }
                //Check if player has the right to perform what he's asking, only perform for InGame actions
                if (record.record_source == AdKatsRecord.Sources.InGame) {
                    //Attempt to fetch the source player
                    if (!_playerDictionary.TryGetValue(record.source_name, out record.source_player)) {
                        ConsoleError("Source player not found in server for in-game command, unable to complete command.");
                        FinalizeRecord(record);
                        return;
                    }
                    if (!HasAccess(record.source_player, record.command_type)) {
                        DebugWrite("No rights to call command", 6);
                        //Only tell the user they dont have access if the command is active
                        if (record.command_type.command_active != AdKatsCommand.CommandActive.Disabled) {
                            SendMessageToSource(record, "Your user role " + record.source_player.player_role.role_name + " does not have access to " + record.command_type.command_name + ".");
                        }
                        FinalizeRecord(record);
                        return;
                    }
                }

                //GATE 3: Specific data based on command type.
                switch (record.command_type.command_key) {
                    case "player_move": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    break;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.record_message = "MovePlayer";
                                record.target_name = parameters[0];
                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    CompleteTargetInformation(record, false);
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;
                    case "player_fmove": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.record_message = "ForceMovePlayer";
                                record.target_name = parameters[0];
                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    CompleteTargetInformation(record, false);
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                return;
                        }
                    }
                        break;
                    case "self_teamswap": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //May only call this command from in-game
                        if (record.record_source != AdKatsRecord.Sources.InGame) {
                            SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                            FinalizeRecord(record);
                            return;
                        }
                        record.record_message = "TeamSwap";
                        record.target_name = record.source_name;
                        CompleteTargetInformation(record, false);
                    }
                        break;
                    case "self_assist": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //May only call this command from in-game
                        if (record.record_source != AdKatsRecord.Sources.InGame) {
                            SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Player Info Check
                        record.record_message = "Assist Losing Team";
                        record.target_name = record.source_name;
                        if (!_playerDictionary.TryGetValue(record.target_name, out record.target_player)) {
                            SendMessageToSource(record, "Player information not found. Unable to process command.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Team Info Check
                        AdKatsTeam team1 = _teamDictionary[1];
                        AdKatsTeam team2 = _teamDictionary[2];
                        AdKatsTeam winningTeam, losingTeam;
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
                        String debug = (record.source_name == "ColColonCleaner") ? ("[" + winningTeam.TeamTicketCount + ":" + losingTeam.TeamTicketCount + "][" + (int)winningTeam.TeamTicketDifferenceRate + ":" + (int)losingTeam.TeamTicketDifferenceRate + "]") : ("");
                        
                        record.record_message = "Assist Weak Team [" + winningTeam.TeamTicketCount + ":" + losingTeam.TeamTicketCount + "]";
                        if ((record.target_player.frostbitePlayerInfo.TeamID == winningTeam.TeamID) && 
                            Math.Abs(winningTeam.TeamTicketDifferenceRate) < Math.Abs(losingTeam.TeamTicketDifferenceRate))
                        {
                            //60 second timeout
                            Double timeout = (60 - (DateTime.UtcNow - _commandUsageTimes["self_assist"]).TotalSeconds);
                            if (timeout > 0)
                            {
                                SendMessageToSource(record, "Please wait " + timeout + " seconds before using assist. Thank you. " + debug);
                                FinalizeRecord(record);
                                return;
                            }

                            SendMessageToSource(record, "Queuing you to assist the weak team. Thank you. " + debug);
                        }
                        else
                        {
                            SendMessageToSource(record, "You may only assist a weak team. " + debug);
                            FinalizeRecord(record);
                            return;
                        }

                        QueueRecordForProcessing(record);
                    }
                        break;
                    case "self_kill": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //May only call this command from in-game
                        if (record.record_source != AdKatsRecord.Sources.InGame) {
                            SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.record_message = "Self-Inflicted";
                        record.target_name = record.source_name;
                        CompleteTargetInformation(record, false);
                    }
                        break;
                    case "player_kill": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, false);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                }
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                    return;
                                }

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_kick": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                    return;
                                }

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_ban_temp": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 3);

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
                            else if (stringDuration.EndsWith("y")) {
                                stringDuration = stringDuration.TrimEnd('y');
                                durationMultiplier = 525949.0;
                            }
                            if (!Double.TryParse(stringDuration, out recordDuration)) {
                                SendMessageToSource(record, "Invalid time given, unable to submit.");
                                return;
                            }
                            record.command_numeric = (int) (recordDuration * durationMultiplier);
                            if (record.command_numeric > 5259490.0) {
                                SendMessageToSource(record, "You cannot temp ban for longer than 10 years. Do a permaban instead.");
                                return;
                            }
                        }

                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.command_numeric = (int) (recordDuration * durationMultiplier);
                                //Target is source
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 2:
                                record.command_numeric = (int) (recordDuration * durationMultiplier);

                                record.target_name = parameters[1];
                                DebugWrite("target: " + record.target_name, 6);

                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            case 3:
                                record.command_numeric = (int) (recordDuration * durationMultiplier);

                                record.target_name = parameters[1];
                                DebugWrite("target: " + record.target_name, 6);

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                    return;
                                }

                                DebugWrite("reason: " + record.record_message, 6);

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_ban_perm": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                    return;
                                }

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_ban_perm_future": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " cannot be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (!_UseBanEnforcer || !_UseBanEnforcerPreviousState) {
                            SendMessageToSource(record, " can only be used when ban enforcer is enabled.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 3);

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
                            else if (stringDuration.EndsWith("y")) {
                                stringDuration = stringDuration.TrimEnd('y');
                                durationMultiplier = 525949.0;
                            }
                            if (!Double.TryParse(stringDuration, out recordDuration)) {
                                SendMessageToSource(record, "Invalid time given, unable to submit.");
                                return;
                            }
                            record.command_numeric = (int) (recordDuration * durationMultiplier);
                        }
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.command_numeric = (int) (recordDuration * durationMultiplier);
                                //Target is source
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 2:
                                record.command_numeric = (int) (recordDuration * durationMultiplier);

                                record.target_name = parameters[1];
                                DebugWrite("target: " + record.target_name, 6);

                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            case 3:
                                record.command_numeric = (int) (recordDuration * durationMultiplier);

                                record.target_name = parameters[1];
                                DebugWrite("target: " + record.target_name, 6);

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                    return;
                                }

                                DebugWrite("reason: " + record.record_message, 6);

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_unban": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (!_UseBanEnforcer || !_UseBanEnforcerPreviousState) {
                            SendMessageToSource(record, "The unban command can only be used when ban enforcer is enabled.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.record_message = "Admin Unban";

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                //Unban the last player you've banned
                                SendMessageToSource(record, "Unbanning the last person you banned is not implemented yet.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                //Unban the target player
                                String partialName = parameters[0];

                                if (String.IsNullOrEmpty(partialName) || partialName.Length < 3) {
                                    SendMessageToSource(record, "Name search must be at least 3 characters.");
                                    FinalizeRecord(record);
                                    return;
                                }

                                var matchingBans = new List<AdKatsBan>();
                                List<AdKatsPlayer> matchingPlayers;
                                if (FetchMatchingPlayers(partialName, out matchingPlayers, false)) {
                                    foreach (AdKatsPlayer aPlayer in matchingPlayers) {
                                        matchingBans.AddRange(FetchPlayerBans(aPlayer));
                                    }
                                }
                                if (matchingBans.Count == 0) {
                                    SendMessageToSource(record, "No players matching '" + partialName + "' have active bans.");
                                    FinalizeRecord(record);
                                }
                                if (matchingBans.Count <= 3) {
                                    foreach (AdKatsBan innerBan in matchingBans) {
                                        SendMessageToSource(record, innerBan.ban_record.target_player.player_name + " | " + innerBan.ban_record.record_message);
                                    }
                                    AdKatsBan aBan = matchingBans[0];
                                    record.target_name = aBan.ban_record.target_player.player_name;
                                    record.target_player = aBan.ban_record.target_player;
                                    ConfirmActionWithSource(record);
                                }
                                else {
                                    SendMessageToSource(record, "Too many banned players match your search, try again.");
                                    FinalizeRecord(record);
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_punish": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                    return;
                                }

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_forgive": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                    return;
                                }

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_mute": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                    return;
                                }

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_join": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "foreveralone.jpg");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                record.target_name = parameters[0];
                                record.record_message = "Joining Player";
                                if (!HandleRoundReport(record)) {
                                    CompleteTargetInformation(record, false);
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_roundwhitelist": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.record_message = "Self-Inflicted";
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, true);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!HandleRoundReport(record)) {
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            case 2:
                                record.target_name = parameters[0];

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], false);

                                //Handle based on report ID if possible
                                if (!HandleRoundReport(record)) {
                                    if (record.record_message.Length >= _RequiredReasonLength) {
                                        CompleteTargetInformation(record, false);
                                    }
                                    else {
                                        SendMessageToSource(record, "Reason too short, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_report": {
                        //Get the command text for report
                        String command = _CommandKeyDictionary["player_report"].command_text;

                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], false);

                                DebugWrite("reason: " + record.record_message, 6);

                                //Only 1 character reasons are required for reports and admin calls
                                if (record.record_message.Length >= 1) {
                                    CompleteTargetInformation(record, false);
                                }
                                else {
                                    DebugWrite("reason too short", 6);
                                    SendMessageToSource(record, "Reason too short, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_calladmin": {
                        //Get the command text for call admin
                        String command = _CommandKeyDictionary["player_calladmin"].command_text;

                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                //attempt to handle via pre-message ID
                                record.record_message = GetPreMessage(parameters[1], false);

                                DebugWrite("reason: " + record.record_message, 6);
                                //Only 1 character reasons are required for reports and admin calls
                                if (record.record_message.Length >= 1) {
                                    CompleteTargetInformation(record, false);
                                }
                                else {
                                    DebugWrite("reason too short", 6);
                                    SendMessageToSource(record, "Reason too short, unable to submit.");
                                    FinalizeRecord(record);
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "server_nuke": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                String targetTeam = parameters[0];
                                record.record_message = "Nuke Server";
                                DebugWrite("target: " + targetTeam, 6);
                                if (targetTeam.ToLower().Contains("us")) {
                                    AdKatsTeam aTeam = GetTeamByKey("US");
                                    if (aTeam != null) {
                                        record.target_name = aTeam.TeamName;
                                        record.command_numeric = aTeam.TeamID;
                                        record.record_message += " (" + aTeam.TeamName + ")";
                                    }
                                    else {
                                        SendMessageToSource(record, "Team US does not exist on this map.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                else if (targetTeam.ToLower().Contains("ru")) {
                                    AdKatsTeam aTeam = GetTeamByKey("RU");
                                    if (aTeam != null) {
                                        record.target_name = aTeam.TeamName;
                                        record.command_numeric = aTeam.TeamID;
                                        record.record_message += " (" + aTeam.TeamName + ")";
                                    }
                                    else {
                                        SendMessageToSource(record, "Team RU does not exist on this map.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                else if (targetTeam.ToLower().Contains("cn")) {
                                    AdKatsTeam aTeam = GetTeamByKey("CN");
                                    if (aTeam != null) {
                                        record.target_name = aTeam.TeamName;
                                        record.command_numeric = aTeam.TeamID;
                                        record.record_message += " (" + aTeam.TeamName + ")";
                                    }
                                    else {
                                        SendMessageToSource(record, "Team CN does not exist on this map.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                else if (targetTeam.ToLower().Contains("all")) {
                                    record.target_name = "Everyone";
                                    record.record_message += " (Everyone)";
                                }
                                else {
                                    SendMessageToSource(record, "Use 'US', 'RU', 'CN', or 'ALL' as targets.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                //Have the admin confirm the action
                                ConfirmActionWithSource(record);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "server_kickall":
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.target_name = "Non-Admins";
                        record.record_message = "Kick All Players";
                        ConfirmActionWithSource(record);
                        break;
                    case "server_swapnuke":
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        /*if ((_playerDictionary.Count + 1) >= _serverInfo.MaxPlayerCount)
                        {
                            SendMessageToSource(record, record.command_type.command_key + " be performed without an open slot in the server to move players.");
                            FinalizeRecord(record);
                            return;
                        }*/

                        record.target_name = "Everyone";
                        record.record_message = "TeamSwap All Players";
                        ConfirmActionWithSource(record);
                        break;
                    case "round_end": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                String targetTeam = parameters[0];
                                DebugWrite("target team: " + targetTeam, 6);
                                record.record_message = "End Round";
                                if (targetTeam.ToLower().Contains("us")) {
                                    AdKatsTeam aTeam = GetTeamByKey("US");
                                    if (aTeam != null) {
                                        record.target_name = aTeam.TeamName;
                                        record.command_numeric = aTeam.TeamID;
                                        record.record_message += " (" + aTeam.TeamKey + " Win)";
                                    }
                                    else {
                                        SendMessageToSource(record, "Team US does not exist on this map.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                else if (targetTeam.ToLower().Contains("ru")) {
                                    AdKatsTeam aTeam = GetTeamByKey("RU");
                                    if (aTeam != null) {
                                        record.target_name = aTeam.TeamName;
                                        record.command_numeric = aTeam.TeamID;
                                        record.record_message += " (" + aTeam.TeamKey + " Win)";
                                    }
                                    else {
                                        SendMessageToSource(record, "Team RU does not exist on this map.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                else if (targetTeam.ToLower().Contains("cn")) {
                                    AdKatsTeam aTeam = GetTeamByKey("CN");
                                    if (aTeam != null) {
                                        record.target_name = aTeam.TeamName;
                                        record.command_numeric = aTeam.TeamID;
                                        record.record_message += " (" + aTeam.TeamKey + " Win)";
                                    }
                                    else {
                                        SendMessageToSource(record, "Team CN does not exist on this map.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                else {
                                    SendMessageToSource(record, "Use 'US', 'RU', or 'CN' as team codes to end round");
                                    FinalizeRecord(record);
                                    return;
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                        //Have the admin confirm the action
                        ConfirmActionWithSource(record);
                    }
                        break;
                    case "round_restart":
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.target_name = "Server";
                        record.record_message = "Restart Round";
                        ConfirmActionWithSource(record);
                        break;
                    case "round_next":
                        CancelSourcePendingAction(record);

                        if (_serverType == "OFFICIAL") {
                            SendMessageToSource(record, record.command_type.command_key + " be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.target_name = "Server";
                        record.record_message = "Run Next Map";
                        ConfirmActionWithSource(record);
                        break;
                    case "self_whatis": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                record.record_message = GetPreMessage(parameters[0], true);
                                if (record.record_message == null) {
                                    SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                    FinalizeRecord(record);
                                }
                                SendMessageToSource(record, record.record_message);
                                FinalizeRecord(record);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                        //This type is not processed
                    }
                        break;
                    case "self_voip": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Send them voip information
                        SendMessageToSource(record, _ServerVoipAddress);
                        FinalizeRecord(record);
                    }
                        break;
                    case "self_rules": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                record.target_name = record.source_name;
                                record.record_message = "Player Requested Rules";
                                if (record.record_source == AdKatsRecord.Sources.InGame) {
                                    if (_playerDictionary.TryGetValue(record.target_name, out record.target_player)) {
                                        record.target_name = record.target_player.player_name;
                                        //Cancel call if rules already requested in the past 10 seconds
                                        if (record.target_player.TargetedRecords.Any(aRecord => aRecord.command_action.command_key == "self_rules" && aRecord.record_time.AddSeconds(Math.Abs(_ServerRulesList.Count() * _ServerRulesInterval)) > DateTime.UtcNow)) {
                                            SendMessageToSource(record, "Please wait for previous rules request to finish.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    else {
                                        ConsoleError("394871 this error should never happen.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                                else {
                                    record.target_name = "ExternalSource";
                                }
                                QueueRecordForProcessing(record);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                record.record_message = "Telling Player Rules";
                                if (!HandleRoundReport(record)) {
                                    CompleteTargetInformation(record, false);
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "self_uptime": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        record.record_message = "Player Requested Uptime";
                        if (record.record_source == AdKatsRecord.Sources.InGame) {
                            record.target_name = record.source_name;
                            if (_playerDictionary.TryGetValue(record.target_name, out record.target_player)) {
                                record.target_name = record.target_player.player_name;
                            }
                            else {
                                ConsoleError("48204928 this error should never happen.");
                                FinalizeRecord(record);
                                return;
                            }
                        }
                        else {
                            record.target_name = "ExternalSource";
                        }
                        QueueRecordForProcessing(record);
                    }
                        break;
                    case "self_admins": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        record.record_message = "Player Requested Online Admins";
                        if (record.record_source == AdKatsRecord.Sources.InGame) {
                            record.target_name = record.source_name;
                            CompleteTargetInformation(record, false);
                        }
                        else {
                            record.target_name = "ExternalSource";
                            QueueRecordForProcessing(record);
                        }
                    }
                        break;
                    case "self_lead": {
                        //Remove previous commands awaiting confirmationf
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                if (record.record_source != AdKatsRecord.Sources.InGame) {
                                    SendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.target_name = record.source_name;
                                record.record_message = "Player Taking Squad Lead";
                                CompleteTargetInformation(record, false);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                record.record_message = "Giving Player Squad Lead";
                                if (!HandleRoundReport(record)) {
                                    CompleteTargetInformation(record, false);
                                }
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "admin_accept": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "Report ID must be given. Unable to submit.");
                                FinalizeRecord(record);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!AcceptRoundReport(record)) {
                                    SendMessageToSource(record, "Invalid report ID given, unable to submit.");
                                }
                                FinalizeRecord(record);
                                return;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                        record.record_action_executed = true;
                    }
                        break;
                    case "admin_deny": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "Report ID must be given. Unable to submit.");
                                FinalizeRecord(record);
                                break;
                            case 1:
                                record.target_name = parameters[0];
                                //Handle based on report ID as only option
                                if (!DenyRoundReport(record)) {
                                    SendMessageToSource(record, "Invalid report ID given, unable to submit.");
                                }
                                FinalizeRecord(record);
                                return;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                        record.record_action_executed = true;
                    }
                        break;
                    case "admin_say": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                record.record_message = GetPreMessage(parameters[0], false);
                                DebugWrite("message: " + record.record_message, 6);
                                record.target_name = "Server";
                                QueueRecordForProcessing(record);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_say": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "No message given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                CompleteTargetInformation(record, false);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "admin_yell": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                record.record_message = GetPreMessage(parameters[0], false);
                                DebugWrite("message: " + record.record_message, 6);
                                record.target_name = "Server";
                                QueueRecordForProcessing(record);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_yell": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "No message given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                CompleteTargetInformation(record, false);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "admin_tell": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 1);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                record.record_message = GetPreMessage(parameters[0], false);
                                DebugWrite("message: " + record.record_message, 6);
                                record.target_name = "Server";
                                QueueRecordForProcessing(record);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_tell": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "No message given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                CompleteTargetInformation(record, false);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_blacklistdisperse": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (!_FeedMultiBalancerDisperseList) {
                            SendMessageToSource(record, "Enable 'Feed MULTIBalancer Even Dispersion List' to use this command.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "No message given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                CompleteTargetInformation(record, false);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_whitelistbalance": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (!_FeedMultiBalancerWhitelist) {
                            SendMessageToSource(record, "Enable 'Feed MULTIBalancer Whitelist' to use this command.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "No message given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                CompleteTargetInformation(record, false);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_slotreserved": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (!_FeedServerReservedSlots) {
                            SendMessageToSource(record, "Enable 'Feed Server Reserved Slots' to use this command.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "No message given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                CompleteTargetInformation(record, false);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "player_slotspectator": {
                        //Remove previous commands awaiting confirmation
                        CancelSourcePendingAction(record);

                        if (!_FeedServerSpectatorList) {
                            SendMessageToSource(record, "Enable 'Feed Server Spectator Slots' to use this command.");
                            FinalizeRecord(record);
                            return;
                        }

                        //Parse parameters using max param count
                        String[] parameters = ParseParameters(remainingMessage, 2);
                        switch (parameters.Length) {
                            case 0:
                                SendMessageToSource(record, "No parameters given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 1:
                                SendMessageToSource(record, "No message given, unable to submit.");
                                FinalizeRecord(record);
                                return;
                            case 2:
                                record.target_name = parameters[0];
                                DebugWrite("target: " + record.target_name, 6);

                                record.record_message = GetPreMessage(parameters[1], false);
                                DebugWrite("message: " + record.record_message, 6);

                                CompleteTargetInformation(record, false);
                                break;
                            default:
                                SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                FinalizeRecord(record);
                                return;
                        }
                    }
                        break;
                    case "command_confirm":
                        DebugWrite("attempting to confirm command", 6);
                        AdKatsRecord recordAttempt = null;
                        _ActionConfirmDic.TryGetValue(record.source_name, out recordAttempt);
                        if (recordAttempt != null) {
                            DebugWrite("command found, calling processing", 6);
                            _ActionConfirmDic.Remove(record.source_name);
                            QueueRecordForProcessing(recordAttempt);
                            FinalizeRecord(record);
                        }
                        DebugWrite("no command to confirm", 6);
                        FinalizeRecord(record);
                        //This type is not processed
                        break;
                    case "command_cancel":
                        DebugWrite("attempting to cancel command", 6);
                        if (!_ActionConfirmDic.Remove(record.source_name)) {
                            DebugWrite("no command to cancel", 6);
                        }
                        //This type is not processed
                        FinalizeRecord(record);
                        break;
                    default:
                        ConsoleError("Unable to complete record for " + record.command_type.command_key + ", handler not found.");
                        FinalizeRecord(record);
                        return;
                }
            }
            catch (Exception e) {
                record.record_exception = HandleException(new AdKatsException("Error occured while completing record information.", e));
                FinalizeRecord(record);
            }
        }

        private AdKatsTeam GetTeamByKey(String teamKey) {
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (_teamDictionary) {
                foreach (AdKatsTeam curTeam in _teamDictionary.Values) {
                    if (String.Equals(curTeam.TeamKey, teamKey, StringComparison.CurrentCultureIgnoreCase)) {
                        //Team found
                        return curTeam;
                    }
                }
            }
            return null;
        }

        public void FinalizeRecord(AdKatsRecord record) {
            if (record.external_responseRequested) {
                var responseHashtable = new Hashtable {
                    {"caller_identity", "AdKats"},
                    {"response_requested", false},
                    {"response_type", "IssueCommand"},
                    {"response_value", CPluginVariable.EncodeStringArray(record.debugMessages.ToArray())}
                };
                ExecuteCommand("procon.protected.plugins.call", record.external_responseClass, record.external_responseMethod, JSON.JsonEncode(responseHashtable));
            }
            //Performance testing area
            if (record.source_name == _DebugSoldierName) {
                SendMessageToSource(record, "Duration: " + ((int) DateTime.UtcNow.Subtract(_CommandStartTime).TotalMilliseconds) + "ms");
            }
            if (record.record_source == AdKatsRecord.Sources.InGame || record.record_source == AdKatsRecord.Sources.InternalAutomated) {
                DebugWrite("In-Game/Automated " + record.command_action.command_key + " record took " + (DateTime.UtcNow - record.record_time).TotalMilliseconds + "ms to complete.", 3);
            }
        }

        public void CompleteTargetInformation(AdKatsRecord record, Boolean requireConfirm) {
            try {
                //Check for an exact match
                if (_playerDictionary.TryGetValue(record.target_name, out record.target_player)) {
                    record.target_name = record.target_player.player_name;
                    if (!requireConfirm) {
                        //Process record right away
                        QueueRecordForProcessing(record);
                    }
                    else {
                        ConfirmActionWithSource(record);
                    }
                }
                else {
                    List<String> currentPlayerNames = _playerDictionary.Keys.ToList();
                    //Get all subString matches
                    var subStringMatches = new List<string>();
                    subStringMatches.AddRange(currentPlayerNames.Where(playerName => Regex.Match(playerName, record.target_name, RegexOptions.IgnoreCase).Success));
                    if (subStringMatches.Count == 1) {
                        //Only one subString match, call processing without confirmation if able
                        if (!_playerDictionary.TryGetValue(subStringMatches[0], out record.target_player)) {
                            ConsoleError("Error fetching player for substring match.");
                            return;
                        }
                        record.target_name = subStringMatches[0];
                        if (!requireConfirm) {
                            //Process record right away
                            QueueRecordForProcessing(record);
                        }
                        else {
                            ConfirmActionWithSource(record);
                        }
                    }
                    else if (subStringMatches.Count > 1) {
                        //Multiple players matched the query, choose correct one
                        String msg = "'" + record.target_name + "' matches multiple players: ";
                        bool first = true;
                        String suggestion = null;
                        foreach (String playerName in subStringMatches) {
                            if (first) {
                                msg = msg + playerName;
                                first = false;
                            }
                            else {
                                msg = msg + ", " + playerName;
                            }
                            //Suggest player names that start with the text admins entered over others
                            if (playerName.ToLower().StartsWith(record.target_name.ToLower())) {
                                suggestion = playerName;
                            }
                        }
                        if (suggestion == null) {
                            //If no player id starts with what admins typed, suggest subString id with lowest Levenshtein distance
                            Int32 bestDistance = Int32.MaxValue;
                            foreach (String playerName in subStringMatches) {
                                Int32 distance = LevenshteinDistance(record.target_name, playerName);
                                if (distance < bestDistance) {
                                    bestDistance = distance;
                                    suggestion = playerName;
                                }
                            }
                        }
                        //If the suggestion is still null, something has failed
                        if (suggestion == null) {
                            DebugWrite("id suggestion system failed subString match", 5);
                            SendMessageToSource(record, "name suggestion system failed substring match");
                            FinalizeRecord(record);
                            return;
                        }

                        //Inform admin of multiple players found
                        SendMessageToSource(record, msg);

                        //Use suggestion for target
                        if (_playerDictionary.TryGetValue(suggestion, out record.target_player)) {
                            record.target_name = suggestion;
                            //Send record to attempt list for confirmation
                            ConfirmActionWithSource(record);
                        }
                        else {
                            ConsoleError("Substring match fetch failed.");
                            FinalizeRecord(record);
                        }
                    }
                    else {
                        //There were no players found, run a fuzzy search using Levenshtein Distance on all players in server
                        String fuzzyMatch = null;
                        Int32 bestDistance = Int32.MaxValue;
                        foreach (String playerName in currentPlayerNames) {
                            Int32 distance = LevenshteinDistance(record.target_name, playerName);
                            if (distance < bestDistance) {
                                bestDistance = distance;
                                fuzzyMatch = playerName;
                            }
                        }
                        //If the suggestion is still null, something has failed
                        if (fuzzyMatch == null) {
                            DebugWrite("id suggestion system failed fuzzy match", 5);
                            SendMessageToSource(record, "Player suggestion could not find a matching player.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (_playerDictionary.TryGetValue(fuzzyMatch, out record.target_player)) {
                            record.target_name = fuzzyMatch;
                            //Send record to attempt list for confirmation
                            ConfirmActionWithSource(record);
                        }
                        else {
                            SendMessageToSource(record, "Player suggestion found matching player, but it could not be fetched.");
                            FinalizeRecord(record);
                        }
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = HandleException(new AdKatsException("Error while completing target information.", e));
                FinalizeRecord(record);
            }
        }

        public void ConfirmActionWithSource(AdKatsRecord record) {
            DebugWrite("Entering confirmActionWithSource", 7);
            try {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_ActionConfirmDic) {
                    //Cancel any source pending action
                    CancelSourcePendingAction(record);
                    //Send record to attempt list
                    _ActionConfirmDic.Add(record.source_name, record);
                    SendMessageToSource(record, record.command_type.command_name + "->" + record.target_name + " for " + record.record_message + "?");
                }
            }
            catch (Exception e) {
                record.record_exception = HandleException(new AdKatsException("Error while confirming action with record source.", e));
            }
            DebugWrite("Exiting confirmActionWithSource", 7);
        }

        public void CancelSourcePendingAction(AdKatsRecord record) {
            DebugWrite("Entering cancelSourcePendingAction", 7);
            try {
                DebugWrite("attempting to cancel command", 6);
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_ActionConfirmDic) {
                    if (_ActionConfirmDic.Remove(record.source_name)) {
                        SendMessageToSource(record, "Previous command Canceled.");
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = HandleException(new AdKatsException("Error while canceling source pending action.", e));
            }
            DebugWrite("Exiting cancelSourcePendingAction", 7);
        }

        /*public void AutoWhitelistPlayers() {
            try {
                ConsoleWrite(DateTime.Now.Ticks + " " + (String.IsNullOrEmpty(Thread.CurrentThread.Name)?("mainthread"):(Thread.CurrentThread.Name)) + " preparing to lock at " + new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber()); lock (_PlayerDictionary) {
                    if (_PlayersToAutoWhitelist > 0) {
                        var random = new Random();
                        var playerListCopy = new List<String>();
                        foreach (AdKatsPlayer player in _PlayerDictionary.Values) {
                            DebugWrite("Checking for TeamSwap access on " + player.player_name, 6);
                            if (!HasAccess(player, _CommandKeyDictionary["self_teamswap"])) {
                                DebugWrite("player doesnt have access, adding them to chance list", 6);
                                if (!playerListCopy.Contains(player.player_name)) {
                                    playerListCopy.Add(player.player_name);
                                }
                            }
                        }
                        if (playerListCopy.Count > 0) {
                            Int32 maxIndex = (playerListCopy.Count < _PlayersToAutoWhitelist) ? (playerListCopy.Count) : (_PlayersToAutoWhitelist);
                            DebugWrite("MaxIndex: " + maxIndex, 6);
                            for (Int32 index = 0; index < maxIndex; index++) {
                                String playerName = null;
                                Int32 iterations = 0;
                                do {
                                    playerName = playerListCopy[random.Next(0, playerListCopy.Count - 1)];
                                } while (_TeamswapRoundWhitelist.ContainsKey(playerName) && (iterations++ < 100));
                                if (!_TeamswapRoundWhitelist.ContainsKey(playerName)) {
                                    AdKatsPlayer aPlayer = null;
                                    if (_PlayerDictionary.TryGetValue(playerName, out aPlayer)) {
                                        //Create the Exception record
                                        var record = new AdKatsRecord {
                                                                                   record_source = AdKatsRecord.Sources.InternalAutomated,
                                                                                   server_id = _ServerID,
                                                                                   command_type = _CommandKeyDictionary["player_roundwhitelist"],
                                                                                   command_numeric = 0,
                                                                                   target_name = aPlayer.player_name,
                                                                                   target_player = aPlayer,
                                                                                   source_name = "AdKats",
                                                                                   record_message = "Round-Whitelisting Player"
                                                                               };
                                        //Process the record
                                        QueueRecordForProcessing(record);
                                    }
                                    else {
                                        ConsoleError("Player was not in player dictionary when calling auto-whitelist.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while auto-whitelisting players.", e));
            }
        }*/

        public AdKatsRecord FetchRoundReport(String reportID, Boolean remove) {
            AdKatsRecord reportedRecord = null;
            try {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_RoundReports) {
                    if (_RoundReports.TryGetValue(reportID, out reportedRecord) && remove) {
                        if (_RoundReports.Remove(reportID)) {
                            DebugWrite("Round report [" + reportID + "] removed.", 3);
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching round report.", e));
            }
            return reportedRecord;
        }

        public Boolean DenyRoundReport(AdKatsRecord record) {
            try {
                DebugWrite("Attempting to handle based on round report.", 6);
                AdKatsRecord reportedRecord = FetchRoundReport(record.target_name, true);
                if (reportedRecord != null) {
                    DebugWrite("Denying round report.", 5);
                    reportedRecord.command_action = _CommandKeyDictionary["player_report_deny"];
                    UpdateRecord(reportedRecord);
                    SendMessageToSource(reportedRecord, "Your report [" + reportedRecord.command_numeric + "] has been denied.");
                    OnlineAdminSayMessage("Report [" + reportedRecord.command_numeric + "] has been denied by " + record.source_name + ".");

                    record.target_name = reportedRecord.source_name;
                    record.target_player = reportedRecord.source_player;
                    QueueRecordForProcessing(record);
                    return true;
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while denying round report.", e));
            }
            return false;
        }

        public Boolean AcceptRoundReport(AdKatsRecord record) {
            try {
                DebugWrite("Attempting to handle based on round report.", 6);
                AdKatsRecord reportedRecord = FetchRoundReport(record.target_name, true);
                if (reportedRecord != null) {
                    DebugWrite("Accepting round report.", 5);
                    reportedRecord.command_action = _CommandKeyDictionary["player_report_confirm"];
                    UpdateRecord(reportedRecord);
                    SendMessageToSource(reportedRecord, "Your report [" + reportedRecord.command_numeric + "] has been accepted. Thank you.");
                    OnlineAdminSayMessage("Report [" + reportedRecord.command_numeric + "] has been accepted by " + record.source_name + ".");

                    record.target_name = reportedRecord.source_name;
                    record.target_player = reportedRecord.source_player;

                    record.record_action_executed = true;
                    QueueRecordForProcessing(record);
                    return true;
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while denying round report.", e));
            }
            return false;
        }

        public Boolean HandleRoundReport(AdKatsRecord record) {
            try {
                DebugWrite("Attempting to handle based on round report.", 6);
                AdKatsRecord reportedRecord = FetchRoundReport(record.target_name, true);
                if (reportedRecord != null) {
                    DebugWrite("Handling round report.", 5);
                    reportedRecord.command_action = _CommandKeyDictionary["player_report_confirm"];
                    UpdateRecord(reportedRecord);
                    SendMessageToSource(reportedRecord, "Your report [" + reportedRecord.command_numeric + "] has been acted on. Thank you.");
                    OnlineAdminSayMessage("Report [" + reportedRecord.command_numeric + "] has been acted on by " + record.source_name + ".");

                    record.target_name = reportedRecord.target_name;
                    record.target_player = reportedRecord.target_player;
                    if (String.IsNullOrEmpty(record.record_message) || record.record_message.Length < _RequiredReasonLength) {
                        record.record_message = reportedRecord.record_message;
                    }
                    QueueRecordForProcessing(record);
                    return true;
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while handling round report.", e);
                HandleException(record.record_exception);
            }
            return false;
        }

        //replaces the message with a pre-message
        public String GetPreMessage(String message, Boolean required) {
            DebugWrite("Entering getPreMessage", 7);
            try {
                if (!string.IsNullOrEmpty(message)) {
                    //Attempt to fill the message via pre-message ID
                    Int32 preMessageID = 0;
                    DebugWrite("Raw preMessageID: " + message, 6);
                    Boolean valid = Int32.TryParse(message, out preMessageID);
                    if (valid && (preMessageID > 0) && (preMessageID <= _PreMessageList.Count)) {
                        message = _PreMessageList[preMessageID - 1];
                    }
                    else if (required) {
                        return null;
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while getting pre-message.", e));
            }
            DebugWrite("Exiting getPreMessage", 7);
            return message;
        }

        private void QueueRecordForActionHandling(AdKatsRecord record) {
            DebugWrite("Entering queueRecordForActionHandling", 6);
            try {
                DebugWrite("Preparing to queue record for action handling", 6);
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_UnprocessedActionQueue) {
                    _UnprocessedActionQueue.Enqueue(record);
                    DebugWrite("Record queued for action handling", 6);
                    _ActionHandlingWaitHandle.Set();
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while queuing record for action handling.", e);
                HandleException(record.record_exception);
            }
            DebugWrite("Exiting queueRecordForActionHandling", 6);
        }

        private void ActionHandlingThreadLoop() {
            try {
                DebugWrite("ACTION: Starting Action Thread", 1);
                Thread.CurrentThread.Name = "action";
                DateTime loopStart;
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("ACTION: Entering Action Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("ACTION: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        //Sleep for 10ms
                        Thread.Sleep(10);

                        //Handle Inbound Actions
                        if (_UnprocessedActionQueue.Count > 0) {
                            Queue<AdKatsRecord> unprocessedActions;
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_UnprocessedActionQueue) {
                                DebugWrite("ACTION: Inbound actions found. Grabbing.", 6);
                                //Grab all messages in the queue
                                unprocessedActions = new Queue<AdKatsRecord>(_UnprocessedActionQueue.ToArray());
                                //Clear the queue for next run
                                _UnprocessedActionQueue.Clear();
                            }
                            //Loop through all records in order that they came in
                            while (unprocessedActions.Count > 0) {
                                DebugWrite("ACTION: Preparing to Run Actions for record", 6);
                                //Dequeue the record
                                AdKatsRecord record = unprocessedActions.Dequeue();

                                //Run the appropriate action
                                RunAction(record);
                                //If more processing is needed, then perform it
                                //If any errors exist in the record, do not re-queue
                                if (record.record_exception == null) {
                                    QueueRecordForProcessing(record);
                                }
                                else {
                                    DebugWrite("ACTION: Record has errors, not re-queueing after action.", 3);
                                }
                            }
                        }
                        else {
                            DebugWrite("ACTION: No inbound actions. Waiting.", 6);
                            //Wait for new actions
                            _ActionHandlingWaitHandle.Reset();
                            _ActionHandlingWaitHandle.WaitOne(Timeout.Infinite);
                            continue;
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("Action Handling thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in Action Handling thread. Skipping current loop.", e));
                    }
                    if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                        ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                }
                DebugWrite("ACTION: Ending Action Handling Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in action handling thread.", e));
            }
        }

        private void RunAction(AdKatsRecord record) {
            DebugWrite("Entering runAction", 6);
            try {
                //Make sure record has an action
                if (record.command_action == null) {
                    record.command_action = record.command_type;
                }
                //Perform Actions
                switch (record.command_action.command_key) {
                    case "player_changename":
                    case "player_changeip":
                        record.record_action_executed = true;
                        break;
                    case "player_move":
                        MoveTarget(record);
                        break;
                    case "player_fmove":
                        ForceMoveTarget(record);
                        break;
                    case "self_teamswap":
                        ForceMoveTarget(record);
                        break;
                    case "self_assist":
                        AssistWeakTeam(record);
                        break;
                    case "self_kill":
                        KillTarget(record, null);
                        break;
                    case "player_kill":
                        KillTarget(record, null);
                        break;
                    case "player_kill_lowpop":
                        KillTarget(record, null);
                        break;
                    case "player_kill_repeat":
                        KillTarget(record, null);
                        break;
                    case "player_kick":
                        KickTarget(record, null);
                        break;
                    case "player_ban_temp":
                        TempBanTarget(record, null);
                        break;
                    case "player_ban_perm":
                        PermaBanTarget(record, null);
                        break;
                    case "player_ban_perm_future":
                        FuturePermaBanTarget(record, null);
                        break;
                    case "player_unban":
                        UnBanTarget(record, null);
                        break;
                    case "player_punish":
                        PunishTarget(record);
                        break;
                    case "player_forgive":
                        ForgiveTarget(record);
                        break;
                    case "player_mute":
                        MuteTarget(record);
                        break;
                    case "player_join":
                        JoinTarget(record);
                        break;
                    case "player_roundwhitelist":
                        RoundWhitelistTarget(record);
                        break;
                    case "player_report":
                        ReportTarget(record);
                        break;
                    case "player_calladmin":
                        CallAdminOnTarget(record);
                        break;
                    case "round_restart":
                        RestartLevel(record);
                        break;
                    case "round_next":
                        NextLevel(record);
                        break;
                    case "round_end":
                        EndLevel(record);
                        break;
                    case "server_nuke":
                        NukeTarget(record);
                        break;
                    case "server_kickall":
                        KickAllPlayers(record);
                        break;
                    case "server_swapnuke":
                        SwapNuke(record);
                        break;
                    case "admin_say":
                        AdminSay(record);
                        break;
                    case "player_say":
                        PlayerSay(record);
                        break;
                    case "admin_yell":
                        AdminYell(record);
                        break;
                    case "player_yell":
                        PlayerYell(record);
                        break;
                    case "admin_tell":
                        AdminTell(record);
                        break;
                    case "player_tell":
                        PlayerTell(record);
                        break;
                    case "player_blacklistdisperse":
                        BalanceDisperseTarget(record);
                        break;
                    case "player_whitelistbalance":
                        BalanceWhitelistTarget(record);
                        break;
                    case "player_slotreserved":
                        ReservedSlotTarget(record);
                        break;
                    case "player_slotspectator":
                        SpectatorSlotTarget(record);
                        break;
                    case "self_rules":
                        SendServerRules(record);
                        break;
                    case "self_uptime":
                        SendUptime(record);
                        break;
                    case "self_admins":
                        SendOnlineAdmins(record);
                        break;
                    case "self_lead":
                        LeadCurrentSquad(record);
                        break;
                    case "admin_accept":
                        //Don't do anything here
                        break;
                    case "admin_deny":
                        //Don't do anything here
                        break;
                    case "banenforcer_enforce":
                        //Don't do anything here, ban enforcer handles this
                        break;
                    case "adkats_exception":
                        record.record_action_executed = true;
                        break;
                    default:
                        record.record_action_executed = true;
                        SendMessageToSource(record, "Command not recognized when running " + record.command_action.command_key + " action.");
                        record.record_exception = HandleException(new AdKatsException("Command " + record.command_action + " not found in runAction"));
                        FinalizeRecord(record);
                        break;
                }
                DebugWrite(record.command_type.command_key + " last used " + FormatTimeString(DateTime.UtcNow - _commandUsageTimes[record.command_type.command_key], 10) + " ago.", 3);
                _commandUsageTimes[record.command_type.command_key] = DateTime.UtcNow;
            }
            catch (Exception e) {
                record.record_exception = HandleException(new AdKatsException("Error while choosing action for record.", e));
            }
            DebugWrite("Exiting runAction", 6);
        }

        public void MoveTarget(AdKatsRecord record) {
            DebugWrite("Entering moveTarget", 6);
            try {
                QueuePlayerForMove(record.target_player.frostbitePlayerInfo);
                PlayerSayMessage(record.target_name, "On your next death you will be moved to the opposing team.");
                SendMessageToSource(record, record.target_name + " will be sent to TeamSwap on their next death.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for move record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting moveTarget", 6);
        }

        public void ForceMoveTarget(AdKatsRecord record) {
            DebugWrite("Entering forceMoveTarget", 6);
            String message = null;
            try {
                if (record.command_type == _CommandKeyDictionary["self_teamswap"]) {
                    if ((record.source_player != null && HasAccess(record.source_player, _CommandKeyDictionary["self_teamswap"])) || ((_TeamSwapTicketWindowHigh >= _HighestTicketCount) && (_TeamSwapTicketWindowLow <= _LowestTicketCount))) {
                        message = "Calling Teamswap on self";
                        DebugWrite(message, 6);
                        QueuePlayerForForceMove(record.target_player.frostbitePlayerInfo);
                    }
                    else {
                        message = "Player unable to TeamSwap";
                        DebugWrite(message, 6);
                        SendMessageToSource(record, "You cannot TeamSwap at this time. Game outside ticket window [" + _TeamSwapTicketWindowLow + ", " + _TeamSwapTicketWindowHigh + "].");
                    }
                }
                else {
                    message = "TeamSwap called on " + record.target_name;
                    DebugWrite("Calling Teamswap on target", 6);
                    SendMessageToSource(record, "" + record.target_name + " sent to TeamSwap.");
                    QueuePlayerForForceMove(record.target_player.frostbitePlayerInfo);
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for force-move/teamswap record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting forceMoveTarget", 6);
        }

        public void AssistWeakTeam(AdKatsRecord record) {
            DebugWrite("Entering AssistLosingTeam", 6);
            try {
                QueuePlayerForForceMove(record.target_player.frostbitePlayerInfo);
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for assist record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting AssistLosingTeam", 6);
        }

        public void KillTarget(AdKatsRecord record, String additionalMessage) {
            DebugWrite("Entering killTarget", 6);
            String message = null;
            try {
                additionalMessage = !string.IsNullOrEmpty(additionalMessage) ? (" " + additionalMessage) : ("");
                if (record.source_name != record.target_name) {
                    switch (_gameVersion) {
                        case GameVersion.BF3:
                            if ((record.source_name == "AutoAdmin" || record.source_name == "ProconAdmin") && record.command_type.command_key == "player_punish") {
                                AdminSayMessage("Punishing " + record.target_name + " for " + record.record_message);
                            }
                            var seconds = (int) DateTime.UtcNow.Subtract(record.target_player.lastDeath).TotalSeconds;
                            DebugWrite("Killing player. Player last died " + seconds + " seconds ago.", 3);
                            if (seconds < 6 && record.command_action.command_key != "player_kill_repeat") {
                                DebugWrite("Queueing player for kill on spawn. (" + seconds + ")&(" + record.command_action + ")", 3);
                                if (!_ActOnSpawnDictionary.ContainsKey(record.target_player.player_name)) {
                                    if (_isTestingAuthorized)
                                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                    lock (_ActOnSpawnDictionary) {
                                        record.command_action = _CommandKeyDictionary["player_kill_repeat"];
                                        _ActOnSpawnDictionary.Add(record.target_player.player_name, record);
                                    }
                                }
                            }
                            break;
                        case GameVersion.BF4:
                            if (!record.isAliveChecked) {
                                if ((record.source_name == "AutoAdmin" || record.source_name == "ProconAdmin") && record.command_type.command_key == "player_punish") {
                                    AdminSayMessage("Punishing " + record.target_name + " for " + record.record_message);
                                }
                                if (!_ActOnIsAliveDictionary.ContainsKey(record.target_player.player_name)) {
                                    if (_isTestingAuthorized)
                                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                    lock (_ActOnIsAliveDictionary) {
                                        _ActOnIsAliveDictionary.Add(record.target_player.player_name, record);
                                    }
                                }
                                ExecuteCommand("procon.protected.send", "player.isAlive", record.target_name);
                                return;
                            }
                            break;
                        default:
                            ConsoleError("Invalid game version in killtarget");
                            return;
                    }
                }

                //Perform actions
                if (String.IsNullOrEmpty(record.target_player.player_name)) {
                    ConsoleError("playername null in 5437");
                }
                else {
                    ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_player.player_name);
                    if (record.source_name != record.target_name) {
                        PlayerTellMessage(record.target_name, "Killed by admin for " + record.record_message + " " + additionalMessage);
                        SendMessageToSource(record, "You KILLED " + record.target_name + " for " + record.record_message + additionalMessage);
                    }
                    else {
                        PlayerTellMessage(record.target_name, "You killed yourself");
                    }
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for kill record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting killTarget", 6);
        }

        public void KickTarget(AdKatsRecord record, String additionalMessage) {
            DebugWrite("Entering kickTarget", 6);
            try {
                String kickReason = GenerateKickReason(record);
                //Perform Actions
                DebugWrite("Kick Message: '" + kickReason + "'", 3);
                if (String.IsNullOrEmpty(record.target_player.player_name) || String.IsNullOrEmpty(kickReason)) {
                    ConsoleError("Item null in 5464");
                }
                else {
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", record.target_player.player_name, kickReason);
                    if (record.target_name != record.source_name) {
                        AdminSayMessage("Player " + record.target_name + " was KICKED by " + ((_ShowAdminNameInSay) ? (record.source_name) : ("admin")) + " for " + record.record_message + " " + additionalMessage);
                    }
                    SendMessageToSource(record, "You KICKED " + record.target_name + " for " + record.record_message + ". " + additionalMessage);
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for kick record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting kickTarget", 6);
        }

        public void TempBanTarget(AdKatsRecord record, String additionalMessage) {
            DebugWrite("Entering tempBanTarget", 6);
            try {
                //Subtract 1 second for visual effect
                Int32 seconds = (record.command_numeric * 60) - 1;

                //Perform Actions
                //Only post to ban enforcer if there are no exceptions
                if (_UseBanEnforcer && record.record_exception == null) {
                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                    //Create the ban
                    var aBan = new AdKatsBan {
                        ban_record = record,
                        ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable)),
                        ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable)),
                        ban_enforceIP = ipAvailable && (_DefaultEnforceIP || (!nameAvailable && !guidAvailable))
                    };

                    //Queue the ban for upload
                    QueueBanForProcessing(aBan);
                }
                else {
                    if (record.record_exception != null) {
                        HandleException(new AdKatsException("Defaulting to procon banlist usage since exceptions existed in record"));
                    }
                    //Trim the ban message if necessary
                    String banMessage = record.source_name + " - " + record.record_message;
                    Int32 cutLength = banMessage.Length - 80;
                    if (cutLength > 0) {
                        banMessage = banMessage.Substring(0, banMessage.Length - cutLength);
                    }
                    DebugWrite("Ban Message: '" + banMessage + "'", 3);
                    if (!String.IsNullOrEmpty(record.target_player.player_guid)) {
                        ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_player.player_guid, "seconds", seconds + "", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_ip)) {
                        ExecuteCommand("procon.protected.send", "banList.add", "ip", record.target_player.player_ip, "seconds", seconds + "", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_name)) {
                        ExecuteCommand("procon.protected.send", "banList.add", "id", record.target_player.player_name, "seconds", seconds + "", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else {
                        ConsoleError("Player has no information to ban with.");
                        SendMessageToSource(record, "ERROR");
                    }
                }
                if (record.target_name != record.source_name) {
                    AdminSayMessage("Player " + record.target_player.player_name + " was BANNED by " + ((_ShowAdminNameInSay) ? (record.source_name) : ("admin")) + " for " + record.record_message + " " + additionalMessage);
                }
                SendMessageToSource(record, "You TEMP BANNED " + record.target_name + " for " + FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 3) + "." + additionalMessage);
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for TempBan record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting tempBanTarget", 6);
        }

        public void PermaBanTarget(AdKatsRecord record, String additionalMessage) {
            DebugWrite("Entering permaBanTarget", 6);
            try {
                //Perform Actions
                //Only post to ban enforcer if there are no exceptions
                if (_UseBanEnforcer && record.record_exception == null) {
                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                    //Create the ban
                    var aBan = new AdKatsBan {
                        ban_record = record,
                        ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable)),
                        ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable)),
                        ban_enforceIP = ipAvailable && (_DefaultEnforceIP || (!nameAvailable && !guidAvailable))
                    };

                    //Queue the ban for upload
                    QueueBanForProcessing(aBan);
                }
                else {
                    if (record.record_exception != null) {
                        HandleException(new AdKatsException("Defaulting to procon banlist usage since exceptions existed in record"));
                    }
                    //Trim the ban message if necessary
                    String banMessage = record.source_name + " - " + record.record_message;
                    Int32 cutLength = banMessage.Length - 80;
                    if (cutLength > 0) {
                        banMessage = banMessage.Substring(0, banMessage.Length - cutLength);
                    }
                    DebugWrite("Ban Message: '" + banMessage + "'", 3);
                    if (!String.IsNullOrEmpty(record.target_player.player_guid)) {
                        ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_player.player_guid, "perm", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_ip)) {
                        ExecuteCommand("procon.protected.send", "banList.add", "ip", record.target_player.player_ip, "perm", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_name)) {
                        ExecuteCommand("procon.protected.send", "banList.add", "id", record.target_player.player_name, "perm", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else {
                        ConsoleError("Player has no information to ban with.");
                        SendMessageToSource(record, "ERROR");
                    }
                }
                if (record.target_name != record.source_name) {
                    AdminSayMessage("Player " + record.target_player.player_name + " was BANNED by " + ((_ShowAdminNameInSay) ? (record.source_name) : ("admin")) + " for " + record.record_message + additionalMessage);
                }
                SendMessageToSource(record, "You PERMA BANNED " + record.target_player.player_name + "." + additionalMessage);
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for PermaBan record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting permaBanTarget", 6);
        }

        public void FuturePermaBanTarget(AdKatsRecord record, String additionalMessage) {
            DebugWrite("Entering permaBanTarget", 6);
            try {
                record.record_action_executed = true;
                if (_UseBanEnforcer && record.record_exception == null) {
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);
                    var aBan = new AdKatsBan {
                        ban_record = record,
                        ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable)),
                        ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable)),
                        ban_enforceIP = ipAvailable && (_DefaultEnforceIP || (!nameAvailable && !guidAvailable))
                    };
                    QueueBanForProcessing(aBan);
                    DateTime endTime = record.record_time + TimeSpan.FromMinutes(record.command_numeric);
                    SendMessageToSource(record, "You FUTURE BANNED " + record.target_player.player_name + ". Their ban will activate at " + endTime + " UTC. " + additionalMessage);
                }
                else {
                    SendMessageToSource(record, "Future ban cannot be posted.");
                    FinalizeRecord(record);
                    return;
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for PermaBan record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting permaBanTarget", 6);
        }

        public void UnBanTarget(AdKatsRecord record, String additionalMessage) {
            DebugWrite("Entering UnBanTarget", 6);
            try {
                record.record_action_executed = true;
                if (record.target_player == null) {
                    ConsoleError("Player was null when attempting to unban.");
                    FinalizeRecord(record);
                    return;
                }
                List<AdKatsBan> banList = FetchPlayerBans(record.target_player);
                if (banList.Count == 0) {
                    ConsoleError("Bans could not be fetched when attempting to unban");
                    FinalizeRecord(record);
                    return;
                }
                foreach (AdKatsBan aBan in banList) {
                    aBan.ban_status = "Disabled";
                    UpdateBanStatus(aBan);
                }
                SendMessageToSource(record, record.target_player.player_name + " is now unbanned.");
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for UnBan record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting UnBanTarget", 6);
        }

        public void EnforceBan(AdKatsBan aBan, Boolean verbose) {
            DebugWrite("Entering enforceBan", 6);
            try {
                //Create the total kick message
                String generatedBanReason = GenerateBanReason(aBan);
                DebugWrite("Ban Enforce Message: '" + generatedBanReason + "'", 3);

                //Perform Actions
                if (_playerDictionary.ContainsKey(aBan.ban_record.target_player.player_name) && aBan.ban_startTime < DateTime.UtcNow) {
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", aBan.ban_record.target_player.player_name, generatedBanReason);
                    //Inform the server of the enforced ban
                    if (verbose) {
                        AdminSayMessage("Enforcing ban on " + aBan.ban_record.target_player.player_name + " for " + aBan.ban_record.record_message);
                    }
                }
            }
            catch (Exception e) {
                aBan.ban_exception = new AdKatsException("Error while enforcing ban.", e);
                HandleException(aBan.ban_exception);
            }
            DebugWrite("Exiting enforceBan", 6);
        }

        public void PunishTarget(AdKatsRecord record) {
            DebugWrite("Entering punishTarget", 6);
            try {
                //If the record has any exceptions, skip everything else and just kill the player
                if (record.record_exception == null) {
                    //Get number of points the player from server
                    Int32 points = FetchPoints(record.target_player, false);
                    DebugWrite(record.target_player.player_name + " has " + points + " points.", 5);
                    //Get the proper action to take for player punishment
                    String action = "noaction";
                    String skippedAction = null;
                    if (points > (_PunishmentHierarchy.Length - 1)) {
                        action = _PunishmentHierarchy[_PunishmentHierarchy.Length - 1];
                    }
                    else if (points > 1) {
                        action = _PunishmentHierarchy[points - 1];
                        if (record.isIRO) {
                            skippedAction = _PunishmentHierarchy[points - 2];
                        }
                    }
                    else {
                        action = _PunishmentHierarchy[0];
                    }

                    //Handle the case where and IRO punish skips higher level punishment for a lower one, use the higher one
                    if (skippedAction != null && _PunishmentSeverityIndex.IndexOf(skippedAction) > _PunishmentSeverityIndex.IndexOf(action)) {
                        action = skippedAction;
                    }

                    //Set additional message
                    String pointMessage = " [" + ((record.isIRO) ? ("IRO ") : ("")) + points + "pts]";
                    if (!record.record_message.Contains(pointMessage)) {
                        record.record_message += pointMessage;
                    }
                    const string additionalMessage = "";

                    Boolean isLowPop = _OnlyKillOnLowPop && (_playerDictionary.Count < _LowPopPlayerCount);
                    Boolean iroOverride = record.isIRO && _IROOverridesLowPop;

                    DebugWrite("Server low population: " + isLowPop + " (" + _playerDictionary.Count + " <? " + _LowPopPlayerCount + ") | Override: " + iroOverride, 5);

                    //Call correct action
                    if ((action == "kill" || (isLowPop && !iroOverride)) && !action.Equals("ban")) {
                        record.command_action = (isLowPop) ? (_CommandKeyDictionary["player_kill_lowpop"]) : (_CommandKeyDictionary["player_kill"]);
                        KillTarget(record, additionalMessage);
                    }
                    else if (action == "kick") {
                        record.command_action = _CommandKeyDictionary["player_kick"];
                        KickTarget(record, additionalMessage);
                    }
                    else if (action == "tban60") {
                        record.command_numeric = 60;
                        record.command_action = _CommandKeyDictionary["player_ban_temp"];
                        TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tban120") {
                        record.command_numeric = 120;
                        record.command_action = _CommandKeyDictionary["player_ban_temp"];
                        TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tbanday") {
                        record.command_numeric = 1440;
                        record.command_action = _CommandKeyDictionary["player_ban_temp"];
                        TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tbanweek") {
                        record.command_numeric = 10080;
                        record.command_action = _CommandKeyDictionary["player_ban_temp"];
                        TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tban2weeks") {
                        record.command_numeric = 20160;
                        record.command_action = _CommandKeyDictionary["player_ban_temp"];
                        TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "tbanmonth") {
                        record.command_numeric = 43200;
                        record.command_action = _CommandKeyDictionary["player_ban_temp"];
                        TempBanTarget(record, additionalMessage);
                    }
                    else if (action == "ban") {
                        record.command_action = _CommandKeyDictionary["player_ban_perm"];
                        PermaBanTarget(record, additionalMessage);
                    }
                    else {
                        record.command_action = _CommandKeyDictionary["player_kill"];
                        KillTarget(record, additionalMessage);
                        record.record_exception = new AdKatsException("Punish options are set incorrectly. '" + action + "' not found. Inform plugin setting manager.");
                        HandleException(record.record_exception);
                    }
                }
                else {
                    //Exception found, just kill the player
                    record.command_action = _CommandKeyDictionary["player_kill"];
                    KillTarget(record, null);
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Punish record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting punishTarget", 6);
        }

        public void ForgiveTarget(AdKatsRecord record) {
            DebugWrite("Entering forgiveTarget", 6);
            try {
                //If the record has any exceptions, skip everything
                if (record.record_exception == null) {
                    Int32 points = FetchPoints(record.target_player, false);
                    PlayerSayMessage(record.target_player.player_name, "Forgiven 1 infraction point. You now have " + points + " point(s) against you.");
                    SendMessageToSource(record, "Forgive Logged for " + record.target_player.player_name + ". They now have " + points + " infraction points.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Forgive record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting forgiveTarget", 6);
        }

        public void BalanceDisperseTarget(AdKatsRecord record) {
            DebugWrite("Entering DisperseTarget", 6);
            try {
                record.record_action_executed = true;
                List<AdKatsSpecialPlayer> matchingPlayers = FetchMatchingSpecialPlayers("blacklist_dispersion", record.target_player);
                if (matchingPlayers.Count > 0) {
                    SendMessageToSource(record, matchingPlayers.Count + " matching player(s) already under dispersion for this server.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        INSERT INTO
	                        `adkats_specialplayers`
                        (
	                        `player_group`,
	                        `player_id`,
                            `player_server`
                        )
                        VALUES
                        (
	                        'blacklist_dispersion',
	                        @player_id,
                            @player_server
                        )";
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_server", _serverID);

                        Int32 rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0) {
                            SendMessageToSource(record, "Player " + record.target_player.player_name + " added to even dispersion for this server.");
                            DebugWrite("Player " + record.target_player.player_name + " added to even dispersion for this server.", 3);
                            FetchAllAccess(true);
                        }
                        else {
                            ConsoleError("Unable to add player to even dispersion. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Disperse record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting DisperseTarget", 6);
        }

        public void BalanceWhitelistTarget(AdKatsRecord record) {
            DebugWrite("Entering BalanceWhitelistTarget", 6);
            try {
                record.record_action_executed = true;
                List<AdKatsSpecialPlayer> matchingPlayers = FetchMatchingSpecialPlayers("whitelist_multibalancer", record.target_player);
                if (matchingPlayers.Count > 0) {
                    SendMessageToSource(record, matchingPlayers.Count + " matching player(s) already under autobalance whitelist for this server.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        INSERT INTO
	                        `adkats_specialplayers`
                        (
	                        `player_group`,
	                        `player_id`,
                            `player_server`
                        )
                        VALUES
                        (
	                        'whitelist_multibalancer',
	                        @player_id,
                            @player_server
                        )";
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_server", _serverID);

                        Int32 rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0) {
                            SendMessageToSource(record, "Player " + record.target_player.player_name + " added to autobalance whitelist for this server.");
                            DebugWrite("Player " + record.target_player.player_name + " added to autobalance whitelist for this server.", 3);
                            FetchAllAccess(true);
                        }
                        else {
                            ConsoleError("Unable to add player to autobalance whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Balance Whitelist record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting BalanceWhitelistTarget", 6);
        }

        public void ReservedSlotTarget(AdKatsRecord record) {
            DebugWrite("Entering ReservedSlotTarget", 6);
            try {
                record.record_action_executed = true;
                List<AdKatsSpecialPlayer> matchingPlayers = FetchMatchingSpecialPlayers("slot_reserved", record.target_player);
                if (matchingPlayers.Count > 0) {
                    SendMessageToSource(record, matchingPlayers.Count + " matching player(s) already in reserved slot list for this server.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        INSERT INTO
	                        `adkats_specialplayers`
                        (
	                        `player_group`,
	                        `player_id`,
                            `player_server`
                        )
                        VALUES
                        (
	                        'slot_reserved',
	                        @player_id,
                            @player_server
                        )";
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_server", _serverID);

                        Int32 rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0) {
                            SendMessageToSource(record, "Player " + record.target_player.player_name + " added to reserved slot for this server.");
                            DebugWrite("Player " + record.target_player.player_name + " added to reserved slot for this server.", 3);
                            FetchAllAccess(true);
                        }
                        else {
                            ConsoleError("Unable to add player to reserved slot. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Reserved Slot record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting ReservedSlotTarget", 6);
        }

        public void SpectatorSlotTarget(AdKatsRecord record) {
            DebugWrite("Entering SpectatorSlotTarget", 6);
            try {
                record.record_action_executed = true;
                List<AdKatsSpecialPlayer> matchingPlayers = FetchMatchingSpecialPlayers("slot_spectator", record.target_player);
                if (matchingPlayers.Count > 0) {
                    SendMessageToSource(record, matchingPlayers.Count + " matching player(s) already in spectator slot list for this server.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        INSERT INTO
	                        `adkats_specialplayers`
                        (
	                        `player_group`,
	                        `player_id`,
                            `player_server`
                        )
                        VALUES
                        (
	                        'slot_spectator',
	                        @player_id,
                            @player_server
                        )";
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_server", _serverID);

                        Int32 rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0) {
                            SendMessageToSource(record, "Player " + record.target_player.player_name + " added to spectator slot for this server.");
                            DebugWrite("Player " + record.target_player.player_name + " added to spectator slot for this server.", 3);
                            FetchAllAccess(true);
                        }
                        else {
                            ConsoleError("Unable to add player to spectator slot. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Spectator Slot record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting SpectatorSlotTarget", 6);
        }

        public void MuteTarget(AdKatsRecord record) {
            DebugWrite("Entering muteTarget", 6);
            try {
                if (!HasAccess(record.target_player, _CommandKeyDictionary["player_mute"])) {
                    if (!_RoundMutedPlayers.ContainsKey(record.target_player.player_name)) {
                        _RoundMutedPlayers.Add(record.target_player.player_name, 0);
                        PlayerSayMessage(record.target_player.player_name, _MutedPlayerMuteMessage);
                        SendMessageToSource(record, record.target_player.player_name + " has been muted for this round.");
                    }
                    else {
                        SendMessageToSource(record, record.target_player.player_name + " already muted for this round.");
                    }
                }
                else {
                    SendMessageToSource(record, "You can't mute an admin.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Mute record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting muteTarget", 6);
        }

        public void JoinTarget(AdKatsRecord record) {
            DebugWrite("Entering joinTarget", 6);
            try {
                //Get source player
                AdKatsPlayer sourcePlayer = null;
                if (_playerDictionary.TryGetValue(record.source_name, out sourcePlayer)) {
                    //If the source has access to move players, then the squad will be unlocked for their entry
                    if (HasAccess(record.source_player, _CommandKeyDictionary["player_move"])) {
                        //Unlock target squad
                        SendMessageToSource(record, "Unlocking target squad if needed, please wait.");
                        ExecuteCommand("procon.protected.send", "squad.private", record.target_player.frostbitePlayerInfo.TeamID + "", record.target_player.frostbitePlayerInfo.SquadID + "", "false");
                        //If anything longer is needed...tisk tisk
                        Thread.Sleep(500);
                    }
                    //Check for player access to change teams
                    if (record.target_player.frostbitePlayerInfo.TeamID != sourcePlayer.frostbitePlayerInfo.TeamID && !HasAccess(record.source_player, _CommandKeyDictionary["self_teamswap"])) {
                        SendMessageToSource(record, "Target player is not on your team, you need @" + _CommandKeyDictionary["self_teamswap"].command_text + "/TeamSwap access to join them.");
                    }
                    else {
                        //Move to specific squad
                        ExecuteCommand("procon.protected.send", "admin.movePlayer", record.source_name, record.target_player.frostbitePlayerInfo.TeamID + "", record.target_player.frostbitePlayerInfo.SquadID + "", "true");
                        SendMessageToSource(record, "Attempting to join " + record.target_player.player_name);
                    }
                }
                else {
                    SendMessageToSource(record, "Unable to find you in the player list, please try again.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Join record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting joinTarget", 6);
        }

        public void RoundWhitelistTarget(AdKatsRecord record) {
            //Implementation removed until further notice
            return;
            DebugWrite("Entering roundWhitelistTarget", 6);
            try {
                if (!_TeamswapRoundWhitelist.ContainsKey(record.target_player.player_name)) {
                    if (_TeamswapRoundWhitelist.Count < _PlayersToAutoWhitelist + 2) {
                        _TeamswapRoundWhitelist.Add(record.target_player.player_name, false);
                        record.target_player.player_role.RoleAllowedCommands.Add("self_teamswap", GetCommandByKey("self_teamswap"));
                        String command = GetCommandByKey("self_teamswap").command_text;
                        SendMessageToSource(record, record.target_player.player_name + " can now use @" + command + " for this round.");
                    }
                    else {
                        SendMessageToSource(record, "Cannot whitelist more than two extra people per round.");
                    }
                }
                else {
                    SendMessageToSource(record, record.target_player.player_name + " is already in this round's TeamSwap whitelist.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for RoundWhitelist record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting roundWhitelistTarget", 6);
        }

        public void ReportTarget(AdKatsRecord record) {
            DebugWrite("Entering reportTarget", 6);
            try {
                var random = new Random();
                Int32 reportID;
                do {
                    reportID = random.Next(100, 999);
                } while (_RoundReports.ContainsKey(reportID + ""));
                record.command_numeric = reportID;
                _RoundReports.Add(reportID + "", record);
                record.record_action_executed = true;
                AttemptReportAutoAction(record, reportID + "");
                String sourceAAIdentifier = (record.source_player != null && record.source_player.player_aa) ? ("[AA]") : ("");
                String targetAAIdentifier = (record.target_player != null && record.target_player.player_aa) ? ("[AA]") : ("");
                String slotID = (record.target_player != null) ? (record.target_player.player_slot) : (null);
                if (!String.IsNullOrEmpty(slotID)) {
                    ExecuteCommand("procon.protected.send", "punkBuster.pb_sv_command", "pb_sv_getss " + slotID);
                }
                OnlineAdminSayMessage("REPORT [" + reportID + "]: " + sourceAAIdentifier + record.source_name + " reported " + targetAAIdentifier + record.target_player.player_name + " for " + record.record_message);
                if (_UseEmail) {
                    _EmailHandler.SendReport(record);
                }
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for Report record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting reportTarget", 6);
        }

        public void CallAdminOnTarget(AdKatsRecord record) {
            DebugWrite("Entering callAdminOnTarget", 6);
            try {
                var random = new Random();
                Int32 reportID;
                do {
                    reportID = random.Next(100, 999);
                } while (_RoundReports.ContainsKey(reportID + ""));
                record.command_numeric = reportID;
                _RoundReports.Add(reportID + "", record);
                AttemptReportAutoAction(record, reportID + "");
                String sourceAAIdentifier = (record.source_player != null && record.source_player.player_aa) ? ("[AA]") : ("");
                String targetAAIdentifier = (record.target_player != null && record.target_player.player_aa) ? ("[AA]") : ("");
                String slotID = (record.target_player != null) ? (record.target_player.player_slot) : (null);
                if (!String.IsNullOrEmpty(slotID)) {
                    ExecuteCommand("procon.protected.send", "punkBuster.pb_sv_command", "pb_sv_getss " + slotID);
                }
                OnlineAdminSayMessage("ADMIN CALL [" + reportID + "]: " + sourceAAIdentifier + record.source_name + " called admin on " + targetAAIdentifier + record.target_name + " for " + record.record_message);
                if (_UseEmail) {
                    _EmailHandler.SendReport(record);
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for CallAdmin record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting callAdminOnTarget", 6);
        }

        public void AttemptReportAutoAction(AdKatsRecord record, String reportID) {
            Boolean sourceAA = record.source_player != null && record.source_player.player_aa;
            Int32 onlineAdminCount = FetchOnlineAdminSoldiers().Count;
            String messageLower = record.record_message.ToLower();
            Boolean canAutoHandle = _UseAAReportAutoHandler && sourceAA && _AutoReportHandleStrings.Any() && !String.IsNullOrEmpty(_AutoReportHandleStrings[0]) && _AutoReportHandleStrings.Any(messageLower.Contains) && !record.target_player.player_aa && !RoleIsInteractionAble(record.target_player.player_role);
            Boolean adminsOnline = onlineAdminCount > 0;
            String reportMessage = "";
            if (!_isTestingAuthorized || !sourceAA || !adminsOnline) {
                reportMessage = "REPORT [" + reportID + "] sent on " + record.target_name + " for " + record.record_message;
            }
            else {
                reportMessage = "REPORT [" + reportID + "] on " + record.target_name + " sent to " + onlineAdminCount + " in-game admin" + ((onlineAdminCount > 1) ? ("s") : ("")) + ". " + ((canAutoHandle) ? ("Admins have 45 seconds before auto-handling.") : (""));
            }
            SendMessageToSource(record, reportMessage);
            if (!canAutoHandle) {
                //ConsoleWarn("canceling auto-handler.");
                return;
            }
            var reportAutoHandler = new Thread(new ThreadStart(delegate {
                //ConsoleWarn("Starting report auto-handler thread.");
                try {
                    Thread.CurrentThread.Name = "reportautohandler";
                    //If admins are online, act after 45 seconds. If they are not, act after 5 seconds.
                    Thread.Sleep(TimeSpan.FromSeconds((adminsOnline) ? (45.0) : (5.0)));
                    //Get the reported record
                    AdKatsRecord reportedRecord;
                    if (_RoundReports.TryGetValue(reportID, out reportedRecord) && _useExperimentalTools) {
                        if (CanPunish(reportedRecord, 90) || !adminsOnline) {
                            //Remove it from the reports for this round
                            _RoundReports.Remove(reportID);
                            //Update it in the database
                            reportedRecord.command_action = _CommandKeyDictionary["player_report_confirm"];
                            UpdateRecord(reportedRecord);
                            //Get target information
                            var aRecord = new AdKatsRecord {
                                record_source = AdKatsRecord.Sources.InternalAutomated,
                                server_id = _serverID,
                                command_type = _CommandKeyDictionary["player_punish"],
                                command_numeric = 0,
                                target_name = reportedRecord.target_player.player_name,
                                target_player = reportedRecord.target_player,
                                source_name = "ProconAdmin",
                                record_message = reportedRecord.record_message
                            };
                            //Inform the reporter that they helped the admins
                            SendMessageToSource(reportedRecord, "Your report [" + reportedRecord.command_numeric + "] has been acted on. Thank you.");
                            //Queue for processing
                            QueueRecordForProcessing(aRecord);
                        }
                        else {
                            SendMessageToSource(reportedRecord, "Reported player has already been acted on.");
                        }
                    }
                }
                catch (Exception) {
                    HandleException(new AdKatsException("Error while auto-handling report."));
                }
                DebugWrite("Exiting a report auto-handler.", 5);
            }));

            //Start the thread
            StartAndLogThread(reportAutoHandler);
        }

        public void RestartLevel(AdKatsRecord record) {
            DebugWrite("Entering restartLevel", 6);
            try {
                ExecuteCommand("procon.protected.send", "mapList.restartRound");
                SendMessageToSource(record, "Round Restarted.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for RestartLevel record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting restartLevel", 6);
        }

        public void NextLevel(AdKatsRecord record) {
            DebugWrite("Entering nextLevel", 6);
            try {
                ExecuteCommand("procon.protected.send", "mapList.runNextRound");
                SendMessageToSource(record, "Next round has been run.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for NextLevel record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting nextLevel", 6);
        }

        public void EndLevel(AdKatsRecord record) {
            DebugWrite("Entering forgiveTarget", 6);
            try {
                ExecuteCommand("procon.protected.send", "mapList.endRound", record.command_numeric + "");
                SendMessageToSource(record, "Ended round with " + record.target_name + " as winner.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for EndLevel record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting forgiveTarget", 6);
        }

        public void NukeTarget(AdKatsRecord record) {
            DebugWrite("Entering nukeTarget", 6);
            try {
                int count = 0;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_playerDictionary) {
                    foreach (AdKatsPlayer player in _playerDictionary.Values.Where(player => (player.frostbitePlayerInfo.TeamID == record.command_numeric) || (record.target_name == "Everyone"))) {
                        ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                        PlayerSayMessage(record.source_name, "Admin Nuke Issued On " + record.target_name);
                    }
                }
                SendMessageToSource(record, "You NUKED " + record.target_name + " for " + record.record_message + ".");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for NukeServer record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting nukeTarget", 6);
        }

        public void SwapNuke(AdKatsRecord record) {
            DebugWrite("Entering SwapNuke", 6);
            try {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_playerDictionary) {
                    foreach (AdKatsPlayer player in _playerDictionary.Values) {
                        QueuePlayerForForceMove(player.frostbitePlayerInfo);
                    }
                }
                SendMessageToSource(record, "You SwapNuked the server.");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for SwapNuke record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting SwapNuke", 6);
        }

        public void KickAllPlayers(AdKatsRecord record) {
            DebugWrite("Entering kickAllPlayers", 6);
            try {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_playerDictionary) {
                    foreach (AdKatsPlayer player in _playerDictionary.Values.Where(player => player.player_role.role_key == "guest_default")) {
                        Thread.Sleep(50);
                        ExecuteCommand("procon.protected.send", "admin.kickPlayer", player.player_name, "(" + record.source_name + ") " + record.record_message);
                    }
                }
                AdminSayMessage("All guest players have been kicked.");
                SendMessageToSource(record, "You KICKED EVERYONE for '" + record.record_message + "'");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for KickAll record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting kickAllPlayers", 6);
        }

        public void AdminSay(AdKatsRecord record) {
            DebugWrite("Entering adminSay", 6);
            try {
                AdminSayMessage(record.record_message);
                if (record.record_source != AdKatsRecord.Sources.InGame) {
                    SendMessageToSource(record, "Server has been told '" + record.record_message + "' by SAY");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for AdminSay record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting adminSay", 6);
        }

        public void PlayerSay(AdKatsRecord record) {
            DebugWrite("Entering playerSay", 6);
            try {
                PlayerSayMessage(record.target_player.player_name, record.record_message);
                SendMessageToSource(record, record.target_player.player_name + " has been told '" + record.record_message + "' by SAY");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for playerSay record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting playerSay", 6);
        }

        public void AdminYell(AdKatsRecord record) {
            DebugWrite("Entering adminYell", 6);
            try {
                AdminYellMessage(record.record_message);
                if (record.record_source != AdKatsRecord.Sources.InGame) {
                    SendMessageToSource(record, "Server has been told '" + record.record_message + "' by YELL");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for AdminYell record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting adminYell", 6);
        }

        public void PlayerYell(AdKatsRecord record) {
            DebugWrite("Entering playerYell", 6);
            try {
                PlayerYellMessage(record.target_player.player_name, record.record_message);
                SendMessageToSource(record, record.target_player.player_name + " has been told '" + record.record_message + "' by YELL");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for playerYell record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting playerYell", 6);
        }

        public void AdminTell(AdKatsRecord record) {
            DebugWrite("Entering adminTell", 6);
            try {
                AdminTellMessage(record.record_message);
                if (record.record_source != AdKatsRecord.Sources.InGame) {
                    SendMessageToSource(record, "Server has been told '" + record.record_message + "' by TELL");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for AdminYell record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting adminTell", 6);
        }

        public void PlayerTell(AdKatsRecord record) {
            DebugWrite("Entering playerTell", 6);
            try {
                PlayerTellMessage(record.target_player.player_name, record.record_message);
                SendMessageToSource(record, record.target_player.player_name + " has been told '" + record.record_message + "' by TELL");
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while taking action for playerTell record.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting playerTell", 6);
        }

        public void SendServerRules(AdKatsRecord record) {
            DebugWrite("Entering sendServerRules", 6);
            try {
                //If server has rules
                if (_ServerRulesList.Length > 0) {
                    var rulePrinter = new Thread(new ThreadStart(delegate {
                        DebugWrite("Starting a rule printer thread.", 5);
                        try {
                            Thread.CurrentThread.Name = "ruleprinter";
                            //Wait the rule delay duration
                            Thread.Sleep(TimeSpan.FromSeconds(_ServerRulesDelay));
                            Int32 ruleIndex = 0;
                            IEnumerable<string> validRules = _ServerRulesList.Where(rule => !String.IsNullOrEmpty(rule));
                            foreach (string rule in validRules) {
                                String currentPrefix = "(" + (++ruleIndex) + "/" + validRules.Count() + ") ";
                                if (record.target_player != null) {
                                    PlayerSayMessage(record.target_player.player_name, currentPrefix + GetPreMessage(rule, false));
                                }
                                else {
                                    SendMessageToSource(record, currentPrefix + GetPreMessage(rule, false));
                                }
                                Thread.Sleep(TimeSpan.FromSeconds(_ServerRulesInterval));
                            }
                        }
                        catch (Exception) {
                            HandleException(new AdKatsException("Error while printing server rules"));
                        }
                        DebugWrite("Exiting a rule printer.", 5);
                        LogThreadExit();
                    }));

                    //Start the thread
                    StartAndLogThread(rulePrinter);
                }
                //Set the executed bool
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while sending server rules.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting sendServerRules", 6);
        }

        public void SendUptime(AdKatsRecord record) {
            DebugWrite("Entering SendUptime", 6);
            try {
                var uptimePrinter = new Thread(new ThreadStart(delegate {
                    DebugWrite("Starting a uptime printer thread.", 5);
                    try {
                        Thread.CurrentThread.Name = "uptimeprinter";
                        Thread.Sleep(500);
                        SendMessageToSource(record, "Server: " + FormatTimeString(TimeSpan.FromSeconds(_serverInfo.ServerUptime), 10));
                        Thread.Sleep(2000);
                        SendMessageToSource(record, "Procon: " + FormatTimeString(DateTime.UtcNow - _ProconStartTime, 10));
                        Thread.Sleep(2000);
                        SendMessageToSource(record, "AdKats: " + FormatTimeString(DateTime.UtcNow - _AdKatsStartTime, 10));
                        Thread.Sleep(2000);
                        SendMessageToSource(record, "Last Player List: " + FormatTimeString(DateTime.UtcNow - _lastSuccessfulPlayerList, 10) + " ago");
                    }
                    catch (Exception) {
                        HandleException(new AdKatsException("Error while printing uptime"));
                    }
                    DebugWrite("Exiting a uptime printer.", 5);
                    LogThreadExit();
                }));

                //Start the thread
                StartAndLogThread(uptimePrinter);
                //Set the executed bool
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while sending uptime.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting SendUptime", 6);
        }

        public void SendOnlineAdmins(AdKatsRecord record) {
            DebugWrite("Entering SendOnlineAdmins", 6);
            try {
                String onlineAdmins = FetchOnlineAdminSoldiers().Aggregate("Online Admins:", (current, aPlayer) => current + (" " + aPlayer.player_name));
                //Send online admins
                SendMessageToSource(record, onlineAdmins);
                //Set the executed bool
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while sending online admins.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting SendOnlineAdmins", 6);
        }

        public void LeadCurrentSquad(AdKatsRecord record) {
            DebugWrite("Entering LeadCurrentSquad", 6);
            try {
                ExecuteCommand("procon.protected.send", "squad.leader", record.target_player.frostbitePlayerInfo.TeamID.ToString(), record.target_player.frostbitePlayerInfo.SquadID.ToString(), record.target_player.player_name);
                PlayerSayMessage(record.target_player.player_name, "You are now the leader of your current squad.");
                if (record.source_name != record.target_name) {
                    SendMessageToSource(record, record.target_player.player_name + " is now the leader of their current squad.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Error while leading curring squad.", e);
                HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            DebugWrite("Exiting LeadCurrentSquad", 6);
        }

        private void QueueUserForUpload(AdKatsUser user) {
            try {
                DebugWrite("Preparing to queue user for access upload.", 6);
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_UserUploadQueue) {
                    _UserUploadQueue.Enqueue(user);
                    DebugWrite("User queued for access upload", 6);
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queuing user upload.", e));
            }
        }

        private void QueueUserForRemoval(AdKatsUser user) {
            try {
                DebugWrite("Preparing to queue user for access removal", 6);
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_UserRemovalQueue) {
                    _UserRemovalQueue.Enqueue(user);
                    DebugWrite("User queued for access removal", 6);
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queuing access removal.", e));
            }
        }

        private Boolean HasAccess(AdKatsPlayer aPlayer, AdKatsCommand command) {
            try {
                if (aPlayer == null) {
                    ConsoleError("player was null in hasAccess.");
                    return false;
                }
                if (aPlayer.player_role == null) {
                    ConsoleError("player role was null in hasAccess.");
                    return false;
                }
                if (command == null) {
                    ConsoleError("Command was null in hasAccess.");
                    return false;
                }
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (aPlayer.player_role) {
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (aPlayer.player_role.RoleAllowedCommands) {
                        if (aPlayer.player_role.RoleAllowedCommands.ContainsKey(command.command_key)) {
                            return true;
                        }
                        if (aPlayer.player_role.ConditionalAllowedCommands.Values.Any(innerCommand => (innerCommand.Value.command_key == command.command_key) && innerCommand.Key(this, aPlayer))) {
                            return true;
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while checking command access on player.", e));
            }
            return false;
        }

        private void DatabaseCommunicationThreadLoop() {
            try {
                DebugWrite("DBCOMM: Starting Database Comm Thread", 1);
                Thread.CurrentThread.Name = "databasecomm";
                Boolean firstRun = true;
                DateTime loopStart;
                var counter = new Stopwatch();
                while (true) {
                    loopStart = DateTime.UtcNow;
                    try {
                        DebugWrite("DBCOMM: Entering Database Comm Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("DBCOMM: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        //Check if database connection settings have changed
                        if (_dbSettingsChanged) {
                            DebugWrite("DBCOMM: DB Settings have changed, calling test.", 6);
                            if (TestDatabaseConnection()) {
                                DebugWrite("DBCOMM: Database Connection Good. Continuing Thread.", 6);
                            }
                            else {
                                _dbSettingsChanged = true;
                                continue;
                            }
                        }
                        //On first run, pull all roles and commands and update database if needed
                        if (firstRun) {
                            FetchCommands();
                            FetchRoles();
                            UpdateDatabase37014000();
                        }
                        counter.Reset();
                        counter.Start();
                        FeedStatLoggerSettings();
                        counter.Stop();
                        if (FullDebug)
                            ConsoleWrite("DBCOMM: FeedStatLoggerSettings took " + counter.ElapsedMilliseconds + "ms");

                        //Update server ID
                        if (_serverID < 0) {
                            //Checking for database server info
                            if (FetchDBServerInfo()) {
                                ConsoleSuccess("Database Server Info Fetched. Server ID is " + _serverID + "!");

                                //Push all settings for this instance to the database
                                UploadAllSettings();
                            }
                            else {
                                //Inform the user
                                ConsoleError("Database Server info could not be fetched! Make sure XpKiller's Stat Logger is running on this server!");
                                //Disable the plugin
                                Disable();
                                break;
                            }
                        }
                        else {
                            DebugWrite("Skipping server ID fetch. Server ID: " + _serverID, 7);
                        }

                        //Check if settings need sync
                        if (firstRun || _settingImportID != _serverID || _lastDbSettingFetch.AddSeconds(DbSettingFetchFrequency) < DateTime.UtcNow) {
                            DebugWrite("Preparing to fetch settings from server " + _serverID, 6);
                            //Fetch new settings from the database
                            FetchSettings(_settingImportID, _settingImportID != _serverID);
                        }

                        counter.Reset();
                        counter.Start();
                        HandleSettingUploads();
                        counter.Stop();
                        if (FullDebug)
                            ConsoleWrite("DBCOMM: HandleSettingUploads took " + counter.ElapsedMilliseconds + "ms");

                        counter.Reset();
                        counter.Start();
                        HandleCommandUploads();
                        counter.Stop();
                        if (FullDebug)
                            ConsoleWrite("DBCOMM: HandleCommandUploads took " + counter.ElapsedMilliseconds + "ms");

                        counter.Reset();
                        counter.Start();
                        HandleRoleUploads();
                        counter.Stop();
                        if (FullDebug)
                            ConsoleWrite("DBCOMM: HandleRoleUploads took " + counter.ElapsedMilliseconds + "ms");

                        counter.Reset();
                        counter.Start();
                        HandleRoleRemovals();
                        counter.Stop();
                        if (FullDebug)
                            ConsoleWrite("DBCOMM: HandleRoleRemovals took " + counter.ElapsedMilliseconds + "ms");

                        counter.Reset();
                        counter.Start();
                        //Check for new actions from the database at given interval
                        if (_fetchActionsFromDb && (DateTime.UtcNow > _lastDbActionFetch.AddSeconds(DbActionFetchFrequency))) {
                            RunActionsFromDB();
                        }
                        else {
                            DebugWrite("DBCOMM: Skipping DB action fetch", 7);
                        }
                        counter.Stop();
                        if (FullDebug)
                            ConsoleWrite("DBCOMM: RunActionsFromDB took " + counter.ElapsedMilliseconds + "ms");

                        HandleUserChanges();

                        //Start the other threads
                        if (firstRun) {
                            _AdKatsStartTime = DateTime.UtcNow;

                            //Start other threads
                            StartAndLogThread(_PlayerListingThread);
                            StartAndLogThread(_AccessFetchingThread);
                            StartAndLogThread(_KillProcessingThread);
                            StartAndLogThread(_MessageProcessingThread);
                            StartAndLogThread(_CommandParsingThread);
                            StartAndLogThread(_ActionHandlingThread);
                            StartAndLogThread(_TeamSwapThread);
                            StartAndLogThread(_BanEnforcerThread);
                            StartAndLogThread(_HackerCheckerThread);
                            firstRun = false;

                            _threadsReady = true;
                            UpdateSettingPage();

                            //Register a command to indicate availibility to other plugins
                            RegisterCommand(_issueCommandMatchCommand);
                            RegisterCommand(_fetchAuthorizedSoldiersMatchCommand);

                            ConsoleWrite("^b^2Running!^n^0 Version: " + GetPluginVersion());
                        }

                        counter.Reset();
                        counter.Start();
                        if (_UseBanEnforcer) {
                            HandleActiveBanEnforcer();
                        }
                        else {
                            if (_UseBanEnforcerPreviousState) {
                                RepopulateProconBanList();
                                _UseBanEnforcerPreviousState = false;
                            }
                        }
                        counter.Stop();
                        if (FullDebug)
                            ConsoleWrite("DBCOMM: HandleActiveBanEnforcer took " + counter.ElapsedMilliseconds + "ms");

                        if (_UnprocessedRecordQueue.Count > 0) {
                            counter.Reset();
                            counter.Start();
                            DebugWrite("DBCOMM: Unprocessed: " + _UnprocessedRecordQueue.Count + " Current: 0", 4);
                            DebugWrite("DBCOMM: Preparing to lock inbound record queue to retrive new records", 7);
                            Queue<AdKatsRecord> inboundRecords;
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_UnprocessedRecordQueue) {
                                DebugWrite("DBCOMM: Inbound records found. Grabbing.", 6);
                                //Grab all messages in the queue
                                inboundRecords = new Queue<AdKatsRecord>(_UnprocessedRecordQueue.ToArray());
                                //Clear the queue for next run
                                _UnprocessedRecordQueue.Clear();
                            }
                            //Loop through all records in order that they came in
                            while (inboundRecords.Count > 0) {
                                DebugWrite("DBCOMM: Unprocessed: " + _UnprocessedRecordQueue.Count + " Current: " + inboundRecords.Count, 4);
                                //Pull the next record
                                AdKatsRecord record = inboundRecords.Dequeue();
                                //Process the record message
                                record.record_message = ReplacePlayerInformation(record.record_message, record.target_player);
                                //Upload the record
                                Boolean success = HandleRecordUpload(record);
                                if (record.target_player != null) {
                                    //Assign the record to the user's recent records
                                    record.target_player.TargetedRecords.Add(record);
                                }
                                //Check for action handling needs
                                if (!record.record_action_executed && success) {
                                    //Action is only called after initial upload, not after update
                                    DebugWrite("DBCOMM: Upload success. Attempting to add to action queue.", 6);

                                    //Only queue the record for action handling if it's not an enforced ban
                                    if (record.command_type.command_key != "banenforcer_enforce") {
                                        QueueRecordForActionHandling(record);
                                    }
                                }
                                else {
                                    DebugWrite("DBCOMM: Update success. Record does not need action handling.", 6);
                                    //finalize the record
                                    FinalizeRecord(record);
                                }
                            }
                            counter.Stop();
                            if (FullDebug)
                                ConsoleWrite("DBCOMM: UnprocessedRecords took " + counter.ElapsedMilliseconds + "ms");
                        }
                        else {
                            counter.Reset();
                            counter.Start();
                            DebugWrite("DBCOMM: No unprocessed records. Waiting for input", 7);
                            _DbCommunicationWaitHandle.Reset();

                            if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                                ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                            if (_fetchActionsFromDb || _UseBanEnforcer || _UsingAwa) {
                                //If waiting on DB input, the maximum time we can wait is "db action frequency"
                                _DbCommunicationWaitHandle.WaitOne(DbActionFetchFrequency * 1000);
                            }
                            else {
                                //Maximum wait time is DB access fetch time
                                _DbCommunicationWaitHandle.WaitOne(DbUserFetchFrequency * 1000);
                            }
                            counter.Stop();
                            if (FullDebug)
                                ConsoleWrite("DBCOMM: Waiting after complete took " + counter.ElapsedMilliseconds + "ms");
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("Database Comm thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in Database Comm thread. Skipping current loop.", e));
                    }
                }
                DebugWrite("DBCOMM: Ending Database Comm Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in database comm thread.", e));
            }
        }

        private void FeedStatLoggerSettings() {
            //Every 30 minutes feed stat logger settings
            if (_LastStatLoggerStatusUpdateTime.AddMinutes(60) < DateTime.UtcNow) {
                _LastStatLoggerStatusUpdateTime = DateTime.UtcNow;
                if (_statLoggerVersion == "BF3") {
                    if (_isTestingAuthorized) {
                        _FeedStatLoggerSettings = true;
                    }
                    if (_FeedStatLoggerSettings) {
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Chatlogging?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Log ServerSPAM?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Instant Logging of Chat Messages?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Statslogging?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Weaponstats?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable KDR correction?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "MapStats ON?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Session ON?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Save Sessiondata to DB?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Log playerdata only (no playerstats)?", "No");
                        Double slOffset = DateTime.UtcNow.Subtract(DateTime.Now).TotalHours;
                        SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Servertime Offset", slOffset + "");
                    }
                }
                else if (_statLoggerVersion == "UNIVERSAL") {
                    if (_isTestingAuthorized) {
                        _FeedStatLoggerSettings = true;
                    }
                    if (_FeedStatLoggerSettings) {
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Chatlogging?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Log ServerSPAM?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Instant Logging of Chat Messages?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Statslogging?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable Weaponstats?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Enable KDR correction?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "MapStats ON?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Session ON?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Save Sessiondata to DB?", "Yes");
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Log playerdata only (no playerstats)?", "No");
                        Double slOffset = DateTime.UtcNow.Subtract(DateTime.Now).TotalHours;
                        SetExternalPluginSetting("CChatGUIDStatsLogger", "Servertime Offset", slOffset + "");
                    }
                }
                else {
                    ConsoleError("Stat logger version is unknown, unable to feed stat logger settings.");
                }
                //TODO put back in the future
                //confirmStatLoggerSetup();
            }
        }

        private void HandleSettingUploads() {
            //Handle Inbound Setting Uploads
            if (_SettingUploadQueue.Count > 0) {
                DebugWrite("DBCOMM: Preparing to lock inbound setting queue to get new settings", 7);
                Queue<CPluginVariable> inboundSettingUpload;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_SettingUploadQueue) {
                    DebugWrite("DBCOMM: Inbound settings found. Grabbing.", 6);
                    //Grab all settings in the queue
                    inboundSettingUpload = new Queue<CPluginVariable>(_SettingUploadQueue.ToArray());
                    //Clear the queue for next run
                    _SettingUploadQueue.Clear();
                }
                //Loop through all settings in order that they came in
                while (inboundSettingUpload.Count > 0) {
                    CPluginVariable setting = inboundSettingUpload.Dequeue();

                    UploadSetting(setting);
                }
            }
        }

        private void HandleCommandUploads() {
            //Handle Inbound Command Uploads
            if (_CommandUploadQueue.Count > 0) {
                DebugWrite("DBCOMM: Preparing to lock inbound command queue to get new commands", 7);
                Queue<AdKatsCommand> inboundCommandUpload;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_CommandUploadQueue) {
                    DebugWrite("DBCOMM: Inbound commands found. Grabbing.", 6);
                    //Grab all commands in the queue
                    inboundCommandUpload = new Queue<AdKatsCommand>(_CommandUploadQueue.ToArray());
                    //Clear the queue for next run
                    _CommandUploadQueue.Clear();
                }
                //Loop through all commands in order that they came in
                while (inboundCommandUpload.Count > 0) {
                    AdKatsCommand command = inboundCommandUpload.Dequeue();

                    UploadCommand(command);
                }
                UpdateSettingPage();
            }
        }

        private void HandleRoleUploads() {
            //Handle Inbound Role Uploads
            if (_RoleUploadQueue.Count > 0) {
                DebugWrite("DBCOMM: Preparing to lock inbound role queue to get new roles", 7);
                Queue<AdKatsRole> inboundRoleUpload;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_RoleUploadQueue) {
                    DebugWrite("DBCOMM: Inbound roles found. Grabbing.", 6);
                    //Grab all roles in the queue
                    inboundRoleUpload = new Queue<AdKatsRole>(_RoleUploadQueue.ToArray());
                    //Clear the queue for next run
                    _RoleUploadQueue.Clear();
                }
                //Loop through all roles in order that they came in
                while (inboundRoleUpload.Count > 0) {
                    AdKatsRole aRole = inboundRoleUpload.Dequeue();
                    UploadRole(aRole);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_RoleIDDictionary) {
                        if (_RoleIDDictionary.ContainsKey(aRole.role_id)) {
                            _RoleIDDictionary[aRole.role_id] = aRole;
                        }
                        else {
                            _RoleIDDictionary.Add(aRole.role_id, aRole);
                        }
                        if (_RoleKeyDictionary.ContainsKey(aRole.role_key)) {
                            _RoleKeyDictionary[aRole.role_key] = aRole;
                        }
                        else {
                            _RoleKeyDictionary.Add(aRole.role_key, aRole);
                        }
                        if (_RoleNameDictionary.ContainsKey(aRole.role_name)) {
                            _RoleNameDictionary[aRole.role_name] = aRole;
                        }
                        else {
                            _RoleNameDictionary.Add(aRole.role_name, aRole);
                        }
                    }
                }
                UpdateSettingPage();
            }
        }

        private void HandleRoleRemovals() {
            //Handle Inbound Role Removal
            if (_RoleRemovalQueue.Count > 0) {
                DebugWrite("DBCOMM: Preparing to lock removal role queue to get new roles", 7);
                Queue<AdKatsRole> inboundRoleRemoval;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_RoleRemovalQueue) {
                    DebugWrite("DBCOMM: Inbound roles found. Grabbing.", 6);
                    //Grab all roles in the queue
                    inboundRoleRemoval = new Queue<AdKatsRole>(_RoleRemovalQueue.ToArray());
                    //Clear the queue for next run
                    _RoleRemovalQueue.Clear();
                }
                //Loop through all commands in order that they came in
                while (inboundRoleRemoval.Count > 0) {
                    AdKatsRole aRole = inboundRoleRemoval.Dequeue();
                    RemoveRole(aRole);
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_RoleIDDictionary) {
                        if (_RoleIDDictionary.ContainsKey(aRole.role_id)) {
                            _RoleIDDictionary.Remove(aRole.role_id);
                        }
                        if (_RoleKeyDictionary.ContainsKey(aRole.role_key)) {
                            _RoleKeyDictionary.Remove(aRole.role_key);
                        }
                        if (_RoleNameDictionary.ContainsKey(aRole.role_name)) {
                            _RoleNameDictionary.Remove(aRole.role_name);
                        }
                    }
                }
                UpdateSettingPage();
            }
        }

        private void HandleUserChanges() {
            //Handle access updates
            if (_UserUploadQueue.Count > 0 || _UserRemovalQueue.Count > 0) {
                DebugWrite("DBCOMM: Inbound access changes found. Grabbing.", 6);
                Queue<AdKatsUser> inboundUserUploads;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_UserUploadQueue) {
                    inboundUserUploads = new Queue<AdKatsUser>(_UserUploadQueue.ToArray());
                    _UserUploadQueue.Clear();
                }
                Queue<AdKatsUser> inboundUserRemoval;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_UserRemovalQueue) {
                    inboundUserRemoval = new Queue<AdKatsUser>(_UserRemovalQueue.ToArray());
                    _UserRemovalQueue.Clear();
                }
                //Loop through all records in order that they came in
                while (inboundUserUploads.Count > 0) {
                    AdKatsUser user = inboundUserUploads.Dequeue();
                    UploadUser(user);
                }
                //Loop through all records in order that they came in
                while (inboundUserRemoval.Count > 0) {
                    AdKatsUser user = inboundUserRemoval.Dequeue();
                    ConsoleWarn("Removing user " + user.user_name);
                    RemoveUser(user);
                }
                FetchAllAccess(true);
            }
            else if (DateTime.UtcNow > _lastUserFetch.AddSeconds(DbUserFetchFrequency) || _userCache.Count == 0) {
                FetchAllAccess(true);
            }
            else {
                DebugWrite("DBCOMM: No inbound user changes.", 7);
            }
        }

        private void HandleActiveBanEnforcer() {
            //Call banlist at set interval (20 seconds)
            if (_UseBanEnforcerPreviousState && (DateTime.UtcNow > _LastBanListCall.AddSeconds(20))) {
                _LastBanListCall = DateTime.UtcNow;
                DebugWrite("banlist.list called at interval.", 6);
                ExecuteCommand("procon.protected.send", "banList.list");
            }

            FetchNameBanCount();
            FetchGUIDBanCount();
            FetchIPBanCount();
            if (!_UseBanEnforcerPreviousState || (DateTime.UtcNow > _LastDbBanFetch.AddSeconds(DbBanFetchFrequency))) {
                //Load all bans on startup
                if (!_UseBanEnforcerPreviousState) {
                    //Get all bans from procon
                    ConsoleWarn("Preparing to queue procon bans for import. Please wait.");
                    _DbCommunicationWaitHandle.Reset();
                    ExecuteCommand("procon.protected.send", "banList.list");
                    _DbCommunicationWaitHandle.WaitOne(TimeSpan.FromMinutes(5));
                    if (_CBanProcessingQueue.Count > 0) {
                        ConsoleWrite(_CBanProcessingQueue.Count + " procon bans queued for import. Import might take several minutes if you have many bans!");
                    }
                    else {
                        ConsoleWrite("No procon bans to import into Ban Enforcer.");
                    }
                }
            }
            else {
                DebugWrite("DBCOMM: Skipping DB ban fetch", 7);
            }

            //Handle Inbound Ban Comms
            if (_BanEnforcerProcessingQueue.Count > 0) {
                DebugWrite("DBCOMM: Preparing to lock inbound ban enforcer queue to retrive new bans", 7);
                Queue<AdKatsBan> inboundBans;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_BanEnforcerProcessingQueue) {
                    DebugWrite("DBCOMM: Inbound bans found. Grabbing.", 6);
                    //Grab all messages in the queue
                    inboundBans = new Queue<AdKatsBan>(_BanEnforcerProcessingQueue.ToArray());
                    //Clear the queue for next run
                    _BanEnforcerProcessingQueue.Clear();
                }
                Int32 index = 1;
                //Loop through all bans in order that they came in
                while (inboundBans.Count > 0) {
                    if (!_pluginEnabled || !_UseBanEnforcer) {
                        ConsoleWarn("Canceling ban import mid-operation.");
                        break;
                    }
                    //Grab the ban
                    AdKatsBan aBan = inboundBans.Dequeue();

                    DebugWrite("DBCOMM: Processing Frostbite Ban: " + index++, 6);

                    //Upload the ban
                    UploadBan(aBan);

                    //Only perform special action when ban is direct
                    //Indirect bans are through the procon banlist, so the player has already been kicked
                    if (aBan.ban_record.source_name != "BanEnforcer") {
                        //Enforce the ban
                        EnforceBan(aBan, false);
                    }
                }
            }

            //Handle BF3 Ban Manager imports
            if (!_UseBanEnforcerPreviousState) {
                //Import all bans from BF3 Ban Manager
                ImportBansFromBBM5108();
            }

            //Handle Inbound CBan Uploads
            if (_CBanProcessingQueue.Count > 0) {
                if (!_UseBanEnforcerPreviousState) {
                    ConsoleWarn("Do not disable AdKats or change any settings until upload is complete!");
                }
                DebugWrite("DBCOMM: Preparing to lock inbound cBan queue to retrive new cBans", 7);
                Double totalCBans = 0;
                Double bansImported = 0;
                Boolean earlyExit = false;
                DateTime startTime = DateTime.UtcNow;
                Queue<CBanInfo> inboundCBans;
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_CBanProcessingQueue) {
                    DebugWrite("DBCOMM: Inbound cBans found. Grabbing.", 6);
                    //Grab all cBans in the queue
                    inboundCBans = new Queue<CBanInfo>(_CBanProcessingQueue.ToArray());
                    totalCBans = inboundCBans.Count;
                    //Clear the queue for next run
                    _CBanProcessingQueue.Clear();
                }
                //Loop through all cBans in order that they came in
                Boolean bansFound = false;
                while (inboundCBans.Count > 0) {
                    //Break from the loop if the plugin is disabled or the setting is reverted.
                    if (!_pluginEnabled || !_UseBanEnforcer) {
                        ConsoleWarn("You exited the ban upload process early, the process was not completed.");
                        earlyExit = true;
                        break;
                    }

                    bansFound = true;

                    CBanInfo cBan = inboundCBans.Dequeue();

                    //Create the record
                    var record = new AdKatsRecord();
                    record.record_source = AdKatsRecord.Sources.InternalAutomated;
                    //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                    switch (cBan.BanLength.Subset) {
                        case TimeoutSubset.TimeoutSubsetType.Seconds:
                            record.command_type = _CommandKeyDictionary["player_ban_temp"];
                            record.command_action = _CommandKeyDictionary["player_ban_temp"];
                            record.command_numeric = cBan.BanLength.Seconds / 60;
                            break;
                        case TimeoutSubset.TimeoutSubsetType.Permanent:
                            record.command_type = _CommandKeyDictionary["player_ban_perm"];
                            record.command_action = _CommandKeyDictionary["player_ban_perm"];
                            record.command_numeric = 0;
                            break;
                        case TimeoutSubset.TimeoutSubsetType.Round:
                            //Accept round ban as 1 hour timeban
                            record.command_type = _CommandKeyDictionary["player_ban_temp"];
                            record.command_action = _CommandKeyDictionary["player_ban_temp"];
                            record.command_numeric = 60;
                            break;
                        default:
                            //Ban type is unknown, unable to process
                            continue;
                    }
                    record.source_name = _CBanAdminName;
                    record.server_id = _serverID;
                    record.target_player = FetchPlayer(true, false, null, -1, cBan.SoldierName, (!String.IsNullOrEmpty(cBan.Guid)) ? (cBan.Guid.ToUpper()) : (null), cBan.IpAddress);
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
                    var aBan = new AdKatsBan {
                        ban_record = record,
                        ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable) || !String.IsNullOrEmpty(cBan.SoldierName)),
                        ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable) || !String.IsNullOrEmpty(cBan.Guid)),
                        ban_enforceIP = ipAvailable && (_DefaultEnforceIP || (!nameAvailable && !guidAvailable) || !String.IsNullOrEmpty(cBan.IpAddress))
                    };
                    if (!aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP) {
                        ConsoleError("Unable to create ban, no proper player information");
                        continue;
                    }

                    //Upload the ban
                    DebugWrite("Uploading ban from procon.", 5);
                    UploadBan(aBan);

                    if (!_UseBanEnforcerPreviousState && (++bansImported % 25 == 0)) {
                        ConsoleWrite(Math.Round(100 * bansImported / totalCBans, 2) + "% of bans uploaded. AVG " + Math.Round(bansImported / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " uploads/sec.");
                    }
                }
                if (bansFound && !earlyExit) {
                    //If all bans have been queued for processing, clear the ban list
                    ExecuteCommand("procon.protected.send", "banList.clear");
                    ExecuteCommand("procon.protected.send", "banList.save");
                    ExecuteCommand("procon.protected.send", "banList.list");
                    if (!_UseBanEnforcerPreviousState) {
                        ConsoleSuccess("All bans uploaded into AdKats database.");
                    }
                }
            }
            _UseBanEnforcerPreviousState = true;
        }

        private Boolean ConnectionCapable() {
            if (!string.IsNullOrEmpty(_mySqlDatabaseName) && !string.IsNullOrEmpty(_mySqlHostname) && !string.IsNullOrEmpty(_mySqlPassword) && !string.IsNullOrEmpty(_mySqlPort) && !string.IsNullOrEmpty(_mySqlUsername)) {
                DebugWrite("MySql Connection capable. All variables in place.", 7);
                return true;
            }
            return false;
        }

        private MySqlConnection GetDatabaseConnection() {
            if (ConnectionCapable()) {
                var conn = new MySqlConnection(PrepareMySqlConnectionString());
                conn.Open();
                return conn;
            }
            ConsoleError("Attempted to connect to database without all variables in place.");
            return null;
        }

        private Boolean TestDatabaseConnection() {
            Boolean databaseValid = false;
            DebugWrite("testDatabaseConnection starting!", 6);
            if (ConnectionCapable()) {
                Boolean success = false;
                Int32 attempt = 0;
                do {
                    if (!_pluginEnabled) {
                        return false;
                    }
                    attempt++;
                    try {
                        UpdateMySqlConnectionStringBuilder();
                        //Prepare the connection String and create the connection object
                        using (MySqlConnection connection = GetDatabaseConnection()) {
                            ConsoleWrite("Attempting database connection. Attempt " + attempt + " of 5.");
                            //Attempt a ping through the connection
                            if (connection.Ping()) {
                                //Connection good
                                ConsoleSuccess("Database connection open.");
                                success = true;
                            }
                            else {
                                //Connection poor
                                ConsoleError("Database connection FAILED ping test.");
                            }
                        } //databaseConnection gets closed here
                        if (success) {
                            //Make sure database structure is good
                            if (ConfirmDatabaseSetup()) {
                                //Confirm the database is valid
                                databaseValid = true;
                                //clear setting change monitor
                                _dbSettingsChanged = false;
                            }
                            else {
                                Disable();
                                break;
                            }
                        }
                    }
                    catch (Exception e) {
                        //Only perform retries if the error was a timeout
                        if (e.ToString().Contains("Unable to connect")) {
                            ConsoleError("Connection failed on attempt " + attempt + ". " + ((attempt <= 5) ? ("Retrying in 5 seconds. ") : ("")));
                            Thread.Sleep(5000);
                        }
                        else {
                            break;
                        }
                    }
                } while (!success && attempt < 5);
                if (!success) {
                    //Invalid credentials or no connection to database
                    ConsoleError("Database connection FAILED with EXCEPTION. Bad credentials, invalid hostname, or invalid port.");
                    Disable();
                }
            }
            else {
                ConsoleError("Not DB connection capable yet, complete SQL connection variables.");
            }
            DebugWrite("testDatabaseConnection finished!", 6);

            return databaseValid;
        }

        private Boolean ConfirmDatabaseSetup() {
            DebugWrite("Confirming Database Structure.", 3);
            try {
                if (!ConfirmStatLoggerTables()) {
                    ConsoleError("Tables from XPKiller's Stat Logger not present in the database. Enable that plugin then re-run AdKats!");
                    return false;
                }
                if (!ConfirmAdKatsTables()) {
                    ConsoleError("AdKats tables not present in the database. For this release the adkats database setup script must be run manually. Run the script then restart AdKats.");
                    return false;
                }
                ConsoleSuccess("Database confirmed functional for AdKats use.");
                return true;
            }
            catch (Exception e) {
                ConsoleError("ERROR in helper_confirmDatabaseSetup: " + e);
                return false;
            }
        }

        private void runDBSetupScript() {
            try {
                ConsoleWrite("Running database setup script. You will not lose any data.");
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        var downloader = new WebClient();
                        //Set the insert command structure
                        command.CommandText = downloader.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/adkats.sql");
                        try {
                            //Attempt to execute the query
                            Int32 rowsAffected = command.ExecuteNonQuery();
                            ConsoleWrite("Setup script successful, your database is now prepared for use by AdKats " + GetPluginVersion());
                        }
                        catch (Exception e) {
                            ConsoleError("Your database did not accept the script. Does your account have access to table creation? AdKats will not function properly. Exception: " + e);
                        }
                    }
                }
            }
            catch (Exception e) {
                ConsoleError("ERROR when setting up DB, you might not have connection to github: " + e);
            }
        }

        private Boolean ConfirmAdKatsTables() {
            if (!ConfirmTable("adkats_specialplayers")) {
                ConsoleWarn("Special players table not found. Attempting to add.");
                SendNonQuery("Adding special soldiers table", @"CREATE TABLE IF NOT EXISTS `adkats_specialplayers`( 
                      `specialplayer_id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
                      `player_group` varchar(30) COLLATE utf8_unicode_ci NOT NULL,
                      `player_id` int(10) UNSIGNED DEFAULT NULL,
                      `player_game` tinyint(4) UNSIGNED DEFAULT NULL,
                      `player_server` smallint(5) UNSIGNED DEFAULT NULL,
                      `player_identifier` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL,
                      PRIMARY KEY (`specialplayer_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='AdKats - Special Player List'", true);
                SendNonQuery("Adding special soldiers table foreign keys", @"ALTER TABLE `adkats_specialplayers`
                          ADD CONSTRAINT `adkats_specialplayers_game_id` FOREIGN KEY (`player_game`) REFERENCES `tbl_games`(`GameID`) ON UPDATE NO ACTION ON DELETE CASCADE, 
                          ADD CONSTRAINT `adkats_specialplayers_server_id` FOREIGN KEY (`player_server`) REFERENCES `tbl_server`(`ServerID`) ON UPDATE NO ACTION ON DELETE CASCADE, 
                          ADD CONSTRAINT `adkats_specialplayers_player_id` FOREIGN KEY (`player_id`) REFERENCES `tbl_playerdata`(`PlayerID`) ON UPDATE NO ACTION ON DELETE CASCADE", true);
            }
            return ConfirmTable("adkats_bans") && ConfirmTable("adkats_commands") && ConfirmTable("adkats_infractions_global") && ConfirmTable("adkats_infractions_server") && ConfirmTable("adkats_records_debug") && ConfirmTable("adkats_records_main") && ConfirmTable("adkats_rolecommands") && ConfirmTable("adkats_roles") && ConfirmTable("adkats_settings") && ConfirmTable("adkats_users") && ConfirmTable("adkats_usersoldiers") && ConfirmTable("adkats_specialplayers");
        }

        private Boolean ConfirmStatLoggerTables() {
            Boolean confirmed = true;
            //All versions of stat logger should have these tables
            if (ConfirmTable("tbl_playerdata") && ConfirmTable("tbl_server") && ConfirmTable("tbl_chatlog")) {
                //The universal version has a tbl_games table, detect that
                if (ConfirmTable("tbl_games")) {
                    _statLoggerVersion = "UNIVERSAL";
                    Boolean gameIDFound = false;
                    using (MySqlConnection connection = GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            //Attempt to execute the query
                            command.CommandText = @"
                            SELECT 
                                `GameID` AS `game_id`, 
                                `Name` AS `game_name` 
                            FROM 
                                `tbl_games`";
                            using (MySqlDataReader reader = command.ExecuteReader()) {
                                if (_isTestingAuthorized)
                                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                lock (_gameIDDictionary) {
                                    while (reader.Read()) {
                                        String gameName = reader.GetString("game_name");
                                        Int32 gameID = reader.GetInt32("game_id");
                                        if (!_gameIDDictionary.ContainsKey(gameID)) {
                                            if (_gameVersion.ToString() == gameName) {
                                                _gameID = gameID;
                                                gameIDFound = true;
                                            }
                                            switch (gameName) {
                                                case "BF3":
                                                    _gameIDDictionary.Add(gameID, GameVersion.BF3);
                                                    break;
                                                case "BF4":
                                                    _gameIDDictionary.Add(gameID, GameVersion.BF4);
                                                    break;
                                                default:
                                                    ConsoleError("Game name " + gameName + " not recognized.");
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        confirmed = gameIDFound;
                    }
                }
            }
            else {
                confirmed = false;
            }
            return confirmed;
        }

        private void UpdateDatabase37014000() {
            try {
                //Check if old database table exists
                if (!ConfirmTable("adkats_records")) {
                    DebugWrite("Not updating database, no old tables to pull from.", 1);
                    return;
                }
                //If the new main record table contains records return without handling
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT
	                        *
                        FROM
	                        `adkats_records_main`
                        LIMIT 1";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                ConsoleWarn("Not updating database, new records already found.");
                                return;
                            }
                        }
                    }
                }
                ConsoleWarn("Updating database information from version 3.7 spec to 4.0 spec. DO NOT DISABLE ADKATS!");
                ConsoleWrite("Updating Users.");
                //Add new users for every player in the access list
                var oldUsers = new List<AdKatsUser>();
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT
	                        `player_name`,
	                        `access_level`
                        FROM
	                        `adkats_accesslist`
                        ORDER BY
                            `access_level`
                        ASC";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                //Set all users to default guest role
                                oldUsers.Add(new AdKatsUser {
                                    user_name = reader.GetString("player_name"),
                                    user_role = _RoleKeyDictionary["guest_default"]
                                });
                            }
                        }
                    }
                }
                foreach (AdKatsUser aUser in oldUsers) {
                    ConsoleWrite("Processing user " + aUser.user_name);
                    //Attempt to add soldiers matching the user's names
                    TryAddUserSoldier(aUser, aUser.user_name);
                    UploadUser(aUser);
                }
                ConsoleSuccess(oldUsers.Count + " old users fetched and updated to new spec.");
                ConsoleWarn("Updating Records...");
                //Generate old->new command key dictionary
                var commandConversionDictionary = new Dictionary<string, AdKatsCommand>();
                commandConversionDictionary.Add("AdminSay", _CommandKeyDictionary["admin_say"]);
                commandConversionDictionary.Add("AdminTell", _CommandKeyDictionary["admin_tell"]);
                commandConversionDictionary.Add("AdminYell", _CommandKeyDictionary["admin_yell"]);
                commandConversionDictionary.Add("CallAdmin", _CommandKeyDictionary["player_calladmin"]);
                commandConversionDictionary.Add("ConfirmReport", _CommandKeyDictionary["player_report_confirm"]);
                commandConversionDictionary.Add("EndLevel", _CommandKeyDictionary["round_end"]);
                commandConversionDictionary.Add("EnforceBan", _CommandKeyDictionary["banenforcer_enforce"]);
                commandConversionDictionary.Add("Exception", _CommandKeyDictionary["adkats_exception"]);
                commandConversionDictionary.Add("ForceMove", _CommandKeyDictionary["player_fmove"]);
                commandConversionDictionary.Add("Forgive", _CommandKeyDictionary["player_forgive"]);
                commandConversionDictionary.Add("Join", _CommandKeyDictionary["player_join"]);
                commandConversionDictionary.Add("Kick", _CommandKeyDictionary["player_kick"]);
                commandConversionDictionary.Add("Kill", _CommandKeyDictionary["player_kill"]);
                commandConversionDictionary.Add("KickAll", _CommandKeyDictionary["server_kickall"]);
                commandConversionDictionary.Add("LowPopKill", _CommandKeyDictionary["player_kill_lowpop"]);
                commandConversionDictionary.Add("Move", _CommandKeyDictionary["player_move"]);
                commandConversionDictionary.Add("Mute", _CommandKeyDictionary["player_mute"]);
                commandConversionDictionary.Add("NextLevel", _CommandKeyDictionary["round_next"]);
                commandConversionDictionary.Add("Nuke", _CommandKeyDictionary["server_nuke"]);
                commandConversionDictionary.Add("PermaBan", _CommandKeyDictionary["player_ban_perm"]);
                commandConversionDictionary.Add("PlayerSay", _CommandKeyDictionary["player_say"]);
                commandConversionDictionary.Add("PlayerYell", _CommandKeyDictionary["player_yell"]);
                commandConversionDictionary.Add("PlayerTell", _CommandKeyDictionary["player_tell"]);
                commandConversionDictionary.Add("Punish", _CommandKeyDictionary["player_punish"]);
                commandConversionDictionary.Add("RepeatKill", _CommandKeyDictionary["player_kill_repeat"]);
                commandConversionDictionary.Add("Report", _CommandKeyDictionary["player_report"]);
                commandConversionDictionary.Add("RestartLevel", _CommandKeyDictionary["round_restart"]);
                commandConversionDictionary.Add("RequestRules", _CommandKeyDictionary["self_rules"]);
                commandConversionDictionary.Add("RoundWhitelist", _CommandKeyDictionary["player_roundwhitelist"]);
                commandConversionDictionary.Add("KillSelf", _CommandKeyDictionary["self_kill"]);
                commandConversionDictionary.Add("Teamswap", _CommandKeyDictionary["self_teamswap"]);
                commandConversionDictionary.Add("TempBan", _CommandKeyDictionary["player_ban_temp"]);
                ConsoleWrite("Updating Bans...");
                //Download all bans
                Double totalBans = 0;
                Double bansDownloaded = 0;
                Double bansUpdated = 0;
                DateTime startTime = DateTime.UtcNow;
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            COUNT(*) AS `ban_count`
                        FROM 
	                        `adkats_banlist`";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                totalBans = reader.GetInt64("ban_count");
                            }
                        }
                    }
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
	                        `adkats_records`.`record_id`,
	                        `adkats_records`.`server_id`,
	                        `adkats_records`.`command_type`,
	                        `adkats_records`.`command_action`,
	                        `adkats_records`.`command_numeric`,
	                        `adkats_records`.`target_name`,
	                        `adkats_records`.`target_id`,
	                        `adkats_records`.`source_name`,
	                        `adkats_records`.`source_id`,
	                        `adkats_records`.`record_message`,
	                        `adkats_records`.`record_time`,
	                        `adkats_records`.`adkats_read`,
	                        `adkats_records`.`adkats_web`,
	                        `adkats_banlist`.`ban_id`, 
	                        `adkats_banlist`.`player_id`, 
	                        `adkats_banlist`.`latest_record_id`, 
	                        `adkats_banlist`.`ban_status`, 
	                        `adkats_banlist`.`ban_notes`, 
	                        `adkats_banlist`.`ban_sync`, 
	                        `adkats_banlist`.`ban_startTime`, 
	                        `adkats_banlist`.`ban_endTime`, 
	                        `adkats_banlist`.`ban_enforceName`, 
	                        `adkats_banlist`.`ban_enforceGUID`, 
	                        `adkats_banlist`.`ban_enforceIP`
                        FROM 
	                        `adkats_banlist`
                        INNER JOIN
	                        `adkats_records`
                        ON
	                        `adkats_banlist`.`latest_record_id` = `adkats_records`.`record_id`";

                        var importedBans = new List<AdKatsBan>();
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            //Loop through all incoming bans
                            while (reader.Read()) {
                                //Create the ban element
                                var aBan = new AdKatsBan {
                                    ban_id = reader.GetInt64("ban_id"),
                                    ban_status = reader.GetString("ban_status"),
                                    ban_notes = reader.GetString("ban_notes"),
                                    ban_sync = reader.GetString("ban_sync"),
                                    ban_startTime = reader.GetDateTime("ban_startTime"),
                                    ban_endTime = reader.GetDateTime("ban_endTime"),
                                    ban_enforceName = (reader.GetString("ban_enforceName") == "Y"),
                                    ban_enforceGUID = (reader.GetString("ban_enforceGUID") == "Y"),
                                    ban_enforceIP = (reader.GetString("ban_enforceIP") == "Y")
                                };
                                var aRecord = new AdKatsRecord();
                                aRecord.record_source = AdKatsRecord.Sources.InternalAutomated;
                                aRecord.server_id = reader.GetInt64("server_id");
                                AdKatsCommand aCommandType = null;
                                if (commandConversionDictionary.TryGetValue(reader.GetString("command_type"), out aCommandType)) {
                                    aRecord.command_type = aCommandType;
                                }
                                else {
                                    //Skip record
                                    //ConsoleError("Unable to parse '" + reader.GetString("command_type") + "' as a valid command type. Skipping ban record.");
                                    continue;
                                }
                                AdKatsCommand aCommandAction = null;
                                if (commandConversionDictionary.TryGetValue(reader.GetString("command_action"), out aCommandAction)) {
                                    aRecord.command_action = aCommandAction;
                                }
                                else {
                                    //Skip record
                                    //ConsoleError("Unable to parse '" + reader.GetString("command_action") + "' as a valid command action. Skipping ban record.");
                                    continue;
                                }
                                aRecord.command_numeric = reader.GetInt32("command_numeric");
                                aRecord.record_message = reader.GetString("record_message");
                                aRecord.target_name = reader.GetString("target_name");
                                if (!reader.IsDBNull(6)) {
                                    Int32 playerID = reader.GetInt32(6);
                                    aRecord.target_player = new AdKatsPlayer {
                                        player_id = playerID
                                    };
                                }
                                aRecord.source_name = reader.GetString("source_name");
                                if (!reader.IsDBNull(8)) {
                                    Int32 playerID = reader.GetInt32(8);
                                    aRecord.source_player = new AdKatsPlayer {
                                        player_id = playerID
                                    };
                                }
                                aRecord.record_time = reader.GetDateTime("record_time");
                                aBan.ban_record = aRecord;
                                importedBans.Add(aBan);

                                if (++bansDownloaded % 100 == 0) {
                                    ConsoleWrite(Math.Round(100 * bansDownloaded / totalBans, 2) + "% of bans downloaded. AVG " + Math.Round(bansDownloaded / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " downloads/sec.");
                                }
                            }
                        }
                        if (importedBans.Count > 0) {
                            ConsoleWarn(importedBans.Count + " bans downloaded, beginning update to 4.0 spec.");
                        }
                        startTime = DateTime.UtcNow;
                        //Upload all of those bans to the new database
                        foreach (AdKatsBan aBan in importedBans) {
                            UploadBan(aBan);
                            if (++bansUpdated % 15 == 0) {
                                ConsoleWrite(Math.Round(100 * bansUpdated / totalBans, 2) + "% of bans updated. AVG " + Math.Round(bansUpdated / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " updates/sec.");
                            }
                        }
                        ConsoleSuccess("All AdKats Enforced bans updated to 4.0 spec.");
                    }
                }
                //Import all records that do not have command action TempBan or PermaBan
                Double recordsDownloaded = 0;
                Double recordsProcessed = 0;
                var oldRecords = new List<AdKatsRecord>();
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
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
	                        `record_time`,
	                        `adkats_read`,
	                        `adkats_web`
                        FROM
	                        `adkats_records`
                        WHERE
                            `command_action` <> 'Exception'
                        ORDER BY
	                        `record_id`
                        ASC";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                var aRecord = new AdKatsRecord();
                                aRecord.record_source = AdKatsRecord.Sources.InternalAutomated;
                                aRecord.server_id = reader.GetInt64("server_id");
                                AdKatsCommand aCommandType = null;
                                if (commandConversionDictionary.TryGetValue(reader.GetString("command_type"), out aCommandType)) {
                                    aRecord.command_type = aCommandType;
                                }
                                else {
                                    //Skip record
                                    //ConsoleError("Unable to parse '" + reader.GetString("command_type") + "' as a valid command type. Skipping record.");
                                    continue;
                                }
                                AdKatsCommand aCommandAction = null;
                                if (commandConversionDictionary.TryGetValue(reader.GetString("command_action"), out aCommandAction)) {
                                    aRecord.command_action = aCommandAction;
                                }
                                else {
                                    //Skip record
                                    //ConsoleError("Unable to parse '" + reader.GetString("command_action") + "' as a valid command action. Cancelling database update.");
                                    return;
                                }
                                aRecord.command_numeric = reader.GetInt32("command_numeric");
                                aRecord.record_message = reader.GetString("record_message");
                                aRecord.target_name = reader.GetString("target_name");
                                if (!reader.IsDBNull(6)) {
                                    Int32 playerID = reader.GetInt32(6);
                                    aRecord.target_player = new AdKatsPlayer {
                                        player_id = playerID
                                    };
                                }
                                aRecord.source_name = reader.GetString("source_name");
                                if (!reader.IsDBNull(8)) {
                                    Int32 playerID = reader.GetInt32(8);
                                    aRecord.source_player = new AdKatsPlayer {
                                        player_id = playerID
                                    };
                                }
                                aRecord.record_time = reader.GetDateTime("record_time");
                                //Set all users to default guest role
                                oldRecords.Add(aRecord);
                                if (++recordsDownloaded % 5000 == 0) {
                                    ConsoleWrite(recordsDownloaded + " records downloaded for processing.");
                                }
                            }
                        }
                    }
                }
                ConsoleSuccess("All records prepared for update.");
                //Upload all of those records to the new database spec
                ConsoleWarn("Updating all prepared records...");
                foreach (AdKatsRecord aRecord in oldRecords) {
                    //Attempt to upload the record
                    UploadRecord(aRecord);
                    if (++recordsProcessed % 50 == 0) {
                        ConsoleWrite(Math.Round(100 * recordsProcessed / recordsDownloaded, 3) + "% of records updated into 4.0 spec.");
                    }
                }
                ConsoleSuccess("All records updated to 4.0 spec.");
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating database information from 3.7 spec to 4.0 spec.", e));
            }
        }

        private Boolean ConfirmTable(String tablename) {
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '" + _mySqlDatabaseName + "' AND TABLE_NAME= '" + tablename + "'";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            return reader.Read();
                        }
                    }
                }
            }
            catch (Exception e) {
                ConsoleError("ERROR in ConfirmTable: " + e);
                return false;
            }
        }

        private void UpdateMySqlConnectionStringBuilder() {
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (_dbCommStringBuilder) {
                UInt32 uintport = 3306;
                UInt32.TryParse(_mySqlPort, out uintport);
                //Add connection variables
                _dbCommStringBuilder.Port = uintport;
                _dbCommStringBuilder.Server = _mySqlHostname;
                _dbCommStringBuilder.UserID = _mySqlUsername;
                _dbCommStringBuilder.Password = _mySqlPassword;
                _dbCommStringBuilder.Database = _mySqlDatabaseName;
                //Set up connection pooling
                if (UseConnectionPooling) {
                    _dbCommStringBuilder.Pooling = true;
                    _dbCommStringBuilder.MinimumPoolSize = Convert.ToUInt32(MinConnectionPoolSize);
                    _dbCommStringBuilder.MaximumPoolSize = Convert.ToUInt32(MaxConnectionPoolSize);
                    _dbCommStringBuilder.ConnectionLifeTime = 600;
                }
                else {
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

        private String PrepareMySqlConnectionString() {
            return _dbCommStringBuilder.ConnectionString;
        }

        private void UploadAllSettings() {
            if (!_pluginEnabled) {
                return;
            }
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                DebugWrite("uploadAllSettings starting!", 6);
                QueueSettingForUpload(new CPluginVariable(@"Auto-Enable/Keep-Alive", typeof (Boolean), _useKeepAlive));
                QueueSettingForUpload(new CPluginVariable(@"Debug level", typeof (int), _DebugLevel));
                QueueSettingForUpload(new CPluginVariable(@"Debug Soldier Name", typeof (String), _DebugSoldierName));
                QueueSettingForUpload(new CPluginVariable(@"Server VOIP Address", typeof (String), _ServerVoipAddress));
                QueueSettingForUpload(new CPluginVariable(@"Rule Print Delay", typeof (Double), _ServerRulesDelay));
                QueueSettingForUpload(new CPluginVariable(@"Rule Print Interval", typeof (Double), _ServerRulesInterval));
                QueueSettingForUpload(new CPluginVariable(@"Server Rule List", typeof (String), CPluginVariable.EncodeStringArray(_ServerRulesList)));
                QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Whitelist", typeof (Boolean), _FeedMultiBalancerWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Even Dispersion List", typeof (Boolean), _FeedMultiBalancerDisperseList));
                QueueSettingForUpload(new CPluginVariable(@"Feed Server Reserved Slots", typeof (Boolean), _FeedServerReservedSlots));
                QueueSettingForUpload(new CPluginVariable(@"Feed Server Spectator List", typeof (Boolean), _FeedServerSpectatorList));
                QueueSettingForUpload(new CPluginVariable(@"Feed Stat Logger Settings", typeof (Boolean), _FeedStatLoggerSettings));
                QueueSettingForUpload(new CPluginVariable(@"Round Timer: Enable", typeof (Boolean), _useRoundTimer));
                QueueSettingForUpload(new CPluginVariable(@"Round Timer: Round Duration Minutes", typeof (Double), _roundTimeMinutes));
                QueueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof (Boolean), _UseWeaponLimiter));
                QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Weapon String", typeof (String), _WeaponLimiterString));
                QueueSettingForUpload(new CPluginVariable(@"NO EXPLOSIVES Exception String", typeof (String), _WeaponLimiterExceptionString));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Report-Handler Strings", typeof (String), CPluginVariable.EncodeStringArray(_AutoReportHandleStrings)));
                QueueSettingForUpload(new CPluginVariable(@"Use Grenade Cook Catcher", typeof (Boolean), _UseGrenadeCookCatcher));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: Enable", typeof (Boolean), _UseHackerChecker));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Enable", typeof (Boolean), _UseDpsChecker));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Trigger Level", typeof (Double), _DpsTriggerLevel));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Ban Message", typeof (String), _HackerCheckerDPSBanMessage));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Enable", typeof (Boolean), _UseHskChecker));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Trigger Level", typeof (Double), _HskTriggerLevel));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Ban Message", typeof (String), _HackerCheckerHSKBanMessage));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: KPM Checker: Enable", typeof (Boolean), _UseKpmChecker));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: KPM Checker: Trigger Level", typeof (Double), _KpmTriggerLevel));
                QueueSettingForUpload(new CPluginVariable(@"HackerChecker: KPM Checker: Ban Message", typeof (String), _HackerCheckerKPMBanMessage));
                QueueSettingForUpload(new CPluginVariable(@"External Access Key", typeof (String), _ExternalCommandAccessKey));
                QueueSettingForUpload(new CPluginVariable(@"Fetch Actions from Database", typeof (Boolean), _fetchActionsFromDb));
                QueueSettingForUpload(new CPluginVariable(@"Use Additional Ban Message", typeof (Boolean), _UseBanAppend));
                QueueSettingForUpload(new CPluginVariable(@"Additional Ban Message", typeof (String), _BanAppend));
                QueueSettingForUpload(new CPluginVariable(@"Procon Ban Admin Name", typeof (String), _CBanAdminName));
                QueueSettingForUpload(new CPluginVariable(@"Use Ban Enforcer", typeof (Boolean), _UseBanEnforcer));
                QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by NAME", typeof (Boolean), _DefaultEnforceName));
                QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by GUID", typeof (Boolean), _DefaultEnforceGUID));
                QueueSettingForUpload(new CPluginVariable(@"Enforce New Bans by IP", typeof (Boolean), _DefaultEnforceIP));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Required Reason Length", typeof (Int32), _RequiredReasonLength));
                QueueSettingForUpload(new CPluginVariable(@"Punishment Hierarchy", typeof (String), CPluginVariable.EncodeStringArray(_PunishmentHierarchy)));
                QueueSettingForUpload(new CPluginVariable(@"Combine Server Punishments", typeof (Boolean), _CombineServerPunishments));
                QueueSettingForUpload(new CPluginVariable(@"Only Kill Players when Server in low population", typeof (Boolean), _OnlyKillOnLowPop));
                QueueSettingForUpload(new CPluginVariable(@"Low Population Value", typeof (Int32), _LowPopPlayerCount));
                QueueSettingForUpload(new CPluginVariable(@"Use IRO Punishment", typeof (Boolean), _IROActive));
                QueueSettingForUpload(new CPluginVariable(@"IRO Punishment Overrides Low Pop", typeof (Boolean), _IROOverridesLowPop));
                QueueSettingForUpload(new CPluginVariable(@"IRO Timeout (Minutes)", typeof (Int32), _IROTimeout));
                QueueSettingForUpload(new CPluginVariable(@"Send Emails", typeof (Boolean), _UseEmail));
                QueueSettingForUpload(new CPluginVariable(@"Use SSL?", typeof (Boolean), _EmailHandler.UseSSL));
                QueueSettingForUpload(new CPluginVariable(@"SMTP-Server address", typeof (String), _EmailHandler.SMTPServer));
                QueueSettingForUpload(new CPluginVariable(@"SMTP-Server port", typeof (Int32), _EmailHandler.SMTPPort));
                QueueSettingForUpload(new CPluginVariable(@"Sender address", typeof (String), _EmailHandler.SenderEmail));
                QueueSettingForUpload(new CPluginVariable(@"SMTP-Server username", typeof (String), _EmailHandler.SMTPUser));
                QueueSettingForUpload(new CPluginVariable(@"SMTP-Server password", typeof (String), _EmailHandler.SMTPPassword));
                QueueSettingForUpload(new CPluginVariable(@"Custom HTML Addition", typeof (String), _EmailHandler.CustomHTMLAddition));
                QueueSettingForUpload(new CPluginVariable(@"Extra Recipient Email Addresses", typeof (String[]), _EmailHandler.RecipientEmails.ToArray()));
                QueueSettingForUpload(new CPluginVariable(@"On-Player-Muted Message", typeof (String), _MutedPlayerMuteMessage));
                QueueSettingForUpload(new CPluginVariable(@"On-Player-Killed Message", typeof (String), _MutedPlayerKillMessage));
                QueueSettingForUpload(new CPluginVariable(@"On-Player-Kicked Message", typeof (String), _MutedPlayerKickMessage));
                QueueSettingForUpload(new CPluginVariable(@"# Chances to give player before kicking", typeof (Int32), _MutedPlayerChances));
                QueueSettingForUpload(new CPluginVariable(@"Auto-Whitelist Count", typeof (Int32), _PlayersToAutoWhitelist));
                QueueSettingForUpload(new CPluginVariable(@"Ticket Window High", typeof (Int32), _TeamSwapTicketWindowHigh));
                QueueSettingForUpload(new CPluginVariable(@"Ticket Window Low", typeof (Int32), _TeamSwapTicketWindowLow));
                QueueSettingForUpload(new CPluginVariable(@"Enable Admin Assistant Perk", typeof (Boolean), _EnableAdminAssistantPerk));
                QueueSettingForUpload(new CPluginVariable(@"Minimum Confirmed Reports Per Month", typeof (Int32), _MinimumRequiredMonthlyReports));
                QueueSettingForUpload(new CPluginVariable(@"Yell display time seconds", typeof (Int32), _YellDuration));
                QueueSettingForUpload(new CPluginVariable(@"Pre-Message List", typeof (String), CPluginVariable.EncodeStringArray(_PreMessageList.ToArray())));
                QueueSettingForUpload(new CPluginVariable(@"Require Use of Pre-Messages", typeof (Boolean), _RequirePreMessageUse));
                QueueSettingForUpload(new CPluginVariable(@"Display Admin Name in Kick and Ban Announcement", typeof (Boolean), _ShowAdminNameInSay));
                DebugWrite("uploadAllSettings finished!", 6);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing all settings for upload.", e));
            }
        }

        private void UploadSetting(CPluginVariable var) {
            DebugWrite("uploadSetting starting!", 7);
            //Make sure database connection active
            if (HandlePossibleDisconnect() || !_settingsFetched) {
                return;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Check for length too great
                        if (var.Value.Length > 1499) {
                            ConsoleError("Unable to upload setting, length of setting too great. Really dude? It's 1500+ chars. This is battlefield, not a book club.");
                            return;
                        }
                        DebugWrite(var.Value, 7);
                        //Set the insert command structure
                        command.CommandText = @"
                        INSERT INTO `" + _mySqlDatabaseName + @"`.`adkats_settings` 
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
                            `setting_value` = @setting_value";

                        command.Parameters.AddWithValue("@server_id", _serverID);
                        command.Parameters.AddWithValue("@setting_name", var.Name);
                        command.Parameters.AddWithValue("@setting_type", var.Type);
                        command.Parameters.AddWithValue("@setting_value", var.Value);
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0) {
                            DebugWrite("Setting " + var.Name + " pushed to database", 7);
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while uploading setting to database.", e));
            }

            DebugWrite("uploadSetting finished!", 7);
        }

        private void FetchSettings(long serverID, Boolean verbose) {
            DebugWrite("fetchSettings starting!", 6);
            Boolean success = false;
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                //Success fetching settings
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        String sql = @"
                        SELECT  
                            `setting_name`, 
                            `setting_type`, 
                            `setting_value`
                        FROM 
                            `" + _mySqlDatabaseName + @"`.`adkats_settings` 
                        WHERE 
                            `server_id` = " + serverID;
                        command.CommandText = sql;
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            //Grab the settings
                            while (reader.Read()) {
                                success = true;
                                //Create as variable in case needed later
                                var var = new CPluginVariable(reader.GetString("setting_name"), reader.GetString("setting_type"), reader.GetString("setting_value"));
                                SetPluginVariable(var.Name, var.Value);
                            }
                            if (success) {
                                _lastDbSettingFetch = DateTime.UtcNow;
                                UpdateSettingPage();
                            }
                            else {
                                if (serverID == _serverID) {
                                    UploadAllSettings();
                                }
                                else if (verbose) {
                                    ConsoleError("Settings could not be loaded. Server " + serverID + " invalid.");
                                }
                            }
                            _settingsFetched = true;
                            _settingImportID = _serverID;
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching settings from database.", e));
            }
            DebugWrite("fetchSettings finished!", 6);
        }

        private void UploadCommand(AdKatsCommand command) {
            DebugWrite("uploadCommand starting!", 6);

            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand sqlcommand = connection.CreateCommand()) {
                        //Set the insert command structure
                        sqlcommand.CommandText = @"
                        INSERT INTO 
                        `" + _mySqlDatabaseName + @"`.`adkats_commands` 
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
                HandleException(new AdKatsException("Unexpected error uploading command.", e));
            }
        }

        private List<AdKatsPlayer> FetchAdminSoldiers() {
            var adminSoldiers = new List<AdKatsPlayer>();
            //Loop over the user list
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (_userCache) {
                foreach (AdKatsUser user in _userCache.Values.Where(user => RoleIsInteractionAble(user.user_role))) {
                    adminSoldiers.AddRange(user.soldierDictionary.Values);
                }
            }
            return adminSoldiers;
        }

        private List<AdKatsPlayer> FetchOnlineAdminSoldiers() {
            var onlineAdminSoldiers = new Dictionary<String, AdKatsPlayer>();
            foreach (AdKatsPlayer aPlayer in FetchAdminSoldiers()) {
                AdKatsPlayer adminSoldier;
                if (_playerDictionary.TryGetValue(aPlayer.player_name, out adminSoldier)) {
                    if (!onlineAdminSoldiers.ContainsKey(aPlayer.player_name)) {
                        onlineAdminSoldiers.Add(adminSoldier.player_name, adminSoldier);
                    }
                }
            }
            return onlineAdminSoldiers.Values.ToList();
        }

        public List<AdKatsSpecialPlayer> FetchMatchingSpecialPlayers(String group, AdKatsPlayer aPlayer) {
            DebugWrite("Entering FetchMatchingSpecialPlayers", 6);
            try {
                var matchingPlayers = new List<AdKatsSpecialPlayer>();
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Attempt to execute the query
                        command.CommandText = @"
                        SELECT 
	                        specialplayer_id,
	                        player_group,
	                        player_id,
	                        player_game,
	                        player_server,
	                        player_identifier
                        FROM 
	                        adkats_specialplayers
                        WHERE
	                        player_id = @player_id
                        OR
	                        player_identifier = @player_identifier_name
                        OR
	                        player_identifier = @player_identifier_guid
                        OR
	                        player_identifier = @player_identifier_ip";
                        command.Parameters.AddWithValue("player_id", aPlayer.player_id);
                        command.Parameters.AddWithValue("player_identifier_name", aPlayer.player_name);
                        command.Parameters.AddWithValue("player_identifier_guid", aPlayer.player_guid);
                        command.Parameters.AddWithValue("player_identifier_ip", aPlayer.player_ip);
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                var asPlayer = new AdKatsSpecialPlayer();
                                asPlayer.player_group = reader.GetString("player_group");
                                if (asPlayer.player_group != group) {
                                    continue;
                                }
                                Int32? player_id = null;
                                if (!reader.IsDBNull(2)) {
                                    player_id = reader.GetInt32("player_id");
                                }
                                if (!reader.IsDBNull(3))
                                    asPlayer.player_game = reader.GetInt32("player_game");
                                if (!reader.IsDBNull(4))
                                    asPlayer.player_server = reader.GetInt32("player_server");
                                if (!reader.IsDBNull(5))
                                    asPlayer.player_identifier = reader.GetString("player_identifier");
                                if (asPlayer.player_game != null && asPlayer.player_game != _gameID) {
                                    DebugWrite("Matching player rejected for mismatched game id", 4);
                                    continue;
                                }
                                if (asPlayer.player_server != null && asPlayer.player_server != _serverID) {
                                    DebugWrite("Matching player rejected for mismatched server id", 4);
                                    continue;
                                }
                                if (player_id != null) {
                                    if (player_id == aPlayer.player_id) {
                                        DebugWrite("Matching player rejected for mismatched player id", 4);
                                        continue;
                                    }
                                    asPlayer.player_object = aPlayer;
                                }
                                if (String.IsNullOrEmpty(asPlayer.player_identifier) && asPlayer.player_identifier != aPlayer.player_name && asPlayer.player_identifier != aPlayer.player_guid && asPlayer.player_identifier != aPlayer.player_ip) {
                                    DebugWrite("Matching player rejected for mismatched player identifier", 4);
                                    continue;
                                }
                                matchingPlayers.Add(asPlayer);
                            }
                        }
                    }
                }
                return matchingPlayers;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while taking action for Disperse record.", e));
            }
            DebugWrite("Exiting FetchMatchingSpecialPlayers", 6);
            return null;
        }

        private List<AdKatsPlayer> FetchElevatedSoldiers() {
            var elevatedSoldiers = new List<AdKatsPlayer>();
            //Loop over the user list
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (_userCache) {
                foreach (AdKatsUser user in _userCache.Values.Where(user => !RoleIsInteractionAble(user.user_role))) {
                    elevatedSoldiers.AddRange(user.soldierDictionary.Values);
                }
            }
            return elevatedSoldiers;
        }

        private List<AdKatsPlayer> FetchSoldiersOfRole(AdKatsRole aRole) {
            var roleSoldiers = new List<AdKatsPlayer>();
            //Loop over the user list
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (_userCache) {
                foreach (AdKatsUser user in _userCache.Values.Where(user => user.user_role.role_key == aRole.role_key)) {
                    roleSoldiers.AddRange(user.soldierDictionary.Values);
                }
            }
            return roleSoldiers;
        }

        private List<AdKatsPlayer> FetchAllUserSoldiers() {
            var userSoldiers = new List<AdKatsPlayer>();
            //Loop over the user list
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (_userCache) {
                foreach (AdKatsUser user in _userCache.Values) {
                    userSoldiers.AddRange(user.soldierDictionary.Values);
                }
            }
            return userSoldiers;
        }

        private Boolean HandleRecordUpload(AdKatsRecord record) {
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return true;
            }
            try {
                DebugWrite("DBCOMM: Entering handle record upload", 5);
                if (record.record_id != -1 || record.record_action_executed) {
                    //Record already has a record ID, or action has already been taken, it can only be updated
                    if (record.command_type.command_logging != AdKatsCommand.CommandLogging.Ignore) {
                        if (record.record_exception == null) {
                            //Only call update if the record contained no errors
                            DebugWrite("DBCOMM: UPDATING record for " + record.command_type, 5);
                            //Update Record
                            UpdateRecord(record);
                            return false;
                        }
                        DebugWrite("DBCOMM: " + record.command_type + " record contained errors, skipping UPDATE", 4);
                    }
                    else {
                        DebugWrite("DBCOMM: Skipping record UPDATE for " + record.command_type, 5);
                    }
                }
                else {
                    DebugWrite("DBCOMM: Record needs full upload, checking.", 5);
                    //No record ID. Perform full upload
                    switch (record.command_type.command_key) {
                        case "player_punish":
                            //Upload for punish is required
                            if (CanPunish(record, 20)) {
                                //Check if the punish will be Double counted
                                Boolean iroStatus = _IROActive && FetchIROStatus(record);
                                if (iroStatus) {
                                    record.isIRO = true;
                                    //Upload record twice
                                    DebugWrite("DBCOMM: UPLOADING IRO Punish", 5); //IRO - Immediate Repeat Offence
                                    UploadRecord(record);
                                    UploadRecord(record);
                                }
                                else {
                                    //Upload record once
                                    DebugWrite("DBCOMM: UPLOADING Punish", 5);
                                    UploadRecord(record);
                                }
                            }
                            else {
                                SendMessageToSource(record, record.target_player.player_name + " already acted on in the last 20 seconds.");
                                return false;
                            }
                            break;
                        case "player_forgive":
                            //Upload for forgive is required
                            //No restriction on forgives/minute
                            DebugWrite("DBCOMM: UPLOADING Forgive", 5);
                            UploadRecord(record);
                            break;
                        default:
                            //Case for any other command
                            //Check logging setting for record command type
                            if (record.command_type.command_logging != AdKatsCommand.CommandLogging.Ignore) {
                                DebugWrite("UPLOADING record for " + record.command_type, 5);
                                //Upload Record
                                UploadRecord(record);
                            }
                            else {
                                DebugWrite("Skipping record UPLOAD for " + record.command_type, 6);
                            }
                            break;
                    }
                }
            }
            catch (Exception e) {
                record.record_exception = HandleException(new AdKatsException("Error while handling record upload.", e));
            }
            return true;
        }

        private Boolean UploadRecord(AdKatsRecord record) {
            DebugWrite("uploadRecord starting!", 6);

            Boolean success = false;
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return false;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
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
                            @record_time, 
                            'Y'
                        )";

                        //Fill the command
                        command.Parameters.AddWithValue("@server_id", record.server_id);
                        if (record.command_type == null) {
                            ConsoleError("Command type was null in uploadRecord, unable to continue.");
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
                        String sName = "NoNameSource";
                        if (!String.IsNullOrEmpty(record.source_name)) {
                            sName = record.source_name;
                        }
                        if (record.source_player != null) {
                            if (!String.IsNullOrEmpty(record.source_player.player_name)) {
                                sName = record.source_player.player_name;
                            }
                            command.Parameters.AddWithValue("@source_id", record.source_player.player_id);
                        }
                        else {
                            command.Parameters.AddWithValue("@source_id", null);
                        }
                        command.Parameters.AddWithValue("@source_name", sName);

                        String messageIRO = record.record_message + ((record.isIRO) ? (" [IRO]") : (""));
                        //Trim to 500 characters (Should only hit this limit when processing error messages)
                        messageIRO = messageIRO.Length <= 500 ? messageIRO : messageIRO.Substring(0, 500);
                        command.Parameters.AddWithValue("@record_message", messageIRO);

                        command.Parameters.AddWithValue("@record_time", record.record_time);

                        //Get reference to the command in case of error
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0) {
                            success = true;
                            record.record_id = command.LastInsertedId;
                        }
                    }
                }

                if (success) {
                    DebugWrite(record.command_action.command_key + " upload for " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
                }
                else {
                    record.record_exception = new AdKatsException("Unknown error uploading record.");
                    HandleException(record.record_exception);
                }

                DebugWrite("uploadRecord finished!", 6);
                return success;
            }
            catch (Exception e) {
                record.record_exception = new AdKatsException("Unexpected error uploading Record.", e);
                HandleException(record.record_exception);
                return false;
            }
        }

        private void UpdateRecordEndPointReputations(AdKatsRecord aRecord) {
            if (aRecord.source_player != null && aRecord.source_player.player_id > 0) {
                UpdatePlayerReputation(aRecord.source_player);
            }
            if (aRecord.target_player != null && aRecord.target_player.player_id > 0) {
                UpdatePlayerReputation(aRecord.target_player);
            }
        }

        private void UpdatePlayerReputation(AdKatsPlayer aPlayer) {
            try {
                if (!_isTestingAuthorized) {
                    return;
                }
                double sourceReputation = 0.0;
                double targetReputation = (-25) * FetchPoints(aPlayer, true);
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"SELECT 
	                        command_type,
	                        command_action,
	                        count(record_id) command_count
                        FROM
	                        adkats_records_main
                        WHERE
	                        source_id = @player_id
                        AND
	                        target_name <> source_name
                        GROUP BY command_type, command_action";

                        command.Parameters.AddWithValue("player_id", aPlayer.player_id);
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                String typeAction = reader.GetInt32("command_type") + "|" + reader.GetInt32("command_action");
                                Double command_count = reader.GetDouble("command_count");
                                Double weight = 0;
                                if (_commandSourceReputationDictionary.TryGetValue(typeAction, out weight)) {
                                    sourceReputation += (weight * command_count);
                                }
                                else {
                                    ConsoleError("Unable to find source weight for type|action: " + typeAction);
                                }
                            }
                        }
                    }
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"SELECT 
	                        command_type,
	                        command_action,
	                        count(record_id) command_count
                        FROM
	                        adkats_records_main
                        WHERE
	                        target_id = @player_id
                        AND
	                        target_name <> source_name
                        GROUP BY command_type, command_action";

                        command.Parameters.AddWithValue("player_id", aPlayer.player_id);
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                String typeAction = reader.GetInt32("command_type") + "|" + reader.GetInt32("command_action");
                                Double command_count = reader.GetDouble("command_count");
                                Double weight = 0;
                                if (_commandTargetReputationDictionary.TryGetValue(typeAction, out weight)) {
                                    targetReputation += (weight * command_count);
                                }
                                else {
                                    ConsoleError("Unable to find target weight for type|action: " + typeAction);
                                }
                            }
                        }
                    }
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"REPLACE INTO
	                        adkats_player_reputation
                        VALUES 
                        (
	                        @player_id,
	                        @game_id,
	                        @target_rep,
	                        @source_rep,
	                        @total_rep
                        )";

                        command.Parameters.AddWithValue("player_id", aPlayer.player_id);
                        if (aPlayer.game_id <= 0) {
                            aPlayer.game_id = _gameID;
                        }
                        command.Parameters.AddWithValue("game_id", aPlayer.game_id);
                        command.Parameters.AddWithValue("target_rep", targetReputation);
                        command.Parameters.AddWithValue("source_rep", sourceReputation);
                        command.Parameters.AddWithValue("total_rep", targetReputation + sourceReputation);

                        Int32 rowsAffected = command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating player reputation.", e));
            }
        }

        private Boolean SendQuery(String query, Boolean verbose) {
            if (String.IsNullOrEmpty(query)) {
                return false;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        //Attempt to execute the query
                        command.CommandText = query;
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                if (verbose) {
                                    ConsoleSuccess("Query returned values.");
                                }
                                return true;
                            }
                            if (verbose) {
                                ConsoleError("Query returned no results.");
                            }
                            return false;
                        }
                    }
                }
            }
            catch (Exception e) {
                if (verbose) {
                    ConsoleError(e.ToString());
                }
                return false;
            }
        }

        private Boolean SendNonQuery(String desc, String nonQuery, Boolean verbose) {
            if (String.IsNullOrEmpty(nonQuery)) {
                return false;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = nonQuery;
                        //Attempt to execute the non query
                        Int32 rowsAffected = command.ExecuteNonQuery();
                        if (verbose) {
                            ConsoleSuccess("Non-Query success. " + rowsAffected + " rows affected. [" + desc + "]");
                        }
                        return true;
                    }
                }
            }
            catch (Exception e) {
                if (verbose) {
                    ConsoleError("Non-Query failed. [" + desc + "]: " + e);
                }
                return false;
            }
        }

        private void UpdateRecord(AdKatsRecord record) {
            DebugWrite("updateRecord starting!", 6);

            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return;
            }
            try {
                Int32 attempts = 0;
                Boolean success = false;
                do {
                    try {
                        using (MySqlConnection connection = GetDatabaseConnection()) {
                            using (MySqlCommand command = connection.CreateCommand()) {
                                String tablename = (record.isDebug) ? ("`adkats_records_debug`") : ("`adkats_records_main`");
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
                                if (rowsAffected > 0) {
                                    success = true;
                                }
                            }
                        }
                    }
                    catch (Exception e) {
                        ConsoleError(e.ToString());
                        success = false;
                    }
                } while (!success && attempts++ < 5);

                if (success) {
                    DebugWrite(record.command_action.command_key + " update for " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
                    UpdateRecordEndPointReputations(record);
                }
                else {
                    ConsoleError(record.command_action.command_key + " update for " + record.target_name + " by " + record.source_name + " FAILED!");
                }

                DebugWrite("updateRecord finished!", 6);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating record", e));
            }
        }

        //DONE
        private AdKatsRecord FetchRecordByID(Int64 recordID, Boolean debug) {
            DebugWrite("fetchRecordByID starting!", 6);
            AdKatsRecord record = null;
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return null;
            }
            try {
                //Success fetching record
                Boolean success = false;
                using (MySqlConnection connection = GetDatabaseConnection()) {
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
                                if (!_CommandIDDictionary.TryGetValue(commandTypeInt, out record.command_type)) {
                                    ConsoleError("Unable to parse command type " + commandTypeInt + " when fetching record by ID.");
                                }
                                Int32 commandActionInt = reader.GetInt32("command_action");
                                if (!_CommandIDDictionary.TryGetValue(commandActionInt, out record.command_action)) {
                                    ConsoleError("Unable to parse command action " + commandActionInt + " when fetching record by ID.");
                                }
                                record.command_numeric = reader.GetInt32("command_numeric");
                                record.target_name = reader.GetString("target_name");
                                if (!reader.IsDBNull(6)) {
                                    record.target_player = new AdKatsPlayer {
                                        player_id = reader.GetInt64(6)
                                    };
                                }
                                record.source_name = reader.GetString("source_name");
                                record.record_message = reader.GetString("record_message");
                                record.record_time = reader.GetDateTime("record_time");
                            }
                            if (success) {
                                DebugWrite("Record found for ID " + recordID, 5);
                            }
                            else {
                                DebugWrite("No record found for ID " + recordID, 5);
                            }
                        }
                        if (success && record.target_player != null) {
                            long oldID = record.target_player.player_id;
                            record.target_player = FetchPlayer(false, true, null, oldID, null, null, null);
                            if (record.target_player == null) {
                                ConsoleError("Unable to find player ID: " + oldID);
                                return null;
                            }
                            if (!String.IsNullOrEmpty(record.target_player.player_name)) {
                                record.target_name = record.target_player.player_name;
                            }
                            else {
                                record.target_name = "NoNameTarget";
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching record by ID", e));
            }

            DebugWrite("fetchRecordByID finished!", 6);
            return record;
        }

        //DONE
        private IEnumerable<AdKatsRecord> FetchUnreadRecords() {
            DebugWrite("fetchUnreadRecords starting!", 6);
            //Create return list
            var records = new List<AdKatsRecord>();
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return records;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
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
                            `" + _mySqlDatabaseName + @"`.`adkats_records_main` 
                        WHERE 
                            `adkats_read` = 'N' 
                        AND 
                            `server_id` = " + _serverID;
                        command.CommandText = sql;
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            //Grab the record
                            while (reader.Read()) {
                                var record = new AdKatsRecord();
                                record.record_source = AdKatsRecord.Sources.Database;
                                record.record_id = reader.GetInt64("record_id");
                                record.server_id = reader.GetInt64("server_id");
                                Int32 commandTypeInt = reader.GetInt32("command_type");
                                if (!_CommandIDDictionary.TryGetValue(commandTypeInt, out record.command_type)) {
                                    ConsoleError("Unable to parse command type " + commandTypeInt + " when fetching record by ID.");
                                }
                                Int32 commandActionInt = reader.GetInt32("command_action");
                                if (!_CommandIDDictionary.TryGetValue(commandActionInt, out record.command_action)) {
                                    ConsoleError("Unable to parse command action " + commandActionInt + " when fetching record by ID.");
                                }
                                record.command_numeric = reader.GetInt32("command_numeric");
                                record.target_name = reader.GetString("target_name");
                                object value = reader.GetValue(6);
                                Int64 targetIDParse = -1;
                                DebugWrite("id fetched!", 6);
                                if (Int64.TryParse(value.ToString(), out targetIDParse)) {
                                    DebugWrite("id parsed! " + targetIDParse, 6);
                                    //Check if the player needs to be imported, or if they are already in the server
                                    AdKatsPlayer importedPlayer = FetchPlayer(false, true, null, targetIDParse, null, null, null);
                                    if (importedPlayer == null) {
                                        continue;
                                    }
                                    AdKatsPlayer currentPlayer = null;
                                    if (!String.IsNullOrEmpty(importedPlayer.player_name) && _playerDictionary.TryGetValue(importedPlayer.player_name, out currentPlayer)) {
                                        DebugWrite("External player is currently in the server, using existing data.", 5);
                                        record.target_player = currentPlayer;
                                    }
                                    else {
                                        DebugWrite("External player is not in the server, fetching from database.", 5);
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
                HandleException(new AdKatsException("Error while fetching unread records from database.", e));
            }

            DebugWrite("fetchUnreadRecords finished!", 6);
            return records;
        }

        //DONE
        private Int64 FetchNameBanCount() {
            DebugWrite("fetchNameBanCount starting!", 7);
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return 0;
            }
            if (_NameBanCount >= 0 && (DateTime.UtcNow - _LastNameBanCountFetch).TotalSeconds < 30) {
                return _NameBanCount;
            }
            _LastNameBanCountFetch = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
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
                                _NameBanCount = reader.GetInt64("ban_count");
                                return _NameBanCount;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching number of id bans.", e));
            }

            DebugWrite("fetchNameBanCount finished!", 7);
            return -1;
        }

        //DONE
        private Int64 FetchGUIDBanCount() {
            DebugWrite("fetchGUIDBanCount starting!", 7);
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return 0;
            }
            if (_GUIDBanCount >= 0 && (DateTime.UtcNow - _LastGUIDBanCountFetch).TotalSeconds < 30) {
                return _GUIDBanCount;
            }
            _LastGUIDBanCountFetch = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
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
                                _GUIDBanCount = reader.GetInt64("ban_count");
                                return _GUIDBanCount;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching number of GUID bans.", e));
            }

            DebugWrite("fetchGUIDBanCount finished!", 7);
            return -1;
        }

        //DONE
        private Int64 FetchIPBanCount() {
            DebugWrite("fetchIPBanCount starting!", 7);
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return 0;
            }
            if (_IPBanCount >= 0 && (DateTime.UtcNow - _LastIPBanCountFetch).TotalSeconds < 30) {
                return _IPBanCount;
            }
            _LastIPBanCountFetch = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
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
                                _IPBanCount = reader.GetInt64("ban_count");
                                return _IPBanCount;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching number of IP bans.", e));
            }

            DebugWrite("fetchIPBanCount finished!", 7);
            return -1;
        }

        private void RemoveUser(AdKatsUser user) {
            DebugWrite("removeUser starting!", 6);
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = "DELETE FROM `" + _mySqlDatabaseName + "`.`adkats_users` WHERE `user_id` = @user_id";
                        command.Parameters.AddWithValue("@user_id", user.user_id);
                        Int32 rowsAffected = command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while removing user.", e));
            }
            DebugWrite("removeUser finished!", 6);
        }

        private void RemoveRole(AdKatsRole aRole) {
            DebugWrite("removeRole starting!", 6);
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                //Assign "Default Guest" to all users currently on this role
                AdKatsRole guestRole = null;
                if (_RoleKeyDictionary.TryGetValue("guest_default", out guestRole)) {
                    foreach (AdKatsUser aUser in _userCache.Values) {
                        if (aUser.user_role.role_key == aRole.role_key) {
                            aUser.user_role = guestRole;
                        }
                        UploadUser(aUser);
                    }
                }
                else {
                    ConsoleError("Could not fetch default guest user role. Unsafe to remove requested user role.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = "DELETE FROM `" + _mySqlDatabaseName + "`.`adkats_rolecommands` WHERE `role_id` = @role_id";
                        command.Parameters.AddWithValue("@role_id", aRole.role_id);
                        Int32 rowsAffected = command.ExecuteNonQuery();
                    }
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = "DELETE FROM `" + _mySqlDatabaseName + "`.`adkats_roles` WHERE `role_id` = @role_id";
                        command.Parameters.AddWithValue("@role_id", aRole.role_id);
                        Int32 rowsAffected = command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while removing user.", e));
            }
            DebugWrite("removeRole finished!", 6);
        }

        private void UploadUser(AdKatsUser aUser) {
            DebugWrite("uploadUser starting!", 6);
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                DebugWrite("Uploading user: " + aUser.user_name, 5);

                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        if (aUser.user_role == null) {
                            AdKatsRole aRole = null;
                            if (_RoleKeyDictionary.TryGetValue("guest_default", out aRole)) {
                                aUser.user_role = aRole;
                            }
                            else {
                                ConsoleError("Unable to assign default guest role to user " + aUser.user_name + ". Unable to upload user.");
                                return;
                            }
                        }
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
                            DebugWrite("User uploaded to database SUCCESSFULY.", 5);
                        }
                        else {
                            ConsoleError("Unable to upload user " + aUser.user_name + " to database.");
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
                        foreach (AdKatsPlayer aPlayer in aUser.soldierDictionary.Values) {
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
                                    DebugWrite("Soldier link " + aUser.user_id + "->" + aPlayer.player_id + " uploaded to database SUCCESSFULY.", 5);
                                }
                                else {
                                    ConsoleError("Unable to upload soldier link for " + aUser.user_name + " to database.");
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating player access.", e));
            }

            DebugWrite("uploadUser finished!", 6);
        }

        private void TryAddUserSoldier(AdKatsUser aUser, String soldierName) {
            try {
                //Attempt to fetch the soldier
                if (!String.IsNullOrEmpty(soldierName) && SoldierNameValid(soldierName)) {
                    List<AdKatsPlayer> matchingPlayers;
                    if (FetchMatchingPlayers(soldierName, out matchingPlayers, false)) {
                        if (matchingPlayers.Count > 0) {
                            if (matchingPlayers.Count > 10) {
                                ConsoleError("Too many players matched the query, unable to add.");
                                return;
                            }
                            foreach (AdKatsPlayer matchingPlayer in matchingPlayers) {
                                bool playerDuplicate = false;
                                //Make sure the player is not already assigned to another user
                                if (_isTestingAuthorized)
                                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                lock (_userCache) {
                                    if (_userCache.Values.Any(innerUser => innerUser.soldierDictionary.ContainsKey(matchingPlayer.player_id))) {
                                        playerDuplicate = true;
                                    }
                                }
                                if (!playerDuplicate) {
                                    if (aUser.soldierDictionary.ContainsKey(matchingPlayer.player_id)) {
                                        aUser.soldierDictionary.Remove(matchingPlayer.player_id);
                                    }
                                    aUser.soldierDictionary.Add(matchingPlayer.player_id, matchingPlayer);
                                }
                                else {
                                    ConsoleError("Player " + matchingPlayer.player_name + "(" + _gameIDDictionary[matchingPlayer.game_id] + ") already assigned to a user.");
                                }
                            }
                            return;
                        }
                        ConsoleError("Players matching '" + soldierName + "' not found in database. Unable to assign to user.");
                    }
                }
                else {
                    ConsoleError("'" + soldierName + "' was an invalid soldier name. Unable to assign to user.");
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while attempting to add user soldier.", e));
            }
        }

        //DONE
        private void UploadRole(AdKatsRole aRole) {
            DebugWrite("uploadRole starting!", 6);
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (aRole) {
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (aRole.RoleAllowedCommands) {
                        DebugWrite("Uploading role: " + aRole.role_name, 5);

                        //Open db connection
                        using (MySqlConnection connection = GetDatabaseConnection()) {
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
                                    DebugWrite("Role uploaded to database SUCCESSFULY.", 5);
                                }
                                else {
                                    ConsoleError("Unable to upload role " + aRole.role_name + " to database.");
                                    return;
                                }
                            }
                            //Delete all current allowed commands
                            using (MySqlCommand command = connection.CreateCommand()) {
                                command.CommandText = @"DELETE FROM `adkats_rolecommands` where `role_id` = " + aRole.role_id;
                                //Attempt to execute the query
                                Int32 rowsAffected = command.ExecuteNonQuery();
                            }
                            foreach (AdKatsCommand aCommand in aRole.RoleAllowedCommands.Values) {
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
                                        DebugWrite("Role-command uploaded to database SUCCESSFULY.", 5);
                                    }
                                    else {
                                        ConsoleError("Unable to upload role-command for " + aRole.role_name + ".");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while uploading role.", e));
            }

            DebugWrite("uploadRole finished!", 6);
        }

        //DONE
        private void UploadBan(AdKatsBan aBan) {
            DebugWrite("uploadBan starting!", 6);

            Boolean success = false;
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return;
            }
            if (aBan == null) {
                ConsoleError("Ban invalid in uploadBan.");
            }
            else {
                try {
                    //Upload the inner record if needed
                    if (aBan.ban_record.record_id < 0) {
                        if (!UploadRecord(aBan.ban_record)) {
                            return;
                        }
                    }

                    using (MySqlConnection connection = GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            command.CommandText = @"
                            INSERT INTO 
                            `" + _mySqlDatabaseName + @"`.`adkats_bans` 
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
	                            `ban_sync` = @ban_sync";

                            command.Parameters.AddWithValue("@player_id", aBan.ban_record.target_player.player_id);
                            command.Parameters.AddWithValue("@latest_record_id", aBan.ban_record.record_id);
                            if (String.IsNullOrEmpty(aBan.ban_status)) {
                                aBan.ban_exception = new AdKatsException("Ban status was null or empty when posting.");
                                HandleException(aBan.ban_exception);
                                return;
                            }
                            if (aBan.ban_status != "Active" && aBan.ban_status != "Disabled" && aBan.ban_status != "Expired") {
                                aBan.ban_exception = new AdKatsException("Ban status of '" + aBan.ban_status + "' was invalid when posting.");
                                HandleException(aBan.ban_exception);
                                return;
                            }
                            command.Parameters.AddWithValue("@ban_status", aBan.ban_status);
                            if (String.IsNullOrEmpty(aBan.ban_notes))
                                aBan.ban_notes = "NoNotes";
                            command.Parameters.AddWithValue("@ban_notes", aBan.ban_notes);
                            command.Parameters.AddWithValue("@ban_enforceName", aBan.ban_enforceName ? ('Y') : ('N'));
                            command.Parameters.AddWithValue("@ban_enforceGUID", aBan.ban_enforceGUID ? ('Y') : ('N'));
                            command.Parameters.AddWithValue("@ban_enforceIP", aBan.ban_enforceIP ? ('Y') : ('N'));
                            command.Parameters.AddWithValue("@ban_sync", "*" + _serverID + "*");
                            //Handle permaban case
                            if (aBan.ban_record.command_action.command_key.Contains("player_ban_perm")) {
                                command.Parameters.AddWithValue("@ban_durationMinutes", (Int32) _PermaBanEndTime.Subtract(DateTime.UtcNow).TotalMinutes);
                            }
                            else {
                                command.Parameters.AddWithValue("@ban_durationMinutes", aBan.ban_record.command_numeric);
                            }
                            if (aBan.ban_record.command_action.command_key == "player_ban_perm_future") {
                                command.Parameters.AddWithValue("@ban_startTime", aBan.ban_record.record_time + TimeSpan.FromMinutes(aBan.ban_record.command_numeric));
                            }
                            else {
                                command.Parameters.AddWithValue("@ban_startTime", aBan.ban_record.record_time);
                            }
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() >= 0) {
                                //Rows affected should be > 0
                                DebugWrite("Success Uploading Ban on player " + aBan.ban_record.target_player.player_id, 5);
                                success = true;
                            }
                        }
                        if (success) {
                            using (MySqlCommand command = connection.CreateCommand()) {
                                command.CommandText = @"
                                SELECT 
                                    `ban_id`,
                                    `ban_startTime`, 
                                    `ban_endTime`,
                                    `ban_status`
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
                                        String status = reader.GetString("ban_status");
                                        if (status != aBan.ban_status) {
                                            aBan.ban_exception = new AdKatsException("Ban status was invalid when confirming ban post. Your database is not in strict mode.");
                                            HandleException(aBan.ban_exception);
                                            return;
                                        }
                                        //Update setting page to reflect the ban count
                                        UpdateSettingPage();
                                        DebugWrite("Ban ID: " + aBan.ban_id, 5);
                                    }
                                    else {
                                        ConsoleError("Could not fetch ban information after upload");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error while uploading new ban.", e));
                }
            }
            DebugWrite("uploadBan finished!", 6);
        }

        private Boolean FetchMatchingPlayers(String playerName, out List<AdKatsPlayer> resultPlayers, Boolean verbose) {
            DebugWrite("FetchMatchingPlayers starting!", 6);
            resultPlayers = new List<AdKatsPlayer>();
            if (String.IsNullOrEmpty(playerName)) {
                if (verbose) {
                    ConsoleError("Player id was blank when fetching players.");
                }
                return false;
            }
            using (MySqlConnection connection = GetDatabaseConnection()) {
                using (MySqlCommand command = connection.CreateCommand()) {
                    command.CommandText = @"
                    SELECT 
	                    `PlayerID` AS `player_id`
                    FROM 
	                    `tbl_playerdata`
                    WHERE
	                    `SoldierName` LIKE '%" + playerName + "%'";
                    //Attempt to execute the query
                    using (MySqlDataReader reader = command.ExecuteReader()) {
                        //Grab the matching players
                        while (reader.Read()) {
                            AdKatsPlayer aPlayer = FetchPlayer(false, true, null, reader.GetInt64("player_id"), null, null, null);
                            if (aPlayer != null) {
                                resultPlayers.Add(aPlayer);
                            }
                        }
                        if (resultPlayers.Count == 0) {
                            if (verbose) {
                                ConsoleError("No players found matching '" + playerName + "'");
                            }
                            return false;
                        }
                    }
                }
            }
            DebugWrite("FetchMatchingPlayers finished!", 6);
            return true;
        }

        //DONE
        private AdKatsPlayer FetchPlayer(Boolean allowUpdate, Boolean allowOtherGames, Int32? gameID, Int64 playerID, String playerName, String playerGUID, String playerIP) {
            DebugWrite("fetchPlayer starting!", 6);
            //Create return list
            AdKatsPlayer aPlayer = null;
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                //If AdKats is disconnected from the database, return the player as-is
                aPlayer = new AdKatsPlayer {
                    game_id = _gameID,
                    player_name = playerName,
                    player_guid = playerGUID,
                    player_ip = playerIP
                };
                AssignPlayerRole(aPlayer);
                return aPlayer;
            }
            if (playerID < 0 && String.IsNullOrEmpty(playerName) && String.IsNullOrEmpty(playerGUID) && String.IsNullOrEmpty(playerIP)) {
                ConsoleError("Attempted to fetch player with no information.");
            }
            else {
                try {
                    using (MySqlConnection connection = GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            String sql = @"
                            SELECT 
                                `PlayerID` as `player_id`, 
                                `SoldierName` as `player_name`, 
                                `EAGUID` as `player_guid`, 
                                `PBGUID` as `player_pbguid`, 
                                `IP_Address` as `player_ip`";
                            if (_gameID > 0) {
                                sql += ",`GameID` as `game_id` ";
                            }
                            sql += "FROM `" + _mySqlDatabaseName + @"`.`tbl_playerdata` ";
                            bool sqlEnder = true;
                            if (playerID >= 0) {
                                sql += " WHERE ( ";
                                sqlEnder = false;
                                sql += " `PlayerID` = " + playerID + " ";
                            }
                            if (!String.IsNullOrEmpty(playerGUID)) {
                                if (sqlEnder) {
                                    sql += " WHERE ( ";
                                    sqlEnder = false;
                                }
                                else {
                                    sql += " OR ";
                                }
                                sql += " `EAGUID` LIKE '" + playerGUID + "' ";
                            }
                            if (String.IsNullOrEmpty(playerGUID) && !String.IsNullOrEmpty(playerName)) {
                                if (sqlEnder) {
                                    sql += " WHERE ( ";
                                    sqlEnder = false;
                                }
                                else {
                                    sql += " OR ";
                                }
                                sql += " `SoldierName` LIKE '" + playerName + "' ";
                            }
                            if (String.IsNullOrEmpty(playerGUID) && !String.IsNullOrEmpty(playerIP)) {
                                if (sqlEnder) {
                                    sql += " WHERE ( ";
                                    sqlEnder = false;
                                }
                                else {
                                    sql += " OR ";
                                }
                                sql += " `IP_Address` LIKE '" + playerIP + "' ";
                            }
                            if (!sqlEnder) {
                                sql += " ) ";
                            }
                            if ((_gameID > 0 && !allowOtherGames) || gameID != null) {
                                if (gameID != null) {
                                    sql += " AND `GameID` = " + gameID + " ";
                                }
                                else {
                                    sql += " AND `GameID` = " + _gameID + " ";
                                }
                            }
                            command.CommandText = sql;
                            using (MySqlDataReader reader = command.ExecuteReader()) {
                                if (reader.Read()) {
                                    aPlayer = new AdKatsPlayer();
                                    //Player ID will never be null
                                    aPlayer.player_id = reader.GetInt64("player_id");
                                    aPlayer.game_id = reader.GetInt32("game_id");
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
                                    DebugWrite("No player matching search information.", 5);
                                }
                            }
                        }
                        if (allowUpdate) {
                            if (aPlayer == null) {
                                DebugWrite("Adding player to database.", 5);
                                using (MySqlCommand command = connection.CreateCommand()) {
                                    Int32? useableGameID = null;
                                    if (gameID != null) {
                                        useableGameID = gameID;
                                    }
                                    else if (_gameID > 0) {
                                        useableGameID = _gameID;
                                    }
                                    //Set the insert command structure
                                    Boolean hasPrevious = (_gameID > 0) || !String.IsNullOrEmpty(playerName) || !String.IsNullOrEmpty(playerGUID) || !String.IsNullOrEmpty(playerIP);
                                    command.CommandText = @"
                                    INSERT INTO `" + _mySqlDatabaseName + @"`.`tbl_playerdata` 
                                    (
                                        " + ((useableGameID != null) ? ("`GameID`") : ("")) + @"
                                        " + ((!String.IsNullOrEmpty(playerName)) ? (((useableGameID != null) ? (",") : ("")) + "`SoldierName`") : ("")) + @"
                                        " + ((!String.IsNullOrEmpty(playerGUID)) ? ((hasPrevious ? (",") : ("")) + "`EAGUID`") : ("")) + @"
                                        " + ((!String.IsNullOrEmpty(playerIP)) ? ((hasPrevious ? (",") : ("")) + "`IP_Address`") : ("")) + @"
                                    ) 
                                    VALUES 
                                    (
                                        " + ((useableGameID != null) ? (useableGameID + "") : ("")) + @"
                                        " + ((!String.IsNullOrEmpty(playerName)) ? (((useableGameID != null) ? (",") : ("")) + "'" + playerName + "'") : ("")) + @"
                                        " + ((!String.IsNullOrEmpty(playerGUID)) ? ((hasPrevious ? (",") : ("")) + "'" + playerGUID + "'") : ("")) + @"
                                        " + ((!String.IsNullOrEmpty(playerIP)) ? ((hasPrevious ? (",") : ("")) + "'" + playerIP + "'") : ("")) + @"
                                    )
                                    ON DUPLICATE KEY 
                                    UPDATE 
                                        `PlayerID` = LAST_INSERT_ID(`PlayerID`)
                                        " + ((!String.IsNullOrEmpty(playerName)) ? (@",`SoldierName` = '" + playerName + "'") : ("")) + @"
                                        " + ((!String.IsNullOrEmpty(playerGUID)) ? (@",`EAGUID` = '" + playerGUID + "'") : ("")) + @"
                                        " + ((!String.IsNullOrEmpty(playerIP)) ? (@",`IP_Address` = '" + playerIP + "'") : (""));
                                    //Attempt to execute the query
                                    if (command.ExecuteNonQuery() > 0) {
                                        //Rows affected should be > 0
                                        aPlayer = new AdKatsPlayer {
                                            player_id = command.LastInsertedId,
                                            player_name = playerName,
                                            player_guid = playerGUID,
                                            player_ip = playerIP
                                        };
                                        if (useableGameID != null) {
                                            aPlayer.game_id = (long) useableGameID;
                                        }
                                        else {
                                            aPlayer.game_id = _gameID;
                                        }
                                    }
                                    else {
                                        ConsoleError("Unable to add player to database.");
                                        return null;
                                    }
                                }
                            }
                            if (!String.IsNullOrEmpty(playerName) && !String.IsNullOrEmpty(aPlayer.player_guid) && playerName != aPlayer.player_name) {
                                aPlayer.player_name_previous = aPlayer.player_name;
                                aPlayer.player_name = playerName;
                                var record = new AdKatsRecord {
                                    record_source = AdKatsRecord.Sources.InternalAutomated,
                                    server_id = _serverID,
                                    command_type = _CommandKeyDictionary["player_changename"],
                                    command_numeric = 0,
                                    target_name = aPlayer.player_name,
                                    target_player = aPlayer,
                                    source_name = "AdKats",
                                    record_message = aPlayer.player_name_previous
                                };
                                QueueRecordForProcessing(record);
                                DebugWrite(aPlayer.player_name_previous + " changed their name to " + playerName + ". Updating the database.", 2);
                                UpdatePlayer(aPlayer);
                            }
                        }
                        if (aPlayer == null) {
                            return null;
                        }
                        //Assign player role
                        AssignPlayerRole(aPlayer);
                    }
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error while fetching player.", e));
                }
            }
            DebugWrite("fetchPlayer finished!", 6);
            return aPlayer;
        }

        private AdKatsPlayer UpdatePlayer(AdKatsPlayer player) {
            DebugWrite("updatePlayer starting!", 6);
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return player;
            }
            if (player == null || player.player_id < 0 || (String.IsNullOrEmpty(player.player_name) && String.IsNullOrEmpty(player.player_guid) & String.IsNullOrEmpty(player.player_ip))) {
                ConsoleError("Attempted to update player without required information.");
            }
            else {
                try {
                    using (MySqlConnection connection = GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            //Set the insert command structure
                            command.CommandText = @"
                            UPDATE `" + _mySqlDatabaseName + @"`.`tbl_playerdata` SET 
                                `SoldierName` = '" + player.player_name + @"',
                                `EAGUID` = '" + player.player_guid + @"',
                                `IP_Address` = '" + player.player_ip + @"'
                            WHERE `tbl_playerdata`.`PlayerID` = " + player.player_id;
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() > 0) {
                                DebugWrite("Update player info success.", 5);
                            }
                        }
                    }
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error while updating player.", e));
                }
            }
            DebugWrite("updatePlayer finished!", 6);
            return player;
        }

        //DONE
        private List<AdKatsBan> FetchPlayerBans(AdKatsPlayer player) {
            DebugWrite("fetchPlayerBan starting!", 6);
            var aBanList = new List<AdKatsBan>();
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return null;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
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
                            `adkats_bans`.`ban_status` = 'Active' ";
                        if (_gameID > 0) {
                            query += " AND `tbl_playerdata`.`GameID` = " + _gameID;
                        }
                        query += " AND (";
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
                            HandleException(new AdKatsException("No data to fetch ban with. This should never happen."));
                            return aBanList;
                        }
                        query += ")";

                        //Assign the query
                        command.CommandText = query;

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                //Create the ban element
                                var aBan = new AdKatsBan {
                                    ban_id = reader.GetInt64("ban_id"),
                                    ban_status = reader.GetString("ban_status"),
                                    ban_notes = reader.GetString("ban_notes"),
                                    ban_sync = reader.GetString("ban_sync"),
                                    ban_startTime = reader.GetDateTime("ban_startTime"),
                                    ban_endTime = reader.GetDateTime("ban_endTime"),
                                    ban_enforceName = (reader.GetString("ban_enforceName") == "Y"),
                                    ban_enforceGUID = (reader.GetString("ban_enforceGUID") == "Y"),
                                    ban_enforceIP = (reader.GetString("ban_enforceIP") == "Y"),
                                    ban_record = FetchRecordByID(reader.GetInt64("latest_record_id"), false)
                                };
                                if (aBan.ban_endTime.Subtract(DateTime.UtcNow).TotalSeconds < 0) {
                                    aBan.ban_status = "Expired";
                                    UpdateBanStatus(aBan);
                                }
                                else if (String.IsNullOrEmpty(player.player_name_previous) && aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP) {
                                    var record = new AdKatsRecord {
                                        record_source = AdKatsRecord.Sources.InternalAutomated,
                                        server_id = _serverID,
                                        command_type = _CommandKeyDictionary["player_unban"],
                                        command_numeric = 0,
                                        target_name = player.player_name,
                                        target_player = player,
                                        source_name = "BanEnforcer",
                                        record_message = "Name-Banned player has changed their name. (" + player.player_name_previous + " -> " + player.player_name + ")"
                                    };
                                    QueueRecordForProcessing(record);
                                }
                                else if (_serverGroup == FetchServerGroup(aBan.ban_record.server_id) && aBan.ban_startTime < DateTime.UtcNow) {
                                    aBanList.Add(aBan);
                                }
                            }
                            if (aBanList.Count > 1) {
                                ConsoleWarn("Multiple bans matched player information, multiple accounts detected.");
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching player ban.", e));
            }
            return aBanList;
        }

        //DONE
        private void RepopulateProconBanList() {
            DebugWrite("repopulateProconBanList starting!", 6);
            ConsoleWarn("Downloading bans from database, please wait. This might take several minutes depending on your ban count!");

            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return;
            }
            Double totalBans = 0;
            Double bansDownloaded = 0;
            Double bansRepopulated = 0;
            Boolean earlyExit = false;
            DateTime startTime = DateTime.UtcNow;

            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
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

                        var importedBans = new List<AdKatsBan>();
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            //Loop through all incoming bans
                            while (reader.Read()) {
                                //Break from the loop if the plugin is disabled or the setting is reverted.
                                if (!_pluginEnabled || _UseBanEnforcer) {
                                    ConsoleWarn("You exited the ban download process early, the process was not completed.");
                                    earlyExit = true;
                                    break;
                                }
                                //Create the ban element
                                var aBan = new AdKatsBan {
                                    ban_id = reader.GetInt64("ban_id"),
                                    ban_status = reader.GetString("ban_status"),
                                    ban_notes = reader.GetString("ban_notes"),
                                    ban_sync = reader.GetString("ban_sync"),
                                    ban_startTime = reader.GetDateTime("ban_startTime"),
                                    ban_endTime = reader.GetDateTime("ban_endTime"),
                                    ban_record = FetchRecordByID(reader.GetInt64("latest_record_id"), false),
                                    ban_enforceName = (reader.GetString("ban_enforceName") == "Y"),
                                    ban_enforceGUID = (reader.GetString("ban_enforceGUID") == "Y"),
                                    ban_enforceIP = (reader.GetString("ban_enforceIP") == "Y")
                                };
                                importedBans.Add(aBan);

                                if (++bansDownloaded % 15 == 0) {
                                    ConsoleWrite(Math.Round(100 * bansDownloaded / totalBans, 2) + "% of bans downloaded. AVG " + Math.Round(bansDownloaded / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " downloads/sec.");
                                }
                            }
                        }
                        if (importedBans.Count > 0) {
                            ConsoleWarn(importedBans.Count + " bans downloaded, beginning repopulation to ban list.");
                        }
                        startTime = DateTime.UtcNow;
                        foreach (AdKatsBan aBan in importedBans) {
                            //Get the record information
                            var totalBanSeconds = (long) aBan.ban_endTime.Subtract(DateTime.UtcNow).TotalSeconds;
                            if (totalBanSeconds > 0) {
                                DebugWrite("Re-ProconBanning: " + aBan.ban_record.target_player.player_name + " for " + totalBanSeconds + "sec for " + aBan.ban_record.record_message, 4);

                                //Push the id ban
                                if (aBan.ban_enforceName) {
                                    Thread.Sleep(75);
                                    //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                    if (totalBanSeconds > 0 && totalBanSeconds < 31536000) {
                                        ExecuteCommand("procon.protected.send", "banList.add", "id", aBan.ban_record.target_player.player_name, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                    }
                                    else {
                                        ExecuteCommand("procon.protected.send", "banList.add", "id", aBan.ban_record.target_player.player_name, "perm", aBan.ban_record.record_message);
                                    }
                                }

                                //Push the guid ban
                                if (aBan.ban_enforceGUID) {
                                    Thread.Sleep(75);
                                    //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                    if (totalBanSeconds > 0 && totalBanSeconds < 31536000) {
                                        ExecuteCommand("procon.protected.send", "banList.add", "guid", aBan.ban_record.target_player.player_guid, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                    }
                                    else {
                                        ExecuteCommand("procon.protected.send", "banList.add", "guid", aBan.ban_record.target_player.player_guid, "perm", aBan.ban_record.record_message);
                                    }
                                }

                                //Push the IP ban
                                if (aBan.ban_enforceIP) {
                                    Thread.Sleep(75);
                                    //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                    if (totalBanSeconds > 0 && totalBanSeconds < 31536000) {
                                        ExecuteCommand("procon.protected.send", "banList.add", "ip", aBan.ban_record.target_player.player_ip, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                    }
                                    else {
                                        ExecuteCommand("procon.protected.send", "banList.add", "ip", aBan.ban_record.target_player.player_ip, "perm", aBan.ban_record.record_message);
                                    }
                                }
                            }

                            if (++bansRepopulated % 15 == 0) {
                                ConsoleWrite(Math.Round(100 * bansRepopulated / totalBans, 2) + "% of bans repopulated. AVG " + Math.Round(bansRepopulated / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " downloads/sec.");
                            }
                        }
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                        if (!earlyExit) {
                            ConsoleSuccess("All AdKats Enforced bans repopulated to procon's ban list.");
                        }

                        //Update the last db ban fetch time
                        _LastDbBanFetch = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while repopulating procon banlist.", e));
            }
        }

        //DONE
        private Boolean UpdateBanStatus(AdKatsBan aBan) {
            DebugWrite("updateBanStatus starting!", 6);
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return false;
            }

            Boolean success = false;
            if (aBan == null) {
                ConsoleError("Ban invalid in updateBanStatus.");
            }
            else {
                try {
                    //Conditionally modify the ban_sync for this server
                    if (!aBan.ban_sync.Contains("*" + _serverID + "*")) {
                        aBan.ban_sync += ("*" + _serverID + "*");
                    }

                    using (MySqlConnection connection = GetDatabaseConnection()) {
                        using (MySqlCommand command = connection.CreateCommand()) {
                            String query = @"
                            UPDATE 
                            `" + _mySqlDatabaseName + @"`.`adkats_bans` 
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
                    HandleException(new AdKatsException("Error while updating status of ban.", e));
                }
            }

            DebugWrite("updateBanStatus finished!", 6);
            return success;
        }

        //DONE
        private void ImportBansFromBBM5108() {
            //Check if tables exist from BF3 Ban Manager
            if (!ConfirmTable("bm_banlist")) {
                return;
            }
            ConsoleWarn("BF3 Ban Manager tables detected. Checking validity....");

            //Check if any BBM5108 bans exist in the AdKats Banlist
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            * 
                        FROM 
                            `" + _mySqlDatabaseName + @"`.`adkats_bans` 
                        WHERE 
                            `adkats_bans`.`ban_notes` = 'BBM5108' 
                        LIMIT 1";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                ConsoleWarn("BF3 Ban Manager bans already imported, canceling import.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while checking for BBM Bans.", e));
                return;
            }

            ConsoleWarn("Validity confirmed. Preparing to fetch all BF3 Ban Manager Bans...");
            Double totalBans = 0;
            Double bansImported = 0;
            var inboundBBMBans = new Queue<BBM5108Ban>();
            DateTime startTime = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        DebugWrite("Creating query to import BBM5108", 3);
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
                                    DebugWrite("BBM5108 bans found, grabbing.", 3);
                                    told = true;
                                }
                                var bbmBan = new BBM5108Ban {
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
                HandleException(new AdKatsException("Error while fetching BBM Bans.", e));
                return;
            }
            ConsoleWarn(totalBans + " Ban Manager bans fetched, starting import to AdKats Ban Enforcer...");

            try {
                //Loop through all BBMBans in order that they came in
                while (inboundBBMBans.Count > 0) {
                    //Break from the loop if the plugin is disabled or the setting is reverted.
                    if (!_pluginEnabled || !_UseBanEnforcer) {
                        ConsoleError("You exited the ban import process process early, the process was not completed and cannot recover without manual override. Talk to ColColonCleaner.");
                        break;
                    }

                    BBM5108Ban bbmBan = inboundBBMBans.Dequeue();

                    //Create the record
                    var record = new AdKatsRecord();
                    //Fetch the player
                    record.target_player = FetchPlayer(true, true, null, -1, bbmBan.soldiername, bbmBan.eaguid, null);

                    record.record_source = AdKatsRecord.Sources.InternalAutomated;
                    if (bbmBan.ban_length == "permanent") {
                        DebugWrite("Ban is permanent", 4);
                        record.command_type = _CommandKeyDictionary["player_ban_perm"];
                        record.command_action = _CommandKeyDictionary["player_ban_perm"];
                        record.command_numeric = 0;
                    }
                    else if (bbmBan.ban_length == "seconds") {
                        DebugWrite("Ban is temporary", 4);
                        record.command_type = _CommandKeyDictionary["player_ban_temp"];
                        record.command_action = _CommandKeyDictionary["player_ban_temp"];
                        record.command_numeric = (Int32) (bbmBan.ban_duration - DateTime.UtcNow).TotalMinutes;
                    }
                    else {
                        //Ignore all other cases e.g. round bans
                        DebugWrite("Ban type '" + bbmBan.ban_length + "' not usable", 3);
                        continue;
                    }

                    record.source_name = "BanEnforcer";
                    record.server_id = _serverID;
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
                    var aBan = new AdKatsBan {
                        ban_record = record,
                        ban_notes = "BBM5108",
                        ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable) || !String.IsNullOrEmpty(bbmBan.soldiername)),
                        ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable) || !String.IsNullOrEmpty(bbmBan.eaguid)),
                        ban_enforceIP = ipAvailable && _DefaultEnforceIP
                    };
                    if (!aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP) {
                        ConsoleError("Unable to create ban, no proper player information");
                        continue;
                    }

                    //Upload the ban
                    DebugWrite("Uploading Ban Manager ban.", 5);
                    UploadBan(aBan);

                    if (++bansImported % 25 == 0) {
                        ConsoleWrite(Math.Round(100 * bansImported / totalBans, 2) + "% of Ban Manager bans uploaded. AVG " + Math.Round(bansImported / ((DateTime.UtcNow - startTime).TotalSeconds), 2) + " uploads/sec.");
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while processing imported BBM Bans to AdKats banlist.", e));
                return;
            }
            if (inboundBBMBans.Count == 0) {
                ConsoleSuccess("All Ban Manager bans imported into AdKats Ban Enforcer!");
            }
        }

        //Done
        private Boolean CanPunish(AdKatsRecord record, Int32 duration) {
            DebugWrite("canPunish starting!", 6);
            if (duration < 1) {
                ConsoleError("CanPunish duration must be positive.");
                return false;
            }
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return false;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            `record_time` AS `latest_time` 
                        FROM 
                            `adkats_records_main` 
                        WHERE 
                            `adkats_records_main`.`command_type` = " + GetCommandByKey("player_punish").command_id + @" 
                        AND 
                            `adkats_records_main`.`target_id` = " + record.target_player.player_id + @" 
                        AND 
                            DATE_ADD(`record_time`, INTERVAL " + duration + @" SECOND) > UTC_TIMESTAMP() 
                        ORDER BY 
                            `record_time` 
                        DESC LIMIT 1";

                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                DebugWrite("can't upload punish", 6);
                                return false;
                            }
                            return true;
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while checking if player can be punished.", e));
                //Assume false if any errors
                return false;
            }
        }

        //DONE
        private Boolean FetchIROStatus(AdKatsRecord record) {
            DebugWrite("FetchIROStatus starting!", 6);
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                record.record_exception = new AdKatsException("Database not connected.");
                return false;
            }

            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
	                        `record_time` AS `latest_time` 
                        FROM 
	                        `adkats_records_main`
                        INNER JOIN
	                        `adkats_commands`
                        ON
	                        `adkats_records_main`.`command_type` = `adkats_commands`.`command_id`
                        WHERE 
	                        `adkats_commands`.`command_key` = 'player_punish' 
                        AND 
                            `adkats_records_main`.`target_id` = " + record.target_player.player_id + @" 
                        AND 
                            DATE_ADD(`record_time`, INTERVAL " + _IROTimeout + @" MINUTE) > UTC_TIMESTAMP() 
                        ORDER BY 
                            `record_time` 
                        DESC LIMIT 1";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                DebugWrite("Punish is Double counted", 6);
                                return true;
                            }
                            return false;
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while checking if punish will be IRO.", e));
                //Assume false if any errors
                return false;
            }
        }

        //DONE
        private void RunActionsFromDB() {
            DebugWrite("runActionsFromDB starting!", 7);
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                foreach (AdKatsRecord record in FetchUnreadRecords()) {
                    QueueRecordForActionHandling(record);
                }
                //Update the last time this was fetched
                _lastDbActionFetch = DateTime.UtcNow;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing unread records for action handling.", e));
            }
        }

        //DONE
        private Int32 FetchPoints(AdKatsPlayer player, Boolean combineOverride) {
            DebugWrite("fetchPoints starting!", 6);

            Int32 returnVal = -1;
            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return returnVal;
            }

            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        if (_CombineServerPunishments || combineOverride) {
                            command.CommandText = @"SELECT `total_points` FROM `" + _mySqlDatabaseName + @"`.`adkats_infractions_global` WHERE `player_id` = @player_id";
                            command.Parameters.AddWithValue("@player_id", player.player_id);
                        }
                        else {
                            command.CommandText = @"SELECT `total_points` FROM `" + _mySqlDatabaseName + @"`.`adkats_infractions_server` WHERE `player_id` = @player_id and `server_id` = @server_id";
                            command.Parameters.AddWithValue("@player_id", player.player_id);
                            command.Parameters.AddWithValue("@server_id", _serverID);
                        }
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            returnVal = reader.Read() ? reader.GetInt32("total_points") : 0;
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while getting infraction points for player.", e));
            }
            DebugWrite("fetchPoints finished!", 6);
            return (returnVal > 0) ? (returnVal) : (0);
        }

        private void FetchCommands() {
            DebugWrite("fetchCommands starting!", 6);
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_CommandIDDictionary) {
                    using (MySqlConnection connection = GetDatabaseConnection()) {
                        using (MySqlCommand sqlcommand = connection.CreateCommand()) {
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
                            var validIDs = new HashSet<Int64>();
                            using (MySqlDataReader reader = sqlcommand.ExecuteReader()) {
                                _CommandKeyDictionary.Clear();
                                _CommandNameDictionary.Clear();
                                _CommandTextDictionary.Clear();
                                while (reader.Read()) {
                                    //ID is the immutable element
                                    int commandID = reader.GetInt32("command_id");
                                    var commandActive = (AdKatsCommand.CommandActive) Enum.Parse(typeof (AdKatsCommand.CommandActive), reader.GetString("command_active"));
                                    string commandKey = reader.GetString("command_key");
                                    var commandLogging = (AdKatsCommand.CommandLogging) Enum.Parse(typeof (AdKatsCommand.CommandLogging), reader.GetString("command_logging"));
                                    string commandName = reader.GetString("command_name");
                                    string commandText = reader.GetString("command_text");
                                    bool commandPlayerInteraction = reader.GetBoolean("command_playerInteraction");

                                    validIDs.Add(commandID);
                                    AdKatsCommand currentCommand;
                                    if (_CommandIDDictionary.TryGetValue(commandID, out currentCommand)) {
                                        if (!currentCommand.command_active.Equals(commandActive)) {
                                            ConsoleWarn(currentCommand.command_key + " active state being changed from " + currentCommand.command_active + " to " + commandActive);
                                            currentCommand.command_active = commandActive;
                                        }
                                        if (currentCommand.command_key != commandKey) {
                                            ConsoleWarn(currentCommand.command_key + " command key being changed from " + currentCommand.command_key + " to " + commandKey);
                                            currentCommand.command_key = commandKey;
                                        }
                                        if (!currentCommand.command_logging.Equals((commandLogging))) {
                                            ConsoleWarn(currentCommand.command_key + " logging state being changed from " + currentCommand.command_logging + " to " + commandLogging);
                                            currentCommand.command_logging = commandLogging;
                                        }
                                        if (currentCommand.command_name != commandName) {
                                            ConsoleWarn(currentCommand.command_key + " command name being changed from " + currentCommand.command_name + " to " + commandName);
                                            currentCommand.command_name = commandName;
                                        }
                                        if (currentCommand.command_text != commandText) {
                                            ConsoleWarn(currentCommand.command_key + " command text being changed from " + currentCommand.command_text + " to " + commandText);
                                            currentCommand.command_text = commandText;
                                        }
                                        if (currentCommand.command_playerInteraction != commandPlayerInteraction) {
                                            ConsoleWarn(currentCommand.command_key + " player interaction state being changed from " + currentCommand.command_playerInteraction + " to " + commandPlayerInteraction);
                                            currentCommand.command_playerInteraction = commandPlayerInteraction;
                                        }
                                    }
                                    else {
                                        currentCommand = new AdKatsCommand {
                                            command_id = commandID,
                                            command_active = commandActive,
                                            command_key = commandKey,
                                            command_logging = commandLogging,
                                            command_name = commandName,
                                            command_text = commandText,
                                            command_playerInteraction = commandPlayerInteraction
                                        };
                                        _CommandIDDictionary.Add(currentCommand.command_id, currentCommand);
                                    }
                                    _CommandKeyDictionary.Add(currentCommand.command_key, currentCommand);
                                    _CommandNameDictionary.Add(currentCommand.command_name, currentCommand);
                                    _CommandTextDictionary.Add(currentCommand.command_text, currentCommand);
                                    _commandUsageTimes[currentCommand.command_key] = DateTime.UtcNow;
                                }
                            }
                            if (_CommandIDDictionary.Count > 0) {
                                foreach (AdKatsCommand remCommand in _CommandIDDictionary.Values.Where(aRole => !validIDs.Contains(aRole.command_id)).ToList()) {
                                    ConsoleWarn("Removing command " + remCommand.command_key);
                                    _CommandIDDictionary.Remove(remCommand.command_id);
                                }
                                if (!_CommandIDDictionary.ContainsKey(1)) {
                                    SendNonQuery("Adding command 1", "REPLACE INTO `adkats_commands` VALUES(1, 'Active', 'command_confirm', 'Unable', 'Confirm Command', 'yes', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(2)) {
                                    SendNonQuery("Adding command 2", "REPLACE INTO `adkats_commands` VALUES(2, 'Active', 'command_cancel', 'Unable', 'Cancel Command', 'no', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(3)) {
                                    SendNonQuery("Adding command 3", "REPLACE INTO `adkats_commands` VALUES(3, 'Active', 'player_kill', 'Log', 'Kill Player', 'kill', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(4)) {
                                    SendNonQuery("Adding command 4", "REPLACE INTO `adkats_commands` VALUES(4, 'Invisible', 'player_kill_lowpop', 'Log', 'Kill Player (Low Population)', 'lowpopkill', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(5)) {
                                    SendNonQuery("Adding command 5", "REPLACE INTO `adkats_commands` VALUES(5, 'Invisible', 'player_kill_repeat', 'Log', 'Kill Player (Repeat Kill)', 'repeatkill', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(6)) {
                                    SendNonQuery("Adding command 6", "REPLACE INTO `adkats_commands` VALUES(6, 'Active', 'player_kick', 'Log', 'Kick Player', 'kick', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(7)) {
                                    SendNonQuery("Adding command 7", "REPLACE INTO `adkats_commands` VALUES(7, 'Active', 'player_ban_temp', 'Log', 'Temp-Ban Player', 'tban', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(8)) {
                                    SendNonQuery("Adding command 8", "REPLACE INTO `adkats_commands` VALUES(8, 'Active', 'player_ban_perm', 'Log', 'Permaban Player', 'ban', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(9)) {
                                    SendNonQuery("Adding command 9", "REPLACE INTO `adkats_commands` VALUES(9, 'Active', 'player_punish', 'Mandatory', 'Punish Player', 'punish', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(10)) {
                                    SendNonQuery("Adding command 10", "REPLACE INTO `adkats_commands` VALUES(10, 'Active', 'player_forgive', 'Mandatory', 'Forgive Player', 'forgive', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(11)) {
                                    SendNonQuery("Adding command 11", "REPLACE INTO `adkats_commands` VALUES(11, 'Active', 'player_mute', 'Log', 'Mute Player', 'mute', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(12)) {
                                    SendNonQuery("Adding command 12", "REPLACE INTO `adkats_commands` VALUES(12, 'Active', 'player_join', 'Log', 'Join Player', 'join', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(13)) {
                                    SendNonQuery("Adding command 13", "REPLACE INTO `adkats_commands` VALUES(13, 'Active', 'player_roundwhitelist', 'Ignore', 'Round Whitelist Player', 'roundwhitelist', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(14)) {
                                    SendNonQuery("Adding command 14", "REPLACE INTO `adkats_commands` VALUES(14, 'Active', 'player_move', 'Log', 'On-Death Move Player', 'move', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(15)) {
                                    SendNonQuery("Adding command 15", "REPLACE INTO `adkats_commands` VALUES(15, 'Active', 'player_fmove', 'Log', 'Force Move Player', 'fmove', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(16)) {
                                    SendNonQuery("Adding command 16", "REPLACE INTO `adkats_commands` VALUES(16, 'Active', 'self_teamswap', 'Log', 'Teamswap Self', 'moveme', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(17)) {
                                    SendNonQuery("Adding command 17", "REPLACE INTO `adkats_commands` VALUES(17, 'Active', 'self_kill', 'Log', 'Kill Self', 'killme', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(18)) {
                                    SendNonQuery("Adding command 18", "REPLACE INTO `adkats_commands` VALUES(18, 'Active', 'player_report', 'Log', 'Report Player', 'report', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(19)) {
                                    SendNonQuery("Adding command 19", "REPLACE INTO `adkats_commands` VALUES(19, 'Invisible', 'player_report_confirm', 'Log', 'Report Player (Confirmed)', 'confirmreport', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(20)) {
                                    SendNonQuery("Adding command 20", "REPLACE INTO `adkats_commands` VALUES(20, 'Active', 'player_calladmin', 'Log', 'Call Admin on Player', 'admin', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(21)) {
                                    SendNonQuery("Adding command 21", "REPLACE INTO `adkats_commands` VALUES(21, 'Active', 'admin_say', 'Log', 'Admin Say', 'say', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(22)) {
                                    SendNonQuery("Adding command 22", "REPLACE INTO `adkats_commands` VALUES(22, 'Active', 'player_say', 'Log', 'Player Say', 'psay', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(23)) {
                                    SendNonQuery("Adding command 23", "REPLACE INTO `adkats_commands` VALUES(23, 'Active', 'admin_yell', 'Log', 'Admin Yell', 'yell', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(24)) {
                                    SendNonQuery("Adding command 24", "REPLACE INTO `adkats_commands` VALUES(24, 'Active', 'player_yell', 'Log', 'Player Yell', 'pyell', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(25)) {
                                    SendNonQuery("Adding command 25", "REPLACE INTO `adkats_commands` VALUES(25, 'Active', 'admin_tell', 'Log', 'Admin Tell', 'tell', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(26)) {
                                    SendNonQuery("Adding command 26", "REPLACE INTO `adkats_commands` VALUES(26, 'Active', 'player_tell', 'Log', 'Player Tell', 'ptell', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(27)) {
                                    SendNonQuery("Adding command 27", "REPLACE INTO `adkats_commands` VALUES(27, 'Active', 'self_whatis', 'Unable', 'What Is', 'whatis', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(28)) {
                                    SendNonQuery("Adding command 28", "REPLACE INTO `adkats_commands` VALUES(28, 'Active', 'self_voip', 'Unable', 'VOIP', 'voip', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(29)) {
                                    SendNonQuery("Adding command 29", "REPLACE INTO `adkats_commands` VALUES(29, 'Active', 'self_rules', 'Log', 'Request Rules', 'rules', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(30)) {
                                    SendNonQuery("Adding command 30", "REPLACE INTO `adkats_commands` VALUES(30, 'Active', 'round_restart', 'Log', 'Restart Current Round', 'restart', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(31)) {
                                    SendNonQuery("Adding command 31", "REPLACE INTO `adkats_commands` VALUES(31, 'Active', 'round_next', 'Log', 'Run Next Round', 'nextlevel', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(32)) {
                                    SendNonQuery("Adding command 32", "REPLACE INTO `adkats_commands` VALUES(32, 'Active', 'round_end', 'Log', 'End Current Round', 'endround', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(33)) {
                                    SendNonQuery("Adding command 33", "REPLACE INTO `adkats_commands` VALUES(33, 'Active', 'server_nuke', 'Log', 'Server Nuke', 'nuke', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(34)) {
                                    SendNonQuery("Adding command 34", "REPLACE INTO `adkats_commands` VALUES(34, 'Active', 'server_kickall', 'Log', 'Kick All Guests', 'kickall', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(35)) {
                                    SendNonQuery("Adding command 35", "REPLACE INTO `adkats_commands` VALUES(35, 'Invisible', 'adkats_exception', 'Mandatory', 'Logged Exception', 'logexception', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(36)) {
                                    SendNonQuery("Adding command 36", "REPLACE INTO `adkats_commands` VALUES(36, 'Invisible', 'banenforcer_enforce', 'Mandatory', 'Enforce Active Ban', 'enforceban', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(37)) {
                                    SendNonQuery("Adding command 37", "REPLACE INTO `adkats_commands` VALUES(37, 'Active', 'player_unban', 'Log', 'Unban Player', 'unban', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(38)) {
                                    SendNonQuery("Adding command 38", "REPLACE INTO `adkats_commands` VALUES(38, 'Active', 'self_admins', 'Log', 'Request Online Admins', 'admins', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(39)) {
                                    SendNonQuery("Adding command 39", "REPLACE INTO `adkats_commands` VALUES(39, 'Active', 'self_lead', 'Log', 'Lead Current Squad', 'lead', FALSE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(40)) {
                                    SendNonQuery("Adding command 40", "REPLACE INTO `adkats_commands` VALUES(40, 'Active', 'admin_accept', 'Log', 'Accept Round Report', 'accept', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(41)) {
                                    SendNonQuery("Adding command 41", "REPLACE INTO `adkats_commands` VALUES(41, 'Active', 'admin_deny', 'Log', 'Deny Round Report', 'deny', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(42)) {
                                    SendNonQuery("Adding command 42", "REPLACE INTO `adkats_commands` VALUES(42, 'Invisible', 'player_report_deny', 'Log', 'Report Player (Denied)', 'denyreport', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(43)) {
                                    SendNonQuery("Adding command 43", "REPLACE INTO `adkats_commands` VALUES(43, 'Active', 'server_swapnuke', 'Log', 'SwapNuke Server', 'swapnuke', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(44)) {
                                    SendNonQuery("Adding command 44", "REPLACE INTO `adkats_commands` VALUES(44, 'Active', 'player_blacklistdisperse', 'Log', 'Blacklist Disperse Player', 'disperse', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(45)) {
                                    SendNonQuery("Adding command 45", "REPLACE INTO `adkats_commands` VALUES(45, 'Active', 'player_whitelistbalance', 'Log', 'Autobalance Whitelist Player', 'whitelist', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(46)) {
                                    SendNonQuery("Adding command 46", "REPLACE INTO `adkats_commands` VALUES(46, 'Active', 'player_slotreserved', 'Log', 'Reserved Slot Player', 'reserved', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(47)) {
                                    SendNonQuery("Adding command 47", "REPLACE INTO `adkats_commands` VALUES(47, 'Active', 'player_slotspectator', 'Log', 'Spectator Slot Player', 'spectator', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(48)) {
                                    SendNonQuery("Adding command 48", "REPLACE INTO `adkats_commands` VALUES(48, 'Invisible', 'player_changename', 'Log', 'Player Changed Name', 'changename', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(49)) {
                                    SendNonQuery("Adding command 49", "REPLACE INTO `adkats_commands` VALUES(49, 'Invisible', 'player_changeip', 'Log', 'Player Changed IP', 'changeip', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(50)) {
                                    SendNonQuery("Adding command 50", "REPLACE INTO `adkats_commands` VALUES(50, 'Active', 'player_ban_perm_future', 'Log', 'Future Permaban Player', 'fban', TRUE)", true);
                                }
                                if (!_CommandIDDictionary.ContainsKey(51)) {
                                    SendNonQuery("Adding command 51", "REPLACE INTO `adkats_commands` VALUES(51, 'Active', 'self_assist', 'Log', 'Assist Losing Team', 'assist', FALSE)", true);
                                }
                                SendNonQuery("Updating command 51 player interaction", "UPDATE `adkats_commands` SET `command_playerInteraction`=0 WHERE `command_id`=51", false);
                                if (!_CommandIDDictionary.ContainsKey(52)) {
                                    SendNonQuery("Adding command 52", "REPLACE INTO `adkats_commands` VALUES(52, 'Active', 'self_uptime', 'Log', 'Request Uptimes', 'uptime', FALSE)", true);
                                }
                            }
                            else {
                                ConsoleError("Commands could not be fetched.");
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching commands from database.", e));
            }
            UpdateSettingPage();
            DebugWrite("fetchCommands finished!", 6);
        }

        private void FetchRoles() {
            DebugWrite("fetchRoles starting!", 6);
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_RoleIDDictionary) {
                    using (MySqlConnection connection = GetDatabaseConnection()) {
                        using (MySqlCommand sqlcommand = connection.CreateCommand()) {
                            const string sql = @"
                            SELECT 
	                            `role_id`,
	                            `role_key`,
	                            `role_name`
                            FROM 
	                            `adkats_roles`";
                            sqlcommand.CommandText = sql;
                            var validIDs = new HashSet<Int64>();
                            using (MySqlDataReader reader = sqlcommand.ExecuteReader()) {
                                _RoleKeyDictionary.Clear();
                                _RoleNameDictionary.Clear();
                                while (reader.Read()) {
                                    long roleID = reader.GetInt64("role_id");
                                    string roleKey = reader.GetString("role_key");
                                    string roleName = reader.GetString("role_name");
                                    validIDs.Add(roleID);
                                    AdKatsRole currentRole;
                                    if (_RoleIDDictionary.TryGetValue(roleID, out currentRole)) {
                                        if (currentRole.role_key != roleKey) {
                                            ConsoleWarn(currentRole.role_key + " role key being changed from " + currentRole.role_key + " to " + roleKey);
                                            currentRole.role_key = roleKey;
                                        }
                                        if (currentRole.role_name != roleName) {
                                            ConsoleWarn(currentRole.role_key + " role name being changed from " + currentRole.role_name + " to " + roleName);
                                            currentRole.role_name = roleName;
                                        }
                                    }
                                    else {
                                        currentRole = new AdKatsRole {
                                            role_id = roleID,
                                            role_key = roleKey,
                                            role_name = roleName
                                        };
                                        _RoleIDDictionary.Add(currentRole.role_id, currentRole);
                                    }
                                    _RoleKeyDictionary.Add(currentRole.role_key, currentRole);
                                    _RoleNameDictionary.Add(currentRole.role_name, currentRole);
                                }
                                foreach (AdKatsRole remRole in _RoleIDDictionary.Values.Where(aRole => !validIDs.Contains(aRole.role_id)).ToList()) {
                                    ConsoleWarn("Removing role " + remRole.role_key);
                                    _RoleIDDictionary.Remove(remRole.role_id);
                                }
                            }
                        }
                        using (MySqlCommand sqlcommand = connection.CreateCommand()) {
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
                                var rIDcIDDictionary = new Dictionary<Int64, HashSet<Int64>>();
                                while (reader.Read()) {
                                    int roleID = reader.GetInt32("role_id");
                                    long commandID = reader.GetInt64("command_id");
                                    HashSet<Int64> allowedCommandIDs;
                                    if (!rIDcIDDictionary.TryGetValue(roleID, out allowedCommandIDs)) {
                                        allowedCommandIDs = new HashSet<Int64>();
                                        rIDcIDDictionary.Add(roleID, allowedCommandIDs);
                                    }
                                    allowedCommandIDs.Add(commandID);
                                }
                                foreach (var currentRoleElement in rIDcIDDictionary) {
                                    AdKatsRole aRole;
                                    if (!_RoleIDDictionary.TryGetValue(currentRoleElement.Key, out aRole)) {
                                        ConsoleError("Role for ID " + currentRoleElement.Key + " not found in role dictionary when assigning commands.");
                                        continue;
                                    }
                                    foreach (long curRoleID in currentRoleElement.Value) {
                                        AdKatsCommand aCommand;
                                        if (!_CommandIDDictionary.TryGetValue(curRoleID, out aCommand)) {
                                            ConsoleError("Command for ID " + curRoleID + " not found in command dictionary when assigning commands.");
                                            continue;
                                        }
                                        if (!aRole.RoleAllowedCommands.ContainsKey(aCommand.command_key)) {
                                            aRole.RoleAllowedCommands.Add(aCommand.command_key, aCommand);
                                        }
                                    }
                                    KeyValuePair<Int64, HashSet<Int64>> element = currentRoleElement;
                                    foreach (AdKatsCommand remCommand in aRole.RoleAllowedCommands.Values.ToList().Where(remCommand => !element.Value.Contains(remCommand.command_id))) {
                                        ConsoleWarn("Removing command " + remCommand.command_key + " from role " + aRole.role_key);
                                        aRole.RoleAllowedCommands.Remove(remCommand.command_key);
                                    }
                                    FillConditionalAllowedCommands(aRole);
                                }
                            }
                        }
                        if (_RoleIDDictionary.Count == 0) {
                            ConsoleError("Roles could not be fetched.");
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching roles from database.", e));
            }
            UpdateSettingPage();
            DebugWrite("fetchRoles finished!", 6);
        }

        private void FillConditionalAllowedCommands(AdKatsRole aRole) {
            //Teamswap Command
            AdKatsCommand teamswapCommand;
            if (_CommandKeyDictionary.TryGetValue("self_teamswap", out teamswapCommand)) {
                if (!aRole.ConditionalAllowedCommands.ContainsKey(teamswapCommand.command_key))
                    aRole.ConditionalAllowedCommands.Add(teamswapCommand.command_key, new KeyValuePair<Func<AdKats, AdKatsPlayer, Boolean>, AdKatsCommand>(AAPerkFunc, teamswapCommand));
            }
            else {
                ConsoleError("Unable to find teamswap command when assigning conditional commands.");
            }
            //Admins Command
            AdKatsCommand adminsCommand;
            if (_CommandKeyDictionary.TryGetValue("self_admins", out adminsCommand)) {
                if (!aRole.ConditionalAllowedCommands.ContainsKey(adminsCommand.command_key))
                    aRole.ConditionalAllowedCommands.Add(adminsCommand.command_key, new KeyValuePair<Func<AdKats, AdKatsPlayer, Boolean>, AdKatsCommand>(AAPerkFunc, adminsCommand));
            }
            else {
                ConsoleError("Unable to find teamswap command when assigning conditional commands.");
            }
        }

        private void FetchUserList() {
            DebugWrite("fetchUserList starting!", 6);
            if (HandlePossibleDisconnect()) {
                return;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"SELECT 
	                        `adkats_users`.`user_id`,
	                        `adkats_users`.`user_name`,
	                        `adkats_users`.`user_email`,
	                        `adkats_users`.`user_phone`,
	                        `adkats_users`.`user_role`
                        FROM 
	                        `adkats_users`";
                        var validIDs = new List<Int64>();
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_userCache) {
                                while (reader.Read()) {
                                    int userID = reader.GetInt32("user_id"); //0
                                    validIDs.Add(userID);
                                    string userName = reader.GetString("user_name"); //1
                                    String userEmail = null;
                                    if (!reader.IsDBNull(2))
                                        userEmail = reader.GetString("user_email"); //2
                                    String userPhone = null;
                                    if (!reader.IsDBNull(3))
                                        userPhone = reader.GetString("user_phone"); //3
                                    AdKatsRole userRole;
                                    if (!_RoleIDDictionary.TryGetValue(reader.GetInt32("user_role"), out userRole)) {
                                        ConsoleError("Unable to find user role for role ID " + reader.GetInt32("user_role"));
                                        continue;
                                    }
                                    AdKatsUser aUser;
                                    if (_userCache.TryGetValue(userID, out aUser)) {
                                        aUser.user_name = userName;
                                        aUser.user_email = userEmail;
                                        aUser.user_phone = userPhone;
                                        aUser.user_role = userRole;
                                    }
                                    else {
                                        aUser = new AdKatsUser {
                                            user_id = userID,
                                            user_name = userName,
                                            user_email = userEmail,
                                            user_phone = userPhone,
                                            user_role = userRole
                                        };
                                        _userCache.Add(aUser.user_id, aUser);
                                    }
                                }
                                foreach (AdKatsUser remUser in _userCache.Values.Where(usr => validIDs.All(id => id != usr.user_id)).ToList()) {
                                    _userCache.Remove(remUser.user_id);
                                    ConsoleSuccess("User " + remUser.user_name + " removed.");
                                }
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
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_userCache) {
                                foreach (AdKatsPlayer aPlayer in _userCache.Values.SelectMany(aUser => aUser.soldierDictionary.Values)) {
                                    aPlayer.update_playerUpdated = false;
                                }
                                while (reader.Read()) {
                                    int userID = reader.GetInt32("user_id"); //0
                                    int playerID = reader.GetInt32("player_id"); //1
                                    int gameID = reader.GetInt32("game_id"); //2
                                    String clanTag = null;
                                    if (!reader.IsDBNull(3))
                                        clanTag = reader.GetString("clan_tag"); //3
                                    String playerName = null;
                                    if (!reader.IsDBNull(4))
                                        playerName = reader.GetString("player_name"); //4
                                    String playerGUID = null;
                                    if (!reader.IsDBNull(5))
                                        playerGUID = reader.GetString("player_guid"); //5
                                    String playerIP = null;
                                    if (!reader.IsDBNull(6))
                                        playerIP = reader.GetString("player_ip"); //6

                                    AdKatsUser aUser;
                                    if (_userCache.TryGetValue(userID, out aUser)) {
                                        AdKatsPlayer aPlayer;
                                        if (aUser.soldierDictionary.TryGetValue(playerID, out aPlayer)) {
                                            aPlayer.game_id = gameID;
                                            aPlayer.clan_tag = clanTag;
                                            aPlayer.player_name = playerName;
                                            aPlayer.player_guid = playerGUID;
                                            aPlayer.player_ip = playerIP;
                                        }
                                        else {
                                            aPlayer = new AdKatsPlayer {
                                                player_id = playerID,
                                                game_id = gameID,
                                                clan_tag = clanTag,
                                                player_name = playerName,
                                                player_guid = playerGUID,
                                                player_ip = playerIP
                                            };
                                            aUser.soldierDictionary.Add(playerID, aPlayer);
                                        }
                                        aPlayer.player_role = aUser.user_role;

                                        aPlayer.update_playerUpdated = true;
                                    }
                                    else {
                                        ConsoleError("Unable to add soldier " + playerID + " to user " + userID + " when fetching user list. User not found.");
                                    }
                                }
                                foreach (AdKatsUser aUser in _userCache.Values) {
                                    foreach (AdKatsPlayer aPlayer in aUser.soldierDictionary.Values.Where(aPlayer => !aPlayer.update_playerUpdated)) {
                                        aUser.soldierDictionary.Remove(aPlayer.player_id);
                                    }
                                }
                            }
                        }
                    }
                    var tempSpecialPlayerCache = new List<AdKatsSpecialPlayer>();
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT
	                        `player_group`,
	                        `player_id`,
	                        `player_game`,
	                        `player_server`,
	                        `player_identifier`
                        FROM 
	                        `adkats_specialplayers`
                        ORDER BY 
	                        `player_group`
                        DESC";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            while (reader.Read()) {
                                var asPlayer = new AdKatsSpecialPlayer();
                                asPlayer.player_group = reader.GetString("player_group"); //0
                                if (!reader.IsDBNull(1))
                                    asPlayer.player_object = FetchPlayer(false, false, null, reader.GetInt32("player_id"), null, null, null); //1
                                if (!reader.IsDBNull(2))
                                    asPlayer.player_game = reader.GetInt32("player_game"); //2
                                if (!reader.IsDBNull(3))
                                    asPlayer.player_server = reader.GetInt32("player_server"); //3
                                if (!reader.IsDBNull(4))
                                    asPlayer.player_identifier = reader.GetString("player_identifier"); //4
                                //Check if special player applies to this server
                                Boolean allowed = true;
                                if (asPlayer.player_object == null) {
                                    //If they didn't define a player, they must define an identifier
                                    if (String.IsNullOrEmpty(asPlayer.player_identifier)) {
                                        allowed = false;
                                    }
                                    //Did they define a game for the special player?
                                    if (asPlayer.player_game != null) {
                                        //They did, only use if the given game id is this server's game id
                                        if (asPlayer.player_game != _gameID) {
                                            allowed = false;
                                        }
                                    }
                                }
                                else if (asPlayer.player_object.game_id != _gameID) {
                                    allowed = false;
                                }
                                //Did they define a server for the special player?
                                if (asPlayer.player_server != null) {
                                    //They did, only use if the given server id is this server's server id
                                    if (asPlayer.player_server != _serverID) {
                                        allowed = false;
                                    }
                                }
                                if (allowed) {
                                    //Add the user to temp list
                                    tempSpecialPlayerCache.Add(asPlayer);
                                }
                            }
                        }
                    }
                    //Update the special player cache
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_specialPlayerGroupCache) {
                        _specialPlayerGroupCache.Clear();
                        foreach (AdKatsSpecialPlayer asPlayer in tempSpecialPlayerCache) {
                            List<AdKatsSpecialPlayer> currentList;
                            if (_specialPlayerGroupCache.TryGetValue(asPlayer.player_group, out currentList)) {
                                currentList.Add(asPlayer);
                            }
                            else {
                                currentList = new List<AdKatsSpecialPlayer> {
                                    asPlayer
                                };
                                _specialPlayerGroupCache.Add(asPlayer.player_group, currentList);
                            }
                        }
                        //Debug logging purposes only
                        if (_DebugLevel > 4) {
                            foreach (string key in _specialPlayerGroupCache.Keys) {
                                List<AdKatsSpecialPlayer> asPlayerList;
                                if (_specialPlayerGroupCache.TryGetValue(key, out asPlayerList)) {
                                    DebugWrite("SPECIAL: List: " + key, 4);
                                    foreach (AdKatsSpecialPlayer asPlayer in asPlayerList) {
                                        if (asPlayer.player_object != null) {
                                            DebugWrite("SPECIAL: Contents: " + asPlayer.player_object.player_name, 4);
                                        }
                                        else {
                                            DebugWrite("SPECIAL: Contents: " + asPlayer.player_identifier, 4);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching access list.", e));
            }

            _PlayerRoleRefetch = true;
            _PlayerProcessingWaitHandle.Set();

            UpdateMULTIBalancerWhitelist();
            UpdateMULTIBalancerDisperseList();
            UpdateReservedSlots();
            UpdateSpectatorList();
            _lastUserFetch = DateTime.UtcNow;
            if (_userCache.Count > 0) {
                DebugWrite("User List Fetched from Database. User Count: " + _userCache.Count, 1);
            }
            else {
                ConsoleWarn("No users in the user table. Add a new user with 'Add User'.");
            }

            UpdateSettingPage();
            DebugWrite("fetchUserList finished!", 6);
        }

        private Boolean AssignPlayerRole(AdKatsPlayer aPlayer) {
            AdKatsUser matchingUser = _userCache.Values.FirstOrDefault(aUser => aUser.soldierDictionary.Values.Any(uPlayer => uPlayer.player_id == aPlayer.player_id));
            AdKatsRole aRole = null;
            Boolean authorized = false;
            if (matchingUser != null) {
                authorized = true;
                aRole = matchingUser.user_role;
            }
            else {
                aRole = _RoleKeyDictionary["guest_default"];
            }
            //Debug Block
            if (aPlayer.player_role == null) {
                if (authorized) {
                    DebugWrite("Player " + aPlayer.player_name + " has been assigned authorized role " + aRole.role_name + ".", 4);
                }
                else {
                    DebugWrite("Player " + aPlayer.player_name + " has been assigned the guest role.", 4);
                }
            }
            else {
                if (aPlayer.player_role.role_key != aRole.role_key) {
                    if (authorized) {
                        DebugWrite("Role for authorized player " + aPlayer.player_name + " has been CHANGED to " + aRole.role_name + ".", 4);
                        PlayerSayMessage(aPlayer.player_name, "You have been assigned the authorized role " + aRole.role_name + ".");
                    }
                    else {
                        DebugWrite("Player " + aPlayer.player_name + " has been assigned the guest role.", 4);
                        PlayerSayMessage(aPlayer.player_name, "You have been assigned the guest role.");
                    }
                }
            }
            aPlayer.player_role = aRole;
            aPlayer.player_aa = IsAdminAssistant(aPlayer);
            if (aPlayer.player_aa) {
                DebugWrite(aPlayer.player_name + " IS an Admin Assistant.", 3);
            }
            return authorized;
        }

        private Boolean IsAdminAssistant(AdKatsPlayer aPlayer) {
            DebugWrite("IsAdminAssistant starting!", 7);
            if (!_EnableAdminAssistants) {
                return false;
            }
            Boolean isAdminAssistant = aPlayer.player_aa;
            if (aPlayer.player_aa_fetched) {
                return isAdminAssistant;
            }
            if (HandlePossibleDisconnect()) {
                return false;
            }
            if (RoleIsInteractionAble(aPlayer.player_role)) {
                return false;
            }
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT
	                        'isAdminAssistant'
                        FROM 
	                        `adkats_records_main`
                        WHERE (
	                        SELECT count(`command_action`) 
	                        FROM `adkats_records_main` 
	                        WHERE `command_action` = " + GetCommandByKey("player_report_confirm").command_id + @"
	                        AND `source_id` = " + aPlayer.player_id + @" 
	                        AND (`adkats_records_main`.`record_time` BETWEEN date_sub(UTC_TIMESTAMP(),INTERVAL 30 DAY) AND UTC_TIMESTAMP())
                        ) >= " + _MinimumRequiredMonthlyReports + @" LIMIT 1
                        UNION
                        SELECT
	                        'isGrandfatheredAdminAssistant'
                        FROM 
	                        `adkats_records_main`
                        WHERE (
	                        SELECT count(`command_action`) 
	                        FROM `adkats_records_main` 
	                        WHERE `command_action` = " + GetCommandByKey("player_report_confirm").command_id + @" 
	                        AND `source_id` = " + aPlayer.player_id + @"
                        ) >= 75";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                //Player is an admin assistant, give them access to the self_admins command
                                isAdminAssistant = true;
                            }
                            aPlayer.player_aa_fetched = true;
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while checking if player is an admin assistant.", e));
            }
            DebugWrite("IsAdminAssistant finished!", 7);
            return isAdminAssistant;
        }

        //DONE
        private Boolean FetchDBServerInfo() {
            DebugWrite("FetchDBServerInfo starting!", 6);

            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return false;
            }

            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            `ServerID` as `server_id`,
                            `ServerGroup` as `server_group`
                        FROM 
                            `tbl_server` 
                        WHERE 
                            IP_Address = @IP_Address";
                        command.Parameters.AddWithValue("@IP_Address", _serverIP);
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                Int64 serverID = reader.GetInt64("server_id");
                                Int64 serverGroup = reader.GetInt64("server_group");
                                if (_serverID != -1 && _serverGroup != -1) {
                                    DebugWrite("Attempted server ID and group update after valuse already chosen.", 5);
                                }
                                else {
                                    _serverID = serverID;
                                    _serverGroup = serverGroup;
                                    _settingImportID = _serverID;
                                    DebugWrite("Server ID fetched: " + _serverID, 1);
                                }
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching server ID from database.", e));
            }

            DebugWrite("FetchDBServerInfo finished!", 6);
            return false;
        }

        private Int64 FetchServerGroup(Int64 serverID) {
            DebugWrite("fetchServerGroup starting!", 6);

            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return -1;
            }

            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"
                        SELECT 
                            `ServerGroup` as `server_group`
                        FROM 
                            `tbl_server` 
                        WHERE 
                            `ServerID` = @ServerID";
                        command.Parameters.AddWithValue("@ServerID", serverID);
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                return reader.GetInt64("server_group");
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching server group from database for server " + serverID + ".", e));
            }

            DebugWrite("fetchServerGroup finished!", 6);
            return -1;
        }

        private Boolean HandlePossibleDisconnect() {
            Boolean returnVal = false;
            //Make sure database connection active
            if (_databaseConnectionCriticalState || !DebugDatabaseConnectionActive()) {
                if (!_databaseConnectionCriticalState) {
                    HandleDatabaseConnectionInteruption();
                }
                returnVal = true;
            }
            return returnVal;
        }

        private Boolean DebugDatabaseConnectionActive() {
            DebugWrite("databaseConnectionActive starting!", 8);

            Boolean active = true;

            DateTime startTime = DateTime.UtcNow;
            try {
                using (MySqlConnection connection = GetDatabaseConnection()) {
                    using (MySqlCommand command = connection.CreateCommand()) {
                        command.CommandText = @"SELECT UTC_TIMESTAMP() AS `current_time`";
                        using (MySqlDataReader reader = command.ExecuteReader()) {
                            if (reader.Read()) {
                                active = true;
                            }
                            else {
                                active = false;
                            }
                        }
                    }
                }
            }
            catch (Exception) {
                active = false;
            }
            if ((DateTime.UtcNow - startTime).TotalSeconds > 10) {
                //If the connection took longer than 10 seconds also say the database is disconnected
                active = false;
            }
            DebugWrite("databaseConnectionActive finished!", 8);
            return active;
        }

        private void UpdateMULTIBalancerWhitelist() {
            try {
                if (_FeedMultiBalancerWhitelist) {
                    var autobalanceWhitelistedPlayers = new List<String>();
                    //Pull players from special player cache
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_specialPlayerGroupCache) {
                        List<AdKatsSpecialPlayer> whitelistedPlayers;
                        if (_specialPlayerGroupCache.TryGetValue("whitelist_multibalancer", out whitelistedPlayers)) {
                            foreach (AdKatsSpecialPlayer asPlayer in whitelistedPlayers) {
                                String playerIdentifier = null;
                                if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_name)) {
                                    playerIdentifier = asPlayer.player_object.player_name;
                                }
                                else {
                                    playerIdentifier = asPlayer.player_identifier;
                                }
                                //Skip if no valid info found
                                if (String.IsNullOrEmpty(playerIdentifier)) {
                                    ConsoleError("Player under whitelist_multibalancer was not valid. Unable to add to MULTIBalancer whitelist.");
                                    continue;
                                }
                                if (!autobalanceWhitelistedPlayers.Contains(playerIdentifier)) {
                                    autobalanceWhitelistedPlayers.Add(playerIdentifier);
                                }
                            }
                        }
                    }
                    //Pull players from user list
                    if (_userCache.Count > 0 && _FeedMultiBalancerWhitelist_UserCache) {
                        if (_isTestingAuthorized)
                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                        lock (_userCache) {
                            foreach (AdKatsUser user in _userCache.Values) {
                                if (_isTestingAuthorized)
                                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                lock (user) {
                                    if (_isTestingAuthorized)
                                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                    lock (user.user_role) {
                                        if (_isTestingAuthorized)
                                            PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                        lock (user.user_role.RoleAllowedCommands) {
                                            //Check each user's allowed commands
                                            foreach (AdKatsCommand command in user.user_role.RoleAllowedCommands.Values) {
                                                //If the teamswap command is allowed add each of the user's soldiers to the whitelist
                                                if (command.command_key == "self_teamswap") {
                                                    foreach (AdKatsPlayer soldier in user.soldierDictionary.Values) {
                                                        if (!autobalanceWhitelistedPlayers.Contains(soldier.player_name)) {
                                                            autobalanceWhitelistedPlayers.Add(soldier.player_name);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    //Pull players from teamswap whitelist
                    foreach (String tempName in _TeamswapRoundWhitelist.Keys) {
                        if (!autobalanceWhitelistedPlayers.Contains(tempName)) {
                            autobalanceWhitelistedPlayers.Add(tempName);
                        }
                    }
                    SetExternalPluginSetting("MULTIbalancer", "2 - Exclusions|On Whitelist", "True");
                    SetExternalPluginSetting("MULTIbalancer", "1 - Settings|Whitelist", CPluginVariable.EncodeStringArray(autobalanceWhitelistedPlayers.ToArray()));
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating MULTIBalancer whitelist.", e));
            }
        }

        private void UpdateMULTIBalancerDisperseList() {
            try {
                if (_FeedMultiBalancerDisperseList) {
                    var evenDispersionList = new List<String>();
                    //Pull players from special player cache
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_specialPlayerGroupCache) {
                        List<AdKatsSpecialPlayer> evenDispersedPlayers;
                        if (_specialPlayerGroupCache.TryGetValue("blacklist_dispersion", out evenDispersedPlayers)) {
                            foreach (AdKatsSpecialPlayer asPlayer in evenDispersedPlayers) {
                                String playerIdentifier = null;
                                if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_name)) {
                                    playerIdentifier = asPlayer.player_object.player_name;
                                }
                                else {
                                    playerIdentifier = asPlayer.player_identifier;
                                }
                                //Skip if no valid info found
                                if (String.IsNullOrEmpty(playerIdentifier)) {
                                    ConsoleError("Player under blacklist_dispersion was not valid. Unable to add to MULTIBalancer even dispersion list.");
                                    continue;
                                }
                                if (!evenDispersionList.Contains(playerIdentifier)) {
                                    evenDispersionList.Add(playerIdentifier);
                                }
                            }
                        }
                    }
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
                    SetExternalPluginSetting("MULTIbalancer", "1 - Settings|Disperse Evenly List", CPluginVariable.EncodeStringArray(evenDispersionList.ToArray()));
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating MULTIBalancer even dispersion list.", e));
            }
        }

        private void UpdateReservedSlots() {
            try {
                if (!_FeedServerReservedSlots || _CurrentReservedSlotPlayers == null) {
                    return;
                }
                DebugWrite("Checking validity of reserved slotted players.", 6);
                var allowedReservedSlotPlayers = new List<string>();
                //Pull players from special player cache
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_specialPlayerGroupCache) {
                    List<AdKatsSpecialPlayer> reservedPlayers;
                    if (_specialPlayerGroupCache.TryGetValue("slot_reserved", out reservedPlayers)) {
                        foreach (AdKatsSpecialPlayer asPlayer in reservedPlayers) {
                            String playerIdentifier = null;
                            if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_name)) {
                                playerIdentifier = asPlayer.player_object.player_name;
                            }
                            else {
                                if (SoldierNameValid(asPlayer.player_identifier)) {
                                    playerIdentifier = asPlayer.player_identifier;
                                }
                                else {
                                    ConsoleError("Player under reserved_slot list '" + asPlayer.player_identifier + "' was not a valid soldier name. Unable to add to reserved slot list.");
                                }
                            }
                            //Skip if no valid info found
                            if (String.IsNullOrEmpty(playerIdentifier)) {
                                continue;
                            }
                            if (!allowedReservedSlotPlayers.Contains(playerIdentifier)) {
                                allowedReservedSlotPlayers.Add(playerIdentifier);
                            }
                        }
                    }
                }
                //Pull players from user list
                if (_userCache.Count > 0 && _FeedServerReservedSlots_UserCache) {
                    if (_isTestingAuthorized)
                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (_userCache) {
                        foreach (AdKatsUser user in _userCache.Values) {
                            foreach (AdKatsPlayer soldier in user.soldierDictionary.Values) {
                                //Only add soldiers for the current game
                                if (soldier.game_id == _gameID) {
                                    if (!allowedReservedSlotPlayers.Contains(soldier.player_name)) {
                                        allowedReservedSlotPlayers.Add(soldier.player_name);
                                    }
                                }
                            }
                        }
                    }
                }
                //All players fetched, update the server lists
                //Remove soldiers from the list where needed
                foreach (String playerName in _CurrentReservedSlotPlayers) {
                    if (!allowedReservedSlotPlayers.Contains(playerName)) {
                        DebugWrite(playerName + " in server reserved slots, but not in allowed reserved players. Removing.", 3);
                        ExecuteCommand("procon.protected.send", "reservedSlotsList.remove", playerName);
                        Thread.Sleep(5);
                    }
                }
                //Add soldiers to the list where needed
                foreach (String playerName in allowedReservedSlotPlayers) {
                    if (!_CurrentReservedSlotPlayers.Contains(playerName)) {
                        DebugWrite(playerName + " in allowed reserved players, but not in server reserved slots. Adding.", 3);
                        ExecuteCommand("procon.protected.send", "reservedSlotsList.add", playerName);
                        Thread.Sleep(5);
                    }
                }
                //Save the list
                ExecuteCommand("procon.protected.send", "reservedSlotsList.save");
                //Display the list
                ExecuteCommand("procon.protected.send", "reservedSlotsList.list");
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating server reserved slots.", e));
            }
        }

        public override void OnReservedSlotsList(List<String> soldierNames) {
            try {
                DebugWrite("Reserved slots listed.", 5);
                _CurrentReservedSlotPlayers = soldierNames;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling reserved slot list.", e));
            }
        }

        private void UpdateSpectatorList() {
            try {
                if (!_FeedServerSpectatorList || _CurrentSpectatorListPlayers == null) {
                    return;
                }
                DebugWrite("Updating spectator list players.", 6);
                var allowedSpectatorSlotPlayers = new List<string>();
                //Pull players from special player cache
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (_specialPlayerGroupCache) {
                    List<AdKatsSpecialPlayer> spectators;
                    if (_specialPlayerGroupCache.TryGetValue("slot_spectator", out spectators)) {
                        foreach (AdKatsSpecialPlayer asPlayer in spectators) {
                            String playerIdentifier = null;
                            if (asPlayer.player_object != null && !String.IsNullOrEmpty(asPlayer.player_object.player_name)) {
                                playerIdentifier = asPlayer.player_object.player_name;
                            }
                            else {
                                if (SoldierNameValid(asPlayer.player_identifier)) {
                                    playerIdentifier = asPlayer.player_identifier;
                                }
                                else {
                                    ConsoleError("Player under slot_spectator list '" + asPlayer.player_identifier + "' was not a valid soldier name. Unable to add to spectator slot list.");
                                }
                            }
                            //Skip if no valid info found
                            if (String.IsNullOrEmpty(playerIdentifier)) {
                                continue;
                            }
                            if (!allowedSpectatorSlotPlayers.Contains(playerIdentifier)) {
                                allowedSpectatorSlotPlayers.Add(playerIdentifier);
                            }
                        }
                    }
                }
                //Pull players from user list
                if (_userCache.Count > 0 && _FeedServerSpectatorList_UserCache) {
                    foreach (AdKatsUser user in _userCache.Values) {
                        foreach (AdKatsPlayer soldier in user.soldierDictionary.Values) {
                            //Only add soldiers for the current game
                            if (soldier.game_id == _gameID) {
                                //Only add if players are admins
                                if (RoleIsInteractionAble(soldier.player_role)) {
                                    if (!allowedSpectatorSlotPlayers.Contains(soldier.player_name)) {
                                        allowedSpectatorSlotPlayers.Add(soldier.player_name);
                                    }
                                }
                            }
                        }
                    }
                }
                //All players fetched, update the server lists
                //Remove soldiers from the list where needed
                foreach (String playerName in _CurrentSpectatorListPlayers) {
                    if (!allowedSpectatorSlotPlayers.Contains(playerName)) {
                        DebugWrite(playerName + " in server spectator slots, but not in allowed spectator players. Removing.", 3);
                        ExecuteCommand("procon.protected.send", "spectatorList.remove", playerName);
                        Thread.Sleep(5);
                    }
                }
                //Add soldiers to the list where needed
                foreach (String playerName in allowedSpectatorSlotPlayers) {
                    if (!_CurrentSpectatorListPlayers.Contains(playerName)) {
                        DebugWrite(playerName + " in allowed spectator players, but not in server spectator slots. Adding.", 3);
                        ExecuteCommand("procon.protected.send", "spectatorList.add", playerName);
                        Thread.Sleep(5);
                    }
                }
                //Save the list
                ExecuteCommand("procon.protected.send", "spectatorList.save");
                //Display the list
                ExecuteCommand("procon.protected.send", "spectatorList.list");
                DebugWrite("DONE checking validity of spectator list players.", 6);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating server spectator list.", e));
            }
        }

        public override void OnSpectatorListList(List<String> soldierNames) {
            try {
                DebugWrite("Spectators listed.", 5);
                _CurrentSpectatorListPlayers = soldierNames;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling spectator list.", e));
            }
        }

        public override void OnMaxSpectators(Int32 limit) {
            _maxSpectators = limit;
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

        public void IssueCommand(params String[] commandParams) {
            DebugWrite("IssueCommand starting!", 6);
            try {
                if (!_threadsReady) {
                    ConsoleError("Attempted to issue external command before AdKats threads were running.");
                }
                if (commandParams.Length < 1) {
                    ConsoleError("External command handling canceled. No parameters were provided.");
                    return;
                }
                //TODO add logging for this
                new Thread(ParseExternalCommand).Start(commandParams[0]);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while starting external command processing.", e));
            }
            DebugWrite("IssueCommand finished!", 6);
        }

        private void ParseExternalCommand(Object clientInformation) {
            DebugWrite("ParseExternalCommand starting!", 6);
            try {
                //Set current thread id
                Thread.CurrentThread.Name = "ParseExternalCommand";

                //Create the new record
                var record = new AdKatsRecord {
                    record_source = AdKatsRecord.Sources.ExternalPlugin,
                    server_id = _serverID,
                    record_time = DateTime.UtcNow
                };

                //Parse information into a record
                var parsedClientInformation = (Hashtable) JSON.JsonDecode((String) clientInformation);

                //Import the caller identity
                if (!parsedClientInformation.ContainsKey("caller_identity")) {
                    ConsoleError("Parsed command didn't contain a caller_identity! Unable to process external command.");
                    return;
                }
                var callerIdentity = (String) parsedClientInformation["caller_identity"];
                if (String.IsNullOrEmpty(callerIdentity)) {
                    ConsoleError("caller_identity was empty. Unable to process external command.");
                    return;
                }
                record.external_callerIdentity = callerIdentity;

                //Import the callback options
                if (!parsedClientInformation.ContainsKey("response_requested")) {
                    ConsoleError("Parsed command didn't contain response_requested! Unable to process external command.");
                    return;
                }
                var callbackRequested = (Boolean) parsedClientInformation["response_requested"];
                record.external_responseRequested = callbackRequested;
                if (callbackRequested) {
                    if (!parsedClientInformation.ContainsKey("response_class")) {
                        ConsoleError("Parsed command didn't contain a response_class! Unable to process external command.");
                        return;
                    }
                    var callbackClass = (String) parsedClientInformation["response_class"];
                    if (String.IsNullOrEmpty(callbackClass)) {
                        ConsoleError("response_class was empty. Unable to process external command.");
                        return;
                    }
                    record.external_responseClass = callbackClass;

                    if (!parsedClientInformation.ContainsKey("response_method")) {
                        ConsoleError("Parsed command didn't contain a response_method! Unable to process external command.");
                        return;
                    }
                    var callbackMethod = (String) parsedClientInformation["response_method"];
                    if (String.IsNullOrEmpty(callbackMethod)) {
                        ConsoleError("response_method was empty. Unable to process external command.");
                        return;
                    }
                    record.external_responseMethod = callbackMethod;
                }

                //Import the command type
                if (!parsedClientInformation.ContainsKey("command_type")) {
                    record.record_exception = HandleException(new AdKatsException("Parsed command didn't contain a command_type!"));
                    return;
                }
                var unparsedCommandType = (String) parsedClientInformation["command_type"];
                if (String.IsNullOrEmpty(unparsedCommandType)) {
                    ConsoleError("command_type was empty. Unable to process external command.");
                    return;
                }
                if (!_CommandKeyDictionary.TryGetValue(unparsedCommandType, out record.command_type)) {
                    ConsoleError("command_type was invalid, not found in definition. Unable to process external command.");
                    return;
                }

                //Import the command numeric
                //Only required for temp ban
                if (record.command_type.command_key == "player_ban_temp") {
                    if (!parsedClientInformation.ContainsKey("command_numeric")) {
                        ConsoleError("Parsed command didn't contain a command_numeric! Unable to parse command.");
                        return;
                    }
                    record.command_numeric = (Int32) parsedClientInformation["command_numeric"];
                }

                //Import the source name
                if (!parsedClientInformation.ContainsKey("source_name")) {
                    ConsoleError("Parsed command didn't contain a source_name!");
                    return;
                }
                var sourceName = (String) parsedClientInformation["source_name"];
                if (String.IsNullOrEmpty(sourceName)) {
                    ConsoleError("source_name was empty. Unable to process external command.");
                    return;
                }
                record.source_name = sourceName;

                //Import the target name
                if (!parsedClientInformation.ContainsKey("target_name")) {
                    ConsoleError("Parsed command didn't contain a target_name! Unable to process external command.");
                    return;
                }
                var targetName = (String) parsedClientInformation["target_name"];
                if (String.IsNullOrEmpty(targetName)) {
                    ConsoleError("source_name was empty. Unable to process external command.");
                    return;
                }
                record.target_name = targetName;

                //Import the target guid
                String target_guid = null;
                if (parsedClientInformation.ContainsKey("target_guid")) {
                    target_guid = (String) parsedClientInformation["target_guid"];
                }

                //Import the record message
                if (!parsedClientInformation.ContainsKey("record_message")) {
                    ConsoleError("Parsed command didn't contain a record_message! Unable to process external command.");
                    return;
                }
                var recordMessage = (String) parsedClientInformation["record_message"];
                if (String.IsNullOrEmpty(recordMessage)) {
                    ConsoleError("record_message was empty. Unable to process external command.");
                    return;
                }
                record.record_message = recordMessage;

                _playerDictionary.TryGetValue(record.source_name, out record.source_player);
                if (!_playerDictionary.TryGetValue(record.target_name, out record.target_player) && record.command_type.command_key.StartsWith("player_")) {
                    if (String.IsNullOrEmpty(target_guid)) {
                        ConsoleError("Target player '" + record.target_name + "' was not found in the server. And target_guid was not provided. Unable to process external command.");
                        return;
                    }
                    record.target_player = FetchPlayer(true, false, null, -1, record.target_name, target_guid, null);
                }
                QueueRecordForProcessing(record);
            }
            catch (Exception e) {
                //Log the error in console
                HandleException(new AdKatsException("Unable to process external command.", e));
            }
            DebugWrite("ParseExternalCommand finished!", 6);
        }

        public void FetchAuthorizedSoldiers(params String[] commandParams) {
            DebugWrite("FetchAuthorizedSoldiers starting!", 6);
            if (!commandParams.Any()) {
                ConsoleError("Authorized soldier fetch canceled. No parameters were provided.");
                return;
            }
            //TODO add logging for this
            new Thread(SendAuthorizedSoldiers).Start(commandParams[0]);
            DebugWrite("FetchAuthorizedSoldiers finished!", 6);
        }

        private void SendAuthorizedSoldiers(Object clientInformation) {
            DebugWrite("SendAuthorizedSoldiers starting!", 6);
            try {
                //Set current thread id
                Thread.CurrentThread.Name = "SendAuthorizedSoldiers";

                //Create the new record
                var record = new AdKatsRecord {
                    record_source = AdKatsRecord.Sources.ExternalPlugin
                };

                //Parse information into a record
                var parsedClientInformation = (Hashtable) JSON.JsonDecode((String) clientInformation);

                //Import the caller identity
                if (!parsedClientInformation.ContainsKey("caller_identity")) {
                    ConsoleError("Parsed command didn't contain a caller_identity! Unable to process soldier fetch.");
                    return;
                }
                var callerIdentity = (String) parsedClientInformation["caller_identity"];
                if (String.IsNullOrEmpty(callerIdentity)) {
                    ConsoleError("caller_identity was empty. Unable to process soldier fetch.");
                    return;
                }
                record.external_callerIdentity = callerIdentity;

                //Import the callback options
                if (!parsedClientInformation.ContainsKey("response_requested")) {
                    ConsoleError("Parsed command didn't contain response_requested! Unable to process soldier fetch.");
                    return;
                }
                var callbackRequested = (Boolean) parsedClientInformation["response_requested"];
                record.external_responseRequested = callbackRequested;
                if (callbackRequested) {
                    if (!parsedClientInformation.ContainsKey("response_class")) {
                        ConsoleError("Parsed command didn't contain a response_class! Unable to process soldier fetch.");
                        return;
                    }
                    var callbackClass = (String) parsedClientInformation["response_class"];
                    if (String.IsNullOrEmpty(callbackClass)) {
                        ConsoleError("response_class was empty. Unable to process soldier fetch.");
                        return;
                    }
                    record.external_responseClass = callbackClass;

                    if (!parsedClientInformation.ContainsKey("response_method")) {
                        ConsoleError("Parsed command didn't contain a response_method!");
                        return;
                    }
                    var callbackMethod = (String) parsedClientInformation["response_method"];
                    if (String.IsNullOrEmpty(callbackMethod)) {
                        ConsoleError("response_method was empty. Unable to process soldier fetch.");
                        return;
                    }
                    record.external_responseMethod = callbackMethod;
                }
                else {
                    ConsoleError("response_requested must be true to return authorized soldiers. Unable to process soldier fetch.");
                    return;
                }

                List<AdKatsPlayer> soldierList;
                Boolean containsUserSubset = parsedClientInformation.ContainsKey("user_subset");
                Boolean containsUserRole = parsedClientInformation.ContainsKey("user_role");
                if (containsUserRole && containsUserSubset) {
                    ConsoleError("Both user_subset and user_role were used in request. Only one may be used at any time. Unable to process soldier fetch.");
                    return;
                }
                if (containsUserRole) {
                    var roleString = (String) parsedClientInformation["user_role"];
                    if (String.IsNullOrEmpty(roleString)) {
                        ConsoleError("user_role was found in request, but it was empty. Unable to process soldier fetch.");
                        return;
                    }
                    AdKatsRole aRole;
                    if (!_RoleKeyDictionary.TryGetValue(roleString, out aRole)) {
                        ConsoleError("Specified user role '" + roleString + "' was not found. Unable to process soldier fetch.");
                        return;
                    }
                    soldierList = FetchSoldiersOfRole(aRole);
                }
                else if (containsUserSubset) {
                    var subset = (String) parsedClientInformation["user_subset"];
                    if (String.IsNullOrEmpty(subset)) {
                        DebugWrite("user_subset was found in request, but it was empty. Unable to process soldier fetch.", 3);
                        return;
                    }
                    switch (subset) {
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
                            ConsoleError("request_subset was found in request, but it was invalid. Unable to process soldier fetch.");
                            return;
                    }
                }
                else {
                    ConsoleError("Neither user_subset nor user_role was found in request. Unable to process soldier fetch.");
                    return;
                }

                if (soldierList == null) {
                    ConsoleError("Internal error, all parameters were correct, but soldier list was not fetched.");
                    return;
                }

                String[] soldierNames = (from aPlayer in soldierList where (!String.IsNullOrEmpty(aPlayer.player_name) && aPlayer.game_id == _gameID) select aPlayer.player_name).ToArray();

                var responseHashtable = new Hashtable();
                responseHashtable.Add("caller_identity", "AdKats");
                responseHashtable.Add("response_requested", false);
                responseHashtable.Add("response_type", "FetchAuthorizedSoldiers");
                responseHashtable.Add("response_value", CPluginVariable.EncodeStringArray(soldierNames));

                //TODO add error message if target not found

                ExecuteCommand("procon.protected.plugins.call", record.external_responseClass, record.external_responseMethod, JSON.JsonEncode(responseHashtable));
            }
            catch (Exception e) {
                //Log the error in console
                HandleException(new AdKatsException("Error returning authorized soldiers .", e));
            }
            DebugWrite("SendAuthorizedSoldiers finished!", 6);
        }

        public AdKatsPlayerStats FetchPlayerStats(AdKatsPlayer aPlayer) {
            DebugWrite("entering getPlayerStats", 7);
            //Create return value
            var stats = new AdKatsPlayerStats();
            try {
                //Fetch from BF3Stats
                Hashtable responseData = null;
                if (_gameVersion == GameVersion.BF3) {
                    responseData = FetchBF3StatsPlayer(aPlayer.player_name);
                    if (responseData != null) {
                        var dataStatus = (String) responseData["status"];
                        if (dataStatus == "error") {
                            stats.StatsException = new AdKatsException("BF3 Stats reported error.");
                        }
                        else if (dataStatus == "notfound") {
                            stats.StatsException = new AdKatsException(aPlayer.player_name + " not found");
                        }
                        else {
                            //Pull the global stats
                            stats.Platform = (String) responseData["plat"];
                            stats.ClanTag = (String) responseData["tag"];
                            stats.Language = (String) responseData["language"];
                            stats.Country = (String) responseData["country"];
                            stats.CountryName = (String) responseData["country_name"];
                            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                            stats.FirstSeen = dtDateTime.AddSeconds((Double) responseData["date_insert"]);
                            stats.LastPlayerUpdate = dtDateTime.AddSeconds((Double) responseData["date_update"]);

                            //Get internal stats
                            if (dataStatus == "data") {
                                var statsList = (Hashtable) responseData["stats"];
                                stats.LastStatUpdate = dtDateTime.AddSeconds((Double) statsList["date_update"]);
                                //Get rank
                                var rankTable = (Hashtable) statsList["rank"];
                                stats.Rank = (Double) rankTable["nr"];
                                //Get overall
                                var global = (Hashtable) statsList["global"];
                                stats.Kills = (Double) global["kills"];
                                stats.Deaths = (Double) global["deaths"];
                                stats.Wins = (Double) global["wins"];
                                stats.Losses = (Double) global["losses"];
                                stats.Shots = (Double) global["shots"];
                                stats.Hits = (Double) global["hits"];
                                stats.Headshots = (Double) global["headshots"];
                                stats.Time = TimeSpan.FromSeconds((Double) global["time"]);
                                //Get weapons
                                stats.WeaponStats = new Dictionary<String, AdKatsWeaponStats>();
                                var weaponStats = (Hashtable) statsList["weapons"];
                                foreach (String weaponKey in weaponStats.Keys) {
                                    //Create new construct
                                    var weapon = new AdKatsWeaponStats();
                                    //Grab data
                                    var currentWeapon = (Hashtable) weaponStats[weaponKey];
                                    //Parse into construct
                                    weapon.ID = (String) currentWeapon["name"];
                                    weapon.Shots = (Double) currentWeapon["shots"];
                                    weapon.Hits = (Double) currentWeapon["hits"];
                                    weapon.Kills = (Double) currentWeapon["kills"];
                                    weapon.Headshots = (Double) currentWeapon["headshots"];
                                    weapon.Category = (String) currentWeapon["category"];
                                    weapon.Kit = (String) currentWeapon["kit"];
                                    weapon.Range = (String) currentWeapon["range"];
                                    weapon.Time = TimeSpan.FromSeconds((Double) currentWeapon["time"]);
                                    //Calculate values
                                    weapon.HSKR = weapon.Headshots / weapon.Kills;
                                    weapon.KPM = weapon.Kills / weapon.Time.TotalMinutes;
                                    weapon.DPS = weapon.Kills / weapon.Hits * 100;
                                    //Assign the construct
                                    stats.WeaponStats.Add(weapon.ID, weapon);
                                }
                            }
                            else {
                                stats.StatsException = new AdKatsException(aPlayer.player_name + " did not have stats");
                            }
                        }
                    }
                }
                else if (_gameVersion == GameVersion.BF4) {
                    responseData = FetchBF4StatsPlayer(aPlayer.player_name);
                    if (responseData != null) {
                        if (responseData.ContainsKey("error")) {
                            stats.StatsException = new AdKatsException("BF4 stats returned error '" + ((String) responseData["error"]) + "' when querying for player '" + aPlayer.player_name + "'.");
                            ConsoleError(stats.StatsException.ToString());
                            return null;
                        }
                        if (!responseData.ContainsKey("player") || !responseData.ContainsKey("stats") || !responseData.ContainsKey("weapons")) {
                            stats.StatsException = new AdKatsException("BF4 stats response for player '" + aPlayer.player_name + "' was invalid.");
                            ConsoleError(stats.StatsException.ToString());
                            return null;
                        }
                        try {
                            //Player section
                            var playerData = (Hashtable) responseData["player"];
                            if (playerData != null && playerData.Count > 0) {
                                stats.Platform = (String) playerData["plat"];
                                stats.ClanTag = (String) playerData["tag"];
                                stats.Country = (String) playerData["country"];
                                stats.CountryName = (String) playerData["countryName"];
                                var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                                var createMilli = (Double) playerData["dateCreate"];
                                stats.FirstSeen = dtDateTime.AddMilliseconds(createMilli);
                                var updateMilli = (Double) playerData["dateUpdate"];
                                stats.LastPlayerUpdate = dtDateTime.AddMilliseconds(updateMilli);
                                //Get rank
                                var rankData = (Hashtable) playerData["rank"];
                                stats.Rank = (Double) rankData["nr"];
                            }
                            else {
                                stats.StatsException = new AdKatsException(aPlayer.player_name + " did not have global info.");
                            }

                            //Get Stats
                            var statsData = (Hashtable) responseData["stats"];
                            if (statsData != null && statsData.Count > 0) {
                                //Stat last update is the same as last player update in BF4
                                stats.LastStatUpdate = stats.LastPlayerUpdate;
                                stats.Kills = (Double) statsData["kills"];
                                stats.Deaths = (Double) statsData["deaths"];
                                stats.Wins = (Double) statsData["numWins"];
                                stats.Losses = (Double) statsData["numLosses"];
                                stats.Shots = (Double) statsData["shotsFired"];
                                stats.Hits = (Double) statsData["shotsHit"];
                                stats.Headshots = (Double) statsData["headshots"];
                                stats.Time = TimeSpan.FromSeconds((Double) statsData["timePlayed"]);
                            }
                            else {
                                stats.StatsException = new AdKatsException(aPlayer.player_name + " did not have global stats.");
                            }

                            //Get Weapons
                            var weaponData = (ArrayList) responseData["weapons"];
                            if (weaponData != null && weaponData.Count > 0) {
                                stats.WeaponStats = new Dictionary<String, AdKatsWeaponStats>();
                                foreach (Hashtable currentWeapon in weaponData) {
                                    //Create new construct
                                    var weapon = new AdKatsWeaponStats();
                                    //Grab stat data
                                    var currentWeaponStats = (Hashtable) currentWeapon["stat"];
                                    weapon.ID = (String) currentWeaponStats["id"];
                                    weapon.Time = TimeSpan.FromSeconds((Double) currentWeaponStats["time"]);
                                    weapon.Shots = (Double) currentWeaponStats["shots"];
                                    weapon.Hits = (Double) currentWeaponStats["hits"];
                                    weapon.Kills = (Double) currentWeaponStats["kills"];
                                    weapon.Headshots = (Double) currentWeaponStats["hs"];

                                    //Grab detail data
                                    var currentWeaponDetail = (Hashtable) currentWeapon["detail"];
                                    weapon.Category = (String) currentWeaponDetail["category"];
                                    //leave kit alone
                                    //leave range alone
                                    //Calculate values
                                    weapon.HSKR = weapon.Headshots / weapon.Kills;
                                    weapon.KPM = weapon.Kills / weapon.Time.TotalMinutes;
                                    weapon.DPS = weapon.Kills / weapon.Hits * 100;
                                    //Assign the construct
                                    stats.WeaponStats.Add(weapon.ID, weapon);
                                }
                            }
                            else {
                                stats.StatsException = new AdKatsException(aPlayer.player_name + " did not have weapon stats.");
                            }
                        }
                        catch (Exception e) {
                            ConsoleError(e.ToString());
                        }
                    }
                }
            }
            catch (Exception e) {
                stats.StatsException = new AdKatsException("Server error fetching stats.", e);
            }
            //Assign the stats if no errors
            if (stats.StatsException == null) {
                aPlayer.stats = stats;
            }
            DebugWrite("exiting getPlayerStats", 7);
            return stats;
        }

        private Hashtable FetchBF3StatsPlayer(String playerName) {
            Hashtable playerData = null;
            try {
                using (var client = new WebClient()) {
                    var data = new NameValueCollection {
                        {"player", playerName},
                        {"opt", "all"}
                    };
                    byte[] response = client.UploadValues("http://api.bf3stats.com/pc/player/", data);
                    if (response != null) {
                        String textResponse = System.Text.Encoding.Default.GetString(response);
                        playerData = (Hashtable) JSON.JsonDecode(textResponse);
                    }
                }
            }
            catch (Exception e) {
                //Do nothing
            }
            return playerData;
        }

        private Hashtable FetchBF4StatsPlayer(String playerName) {
            Hashtable playerData = null;
            try {
                using (var client = new WebClient()) {
                    var data = new NameValueCollection {
                        {"plat", "pc"},
                        {"name", playerName},
                        {"output", "json"}
                    };
                    byte[] response = client.UploadValues("http://api.bf4stats.com/api/playerInfo", data);
                    if (response != null) {
                        String textResponse = System.Text.Encoding.Default.GetString(response);
                        playerData = (Hashtable) JSON.JsonDecode(textResponse);
                    }
                }
            }
            catch (Exception) {
                //Do nothing
            }
            return playerData;
        }

        private void PopulateCommandReputationDictionaries() {
            try {
                var sourceDic = new Dictionary<String, Double>();
                var targetDic = new Dictionary<String, Double>();
                ArrayList reputationStats = FetchReputationStats();
                foreach (Hashtable repWeapon in reputationStats) {
                    sourceDic[(String) repWeapon["command_typeaction"]] = (double) repWeapon["source_weight"];
                    targetDic[(String) repWeapon["command_typeaction"]] = (double) repWeapon["target_weight"];
                }
                _commandSourceReputationDictionary = sourceDic;
                _commandTargetReputationDictionary = targetDic;
                ConsoleSuccess("Populated reputation dictionaries");
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while populating command reputations", e));
            }
        }

        private ArrayList FetchReputationStats() {
            ArrayList repTable = null;
            using (var client = new WebClient()) {
                try {
                    const string url = "https://raw.github.com/ColColonCleaner/AdKats/dev/adkatsreputationstats.json";
                    String textResponse = client.DownloadString(url);
                    repTable = (ArrayList) JSON.JsonDecode(textResponse);
                }
                catch (Exception e) {
                    ConsoleError(e.ToString());
                }
            }
            return repTable;
        }

        /*private Hashtable updateBF3StatsPlayer(String player_name)
        {
            Hashtable playerData = null;
            try
            {
                using (WebClient client = new WebClient())
                {
                    System.Security.Cryptography.HMACSHA256 hmac256 = new HMACSHA256(GetBytes("0wPt049KGUTESASnNdi6gnMLht3KdV20"));
                    Hashtable hashData = new Hashtable();
                    hashData.Add("time", ConvertToTimestamp(DateTime.UtcNow) + "");
                    hashData.Add("ident", "pUNykTul3R");
                    hashData.Add("player", player_name);
                    hashData.Add("type", "cronjob");
                    String dataString = JSON.JsonEncode(hashData);
                    ConsoleError("DATA:" + dataString);
                    //url encode the data
                    dataString = System.Web.HttpUtility.UrlEncode(dataString);
                    ConsoleError("E DATA: " + dataString);
                    //Compute the sig
                    String sig = System.Web.HttpUtility.UrlEncode(GetString(hmac256.ComputeHash(GetBytes(dataString))));
                    ConsoleError("SIG: " + sig);
                    NameValueCollection data = new NameValueCollection();
                    data.Add("data", dataString);
                    data.Add("sig", sig);
                    byte[] response = client.UploadValues("http://api.bf3stats.com/pc/playerupdate/", data);
                    if (response != null)
                    {
                        String textResponse = System.Text.Encoding.Default.GetString(response);
                        ConsoleSuccess(textResponse);
                        playerData = (Hashtable)JSON.JsonDecode(textResponse);
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error updating BF3Stats player.", e));
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

        private void PushThreadDebug(Int64 ticks, String thread, Int32 threadid, Int32 line, String element) {
            try {
                DebugWrite(ticks + " " + thread + " " + threadid + " " + line + " " + element, 4);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("error pushing thread debug", e));
            }
        }

        private String ReplacePlayerInformation(String originalString, AdKatsPlayer aPlayer) {
            String processedString = "";
            if (String.IsNullOrEmpty(originalString)) {
                return processedString;
            }
            //Create new instance of original string
            processedString += originalString;
            if (aPlayer == null) {
                return processedString;
            }
            if (aPlayer.player_id > 0) {
                processedString = processedString.Replace("%player_id%", aPlayer.player_id + "");
            }
            if (!String.IsNullOrEmpty(aPlayer.player_name)) {
                processedString = processedString.Replace("%player_name%", aPlayer.player_name);
            }
            if (!String.IsNullOrEmpty(aPlayer.player_guid)) {
                processedString = processedString.Replace("%player_guid%", aPlayer.player_guid);
            }
            if (!String.IsNullOrEmpty(aPlayer.player_pbguid)) {
                processedString = processedString.Replace("%player_pbguid%", aPlayer.player_pbguid);
            }
            if (!String.IsNullOrEmpty(aPlayer.player_ip)) {
                processedString = processedString.Replace("%player_ip%", aPlayer.player_ip);
            }
            return processedString;
        }

        public Boolean RoleIsInteractionAble(AdKatsRole aRole) {
            if (aRole == null) {
                ConsoleError("role null in RoleIsInteractionAble");
                return false;
            }
            if (_isTestingAuthorized)
                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
            lock (aRole) {
                if (_isTestingAuthorized)
                    PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                lock (aRole.RoleAllowedCommands) {
                    if (aRole.RoleAllowedCommands.Values.Any(command => command.command_playerInteraction)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public AdKatsCommand GetCommandByKey(String commandKey) {
            AdKatsCommand command = null;
            _CommandKeyDictionary.TryGetValue(commandKey, out command);
            return command;
        }

        public String ExtractString(String s, String tag) {
            if (String.IsNullOrEmpty(s) || String.IsNullOrEmpty(tag)) {
                ConsoleError("Unable to extract String. Invalid inputs.");
                return null;
            }
            String startTag = "<" + tag + ">";
            Int32 startIndex = s.IndexOf(startTag, System.StringComparison.Ordinal) + startTag.Length;
            if (startIndex == -1) {
                ConsoleError("Unable to extract String. Tag not found.");
            }
            Int32 endIndex = s.IndexOf("</" + tag + ">", startIndex, System.StringComparison.Ordinal);
            return s.Substring(startIndex, endIndex - startIndex);
        }

        public Boolean SoldierNameValid(String input) {
            try {
                DebugWrite("Checking player '" + input + "' for validity.", 7);
                if (String.IsNullOrEmpty(input)) {
                    ConsoleError("Soldier Name empty or null.");
                    return false;
                }
                if (input.Length > 16) {
                    ConsoleError("Soldier Name '" + input + "' too long, maximum length is 16 characters.");
                    return false;
                }
                if (new Regex("[^a-zA-Z0-9_-]").Replace(input, "").Length != input.Length) {
                    ConsoleError("Soldier Name '" + input + "' contained invalid characters.");
                    return false;
                }
                return true;
            }
            catch (Exception) {
                //Soldier id caused exception in the regex, definitely not valid
                ConsoleError("Soldier Name '" + input + "' contained invalid characters.");
                return false;
            }
        }

        public String FormatTimeString(TimeSpan timeSpan, Int32 maxComponents) {
            DebugWrite("Entering formatTimeString", 7);
            String returnMessage = null;
            if (maxComponents < 1) {
                return returnMessage;
            }
            try {
                String formattedTime = (timeSpan.TotalMilliseconds >= 0) ? ("") : ("-");

                Double secondSubset = Math.Abs(timeSpan.TotalSeconds);
                Double minuteSubset = (secondSubset / 60);
                Double hourSubset = (minuteSubset / 60);
                Double daySubset = (hourSubset / 24);
                Double weekSubset = (daySubset / 7);
                Double monthSubset = (weekSubset / 4);
                Double yearSubset = (monthSubset / 12);

                Int32 years = (Int32)yearSubset;
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
                returnMessage = formattedTime;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while formatting time String.", e));
            }
            DebugWrite("Exiting formatTimeString", 7);
            return returnMessage;
        }

        private Double ConvertToTimestamp(DateTime value) {
            //create Timespan by subtracting the value provided from
            //the Unix Epoch
            TimeSpan span = (value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());

            //return the total seconds (which is a UNIX timestamp)
            return span.TotalSeconds;
        }

        private void RemovePlayerFromDictionary(String playerName, Boolean lockDictionary) {
            DebugWrite("Entering removePlayerFromDictionary", 7);
            try {
                //If the player is currently in the player list, remove them
                if (!String.IsNullOrEmpty(playerName)) {
                    if (_playerDictionary.ContainsKey(playerName)) {
                        DebugWrite("Removing " + playerName + " from current player list.", 4);
                        if (lockDictionary) {
                            if (_isTestingAuthorized)
                                PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                            lock (_playerDictionary) {
                                _playerDictionary.Remove(playerName);
                            }
                        }
                        else {
                            _playerDictionary.Remove(playerName);
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while removing player from player dictionary.", e));
            }
            DebugWrite("Exiting removePlayerFromDictionary", 7);
        }

        public CPlayerInfo BuildCPlayerInfo(String playerName, String playerGUID) {
            DebugWrite("Entering ", 7);
            CPlayerInfo playerInfo = null;
            try {
                IList<String> lstParameters = new List<String>();
                IList<String> lstVariables = new List<String>();
                lstParameters.Add("id");
                lstVariables.Add(playerName);
                lstParameters.Add("guid");
                lstVariables.Add(playerGUID);
                playerInfo = new CPlayerInfo(lstParameters, lstVariables);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while creating CPlayerInfo object.", e));
            }
            DebugWrite("Exiting ", 7);
            return playerInfo;
        }

        private TimeSpan GetRemainingBanTime(AdKatsBan aBan) {
            return aBan.ban_endTime.Subtract(DateTime.UtcNow);
        }

        public Boolean ConfirmStatLoggerSetup() {
            //This function has been disabled for now

            //Make sure database connection active
            if (HandlePossibleDisconnect()) {
                return false;
            }
            try {
                List<MatchCommand> registered = GetRegisteredCommands();
                MatchCommand loggerStatusCommand = null;
                foreach (MatchCommand command in registered) {
                    if (System.String.Compare(command.RegisteredClassname, "CChatGUIDStatsLoggerBF3", System.StringComparison.Ordinal) == 0 && System.String.Compare(command.RegisteredMethodName, "GetStatus", System.StringComparison.Ordinal) == 0) {
                        loggerStatusCommand = command;
                        DebugWrite("Found command for BF3 stat logger.", 5);
                        break;
                    }
                    if (System.String.Compare(command.RegisteredClassname, "CChatGUIDStatsLogger", System.StringComparison.Ordinal) == 0 && System.String.Compare(command.RegisteredMethodName, "GetStatus", System.StringComparison.Ordinal) == 0) {
                        loggerStatusCommand = command;
                        DebugWrite("Found command for Universal stat logger.", 5);
                        break;
                    }
                }
                if (loggerStatusCommand != null) {
                    //Stat logger is installed, fetch its status
                    Hashtable statLoggerStatus = GetStatLoggerStatus();

                    //Only continue if response value
                    if (statLoggerStatus == null) {
                        return false;
                    }
                    foreach (String key in statLoggerStatus.Keys) {
                        DebugWrite("Logger response: (" + key + "): " + statLoggerStatus[key], 5);
                    }
                    if (((String) statLoggerStatus["pluginVersion"]) != "1.1.0.2") {
                        ConsoleError("Invalid version of CChatGUIDStatsLoggerBF3 installed. Version 1.1.0.2 is required. If there is a new version, inform ColColonCleaner.");
                        return false;
                    }

                    if (!Regex.Match((String) statLoggerStatus["DBHost"], _mySqlHostname, RegexOptions.IgnoreCase).Success || !Regex.Match((String) statLoggerStatus["DBPort"], _mySqlPort, RegexOptions.IgnoreCase).Success || !Regex.Match((String) statLoggerStatus["DBName"], _mySqlDatabaseName, RegexOptions.IgnoreCase).Success) {
                        //Are db settings set for AdKats? If not, import them from stat logger.
                        if (String.IsNullOrEmpty(_mySqlHostname) || String.IsNullOrEmpty(_mySqlPort) || String.IsNullOrEmpty(_mySqlDatabaseName)) {
                            _mySqlHostname = (String) statLoggerStatus["DBHost"];
                            _mySqlPort = (String) statLoggerStatus["DBPort"];
                            _mySqlDatabaseName = (String) statLoggerStatus["DBName"];
                            UpdateSettingPage();
                        }
                        //Are DB Settings set for stat logger? If not, set them
                        if (String.IsNullOrEmpty((String) statLoggerStatus["DBHost"]) || String.IsNullOrEmpty((String) statLoggerStatus["DBPort"]) || String.IsNullOrEmpty((String) statLoggerStatus["DBName"])) {
                            SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Host", _mySqlHostname);
                            SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Port", _mySqlPort);
                            SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Database Name", _mySqlDatabaseName);
                            SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "UserName", _mySqlUsername);
                            SetExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Password", _mySqlPassword);

                            ConsoleError("CChatGUIDStatsLoggerBF3 database connection was not configured. It has been set up to use the same database and credentials as AdKats.");
                            //Update the logger status
                            statLoggerStatus = GetStatLoggerStatus();
                        }
                        else {
                            ConsoleError("CChatGUIDStatsLoggerBF3 is not set up to use the same database as AdKats. Modify settings so they both use the same database.");
                            return false;
                        }
                    }
                    if (((String) statLoggerStatus["DBConnectionActive"]) != "True") {
                        ConsoleError("CChatGUIDStatsLoggerBF3's connection to the database is not active. Backup mode Enabled...");
                    }
                    return true;
                }
                ConsoleError("^1^bCChatGUIDStatsLoggerBF3^n plugin not found. Installing special release version 1.1.0.2 of that plugin is required for AdKats!");
                return false;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while confirming stat logger setup.", e));
                return false;
            }
        }

        public Hashtable GetStatLoggerStatus() {
            //Disabled

            //Make sure AdKats database connection active
            if (HandlePossibleDisconnect()) {
                return null;
            }
            try {
                //Check if enabled
                if (!_pluginEnabled) {
                    DebugWrite("Attempted to fetch stat logger status while plugin disabled.", 4);
                    return null;
                }
                //Build request
                var request = new Hashtable();
                request["pluginName"] = "AdKats";
                request["pluginMethod"] = "HandleStatLoggerStatusResponse";
                // Send request
                _StatLoggerStatusWaitHandle.Reset();
                ExecuteCommand("procon.protected.plugins.call", "CChatGUIDStatsLoggerBF3", "GetStatus", JSON.JsonEncode(request));
                //Wait a maximum of 5 seconds for stat logger response
                if (!_StatLoggerStatusWaitHandle.WaitOne(5000)) {
                    ConsoleWarn("^bCChatGUIDStatsLoggerBF3^n is not enabled or is lagging! Attempting to enable, please wait...");
                    Int32 attempts = 0;
                    Boolean success = false;
                    do {
                        attempts++;
                        DebugWrite("Stat Logger Enable Attempt " + attempts, 2);
                        //Issue the command to enable stat logger
                        ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "True");
                        //Wait 5 seconds for enable and initial connect
                        Thread.Sleep(5000);
                        //Refetch the status
                        _StatLoggerStatusWaitHandle.Reset();
                        ExecuteCommand("procon.protected.plugins.call", "CChatGUIDStatsLoggerBF3", "GetStatus", JSON.JsonEncode(request));
                        if (_StatLoggerStatusWaitHandle.WaitOne(5000)) {
                            success = true;
                        }
                    } while (!success && attempts < 10);
                    if (!success) {
                        ConsoleError("CChatGUIDStatsLoggerBF3 could not be enabled automatically. Please enable manually.");
                        return null;
                    }
                }
                if (_LastStatLoggerStatusUpdate != null && _LastStatLoggerStatusUpdate.ContainsKey("pluginVersion") && _LastStatLoggerStatusUpdate.ContainsKey("pluginEnabled") && _LastStatLoggerStatusUpdate.ContainsKey("DBHost") && _LastStatLoggerStatusUpdate.ContainsKey("DBPort") && _LastStatLoggerStatusUpdate.ContainsKey("DBName") && _LastStatLoggerStatusUpdate.ContainsKey("DBTimeOffset") && _LastStatLoggerStatusUpdate.ContainsKey("DBConnectionActive") && _LastStatLoggerStatusUpdate.ContainsKey("ChatloggingEnabled") && _LastStatLoggerStatusUpdate.ContainsKey("InstantChatLoggingEnabled") && _LastStatLoggerStatusUpdate.ContainsKey("StatsLoggingEnabled") && _LastStatLoggerStatusUpdate.ContainsKey("DBliveScoreboardEnabled") && _LastStatLoggerStatusUpdate.ContainsKey("DebugMode") && _LastStatLoggerStatusUpdate.ContainsKey("Error")) {
                    //Response appears to be valid, return it
                    return _LastStatLoggerStatusUpdate;
                }
                //Response is invalid, throw error and return null
                ConsoleError("Status response from CChatGUIDStatsLoggerBF3 was not valid.");
                return null;
            }
            catch (Exception) {
                HandleException(new AdKatsException("Error while getting stat logger status."));
                return null;
            }
        }

        public void HandleStatLoggerStatusResponse(params String[] commands) {
            DebugWrite("Entering HandleStatLoggerStatusResponse", 7);
            try {
                if (commands.Length < 1) {
                    ConsoleError("Status fetch response handle canceled, no parameters provided.");
                    return;
                }
                _LastStatLoggerStatusUpdate = (Hashtable) JSON.JsonDecode(commands[0]);
                _LastStatLoggerStatusUpdateTime = DateTime.UtcNow;
                _StatLoggerStatusWaitHandle.Set();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling stat logger status response.", e));
            }
            DebugWrite("Exiting HandleStatLoggerStatusResponse", 7);
        }

        public static String GetRandom32BitHashCode() {
            String randomString = "";
            var random = new Random();

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
            var encodedString = new StringBuilder();

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
            var bytes = new byte[str.Length * sizeof (char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public String GetString(byte[] bytes) {
            var chars = new char[bytes.Length / sizeof (char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new String(chars);
        }

        //Calling this method will make the settings window refresh with new data
        public void UpdateSettingPage() {
            DebugWrite("Updating Setting Page: " + (DateTime.UtcNow - _lastUpdateSettingRequest).TotalSeconds + " seconds since last update.", 4);
            _lastUpdateSettingRequest = DateTime.UtcNow;
            SetExternalPluginSetting("AdKats", "UpdateSettings", "Update");
        }

        //Calls setVariable with the given parameters
        public void SetExternalPluginSetting(String pluginName, String settingName, String settingValue) {
            if (String.IsNullOrEmpty(pluginName) || String.IsNullOrEmpty(settingName) || settingValue == null) {
                ConsoleError("Required inputs null or empty in setExternalPluginSetting");
                return;
            }
            ExecuteCommand("procon.protected.plugins.setVariable", pluginName, settingName, settingValue);
        }

        //Credit to Micovery and PapaCharlie9 for modified Levenshtein Distance algorithm 
        public static Int32 LevenshteinDistance(String s, String t) {
            s = s.ToLower();
            t = t.ToLower();
            Int32 n = s.Length;
            Int32 m = t.Length;
            var d = new Int32[n + 1, m + 1];
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
            var parameters = new List<String>();
            if (message.Length > 0) {
                //Add all single word/number parameters
                String[] paramSplit = message.Split(' ');
                Int32 maxLoop = (paramSplit.Length < maxParamCount) ? (paramSplit.Length) : (maxParamCount);
                for (Int32 i = 0; i < maxLoop - 1; i++) {
                    DebugWrite("Param " + i + ": " + paramSplit[i], 6);
                    parameters.Add(paramSplit[i]);
                    message = message.TrimStart(paramSplit[i].ToCharArray()).Trim();
                }
                //Add final multi-word parameter
                parameters.Add(message);
            }
            DebugWrite("Num params: " + parameters.Count, 6);
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

        public String BoldMessage(String msg) {
            return "^b" + msg + "^n";
        }

        public String ItalicMessage(String msg) {
            return "^i" + msg + "^n";
        }

        public String ColorMessageMaroon(String msg) {
            return "^1" + msg + "^0";
        }

        public String ColorMessageGreen(String msg) {
            return "^2" + msg + "^0";
        }

        public String ColorMessageOrange(String msg) {
            return "^3" + msg + "^0";
        }

        public String ColorMessageBlue(String msg) {
            return "^4" + msg + "^0";
        }

        public String ColorMessageBlueLight(String msg) {
            return "^5" + msg + "^0";
        }

        public String ColorMessageViolet(String msg) {
            return "^6" + msg + "^0";
        }

        public String ColorMessagePink(String msg) {
            return "^7" + msg + "^0";
        }

        public String ColorMessageRed(String msg) {
            return "^8" + msg + "^0";
        }

        public String ColorMessageGrey(String msg) {
            return "^9" + msg + "^0";
        }

        protected void LogThreadExit() {
            lock (_aliveThreads) {
                _aliveThreads.Remove(Thread.CurrentThread.ManagedThreadId);
            }
        }

        protected void StartAndLogThread(Thread aThread) {
            aThread.Start();
            lock (_aliveThreads) {
                if (!_aliveThreads.ContainsKey(aThread.ManagedThreadId)) {
                    _aliveThreads.Add(aThread.ManagedThreadId, aThread);
                }
            }
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
            TimeSpan remainingTime = GetRemainingBanTime(aBan);
            if (remainingTime.TotalDays > 1000) {
                banDurationString = "[perm]";
            }
            else {
                banDurationString = "[" + FormatTimeString(remainingTime, 2) + "]";
            }
            String sourceNameString = "[" + aBan.ban_record.source_name + "]";
            String banAppendString = ((_UseBanAppend) ? ("[" + _BanAppend + "]") : (""));

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
            ExecuteCommand("procon.protected.pluginconsole.write", msg);
            if (_slowmo) {
                Thread.Sleep(1000);
            }
        }

        public void ProconChatWrite(String msg) {
            ExecuteCommand("procon.protected.chat.write", "AdKats > " + msg);
            if (_slowmo) {
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
            ConsoleWrite(query);
        }

        public AdKatsException HandleException(AdKatsException aException) {
            //If it's null or AdKats isn't enabled, just return
            if (aException == null) {
                ConsoleError("Attempted to handle exception when none was given.");
                return null;
            }
            if (!_pluginEnabled) {
                return aException;
            }
            _slowmo = AdKats.SlowMoOnException;
            String prefix = String.Empty;
            if (aException.InternalException != null) {
                prefix = "Line " + (new StackTrace(aException.InternalException, true)).GetFrame(0).GetFileLineNumber() + ": ";
            }
            //Check if the exception attributes to the database
            if (aException.InternalException != null && (aException.InternalException.GetType() == typeof (System.TimeoutException) || aException.InternalException.ToString().Contains("Unable to connect to any of the specified MySQL hosts") || aException.InternalException.ToString().Contains("Reading from the stream has failed.") || aException.InternalException.ToString().Contains("Too many connections") || aException.InternalException.ToString().Contains("Timeout expired") || aException.InternalException.ToString().Contains("An existing connection was forcibly closed by the remote host") || aException.InternalException.ToString().Contains("Unable to read data") || aException.InternalException.ToString().Contains("Lock wait timeout exceeded"))) {
                HandleDatabaseConnectionInteruption();
            }
            else {
                ConsoleWrite(prefix + aException, ConsoleMessageType.Exception);
                //Create the Exception record
                var record = new AdKatsRecord {
                    record_source = AdKatsRecord.Sources.InternalAutomated,
                    isDebug = true,
                    server_id = _serverID,
                    command_type = _CommandKeyDictionary["adkats_exception"],
                    command_numeric = 0,
                    target_name = "AdKats",
                    target_player = null,
                    source_name = "AdKats",
                    record_message = aException.ToString()
                };
                //Process the record
                QueueRecordForProcessing(record);
            }
            return aException;
        }

        public void HandleDatabaseConnectionInteruption() {
            //Only handle these errors if all threads are already functioning normally
            if (_threadsReady || !_pluginEnabled) {
                if (_databaseTimeouts == 0) {
                    _lastDatabaseTimeout = DateTime.UtcNow;
                }
                ++_databaseTimeouts;
                ConsoleError("Database connection fault detected. This is fault " + _databaseTimeouts + ". Critical disconnect at " + DatabaseTimeoutThreshold + ".");
                //Check for critical state (timeouts > threshold, and last timeout less than 1 minute ago)
                if ((DateTime.UtcNow - _lastDatabaseTimeout).TotalSeconds < 60) {
                    if (_databaseTimeouts >= DatabaseTimeoutThreshold) {
                        try {
                            //If the handler is already alive, return
                            if (_DisconnectHandlingThread != null && _DisconnectHandlingThread.IsAlive) {
                                DebugWrite("Attempted to start disconnect handling thread when it was already running.", 2);
                                return;
                            }
                            //Create a new thread to handle the disconnect orchestration
                            _DisconnectHandlingThread = new Thread(new ThreadStart(delegate {
                                try {
                                    Thread.CurrentThread.Name = "DisconnectHandling";
                                    //Log the time of critical disconnect
                                    DateTime disconnectTime = DateTime.Now;
                                    var disconnectTimer = new Stopwatch();
                                    disconnectTimer.Start();
                                    //Immediately disable Stat Logger
                                    ConsoleError("Database connection in critical failure state. Disabling Stat Logger and putting AdKats in Backup Mode.");
                                    _databaseConnectionCriticalState = true;
                                    ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "False");
                                    ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLogger", "False");
                                    //Set resolved
                                    Boolean restored = false;
                                    //Enter loop to check for database reconnection
                                    do {
                                        //If someone manually disables AdKats, exit everything
                                        if (!_pluginEnabled) {
                                            return;
                                        }
                                        //Wait 15 seconds to retry
                                        Thread.Sleep(15000);
                                        //Check if the connection has been restored
                                        restored = DebugDatabaseConnectionActive();
                                        if (!restored) {
                                            _databaseSuccess = 0;
                                            //Inform the user database still not connectable
                                            ConsoleError("Database still not accessible. (" + String.Format("{0:0.00}", disconnectTimer.Elapsed.TotalMinutes) + " minutes since critical disconnect at " + disconnectTime.ToShortTimeString() + ".");
                                        }
                                        else {
                                            _databaseSuccess++;
                                            ConsoleWarn("Database connection appears restored, but waiting " + (DatabaseSuccessThreshold - _databaseSuccess) + " more successful connections to restore normal operation.");
                                        }
                                    } while (_databaseSuccess < DatabaseSuccessThreshold);
                                    //Connection has been restored, inform the user
                                    disconnectTimer.Stop();
                                    ConsoleSuccess("Database connection restored, re-enabling Stat Logger and returning AdKats to Normal Mode.");
                                    //Reset timeout counts
                                    _databaseSuccess = 0;
                                    _databaseTimeouts = 0;
                                    //re-enable AdKats and Stat Logger
                                    _databaseConnectionCriticalState = false;
                                    ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "True");
                                    ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLogger", "True");

                                    //Clear the player dinctionary, causing all players to be fetched from the database again
                                    if (_isTestingAuthorized)
                                        PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                    lock (_playerDictionary) {
                                        _playerDictionary.Clear();
                                    }

                                    //Create the Exception record
                                    var record = new AdKatsRecord {
                                        record_source = AdKatsRecord.Sources.InternalAutomated,
                                        isDebug = true,
                                        server_id = _serverID,
                                        command_type = _CommandKeyDictionary["adkats_exception"],
                                        command_numeric = 0,
                                        target_name = "Database",
                                        target_player = null,
                                        source_name = "AdKats",
                                        record_message = "Critical Database Disconnect Handled (" + String.Format("{0:0.00}", disconnectTimer.Elapsed.TotalMinutes) + " minutes). AdKats on server " + _serverID + " functioning normally again."
                                    };
                                    //Process the record
                                    QueueRecordForProcessing(record);
                                }
                                catch (Exception) {
                                    ConsoleError("Error handling database disconnect.");
                                }
                                ConsoleSuccess("Exiting Critical Disconnect Handler.");
                                LogThreadExit();
                            }));

                            //Start the thread
                            StartAndLogThread(_DisconnectHandlingThread);
                        }
                        catch (Exception) {
                            ConsoleError("Error while initializing disconnect handling thread.");
                        }
                    }
                }
                else {
                    //Reset the current timout count
                    _databaseTimeouts = 0;
                }
                _lastDatabaseTimeout = DateTime.UtcNow;
            }
            else {
                DebugWrite("Attempted to handle database timeout when threads not running.", 2);
            }
        }

        public void StartRoundTimer() {
            if (_pluginEnabled && _threadsReady) {
                try {
                    //If the thread is still alive, inform the user and return
                    if (_RoundTimerThread != null && _RoundTimerThread.IsAlive) {
                        ConsoleError("Tried to enable a round timer while one was still active.");
                        return;
                    }
                    _RoundTimerThread = new Thread(new ThreadStart(delegate {
                        try {
                            Thread.CurrentThread.Name = "RoundTimer";
                            DebugWrite("starting round timer", 2);
                            Thread.Sleep(3000);
                            var roundTimeSeconds = (Int32) (_roundTimeMinutes * 60);
                            for (Int32 secondsRemaining = roundTimeSeconds; secondsRemaining > 0; secondsRemaining--) {
                                if (_CurrentRoundState == RoundState.Ended || !_pluginEnabled || !_threadsReady) {
                                    return;
                                }
                                if (secondsRemaining == roundTimeSeconds - 60 && secondsRemaining > 60) {
                                    AdminTellMessage("Round will end automatically in ~" + (Int32) (secondsRemaining / 60.0) + " minutes.");
                                    DebugWrite("Round will end automatically in ~" + (Int32) (secondsRemaining / 60.0) + " minutes.", 3);
                                }
                                else if (secondsRemaining == (roundTimeSeconds / 2) && secondsRemaining > 60) {
                                    AdminTellMessage("Round will end automatically in ~" + (Int32) (secondsRemaining / 60.0) + " minutes.");
                                    DebugWrite("Round will end automatically in ~" + (Int32) (secondsRemaining / 60.0) + " minutes.", 3);
                                }
                                else if (secondsRemaining == 30) {
                                    AdminTellMessage("Round ends in 30 seconds. (Current winning team will win)");
                                    DebugWrite("Round ends in 30 seconds. (Current winning team will win)", 3);
                                }
                                else if (secondsRemaining == 20) {
                                    AdminTellMessage("Round ends in 20 seconds. (Current winning team will win)");
                                    DebugWrite("Round ends in 20 seconds. (Current winning team will win)", 3);
                                }
                                else if (secondsRemaining <= 10) {
                                    AdminSayMessage("Round ends in..." + secondsRemaining);
                                    DebugWrite("Round ends in..." + secondsRemaining, 3);
                                }
                                //Sleep for 1 second
                                Thread.Sleep(1000);
                            }
                            if (_teamDictionary[1].TeamTicketCount < _teamDictionary[2].TeamTicketCount) {
                                ExecuteCommand("procon.protected.send", "mapList.endRound", "2");
                                DebugWrite("Ended Round (2)", 4);
                            }
                            else {
                                ExecuteCommand("procon.protected.send", "mapList.endRound", "1");
                                DebugWrite("Ended Round (1)", 4);
                            }
                        }
                        catch (Exception e) {
                            HandleException(new AdKatsException("Error in round timer thread.", e));
                        }
                        DebugWrite("Exiting round timer.", 2);
                        LogThreadExit();
                    }));

                    //Start the thread
                    StartAndLogThread(_RoundTimerThread);
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error starting round timer thread.", e));
                }
            }
        }

        public class AdKatsBan {
            //Current exception state of the ban
            public DateTime ban_endTime;
            public Boolean ban_enforceGUID = true;
            public Boolean ban_enforceIP = false;
            public Boolean ban_enforceName = false;
            public AdKatsException ban_exception = null;

            public Int64 ban_id = -1;
            public String ban_notes = null;
            public AdKatsRecord ban_record = null;
            public DateTime ban_startTime;
            public String ban_status = "Active";
            public String ban_sync = null;
            //startTime and endTime are not set by AdKats, they are set in the database.
            //startTime and endTime will be valid only when bans are fetched from the database
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

            public CommandActive command_active = CommandActive.Active;
            public Int64 command_id = -1;
            public String command_key = null;
            public CommandLogging command_logging = CommandLogging.Log;
            public String command_name = null;
            public Boolean command_playerInteraction = true;
            public String command_text = null;

            public override string ToString() {
                return command_name ?? "Unknown Command";
            }
        }

        public class AdKatsException {
            public System.Exception InternalException = null;
            public String Message = String.Empty;
            public String Method = String.Empty;
            //Param Constructors
            public AdKatsException(String message, System.Exception internalException) {
                Method = new StackFrame(1).GetMethod().Name;
                Message = message;
                InternalException = internalException;
            }

            public AdKatsException(String message) {
                Method = new StackFrame(1).GetMethod().Name;
                Message = message;
            }

            //Override toString
            public override String ToString() {
                return "[" + Method + "][" + Message + "]" + ((InternalException != null) ? (": " + InternalException) : (""));
            }
        }

        public class AdKatsPlayer {
            public CPunkbusterInfo PBPlayerInfo = null;
            public Queue<KeyValuePair<AdKatsPlayer, DateTime>> RecentKills = null;
            //All records issued on this player during their current session
            public List<AdKatsRecord> TargetedRecords = null;
            public String clan_tag = null;
            public CPlayerInfo frostbitePlayerInfo = null;
            public Int64 game_id = -1;
            public DateTime lastAction = DateTime.UtcNow;
            public DateTime lastDeath = DateTime.UtcNow;
            public DateTime lastSpawn = DateTime.UtcNow;
            public Boolean player_aa = false;
            public Boolean player_aa_fetched = false;
            public Boolean player_aa_told = false;
            public String player_guid = null;
            public Int64 player_id = -1;
            public String player_ip = null;
            public String player_name = null;
            public String player_name_previous = null;
            public Boolean player_online = false;
            public String player_pbguid = null;
            public AdKatsRole player_role = null;
            public String player_slot = null;

            public AdKatsPlayerStats stats = null;

            public Boolean update_playerUpdated = true;

            public AdKatsPlayer() {
                RecentKills = new Queue<KeyValuePair<AdKatsPlayer, DateTime>>();
                TargetedRecords = new List<AdKatsRecord>();
            }
        }

        public class AdKatsPlayerStats {
            public Double Assists = -1;
            public String ClanTag = null;
            public String Country = null;
            public String CountryName = null;
            public Double Deaths = -1;
            public DateTime FirstSeen = DateTime.MaxValue;
            public Double Headshots = -1;
            public Double Hits = -1;
            public Double Kills = -1;
            public String Language = null;
            public DateTime LastPlayerUpdate = DateTime.MaxValue;
            public DateTime LastStatUpdate = DateTime.MaxValue;
            public Double Losses = -1;
            public String Platform = null;

            public Double Rank = -1;
            public Int32 Score = -1;
            public Double Shots = -1;

            public AdKatsException StatsException = null;
            public TimeSpan Time = TimeSpan.FromSeconds(0);
            public Dictionary<String, AdKatsWeaponStats> WeaponStats = null;
            public Double Wins = -1;
        }

        public class AdKatsRecord {
            //Source of this record
            public enum Sources {
                Default,
                InternalAutomated,
                ExternalPlugin,
                InGame,
                Settings,
                ServerCommand,
                Database,
                HTTP
            }

            //Attributes for the record
            public AdKatsCommand command_action = null;
            public Int32 command_numeric = 0;
            public AdKatsCommand command_type = null;

            //All messages sent through this record via sendMessageToSource or other means
            public List<String> debugMessages;

            //Settings for External Plugin commands
            /*
             * SENDING:
             * callerIdentity
             * recordID
             * commandType
             * commandAction
             * isIRO
             * commandNumeric
             * sourceName
             * targetName
             * targetID
             * recordMessage
             * recordTime
             * recordError
             * recordErrorMessages
             * recordDebugMessages
             * actionExecuted
             * */
            public String external_callerIdentity = null;
            public String external_responseClass = null;
            public String external_responseMethod = null;
            public Boolean external_responseRequested = false;
            public Boolean isAliveChecked = false;
            public Boolean isDebug = false;
            public Boolean isIRO = false;
            public Boolean record_action_executed = false;
            public AdKatsException record_exception = null;
            public Int64 record_id = -1;
            public String record_message = null;
            public Sources record_source = Sources.Default;
            public DateTime record_time = DateTime.UtcNow;
            public Int64 server_id = -1;
            public String source_name = null;
            public AdKatsPlayer source_player = null;

            public String target_name = null;
            public AdKatsPlayer target_player = null;

            //Default Constructor
            public AdKatsRecord() {
                debugMessages = new List<string>();
            }
        }

        public class AdKatsRole {
            public Dictionary<String, KeyValuePair<Func<AdKats, AdKatsPlayer, Boolean>, AdKatsCommand>> ConditionalAllowedCommands = null;
            public Dictionary<String, AdKatsCommand> RoleAllowedCommands = null;
            public Int64 role_id = -1;
            public String role_key = null;
            public String role_name = null;

            public AdKatsRole() {
                RoleAllowedCommands = new Dictionary<String, AdKatsCommand>();
                ConditionalAllowedCommands = new Dictionary<String, KeyValuePair<Func<AdKats, AdKatsPlayer, Boolean>, AdKatsCommand>>();
            }
        }

        public class AdKatsSpecialPlayer {
            public Int32? player_game = null;
            public String player_group = null;
            public String player_identifier = null;
            public AdKatsPlayer player_object = null;
            public Int32? player_server = null;
        }

        public class AdKatsTeam {
            //Final vars
            private readonly Queue<KeyValuePair<Double, DateTime>> TeamTicketCounts;
            private readonly Queue<KeyValuePair<Double, DateTime>> TeamTotalScores;

            public AdKatsTeam(Int32 teamID, String teamKey, String teamName, String teamDesc) {
                TeamID = teamID;
                TeamKey = teamKey;
                TeamName = teamName;
                TeamDesc = teamDesc;
                TeamTotalScores = new Queue<KeyValuePair<Double, DateTime>>();
                TeamTicketCounts = new Queue<KeyValuePair<Double, DateTime>>();
            }

            public Int32 TeamID { get; private set; }
            public String TeamKey { get; private set; }
            public String TeamName { get; private set; }
            public String TeamDesc { get; private set; }

            //Live Vars
            public Int32 TeamPlayerCount { get; set; }
            public Int32 TeamTicketCount { get; set; }
            public Double TeamTotalScore { get; set; }
            public Double TeamScoreDifferenceRate { get; private set; }
            public Double TeamTicketDifferenceRate { get; private set; }

            public void UpdatePlayerCount(Int32 playerCount) {
                TeamPlayerCount = playerCount;
            }

            public void UpdateTicketCount(Double ticketCount) {
                TeamTicketCount = (int) ticketCount;
                Boolean removed = false;
                do {
                    removed = false;
                    if (TeamTicketCounts.Any() && (DateTime.UtcNow - TeamTicketCounts.Peek().Value).TotalSeconds > 120) {
                        TeamTicketCounts.Dequeue();
                        removed = true;
                    }
                } while (removed);
                TeamTicketCounts.Enqueue(new KeyValuePair<double, DateTime>(TeamTicketCount, DateTime.UtcNow));

                KeyValuePair<Double, DateTime> oldestSavedTicketCount = TeamTicketCounts.Peek();
                Double ticketDifference = TeamTicketCount - oldestSavedTicketCount.Key;
                Double ticketTimeDifference = (DateTime.UtcNow - oldestSavedTicketCount.Value).TotalMinutes;
                if (TeamTicketCounts.Count >= 3 && ticketTimeDifference >= 0) {
                    TeamTicketDifferenceRate = ticketDifference / ticketTimeDifference;
                }
            }

            public void UpdateTotalScore(Double totalScore) {
                TeamTotalScore = totalScore;
                Boolean removed = false;
                do {
                    removed = false;
                    if (TeamTotalScores.Any() && (DateTime.UtcNow - TeamTotalScores.Peek().Value).TotalSeconds > 120) {
                        TeamTotalScores.Dequeue();
                        removed = true;
                    }
                } while (removed);
                TeamTotalScores.Enqueue(new KeyValuePair<double, DateTime>(TeamTotalScore, DateTime.UtcNow));

                KeyValuePair<Double, DateTime> oldestSavedScore = TeamTotalScores.Peek();
                Double scoreDifference = TeamTotalScore - oldestSavedScore.Key;
                Double scoreTimeDifference = (DateTime.UtcNow - oldestSavedScore.Value).TotalMinutes;
                if (TeamTotalScores.Count >= 3 && scoreTimeDifference >= 0) {
                    TeamScoreDifferenceRate = scoreDifference / scoreTimeDifference;
                }
            }

            public void Reset() {
                TeamTicketCount = 0;
                TeamTotalScore = 0;
                TeamTotalScores.Clear();
                TeamScoreDifferenceRate = 0;
                TeamTicketCounts.Clear();
                TeamTicketDifferenceRate = 0;
            }
        }

        public class AdKatsUser {
            //No reference to player table made here, plain String id access
            public Dictionary<long, AdKatsPlayer> soldierDictionary = null;
            public String user_email = null;
            public Int64 user_id = -1;
            public String user_name = null;
            public String user_phone = null;
            public AdKatsRole user_role = null;

            public AdKatsUser() {
                soldierDictionary = new Dictionary<long, AdKatsPlayer>();
            }
        }

        public class AdKatsWeaponStats {
            public String Category = null;
            public Double DPS = -1;
            public Double HSKR = -1;
            public Double Headshots = -1;
            public Double Hits = -1;
            public String ID = null;
            public Double KPM = -1;
            public Double Kills = -1;
            public String Kit = null;
            public String Range = null;
            public Double Shots = -1;
            public TimeSpan Time = TimeSpan.FromSeconds(0);
        }

        public class BBM5108Ban {
            public DateTime ban_duration;
            public String ban_length = null;
            public String ban_reason = null;
            public String eaguid = null;
            public String soldiername = null;
        }

        public class EmailHandler {
            private readonly Queue<MailMessage> _EmailProcessingQueue = new Queue<MailMessage>();
            public readonly EventWaitHandle _EmailProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            public String CustomHTMLAddition;
            public AdKats Plugin;
            public List<String> RecipientEmails = new List<string>();
            public String SMTPPassword = "paqwjboqkbfywapu";
            public Int32 SMTPPort = 587;
            public String SMTPServer = "smtp.gmail.com";
            public String SMTPUser = "adkatsbattlefield@gmail.com";
            public String SenderEmail = "adkatsbattlefield@gmail.com";
            public Boolean UseSSL = true;
            private Thread _EmailProcessingThread;

            public EmailHandler(AdKats plugin) {
                Plugin = plugin;
                switch (Plugin._gameVersion) {
                    case GameVersion.BF3:
                        CustomHTMLAddition = @"<br><a href='http://battlelog.battlefield.com/bf3/user/%player_name%/'>BF3 Battlelog Profile</a><br>
<br><a href='http://bf3stats.com/stats_pc/%player_name%'>BF3Stats Profile</a><br>
<br><a href='http://history.anticheatinc.com/bf3/?searchvalue=%player_name%'>AntiCheat, INC. Search</a><br>
<br><a href='http://metabans.com/search/%player_name%'>Metabans Search</a><br>
<br><a href='http://i-stats.net/index.php?action=pcheck&game=BF3&player=%player_name%'>I-Stats Search</a><br>
<br><a href='http://www.team-des-fra.fr/CoM/bf3.php?p=%player_name%'>TeamDes Search</a><br>
<br><a href='http://cheatometer.hedix.de/?p=%player_name%'>Hedix Search</a><br>";
                        break;
                    case GameVersion.BF4:
                        CustomHTMLAddition = @"<br><a href='http://battlelog.battlefield.com/bf4/de/user/%player_name%/'>BF4 Battlelog Profile</a><br>
<br><a href='http://bf4stats.com/pc/%player_name%'>BF4Stats Profile</a><br>
<br><a href='http://history.anticheatinc.com/bf4/?searchvalue=%player_name%'>AntiCheat, INC. Search</a><br>
<br><a href='http://metabans.com/search/%player_name%'>Metabans Search</a><br>";
                        break;
                    default:
                        Plugin.ConsoleError("Game version not understood in email handler");
                        CustomHTMLAddition = "";
                        break;
                }
            }

            public void SendReport(AdKatsRecord record) {
                try {
                    if (Plugin.FetchOnlineAdminSoldiers().Any()) {
                        return;
                    }
                    //Create a new thread to handle keep-alive
                    //This thread will remain running for the duration the layer is online
                    var emailSendingThread = new Thread(new ThreadStart(delegate {
                        try {
                            Thread.CurrentThread.Name = "emailsending";
                            String subject = String.Empty;
                            String body = String.Empty;

                            var sb = new StringBuilder();
                            if (String.IsNullOrEmpty(Plugin._serverName)) {
                                //Unable to send report email, server id unknown
                                return;
                            }
                            subject = record.target_player.player_name + " reported in [" + Plugin._gameVersion + "] " + Plugin._serverName;
                            sb.Append("<h1>AdKats " + Plugin._gameVersion + " Player Report [" + record.command_numeric + "]</h1>");
                            sb.Append("<h2>" + Plugin._serverName + "</h2>");
                            sb.Append("<h3>" + DateTime.Now + " ProCon Time</h3>");
                            sb.Append("<h3>" + record.source_name + " has reported " + record.target_player.player_name + " for " + record.record_message + "</h3>");
                            sb.Append("<p>");
                            CPlayerInfo playerInfo = record.target_player.frostbitePlayerInfo;
                            //sb.Append("<h4>Current Information on " + record.target_name + ":</h4>");
                            int numReports = Plugin._RoundReports.Values.Count(aRecord => aRecord.target_name == record.target_name);
                            sb.Append("Reported " + numReports + " times during the current round.<br/>");
                            sb.Append("Has " + Plugin.FetchPoints(record.target_player, false) + " infraction points on this server.<br/>");
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
                            String processedCustomHTML = Plugin.ReplacePlayerInformation(CustomHTMLAddition, record.target_player);
                            sb.Append(processedCustomHTML);
                            sb.Append("</p>");

                            body = sb.ToString();

                            EmailWrite(subject, body);
                        }
                        catch (Exception e) {
                            Plugin.HandleException(new AdKatsException("Error in email sending thread.", e));
                        }
                        Plugin.LogThreadExit();
                    }));
                    //Start the thread
                    Plugin.StartAndLogThread(emailSendingThread);
                }
                catch (Exception e) {
                    Plugin.HandleException(new AdKatsException("Error when sending email.", e));
                }
            }

            private void EmailWrite(String subject, String body) {
                try {
                    var email = new MailMessage();

                    email.From = new MailAddress(SenderEmail, "AdKats Report System");

                    Boolean someAdded = false;
                    if (Plugin._isTestingAuthorized)
                        Plugin.PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                    lock (Plugin._userCache) {
                        foreach (AdKatsUser aUser in Plugin._userCache.Values) {
                            //Check for not null and default values
                            if (Plugin.RoleIsInteractionAble(aUser.user_role) && !String.IsNullOrEmpty(aUser.user_email)) {
                                if (Regex.IsMatch(aUser.user_email, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$")) {
                                    email.Bcc.Add(new MailAddress(aUser.user_email));
                                    someAdded = true;
                                }
                                else {
                                    Plugin.ConsoleError("Error in user email address: " + aUser.user_email);
                                }
                            }
                        }
                        foreach (String extraEmail in RecipientEmails) {
                            if (String.IsNullOrEmpty(extraEmail.Trim()))
                                continue;

                            if (Regex.IsMatch(extraEmail, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$")) {
                                email.Bcc.Add(new MailAddress(extraEmail));
                                someAdded = true;
                            }
                            else {
                                Plugin.ConsoleError("Error in extra email address: " + extraEmail);
                            }
                        }
                    }
                    if (!someAdded) {
                        Plugin.ConsoleError("Unable to send email. No users with emails have access to player interaction commands.");
                        return;
                    }

                    email.Subject = subject;
                    email.Body = body;
                    email.IsBodyHtml = true;
                    email.BodyEncoding = Encoding.UTF8;

                    QueueEmailForSending(email);

                    Plugin.DebugWrite("A notification email has been sent.", 1);
                }
                catch (Exception e) {
                    Plugin.ConsoleError("Error while sending email: " + e);
                }
            }

            private void QueueEmailForSending(MailMessage email) {
                Plugin.DebugWrite("Entering QueueEmailForSending", 7);
                try {
                    if (Plugin._pluginEnabled) {
                        Plugin.DebugWrite("Preparing to queue email for processing", 6);
                        if (Plugin._isTestingAuthorized)
                            Plugin.PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                        lock (_EmailProcessingQueue) {
                            _EmailProcessingQueue.Enqueue(email);
                            Plugin.DebugWrite("Email queued for processing", 6);
                            //Start the processing thread if not already running
                            if (_EmailProcessingThread == null || !_EmailProcessingThread.IsAlive) {
                                _EmailProcessingThread = new Thread(EmailProcessingThreadLoop) {
                                    IsBackground = true
                                };
                                Plugin.StartAndLogThread(_EmailProcessingThread);
                            }
                            _EmailProcessingWaitHandle.Set();
                        }
                    }
                }
                catch (Exception e) {
                    Plugin.HandleException(new AdKatsException("Error while queueing email for processing.", e));
                }
                Plugin.DebugWrite("Exiting QueueEmailForSending", 7);
            }

            public void EmailProcessingThreadLoop() {
                try {
                    Plugin.DebugWrite("EMAIL: Starting Email Handling Thread", 1);
                    Thread.CurrentThread.Name = "EmailHandling";
                    DateTime loopStart;
                    while (true) {
                        loopStart = DateTime.UtcNow;
                        try {
                            Plugin.DebugWrite("EMAIL: Entering Email Handling Thread Loop", 7);
                            if (!Plugin._pluginEnabled) {
                                Plugin.DebugWrite("EMAIL: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                                break;
                            }

                            //Get all unprocessed inbound emails
                            var inboundEmailMessages = new Queue<MailMessage>();
                            if (_EmailProcessingQueue.Any()) {
                                Plugin.DebugWrite("EMAIL: Preparing to lock inbound mail queue to retrive new mail", 7);
                                if (Plugin._isTestingAuthorized)
                                    Plugin.PushThreadDebug(DateTime.Now.Ticks, (String.IsNullOrEmpty(Thread.CurrentThread.Name) ? ("mainthread") : (Thread.CurrentThread.Name)), Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber(), "");
                                lock (_EmailProcessingQueue) {
                                    Plugin.DebugWrite("EMAIL: Inbound mail found. Grabbing.", 6);
                                    //Grab all mail in the queue
                                    inboundEmailMessages = new Queue<MailMessage>(_EmailProcessingQueue.ToArray());
                                    //Clear the queue for next run
                                    _EmailProcessingQueue.Clear();
                                }
                            }
                            else {
                                Plugin.DebugWrite("EMAIL: No inbound mail. Waiting for Input.", 4);
                                //Wait for input
                                _EmailProcessingWaitHandle.Reset();
                                _EmailProcessingWaitHandle.WaitOne(Timeout.Infinite);
                                continue;
                            }

                            //Loop through all mails in order that they came in
                            while (inboundEmailMessages.Any()) {
                                if (!Plugin._pluginEnabled) {
                                    break;
                                }
                                Plugin.DebugWrite("EMAIL: begin reading mail", 6);
                                //Dequeue the first/next mail
                                var smtp = new SmtpClient(SMTPServer, SMTPPort) {
                                    EnableSsl = UseSSL,
                                    Timeout = 10000,
                                    DeliveryMethod = SmtpDeliveryMethod.Network,
                                    UseDefaultCredentials = false,
                                    Credentials = new NetworkCredential(SMTPUser, SMTPPassword)
                                };
                                smtp.Send(inboundEmailMessages.Dequeue());
                                if (inboundEmailMessages.Any()) {
                                    //Wait 5 seconds between loops
                                    Thread.Sleep(5000);
                                }
                            }
                        }
                        catch (Exception e) {
                            if (e is ThreadAbortException) {
                                Plugin.HandleException(new AdKatsException("mail processing thread aborted. Exiting."));
                                break;
                            }
                            Plugin.HandleException(new AdKatsException("Error occured in mail processing thread. skipping loop.", e));
                        }
                        if (AdKats.FullDebug && ((DateTime.UtcNow - loopStart).TotalMilliseconds > 100))
                            Plugin.ConsoleWrite(Thread.CurrentThread.Name + " thread loop took " + ((int) ((DateTime.UtcNow - loopStart).TotalMilliseconds)));
                    }
                    Plugin.DebugWrite("EMAIL: Ending mail Processing Thread", 1);
                    Plugin.LogThreadExit();
                }
                catch (Exception e) {
                    Plugin.HandleException(new AdKatsException("Error occured in mail processing thread.", e));
                }
            }
        }

        public class StatLibrary {
            private readonly AdKats Plugin;
            public Dictionary<String, StatLibraryWeapon> Weapons;

            public StatLibrary(AdKats plugin) {
                Plugin = plugin;
                PopulateWeaponStats();
            }

            private Dictionary<String, StatLibraryWeapon> OverloadBF3Weapons() {
                //Create the game specific libraries
                var bf3Weapons = new Dictionary<String, StatLibraryWeapon>();
                //Add the weapons
                StatLibraryWeapon weapon;
                weapon = new StatLibraryWeapon {
                    id = "G17C",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "G17C SUPP.",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "G17C TACT.",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = ".44 MAGNUM",
                    damage_max = 60,
                    damage_min = 30
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = ".44 SCOPED",
                    damage_max = 60,
                    damage_min = 30
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "93R",
                    damage_max = 20,
                    damage_min = 12.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "G18",
                    damage_max = 20,
                    damage_min = 12.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "G18 SUPP.",
                    damage_max = 20,
                    damage_min = 12.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "G18 TACT.",
                    damage_max = 20,
                    damage_min = 12.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M9",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M9 TACT.",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M9 SUPP.",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M1911",
                    damage_max = 34,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M1911 TACT.",
                    damage_max = 34,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M1911 SUPP.",
                    damage_max = 34,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M1911 S-TAC",
                    damage_max = 34,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "MP412 REX",
                    damage_max = 50,
                    damage_min = 28
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "MP443",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "MP443 TACT.",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "MP443 SUPP.",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "AS VAL",
                    damage_max = 20,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M5K",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "MP7",
                    damage_max = 20,
                    damage_min = 11.2
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "P90",
                    damage_max = 20,
                    damage_min = 11.2
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "PDW-R",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "PP-19",
                    damage_max = 16.7,
                    damage_min = 12.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "PP-2000",
                    damage_max = 25,
                    damage_min = 13.75
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "UMP-45",
                    damage_max = 34,
                    damage_min = 12.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "JNG-90",
                    damage_max = 80,
                    damage_min = 59
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "L96",
                    damage_max = 80,
                    damage_min = 59
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M39 EMR",
                    damage_max = 50,
                    damage_min = 37.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M40A5",
                    damage_max = 80,
                    damage_min = 59
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M98B",
                    damage_max = 95,
                    damage_min = 59
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M417",
                    damage_max = 50,
                    damage_min = 37.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "MK11",
                    damage_max = 50,
                    damage_min = 37.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "QBU-88",
                    damage_max = 50,
                    damage_min = 37.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "SKS",
                    damage_max = 43,
                    damage_min = 27
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "SV98",
                    damage_max = 80,
                    damage_min = 50
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "SVD",
                    damage_max = 50,
                    damage_min = 37.5
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M27 IAR",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "RPK-74M",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "L86A2",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "LSAT",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M60E4",
                    damage_max = 34,
                    damage_min = 22
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M240B",
                    damage_max = 34,
                    damage_min = 22
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M249",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "MG36",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "PKP PECHENEG",
                    damage_max = 34,
                    damage_min = 22
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "QBB-95",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "TYPE 88 LMG",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "A-91",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "ACW-R",
                    damage_max = 20,
                    damage_min = 16.7
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "AKS-74u",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "G36C",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "G53",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M4A1",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "MTAR-21",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "QBZ-95B",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "SCAR-H",
                    damage_max = 30,
                    damage_min = 20
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "SG553",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M4",
                    damage_max = 25,
                    damage_min = 14.3
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "AEK-971",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "AK-74M",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "AN-94",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "AUG A3",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "F2000",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "FAMAS",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "G3A3",
                    damage_max = 34,
                    damage_min = 22
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "KH2002",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "L85A2",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M16A3",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M416",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "SCAR-L",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M16A4",
                    damage_max = 25,
                    damage_min = 18.4
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "XBOW",
                    damage_max = 100,
                    damage_min = 10
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "XBOW SCOPED",
                    damage_max = 100,
                    damage_min = 10
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M320",
                    damage_max = 100,
                    damage_min = 1
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M26",
                    damage_max = 100,
                    damage_min = 1
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M26 MASS",
                    damage_max = 100,
                    damage_min = 1
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M26 SLUG",
                    damage_max = 100,
                    damage_min = 1
                };
                bf3Weapons.Add(weapon.id, weapon);
                weapon = new StatLibraryWeapon {
                    id = "M26 FRAG",
                    damage_max = 100,
                    damage_min = 1
                };
                bf3Weapons.Add(weapon.id, weapon);
                return bf3Weapons;
            }

            private void PopulateWeaponStats() {
                try {
                    Hashtable statTable = FetchWeaponStats();
                    if (Plugin._gameVersion == GameVersion.BF3) {
                        //No need for bandwidth, all BF3 weapons are set in stone now.
                        Weapons = OverloadBF3Weapons();
                        if (Plugin._UseHackerChecker) {
                            Plugin.ConsoleWarn("Downloaded " + Weapons.Count + " " + Plugin._gameVersion + " weapon definitions for hacker checker.");
                        }
                        return;
                    }
                    //Get Weapons
                    var statData = (ArrayList) statTable[Plugin._gameVersion + ""];
                    if (statData != null && statData.Count > 0) {
                        var tempWeapons = new Dictionary<String, StatLibraryWeapon>();
                        foreach (Hashtable currentWeapon in statData) {
                            //Create new construct
                            StatLibraryWeapon weapon;
                            weapon = new StatLibraryWeapon {
                                id = (String) currentWeapon["id"],
                                damage_max = (Double) currentWeapon["damage_max"],
                                damage_min = (Double) currentWeapon["damage_min"]
                            };
                            tempWeapons.Add(weapon.id, weapon);
                        }
                        if (tempWeapons.Count > 0) {
                            Weapons = tempWeapons;
                            if (Plugin._UseHackerChecker) {
                                Plugin.ConsoleWarn("Downloaded " + Weapons.Count + " " + Plugin._gameVersion + " weapon definitions for hacker checker.");
                            }
                        }
                        else {
                            Plugin.HandleException(new AdKatsException("Valid game found, but no weapons were founds."));
                        }
                    }
                    else {
                        Plugin.HandleException(new AdKatsException("Unable to fetch weapon stats from github. Unable to perform hacker checking."));
                    }
                }
                catch (Exception e) {
                    Plugin.HandleException(new AdKatsException("Error while fetching weapon stats for " + Plugin._gameVersion, e));
                }
            }

            private Hashtable FetchWeaponStats() {
                Hashtable statTable = null;
                using (var client = new WebClient()) {
                    try {
                        const string url = "https://raw.github.com/ColColonCleaner/AdKats/dev/adkatsweaponstats.json";
                        String textResponse = client.DownloadString(url);
                        statTable = (Hashtable) JSON.JsonDecode(textResponse);
                    }
                    catch (Exception e) {
                        Plugin.ConsoleError(e.ToString());
                    }
                }
                return statTable;
            }
        }

        public class StatLibraryWeapon {
            public Double damage_max = -1;
            public Double damage_min = -1;
            public String id = null;
        }
    }
}
