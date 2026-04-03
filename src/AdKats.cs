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
 * Modded Levenshtein Distance algorithm and Tag Parsing from Micovery's InsaneLimits
 * Email System adapted from MorpheusX(AUT)'s "Notify Me!"
 * TeamSpeak Integration by Imisnew2
 * Discord report posting by jbrunink
 *
 * Development by Daniel J. Gradinjan (ColColonCleaner)
 * Work on fork by Hedius (Version >= 8.0.0.0)
 * Procon v2 rewrite (Version 9.0.0.0)
 *
 * AdKats.cs
 * Version 9.0.0.0
 * 28-MAR-2026
 *
 * Automatic Update Information
 * <version_code>9.0.0.0</version_code>
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Dapper;

using Flurl;
using Flurl.Http;

using MySqlConnector;

using Newtonsoft.Json.Linq;

using PRoCon.Core;
using PRoCon.Core.Maps;
using PRoCon.Core.Network;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;

namespace PRoConEvents
{
    public partial class AdKats : PRoConPluginAPI, IPRoConPluginInterface
    {

        //Current Plugin Version
        private const String PluginVersion = "9.0.0.0";

        public enum GameVersionEnum
        {
            UNKNOWN,
            BF3,
            BF4,
            BFHL,
            BFBC2
        };

        public enum RoundState
        {
            Loaded,
            Playing,
            Ended
        }

        public enum PopulationState
        {
            Unknown,
            Low,
            Medium,
            High,
        }

        public enum PlayerType
        {
            Player,
            Spectator,
            CommanderPC,
            CommanderMobile
        }

        public enum VersionStatus
        {
            OutdatedBuild,
            StableBuild,
            TestBuild,
            UnknownBuild,
            UnfetchedBuild
        }

        public enum AutoSurrenderAction
        {
            None,
            Surrender,
            Nuke,
            Vote
        }

        public enum VoipJoinDisplayType
        {
            Disabled,
            Say,
            Yell,
            Tell
        }

        private const string s = " | ";
        private const string t = "|";

        private Utilities Util;

        //State
        private Boolean _LevelLoadShutdown;
        private const Boolean FullDebug = false;
        private volatile String _pluginChangelog;
        private volatile String _pluginDescription;
        private volatile String _pluginLinks;
        private volatile Boolean _pluginEnabled;
        private volatile Boolean _pluginRebootOnDisable;
        private volatile String _pluginRebootOnDisableSource;
        private volatile Boolean _threadsReady;
        private volatile String _latestPluginVersion;
        private Int64 _latestPluginVersionInt;
        private Int64 _currentPluginVersionInt;
        private volatile String _pluginVersionStatusString;
        private volatile VersionStatus _pluginVersionStatus = VersionStatus.UnfetchedBuild;
        private volatile Boolean _pluginUpdateServerInfoChecked;
        private volatile Boolean _pluginUpdatePatched;
        private volatile String _pluginPatchedVersion;
        private Int64 _pluginPatchedVersionInt;
        private volatile String _pluginUpdateProgress = "NotStarted";
        private volatile String _pluginDescFetchProgress = "NotStarted";
        private ARecord _pluginUpdateCaller;
        private volatile Boolean _useKeepAlive;
        private Int32 _startingTicketCount = -1;
        private RoundState _roundState = RoundState.Loaded;
        private DateTime _playingStartTime = DateTime.UtcNow;
        private Int32 _highestTicketCount;
        private Int32 _lowestTicketCount = 500000;
        private volatile Boolean _fetchedPluginInformation;
        private Boolean _firstUserListComplete;
        private Boolean _firstPlayerListStarted;
        private Boolean _firstPlayerListComplete;
        private String _vipKickedPlayerName;
        private Boolean _enforceSingleInstance = true;
        private GameVersionEnum GameVersion = GameVersionEnum.UNKNOWN;
        private Boolean _endingRound;
        private readonly AServer _serverInfo;
        private TimeSpan _previousRoundDuration = TimeSpan.Zero;
        private Int32 _soldierHealth = 100;
        private Int64 _settingImportID = -1;
        private Boolean _settingsFetched;
        private Boolean _settingsLocked;
        private String _settingsPassword;
        private Int32 _pingKicksThisRound;
        private Int32 _mapBenefitIndex;
        private Int32 _mapDetrimentIndex;
        private Int32 _pingKicksTotal;
        private Int32 _roundID;
        private Boolean _versionTrackingDisabled;
        private Boolean _automaticUpdatesDisabled;
        private String _currentFlagMessage;
        private Boolean _populationPopulating;
        private readonly Dictionary<String, APlayer> _populationPopulatingPlayers = new Dictionary<String, APlayer>();
        private String _AdKatsLRTExtensionToken = String.Empty;
        private List<CPlayerInfo> _roundOverPlayers = null;
        private Int32 _MemoryUsageCurrent = 0;
        private Int32 _MemoryUsageWarn = 512;
        private Int32 _MemoryUsageRestartPlugin = 1024;
        private Int32 _MemoryUsageRestartProcon = 2048;

        //Debug
        private Boolean _debugDisplayPlayerFetches;

        //Timing
        private readonly DateTime _proconStartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _AdKatsStartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _AdKatsRunningTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastBanListCall = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastDbBanFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastGUIDBanCountFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastIPBanCountFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastNameBanCountFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastStatLoggerStatusUpdateTime = DateTime.UtcNow - TimeSpan.FromMinutes(60);
        private DateTime _lastSuccessfulBanList = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _populationTransitionTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _populationUpdateTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastDatabaseTimeout = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastDbActionFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastDbSettingFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastSettingPageUpdate = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastSettingPageUpdateRequest = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastUserFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _LastPlayerMoveIssued = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _LastPluginDescFetch = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _LastWeaponCodePost = DateTime.UtcNow - TimeSpan.FromHours(1);
        private DateTime _LastTicketRateDisplay = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private DateTime _lastAutoSurrenderTriggerTime = DateTime.UtcNow - TimeSpan.FromSeconds(10);
        private DateTime _LastBattlelogAction = DateTime.UtcNow - TimeSpan.FromSeconds(2);
        private DateTime _LastBattlelogIssue = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private DateTime _LastServerInfoTrigger = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private DateTime _LastServerInfoReceive = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private Object _battlelogLocker = new Object();
        private TimeSpan _BattlelogWaitDuration = TimeSpan.FromSeconds(5);
        private DateTime _LastIPAPIAction = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private readonly TimeSpan _IPAPIWaitDuration = TimeSpan.FromSeconds(6);
        private Object _IPAPILocker = new Object();
        private DateTime _LastGoogleAction = DateTime.UtcNow - TimeSpan.FromSeconds(0.3);
        private readonly TimeSpan _GoogleWaitDuration = TimeSpan.FromSeconds(0.3);
        private DateTime _lastGlitchedPlayerNotification = DateTime.UtcNow;
        private DateTime _lastInvalidPlayerNameNotification = DateTime.UtcNow;
        private DateTime _lastIPAPIError = DateTime.UtcNow;
        private DateTime _lastBattlelogFrequencyMessage = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private Queue<DateTime> _BattlelogActionTimes = new Queue<DateTime>();
        private DateTime _LastPlayerListTrigger = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private Queue<DateTime> _PlayerListTriggerTimes = new Queue<DateTime>();
        private DateTime _LastPlayerListReceive = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private Queue<DateTime> _PlayerListReceiveTimes = new Queue<DateTime>();
        private DateTime _LastPlayerListAccept = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private Queue<DateTime> _PlayerListAcceptTimes = new Queue<DateTime>();
        private DateTime _LastPlayerListProcessed = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private Queue<DateTime> _PlayerListProcessedTimes = new Queue<DateTime>();
        private Boolean _DebugPlayerListing = false;
        private DateTime _LastDebugPlayerListingMessage = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private Queue<TimeSpan> _startupDurations = new Queue<TimeSpan>();
        private DateTime _LastShortKeepAliveCheck = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private DateTime _LastLongKeepAliveCheck = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private DateTime _LastVeryLongKeepAliveCheck = DateTime.UtcNow - TimeSpan.FromSeconds(30);
        private DateTime _LastMemoryWarning = DateTime.UtcNow - TimeSpan.FromSeconds(30);

        //Server
        private PopulationState _populationStatus = PopulationState.Unknown;
        private readonly Dictionary<PopulationState, TimeSpan> _populationDurations = new Dictionary<PopulationState, TimeSpan>();
        private Int32 _lowPopulationPlayerCount = 20;
        private Int32 _highPopulationPlayerCount = 40;
        private String _shortServerName = "";
        private Boolean _automaticServerRestart = false;
        private Boolean _automaticServerRestartProcon = false;
        private Int32 _automaticServerRestartMinHours = 18;
        private Dictionary<String, String> ReadableMaps = new Dictionary<string, string>();
        private Dictionary<String, String> ReadableModes = new Dictionary<string, string>();

        //MySQL connection
        private String _mySqlSchemaName = "";
        private String _mySqlHostname = "";
        private String _mySqlPassword = "";
        private String _mySqlPort = "";
        private String _mySqlUsername = "";
        private readonly MySqlConnectionStringBuilder _dbCommStringBuilder = new MySqlConnectionStringBuilder();
        private Boolean _fetchActionsFromDb = true;
        private const Boolean UseConnectionPooling = true;
        private const Int32 MinConnectionPoolSize = 0;
        private const Int32 MaxConnectionPoolSize = 20;
        private const Boolean UseCompressedConnection = false;
        private const Int32 DatabaseTimeoutThreshold = 15;
        private const Int32 DatabaseSuccessThreshold = 5;
        private Boolean _databaseConnectionCriticalState;
        private Int32 _databaseSuccess;
        private Int32 _databaseTimeouts;
        private readonly List<Double> _DatabaseReaderDurations = new List<Double>();
        private Double _DatabaseReadAverageDuration = 100;
        private readonly List<Double> _DatabaseNonQueryDurations = new List<Double>();
        private Double _DatabaseWriteAverageDuration = 100;
        private volatile Boolean _dbSettingsChanged = true;
        private Boolean _dbTimingChecked;
        private Boolean _dbTimingValid;
        private TimeSpan _dbTimingOffset = TimeSpan.Zero;
        private Boolean _globalTimingChecked;
        private Boolean _globalTimingValid;
        private TimeSpan _globalTimingOffset = TimeSpan.Zero;
        private Boolean _timingValidOverride;
        private String _statLoggerVersion = "BF3";

        //Action fetching
        private const Int32 DbActionFetchFrequency = 10;
        private const Int32 DbSettingFetchFrequency = 300;
        private const Int32 DbBanFetchFrequency = 60;

        //Event trigger dictionaries
        private readonly Dictionary<String, ARecord> _ActOnIsAliveDictionary = new Dictionary<String, ARecord>();
        private readonly Dictionary<String, ARecord> _ActOnSpawnDictionary = new Dictionary<String, ARecord>();
        private readonly Dictionary<String, ARecord> _LoadoutConfirmDictionary = new Dictionary<String, ARecord>();
        private readonly Dictionary<String, ARecord> _ActionConfirmDic = new Dictionary<String, ARecord>();
        private readonly Dictionary<String, Int32> _RoundMutedPlayers = new Dictionary<String, Int32>();
        private readonly List<ARecord> _PlayerReports = new List<ARecord>();
        private readonly HashSet<String> _PlayersRequestingCommands = new HashSet<String>();

        //Threads
        private ThreadManager Threading;

        private Thread _Activator;
        private Thread _Finalizer;
        private Thread _DatabaseCommunicationThread;
        private Thread _MessageProcessingThread;
        private Thread _CommandParsingThread;
        private Thread _PlayerListingThread;
        private Thread _TeamSwapThread;
        private Thread _BanEnforcerThread;
        private Thread _RoundTimerThread;
        private Thread _KillProcessingThread;
        private Thread _AntiCheatThread;
        private Thread _DisconnectHandlingThread;
        private Thread _AccessFetchingThread;
        private Thread _ActionHandlingThread;
        private Thread _BattlelogCommThread;
        private Thread _IPAPICommThread;

        //Threading queues
        private readonly Queue<APlayer> _BanEnforcerCheckingQueue = new Queue<APlayer>();
        private readonly Queue<ABan> _BanEnforcerProcessingQueue = new Queue<ABan>();
        private readonly Queue<CBanInfo> _CBanProcessingQueue = new Queue<CBanInfo>();
        private readonly Queue<ACommand> _CommandRemovalQueue = new Queue<ACommand>();
        private readonly Queue<ACommand> _CommandUploadQueue = new Queue<ACommand>();
        private readonly Queue<APlayer> _AntiCheatQueue = new Queue<APlayer>();
        private readonly Queue<Kill> _KillProcessingQueue = new Queue<Kill>();
        private readonly Queue<List<CPlayerInfo>> _PlayerListProcessingQueue = new Queue<List<CPlayerInfo>>();
        private readonly Queue<CPlayerInfo> _PlayerRemovalProcessingQueue = new Queue<CPlayerInfo>();
        private readonly Queue<ARole> _RoleRemovalQueue = new Queue<ARole>();
        private readonly Queue<ARole> _RoleUploadQueue = new Queue<ARole>();
        private readonly Queue<CPluginVariable> _SettingUploadQueue = new Queue<CPluginVariable>();
        private readonly Queue<AChatMessage> _UnparsedCommandQueue = new Queue<AChatMessage>();
        private readonly Queue<AChatMessage> _UnparsedMessageQueue = new Queue<AChatMessage>();
        private readonly Queue<ARecord> _UnprocessedActionQueue = new Queue<ARecord>();
        private readonly Queue<ARecord> _UnprocessedRecordQueue = new Queue<ARecord>();
        private readonly Queue<AStatistic> _UnprocessedStatisticQueue = new Queue<AStatistic>();
        private readonly Queue<AUser> _UserRemovalQueue = new Queue<AUser>();
        private readonly Queue<AUser> _UserUploadQueue = new Queue<AUser>();
        private readonly Queue<APlayer> _BattlelogFetchQueue = new Queue<APlayer>();
        private readonly Queue<APlayer> _IPInfoFetchQueue = new Queue<APlayer>();

        //Threading wait handles
        private EventWaitHandle _WeaponStatsWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _AccessFetchWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _TeamswapWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _StatLoggerStatusWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _ServerInfoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _RoundEndingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PlayerListUpdateWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _MessageParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _KillProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _AntiCheatWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _DbCommunicationWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _CommandParsingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _BanEnforcerWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _ActionHandlingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PlayerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _BattlelogCommWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private readonly EventWaitHandle _IPInfoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        //Procon MatchCommand
        private readonly MatchCommand _PluginEnabledMatchCommand;
        private readonly MatchCommand _fetchAuthorizedSoldiersMatchCommand;
        private readonly MatchCommand _subscribeAsClientMatchCommand;
        private readonly MatchCommand _issueCommandMatchCommand;

        //Commands global
        private readonly Dictionary<long, ACommand> _CommandIDDictionary = new Dictionary<long, ACommand>();
        private readonly Dictionary<String, ACommand> _CommandKeyDictionary = new Dictionary<String, ACommand>();
        private readonly Dictionary<String, ACommand> _CommandNameDictionary = new Dictionary<String, ACommand>();
        private readonly Dictionary<String, ACommand> _CommandTextDictionary = new Dictionary<String, ACommand>();
        private readonly Dictionary<String, String> _CommandDescriptionDictionary = new Dictionary<string, string>();
        private readonly Dictionary<String, Func<AdKats, Double>> _commandTimeoutDictionary = new Dictionary<string, Func<AdKats, double>>();
        private readonly Dictionary<String, DateTime> _commandUsageTimes = new Dictionary<string, DateTime>();
        private Boolean _AllowAdminSayCommands = true;
        private Boolean _ReservedSquadLead = false;
        private Boolean _ReservedSelfMove = false;
        private Boolean _ReservedSelfKill = false;
        private Boolean _bypassCommandConfirmation = false;
        private List<String> _ExternalPlayerCommands = new List<string>();
        private List<String> _ExternalAdminCommands = new List<string>();
        private List<String> _CommandTargetWhitelistCommands = new List<string>();
        private Int32 _RequiredReasonLength = 4;
        private Int32 _minimumAssistMinutes = 5;
        //Commands specific
        private String _ServerVoipAddress = "Enter teamspeak/discord/etc address here.";
        //Dynamic access
        public Func<AdKats, APlayer, Boolean> AAPerkFunc = ((plugin, aPlayer) => ((plugin._EnableAdminAssistantPerk && aPlayer.player_aa) || (aPlayer.player_reputation > _reputationThresholdGood)));
        public Func<AdKats, APlayer, Boolean> TeamSwapFunc = ((plugin, aPlayer) => ((plugin._EnableAdminAssistantPerk && aPlayer.player_aa) || plugin.GetMatchingVerboseASPlayersOfGroup("whitelist_teamswap", aPlayer).Any()));

        //Roles
        private readonly Dictionary<long, ARole> _RoleIDDictionary = new Dictionary<long, ARole>();
        private readonly Dictionary<String, ARole> _RoleKeyDictionary = new Dictionary<String, ARole>();
        private readonly Dictionary<String, ARole> _RoleNameDictionary = new Dictionary<String, ARole>();
        private Boolean _PlayerRoleRefetch;
        private readonly Dictionary<String, String> _RoleCommandCache = new Dictionary<String, String>();
        private DateTime _RoleCommandCacheUpdate = DateTime.UtcNow - TimeSpan.FromMinutes(5);
        private TimeSpan _RoleCommandCacheUpdateBufferDuration = TimeSpan.FromSeconds(5);
        private DateTime _RoleCommandCacheUpdateBufferStart = DateTime.UtcNow - TimeSpan.FromSeconds(5);

        //Users
        private const Int32 DbUserFetchFrequency = 300;
        private readonly Dictionary<long, AUser> _userCache = new Dictionary<long, AUser>();
        private readonly Dictionary<Int64, ASpecialGroup> _specialPlayerGroupIDDictionary = new Dictionary<Int64, ASpecialGroup>();
        private readonly Dictionary<String, ASpecialGroup> _specialPlayerGroupKeyDictionary = new Dictionary<String, ASpecialGroup>();
        private readonly Dictionary<Int64, ASpecialPlayer> _baseSpecialPlayerCache = new Dictionary<Int64, ASpecialPlayer>();
        private readonly Dictionary<String, ASpecialPlayer> _verboseSpecialPlayerCache = new Dictionary<String, ASpecialPlayer>();

        //Games and teams
        private readonly Dictionary<Int64, GameVersionEnum> _gameIDDictionary = new Dictionary<Int64, GameVersionEnum>();
        private readonly Dictionary<Int32, ATeam> _teamDictionary = new Dictionary<Int32, ATeam>();
        private Boolean _acceptingTeamUpdates;
        private readonly Dictionary<String, Int32> _unmatchedRoundDeathCounts = new Dictionary<String, Int32>();
        private readonly HashSet<String> _unmatchedRoundDeaths = new HashSet<String>();
        private readonly Dictionary<String, ARecord> _roundAssists = new Dictionary<String, ARecord>();

        //Players
        private readonly Dictionary<String, APlayer> _PlayerDictionary = new Dictionary<String, APlayer>();
        private readonly List<String> _MissingPlayers = new List<String>();
        private readonly List<ASquad> _RoundPrepSquads = new List<ASquad>();
        private readonly Dictionary<String, APlayer> _PlayerLeftDictionary = new Dictionary<String, APlayer>();
        private readonly Dictionary<Int64, APlayer> _FetchedPlayers = new Dictionary<Int64, APlayer>();
        private readonly Dictionary<Int64, HashSet<Int64>> _RoundPlayerIDs = new Dictionary<Int64, HashSet<Int64>>();

        //Punishment settings
        private readonly List<String> _PunishmentSeverityIndex;
        private Boolean _CombineServerPunishments;
        private Boolean _AutomaticForgives;
        private Int32 _AutomaticForgiveLastPunishDays = 30;
        private Int32 _AutomaticForgiveLastForgiveDays = 14;
        private Boolean _IROActive = true;
        private Boolean _IROOverridesLowPop;
        private Int32 _IROOverridesLowPopInfractions = 5;
        private Int32 _IROTimeout = 10;
        private Boolean _OnlyKillOnLowPop = true;
        private String[] _PunishmentHierarchy = { "kill", "kick", "tban120", "kill", "kick", "tbanday", "kick", "tbanweek", "kick", "tban2weeks", "kick", "tbanmonth", "kick", "ban" };

        //Teamswap
        private Int32 _TeamSwapTicketWindowHigh = 500000;
        private Int32 _TeamSwapTicketWindowLow;
        private Queue<CPlayerInfo> _Team1MoveQueue = new Queue<CPlayerInfo>();
        private Queue<CPlayerInfo> _Team2MoveQueue = new Queue<CPlayerInfo>();
        private Queue<CPlayerInfo> _TeamswapForceMoveQueue = new Queue<CPlayerInfo>();
        private Queue<CPlayerInfo> _TeamswapOnDeathCheckingQueue = new Queue<CPlayerInfo>();
        private readonly Dictionary<String, CPlayerInfo> _TeamswapOnDeathMoveDic = new Dictionary<String, CPlayerInfo>();

        //AFK manager
        private Boolean _AFKManagerEnable;
        private Boolean _AFKAutoKickEnable;
        private Double _AFKTriggerDurationMinutes = 5;
        private Int32 _AFKTriggerMinimumPlayers = 20;
        private Boolean _AFKIgnoreUserList = true;
        private String[] _AFKIgnoreRoles = { };
        private Boolean _AFKIgnoreChat;

        //Ping enforcer
        private Boolean _pingEnforcerEnable;
        private Int32 _pingEnforcerTriggerMinimumPlayers = 50;
        private Double _pingEnforcerLowTriggerMS = 300;
        private Int32[] _pingEnforcerLowTimeModifier = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private Double _pingEnforcerMedTriggerMS = 300;
        private Int32[] _pingEnforcerMedTimeModifier = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private Double _pingEnforcerHighTriggerMS = 300;
        private Int32[] _pingEnforcerHighTimeModifier = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private Double _pingEnforcerFullTriggerMS = 300;
        private Int32[] _pingEnforcerFullTimeModifier = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private Double _pingMovingAverageDurationSeconds = 180;
        private Boolean _pingEnforcerKickMissingPings = true;
        private Boolean _pingEnforcerIgnoreUserList = true;
        private String _pingEnforcerMessagePrefix = "Please fix your ping and join us again.";
        private String[] _pingEnforcerIgnoreRoles = { };
        private Boolean _attemptManualPingWhenMissing = false;
        private Boolean _pingEnforcerDisplayProconChat = true;

        //Commander manager
        private Boolean _CMDRManagerEnable = false;
        private Int32 _CMDRMinimumPlayers = 40;

        //Ban enforcer
        private Boolean _UseBanAppend;
        private String _BanAppend = "Appeal at your_site.com";
        private Boolean _UseBanEnforcer;
        private Boolean _UseBanEnforcerPreviousState;
        private Boolean _BanEnforcerBF4LenientKick = false;
        private List<ABan> _BanEnforcerSearchResults = new List<ABan>();
        private Boolean _BansQueuing;
        private String _CBanAdminName = "BanEnforcer";
        private Boolean _DefaultEnforceGUID = true;
        private Boolean _DefaultEnforceIP = false;
        private Boolean _DefaultEnforceName = false;
        private Int64 _GUIDBanCount = -1;
        private Int64 _IPBanCount = -1;
        private Int64 _NameBanCount = -1;
        private TimeSpan _MaxTempBanDuration = TimeSpan.FromDays(3650);

        //Reports
        public String[] _AutoReportHandleStrings = { };
        private Boolean _InformReportedPlayers;
        private String[] _PlayerInformExclusionStrings = { };
        private Int32 _MinimumReportHandleSeconds;

        //Email
        private Boolean _UseEmail;

        //PushBullet
        private Boolean _UsePushBullet;

        //Muting
        private Int32 _MutedPlayerChances = 5;
        private Int32 _PersistentMutedPlayerChances = 5;
        private String _MutedPlayerKickMessage = "Talking excessively while muted.";
        private String _MutedPlayerKillMessage = "Do not talk while muted. You can speak again next round.";
        private String _PersistentMutedPlayerKickMessage = "Talking excessively while muted.";
        private String _PersistentMutedPlayerKillMessage = "Do not talk while muted. This mute is permanent/temp.";
        private String _MutedPlayerMuteMessage = "You have been muted by an admin, talking will cause punishment. You can speak again next round.";
        private String _UnMutePlayerMessage = "You have been unmuted by an admin.";
        private Boolean _MutedPlayerIgnoreCommands = true;
        private Boolean _UseFirstSpawnMutedMessage = true;
        private String _FirstSpawnMutedMessage = "You are perma or temp muted! Talking will cause punishment!";
        private Int32 _ForceMuteBanDuration = 60;

        //Surrender
        private Boolean _surrenderVoteEnable;
        private Double _surrenderVoteMinimumPlayerPercentage = 30;
        private Int32 _surrenderVoteMinimumPlayerCount = 16;
        private Int32 _surrenderVoteMinimumTicketGap = 250;
        private Boolean _surrenderVoteTicketRateGapEnable;
        private Double _surrenderVoteMinimumTicketRateGap = 10;
        private Boolean _surrenderVoteTimeoutEnable;
        private Double _surrenderVoteTimeoutMinutes = 5;
        private Boolean _surrenderVoteActive;
        private Boolean _surrenderVoteSucceeded;
        private DateTime _surrenderVoteStartTime = DateTime.UtcNow;
        private readonly HashSet<String> _surrenderVoteList = new HashSet<String>();
        private readonly HashSet<String> _nosurrenderVoteList = new HashSet<String>();
        //Auto-Surrender
        private Boolean _surrenderAutoEnable;
        private Boolean _surrenderAutoSucceeded;
        private Boolean _surrenderAutoUseMetroValues;
        private Boolean _surrenderAutoUseLockerValues;
        private Int32 _surrenderAutoMinimumTicketGap = 100;
        private Int32 _surrenderAutoMinimumTicketCount = 100;
        private Int32 _surrenderAutoMaximumTicketCount = 999;
        private Double _surrenderAutoLosingRateMax = 999;
        private Double _surrenderAutoLosingRateMin = 999;
        private Double _surrenderAutoWinningRateMax = 999;
        private Double _surrenderAutoWinningRateMin = 999;
        private Int32 _surrenderAutoTriggerCountToSurrender = 10;
        private Boolean _surrenderAutoResetTriggerCountOnCancel = true;
        private Boolean _surrenderAutoResetTriggerCountOnFire = true;
        private Int32 _surrenderAutoTriggerCountCurrent;
        private Int32 _surrenderAutoTriggerCountPause;
        private Int32 _surrenderAutoMinimumPlayers = 10;
        private String _surrenderAutoMessage = "Auto-Resolving Round. %WinnerName% Wins!";
        private Boolean _surrenderAutoNukeInstead;
        private Boolean _nukeAutoSlayActive = false;
        private Int32 _surrenderAutoNukeDurationHigh = 0;
        private Int32 _surrenderAutoNukeDurationMed = 0;
        private Int32 _surrenderAutoNukeDurationLow = 0;
        private Int32 _nukeAutoSlayActiveDuration = 0;
        private String _lastNukeSlayDurationMessage = null;
        private Int32 _surrenderAutoNukeDurationIncrease = 0;
        private Int32 _surrenderAutoNukeDurationIncreaseTicketDiff = 100;
        private Int32 _surrenderAutoNukeMinBetween = 60;
        private DateTime _lastNukeTime = DateTime.UtcNow - TimeSpan.FromMinutes(10);
        private ATeam _lastNukeTeam;
        private Boolean _surrenderAutoAnnounceNukePrep = true;
        private Boolean _surrenderAutoNukeLosingTeams = false;
        private Int32 _surrenderAutoNukeLosingMaxDiff = 200;
        private Boolean _surrenderAutoNukeResolveAfterMax = false;
        private Int32 _surrenderAutoMaxNukesEachRound = 4;
        private Dictionary<Int32, Int32> _nukesThisRound = new Dictionary<Int32, Int32>();
        private Boolean _surrenderAutoTriggerVote;
        private String _surrenderAutoNukeMessage = "Nuking %WinnerName% for baserape!";
        private Int32 _NukeCountdownDurationSeconds = 0;
        private Int32 _NukeWinningTeamUpTicketCount = 9999;
        private Boolean _NukeWinningTeamUpTicketHigh = true;

        //EmailHandler
        private EmailHandler _EmailHandler;
        private Boolean _EmailReportsOnlyWhenAdminless;

        //PushBullet
        private PushBulletHandler _PushBulletHandler;
        private Boolean _PushBulletReportsOnlyWhenAdminless;

        //Perks
        private String[] _PerkSpecialPlayerGroups = {
            "slot_reserved",
            "slot_spectator",
            "whitelist_report",
            "whitelist_spambot",
            "whitelist_adminassistant",
            "whitelist_ping",
            "whitelist_anticheat",
            "whitelist_multibalancer",
            "whitelist_populator",
            "whitelist_teamkill",
            "whitelist_bf4db",
            "whitelist_ba"
        };
        private Boolean _UsePerkExpirationNotify = false;
        private Int32 _PerkExpirationNotifyDays = 7;
        //Orchestration
        private List<String> _CurrentReservedSlotPlayers = new List<String>();
        private List<String> _CurrentSpectatorListPlayers;
        private Boolean _FeedMultiBalancerWhitelist;
        private Boolean _FeedMultiBalancerWhitelist_Admins = true;
        private Boolean _FeedMultiBalancerDisperseList;
        private Boolean _FeedBF4DBWhitelist;
        private Boolean _FeedBAWhitelist;
        private Boolean _FeedTeamKillTrackerWhitelist;
        private Boolean _FeedTeamKillTrackerWhitelist_Admins;
        private Boolean _FeedServerReservedSlots;
        private Boolean _FeedServerReservedSlots_VSM;
        private Boolean _FeedServerReservedSlots_Admins = true;
        private Boolean _FeedServerReservedSlots_Admins_VIPKickWhitelist = false;
        private Boolean _FeedServerSpectatorList;
        private Boolean _FeedServerSpectatorList_Admins;
        private Boolean _FeedStatLoggerSettings;
        private Boolean _PostStatLoggerChatManually;
        private Boolean _PostStatLoggerChatManually_PostServerChatSpam = true;
        private Boolean _PostStatLoggerChatManually_IgnoreCommands;
        private Boolean _PostMapBenefitStatistics;
        private Boolean _MULTIBalancerUnswitcherDisabled;
        public readonly String[] _subscriptionGroups = { "OnlineSoldiers" };
        private readonly List<AClient> _subscribedClients = new List<AClient>();
        private String[] _BannedTags = { };
        private DateTime _AutoKickNewPlayerDate = DateTime.UtcNow + TimeSpan.FromDays(7300);
        //Team Power Monitor
        private Boolean _UseTeamPowerMonitorSeeders = false;
        private Boolean _UseTeamPowerDisplayBalance = false;
        private Boolean _UseTeamPowerMonitorScrambler = false;
        private Boolean _ScrambleRequiredTeamsRemoved = false;
        private Boolean _UseTeamPowerMonitorReassign = false;
        private Boolean _UseTeamPowerMonitorReassignLenient = false;
        private Double _TeamPowerMonitorReassignLenientPercent = 30;
        private Boolean _UseTeamPowerMonitorUnswitcher = false;
        private Boolean currentStartingTeam1 = true;
        private Boolean _PlayersAutoAssistedThisRound = false;
        private Double _TeamPowerActiveInfluence = 35;
        //Populators
        private Boolean _PopulatorMonitor;
        private Boolean _PopulatorUseSpecifiedPopulatorsOnly;
        private Boolean _PopulatorPopulatingThisServerOnly;
        private Int32 _PopulatorMinimumPopulationCountPastWeek = 5;
        private Int32 _PopulatorMinimumPopulationCountPast2Weeks = 10;
        private readonly Dictionary<String, APlayer> _populatorPlayers = new Dictionary<String, APlayer>();
        private Boolean _PopulatorPerksEnable;
        private Boolean _PopulatorPerksReservedSlot;
        private Boolean _PopulatorPerksBalanceWhitelist;
        private Boolean _PopulatorPerksPingWhitelist;
        private Boolean _PopulatorPerksTeamKillTrackerWhitelist;

        //Teamspeak
        private readonly TeamSpeakClientViewer _TeamspeakManager;
        private Boolean _TeamspeakPlayerMonitorView;
        private Boolean _TeamspeakPlayerMonitorEnable;
        private readonly Dictionary<String, APlayer> _TeamspeakPlayers = new Dictionary<String, APlayer>();
        private Boolean _TeamspeakPlayerPerksEnable;
        private Boolean _TeamspeakPlayerPerksVIPKickWhitelist;
        private Boolean _TeamspeakPlayerPerksBalanceWhitelist;
        private Boolean _TeamspeakPlayerPerksPingWhitelist;
        private Boolean _TeamspeakPlayerPerksTeamKillTrackerWhitelist;

        // Announcer for online discord players
        private Boolean _TeamspeakOnlinePlayersEnable = true;
        private int _TeamspeakOnlinePlayersInterval = 5;
        private int _TeamspeakOnlinePlayersMaxPlayersToList = 5;
        private string _TeamspeakOnlinePlayersAloneMessage = "%players% is in voice on our TeamSpeak. Check out our TeamSpeak and join them.";
        private string _TeamspeakOnlinePlayersMessage = "%count% players are in voice on our TeamSpeak. Check out our TeamSpeak and join them. Online: %players%";
        private DateTime _LastTeamspeakOnlinePlayersCheck = DateTime.UtcNow - TimeSpan.FromSeconds(70);

        //Discord
        private readonly DiscordManager _DiscordManager;
        private Boolean _DiscordPlayerMonitorView;
        private Boolean _DiscordPlayerMonitorEnable;
        private readonly Dictionary<String, APlayer> _DiscordPlayers = new Dictionary<String, APlayer>();
        private Boolean _DiscordPlayerRequireVoiceForAdmin;
        private Boolean _DiscordPlayerPerksEnable;
        private Boolean _DiscordPlayerPerksVIPKickWhitelist;
        private Boolean _DiscordPlayerPerksBalanceWhitelist;
        private Boolean _DiscordPlayerPerksPingWhitelist;
        private Boolean _DiscordPlayerPerksTeamKillTrackerWhitelist;
        private Boolean _UseDiscordForReports;
        private Boolean _DiscordReportsOnlyWhenAdminless;
        private Boolean _DiscordReportsLeftWithoutAction;

        // Announcer for online discord players
        private Boolean _DiscordOnlinePlayersEnable = true;
        private int _DiscordOnlinePlayersInterval = 5;
        private int _DiscordOnlinePlayersMaxPlayersToList = 5;
        private string _DiscordOnlinePlayersAloneMessage = "%players% is in voice on our Discord. Check out our Discord and join them.";
        private string _DiscordOnlinePlayersMessage = "%count% players are in voice on our Discord. Check out our Discord and join them. Online: %players%";
        private DateTime _LastDiscordOnlinePlayersCheck = DateTime.UtcNow - TimeSpan.FromSeconds(30);

        //Watchlist
        private Boolean _UseDiscordForWatchlist = false;
        private Boolean _DiscordWatchlistLeftEnabled = false;

        //Challenge
        private AChallengeManager ChallengeManager;

        //AntiCheat
        private Boolean _useAntiCheatLIVESystem = true;
        private Boolean _AntiCheatLIVESystemActiveStats = false;
        private Boolean _UseHskChecker;
        private Boolean _UseKpmChecker;
        private Double _HskTriggerLevel = 60.0;
        private Double _KpmTriggerLevel = 5.0;
        private String _AntiCheatDPSBanMessage = "DPS Automatic Ban";
        private String _AntiCheatHSKBanMessage = "HSK Automatic Ban";
        private String _AntiCheatKPMBanMessage = "KPM Automatic Ban";

        //External commands
        private readonly String _instanceKey = GetRandom32BitHashCode();

        //Admin assistants
        public Boolean _EnableAdminAssistantPerk = false;
        public Boolean _EnableAdminAssistants = false;
        public Int32 _MinimumRequiredMonthlyReports = 10;
        public Boolean _UseAAReportAutoHandler = false;

        //Messaging
        private List<String> _PreMessageList;
        private Boolean _RequirePreMessageUse;
        private Boolean _ShowAdminNameInAnnouncement;
        private Boolean _ShowNewPlayerAnnouncement = true;
        private Boolean _ShowPlayerNameChangeAnnouncement = true;
        private Boolean _ShowTargetedPlayerLeftNotification = true;
        private Int32 _YellDuration = 5;
        private Boolean _UseFirstSpawnMessage;
        private Boolean _useFirstSpawnRepMessage;
        private String _FirstSpawnMessage = "FIRST SPAWN MESSAGE";
        private Boolean _EnableLowPlaytimeSpawnMessage = false;
        private Int32 _LowPlaytimeSpawnMessageHours = 5;
        private String _LowPlaytimeSpawnMessage = "ALTERNATIVE SPAWN MESSAGE";
        private Boolean _DisplayTicketRatesInProconChat;
        private Boolean _InformReputablePlayersOfAdminJoins = false;
        private Boolean _InformAdminsOfAdminJoins = true;
        private Boolean _UseAllCapsLimiter = false;
        private Boolean _AllCapsLimiterSpecifiedPlayersOnly = false;
        private Int32 _AllCapsLimterPercentage = 80;
        private Int32 _AllCapsLimterMinimumCharacters = 15;
        private Int32 _AllCapsLimiterWarnThreshold = 3;
        private Int32 _AllCapsLimiterKillThreshold = 5;
        private Int32 _AllCapsLimiterKickThreshold = 6;

        //SpamBot
        private Boolean _spamBotEnabled;
        private List<String> _spamBotSayList;
        private readonly Queue<String> _spamBotSayQueue = new Queue<String>();
        private Int32 _spamBotSayDelaySeconds = 300;
        private DateTime _spamBotSayLastPost = DateTime.UtcNow - TimeSpan.FromSeconds(300);
        private List<String> _spamBotYellList;
        private readonly Queue<String> _spamBotYellQueue = new Queue<String>();
        private Int32 _spamBotYellDelaySeconds = 600;
        private DateTime _spamBotYellLastPost = DateTime.UtcNow - TimeSpan.FromSeconds(600);
        private List<String> _spamBotTellList;
        private readonly Queue<String> _spamBotTellQueue = new Queue<String>();
        private Int32 _spamBotTellDelaySeconds = 900;
        private DateTime _spamBotTellLastPost = DateTime.UtcNow - TimeSpan.FromSeconds(900);
        private Boolean _spamBotExcludeWhitelist = false;
        private Boolean _spamBotExcludeAdmins = true;
        private Boolean _spamBotExcludeTeamspeakDiscord = true;
        private Boolean _spamBotExcludeHighReputation = true;
        private Int32 _spamBotMinPlaytimeHours = 0;
        //Rules
        private Double _ServerRulesDelay = 0.5;
        private Double _ServerRulesInterval = 5;
        private String[] _ServerRulesList = { "No AdKats rules have been set." };
        private Boolean _ServerRulesNumbers = true;
        private Boolean _ServerRulesYell;

        //Locking
        private Double _playerLockingManualDuration = 10;
        private Boolean _playerLockingAutomaticLock;
        private Double _playerLockingAutomaticDuration = 2.5;

        //Round monitor
        private Boolean _useRoundTimer;
        private Double _maxRoundTimeMinutes = 30;

        //Reputation
        private Dictionary<String, Double> _commandSourceReputationDictionary;
        private Dictionary<String, Double> _commandTargetReputationDictionary;
        private const Double _reputationThresholdGood = 75;
        private const Double _reputationThresholdBad = 0;

        //Assist
        private Queue<ARecord> _AssistAttemptQueue = new Queue<ARecord>();
        DateTime _LastAutoAssist = DateTime.UtcNow - TimeSpan.FromSeconds(300);

        //Battlecries
        public enum BattlecryVolume
        {
            Say,
            Yell,
            Tell,
            Disabled
        }
        private BattlecryVolume _battlecryVolume = BattlecryVolume.Disabled;
        private Int32 _battlecryMaxLength = 100;
        private String[] _battlecryDeniedWords = { };

        //Faction randomizer
        public enum FactionRandomizerRestriction
        {
            NoRestriction,
            NeverSameFaction,
            AlwaysSameFaction,
            AlwaysSwapUSvsRU,
            AlwaysSwapUSvsCN,
            AlwaysSwapRUvsCN,
            AlwaysBothUS,
            AlwaysBothRU,
            AlwaysBothCN,
            AlwaysUSvsX,
            AlwaysRUvsX,
            AlwaysCNvsX,
            NeverUSvsX,
            NeverRUvsX,
            NeverCNvsX
        }
        private Boolean _factionRandomizerEnable = false;
        private FactionRandomizerRestriction _factionRandomizerRestriction = FactionRandomizerRestriction.NoRestriction;
        private Boolean _factionRandomizerAllowRepeatSelection = true;
        private Int32 _factionRandomizerCurrentTeam1 = 0;
        private Int32 _factionRandomizerCurrentTeam2 = 1;

        //MapModes
        public List<CMap> _AvailableMapModes = null;

        //Weapon stats
        protected AWeaponDictionary WeaponDictionary;
        private StatLibrary _StatLibrary;
        HashSet<String> _AntiCheatCheckedPlayers = new HashSet<String>();
        HashSet<String> _AntiCheatCheckedPlayersStats = new HashSet<String>();

        //Polling
        private APoll _ActivePoll = null;
        private TimeSpan _PollMaxDuration = TimeSpan.FromMinutes(4);
        private TimeSpan _PollPrintInterval = TimeSpan.FromSeconds(30);
        private Int32 _PollMaxVotes = 18;
        private String[] _AvailablePolls = new String[] {
            "event"
        };

        //Experimental
        private Boolean _UseExperimentalTools;
        private Boolean _ShowQuerySettings;
        private Boolean _DebugKills;
        private readonly Ping _PingProcessor = new Ping();
        private Boolean _UseGrenadeCookCatcher;
        private Dictionary<String, APlayer> _RoundCookers = new Dictionary<String, APlayer>();
        private Boolean _UseWeaponLimiter;
        private String _WeaponLimiterExceptionString = "_Flechette|_Slug|_Dart|_SHG";
        private String _WeaponLimiterString = "ROADKILL|Death|_LVG|_HE|_Frag|_XM25|_FLASH|_V40|_M34|_Flashbang|_SMK|_Smoke|_FGM148|_Grenade|_SLAM|_NLAW|_RPG7|_C4|_Claymore|_FIM92|_M67|_SMAW|_SRAW|_Sa18IGLA|_Tomahawk|_3GL|USAS|MGL|UCAV";

        //Events
        private Boolean _EventWeeklyRepeat = false;
        private DayOfWeek _EventWeeklyDay = DayOfWeek.Saturday;
        private Boolean _EventPollAutomatic = false;
        private Boolean _eventPollYellWinningRule = true;
        private DateTime _EventDate = GetLocalEpochTime();
        private Double _EventHour = 0;
        private Int32 _EventTestRoundNumber = 999999;
        private Double _EventAnnounceDayDifference = 7;
        private Int32 _CurrentEventRoundNumber = 999999;
        private List<AEventOption> _EventRoundOptions = new List<AEventOption>();
        private Boolean _EventRoundPolled = false;
        private Int32 _EventPollMaxOptions = 4;
        private Int32 _EventRoundAutoPollsMax = 7;
        private TimeSpan _EventRoundAutoVoteDuration = TimeSpan.FromMinutes(2.5);
        private List<AEventOption> _EventRoundPollOptions = new List<AEventOption>();
        private String _EventRoundOptionsEnum;
        private String _eventBaseServerName = "Event Base Server Name";
        private String _eventCountdownServerName = "Event Countdown Server Name";
        private String _eventConcreteCountdownServerName = "Event Concrete Countdown Server Name";
        private String _eventActiveServerName = "Event Active Server Name";
        private readonly HashSet<String> _DetectedWeaponCodes = new HashSet<String>();

        // Proxy
        private Boolean _UseProxy = false;
        private String _ProxyURL = "";

        //Settings display
        private Dictionary<String, String> _SettingSections = new Dictionary<String, String>();
        private String _SettingSectionEnum;
        private String _CurrentSettingSection;
        private const String _AllSettingSections = "All Settings .*";

        public readonly Logger Log;

        public AdKats()
        {
            Log = new Logger(this);
            Util = new Utilities(Log);
            Threading = new ThreadManager(Log);
            //Create the server reference
            _serverInfo = new AServer(this);

            //Set defaults for webclient
            ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            _pluginEnabled = false;
            _threadsReady = false;
            //Assign the match commands
            _PluginEnabledMatchCommand = new MatchCommand("AdKats", "PluginEnabled", new List<String>(), "AdKats_PluginEnabled", new List<MatchArgumentFormat>(), new ExecutionRequirements(ExecutionScope.None), "Useable by other plugins to check if AdKats is enabled or in process of starting up.");
            _issueCommandMatchCommand = new MatchCommand("AdKats", "IssueCommand", new List<String>(), "AdKats_IssueCommand", new List<MatchArgumentFormat>(), new ExecutionRequirements(ExecutionScope.None), "Useable by other plugins to call AdKats commands.");
            _fetchAuthorizedSoldiersMatchCommand = new MatchCommand("AdKats", "FetchAuthorizedSoldiers", new List<String>(), "AdKats_FetchAuthorizedSoldiers", new List<MatchArgumentFormat>(), new ExecutionRequirements(ExecutionScope.None), "Useable by other plugins to fetch authorized soldiers.");
            _subscribeAsClientMatchCommand = new MatchCommand("AdKats", "SubscribeAsClient", new List<String>(), "AdKats_SubscribeAsClient", new List<MatchArgumentFormat>(), new ExecutionRequirements(ExecutionScope.None), "Useable by other plugins to subscribe to group events.");
            //Debug level is 0 by default
            Log.DebugLevel = 0;

            //Setting Sections
            AddSettingSection("*", _AllSettingSections);
            AddSettingSection("0", "Instance Settings");
            AddSettingSection("1", "Server Settings");
            AddSettingSection("2", "MySQL Settings");
            AddSettingSection("3", "User Settings");
            AddSettingSection("3-2", "Special Player Display");
            AddSettingSection("3-3", "Verbose Special Player Display");
            AddSettingSection("4", "Role Settings");
            AddSettingSection("4-2", "Role Group Settings");
            AddSettingSection("5", "Command Settings");
            AddSettingSection("6", "Command List");
            AddSettingSection("7", "Punishment Settings");
            AddSettingSection("8", "Email Settings");
            AddSettingSection("8-2", "PushBullet Settings");
            AddSettingSection("8-3", "Discord WebHook Settings");
            AddSettingSection("9", "TeamSwap Settings");
            AddSettingSection("A10", "Admin Assistant Settings");
            AddSettingSection("A11", "Player Mute Settings");
            AddSettingSection("A12", "Messaging Settings");
            AddSettingSection("A12-2", "SpamBot Settings");
            AddSettingSection("A12-3", "Battlecry Settings - Thanks WDF");
            AddSettingSection("A12-4", "All-Caps Chat Monitor");
            AddSettingSection("A13", "Banning Settings");
            AddSettingSection("A13-2", "Ban Enforcer Settings");
            AddSettingSection("A13-3", "Mini Ban Management");
            AddSettingSection("A14", "External Command Settings");
            AddSettingSection("A15", "VOIP Settings");
            AddSettingSection("A16", "Orchestration Settings");
            AddSettingSection("A17", "Round Settings");
            AddSettingSection("A17-2", "Round Faction Randomizer Settings - Thanks FPSG");
            AddSettingSection("A18", "AntiCheat Settings");
            AddSettingSection("A19", "Server Rules Settings");
            AddSettingSection("B20", "AFK Settings");
            AddSettingSection("B21", "Ping Enforcer Settings");
            AddSettingSection("B22", "Commander Manager Settings");
            AddSettingSection("B23", "Player Locking Settings");
            AddSettingSection("B24", "Surrender Vote Settings");
            AddSettingSection("B25", "Auto-Surrender Settings");
            AddSettingSection("B25-2", "Auto-Nuke Settings");
            AddSettingSection("B26", "Statistics Settings");
            AddSettingSection("B27", "Populator Monitor Settings - Thanks CMWGaming");
            AddSettingSection("B28", "Teamspeak Player Monitor Settings - Thanks CMWGaming");
            AddSettingSection("B29", "Discord Player Monitor Settings");
            AddSettingSection("B30", "Discord Watchlist Settings");
            AddSettingSection("C30", "Team Power Monitor");
            AddSettingSection("C31", "Weapon Limiter Settings");
            AddSettingSection("C32", "Challenge Settings");
            AddSettingSection("D98", "Database Timing Mismatch");
            AddSettingSection("D99", "Debugging");
            AddSettingSection("X98", "Proxy Settings");
            AddSettingSection("X99", "Experimental");
            AddSettingSection("Y99", "Event Automation");
            //Build setting section enum
            _SettingSectionEnum = String.Empty;
            Random random = new Random(Environment.TickCount);
            var sections = _SettingSections.Keys.ToList();
            sections.Sort();
            foreach (String sectionKey in sections)
            {
                if (String.IsNullOrEmpty(_SettingSectionEnum))
                {
                    _SettingSectionEnum += "enum.SettingSectionEnum_" + random.Next(100000, 999999) + "(";
                }
                else
                {
                    _SettingSectionEnum += "|";
                }
                _SettingSectionEnum += GetSettingSection(sectionKey);
            }
            _SettingSectionEnum += ")";
            //Set default setting section
            _CurrentSettingSection = GetSettingSection("*");

            //Build event round options enum
            _EventRoundOptionsEnum = String.Empty;
            random = new Random(Environment.TickCount);
            foreach (String map in AEventOption.MapNames.Values)
            {
                foreach (String mode in AEventOption.ModeNames.Values)
                {
                    foreach (String rule in AEventOption.RuleNames.Values.Where(ruleValue => ruleValue != AEventOption.RuleNames[AEventOption.RuleCode.ENDEVENT]))
                    {
                        if (String.IsNullOrEmpty(_EventRoundOptionsEnum))
                        {
                            _EventRoundOptionsEnum += "enum.EventRoundOptionsEnum_" + random.Next(100000, 999999) + "(Remove|";
                        }
                        else
                        {
                            _EventRoundOptionsEnum += "|";
                        }
                        _EventRoundOptionsEnum += map + "/" + mode + "/" + rule;
                    }
                }
            }
            _EventRoundOptionsEnum += ")";

            //Init the punishment severity index
            _PunishmentSeverityIndex = new List<String> {
                "warn",
                "kill",
                "kick",
                "tban60",
                "tban120",
                "tbanday",
                "tban2days",
                "tban3days",
                "tbanweek",
                "tban2weeks",
                "tbanmonth",
                "ban"
            };

            //Init the pre-message list
            _PreMessageList = new List<String> {
                "Predefined message 1",
                "Predefined message 2",
                "Predefined message 3",
                "Predefined message 4",
                "Predefined message 5",
            };

            //Init the spam message lists
            _spamBotSayList = new List<String> {
                "AdminSay1",
                "AdminSay2",
                "AdminSay3"
            };
            foreach (String line in _spamBotSayList)
            {
                _spamBotSayQueue.Enqueue(line);
            }
            _spamBotYellList = new List<String> {
                "AdminYell1",
                "AdminYell2",
                "AdminYell3"
            };
            foreach (String line in _spamBotYellList)
            {
                _spamBotYellQueue.Enqueue(line);
            }
            _spamBotTellList = new List<String> {
                "AdminTell1",
                "AdminTell2",
                "AdminTell3"
            };
            foreach (String line in _spamBotTellList)
            {
                _spamBotTellQueue.Enqueue(line);
            }

            //Fill the population durations
            foreach (PopulationState popState in Enum.GetValues(typeof(PopulationState)).Cast<PopulationState>())
            {
                _populationDurations[popState] = TimeSpan.Zero;
            }

            //Fetch the plugin description and changelog
            FetchPluginDocumentation();

            //Fill command descriptions
            FillCommandDescDictionary();

            //Prepare the keep-alive threads
            SetupStatusMonitor();
            SetupFastStatusMonitor();

            //Start up TeamSpeakClientViewer
            _TeamspeakManager = new TeamSpeakClientViewer(this);
            _DiscordManager = new DiscordManager(this);

            FillReadableMapModeDictionaries();

            try
            {
                //Initialize the weapon name dictionary
                WeaponDictionary = new AWeaponDictionary(this);

                //Initialize the challenge manager
                ChallengeManager = new AChallengeManager(this);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while enabling weapon dictionary or challenge manager.", e));
            }
        }

        // ===========================
        // Metadata Methods
        // ===========================

        public String GetPluginName()
        {
            return "AdKats - Advanced In-Game Admin";
        }

        public String GetPluginVersion()
        {
            return PluginVersion;
        }

        public String GetPluginAuthor()
        {
            return "ColColonCleaner, Prophet731, Hedius, neonardo1";
        }

        public String GetPluginWebsite()
        {
            return "github.com/AdKats/AdKats";
        }

        public String GetPluginDescription()
        {
            String concat = @"
            <p>
                <a href='https://github.com/AdKats/AdKats' name=adkats>
                    <img src='https://raw.githubusercontent.com/AdKats/AdKats/master/images/AdKats.jpg' alt='AdKats Advanced In-Game Admin Tools'>
                </a>
            </p>";
            try
            {
                if (!_fetchedPluginInformation)
                {
                    //Wait up to 10 seconds for the description to fetch
                    Log.Debug(() => "Waiting for plugin information...", 1);
                    _PluginDescriptionWaitHandle.WaitOne(10000);
                }

                //Parse out the descriptions
                if (!String.IsNullOrEmpty(_pluginVersionStatusString))
                {
                    concat += _pluginVersionStatusString;
                }
                if (!String.IsNullOrEmpty(_pluginLinks))
                {
                    concat += _pluginLinks;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching plugin information.", e));
            }
            return concat;
        }

        private String AddSettingSection(String number, String desc)
        {
            _SettingSections[number] = desc;
            return GetSettingSection(number);
        }

        private String GetSettingSection(String number)
        {
            return number + ". " + _SettingSections[number];
        }

        private Boolean IsActiveSettingSection(String number)
        {
            return _CurrentSettingSection == GetSettingSection("*") || _CurrentSettingSection == GetSettingSection(number);
        }


        // ===========================
        // Lifecycle and Monitor Methods
        // ===========================

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion)
        {
            Log.Debug(() => "Entering OnPluginLoaded", 7);
            try
            {
                //Set the server IP
                _serverInfo.ServerIP = strHostName + ":" + strPort;
                //Register all events
                RegisterEvents(GetType().Name,
                    "OnVersion",
                    "OnServerInfo",
                    "OnSoldierHealth",
                    "OnListPlayers",
                    "OnPunkbusterPlayerInfo",
                    "OnReservedSlotsList",
                    "OnPlayerKilled",
                    "OnPlayerIsAlive",
                    "OnPlayerSpawned",
                    "OnPlayerTeamChange",
                    "OnPlayerSquadChange",
                    "OnPlayerJoin",
                    "OnPlayerLeft",
                    "OnPlayerDisconnected",
                    "OnGlobalChat",
                    "OnTeamChat",
                    "OnSquadChat",
                    "OnLevelLoaded",
                    "OnBanAdded",
                    "OnBanRemoved",
                    "OnBanListClear",
                    "OnBanListSave",
                    "OnBanListLoad",
                    "OnBanList",
                    "OnRoundOverTeamScores",
                    "OnRoundOverPlayers",
                    "OnSpectatorListLoad",
                    "OnSpectatorListSave",
                    "OnSpectatorListPlayerAdded",
                    "OnSpectatorListPlayerRemoved",
                    "OnSpectatorListCleared",
                    "OnSpectatorListList",
                    "OnGameAdminLoad",
                    "OnGameAdminSave",
                    "OnGameAdminPlayerAdded",
                    "OnGameAdminPlayerRemoved",
                    "OnGameAdminCleared",
                    "OnGameAdminList",
                    "OnFairFight",
                    "OnIsHitIndicator",
                    "OnCommander",
                    "OnForceReloadWholeMags",
                    "OnServerType",
                    "OnMaxSpectators",
                    "OnTeamFactionOverride",
                    "OnPlayerPingedByAdmin",
                    "OnMapListList",
                    "OnMaplistLoad",
                    "OnMaplistSave",
                    "OnMaplistCleared",
                    "OnMaplistMapAppended",
                    "OnMaplistNextLevelIndex",
                    "OnMaplistMapRemoved",
                    "OnMaplistMapInserted",
                    "OnIPChecked");
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("FATAL ERROR on plugin load.", e));
            }
            Log.Debug(() => "Exiting OnPluginLoaded", 7);
        }

        public void OnPluginEnable()
        {
            try
            {
                //If the finalizer is still alive, inform the user and disable
                if (_Finalizer != null && _Finalizer.IsAlive)
                {
                    Log.Error("Cannot enable plugin while it is shutting down. Please Wait for it to shut down.");
                    Threading.Wait(TimeSpan.FromSeconds(2));
                    //Disable the plugin
                    Disable();
                    return;
                }
                //Create a new thread to activate the plugin
                _Activator = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "Enabler";
                        Thread.Sleep(250);

                        _roundState = RoundState.Loaded;

                        UpdateFactions();

                        // Populate the list of available maps
                        _AvailableMapModes = this.GetMapDefines();
                        if (!_AvailableMapModes.Any())
                        {
                            Log.Error("Available map modes were empty on load.");
                        }

                        //Add command informing other plugins that AdKats is enabling
                        RegisterCommand(_PluginEnabledMatchCommand);
                        RegisterCommand(_subscribeAsClientMatchCommand);

                        if (_pluginRebootOnDisable)
                        {
                            if (!String.IsNullOrEmpty(_pluginRebootOnDisableSource))
                            {
                                PlayerTellMessage(_pluginRebootOnDisableSource, "AdKats is Rebooting");
                            }
                            _pluginRebootOnDisable = false;
                            _pluginRebootOnDisableSource = null;
                        }

                        if ((UtcNow() - _proconStartTime).TotalSeconds <= 25)
                        {
                            Log.Write("Waiting a few seconds for requirements and other plugins to initialize, please wait...");
                            //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                            for (Int32 index = 25 - (Int32)(UtcNow() - _proconStartTime).TotalSeconds; index > 0; index--)
                            {
                                Log.Write(index + "...");
                                Threading.Wait(1000);
                            }
                        }

                        //Make sure the default in-game admin is disabled
                        ExecuteCommand("procon.protected.plugins.enable", "CInGameAdmin", "False");

                        //Initialize the stat library
                        _StatLibrary = new StatLibrary(this);
                        if (_StatLibrary.PopulateWeaponStats())
                        {
                            Log.Success("Fetched " + _StatLibrary.Weapons.Count + " " + GameVersion + " weapon stat definitions.");
                        }
                        else
                        {
                            Log.Error("Failed to fetch weapon stat definitions. AdKats cannot be started.");
                            Disable();
                            Threading.StopWatchdog();
                            return;
                        }

                        //Fetch all reputation information
                        if (PopulateCommandReputationDictionaries())
                        {
                            Log.Success("Fetched reputation definitions.");
                        }
                        else
                        {
                            Log.Error("Failed to fetch reputation definitions. AdKats cannot be started.");
                            Disable();
                            Threading.StopWatchdog();
                            return;
                        }

                        if (GameVersion == GameVersionEnum.BF3 ||
                            GameVersion == GameVersionEnum.BF4 ||
                            GameVersion == GameVersionEnum.BFHL)
                        {
                            //Fetch all weapon information
                            if (WeaponDictionary.PopulateDictionaries())
                            {
                                Log.Success("Fetched weapon information.");
                            }
                            else
                            {
                                Log.Error("Failed to fetch weapon information. AdKats cannot be started.");
                                Disable();
                                Threading.StopWatchdog();
                                return;
                            }
                        }

                        //Fetch all special player group information
                        if (PopulateSpecialGroupDictionaries())
                        {
                            Log.Success("Fetched special player group definitions.");
                        }
                        else
                        {
                            Log.Error("Failed to fetch special player group definitions. AdKats cannot be started.");
                            Disable();
                            Threading.StopWatchdog();
                            return;
                        }

                        //Fetch global timing
                        TimeSpan diffUTCGlobal;
                        _globalTimingValid = TestGlobalTiming(false, true, out diffUTCGlobal);
                        _globalTimingOffset = diffUTCGlobal;

                        //Inform of IP
                        Log.Success("Server IP is " + _serverInfo.ServerIP.ToString());

                        //Set the enabled variable
                        _pluginEnabled = true;

                        //Init and start all the threads
                        InitWaitHandles();
                        OpenAllHandles();
                        InitThreads();
                        StartThreads();
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error while enabling AdKats.", e));
                    }
                    Threading.StopWatchdog();
                }));

                Log.Write("^b^2ENABLED!^n^0 Beginning startup sequence...");
                //Start the thread
                Threading.StartWatchdog(_Activator);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while initializing activator thread.", e));
            }
        }

        public void OnPluginDisable()
        {
            //If the plugin is already disabling then cancel
            if (_Finalizer != null && _Finalizer.IsAlive)
            {
                return;
            }
            try
            {
                //Create a new thread to disabled the plugin
                _Finalizer = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "Finalizer";
                        Log.Info("Shutting down AdKats.");
                        //Disable settings
                        _pluginEnabled = false;
                        _threadsReady = false;
                        //Remove all match commands
                        UnregisterCommand(_PluginEnabledMatchCommand);
                        UnregisterCommand(_issueCommandMatchCommand);
                        UnregisterCommand(_fetchAuthorizedSoldiersMatchCommand);
                        UnregisterCommand(_subscribeAsClientMatchCommand);

                        //Open all handles. Threads will finish on their own.
                        OpenAllHandles();
                        Threading.MonitorShutdown();

                        //Reset all caches and storage
                        if (_UserRemovalQueue != null)
                        {
                            _UserRemovalQueue.Clear();
                        }
                        if (_UserUploadQueue != null)
                        {
                            _UserUploadQueue.Clear();
                        }
                        if (_TeamswapForceMoveQueue != null)
                        {
                            _TeamswapForceMoveQueue.Clear();
                        }
                        if (_TeamswapOnDeathCheckingQueue != null)
                        {
                            _TeamswapOnDeathCheckingQueue.Clear();
                        }
                        if (_TeamswapOnDeathMoveDic != null)
                        {
                            _TeamswapOnDeathMoveDic.Clear();
                        }
                        if (_UnparsedCommandQueue != null)
                        {
                            _UnparsedCommandQueue.Clear();
                        }
                        if (_UnparsedMessageQueue != null)
                        {
                            _UnparsedMessageQueue.Clear();
                        }
                        if (_UnprocessedActionQueue != null)
                        {
                            _UnprocessedActionQueue.Clear();
                        }
                        if (_UnprocessedRecordQueue != null)
                        {
                            _UnprocessedRecordQueue.Clear();
                        }
                        if (_UnprocessedStatisticQueue != null)
                        {
                            _UnprocessedStatisticQueue.Clear();
                        }
                        if (_BanEnforcerCheckingQueue != null)
                        {
                            _BanEnforcerCheckingQueue.Clear();
                        }
                        if (_AssistAttemptQueue != null)
                        {
                            _AssistAttemptQueue.Clear();
                        }
                        if (_Team2MoveQueue != null)
                        {
                            _Team2MoveQueue.Clear();
                        }
                        if (_Team1MoveQueue != null)
                        {
                            _Team1MoveQueue.Clear();
                        }
                        if (_RoundCookers != null)
                        {
                            _RoundCookers.Clear();
                        }
                        if (_PlayerReports != null)
                        {
                            _PlayerReports.Clear();
                        }
                        if (_RoundMutedPlayers != null)
                        {
                            _RoundMutedPlayers.Clear();
                        }
                        if (_PlayerDictionary != null)
                        {
                            _PlayerDictionary.Clear();
                        }
                        if (_PlayerLeftDictionary != null)
                        {
                            _PlayerLeftDictionary.Clear();
                        }
                        if (_FetchedPlayers != null)
                        {
                            _FetchedPlayers.Clear();
                        }
                        _firstPlayerListComplete = false;
                        _firstUserListComplete = false;
                        _firstPlayerListStarted = false;
                        if (_userCache != null)
                        {
                            _userCache.Clear();
                        }
                        if (FrostbitePlayerInfoList != null)
                        {
                            FrostbitePlayerInfoList.Clear();
                        }
                        if (_CBanProcessingQueue != null)
                        {
                            _CBanProcessingQueue.Clear();
                        }
                        if (_BanEnforcerProcessingQueue != null)
                        {
                            _BanEnforcerProcessingQueue.Clear();
                        }
                        if (_ActOnSpawnDictionary != null)
                        {
                            _ActOnSpawnDictionary.Clear();
                        }
                        if (_ActOnIsAliveDictionary != null)
                        {
                            _ActOnIsAliveDictionary.Clear();
                        }
                        if (_ActionConfirmDic != null)
                        {
                            _ActionConfirmDic.Clear();
                        }
                        if (_LoadoutConfirmDictionary != null)
                        {
                            _LoadoutConfirmDictionary.Clear();
                        }
                        _AntiCheatCheckedPlayers.Clear();
                        _AntiCheatCheckedPlayersStats.Clear();
                        _unmatchedRoundDeathCounts.Clear();
                        _unmatchedRoundDeaths.Clear();
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
                        _pluginUpdateServerInfoChecked = false;
                        _databaseConnectionCriticalState = false;
                        _databaseSuccess = 0;
                        _databaseTimeouts = 0;
                        _pingKicksTotal = 0;
                        if (_subscribedClients.Any())
                        {
                            Log.Warn("All active subscriptions removed.");
                            _subscribedClients.Clear();
                        }
                        //Now that plugin is disabled, update the settings page to reflect
                        UpdateSettingPage();
                        Log.Write("^b^1AdKats " + GetPluginVersion() + " Disabled! =(^n^0");
                        //Automatic Enable
                        if (_pluginRebootOnDisable && !_useKeepAlive)
                        {
                            Enable();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error occured while disabling Adkats.", e));
                    }
                }));

                //Start the finalizer thread
                _Finalizer.Start();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured while initializing AdKats disable thread.", e));
            }
        }

        private void FetchPluginDocumentation()
        {
            if (Threading.IsAlive("DescFetching"))
            {
                return;
            }
            _PluginDescriptionWaitHandle.Reset();
            //Create a new thread to fetch the plugin description and changelog
            Thread descFetcher = new Thread(new ThreadStart(delegate
            {
                try
                {
                    Thread.CurrentThread.Name = "DescFetching";
                    _pluginDescFetchProgress = "Started";
                    //Download the readme and changelog
                    Log.Debug(() => "Fetching plugin links...", 2);
                    try
                    {
                        _pluginLinks = Util.HttpDownload("https://raw.githubusercontent.com/AdKats/AdKats/master/LINKS.md?cacherand=" + Environment.TickCount);
                        Log.Debug(() => "Plugin links fetched.", 1);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            _pluginLinks = Util.HttpDownload("https://adkats.e4gl.com/LINKS.md?cacherand=" + Environment.TickCount);
                            Log.Debug(() => "Plugin links fetched from backup location.", 1);
                        }
                        catch (Exception)
                        {
                            Log.Error("Failed to fetch plugin links.");
                        }
                    }
                    _pluginDescFetchProgress = "LinksFetched";
                    Log.Debug(() => "Fetching plugin readme...", 2);
                    try
                    {
                        _pluginDescription = Util.HttpDownload("https://raw.githubusercontent.com/AdKats/AdKats/master/README.md?cacherand=" + Environment.TickCount);
                        Log.Debug(() => "Plugin readme fetched.", 1);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            _pluginDescription = Util.HttpDownload("https://adkats.e4gl.com/README.md?cacherand=" + Environment.TickCount);
                            Log.Debug(() => "Plugin readme fetched from backup location.", 1);
                        }
                        catch (Exception)
                        {
                            Log.Error("Failed to fetch plugin readme.");
                        }
                    }
                    _pluginDescFetchProgress = "DescFetched";
                    Log.Debug(() => "Fetching plugin changelog...", 2);
                    try
                    {
                        _pluginChangelog = Util.HttpDownload("https://raw.githubusercontent.com/AdKats/AdKats/master/CHANGELOG.md?cacherand=" + Environment.TickCount);
                        Log.Debug(() => "Plugin changelog fetched.", 1);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            _pluginChangelog = Util.HttpDownload("https://adkats.e4gl.com/CHANGELOG.md?cacherand=" + Environment.TickCount);
                            Log.Debug(() => "Plugin changelog fetched from backup location.", 1);
                        }
                        catch (Exception)
                        {
                            Log.Error("Failed to fetch plugin changelog.");
                        }
                    }
                    _pluginDescFetchProgress = "ChangeFetched";
                    if (!String.IsNullOrEmpty(_pluginDescription))
                    {
                        //Extract the latest stable version
                        String latestStableVersion = ExtractString(_pluginDescription, "latest_stable_release");
                        if (!String.IsNullOrEmpty(latestStableVersion))
                        {
                            _latestPluginVersion = latestStableVersion;
                            _latestPluginVersionInt = ConvertVersionInt(latestStableVersion);
                            //Get current plugin version
                            _currentPluginVersionInt = ConvertVersionInt(PluginVersion);

                            String versionStatus = String.Empty;
                            //Add the appropriate message to plugin description
                            if (_latestPluginVersionInt > _currentPluginVersionInt)
                            {
                                if (_pluginUpdatePatched)
                                {
                                    versionStatus = @"
                                    <h2 style='color:#DF0101;'>
                                        You are running an outdated version! The update has been patched, reboot PRoCon to run version " + latestStableVersion + @"!
                                    </h2>";
                                }
                                else
                                {
                                    versionStatus = @"
                                    <h2 style='color:#DF0101;'>
                                        You are running an outdated version! Version " + latestStableVersion + @" is available for download!
                                    </h2>
                                    <a href='https://github.com/AdKats/AdKats/' target='_blank'>
                                        Download Version " + latestStableVersion + @"!
                                    </a><br/>
                                    Download link below.";
                                }
                                _pluginVersionStatus = VersionStatus.OutdatedBuild;
                            }
                            else if (_latestPluginVersionInt == _currentPluginVersionInt)
                            {
                                versionStatus = @"
                                <h2 style='color:#01DF01;'>
                                    Congrats! You are running the latest stable version!
                                </h2>";
                                _pluginVersionStatus = VersionStatus.StableBuild;
                            }
                            else if (_latestPluginVersionInt < _currentPluginVersionInt)
                            {
                                versionStatus = @"
                                <h2 style='color:#FF8000;'>
                                    CAUTION! You are running a TEST version! Functionality might not be completely tested.
                                </h2>";
                                _pluginVersionStatus = VersionStatus.TestBuild;
                            }
                            else
                            {
                                _pluginVersionStatus = VersionStatus.UnknownBuild;
                            }
                            //Prepend the message
                            _pluginVersionStatusString = versionStatus;
                            _pluginDescFetchProgress = "VersionStatusSet";
                            //Check for plugin updates
                            CheckForPluginUpdates(false);
                            _pluginDescFetchProgress = "UpdateChecked";
                        }
                    }
                    else if (!_fetchedPluginInformation)
                    {
                        Log.Error("Unable to fetch required documentation files. AdKats cannot be started.");
                        Disable();
                        Threading.StopWatchdog();
                        return;
                    }
                    Log.Debug(() => "Setting desc fetch handle.", 1);
                    _fetchedPluginInformation = true;
                    _LastPluginDescFetch = UtcNow();
                    _PluginDescriptionWaitHandle.Set();
                    _pluginDescFetchProgress = "Completed";
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error while fetching plugin description and changelog.", e));
                }
                Threading.StopWatchdog();
            }));
            //Start the thread
            Threading.StartWatchdog(descFetcher);
        }

        private void RunMemoryMonitor()
        {
            try
            {
                //Memory Monitor - Every 60 seconds
                _MemoryUsageCurrent = (Int32)(GC.GetTotalMemory(true) / 1024 / 1024);
                if (NowDuration(_LastMemoryWarning).TotalSeconds > 60)
                {
                    if (_MemoryUsageCurrent >= _MemoryUsageRestartProcon && NowDuration(_proconStartTime).TotalMinutes > 30 && _firstPlayerListComplete)
                    {
                        Environment.Exit(2232);
                    }
                    else if (_MemoryUsageCurrent >= _MemoryUsageRestartPlugin && NowDuration(_AdKatsRunningTime).TotalMinutes > 30 && _firstPlayerListComplete)
                    {
                        Log.Warn(_MemoryUsageCurrent + "MB estimated memory used.");
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("plugin_restart"),
                            command_numeric = 0,
                            target_name = "AdKats",
                            source_name = "MemoryMonitor",
                            record_message = _MemoryUsageCurrent + "MB estimated memory used",
                            record_time = UtcNow()
                        });
                        _LastMemoryWarning = UtcNow();
                    }
                    else if (_MemoryUsageCurrent >= _MemoryUsageWarn)
                    {
                        String mm = " MAP: ";
                        mm += "1:" + Threading.Count() + ", ";
                        mm += "2:" + _populationPopulatingPlayers.Count() + ", ";
                        mm += "3:" + _ActOnIsAliveDictionary.Count() + ", ";
                        mm += "4:" + _ActOnSpawnDictionary.Count() + ", ";
                        mm += "5:" + _LoadoutConfirmDictionary.Count() + ", ";
                        mm += "6:" + _ActionConfirmDic.Count() + ", ";
                        mm += "7:" + _PlayerReports.Count() + ", ";
                        mm += "8:" + _userCache.Count() + ", ";
                        mm += "9:" + _specialPlayerGroupIDDictionary.Count() + ", ";
                        mm += "10:" + _specialPlayerGroupKeyDictionary.Count() + ", ";
                        mm += "11:" + _baseSpecialPlayerCache.Count() + ", ";
                        mm += "12:" + _verboseSpecialPlayerCache.Count() + ", ";
                        mm += "13:" + _roundAssists.Count() + ", ";
                        mm += "14:" + _PlayerDictionary.Count() + ", ";
                        mm += "15:" + _RoundPrepSquads.Count() + ", ";
                        mm += "16:" + _PlayerLeftDictionary.Count() + ", ";
                        mm += "17:" + _FetchedPlayers.Count() + ", ";
                        mm += "19:" + _populatorPlayers.Count() + ", ";
                        mm += "20:" + _TeamspeakPlayers.Count() + ", ";
                        mm += "21:" + _RoundCookers.Count() + ", ";
                        mm += "22:" + _BanEnforcerCheckingQueue.Count() + ", ";
                        mm += "23:" + _AntiCheatQueue.Count() + ", ";
                        mm += "24:" + _KillProcessingQueue.Count() + ", ";
                        mm += "25:" + _PlayerListProcessingQueue.Count() + ", ";
                        mm += "26:" + _PlayerRemovalProcessingQueue.Count() + ", ";
                        mm += "27:" + _SettingUploadQueue.Count() + ", ";
                        mm += "28:" + _UnparsedCommandQueue.Count() + ", ";
                        mm += "29:" + _UnparsedMessageQueue.Count() + ", ";
                        mm += "30:" + _UnprocessedActionQueue.Count() + ", ";
                        mm += "31:" + _UnprocessedRecordQueue.Count() + ", ";
                        mm += "32:" + _UnprocessedStatisticQueue.Count() + ", ";
                        mm += "33:" + _UserRemovalQueue.Count() + ", ";
                        mm += "34:" + _UserUploadQueue.Count() + ", ";
                        mm += "35:" + _BattlelogFetchQueue.Count() + ", ";
                        mm += "36:" + _IPInfoFetchQueue.Count() + ", ";
                        mm += "37:" + _CommandIDDictionary.Count() + ", ";
                        mm += "38:" + _CommandKeyDictionary.Count() + ", ";
                        mm += "39:" + _CommandNameDictionary.Count() + ", ";
                        mm += "41:" + _CommandTextDictionary.Count() + ", ";
                        mm += "42:" + _RoleIDDictionary.Count() + ", ";
                        mm += "43:" + _RoleKeyDictionary.Count() + ", ";
                        mm += "44:" + _RoleNameDictionary.Count() + ", ";
                        mm += "45:" + _teamDictionary.Count() + ", ";
                        mm += "46:" + _TeamswapOnDeathMoveDic.Count() + ", ";
                        mm += "47:" + _DiscordPlayers.Count() + ", ";
                        mm += "48:" + ChallengeManager.Definitions.Count() + ", ";
                        mm += "49:" + ChallengeManager.Rules.Count() + ", ";
                        mm += "50:" + ChallengeManager.Entries.Count() + ", ";
                        mm += "51:" + ChallengeManager.CompletedRoundEntries.Count() + ", ";
                        Log.Warn(_MemoryUsageCurrent + "MB estimated memory used." + mm);
                        _LastMemoryWarning = UtcNow();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running memory monitor.", e));
            }
        }

        private void RunPlayerListingStatMonitor()
        {
            try
            {
                if (_DebugPlayerListing && NowDuration(_LastDebugPlayerListingMessage).TotalSeconds > 30.0)
                {
                    Log.Info("PlayerListing: " + Math.Round(getPlayerListTriggerRate(), 1) + " Triggered/min, " + Math.Round(getPlayerListReceiveRate(), 1) + " Received/min, " + Math.Round(getPlayerListAcceptRate(), 1) + " Accepted/min, " + Math.Round(getPlayerListProcessedRate(), 1) + " Processed/min");
                    _LastDebugPlayerListingMessage = UtcNow();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running playerliststat monitor.", e));
            }
        }

        private void RunUnswitcherMonitor()
        {
            try
            {
                //Check for unswitcher disable - every 5 seconds
                if (_pluginEnabled && _MULTIBalancerUnswitcherDisabled && (UtcNow() - _LastPlayerMoveIssued).TotalSeconds > 5)
                {
                    Log.Debug(() => "MULTIBalancer Unswitcher Re-Enabled", 3);
                    ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "False");
                    _MULTIBalancerUnswitcherDisabled = false;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running unswitcher monitor.", e));
            }
        }

        private void RunDocumentationMonitor()
        {
            try
            {
                //Check for plugin updates at interval
                if ((UtcNow() - _LastPluginDescFetch).TotalHours > 1)
                {
                    FetchPluginDocumentation();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running documentation monitor.", e));
            }
        }

        private void RunSpambotMonitor()
        {
            try
            {
                //SpamBot - Every 500ms
                var playerCount = GetPlayerCount();
                if (_pluginEnabled &&
                    _spamBotEnabled &&
                    _firstPlayerListComplete &&
                    playerCount > 0)
                {
                    if ((UtcNow() - _spamBotSayLastPost).TotalSeconds > _spamBotSayDelaySeconds && _spamBotSayQueue.Any())
                    {
                        Boolean posted = false;
                        Int32 attempts = 0;
                        do
                        {
                            String message = _spamBotSayQueue.Peek();
                            message = ConfirmSpambotMessageValid(message);
                            if (!String.IsNullOrEmpty(message))
                            {
                                message = ReplaceSpambotEventInfo(message);
                                message = "[SpamBotMessage]" + message;
                                if (_spamBotExcludeWhitelist)
                                {
                                    OnlineNonWhitelistSayMessage(message, playerCount > 5);
                                }
                                else
                                {
                                    AdminSayMessage(message, playerCount > 5);
                                }
                                posted = true;
                                _spamBotSayLastPost = UtcNow();
                            }
                            _spamBotSayQueue.Enqueue(_spamBotSayQueue.Dequeue());
                        } while (!posted && ++attempts < _spamBotSayQueue.Count());
                    }
                    if ((UtcNow() - _spamBotYellLastPost).TotalSeconds > _spamBotYellDelaySeconds && _spamBotYellQueue.Any())
                    {
                        Boolean posted = false;
                        Int32 attempts = 0;
                        do
                        {
                            String message = _spamBotYellQueue.Peek();
                            message = ConfirmSpambotMessageValid(message);
                            if (!String.IsNullOrEmpty(message))
                            {
                                message = ReplaceSpambotEventInfo(message);
                                message = "[SpamBotMessage]" + message;
                                if (_spamBotExcludeWhitelist)
                                {
                                    OnlineNonWhitelistYellMessage(message, playerCount > 5);
                                }
                                else
                                {
                                    AdminYellMessage(message, playerCount > 5, 0);
                                }
                                posted = true;
                                _spamBotYellLastPost = UtcNow();
                            }
                            _spamBotYellQueue.Enqueue(_spamBotYellQueue.Dequeue());
                        } while (!posted && ++attempts < _spamBotYellQueue.Count());
                    }
                    if ((UtcNow() - _spamBotTellLastPost).TotalSeconds > _spamBotTellDelaySeconds && _spamBotTellQueue.Any())
                    {
                        Boolean posted = false;
                        Int32 attempts = 0;
                        do
                        {
                            String message = _spamBotTellQueue.Peek();
                            message = ConfirmSpambotMessageValid(message);
                            if (!String.IsNullOrEmpty(message))
                            {
                                message = ReplaceSpambotEventInfo(message);
                                message = "[SpamBotMessage]" + message;
                                if (_spamBotExcludeWhitelist)
                                {
                                    OnlineNonWhitelistTellMessage(message, playerCount > 5);
                                }
                                else
                                {
                                    AdminTellMessage(message, playerCount > 5);
                                }
                                posted = true;
                                _spamBotTellLastPost = UtcNow();
                            }
                            _spamBotTellQueue.Enqueue(_spamBotTellQueue.Dequeue());
                        } while (!posted && ++attempts < _spamBotTellQueue.Count());
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running spambot monitor.", e));
            }
        }

        private String ConfirmSpambotMessageValid(String messageString)
        {
            //Confirm that rule prefixes conform to the map/modes available
            var allMaps = _AvailableMapModes.Select(mapMode => mapMode.PublicLevelName).Distinct().ToArray();
            var allModes = _AvailableMapModes.Select(mapMode => mapMode.GameMode).Distinct().ToArray();
            var matchingMapMode = _AvailableMapModes.FirstOrDefault(mapMode => mapMode.FileName == _serverInfo.InfoObject.Map &&
                                                                               mapMode.PlayList == _serverInfo.InfoObject.GameMode);
            if (matchingMapMode != null)
            {
                var serverMap = matchingMapMode.PublicLevelName;
                var serverMode = matchingMapMode.GameMode;
                //Check if the rule starts with any map
                foreach (var ruleMap in allMaps)
                {
                    if (messageString.StartsWith(ruleMap + "/"))
                    {
                        //Remove the map from the rule text
                        messageString = TrimStart(messageString, ruleMap + "/");
                        if (ruleMap != serverMap)
                        {
                            return null;
                        }
                        break;
                    }
                }
                //Check if the rule starts with any mode
                foreach (var ruleMode in allModes)
                {
                    if (messageString.StartsWith(ruleMode + "/"))
                    {
                        //Remove the mode from the rule text
                        messageString = TrimStart(messageString, ruleMode + "/");
                        if (ruleMode != serverMode)
                        {
                            return null;
                        }
                        break;
                    }
                }
                //Check again for maps, since they might have put them in a different order
                foreach (var ruleMap in allMaps)
                {
                    if (messageString.StartsWith(ruleMap + "/"))
                    {
                        //Remove the map from the rule text
                        messageString = TrimStart(messageString, ruleMap + "/");
                        if (ruleMap != serverMap)
                        {
                            return null;
                        }
                        break;
                    }
                }
            }
            return messageString;
        }

        private String ReplaceSpambotEventInfo(String message)
        {
            var eventDate = GetEventRoundDateTime();
            if (((_CurrentEventRoundNumber == 999999 && eventDate < DateTime.Now) || _CurrentEventRoundNumber < _roundID) && !EventActive())
            {
                message = message.Replace("%EventDateDuration%", "TBD")
                                 .Replace("%EventDateTime%", "TBD")
                                 .Replace("%EventDate%", "TBD")
                                 .Replace("%EventRound%", "TBD")
                                 .Replace("%RemainingRounds%", "TBD")
                                 .Replace("%s%", "s")
                                 .Replace("%S%", "S");
            }
            else
            {
                if (message.Contains("%EventDateDuration%"))
                {
                    message = message.Replace("%EventDateDuration%", FormatTimeString(eventDate - DateTime.Now, 3));
                }
                if (message.Contains("%EventDateTime%"))
                {
                    message = message.Replace("%EventDateTime%", eventDate.ToShortDateString() + " " + eventDate.ToShortTimeString());
                }
                if (message.Contains("%EventDate%"))
                {
                    message = message.Replace("%EventDate%", eventDate.ToShortDateString());
                }
                if (message.Contains("%CurrentRound%"))
                {
                    message = message.Replace("%CurrentRound%", String.Format("{0:n0}", _roundID));
                }
                if (message.Contains("%EventRound%"))
                {
                    if (_CurrentEventRoundNumber != 999999)
                    {
                        message = message.Replace("%EventRound%", String.Format("{0:n0}", _CurrentEventRoundNumber));
                    }
                    else
                    {
                        message = message.Replace("%EventRound%", String.Format("{0:n0}", FetchEstimatedEventRoundNumber()));
                    }
                }
                if (message.Contains("%RemainingRounds%"))
                {
                    var remainingRounds = 0;
                    if (_CurrentEventRoundNumber != 999999)
                    {
                        remainingRounds = _CurrentEventRoundNumber - _roundID;
                        message = message.Replace("%RemainingRounds%", String.Format("{0:n0}", Math.Max(remainingRounds, 0)));
                        message = message.Replace("%s%", remainingRounds > 1 ? "s" : "");
                        message = message.Replace("%S%", remainingRounds > 1 ? "S" : "");
                    }
                    else
                    {
                        remainingRounds = FetchEstimatedEventRoundNumber() - _roundID;
                        message = message.Replace("%RemainingRounds%", String.Format("{0:n0}", Math.Max(remainingRounds, 0)));
                        message = message.Replace("%s%", remainingRounds > 1 ? "s" : "");
                        message = message.Replace("%S%", remainingRounds > 1 ? "S" : "");
                    }
                }
            }
            return message;
        }

        private void RunAutoAssistMonitor()
        {
            try
            {
                //Automatic Assisting Check - Every 500ms
                // Are there any records to process
                if (_roundState == RoundState.Playing &&
                    _AssistAttemptQueue.Any() &&
                    !_Team1MoveQueue.Any() &&
                    !_Team2MoveQueue.Any() &&
                    NowDuration(_LastAutoAssist).TotalSeconds > 10.0)
                {
                    lock (_AssistAttemptQueue)
                    {
                        // There are, look at the first one without pulling it
                        var assistRecord = _AssistAttemptQueue.Peek();
                        if (NowDuration(assistRecord.record_creationTime).TotalMinutes > 5.0)
                        {
                            // If the record is more than 5 minutes old, get rid of it
                            SendMessageToSource(assistRecord, Log.CViolet("Automatic assist has timed out. Please use " + GetChatCommandByKey("self_assist") + " again to re-queue yourself."));
                            OnlineAdminSayMessage("Automatic assist timed out for " + assistRecord.GetSourceName());
                            _AssistAttemptQueue.Dequeue();
                            return;
                        }
                        else
                        {
                            // The record is active, see if the player can be automatically assisted
                            if (RunAssist(assistRecord.target_player, assistRecord, null, true))
                            {
                                QueueRecordForProcessing(assistRecord);
                                _AssistAttemptQueue.Dequeue();
                                _LastAutoAssist = UtcNow();
                                return;
                            }
                        }
                        var team1Assist = _AssistAttemptQueue.FirstOrDefault(attempt => attempt.source_player != null &&
                                                                                        attempt.source_player.fbpInfo != null &&
                                                                                        attempt.source_player.fbpInfo.TeamID == 1);
                        var team2Assist = _AssistAttemptQueue.FirstOrDefault(attempt => attempt.source_player != null &&
                                                                                        attempt.source_player.fbpInfo != null &&
                                                                                        attempt.source_player.fbpInfo.TeamID == 2);
                        if (team1Assist != null &&
                            team2Assist != null)
                        {
                            // There is a player on each team attempting to switch. Allow the swap.
                            AdminSayMessage(Log.CViolet(team1Assist.GetTargetNames() + " (" + Math.Round(team1Assist.target_player.GetPower(true)) + ") assist SWAP accepted, queueing."));
                            QueueRecordForProcessing(team1Assist);
                            AdminSayMessage(Log.CViolet(team2Assist.GetTargetNames() + " (" + Math.Round(team2Assist.target_player.GetPower(true)) + ") assist SWAP accepted, queueing."));
                            QueueRecordForProcessing(team2Assist);
                            //The players are queued, rebuild the attempt queue without them in it
                            _AssistAttemptQueue = new Queue<ARecord>(_AssistAttemptQueue.Where(aRec => aRec != team1Assist &&
                                                                                                       aRec != team2Assist));
                            _LastAutoAssist = UtcNow();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running autoassist monitor.", e));
            }
        }

        private void RunPurgeMonitor()
        {
            try
            {
                //Keep the extended round stats table clean
                if (_pluginEnabled &&
                    _firstPlayerListComplete)
                {
                    PurgeExtendedRoundStats();
                    PurgeOutdatedStatistics();
                    PurgeOutdatedExceptions();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running purge monitor.", e));
            }
        }

        private void RunAutomaticRestartMonitor()
        {
            try
            {
                //Check for automatic restart window
                if (_automaticServerRestart &&
                    _pluginEnabled &&
                    _threadsReady &&
                    _firstPlayerListComplete)
                {
                    Boolean restart = true;
                    var uptime = TimeSpan.FromSeconds(_serverInfo.InfoObject.ServerUptime);
                    var uptimeString = FormatTimeString(uptime, 3);
                    if (uptime.TotalHours < _automaticServerRestartMinHours)
                    {
                        restart = false;
                    }
                    if (restart && GetPlayerCount() >= 1)
                    {
                        restart = false;
                    }
                    if (restart)
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("server_shutdown"),
                            target_name = "Server",
                            source_name = "AutoAdmin",
                            record_message = "Automatic Server Restart [" + FormatTimeString(uptime, 3) + "]",
                            record_time = UtcNow()
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running restart monitor.", e));
            }
        }

        private void RunPingStatisticsMonitor()
        {
            try
            {
                if (_UseExperimentalTools && _roundState == RoundState.Playing)
                {
                    var players = _PlayerDictionary.Values.ToList();
                    double total = players.Count();
                    if (total > 0)
                    {
                        double over50 = players.Count(aPlayer => aPlayer.player_ping_avg > 50);
                        double over100 = players.Count(aPlayer => aPlayer.player_ping_avg > 100);
                        double over150 = players.Count(aPlayer => aPlayer.player_ping_avg > 150);
                        double over50p = Math.Round(over50 / total * 100, 1);
                        double over100p = Math.Round(over100 / total * 100, 1);
                        double over150p = Math.Round(over150 / total * 100, 1);
                        string over100t = "Over 50ms: (" + Math.Round(over50) + "/" + total + ") " + over50p + "%";
                        string over150t = "Over 100ms: (" + Math.Round(over100) + "/" + total + ") " + over100p + "%";
                        string over200t = "Over 150ms: (" + Math.Round(over150) + "/" + total + ") " + over150p + "%";
                        QueueStatisticForProcessing(new AStatistic()
                        {
                            stat_type = AStatistic.StatisticType.ping_over50,
                            server_id = _serverInfo.ServerID,
                            round_id = _roundID,
                            target_name = _serverInfo.InfoObject.Map,
                            stat_value = over50p,
                            stat_comment = over100t,
                            stat_time = UtcNow()
                        });
                        QueueStatisticForProcessing(new AStatistic()
                        {
                            stat_type = AStatistic.StatisticType.ping_over100,
                            server_id = _serverInfo.ServerID,
                            round_id = _roundID,
                            target_name = _serverInfo.InfoObject.Map,
                            stat_value = over100p,
                            stat_comment = over150t,
                            stat_time = UtcNow()
                        });
                        QueueStatisticForProcessing(new AStatistic()
                        {
                            stat_type = AStatistic.StatisticType.ping_over150,
                            server_id = _serverInfo.ServerID,
                            round_id = _roundID,
                            target_name = _serverInfo.InfoObject.Map,
                            stat_value = over150p,
                            stat_comment = over200t,
                            stat_time = UtcNow()
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running pingstats monitor.", e));
            }
        }

        private void RunPlayerListingMonitor()
        {
            try
            {
                //Player listing check
                if (_pluginEnabled &&
                    _firstPlayerListComplete &&
                    NowDuration(_LastPlayerListProcessed).TotalMinutes > 7.5)
                {
                    //Create report record
                    QueueRecordForProcessing(new ARecord
                    {
                        record_source = ARecord.Sources.Automated,
                        server_id = _serverInfo.ServerID,
                        command_type = GetCommandByKey("player_report"),
                        command_numeric = 0,
                        target_name = "AdKats",
                        source_name = "AdKats",
                        record_message = "Player listing offline. Inform ColColonCleaner.",
                        record_time = UtcNow()
                    });
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running playerlist monitor.", e));
            }
        }

        private void RunTeamPowerStatMonitor()
        {
            try
            {
                if (_UseTeamPowerDisplayBalance &&
                    _firstPlayerListComplete)
                {
                    ATeam t1, t2;
                    if (_roundState != RoundState.Loaded && GetTeamByID(1, out t1) && GetTeamByID(2, out t2))
                    {
                        Double t1Power = t1.GetTeamPower();
                        Double t2Power = t2.GetTeamPower();
                        Double percDiff = Math.Abs(t1Power - t2Power) / ((t1Power + t2Power) / 2.0) * 100.0;
                        String message = "";
                        if (t1Power > t2Power)
                        {
                            message += t1.GetTeamIDKey() + " up " + Math.Round(((t1Power - t2Power) / t2Power) * 100) + "% ";
                        }
                        else
                        {
                            message += t2.GetTeamIDKey() + " up " + Math.Round(((t2Power - t1Power) / t1Power) * 100) + "% ";
                        }
                        message += "^n(" + t1.TeamKey + ":" + t1.GetTeamPower() + ":" + t1.GetTeamPower(false) + " / " + t2.TeamKey + ":" + t2.GetTeamPower() + ":" + t2.GetTeamPower(false) + ")";
                        if (GetPlayerCount() > 5)
                        {
                            ProconChatWrite(Log.FBold(message));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running teampowerstat monitor.", e));
            }
        }

        private void RunEventMonitor()
        {
            try
            {
                // EVENTS
                if (_pluginEnabled &&
                    _firstPlayerListComplete)
                {
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
                    if (_UseExperimentalTools &&
                        _EventDate.ToShortDateString() != GetLocalEpochTime().ToShortDateString())
                    {
                        var eventDate = GetEventRoundDateTime();
                        if (DateTime.Now < eventDate &&
                            _CurrentEventRoundNumber == 999999)
                        {
                            // The event date is set, and in the future
                            var estimateEventRoundNumber = FetchEstimatedEventRoundNumber();
                            // At 3 rounds away, lock in the round number for the event
                            if (Math.Abs(estimateEventRoundNumber - _roundID) <= 3)
                            {
                                _CurrentEventRoundNumber = estimateEventRoundNumber;
                                QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                                UpdateSettingPage();
                            }
                        }
                        var serverName = "";
                        // During the event
                        if (EventActive())
                        {
                            serverName = _eventActiveServerName + " " + GetEventMessage(false);
                        }
                        // Immediately before the event
                        else if (_CurrentEventRoundNumber != 999999 &&
                                 _CurrentEventRoundNumber > _roundID)
                        {
                            serverName = _eventConcreteCountdownServerName;
                        }
                        // Before the event
                        else if (DateTime.Now < eventDate &&
                                 Math.Abs((eventDate - DateTime.Now).TotalDays) < _EventAnnounceDayDifference)
                        {
                            serverName = _eventCountdownServerName;
                        }
                        //After the event, and otherwise
                        else
                        {
                            serverName = _eventBaseServerName;
                        }
                        this.ExecuteCommand("procon.protected.send", "vars.serverName", ProcessEventServerName(serverName, false, false));

                        // EVENT AUTOMATIC POLLING
                        if (_EventPollAutomatic &&
                            !_EventRoundPolled &&
                            // Don't auto-poll after 20 event rounds, just in case nobody votes to end it
                            (_EventRoundOptions.Count() < 20 || !EventActive()) &&
                            _roundState == RoundState.Playing &&
                            _serverInfo.GetRoundElapsedTime() >= _EventRoundAutoVoteDuration &&
                            _ActivePoll == null &&
                            (_CurrentEventRoundNumber == _roundID + 1 || EventActive()))
                        {
                            var options = String.Empty;
                            if (_CurrentEventRoundNumber == _roundID + 1)
                            {
                                options = "reset";
                            }
                            QueueRecordForProcessing(new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("poll_trigger"),
                                command_numeric = 0,
                                target_name = "event",
                                source_name = "EventAutoPolling",
                                record_message = options,
                                record_time = UtcNow()
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running event monitor.", e));
            }
        }

        private void RunTeamOperationMonitor()
        {
            try
            {
                //Team operations
                ATeam team1, team2, winningTeam, losingTeam, mapUpTeam, mapDownTeam;
                if (GetTeamByID(1, out team1) && GetTeamByID(2, out team2))
                {
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
                    // If the mode is rush, the attackers are team 1, use that team for the extra seeder
                    Boolean isRush = false;
                    if (_serverInfo != null &&
                        _serverInfo.InfoObject != null &&
                        !String.IsNullOrEmpty(_serverInfo.InfoObject.GameMode))
                    {
                        isRush = _serverInfo.InfoObject.GameMode.ToLower().Contains("rush");
                    }
                    if (team1.GetTicketDifferenceRate() > team2.GetTicketDifferenceRate() || isRush)
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

                    if (_roundState == RoundState.Playing &&
                        _serverInfo.GetRoundElapsedTime().TotalMinutes > 5 &&
                        Math.Abs(winningTeam.TeamTicketCount - losingTeam.TeamTicketCount) > 100 &&
                        !_Team1MoveQueue.Any() &&
                        !_Team2MoveQueue.Any())
                    {
                        //Auto-assist
                        foreach (var aPlayer in GetOnlinePlayerDictionaryOfGroup("blacklist_autoassist").Values
                                                    .Where(dPlayer => dPlayer.fbpInfo.TeamID == winningTeam.TeamID))
                        {
                            var assistRecord = new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("self_assist"),
                                command_action = GetCommandByKey("self_assist_unconfirmed"),
                                target_name = aPlayer.player_name,
                                target_player = aPlayer,
                                source_name = "AUAManager",
                                record_message = "Auto-assist Weak Team [" + winningTeam.TeamTicketCount + ":" + losingTeam.TeamTicketCount + "][" + FormatTimeString(_serverInfo.GetRoundElapsedTime(), 3) + "]",
                                record_time = UtcNow()
                            };
                            if (RunAssist(assistRecord.target_player, assistRecord, null, true))
                            {
                                QueueRecordForProcessing(assistRecord);
                                _PlayersAutoAssistedThisRound = true;
                                Thread.Sleep(2000);
                            }
                        }
                        //Server seeder balance
                        if (_UseTeamPowerMonitorSeeders &&
                            _PlayerDictionary.Any())
                        {
                            var seeders = _PlayerDictionary.Values.ToList().Where(dPlayer =>
                                                        dPlayer.player_type == PlayerType.Player &&
                                                        NowDuration(dPlayer.lastAction).TotalMinutes > 20);
                            if (seeders.Any())
                            {
                                var mapUpSeeders = seeders.Where(aPlayer => aPlayer.fbpInfo.TeamID == mapUpTeam.TeamID);
                                var mapDownSeeders = seeders.Where(aPlayer => aPlayer.fbpInfo.TeamID == mapDownTeam.TeamID);
                                // This code is fired every 30 seconds
                                // At that interval move players so either both teams have the same number of seeders,
                                // or the map up team has 1 more seeder.
                                if (mapDownSeeders.Count() > mapUpSeeders.Count())
                                {
                                    var aPlayer = mapDownSeeders.First();
                                    aPlayer.RequiredTeam = mapUpTeam;
                                    Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                                    ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                                    _MULTIBalancerUnswitcherDisabled = true;
                                    ExecuteCommand("procon.protected.send", "admin.movePlayer", aPlayer.player_name, aPlayer.RequiredTeam.TeamID.ToString(), "0", "true");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running teamoperation monitor.", e));
            }
        }

        private void RunVOIPMonitor()
        {
            try
            {
                Boolean accessUpdateRequired = false;
                if (_TeamspeakPlayerMonitorEnable)
                {
                    List<APlayer> onlineTeamspeakPlayers = new List<APlayer>();
                    //Check for online teamspeak players
                    foreach (TeamSpeakClientViewer.TeamspeakClient client in _TeamspeakManager.GetPlayersOnTs())
                    {
                        IEnumerable<APlayer> matching = _PlayerDictionary.Values.ToList().Where(dPlayer =>
                            // Match by IP or by name (only if no IP is available), percent matching over 80%
                            ((!String.IsNullOrEmpty(client.AdvIpAddress) && !String.IsNullOrEmpty(dPlayer.player_ip) && client.AdvIpAddress == dPlayer.player_ip) ||
                             ((String.IsNullOrEmpty(client.AdvIpAddress) || String.IsNullOrEmpty(dPlayer.player_ip)) && Util.PercentMatch(client.TsName, dPlayer.player_name) > 80)));
                        if (_TeamspeakManager.DebugClients)
                        {
                            Log.Info("TSClient: " + client.TsName + " | " + client.AdvIpAddress + " | " + ((matching.Any()) ? (matching.Count() + " online players match client.") : ("No matching online players.")));
                        }
                        foreach (var match in matching)
                        {
                            match.TSClientObject = client;
                        }
                        onlineTeamspeakPlayers.AddRange(matching);
                    }
                    List<String> validTsPlayers = new List<String>();
                    foreach (APlayer aPlayer in onlineTeamspeakPlayers)
                    {
                        validTsPlayers.Add(aPlayer.player_name);
                        if (!_TeamspeakPlayers.ContainsKey(aPlayer.player_name))
                        {
                            if (_TeamspeakManager.DebugClients)
                            {
                                Log.Success("Teamspeak soldier " + aPlayer.player_name + " connected.");
                            }

                            var startDuration = NowDuration(_AdKatsStartTime).TotalSeconds;
                            var startupDuration = TimeSpan.FromSeconds(_startupDurations.Average(span => span.TotalSeconds)).TotalSeconds;
                            if (startDuration - startupDuration > 120 && aPlayer.player_type != PlayerType.Spectator && NowDuration(aPlayer.VoipJoinTime).TotalMinutes > 15.0)
                            {
                                var playerName = aPlayer.player_name;
                                var username = aPlayer.TSClientObject.TsName;
                                var playerUsername = playerName + (
                                        aPlayer.player_name.ToLower() != username.ToLower() &&
                                        !aPlayer.player_name.ToLower().Contains(username.ToLower()) &&
                                        !username.ToLower().Contains(aPlayer.player_name.ToLower()) ? " (" + username + ")" : "");
                                var joinMessage = _TeamspeakManager.JoinDisplayMessage.Replace("%player%", playerName).Replace("%username%", username).Replace("%playerusername%", playerUsername);
                                switch (_TeamspeakManager.JoinDisplay)
                                {
                                    case VoipJoinDisplayType.Say:
                                        AdminSayMessage(joinMessage);
                                        break;
                                    case VoipJoinDisplayType.Yell:
                                        AdminYellMessage(joinMessage);
                                        break;
                                    case VoipJoinDisplayType.Tell:
                                        AdminTellMessage(joinMessage);
                                        break;
                                }
                                aPlayer.VoipJoinTime = UtcNow();
                            }
                            accessUpdateRequired = true;
                        }
                        _TeamspeakPlayers[aPlayer.player_name] = aPlayer;
                    }
                    foreach (string removePlayer in _TeamspeakPlayers.Keys.ToList().Where(key => !validTsPlayers.Contains(key)).ToList())
                    {
                        if (_TeamspeakManager.DebugClients)
                        {
                            Log.Success("Teamspeak soldier " + removePlayer + " disconnected.");
                        }
                        accessUpdateRequired = true;
                        _TeamspeakPlayers[removePlayer].TSClientObject = null;
                        _TeamspeakPlayers.Remove(removePlayer);
                    }

                    if (_TeamspeakOnlinePlayersEnable && (UtcNow() - _LastTeamspeakOnlinePlayersCheck).TotalSeconds > _TeamspeakOnlinePlayersInterval * 60)
                    {
                        _LastTeamspeakOnlinePlayersCheck = UtcNow();
                        PostOnlineVoicePlayers(onlineTeamspeakPlayers, _TeamspeakOnlinePlayersMaxPlayersToList,
                            _TeamspeakOnlinePlayersAloneMessage, _TeamspeakOnlinePlayersMessage, _TeamspeakManager.JoinDisplay);
                    }
                }

                if (_pluginEnabled &&
                    _firstPlayerListComplete &&
                    _DiscordPlayerMonitorEnable &&
                    _DiscordPlayerMonitorView)
                {
                    List<APlayer> onlineDiscordPlayers = new List<APlayer>();
                    //Check for online discord players
                    var members = _DiscordManager.GetMembers(false, true, true);
                    foreach (var member in members)
                    {
                        var matching = _PlayerDictionary.Values.ToList().Where(dPlayer =>
                            // Match by ID
                            member.ID == dPlayer.player_discord_id);
                        if (!matching.Any())
                        {
                            // If there are no results by ID, do a name search
                            matching = _PlayerDictionary.Values.ToList().Where(dPlayer =>
                                          // Ignore any online players who already have a discord ID
                                          String.IsNullOrEmpty(dPlayer.player_discord_id) &&
                                          // Make sure there are no players already given this ID
                                          member.PlayerObject == null && member.PlayerTested &&
                                          // Match name, percent matching over 80%
                                          Util.PercentMatch(member.Name, dPlayer.player_name) > 80);
                        }
                        if (_DiscordManager.DebugMembers)
                        {
                            Log.Info("DiscordMember: " + member.Name + " | " + member.ID + " | " + ((matching.Any()) ? (matching.Count() + " online players match member.") : ("No matching online players.")));
                        }
                        foreach (var match in matching)
                        {
                            match.DiscordObject = member;
                            // If their name is an exact match, assign the ID association
                            if (match.player_name == member.Name && String.IsNullOrEmpty(match.player_discord_id))
                            {
                                match.player_discord_id = member.ID;
                                UpdatePlayer(match);
                            }
                        }
                        onlineDiscordPlayers.AddRange(matching);
                    }
                    List<String> validDiscordPlayers = new List<String>();
                    foreach (APlayer aPlayer in onlineDiscordPlayers)
                    {
                        validDiscordPlayers.Add(aPlayer.player_name);
                        if (!_DiscordPlayers.ContainsKey(aPlayer.player_name))
                        {
                            if (_DiscordManager.DebugMembers)
                            {
                                Log.Success("Discord soldier " + aPlayer.player_name + " connected.");
                            }

                            var startDuration = NowDuration(_AdKatsStartTime).TotalSeconds;
                            var startupDuration = TimeSpan.FromSeconds(_startupDurations.Average(span => span.TotalSeconds)).TotalSeconds;
                            if (startDuration - startupDuration > 120 && aPlayer.player_type != PlayerType.Spectator && NowDuration(aPlayer.VoipJoinTime).TotalMinutes > 15.0)
                            {
                                var playerName = aPlayer.player_name;
                                var username = aPlayer.DiscordObject.Name;
                                var playerUsername = playerName + (
                                        aPlayer.player_name.ToLower() != username.ToLower() &&
                                        !aPlayer.player_name.ToLower().Contains(username.ToLower()) &&
                                        !username.ToLower().Contains(aPlayer.player_name.ToLower()) ? " (" + username + ")" : "");
                                var joinMessage = _DiscordManager.JoinMessage.Replace("%player%", playerName).Replace("%username%", username).Replace("%playerusername%", playerUsername);
                                switch (_DiscordManager.JoinDisplay)
                                {
                                    case VoipJoinDisplayType.Say:
                                        AdminSayMessage(joinMessage);
                                        break;
                                    case VoipJoinDisplayType.Yell:
                                        AdminYellMessage(joinMessage);
                                        break;
                                    case VoipJoinDisplayType.Tell:
                                        AdminTellMessage(joinMessage);
                                        break;
                                }
                                aPlayer.VoipJoinTime = UtcNow();
                            }
                            accessUpdateRequired = true;
                        }
                        _DiscordPlayers[aPlayer.player_name] = aPlayer;
                    }
                    foreach (string removePlayer in _DiscordPlayers.Keys.ToList().Where(key => !validDiscordPlayers.Contains(key)).ToList())
                    {
                        if (_DiscordManager.DebugMembers)
                        {
                            Log.Success("Discord soldier " + removePlayer + " disconnected.");
                        }
                        accessUpdateRequired = true;
                        _DiscordPlayers[removePlayer].DiscordObject = null;
                        _DiscordPlayers.Remove(removePlayer);
                    }

                    if (_DiscordOnlinePlayersEnable && (UtcNow() - _LastDiscordOnlinePlayersCheck).TotalSeconds > _DiscordOnlinePlayersInterval * 60)
                    {
                        _LastDiscordOnlinePlayersCheck = UtcNow();
                        PostOnlineVoicePlayers(onlineDiscordPlayers, _DiscordOnlinePlayersMaxPlayersToList,
                            _DiscordOnlinePlayersAloneMessage, _DiscordOnlinePlayersMessage, _DiscordManager.JoinDisplay);
                    }
                }
                if (accessUpdateRequired)
                {
                    FetchAllAccess(true);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running voip monitor.", e));
            }
        }

        /**
         * Goes through playerList and sends a public admin say with online players.
         * maxPlayersToList defines how many players should be names in %players%. %count% contains the total count.
         * aloneMessage is sent if only one players is online. Otherwise, message is sent.
         */
        private void PostOnlineVoicePlayers(List<APlayer> playerList, int maxPlayersToList, string aloneMessage, string message, VoipJoinDisplayType sendType)
        {
            string players = string.Join(", ", playerList.Take(maxPlayersToList).Select(player => player.player_name).ToArray());
            if (playerList.Count > maxPlayersToList)
            {
                players += " and " + (playerList.Count - maxPlayersToList) + " more";
            }
            string msg = (playerList.Count > 1 ? message : aloneMessage).Replace("%players%", players).Replace("%count%", playerList.Count.ToString());
            if (playerList.Count > 0)
            {
                switch (sendType)
                {
                    case VoipJoinDisplayType.Say:
                        AdminSayMessage(msg);
                        break;
                    case VoipJoinDisplayType.Yell:
                        AdminYellMessage(msg);
                        break;
                    case VoipJoinDisplayType.Tell:
                        AdminTellMessage(msg);
                        break;
                }
            }
        }

        private void RunAFKMonitor()
        {
            try
            {
                //Perform AFK processing
                if (_AFKManagerEnable && _AFKAutoKickEnable && GetPlayerCount() > _AFKTriggerMinimumPlayers)
                {
                    //Double list conversion
                    List<APlayer> afkPlayers = _PlayerDictionary.Values.ToList().Where(aPlayer => (UtcNow() - aPlayer.lastAction).TotalMinutes > _AFKTriggerDurationMinutes && aPlayer.player_type != PlayerType.Spectator && !PlayerIsAdmin(aPlayer)).Take(_PlayerDictionary.Values.Count(aPlayer => aPlayer.player_type == PlayerType.Player) - _AFKTriggerMinimumPlayers).ToList();
                    if (_AFKIgnoreUserList)
                    {
                        IEnumerable<string> userSoldierGuids = FetchAllUserSoldiers().Select(aPlayer => aPlayer.player_guid);
                        afkPlayers = afkPlayers.Where(aPlayer => !userSoldierGuids.Contains(aPlayer.player_guid)).ToList();
                    }
                    else
                    {
                        afkPlayers = afkPlayers.Where(aPlayer => !_AFKIgnoreRoles.Contains(aPlayer.player_role.role_key) &&
                                                                 !_AFKIgnoreRoles.Contains(aPlayer.player_role.role_name) &&
                                                                 !_AFKIgnoreRoles.Contains(aPlayer.player_role.role_id.ToString()) &&
                                                                 !_AFKIgnoreRoles.Contains("RLE" + aPlayer.player_role.role_id.ToString())).ToList();
                    }
                    foreach (APlayer aPlayer in afkPlayers)
                    {
                        String afkTime = FormatTimeString(UtcNow() - aPlayer.lastAction, 2);
                        Log.Debug(() => "Kicking " + aPlayer.player_name + " for being AFK " + afkTime + ".", 3);
                        ARecord record = new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_kick"),
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "AFKManager",
                            record_message = "AFK time exceeded [" + afkTime + "/" + GetPlayerTeamKey(aPlayer) + "]. Please rejoin once you return.",
                            record_time = UtcNow()
                        };
                        QueueRecordForProcessing(record);
                        //Only take one
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running AFK monitor.", e));
            }
        }

        private void RunNukeAnnounceMonitor()
        {
            try
            {
                //Nuke Countdowns - Every 50ms
                if (_lastNukeTeam != null)
                {
                    //Auto-Nuke Slay Duration
                    var duration = NowDuration(_lastNukeTime);
                    var nukeInfoMessage = "";
                    var durationIncrease = 0;
                    ATeam team1, team2;
                    if (!_nukeAutoSlayActive && GetTeamByID(1, out team1) && GetTeamByID(2, out team2))
                    {
                        if (Math.Abs(team1.TeamTicketCount - team2.TeamTicketCount) > _surrenderAutoNukeDurationIncreaseTicketDiff)
                        {
                            durationIncrease = _surrenderAutoNukeDurationIncrease * Math.Max(getNukeCount(_lastNukeTeam.TeamID) - 1, 0);
                        }
                        switch (_populationStatus)
                        {
                            case PopulationState.High:
                                if (_surrenderAutoNukeDurationHigh + durationIncrease > 60)
                                {
                                    durationIncrease = Math.Max(0, 60 - _surrenderAutoNukeDurationHigh);
                                }
                                _nukeAutoSlayActiveDuration = _surrenderAutoNukeDurationHigh + durationIncrease;
                                nukeInfoMessage = "High population nuke: " + _surrenderAutoNukeDurationHigh + (durationIncrease > 0 ? " + " + durationIncrease : "") + " seconds.";
                                break;
                            case PopulationState.Medium:
                                if (_surrenderAutoNukeDurationMed + durationIncrease > 45)
                                {
                                    durationIncrease = Math.Max(0, 45 - _surrenderAutoNukeDurationMed);
                                }
                                _nukeAutoSlayActiveDuration = _surrenderAutoNukeDurationMed + durationIncrease;
                                nukeInfoMessage = "Medium population nuke: " + _surrenderAutoNukeDurationMed + (durationIncrease > 0 ? " + " + durationIncrease : "") + " seconds.";
                                break;
                            case PopulationState.Low:
                                if (_surrenderAutoNukeDurationLow + durationIncrease > 30)
                                {
                                    durationIncrease = Math.Max(0, 30 - _surrenderAutoNukeDurationLow);
                                }
                                _nukeAutoSlayActiveDuration = _surrenderAutoNukeDurationLow + durationIncrease;
                                nukeInfoMessage = "Low population nuke: " + _surrenderAutoNukeDurationLow + (durationIncrease > 0 ? " + " + durationIncrease : "") + " seconds.";
                                break;
                        }
                    }
                    if (_nukeAutoSlayActiveDuration > 0)
                    {
                        if (duration.TotalSeconds < _nukeAutoSlayActiveDuration && _roundState == RoundState.Playing)
                        {
                            if (!_nukeAutoSlayActive)
                            {
                                AdminSayMessage(nukeInfoMessage);
                            }
                            _nukeAutoSlayActive = true;
                            Double endDuration = NowDuration(_lastNukeTime.AddSeconds(_nukeAutoSlayActiveDuration)).TotalSeconds;
                            Int32 endDurationSeconds = (Int32)Math.Round(endDuration);
                            String endDurationString = endDurationSeconds.ToString();
                            var durationMessage = _lastNukeTeam.TeamKey + " nuke active for " + endDurationString + " seconds!";
                            if (_lastNukeSlayDurationMessage != durationMessage && endDurationSeconds > 0 && (endDurationSeconds % 2 == 0 || endDuration <= 5))
                            {
                                AdminTellMessage(durationMessage);
                                _lastNukeSlayDurationMessage = durationMessage;
                            }
                        }
                        else if (_nukeAutoSlayActive)
                        {
                            _nukeAutoSlayActive = false;
                            _nukeAutoSlayActiveDuration = 0;
                            AdminTellMessage(_lastNukeTeam.TeamKey + " nuke has ended!");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running nukeannounce monitor.", e));
            }
        }

        private void RunTeamPowerScramblerMonitor()
        {
            try
            {
                if (_UseTeamPowerMonitorScrambler &&
                    _serverInfo != null &&
                    !_ScrambleRequiredTeamsRemoved &&
                    _roundState == RoundState.Playing &&
                    _serverInfo.GetRoundElapsedTime().TotalMinutes > 2)
                {
                    // Clear all required teams/squads 2 minutes into the round so the regular balancer can take over
                    foreach (APlayer aPlayer in GetFetchedPlayers().Where(aPlayer => aPlayer.RequiredTeam != null))
                    {
                        aPlayer.RequiredTeam = null;
                        aPlayer.RequiredSquad = -1;
                    }
                    _ScrambleRequiredTeamsRemoved = true;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running team power scrambler monitor.", e));
            }
        }

        private void RunChallengeMonitor()
        {
            try
            {
                if (ChallengeManager != null)
                {
                    // Fail challenges as necessary
                    var roundEntries = ChallengeManager.GetEntries().Where(entry => !entry.Completed &&
                                                                                    !entry.Failed &&
                                                                                    !entry.Canceled);
                    foreach (var entry in roundEntries)
                    {
                        entry.CheckFailure();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running challenge monitor.", e));
            }
        }

        private void RunReportMonitor()
        {
            try
            {
                if (_PlayerReports.Any())
                {
                    foreach (var report in FetchActivePlayerReports())
                    {
                        FetchRecordUpdate(report);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running report monitor.", e));
            }
        }

        private void SetupStatusMonitor()
        {
            //Create a new thread to handle keep-alive
            //This thread will remain running for the duration the layer is online
            Thread statusMonitorThread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    Thread.CurrentThread.Name = "StatusMonitor";
                    DoServerInfoTrigger();
                    while (true)
                    {
                        try
                        {
                            RunMemoryMonitor();

                            RunPlayerListingStatMonitor();

                            RunUnswitcherMonitor();

                            RunDocumentationMonitor();

                            RunSpambotMonitor();

                            RunAutoAssistMonitor();

                            RunTeamPowerScramblerMonitor();

                            //Prune Watchdog Threads
                            Threading.Prune();

                            //Batch very long keep alive - every 10 minutes
                            if (NowDuration(_LastVeryLongKeepAliveCheck).TotalMinutes > 10.0)
                            {

                                RunPurgeMonitor();

                                FixInvalidCommandIds();

                                RunAutomaticRestartMonitor();

                                _LastVeryLongKeepAliveCheck = UtcNow();
                            }

                            //Batch long keep alive - every 5 minutes
                            if ((UtcNow() - _LastLongKeepAliveCheck).TotalMinutes > 5)
                            {

                                RunPingStatisticsMonitor();

                                _LastLongKeepAliveCheck = UtcNow();
                            }

                            //Batch short keep alive - every 30 seconds
                            if ((UtcNow() - _LastShortKeepAliveCheck).TotalSeconds > 30)
                            {

                                RunPlayerListingMonitor();

                                RunTeamPowerStatMonitor();

                                RunEventMonitor();

                                RunTeamOperationMonitor();

                                RunVOIPMonitor();

                                RunAFKMonitor();

                                RunChallengeMonitor();

                                RunReportMonitor();

                                if (_pluginEnabled && _threadsReady && _firstPlayerListComplete && _enforceSingleInstance)
                                {
                                    AdminSayMessage("/AdKatsInstanceCheck " + _instanceKey + " " + Math.Round((UtcNow() - _AdKatsRunningTime).TotalSeconds), false);
                                }

                                //Enable if auto-enable wanted
                                if (_useKeepAlive && !_pluginEnabled)
                                {
                                    Enable();
                                }

                                //Check for thread warning
                                Threading.Monitor();

                                _LastShortKeepAliveCheck = UtcNow();
                            }

                            //Server info fetch - every 5 seconds
                            if (_threadsReady &&
                                NowDuration(_LastServerInfoReceive).TotalSeconds > 4.5 &&
                                NowDuration(_LastServerInfoTrigger).TotalSeconds > 4.5)
                            {
                                DoServerInfoTrigger();
                            }

                            //Player Info fetch - every 5 seconds
                            if (_threadsReady &&
                                NowDuration(_LastPlayerListAccept).TotalSeconds > 4.5 &&
                                NowDuration(_LastPlayerListTrigger).TotalSeconds > 4.5)
                            {
                                DoPlayerListTrigger();
                            }

                            if (_pluginEnabled)
                            {
                                //Sleep 500ms between loops
                                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                            }
                            else
                            {
                                //Sleep 1000ms between loops
                                Thread.Sleep(TimeSpan.FromMilliseconds(2000));
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error in status monitor. Skipping current loop.", e));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error while running status monitor.", e));
                }
            }));
            //Start the thread
            statusMonitorThread.Start();
        }

        private void SetupFastStatusMonitor()
        {
            //This thread will remain running for the duration the layer is online
            Thread fastStatusMonitorThread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    Thread.CurrentThread.Name = "FastStatusMonitor";
                    while (true)
                    {
                        try
                        {
                            RunNukeAnnounceMonitor();

                            if (_pluginEnabled)
                            {
                                //Sleep 50ms between loops
                                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                            }
                            else
                            {
                                //Sleep 1000ms between loops
                                Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error in fast status monitor. Skipping current loop.", e));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error while running fast status monitor.", e));
                }
            }));
            //Start the thread
            fastStatusMonitorThread.Start();
        }

        public void InitWaitHandles()
        {
            //Initializes all wait handles
            Threading.Init();
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
            _AntiCheatWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _ServerInfoWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _StatLoggerStatusWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _BattlelogCommWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void OpenAllHandles()
        {
            Threading.Set();
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
            _AntiCheatWaitHandle.Set();
            _ServerInfoWaitHandle.Set();
            _StatLoggerStatusWaitHandle.Set();
            _BattlelogCommWaitHandle.Set();
            _EmailHandler._EmailProcessingWaitHandle.Set();
        }

        public void InitThreads()
        {
            try
            {
                //Creats all threads with their starting methods and set to run in the background
                _PlayerListingThread = new Thread(PlayerListingThreadLoop)
                {
                    IsBackground = true
                };

                _AccessFetchingThread = new Thread(AccessFetchingThreadLoop)
                {
                    IsBackground = true
                };

                _KillProcessingThread = new Thread(KillProcessingThreadLoop)
                {
                    IsBackground = true
                };

                _MessageProcessingThread = new Thread(MessagingThreadLoop)
                {
                    IsBackground = true
                };

                _CommandParsingThread = new Thread(CommandParsingThreadLoop)
                {
                    IsBackground = true
                };

                _DatabaseCommunicationThread = new Thread(DatabaseCommunicationThreadLoop)
                {
                    IsBackground = true
                };

                _ActionHandlingThread = new Thread(ActionHandlingThreadLoop)
                {
                    IsBackground = true
                };

                _TeamSwapThread = new Thread(TeamswapThreadLoop)
                {
                    IsBackground = true
                };

                _BanEnforcerThread = new Thread(BanEnforcerThreadLoop)
                {
                    IsBackground = true
                };

                _AntiCheatThread = new Thread(AntiCheatThreadLoop)
                {
                    IsBackground = true
                };

                _BattlelogCommThread = new Thread(BattlelogCommThreadLoop)
                {
                    IsBackground = true
                };

                _IPAPICommThread = new Thread(IPAPICommThreadLoop)
                {
                    IsBackground = true
                };
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured while initializing threads.", e));
            }
        }

        public void StartThreads()
        {
            Log.Debug(() => "Entering StartThreads", 7);
            try
            {
                //Start the main thread
                OnlineAdminSayMessage("AdKats starting.");
                //Reset the master wait handle
                Threading.Reset();
                //DB Comm is the heart of AdKats, everything revolves around that thread
                Threading.StartWatchdog(_DatabaseCommunicationThread);
                //Battlelog comm and IP API threads are independant
                Threading.StartWatchdog(_BattlelogCommThread);
                Threading.StartWatchdog(_IPAPICommThread);
                //Other threads are started within the db comm thread
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while starting processing threads.", e));
            }
            Log.Debug(() => "Exiting StartThreads", 7);
        }

        private void Disable()
        {
            //Call Disable
            ExecuteCommand("procon.protected.plugins.enable", "AdKats", "False");
            //Set enabled false so threads begin exiting
            _pluginEnabled = false;
            _threadsReady = false;
        }

        private void Enable()
        {
            if (Thread.CurrentThread.Name == "Finalizer")
            {
                Thread pluginRebootThread = new Thread(new ThreadStart(delegate
                {
                    Log.Debug(() => "Starting a reboot thread.", 5);
                    try
                    {
                        Thread.CurrentThread.Name = "Reboot";
                        Thread.Sleep(1000);
                        //Call Enable
                        ExecuteCommand("procon.protected.plugins.enable", "AdKats", "True");
                    }
                    catch (Exception)
                    {
                        Log.HandleException(new AException("Error while running reboot."));
                    }
                    Log.Debug(() => "Exiting a reboot thread.", 5);
                    Threading.StopWatchdog();
                }));
                Threading.StartWatchdog(pluginRebootThread);
            }
            else
            {
                //Call Enable
                ExecuteCommand("procon.protected.plugins.enable", "AdKats", "True");
            }
        }

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
                case "BFBC2":
                    GameVersion = GameVersionEnum.BFBC2;
                    break;
            }
            Log.Success("^1Game Version: " + GameVersion);

            //Initialize the Email Handler
            _EmailHandler = new EmailHandler(this);

            //Initialize PushBullet Handler
            _PushBulletHandler = new PushBulletHandler(this);
        }
    }
}
