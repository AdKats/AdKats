/* 
 * AdKats is an Advanced In-Game Admin and Ban Enforcer for Procon Frostbite.
 * 
 * Copyright 2013 A Different Kind, LLC
 * 
 * AdKats was inspired by the gaming community A Different Kind (ADK), with help from the BF3 Admins within the 
 * community. Visit http://www.adkgamers.com/ for more information.
 *
 * The AdKats Frostbite Plugin is free software: you can redistribute it and/or modify it under the terms of the
 * GNU General Public License as published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version. AdKats is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See the GNU General Public License for more details. To view this license, visit http://www.gnu.org/licenses/.
 * 
 * Code Credit:
 * Modded Levenshtein Distance algorithm from Micovery's InsaneLimits
 * Threading Examples from Micovery's InsaneLimits 
 * Email System from "Notify Me!" By MorpheusX(AUT)
 * Twitter Post System from Micovery's InsaneLimits
 * 
 * AdKats.cs
 */

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Collections;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.Reflection;

using MySql.Data.MySqlClient;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.HttpServer;


namespace PRoConEvents
{
    //Aliases
    using EventType = PRoCon.Core.Events.EventType;
    using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

    public class AdKats : PRoConPluginAPI, IPRoConPluginInterface
    {
        #region Variables
        //Current version of the plugin
        string plugin_version = "3.4.9.5";
        //When slowmo is enabled, there will be a 1 second pause between each print to console
        //This will slow the program as a whole whenever the console is printed to
        Boolean slowmo = false;

        //Match command showing whether AdKats is installed and running
        private MatchCommand AdKatsAvailableIndicator;

        //Enumerations
        //Messaging
        public enum ConsoleMessageType
        {
            Warning,
            Error,
            Exception,
            Normal,
            Success
        };
        //Admin Commands
        public enum AdKat_CommandType
        {
            //Case for use while parsing and handling errors
            Default,
            //Confirm or cancel a command
            ConfirmCommand,
            CancelCommand,
            //Moving Commands
            MovePlayer,
            ForceMovePlayer,
            Teamswap,
            KillSelf,
            RoundWhitelistPlayer,
            //Action Commands
            KillPlayer,
            LowPopKillPlayer,
            RepeatKillPlayer,
            KickPlayer,
            TempBanPlayer,
            PermabanPlayer,
            PunishPlayer,
            ForgivePlayer,
            MutePlayer,
            JoinPlayer,
            //Reporting Commands
            ReportPlayer,
            CallAdmin,
            ConfirmReport,
            //Round Commands
            RestartLevel,
            NextLevel,
            EndLevel,
            //Messaging Commands
            AdminSay,
            PlayerSay,
            AdminYell,
            PlayerYell,
            WhatIs,
            Voip,
            //Super-User Commands
            NukeServer,
            KickAll,
            //Ban Enforcer
            EnforceBan,
            //Exceptions
            Exception
        };
        //Source of commands
        public enum AdKat_CommandSource
        {
            Default,
            InGame,
            Console,
            Settings,
            Database,
            HTTP
        }

        //Constants
        //IDs of the two teams as the server understands it
        private static int USTeamID = 1;
        private static int RUTeamID = 2;

        //General Plugin Settings/Status
        private volatile bool isEnabled;
        private volatile bool threadsReady;

        //Player Lists
        private Dictionary<string, AdKat_Player> playerDictionary = new Dictionary<string, AdKat_Player>();
        private DateTime lastSuccessfulPlayerList = DateTime.Now - TimeSpan.FromSeconds(5);
        //player counts per team
        private int USPlayerCount = 0;
        private int RUPlayerCount = 0;

        //Player Access Settings
        private Dictionary<string, AdKat_Access> playerAccessCache = new Dictionary<string, AdKat_Access>();
        private DateTime lastDBAccessFetch = DateTime.Now;
        //frequency in seconds to fetch access changes at
        private int dbAccessFetchFrequency = 300;

        //Database Settings
        private string mySqlHostname = "";
        private string mySqlPort = "";
        private string mySqlDatabaseName = "";
        private string mySqlUsername = "";
        private string mySqlPassword = "";
        private MySqlConnectionStringBuilder dbCommStringBuilder = new MySqlConnectionStringBuilder();
        private Boolean useConnectionPooling = true;
        private int minConnectionPoolSize = 0;
        private int maxConnectionPoolSize = 20;
        private Boolean useCompressedConnection = false;
        private volatile Boolean dbSettingsChanged = true;
        //Database Time Conversion (default to no difference)
        private TimeSpan dbTimeConversion = new TimeSpan(0);
        //Action fetching from database
        private Boolean fetchActionsFromDB = false;
        private DateTime lastDBActionFetch = DateTime.Now;
        //frequency in seconds to fetch actions at
        private int dbActionFrequency = 10;
        private Boolean dbConnectionActive = false;

        //Server Settings
        private Int64 server_id = -1;
        private string server_ip = null;
        private CServerInfo serverInfo = null;

        //Setting Import
        private Int64 settingImportID = -1;
        private DateTime lastDBSettingFetch = DateTime.Now;
        private int dbSettingFetchFrequency = 300;

        //ADK Settings
        //This will automatically change to true on ADK servers
        private Boolean isADK = false;

        //Experimental Tools Settings
        private Boolean useExperimentalTools = false;
        //NO EX Limiter
        private Boolean useNoExplosivesLimit = false;
        //Grenade Cook Catcher
        private Boolean useGrenadeCookCatcher = false;
        //Hacker Checker
        private Boolean useHackerChecker = false;
        private Boolean useDPSChecker = false;
        private double DPSTriggerLevel = 100.0;
        private Boolean useHSKChecker = false;
        private double HSKTriggerLevel = 100.0;
        private Dictionary<string, AdKat_Player> roundCookers = null;

        //Infraction Management Settings
        //Whether to combine server punishments
        private Boolean combineServerPunishments = false;
        //IRO punishment setting
        private Boolean IROOverridesLowPop = false;
        //Default hierarchy of punishments
        private string[] punishmentHierarchy = 
        {
            "kill",
            "kill",
            "kick",
            "tban60",
            "tbanday",
            "tbanweek",
            "tban2weeks",
            "tbanmonth",
            "ban"
        };
        private List<string> punishmentSeverityIndex = null;
        //When punishing, only kill players when server is in low population
        private Boolean onlyKillOnLowPop = true;
        //Default for low populations
        private int lowPopPlayerCount = 20;

        //Ban Management Settings
        //General
        private Boolean useBanAppend = false;
        private string banAppend = "Appeal at your_site.com";
        //Ban Enforcer
        private Boolean useBanEnforcer = false;
        private Boolean useBanEnforcerPreviousState = false;
        private Boolean defaultEnforceName = false;
        private Boolean defaultEnforceGUID = true;
        private Boolean defaultEnforceIP = false;
        private Boolean bansFirstListed = false;
        private DateTime lastBanListCall = DateTime.Now;
        private DateTime lastSuccessfulBanList = DateTime.Now - TimeSpan.FromSeconds(5);
        private DateTime lastDBBanFetch = DateTime.Now - TimeSpan.FromSeconds(5);
        private int dbBanFetchFrequency = 60;
        private DateTime permaBanEndTime = DateTime.Now.AddYears(20);

        //AdKats WebAdmin Settings
        //This is currently a constant
        private Boolean usingAWA = false;

        //Debug Settings
        private volatile int debugLevel;
        private Boolean toldCol = false;
        //Debug Soldier (Used for displaying command action duration in-game)
        private String debugSoldierName = "ColColonCleaner";
        private DateTime commandStartTime = DateTime.Now;

        //Orchestration Settings
        private Boolean feedMULTIBalancerWhitelist = false;
        private Boolean feedServerReservedSlots = false;
        private Boolean feedStatLoggerSettings = false;
        private List<string> currentReservedSlotPlayers = null;
        private Hashtable lastStatLoggerStatusUpdate = null;
        private DateTime lastStatLoggerStatusUpdateTime = DateTime.MinValue;

        //Messaging Settings
        //Pre-Message Settings
        private List<string> preMessageList = new List<string>();
        private Boolean requirePreMessageUse = false;
        //Yell Settings
        private int yellDuration = 5;

        //In-Game Command Settings
        //Player Interaction
        private string killPlayerCommandText = "kill";
        private string kickPlayerCommandText = "kick";
        private string tempBanPlayerCommandText = "tban";
        private string permaBanPlayerCommandText = "ban";
        private string punishPlayerCommandText = "punish";
        private string forgivePlayerCommandText = "forgive";
        private string mutePlayerCommandText = "mute";
        private string joinPlayerCommandText = "join";
        private string roundWhitelistPlayerCommandText = "roundwhitelist";
        private string movePlayerCommandText = "move";
        private string forceMovePlayerCommandText = "fmove";
        private string teamSwapPlayerCommandText = "moveme";
        private string killSelfCommandText = "killme";
        private string reportPlayerCommandText = "report";
        private string callAdminCommandText = "admin";
        //Admin messaging
        private string adminSayCommandText = "say";
        private string playerSayCommandText = "psay";
        private string adminYellCommandText = "yell";
        private string playerYellCommandText = "pyell";
        private string whatIsCommandText = "whatis";
        private string voipCommandText = "voip";
        //Map control
        private string restartLevelCommandText = "restart";
        private string nextLevelCommandText = "nextlevel";
        private string endLevelCommandText = "endround";
        //Power corner
        private string nukeServerCommandText = "nuke";
        private string kickAllCommandText = "kickall";
        //Confirm and cancel
        private string confirmCommandText = "yes";
        private string cancelCommandText = "no";
        //Used to parse incoming commands quickly
        public Dictionary<string, AdKat_CommandType> AdKat_CommandStrings;
        public Dictionary<AdKat_CommandType, int> AdKat_CommandAccessRank;
        //Database record types
        public Dictionary<AdKat_CommandType, string> AdKat_RecordTypes;
        public Dictionary<string, AdKat_CommandType> AdKat_RecordTypesInv;
        //Logging settings
        public Dictionary<AdKat_CommandType, Boolean> AdKat_LoggingSettings;
        //Default required reason length for admins
        private int requiredReasonLength = 5;

        //External Access Settings
        private string externalCommandAccessKey = "NoPasswordSet";

        //TeamSwap Settings
        //The list of players on RU wishing to move to US (This list takes first priority)
        private Queue<CPlayerInfo> USMoveQueue = new Queue<CPlayerInfo>();
        //the list of players on US wishing to move to RU (This list takes secondary)
        private Queue<CPlayerInfo> RUMoveQueue = new Queue<CPlayerInfo>();
        //whether to allow all players, or just players in the whitelist
        private Boolean requireTeamswapWhitelist = true;
        //the lowest ticket count of either team
        private volatile int lowestTicketCount = 500000;
        //the highest ticket count of either team
        private volatile int highestTicketCount = 0;
        //the highest ticket count of either team to allow self move
        private int teamSwapTicketWindowHigh = 500000;
        //the lowest ticket count of either team to allow self move
        private int teamSwapTicketWindowLow = 0;
        //Round only whitelist
        private Dictionary<string, bool> teamswapRoundWhitelist = new Dictionary<string, bool>();
        //Number of random players to whitelist at the beginning of the round
        private int playersToAutoWhitelist = 2;
        //Force move action queue
        private Queue<CPlayerInfo> teamswapForceMoveQueue = new Queue<CPlayerInfo>();
        //Delayed move list
        private Dictionary<String, CPlayerInfo> teamswapOnDeathMoveDic = new Dictionary<String, CPlayerInfo>();
        //Delayed move checking queue
        private Queue<CPlayerInfo> teamswapOnDeathCheckingQueue = new Queue<CPlayerInfo>();

        //Report/Admin Call Settings
        //Reports for the current round
        private Dictionary<string, AdKat_Record> round_reports = new Dictionary<string, AdKat_Record>();

        //Player Mute Settings
        private string mutedPlayerMuteMessage = "You have been muted by an admin, talking will cause punishment. You can speak again next round.";
        private string mutedPlayerKillMessage = "Do not talk while muted. You can speak again next round.";
        private string mutedPlayerKickMessage = "Talking excessively while muted.";
        private int mutedPlayerChances = 5;
        private Dictionary<string, int> round_mutedPlayers = new Dictionary<string, int>();

        //Admin Assistant Settings
        private Boolean enableAdminAssistants = true;
        private Dictionary<string, bool> adminAssistantCache = new Dictionary<string, bool>();
        private int minimumRequiredWeeklyReports = 5;

        //Twitter Settings
        private Boolean useTwitter = false;
        private TwitterHandler twitterHandler = null;

        //Email Settings
        private Boolean useEmail = false;
        private EmailHandler emailHandler = null;

        //Multi-Threading Settings
        //Threads
        private Thread MessagingThread;
        private Thread CommandParsingThread;
        private Thread DatabaseCommThread;
        private Thread ActionHandlingThread;
        private Thread TeamSwapThread;
        private Thread BanEnforcerThread;
        private Thread HackerCheckerThread;
        private Thread activator;
        private Thread finalizer;
        //Mutexes
        public Object playersMutex = new Object();
        public Object banListMutex = new Object();
        public Object reportsMutex = new Object();
        public Object actionConfirmMutex = new Object();
        public Object playerAccessMutex = new Object();
        public Object teamswapMutex = new Object();
        public Object serverInfoMutex = new Object();
        public Object unparsedMessageMutex = new Object();
        public Object unparsedCommandMutex = new Object();
        public Object unprocessedRecordMutex = new Object();
        public Object unprocessedActionMutex = new Object();
        public Object banEnforcerMutex = new Object();
        public Object hackerCheckerMutex = new Object();
        //Handles
        private EventWaitHandle teamswapHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle listPlayersHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle messageParsingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle commandParsingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle dbCommHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle actionHandlingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle banEnforcerHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle serverInfoHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle statLoggerStatusHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle hackerCheckerHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        //Threading Queues
        private Queue<KeyValuePair<String, String>> unparsedMessageQueue = new Queue<KeyValuePair<String, String>>();
        private Queue<KeyValuePair<String, String>> unparsedCommandQueue = new Queue<KeyValuePair<String, String>>();
        private Queue<AdKat_Record> unprocessedRecordQueue = new Queue<AdKat_Record>();
        private Queue<AdKat_Record> unprocessedActionQueue = new Queue<AdKat_Record>();
        private Queue<AdKat_Access> playerAccessUpdateQueue = new Queue<AdKat_Access>();
        private Queue<String> playerAccessRemovalQueue = new Queue<String>();
        private Queue<AdKat_Player> banEnforcerCheckingQueue = new Queue<AdKat_Player>();
        private Queue<AdKat_Player> hackerCheckerQueue = new Queue<AdKat_Player>();
        private Queue<AdKat_Ban> banEnforcerProcessingQueue = new Queue<AdKat_Ban>();
        private Queue<CBanInfo> cBanProcessingQueue = new Queue<CBanInfo>();
        private Queue<CPluginVariable> settingUploadQueue = new Queue<CPluginVariable>();

        //MISC Settings
        //When an action requires confirmation, this dictionary holds those actions until player confirms action
        private Dictionary<string, AdKat_Record> actionConfirmDic = new Dictionary<string, AdKat_Record>();
        //Action will be taken when the player next spawns
        private Dictionary<string, AdKat_Record> actOnSpawnDictionary = new Dictionary<string, AdKat_Record>();
        //VOIP information
        private string serverVoipAddress = "(TS3) TS.ADKGamers.com:3796";

        //Hacker Checker stat library
        private StatLibrary statLibrary = null;

        #endregion

        public AdKats()
        {
            //Set defaults for webclient
            System.Net.ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            this.isEnabled = false;
            this.threadsReady = false;
            //Assign the match command
            this.AdKatsAvailableIndicator = new MatchCommand("AdKats", "NoCallableMethod", new List<string>(), "AdKats_NoCallableMethod", new List<MatchArgumentFormat>(), new ExecutionRequirements(ExecutionScope.None), "Useable by other plugins to determine whether this one is installed and enabled.");
            //Debug level is 0 by default
            debugLevel = 0;
            //Randomize the external access key
            this.externalCommandAccessKey = AdKats.GetRandom32BitHashCode();

            //Init the punishment severity index
            this.punishmentSeverityIndex = new List<string>();
            this.punishmentSeverityIndex.Add("kill");
            this.punishmentSeverityIndex.Add("kick");
            this.punishmentSeverityIndex.Add("tban60");
            this.punishmentSeverityIndex.Add("tbanday");
            this.punishmentSeverityIndex.Add("tbanweek");
            this.punishmentSeverityIndex.Add("tban2weeks");
            this.punishmentSeverityIndex.Add("tbanmonth");
            this.punishmentSeverityIndex.Add("ban");

            //Init the pre-message list
            preMessageList.Add("US TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.");
            preMessageList.Add("RU TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.");
            preMessageList.Add("US TEAM: DO NOT ENTER THE STREETS BEYOND 'A', YOU WILL BE PUNISHED.");
            preMessageList.Add("RU TEAM: DO NOT GO BEYOND THE BLACK LINE ON CEILING BY 'C' FLAG, YOU WILL BE PUNISHED.");
            preMessageList.Add("THIS SERVER IS NO EXPLOSIVES, YOU WILL BE PUNISHED FOR INFRACTIONS.");
            preMessageList.Add("JOIN OUR TEAMSPEAK AT TS.ADKGAMERS.COM:3796");

            //Create command and logging dictionaries
            this.AdKat_CommandStrings = new Dictionary<string, AdKat_CommandType>();
            this.AdKat_LoggingSettings = new Dictionary<AdKat_CommandType, Boolean>();

            //Fill command and logging dictionaries by calling rebind
            this.rebindAllCommands();

            //Create database dictionaries
            this.AdKat_RecordTypes = new Dictionary<AdKat_CommandType, string>();
            this.AdKat_RecordTypesInv = new Dictionary<string, AdKat_CommandType>();
            this.AdKat_CommandAccessRank = new Dictionary<AdKat_CommandType, int>();

            //Fill DB record types for outgoing database commands
            this.AdKat_RecordTypes.Add(AdKat_CommandType.MovePlayer, "Move");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.ForceMovePlayer, "ForceMove");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.Teamswap, "Teamswap");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.KillSelf, "KillSelf");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.KillPlayer, "Kill");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.RepeatKillPlayer, "RepeatKill");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.LowPopKillPlayer, "LowPopKill");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.KickPlayer, "Kick");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.TempBanPlayer, "TempBan");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.PermabanPlayer, "PermaBan");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.PunishPlayer, "Punish");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.ForgivePlayer, "Forgive");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.MutePlayer, "Mute");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.JoinPlayer, "Join");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.RoundWhitelistPlayer, "RoundWhitelist");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.ReportPlayer, "Report");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.CallAdmin, "CallAdmin");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.ConfirmReport, "ConfirmReport");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.AdminSay, "AdminSay");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.PlayerSay, "PlayerSay");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.AdminYell, "AdminYell");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.PlayerYell, "PlayerYell");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.RestartLevel, "RestartLevel");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.NextLevel, "NextLevel");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.EndLevel, "EndLevel");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.NukeServer, "Nuke");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.KickAll, "KickAll");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.EnforceBan, "EnforceBan");
            this.AdKat_RecordTypes.Add(AdKat_CommandType.Exception, "Exception");

            //Fill DB Inverse record types for incoming database commands
            this.AdKat_RecordTypesInv.Add("Move", AdKat_CommandType.MovePlayer);
            this.AdKat_RecordTypesInv.Add("ForceMove", AdKat_CommandType.ForceMovePlayer);
            this.AdKat_RecordTypesInv.Add("Teamswap", AdKat_CommandType.Teamswap);
            this.AdKat_RecordTypesInv.Add("KillSelf", AdKat_CommandType.KillSelf);
            this.AdKat_RecordTypesInv.Add("Kill", AdKat_CommandType.KillPlayer);
            this.AdKat_RecordTypesInv.Add("RepeatKill", AdKat_CommandType.RepeatKillPlayer);
            this.AdKat_RecordTypesInv.Add("LowPopKill", AdKat_CommandType.LowPopKillPlayer);
            this.AdKat_RecordTypesInv.Add("Kick", AdKat_CommandType.KickPlayer);
            this.AdKat_RecordTypesInv.Add("TempBan", AdKat_CommandType.TempBanPlayer);
            this.AdKat_RecordTypesInv.Add("PermaBan", AdKat_CommandType.PermabanPlayer);
            this.AdKat_RecordTypesInv.Add("Punish", AdKat_CommandType.PunishPlayer);
            this.AdKat_RecordTypesInv.Add("Forgive", AdKat_CommandType.ForgivePlayer);
            this.AdKat_RecordTypesInv.Add("Mute", AdKat_CommandType.MutePlayer);
            this.AdKat_RecordTypesInv.Add("Join", AdKat_CommandType.JoinPlayer);
            this.AdKat_RecordTypesInv.Add("RoundWhitelist", AdKat_CommandType.RoundWhitelistPlayer);
            this.AdKat_RecordTypesInv.Add("Report", AdKat_CommandType.ReportPlayer);
            this.AdKat_RecordTypesInv.Add("CallAdmin", AdKat_CommandType.CallAdmin);
            this.AdKat_RecordTypesInv.Add("ConfirmReport", AdKat_CommandType.ConfirmReport);
            this.AdKat_RecordTypesInv.Add("AdminSay", AdKat_CommandType.AdminSay);
            this.AdKat_RecordTypesInv.Add("PlayerSay", AdKat_CommandType.PlayerSay);
            this.AdKat_RecordTypesInv.Add("AdminYell", AdKat_CommandType.AdminYell);
            this.AdKat_RecordTypesInv.Add("PlayerYell", AdKat_CommandType.PlayerYell);
            this.AdKat_RecordTypesInv.Add("RestartLevel", AdKat_CommandType.RestartLevel);
            this.AdKat_RecordTypesInv.Add("NextLevel", AdKat_CommandType.NextLevel);
            this.AdKat_RecordTypesInv.Add("EndLevel", AdKat_CommandType.EndLevel);
            this.AdKat_RecordTypesInv.Add("Nuke", AdKat_CommandType.NukeServer);
            this.AdKat_RecordTypesInv.Add("KickAll", AdKat_CommandType.KickAll);
            this.AdKat_RecordTypesInv.Add("EnforceBan", AdKat_CommandType.EnforceBan);
            this.AdKat_RecordTypesInv.Add("Exception", AdKat_CommandType.Exception);

            //Fill all command access ranks
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.RestartLevel, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.NextLevel, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.EndLevel, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.NukeServer, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.KickAll, 0);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.PermabanPlayer, 1);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.TempBanPlayer, 2);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.RoundWhitelistPlayer, 2);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.KillPlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.KickPlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.PunishPlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ForgivePlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.MutePlayer, 3);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.MovePlayer, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ForceMovePlayer, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.AdminSay, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.AdminYell, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.PlayerSay, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.PlayerYell, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.WhatIs, 4);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.Teamswap, 5);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.KillSelf, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.Voip, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ReportPlayer, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.CallAdmin, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.JoinPlayer, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ConfirmReport, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.ConfirmCommand, 6);
            this.AdKat_CommandAccessRank.Add(AdKat_CommandType.CancelCommand, 6);

            //Set the logging settings for each command
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.AdminSay, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.AdminYell, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.CallAdmin, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.CancelCommand, false);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.ConfirmCommand, false);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.ConfirmReport, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.Default, false);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.EndLevel, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.EnforceBan, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.Exception, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.ForceMovePlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.ForgivePlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.KickAll, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.KillPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.RepeatKillPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.LowPopKillPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.KickPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.MovePlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.MutePlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.JoinPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.NextLevel, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.NukeServer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.PermabanPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.PlayerSay, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.PlayerYell, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.PunishPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.ReportPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.RestartLevel, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.RoundWhitelistPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.Teamswap, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.KillSelf, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.TempBanPlayer, true);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.WhatIs, false);
            this.AdKat_LoggingSettings.Add(AdKat_CommandType.Voip, false);
        }

        #region Plugin details

        public string GetPluginName()
        {          
            return "AdKats - Advanced In-Game Admin";
        }

        public string GetPluginVersion()
        {
            return this.plugin_version;
        }

        public string GetPluginAuthor()
        {
            return "[ADK]ColColonCleaner";
        }

        public string GetPluginWebsite()
        {
            return "https://github.com/ColColonCleaner/AdKats/";
        }

        public string GetPluginDescription()
        {
            //Plugin description gets downloaded from github so updates can be pushed automatically
            string pluginDescription = "DESCRIPTION FETCH FAILED|";
            string pluginChangelog = "CHANGELOG FETCH FAILED";
            try
            {
                //Create web client
                WebClient client = new WebClient();
                //Download the readme and changelog
                pluginDescription = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/README.md");
                if (pluginDescription != "DESCRIPTION FETCH FAILED|")
                {
                    //Extract the latest stable version
                    string latestStableVersion = this.extractString(pluginDescription, "latest_stable_release");
                    if (!String.IsNullOrEmpty(latestStableVersion))
                    {
                        //Convert it to an integer
                        latestStableVersion = latestStableVersion.Replace(".", "");
                        int latestStableVersionInt = Int32.Parse(latestStableVersion);
                        //Get current plugin version
                        int currentVersionInt = Int32.Parse(this.plugin_version.Replace(".", ""));

                        String prepend = String.Empty;
                        //Add the appropriate message to plugin description
                        if (latestStableVersionInt > currentVersionInt)
                        {
                            prepend = @"<h2 style='color:#DF0101;'>
                                You are running an outdated build of AdKats! Version " + latestStableVersion + @" is available for download!
                            </h2>
                            <a href='https://github.com/ColColonCleaner/AdKats/blob/master/CHANGELOG.md' target='_blank'>
                                New in Version " + latestStableVersion + @"!
                            </a> Download link below.";
                        }
                        else if (latestStableVersionInt == currentVersionInt)
                        {
                            prepend = @"<h2 style='color:#01DF01;'>
                                Congrats! You are running the latest stable build of AdKats!
                            </h2>";
                        }
                        else if (latestStableVersionInt < currentVersionInt)
                        {
                            prepend = @"<h2 style='color:#FF8000;'>
                                CAUTION! You are running a BETA or TEST build of AdKats! Functionality might be untested.
                            </h2> Below documentation is for stable build " + latestStableVersion + ".";
                        }
                        //Prepend the message
                        pluginDescription = prepend + pluginDescription;
                    }
                }
                pluginChangelog = client.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/CHANGELOG.md");
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while creating plugin description."));
            }
            //Concat and return
            return pluginDescription + pluginChangelog;
        }

        #endregion

        #region Plugin settings

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn;

            //Only fetch the following settings when plugin disabled
            if (!this.threadsReady)
            {
                lstReturn = new List<CPluginVariable>();

                lstReturn.Add(new CPluginVariable("Complete these settings before enabling.", typeof(string), "Once enabled, more settings will appear."));
                //SQL Settings
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Hostname", typeof(string), mySqlHostname));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Port", typeof(string), mySqlPort));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Database", typeof(string), mySqlDatabaseName));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Username", typeof(string), mySqlUsername));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Password", typeof(string), mySqlPassword));
                //Debugging Settings
                lstReturn.Add(new CPluginVariable("3. Debugging|Debug level", typeof(int), this.debugLevel));
            }
            else
            {
                //If plugin is enabled, return the full storage list
                lstReturn = this.GetPluginVariables();
                //Add display variables

                //Server Settings
                lstReturn.Add(new CPluginVariable("1. Server Settings|Server ID (Display)", typeof(int), this.server_id));
                lstReturn.Add(new CPluginVariable("1. Server Settings|Server IP (Display)", typeof(string), this.server_ip));
                lstReturn.Add(new CPluginVariable("1. Server Settings|Setting Import", typeof(string), this.server_id));
                if (!this.usingAWA)
                {
                    //Admin Settings
                    lstReturn.Add(new CPluginVariable("3. Player Access Settings|Add Access", typeof(string), ""));
                    lstReturn.Add(new CPluginVariable("3. Player Access Settings|Remove Access", typeof(string), ""));
                    if (this.playerAccessCache.Count > 0)
                    {
                        //Sort access list by access level, then by name
                        List<AdKat_Access> tempAccess = new List<AdKat_Access>();
                        foreach (AdKat_Access access in this.playerAccessCache.Values)
                        {
                            tempAccess.Add(access);
                        }
                        tempAccess.Sort(
                            delegate(AdKat_Access a1, AdKat_Access a2)
                            {
                                return (a1.access_level == a2.access_level)
                                            ?
                                        (String.Compare(a1.player_name, a2.player_name))
                                            :
                                        ((a1.access_level < a2.access_level) ? (-1) : (1));
                            }
                        );
                        foreach (AdKat_Access access in tempAccess)
                        {
                            lstReturn.Add(new CPluginVariable("3. Player Access Settings|" + access.player_name + "|Access Level", typeof(int), access.access_level));
                            //lstReturn.Add(new CPluginVariable("3. Player Access Settings|" + access.player_name + "|Email Address", typeof(string), access.player_email));
                        }
                    }
                    else
                    {
                        lstReturn.Add(new CPluginVariable("3. Player Access Settings|No Players in Access List", typeof(string), "Add Players with 'Add Access', or Re-Enable AdKats to fetch."));
                    }
                }
                else
                {
                    lstReturn.Add(new CPluginVariable("3. Player Access Settings|You are using AdKats WebAdmin", typeof(string), "Manage admin settings there."));
                }
            }
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            try
            {

                //SQL Settings
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Hostname", typeof(string), mySqlHostname));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Port", typeof(string), mySqlPort));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Database", typeof(string), mySqlDatabaseName));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Username", typeof(string), mySqlUsername));
                lstReturn.Add(new CPluginVariable("2. MySQL Settings|MySQL Password", typeof(string), mySqlPassword));

                //In-Game Command Settings
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Minimum Required Reason Length", typeof(int), this.requiredReasonLength));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Confirm Command", typeof(string), confirmCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Cancel Command", typeof(string), cancelCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Kill Player", typeof(string), killPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Kick Player", typeof(string), kickPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Temp-Ban Player", typeof(string), tempBanPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Permaban Player", typeof(string), permaBanPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Punish Player", typeof(string), punishPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Forgive Player", typeof(string), forgivePlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Mute Player", typeof(string), mutePlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Join Player", typeof(string), joinPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Round Whitelist Player", typeof(string), roundWhitelistPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|OnDeath Move Player", typeof(string), movePlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Force Move Player", typeof(string), forceMovePlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Teamswap Self", typeof(string), teamSwapPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Kill Self", typeof(string), killSelfCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Report Player", typeof(string), reportPlayerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Call Admin on Player", typeof(string), callAdminCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Admin Say", typeof(string), adminSayCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Player Say", typeof(string), playerSayCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Admin Yell", typeof(string), adminYellCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Player Yell", typeof(string), playerYellCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|What Is", typeof(string), whatIsCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Voip", typeof(string), voipCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Restart Level", typeof(string), restartLevelCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Next Level", typeof(string), nextLevelCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|End Level", typeof(string), endLevelCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Nuke Server", typeof(string), nukeServerCommandText));
                lstReturn.Add(new CPluginVariable("4. In-Game Command Settings|Kick All NonAdmins", typeof(string), kickAllCommandText));

                //Punishment Settings
                lstReturn.Add(new CPluginVariable("5. Punishment Settings|Punishment Hierarchy", typeof(string[]), this.punishmentHierarchy));
                lstReturn.Add(new CPluginVariable("5. Punishment Settings|Combine Server Punishments", typeof(Boolean), this.combineServerPunishments));
                lstReturn.Add(new CPluginVariable("5. Punishment Settings|Only Kill Players when Server in low population", typeof(Boolean), this.onlyKillOnLowPop));
                if (this.onlyKillOnLowPop)
                {
                    lstReturn.Add(new CPluginVariable("5. Punishment Settings|Low Population Value", typeof(int), this.lowPopPlayerCount));
                    lstReturn.Add(new CPluginVariable("5. Punishment Settings|IRO Punishment Overrides Low Pop", typeof(Boolean), this.IROOverridesLowPop));
                }

                //Player Report Settings
                lstReturn.Add(new CPluginVariable("6. Email Settings|Send Emails", typeof(bool), this.useEmail));
                if (this.useEmail == true && false)
                {
                    lstReturn.Add(new CPluginVariable("6. Email Settings|Email: Use SSL?", typeof(Boolean), this.emailHandler.blUseSSL));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|SMTP-Server address", typeof(string), this.emailHandler.strSMTPServer));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|SMTP-Server port", typeof(int), this.emailHandler.iSMTPPort));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|Sender address", typeof(string), this.emailHandler.strSenderMail));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|SMTP-Server username", typeof(string), this.emailHandler.strSMTPUser));
                    lstReturn.Add(new CPluginVariable("6. Email Settings|SMTP-Server password", typeof(string), this.emailHandler.strSMTPPassword));
                }

                //TeamSwap Settings
                lstReturn.Add(new CPluginVariable("7. TeamSwap Settings|Require Whitelist for Access", typeof(Boolean), this.requireTeamswapWhitelist));
                if (this.requireTeamswapWhitelist)
                {
                    lstReturn.Add(new CPluginVariable("7. TeamSwap Settings|Auto-Whitelist Count", typeof(string), this.playersToAutoWhitelist));
                }
                lstReturn.Add(new CPluginVariable("7. TeamSwap Settings|Ticket Window High", typeof(int), this.teamSwapTicketWindowHigh));
                lstReturn.Add(new CPluginVariable("7. TeamSwap Settings|Ticket Window Low", typeof(int), this.teamSwapTicketWindowLow));

                //Admin Assistant Settings
                lstReturn.Add(new CPluginVariable("8. Admin Assistant Settings|Enable Admin Assistant Perk", typeof(Boolean), this.enableAdminAssistants));
                lstReturn.Add(new CPluginVariable("8. Admin Assistant Settings|Minimum Confirmed Reports Per Week", typeof(int), this.minimumRequiredWeeklyReports));

                //Muting Settings
                lstReturn.Add(new CPluginVariable("9. Player Mute Settings|On-Player-Muted Message", typeof(string), this.mutedPlayerMuteMessage));
                lstReturn.Add(new CPluginVariable("9. Player Mute Settings|On-Player-Killed Message", typeof(string), this.mutedPlayerKillMessage));
                lstReturn.Add(new CPluginVariable("9. Player Mute Settings|On-Player-Kicked Message", typeof(string), this.mutedPlayerKickMessage));
                lstReturn.Add(new CPluginVariable("9. Player Mute Settings|# Chances to give player before kicking", typeof(int), this.mutedPlayerChances));

                //Pre-Message Settings
                lstReturn.Add(new CPluginVariable("A10. Messaging Settings|Yell display time seconds", typeof(int), this.yellDuration));
                lstReturn.Add(new CPluginVariable("A10. Messaging Settings|Pre-Message List", typeof(string[]), this.preMessageList.ToArray()));
                lstReturn.Add(new CPluginVariable("A10. Messaging Settings|Require Use of Pre-Messages", typeof(Boolean), this.requirePreMessageUse));

                //Ban Settings
                lstReturn.Add(new CPluginVariable("A11. Banning Settings|Use Additional Ban Message", typeof(Boolean), this.useBanAppend));
                if (this.useBanAppend)
                {
                    lstReturn.Add(new CPluginVariable("A11. Banning Settings|Additional Ban Message", typeof(string), this.banAppend));
                }
                if (!this.usingAWA)
                {
                    lstReturn.Add(new CPluginVariable("A11. Banning Settings|Use Ban Enforcer", typeof(Boolean), this.useBanEnforcer));
                }
                if (this.useBanEnforcer)
                {
                    lstReturn.Add(new CPluginVariable("A11. Banning Settings|Enforce New Bans by NAME", typeof(Boolean), this.defaultEnforceName));
                    lstReturn.Add(new CPluginVariable("A11. Banning Settings|Enforce New Bans by GUID", typeof(Boolean), this.defaultEnforceGUID));
                    lstReturn.Add(new CPluginVariable("A11. Banning Settings|Enforce New Bans by IP", typeof(Boolean), this.defaultEnforceIP));
                    if (this.threadsReady)
                    {
                        lstReturn.Add(new CPluginVariable("A11. Banning Settings|NAME Ban Count", typeof(int), (int)this.fetchNameBanCount()));
                        lstReturn.Add(new CPluginVariable("A11. Banning Settings|GUID Ban Count", typeof(int), (int)this.fetchGUIDBanCount()));
                        lstReturn.Add(new CPluginVariable("A11. Banning Settings|IP Ban Count", typeof(int), (int)this.fetchIPBanCount()));
                    }
                }

                //External Command Settings
                lstReturn.Add(new CPluginVariable("A12. External Command Settings|HTTP External Access Key", typeof(string), this.externalCommandAccessKey));
                if (!this.useBanEnforcer && !this.usingAWA)
                {
                    lstReturn.Add(new CPluginVariable("A12. External Command Settings|Fetch Actions from Database", typeof(Boolean), this.fetchActionsFromDB));
                }
                
                lstReturn.Add(new CPluginVariable("A13. VOIP|Server VOIP Address", typeof(string), this.serverVoipAddress));

                lstReturn.Add(new CPluginVariable("A14. Orchestration Settings|Feed MULTIBalancer Whitelist", typeof(Boolean), this.feedMULTIBalancerWhitelist));
                lstReturn.Add(new CPluginVariable("A14. Orchestration Settings|Feed Server Reserved Slots", typeof(Boolean), this.feedServerReservedSlots));
                lstReturn.Add(new CPluginVariable("A14. Orchestration Settings|Feed Stat Logger Settings", typeof(Boolean), this.feedStatLoggerSettings));

                //Debug settings
                lstReturn.Add(new CPluginVariable("D15. Debugging|Debug level", typeof(int), this.debugLevel));
                lstReturn.Add(new CPluginVariable("D15. Debugging|Debug Soldier Name", typeof(string), this.debugSoldierName));
                lstReturn.Add(new CPluginVariable("D15. Debugging|Command Entry", typeof(string), ""));

                //Experimental tools
                if (this.isADK)
                {
                    lstReturn.Add(new CPluginVariable("X99. Experimental|Use Experimental Tools", typeof(Boolean), this.useExperimentalTools));
                    if (this.useExperimentalTools)
                    {
                        lstReturn.Add(new CPluginVariable("X99. Experimental|Use NO EXPLOSIVES Limiter", typeof(Boolean), this.useNoExplosivesLimit));
                        lstReturn.Add(new CPluginVariable("X99. Experimental|Use Grenade Cook Catcher", typeof(Boolean), this.useGrenadeCookCatcher));
                        lstReturn.Add(new CPluginVariable("X99. Experimental|HackerChecker: Enable", typeof(Boolean), this.useHackerChecker));
                        if (this.useHackerChecker)
                        {
                            lstReturn.Add(new CPluginVariable("X99. Experimental|HackerChecker: DPS Checker: Enable", typeof(Boolean), this.useDPSChecker));
                            if (this.useDPSChecker)
                            {
                                lstReturn.Add(new CPluginVariable("X99. Experimental|HackerChecker: DPS Checker: Trigger Level", typeof(double), this.DPSTriggerLevel));
                            }
                            lstReturn.Add(new CPluginVariable("X99. Experimental|HackerChecker: HSK Checker: Enable", typeof(Boolean), this.useHSKChecker));
                            if (this.useHSKChecker)
                            {
                                lstReturn.Add(new CPluginVariable("X99. Experimental|HackerChecker: HSK Checker: Trigger Level", typeof(double), this.HSKTriggerLevel));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error While Fetching Settings", e));
            }
            return lstReturn;
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            try
            {
                string[] variableParse = CPluginVariable.DecodeStringArray(strVariable);

                if (strVariable.Equals("UpdateSettings"))
                {
                    //Do nothing. Settings page will be updated after return.
                }
                else if (Regex.Match(strVariable, @"HackerCheck Player").Success)
                {
                    if(String.IsNullOrEmpty(strValue))
                    {
                        this.ConsoleError("Player name was null.");
                        return;
                    }
                    AdKat_Player aPlayer = new AdKat_Player();
                    aPlayer.player_name = strValue;
                    //Fetch their stats from BF3Stats
                    this.fetchPlayerStats(aPlayer);
                    if (aPlayer.stats != null)
                    {
                        Boolean hacker = false;
                        if (this.damageHackCheck(aPlayer, true))
                        {
                            hacker = true;
                            this.ConsoleWarn(aPlayer.player_name + " is using damage mod.");
                        }
                        if (this.aimbotHackCheck(aPlayer, true))
                        {
                            hacker = true;
                            this.ConsoleWarn(aPlayer.player_name + " is using aimbot.");
                        }
                        if (!hacker)
                        {
                            this.ConsoleSuccess(aPlayer.player_name + " is clean.");
                        }
                    }
                    else
                    {
                        this.ConsoleError(aPlayer.stats.stats_exception.message);
                    }
                }
                else if (Regex.Match(strVariable, @"Setting Import").Success)
                {
                    int tmp = -1;
                    if (int.TryParse(strValue, out tmp))
                    {
                        if (tmp != -1)
                            this.queueSettingImport(tmp);
                    }
                    else
                    {
                        this.ConsoleError("Invalid Input for Setting Import");
                    }
                }
                else if (Regex.Match(strVariable, @"Using AdKats WebAdmin").Success)
                {
                    Boolean tmp = false;
                    if (Boolean.TryParse(strValue, out tmp))
                    {
                        this.usingAWA = tmp;

                        //Update necessary settings for AWA use
                        if (this.usingAWA)
                        {
                            this.useBanEnforcer = true;
                            this.fetchActionsFromDB = true;
                            this.dbCommHandle.Set();
                        }
                    }
                    else
                    {
                        this.ConsoleError("Invalid Input for Using AdKats WebAdmin");
                    }
                }
                #region debugging
                else if (Regex.Match(strVariable, @"Command Entry").Success)
                {
                    //Check if the message is a command
                    if (strValue.StartsWith("@") || strValue.StartsWith("!"))
                    {
                        strValue = strValue.Substring(1);
                    }
                    else if (strValue.StartsWith("/@") || strValue.StartsWith("/!"))
                    {
                        strValue = strValue.Substring(2);
                    }
                    else if (strValue.StartsWith("/"))
                    {
                        strValue = strValue.Substring(1);
                    }
                    AdKat_Record recordItem = new AdKat_Record();
                    recordItem.command_source = AdKat_CommandSource.Settings;
                    recordItem.source_name = "SettingsAdmin";
                    this.completeRecord(recordItem, strValue);
                }
                else if (Regex.Match(strVariable, @"Debug level").Success)
                {
                    int tmp = 2;
                    if (int.TryParse(strValue, out tmp))
                    {
                        if (tmp != this.debugLevel)
                        {
                            this.debugLevel = tmp;
                            //Once setting has been changed, upload the change to database
                            this.queueSettingForUpload(new CPluginVariable(@"Debug level", typeof(Int32), this.debugLevel));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Debug Soldier Name").Success)
                {
                    if (this.soldierNameValid(strValue))
                    {
                        if (strValue != this.debugSoldierName)
                        {
                            this.debugSoldierName = strValue;
                            //Once setting has been changed, upload the change to database
                            this.queueSettingForUpload(new CPluginVariable(@"Debug Soldier Name", typeof(string), this.debugSoldierName));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Server VOIP Address").Success)
                {
                    if (strValue != this.serverVoipAddress)
                    {
                        this.serverVoipAddress = strValue;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Server VOIP Address", typeof(string), this.serverVoipAddress));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed MULTIBalancer Whitelist").Success)
                {
                    Boolean feedMTB = Boolean.Parse(strValue);
                    if (feedMTB != this.feedMULTIBalancerWhitelist)
                    {
                        this.feedMULTIBalancerWhitelist = feedMTB;
                        this.fetchAccessList();
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Feed MULTIBalancer Whitelist", typeof(Boolean), this.feedMULTIBalancerWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Server Reserved Slots").Success)
                {
                    Boolean feedSRS = Boolean.Parse(strValue);
                    if (feedSRS != this.feedServerReservedSlots)
                    {
                        this.feedServerReservedSlots = feedSRS;
                        this.fetchAccessList();
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Feed Server Reserved Slots", typeof(Boolean), this.feedServerReservedSlots));
                    }
                }
                else if (Regex.Match(strVariable, @"Feed Stat Logger Settings").Success)
                {
                    Boolean feedSLS = Boolean.Parse(strValue);
                    if (feedSLS != this.feedStatLoggerSettings)
                    {
                        this.feedStatLoggerSettings = feedSLS;
                        this.fetchAccessList();
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Feed Stat Logger Settings", typeof(Boolean), this.feedStatLoggerSettings));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Experimental Tools").Success)
                {
                    Boolean useEXP = Boolean.Parse(strValue);
                    if (useEXP != this.useExperimentalTools)
                    {
                        this.useExperimentalTools = useEXP;
                        if (this.useExperimentalTools)
                        {
                            this.ConsoleWarn("Using experimental tools. Take caution.");
                        }
                        else
                        {
                            this.ConsoleWarn("Experimental tools disabled.");
                            this.useNoExplosivesLimit = false;
                            this.useGrenadeCookCatcher = false;
                            this.useHackerChecker = false;
                            this.useDPSChecker = false;
                            this.useHSKChecker = false;
                        }
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Use Experimental Tools", typeof(Boolean), this.useExperimentalTools));
                        this.queueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof(Boolean), this.useNoExplosivesLimit));
                        this.queueSettingForUpload(new CPluginVariable(@"Use Hacker Checker", typeof(Boolean), this.useHackerChecker));
                        this.queueSettingForUpload(new CPluginVariable(@"Use DPS Checker", typeof(Boolean), this.useDPSChecker));
                        this.queueSettingForUpload(new CPluginVariable(@"Use HSK Checker", typeof(Boolean), this.useHSKChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"Use NO EXPLOSIVES Limiter").Success)
                {
                    Boolean useLimiter = Boolean.Parse(strValue);
                    if (useLimiter != this.useNoExplosivesLimit)
                    {
                        this.useNoExplosivesLimit = useLimiter;
                        if (this.useNoExplosivesLimit)
                        {
                            this.ConsoleWarn("Internal NO EXPLOSIVES punish limit activated.");
                        }
                        else
                        {
                            this.ConsoleWarn("Internal NO EXPLOSIVES punish limit disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Use NO EXPLOSIVES Limiter", typeof(Boolean), this.useNoExplosivesLimit));
                    }
                }
                else if (Regex.Match(strVariable, @"Use Grenade Cook Catcher").Success)
                {
                    Boolean useCookCatcher = Boolean.Parse(strValue);
                    if (useCookCatcher != this.useGrenadeCookCatcher)
                    {
                        this.useGrenadeCookCatcher = useCookCatcher;
                        if (this.useGrenadeCookCatcher)
                        {
                            this.ConsoleWarn("Internal Grenade Cook Catcher activated.");
                        }
                        else
                        {
                            this.ConsoleWarn("Internal Grenade Cook Catcher disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Use Grenade Cook Catcher", typeof(Boolean), this.useGrenadeCookCatcher));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: Enable").Success)
                {
                    Boolean useHackChecker = Boolean.Parse(strValue);
                    if (useHackChecker != this.useHackerChecker)
                    {
                        this.useHackerChecker = useHackChecker;
                        if (this.useHackerChecker)
                        {
                            this.ConsoleWarn("Internal Hacker Checker activated.");
                        }
                        else
                        {
                            this.ConsoleWarn("Internal Hacker Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Use Hacker Checker", typeof(Boolean), this.useHackerChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"DPS Checker: Enable").Success)
                {
                    Boolean useDamageChecker = Boolean.Parse(strValue);
                    if (useDamageChecker != this.useDPSChecker)
                    {
                        this.useDPSChecker = useDamageChecker;
                        if (this.useDPSChecker)
                        {
                            this.ConsoleWarn("Internal Damage Mod Checker activated.");
                        }
                        else
                        {
                            this.ConsoleWarn("Internal Damage Mod Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Use DPS Checker", typeof(Boolean), this.useDPSChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: DPS Checker: Trigger Level").Success)
                {
                    double triggerLevel = Double.Parse(strValue);
                    if (this.DPSTriggerLevel != triggerLevel)
                    {
                        if (triggerLevel <= 0)
                        {
                            triggerLevel = 100.0;
                        }
                        this.DPSTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"HackerChecker: DPS Checker: Trigger Level", typeof(double), this.DPSTriggerLevel));
                    }
                }
                else if (Regex.Match(strVariable, @"HSK Checker: Enable").Success)
                {
                    Boolean useAimbotChecker = Boolean.Parse(strValue);
                    if (useAimbotChecker != this.useHSKChecker)
                    {
                        this.useHSKChecker = useAimbotChecker;
                        if (this.useHSKChecker)
                        {
                            this.ConsoleWarn("Internal Aimbot Checker activated.");
                        }
                        else
                        {
                            this.ConsoleWarn("Internal Aimbot Checker disabled.");
                        }
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Use HSK Checker", typeof(Boolean), this.useHSKChecker));
                    }
                }
                else if (Regex.Match(strVariable, @"HackerChecker: HSK Checker: Trigger Level").Success)
                {
                    double triggerLevel = Double.Parse(strValue);
                    if (this.HSKTriggerLevel != triggerLevel)
                    {
                        if (triggerLevel <= 0)
                        {
                            triggerLevel = 100.0;
                        }
                        this.HSKTriggerLevel = triggerLevel;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"HackerChecker: HSK Checker: Trigger Level", typeof(double), this.HSKTriggerLevel));
                    }
                }
                #endregion
                #region HTTP settings
                else if (Regex.Match(strVariable, @"External Access Key").Success)
                {
                    if (strValue != this.externalCommandAccessKey)
                    {
                        this.externalCommandAccessKey = strValue;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"External Access Key", typeof(string), this.externalCommandAccessKey));
                    }
                }
                else if (Regex.Match(strVariable, @"Fetch Actions from Database").Success)
                {
                    Boolean fetch;
                    if (fetch = Boolean.Parse(strValue))
                    {
                        if (fetch != this.fetchActionsFromDB)
                        {
                            this.fetchActionsFromDB = fetch;
                            this.dbCommHandle.Set();
                            //Once setting has been changed, upload the change to database
                            this.queueSettingForUpload(new CPluginVariable(@"Fetch Actions from Database", typeof(Boolean), this.fetchActionsFromDB));
                        }
                    }
                }
                #endregion
                #region ban settings
                else if (Regex.Match(strVariable, @"Use Additional Ban Message").Success)
                {
                    Boolean use = Boolean.Parse(strValue);
                    if (this.useBanAppend != use)
                    {
                        this.useBanAppend = use;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Use Additional Ban Message", typeof(Boolean), this.useBanAppend));
                    }
                }
                else if (Regex.Match(strVariable, @"Additional Ban Message").Success)
                {
                    if (strValue.Length > 30)
                    {
                        strValue = strValue.Substring(0, 30);
                        this.ConsoleError("Ban append cannot be more than 30 characters.");
                    }
                    else
                    {
                        if (this.banAppend != strValue)
                        {
                            this.banAppend = strValue;
                            //Once setting has been changed, upload the change to database
                            this.queueSettingForUpload(new CPluginVariable(@"Additional Ban Message", typeof(string), this.banAppend));
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Use Ban Enforcer").Success)
                {
                    Boolean use = Boolean.Parse(strValue);
                    if (this.useBanEnforcer != use)
                    {
                        this.useBanEnforcer = use;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Use Ban Enforcer", typeof(Boolean), this.useBanEnforcer));
                        if (this.useBanEnforcer)
                        {
                            this.fetchActionsFromDB = true;
                            this.dbCommHandle.Set();
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by NAME").Success)
                {
                    Boolean enforceName = Boolean.Parse(strValue);
                    if (this.defaultEnforceName != enforceName)
                    {
                        this.defaultEnforceName = enforceName;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Enforce New Bans by NAME", typeof(Boolean), this.defaultEnforceName));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by GUID").Success)
                {
                    Boolean enforceGUID = Boolean.Parse(strValue);
                    if (this.defaultEnforceGUID != enforceGUID)
                    {
                        this.defaultEnforceGUID = enforceGUID;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Enforce New Bans by GUID", typeof(Boolean), this.defaultEnforceGUID));
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce New Bans by IP").Success)
                {
                    Boolean enforceIP = Boolean.Parse(strValue);
                    if (this.defaultEnforceIP != enforceIP)
                    {
                        this.defaultEnforceIP = enforceIP;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Enforce New Bans by IP", typeof(Boolean), this.defaultEnforceIP));
                    }
                }
                #endregion
                #region In-Game Command Settings
                else if (Regex.Match(strVariable, @"Minimum Required Reason Length").Success)
                {
                    Int32 required = Int32.Parse(strValue);
                    if (this.requiredReasonLength != required)
                    {
                        this.requiredReasonLength = required;
                        if (this.requiredReasonLength < 1)
                        {
                            this.requiredReasonLength = 1;
                        }
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Minimum Required Reason Length", typeof(Int32), this.requiredReasonLength));
                    }
                }
                else if (Regex.Match(strVariable, @"Confirm Command").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.confirmCommandText != strValue)
                        {
                            this.confirmCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.confirmCommandText = AdKat_CommandType.ConfirmCommand + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Confirm Command", typeof(string), this.confirmCommandText));
                }
                else if (Regex.Match(strVariable, @"Cancel Command").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.cancelCommandText != strValue)
                        {
                            this.cancelCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.cancelCommandText = AdKat_CommandType.CancelCommand + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Cancel Command", typeof(string), this.cancelCommandText));
                }
                else if (strVariable.EndsWith(@"Kill Player"))
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.killPlayerCommandText != strValue)
                        {
                            this.killPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.killPlayerCommandText = AdKat_CommandType.KillPlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Kill Player", typeof(string), this.killPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Kick Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.kickPlayerCommandText != strValue)
                        {
                            this.kickPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.kickPlayerCommandText = AdKat_CommandType.KickPlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Kick Player", typeof(string), this.kickPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Temp-Ban Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.tempBanPlayerCommandText != strValue)
                        {
                            this.tempBanPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.tempBanPlayerCommandText = AdKat_CommandType.TempBanPlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Temp-Ban Player", typeof(string), this.tempBanPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Permaban Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.permaBanPlayerCommandText != strValue)
                        {
                            this.permaBanPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.permaBanPlayerCommandText = AdKat_CommandType.PermabanPlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Permaban Player", typeof(string), this.permaBanPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Punish Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.punishPlayerCommandText != strValue)
                        {
                            this.punishPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.punishPlayerCommandText = AdKat_CommandType.PunishPlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Punish Player", typeof(string), this.punishPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Forgive Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.forgivePlayerCommandText != strValue)
                        {
                            this.forgivePlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.forgivePlayerCommandText = AdKat_CommandType.ForgivePlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Forgive Player", typeof(string), this.forgivePlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Mute Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.mutePlayerCommandText != strValue)
                        {
                            this.mutePlayerCommandText = strValue;
                            rebindAllCommands();
                        }
                    }
                    else
                    {
                        this.mutePlayerCommandText = AdKat_CommandType.MutePlayer + "CommandDisabled";
                    }
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Mute Player", typeof(string), this.mutePlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Join Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.joinPlayerCommandText != strValue)
                        {
                            this.joinPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.joinPlayerCommandText = AdKat_CommandType.JoinPlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Join Player", typeof(string), this.joinPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Round Whitelist Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.roundWhitelistPlayerCommandText != strValue)
                        {
                            this.roundWhitelistPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.roundWhitelistPlayerCommandText = AdKat_CommandType.RoundWhitelistPlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Round Whitelist Player", typeof(string), this.roundWhitelistPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"OnDeath Move Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.movePlayerCommandText != strValue)
                        {
                            this.movePlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.movePlayerCommandText = AdKat_CommandType.MovePlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"OnDeath Move Player", typeof(string), this.movePlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Force Move Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.forceMovePlayerCommandText != strValue)
                        {
                            this.forceMovePlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.forceMovePlayerCommandText = AdKat_CommandType.ForceMovePlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Force Move Player", typeof(string), this.forceMovePlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Teamswap Self").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.teamSwapPlayerCommandText != strValue)
                        {
                            this.teamSwapPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.teamSwapPlayerCommandText = AdKat_CommandType.Teamswap + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Teamswap Self", typeof(string), this.teamSwapPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Kill Self").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.killSelfCommandText != strValue)
                        {
                            this.killSelfCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.killSelfCommandText = AdKat_CommandType.KillSelf + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Kill Self", typeof(string), this.killSelfCommandText));
                }
                else if (Regex.Match(strVariable, @"Report Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.reportPlayerCommandText != strValue)
                        {
                            this.reportPlayerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.reportPlayerCommandText = AdKat_CommandType.ReportPlayer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Report Player", typeof(string), this.reportPlayerCommandText));
                }
                else if (Regex.Match(strVariable, @"Call Admin on Player").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.callAdminCommandText != strValue)
                        {
                            this.callAdminCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.callAdminCommandText = AdKat_CommandType.CallAdmin + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Call Admin on Player", typeof(string), this.callAdminCommandText));
                }
                else if (Regex.Match(strVariable, @"Admin Say").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.adminSayCommandText != strValue)
                        {
                            this.adminSayCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.adminSayCommandText = AdKat_CommandType.AdminSay + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Admin Say", typeof(string), this.adminSayCommandText));
                }
                else if (Regex.Match(strVariable, @"Player Say").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.playerSayCommandText != strValue)
                        {
                            this.playerSayCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.playerSayCommandText = AdKat_CommandType.PlayerSay + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Player Say", typeof(string), this.playerSayCommandText));
                }
                else if (Regex.Match(strVariable, @"Admin Yell").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.adminYellCommandText != strValue)
                        {
                            this.adminYellCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.adminYellCommandText = AdKat_CommandType.AdminYell + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Admin Yell", typeof(string), this.adminYellCommandText));
                }
                else if (Regex.Match(strVariable, @"Player Yell").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.playerYellCommandText != strValue)
                        {
                            this.playerYellCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.playerYellCommandText = AdKat_CommandType.PlayerYell + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Player Yell", typeof(string), this.playerYellCommandText));
                }
                else if (Regex.Match(strVariable, @"What Is").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.whatIsCommandText != strValue)
                        {
                            this.whatIsCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.whatIsCommandText = AdKat_CommandType.WhatIs + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"What Is", typeof(string), this.whatIsCommandText));
                }
                else if (Regex.Match(strVariable, @"Voip").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.voipCommandText != strValue)
                        {
                            this.voipCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.voipCommandText = AdKat_CommandType.Voip + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Voip", typeof(string), this.voipCommandText));
                }
                else if (Regex.Match(strVariable, @"Restart Level").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.restartLevelCommandText != strValue)
                        {
                            this.restartLevelCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.restartLevelCommandText = AdKat_CommandType.RestartLevel + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Restart Level", typeof(string), this.restartLevelCommandText));
                }
                else if (Regex.Match(strVariable, @"Next Level").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.nextLevelCommandText != strValue)
                        {
                            this.nextLevelCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.nextLevelCommandText = AdKat_CommandType.NextLevel + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Next Level", typeof(string), this.nextLevelCommandText));
                }
                else if (Regex.Match(strVariable, @"End Level").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.endLevelCommandText != strValue)
                        {
                            this.endLevelCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.endLevelCommandText = AdKat_CommandType.EndLevel + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"End Level", typeof(string), this.endLevelCommandText));
                }
                else if (Regex.Match(strVariable, @"Nuke Server").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.nukeServerCommandText != strValue)
                        {
                            this.nukeServerCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.nukeServerCommandText = AdKat_CommandType.NukeServer + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Nuke Server", typeof(string), this.nukeServerCommandText));
                }
                else if (Regex.Match(strVariable, @"Kick All NonAdmins").Success)
                {
                    if (strValue.Length > 0)
                    {
                        //trim variable
                        if (strValue.ToLower().EndsWith("|log"))
                        {
                            strValue = strValue.Remove(strValue.IndexOf("|log"));
                        }
                        if (this.kickAllCommandText != strValue)
                        {
                            this.kickAllCommandText = strValue;
                        }
                    }
                    else
                    {
                        this.kickAllCommandText = AdKat_CommandType.KickAll + "CommandDisabled";
                    }
                    rebindAllCommands();
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Kick All NonAdmins", typeof(string), this.kickAllCommandText));
                }
                #endregion
                #region punishment settings
                else if (Regex.Match(strVariable, @"Punishment Hierarchy").Success)
                {
                    this.punishmentHierarchy = CPluginVariable.DecodeStringArray(strValue);
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Punishment Hierarchy", typeof(string), CPluginVariable.EncodeStringArray(this.punishmentHierarchy)));
                }
                else if (Regex.Match(strVariable, @"Combine Server Punishments").Success)
                {
                    Boolean combine = Boolean.Parse(strValue);
                    if (this.combineServerPunishments != combine)
                    {
                        this.combineServerPunishments = combine;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Combine Server Punishments", typeof(Boolean), this.combineServerPunishments));
                    }
                }
                else if (Regex.Match(strVariable, @"Only Kill Players when Server in low population").Success)
                {
                    Boolean onlyKill = Boolean.Parse(strValue);
                    if (onlyKill != this.onlyKillOnLowPop)
                    {
                        this.onlyKillOnLowPop = onlyKill;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Only Kill Players when Server in low population", typeof(Boolean), this.onlyKillOnLowPop));
                    }
                }
                else if (Regex.Match(strVariable, @"Low Population Value").Success)
                {
                    Int32 lowPop = Int32.Parse(strValue);
                    if (lowPop != this.lowPopPlayerCount)
                    {
                        this.lowPopPlayerCount = lowPop;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Low Population Value", typeof(Int32), this.lowPopPlayerCount));
                    }
                }
                else if (Regex.Match(strVariable, @"IRO Punishment Overrides Low Pop").Success)
                {
                    Boolean overrideIRO = Boolean.Parse(strValue);
                    if (overrideIRO != this.IROOverridesLowPop)
                    {
                        this.IROOverridesLowPop = overrideIRO;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"IRO Punishment Overrides Low Pop", typeof(Boolean), this.IROOverridesLowPop));
                    }
                }
                #endregion
                #region sql settings
                else if (Regex.Match(strVariable, @"MySQL Hostname").Success)
                {
                    mySqlHostname = strValue;
                    this.dbSettingsChanged = true;
                    this.dbCommHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Port").Success)
                {
                    int tmp = 3306;
                    int.TryParse(strValue, out tmp);
                    if (tmp > 0 && tmp < 65536)
                    {
                        mySqlPort = strValue;
                        this.dbSettingsChanged = true;
                        this.dbCommHandle.Set();
                    }
                    else
                    {
                        this.ConsoleError("Invalid value for MySQL Port: '" + strValue + "'. Must be number between 1 and 65535!");
                    }
                }
                else if (Regex.Match(strVariable, @"MySQL Database").Success)
                {
                    this.mySqlDatabaseName = strValue;
                    this.dbSettingsChanged = true;
                    this.dbCommHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Username").Success)
                {
                    mySqlUsername = strValue;
                    this.dbSettingsChanged = true;
                    this.dbCommHandle.Set();
                }
                else if (Regex.Match(strVariable, @"MySQL Password").Success)
                {
                    mySqlPassword = strValue;
                    this.dbSettingsChanged = true;
                    this.dbCommHandle.Set();
                }
                #endregion
                #region email settings
                else if (strVariable.CompareTo("Send Emails") == 0)
                {
                    //Disabled
                    this.useEmail = false;// Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable("Send Emails", typeof(Boolean), this.useEmail));
                }
                else if (strVariable.CompareTo("Admin Request Email?") == 0)
                {
                    //this.blNotifyEmail = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    //this.queueSettingForUpload(new CPluginVariable("Admin Request Email?", typeof(string), strValue));
                }
                else if (strVariable.CompareTo("Use SSL?") == 0)
                {
                    this.emailHandler.blUseSSL = Boolean.Parse(strValue);
                    //Once setting has been changed, upload the change to database
                    //this.queueSettingForUpload(new CPluginVariable("Use SSL?", typeof(Boolean), this.emailHandler.blUseSSL));
                }
                else if (strVariable.CompareTo("SMTP-Server address") == 0)
                {
                    this.emailHandler.strSMTPServer = strValue;
                    //Once setting has been changed, upload the change to database
                    //this.queueSettingForUpload(new CPluginVariable("SMTP-Server address", typeof(string), strValue));
                }
                else if (strVariable.CompareTo("SMTP-Server port") == 0)
                {
                    int iPort = Int32.Parse(strValue);
                    if (iPort > 0)
                    {
                        this.emailHandler.iSMTPPort = iPort;
                        //Once setting has been changed, upload the change to database
                        //this.queueSettingForUpload(new CPluginVariable("SMTP-Server port", typeof(Int32), iPort));
                    }
                }
                else if (strVariable.CompareTo("Sender address") == 0)
                {
                    if (strValue == null || strValue == String.Empty)
                    {
                        this.emailHandler.strSenderMail = "SENDER_CANNOT_BE_EMPTY";
                        this.ConsoleError("No sender for email was given! Canceling Operation.");
                    }
                    else
                    {
                        this.emailHandler.strSenderMail = strValue;
                        //Once setting has been changed, upload the change to database
                        //this.queueSettingForUpload(new CPluginVariable("Sender address", typeof(string), strValue));
                    }
                }
                else if (strVariable.CompareTo("SMTP-Server username") == 0)
                {
                    if (strValue == null || strValue == String.Empty)
                    {
                        this.emailHandler.strSMTPUser = "SMTP_USERNAME_CANNOT_BE_EMPTY";
                        this.ConsoleError("No username for SMTP was given! Canceling Operation.");
                    }
                    else
                    {
                        this.emailHandler.strSMTPUser = strValue;
                        //Once setting has been changed, upload the change to database
                        //this.queueSettingForUpload(new CPluginVariable("SMTP-Server username", typeof(string), strValue));
                    }
                }
                else if (strVariable.CompareTo("SMTP-Server password") == 0)
                {
                    if (strValue == null || strValue == String.Empty)
                    {
                        this.emailHandler.strSMTPPassword = "SMTP_PASSWORD_CANNOT_BE_EMPTY";
                        this.ConsoleError("No password for SMTP was given! Canceling Operation.");
                    }
                    else
                    {
                        this.emailHandler.strSMTPPassword = strValue;
                        //Once setting has been changed, upload the change to database
                        //this.queueSettingForUpload(new CPluginVariable("SMTP-Server password", typeof(string), strValue));
                    }
                }
                #endregion
                #region mute settings
                else if (Regex.Match(strVariable, @"On-Player-Muted Message").Success)
                {
                    if (this.mutedPlayerMuteMessage != strValue)
                    {
                        this.mutedPlayerMuteMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"On-Player-Muted Message", typeof(string), this.mutedPlayerMuteMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Killed Message").Success)
                {
                    if (this.mutedPlayerKillMessage != strValue)
                    {
                        this.mutedPlayerKillMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"On-Player-Killed Message", typeof(string), this.mutedPlayerKillMessage));
                    }
                }
                else if (Regex.Match(strVariable, @"On-Player-Kicked Message").Success)
                {
                    if (this.mutedPlayerKickMessage != strValue)
                    {
                        this.mutedPlayerKickMessage = strValue;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"On-Player-Kicked Message", typeof(string), this.mutedPlayerKickMessage));
                    }
                }
                if (Regex.Match(strVariable, @"# Chances to give player before kicking").Success)
                {
                    int tmp = 5;
                    int.TryParse(strValue, out tmp);
                    if (this.mutedPlayerChances != tmp)
                    {
                        this.mutedPlayerChances = tmp;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"# Chances to give player before kicking", typeof(Int32), this.mutedPlayerChances));
                    }
                }
                #endregion
                #region TeamSwap settings
                else if (Regex.Match(strVariable, @"Require Whitelist for Access").Success)
                {
                    Boolean require = Boolean.Parse(strValue);
                    if (this.requireTeamswapWhitelist != require)
                    {
                        this.requireTeamswapWhitelist = require;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Require Whitelist for Access", typeof(Boolean), this.requireTeamswapWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Auto-Whitelist Count").Success)
                {
                    int tmp = 1;
                    int.TryParse(strValue, out tmp);
                    if (tmp != this.playersToAutoWhitelist)
                    {
                        if (tmp < 0)
                            tmp = 0;
                        this.playersToAutoWhitelist = tmp;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Auto-Whitelist Count", typeof(Int32), this.playersToAutoWhitelist));
                    }
                }
                else if (Regex.Match(strVariable, @"Ticket Window High").Success)
                {
                    int tmp = 2;
                    int.TryParse(strValue, out tmp);
                    if (tmp != this.teamSwapTicketWindowHigh)
                    {
                        this.teamSwapTicketWindowHigh = tmp;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Ticket Window High", typeof(Int32), this.teamSwapTicketWindowHigh));
                    }
                }
                else if (Regex.Match(strVariable, @"Ticket Window Low").Success)
                {
                    int tmp = 2;
                    int.TryParse(strValue, out tmp);
                    if (tmp != this.teamSwapTicketWindowLow)
                    {
                        this.teamSwapTicketWindowLow = tmp;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Ticket Window Low", typeof(Int32), this.teamSwapTicketWindowLow));
                    }
                }
                #endregion
                #region Admin Assistants
                else if (Regex.Match(strVariable, @"Enable Admin Assistant Perk").Success)
                {
                    Boolean enableAA = Boolean.Parse(strValue);
                    if (this.enableAdminAssistants != enableAA)
                    {
                        this.enableAdminAssistants = enableAA;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Enable Admin Assistant Perk", typeof(Boolean), this.enableAdminAssistants));
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Confirmed Reports Per Week").Success)
                {
                    Int32 weeklyReports = Int32.Parse(strValue);
                    if (this.minimumRequiredWeeklyReports != weeklyReports)
                    {
                        this.minimumRequiredWeeklyReports = weeklyReports;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Minimum Confirmed Reports Per Week", typeof(Int32), this.minimumRequiredWeeklyReports));
                    }
                }
                #endregion
                #region Messaging Settings
                else if (Regex.Match(strVariable, @"Yell display time seconds").Success)
                {
                    Int32 yellTime = Int32.Parse(strValue);
                    if (this.yellDuration != yellTime)
                    {
                        this.yellDuration = yellTime;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Yell display time seconds", typeof(Int32), this.yellDuration));
                    }
                }
                else if (Regex.Match(strVariable, @"Pre-Message List").Success)
                {
                    this.preMessageList = new List<string>(CPluginVariable.DecodeStringArray(strValue));
                    //Once setting has been changed, upload the change to database
                    this.queueSettingForUpload(new CPluginVariable(@"Pre-Message List", typeof(string), CPluginVariable.EncodeStringArray(this.preMessageList.ToArray())));
                }
                else if (Regex.Match(strVariable, @"Require Use of Pre-Messages").Success)
                {
                    Boolean require = Boolean.Parse(strValue);
                    if (require != this.requirePreMessageUse)
                    {
                        this.requirePreMessageUse = require;
                        //Once setting has been changed, upload the change to database
                        this.queueSettingForUpload(new CPluginVariable(@"Require Use of Pre-Messages", typeof(Boolean), this.requirePreMessageUse));
                    }
                }
                #endregion
                #region access settings
                else if (Regex.Match(strVariable, @"Add Access").Success)
                {
                    if (this.soldierNameValid(strValue))
                    {
                        //Create the access object
                        AdKat_Access access = new AdKat_Access();
                        access.player_name = strValue;
                        access.access_level = 6;
                        //Queue it for processing
                        this.queueAccessUpdate(access);
                    }
                }
                else if (Regex.Match(strVariable, @"Remove Access").Success)
                {
                    this.queueAccessRemoval(strValue);
                }
                else if (this.playerAccessCache.ContainsKey(variableParse[0]))
                {
                    this.DebugWrite("Preparing for access change", 5);
                    AdKat_Access access = this.playerAccessCache[variableParse[0]];
                    if (variableParse[1] == "Access Level")
                    {
                        this.DebugWrite("Changing access level", 5);
                        access.access_level = Int32.Parse(strValue);
                    }
                    else if (variableParse[1] == "Email Address")
                    {
                        this.DebugWrite("Changing email", 5);
                        access.player_email = strValue;
                    }
                    this.queueAccessUpdate(access);
                }
                #endregion
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error occured while updating AdKats settings.", e));
            }
        }

        private void rebindAllCommands()
        {
            this.DebugWrite("Entering rebindAllCommands", 7);
            try
            {
                //Create a new dictionary for all commands
                Dictionary<String, AdKat_CommandType> tempDictionary = new Dictionary<String, AdKat_CommandType>();

                //Update confirm and cancel 
                this.confirmCommandText = this.parseAddCommand(tempDictionary, this.confirmCommandText, AdKat_CommandType.ConfirmCommand);
                this.cancelCommandText = this.parseAddCommand(tempDictionary, this.cancelCommandText, AdKat_CommandType.CancelCommand);
                //Update Action Commands
                this.killPlayerCommandText = this.parseAddCommand(tempDictionary, this.killPlayerCommandText, AdKat_CommandType.KillPlayer);
                this.kickPlayerCommandText = this.parseAddCommand(tempDictionary, this.kickPlayerCommandText, AdKat_CommandType.KickPlayer);
                this.tempBanPlayerCommandText = this.parseAddCommand(tempDictionary, this.tempBanPlayerCommandText, AdKat_CommandType.TempBanPlayer);
                this.permaBanPlayerCommandText = this.parseAddCommand(tempDictionary, this.permaBanPlayerCommandText, AdKat_CommandType.PermabanPlayer);
                this.punishPlayerCommandText = this.parseAddCommand(tempDictionary, this.punishPlayerCommandText, AdKat_CommandType.PunishPlayer);
                this.forgivePlayerCommandText = this.parseAddCommand(tempDictionary, this.forgivePlayerCommandText, AdKat_CommandType.ForgivePlayer);
                this.mutePlayerCommandText = this.parseAddCommand(tempDictionary, this.mutePlayerCommandText, AdKat_CommandType.MutePlayer);
                this.joinPlayerCommandText = this.parseAddCommand(tempDictionary, this.joinPlayerCommandText, AdKat_CommandType.JoinPlayer);
                this.roundWhitelistPlayerCommandText = this.parseAddCommand(tempDictionary, this.roundWhitelistPlayerCommandText, AdKat_CommandType.RoundWhitelistPlayer);
                this.movePlayerCommandText = this.parseAddCommand(tempDictionary, this.movePlayerCommandText, AdKat_CommandType.MovePlayer);
                this.forceMovePlayerCommandText = this.parseAddCommand(tempDictionary, this.forceMovePlayerCommandText, AdKat_CommandType.ForceMovePlayer);
                this.teamSwapPlayerCommandText = this.parseAddCommand(tempDictionary, this.teamSwapPlayerCommandText, AdKat_CommandType.Teamswap);
                this.killSelfCommandText = this.parseAddCommand(tempDictionary, this.killSelfCommandText, AdKat_CommandType.KillSelf);
                this.reportPlayerCommandText = this.parseAddCommand(tempDictionary, this.reportPlayerCommandText, AdKat_CommandType.ReportPlayer);
                this.callAdminCommandText = this.parseAddCommand(tempDictionary, this.callAdminCommandText, AdKat_CommandType.CallAdmin);
                this.nukeServerCommandText = this.parseAddCommand(tempDictionary, this.nukeServerCommandText, AdKat_CommandType.NukeServer);
                this.kickAllCommandText = this.parseAddCommand(tempDictionary, this.kickAllCommandText, AdKat_CommandType.KickAll);
                //Update Messaging Commands
                this.adminSayCommandText = this.parseAddCommand(tempDictionary, this.adminSayCommandText, AdKat_CommandType.AdminSay);
                this.playerSayCommandText = this.parseAddCommand(tempDictionary, this.playerSayCommandText, AdKat_CommandType.PlayerSay);
                this.adminYellCommandText = this.parseAddCommand(tempDictionary, this.adminYellCommandText, AdKat_CommandType.AdminYell);
                this.playerYellCommandText = this.parseAddCommand(tempDictionary, this.playerYellCommandText, AdKat_CommandType.PlayerYell);
                this.whatIsCommandText = this.parseAddCommand(tempDictionary, this.whatIsCommandText, AdKat_CommandType.WhatIs);
                this.voipCommandText = this.parseAddCommand(tempDictionary, this.voipCommandText, AdKat_CommandType.Voip);
                //Update Level Control Commands
                this.restartLevelCommandText = this.parseAddCommand(tempDictionary, this.restartLevelCommandText, AdKat_CommandType.RestartLevel);
                this.nextLevelCommandText = this.parseAddCommand(tempDictionary, this.nextLevelCommandText, AdKat_CommandType.NextLevel);
                this.endLevelCommandText = this.parseAddCommand(tempDictionary, this.endLevelCommandText, AdKat_CommandType.EndLevel);

                //Overwrite command string dictionary with the new one
                this.AdKat_CommandStrings = tempDictionary;
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while rebinding all commands.", e));
            }
            this.DebugWrite("Exiting rebindAllCommands", 7);
        }

        private String parseAddCommand(Dictionary<String, AdKat_CommandType> tempDictionary, String strCommand, AdKat_CommandType enumCommand)
        {
            try
            {
                this.DebugWrite("Entering parseAddCommand. Command: " + strCommand, 7);
                //Command can be in two sections, split input string
                String[] split = strCommand.ToLower().Split('|');
                this.DebugWrite("Split command", 7);
                //Attempt to add command to dictionary
                tempDictionary.Add(split[0], enumCommand);
                this.DebugWrite("added command", 7);

                //Check for additional input (Currently disabled)
                if (split.Length > 1)
                {
                    //There is additional input, check if it's valid

                    //TODO make access ranks for commands editable here
                    /*if (split[1] == "log")
                    {
                        this.setLoggingForCommand(enumCommand, true);
                    }
                    else
                    {
                        this.ConsoleError("Invalid command format for: " + enumCommand);
                        return enumCommand + " INVALID FORMAT";
                    }*/
                }
                this.DebugWrite("parseAddCommand Finished!", 7);
                return strCommand;
            }
            catch (ArgumentException e)
            {
                //The command attempting to add was the same name as another command currently in the dictionary, inform the user.
                this.ConsoleError("Duplicate Command detected for " + enumCommand + ". That command will not work.");
                return enumCommand + "DuplicateCommandName";
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Unexpected error while adding command to command dictionary.", e));
                return enumCommand + "UnknownError";
            }
        }

        #endregion

        #region Threading

        public void InitWaitHandles()
        {
            //Initializes all wait handles 
            this.teamswapHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.listPlayersHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.messageParsingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.commandParsingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.dbCommHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.actionHandlingHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.banEnforcerHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.hackerCheckerHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.serverInfoHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.statLoggerStatusHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void setAllHandles()
        {
            //Opens all handles to make sure all threads complete one loop
            this.teamswapHandle.Set();
            this.listPlayersHandle.Set();
            this.messageParsingHandle.Set();
            this.commandParsingHandle.Set();
            this.dbCommHandle.Set();
            this.actionHandlingHandle.Set();
            this.banEnforcerHandle.Set();
            this.hackerCheckerHandle.Set();
            this.serverInfoHandle.Set();
            this.statLoggerStatusHandle.Set();
        }

        public void InitThreads()
        {
            try
            {
                //Creats all threads with their starting methods and set to run in the background
                this.MessagingThread = new Thread(new ThreadStart(messagingThreadLoop));
                this.MessagingThread.IsBackground = true;

                this.CommandParsingThread = new Thread(new ThreadStart(commandParsingThreadLoop));
                this.CommandParsingThread.IsBackground = true;

                this.DatabaseCommThread = new Thread(new ThreadStart(databaseCommThreadLoop));
                this.DatabaseCommThread.IsBackground = true;

                this.ActionHandlingThread = new Thread(new ThreadStart(actionHandlingThreadLoop));
                this.ActionHandlingThread.IsBackground = true;

                this.TeamSwapThread = new Thread(new ThreadStart(teamswapThreadLoop));
                this.TeamSwapThread.IsBackground = true;

                this.BanEnforcerThread = new Thread(new ThreadStart(banEnforcerThreadLoop));
                this.BanEnforcerThread.IsBackground = true;

                this.HackerCheckerThread = new Thread(new ThreadStart(hackerCheckerThreadLoop));
                this.HackerCheckerThread.IsBackground = true;
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error occured while initializing threads.", e));
            }
        }

        public void StartThreads()
        {
            this.DebugWrite("Entering StartThreads", 7);
            try
            {
                //Start the main thread
                //DB Comm is the heart of AdKats, everything revolves around that thread
                this.DatabaseCommThread.Start();
                //Other threads are started within the db comm thread
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while starting processing threads.", e));
            }
            this.DebugWrite("Exiting StartThreads", 7);
        }

        public Boolean allThreadsReady()
        {
            this.DebugWrite("Entering allThreadsReady", 7);
            Boolean ready = true;
            try
            {
                if (this.teamswapHandle.WaitOne(0))
                {
                    this.DebugWrite("TeamSwap not ready.", 7);
                    ready = false;
                }
                if (this.messageParsingHandle.WaitOne(0))
                {
                    this.DebugWrite("messaging not ready.", 7);
                    ready = false;
                }
                if (this.commandParsingHandle.WaitOne(0))
                {
                    this.DebugWrite("command parsing not ready.", 7);
                    ready = false;
                }
                if (this.dbCommHandle.WaitOne(0))
                {
                    this.DebugWrite("db comm not ready.", 7);
                    ready = false;
                }
                if (this.actionHandlingHandle.WaitOne(0))
                {
                    this.DebugWrite("action handling not ready.", 7);
                    ready = false;
                }
                if (this.banEnforcerHandle.WaitOne(0))
                {
                    this.DebugWrite("ban enforcer not ready.", 7);
                    ready = false;
                }
                if (this.hackerCheckerHandle.WaitOne(0))
                {
                    this.DebugWrite("hacker checker not ready.", 7);
                    ready = false;
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while checking if all threads ready.", e));
            }
            this.DebugWrite("Exiting allThreadsReady", 7);
            return ready;
        }

        #endregion

        #region Procon Events

        private void disable()
        {
            //Call Disable
            this.ExecuteCommand("procon.protected.plugins.enable", "AdKats", "False");
            //Set enable false
            this.isEnabled = false;
            this.threadsReady = false;
        }

        private void enable()
        {
            //Call Enable
            this.ExecuteCommand("procon.protected.plugins.enable", "AdKats", "True");
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.DebugWrite("Entering OnPluginLoaded", 7);
            try
            {
                //Set the server IP
                this.server_ip = strHostName + ":" + strPort;
                //Register all events
                this.RegisterEvents(this.GetType().Name,
                    "OnVersion",
                    "OnServerInfo",
                    "OnListPlayers",
                    "OnPunkbusterPlayerInfo",
                    "OnReservedSlotsList",
                    "OnPlayerKilled",
                    "OnPlayerSpawned",
                    "OnPlayerTeamChange",
                    "OnPlayerJoin",
                    "OnPlayerLeft",
                    "OnGlobalChat",
                    "OnTeamChat",
                    "OnSquadChat",
                    "OnLevelLoaded",
                    "OnBanAdded",
                    "OnBanRemoved",
                    "OnBanListClear",
                    "OnBanListSave",
                    "OnBanListLoad",
                    "OnBanList");
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("FATAL ERROR on plugin load.", e));
            }
            this.DebugWrite("Exiting OnPluginLoaded", 7);
        }

        //DONE
        public void OnPluginEnable()
        {
            try
            {
                //If the finalizer is still alive, inform the user and disable
                if (this.finalizer != null && this.finalizer.IsAlive)
                {
                    ConsoleError("Cannot enable plugin while it is shutting down. Please Wait.");
                    //Disable the plugin
                    this.disable();
                    return;
                }
                //Create a new thread to activate the plugin
                this.activator = new Thread(new ThreadStart(delegate()
                {
                    try
                    {
                        //TODO
                        //Initialize the stat library
                        this.statLibrary = new StatLibrary();

                        ConsoleWrite("Waiting a few seconds for requirements to initialize, please wait...");
                        //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                        Thread.Sleep(5000);
                        ConsoleWrite("Initializing AdKats " + this.GetPluginVersion() + " components.");
                        DateTime startTime = DateTime.Now;

                        //Confirm Stat Logger active and properly configured
                        this.ConsoleWrite("Confirming proper setup for CChatGUIDStatsLoggerBF3, please wait...");
                        if (this.confirmStatLoggerSetup())
                        {
                            this.ConsoleSuccess("^bCChatGUIDStatsLoggerBF3^n enabled and active!");
                        }
                        else
                        {
                            //Stat logger could not be enabled or managed, disabled AdKats
                            this.disable();
                            return;
                        }

                        //Inform of IP
                        this.ConsoleSuccess("Server IP is " + this.server_ip + "!");

                        //Set the enabled variable
                        this.isEnabled = true;

                        //Init and start all the threads
                        this.InitWaitHandles();
                        this.setAllHandles();
                        this.InitThreads();
                        this.StartThreads();
                    }
                    catch (Exception e)
                    {
                        this.HandleException(new AdKat_Exception("Error while enabling AdKats.", e));
                    }
                }));

                //Start the thread
                this.activator.Start();
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while initializing activator thread.", e));
            }
        }

        //DONE
        public void OnPluginDisable()
        {
            //If the plugin is already disabling then cancel
            if (this.finalizer != null && this.finalizer.IsAlive)
                return;
            try
            {
                //Create a new thread to disabled the plugin
                this.finalizer = new Thread(new ThreadStart(delegate()
                {
                    try
                    {
                        //Disable settings
                        this.isEnabled = false;
                        this.threadsReady = false;
                        //Remove the available indicator
                        this.UnregisterCommand(AdKatsAvailableIndicator);
                        //Open all handles. Threads will finish on their own.
                        this.setAllHandles();

                        //Reset all caches and storage
                        if(this.playerAccessRemovalQueue != null)
                            this.playerAccessRemovalQueue.Clear();
                        if (this.playerAccessUpdateQueue != null)
                            this.playerAccessUpdateQueue.Clear();
                        if (this.teamswapForceMoveQueue != null)
                            this.teamswapForceMoveQueue.Clear();
                        if (this.teamswapOnDeathCheckingQueue != null)
                            this.teamswapOnDeathCheckingQueue.Clear();
                        if (this.teamswapOnDeathMoveDic != null)
                            this.teamswapOnDeathMoveDic.Clear();
                        if (this.unparsedCommandQueue != null)
                            this.unparsedCommandQueue.Clear();
                        if (this.unparsedMessageQueue != null)
                            this.unparsedMessageQueue.Clear();
                        if (this.unprocessedActionQueue != null)
                            this.unprocessedActionQueue.Clear();
                        if (this.unprocessedRecordQueue != null)
                            this.unprocessedRecordQueue.Clear();
                        if (this.banEnforcerCheckingQueue != null)
                            this.banEnforcerCheckingQueue.Clear();
                        this.server_id = -1;
                        this.toldCol = false;
                        this.RUPlayerCount = 0;
                        if (this.RUMoveQueue != null)
                            this.RUMoveQueue.Clear();
                        this.USPlayerCount = 0;
                        if (this.USMoveQueue != null)
                            this.USMoveQueue.Clear();
                        if (this.roundCookers != null)
                            this.roundCookers.Clear();
                        if (this.round_reports != null)
                            this.round_reports.Clear();
                        if (this.round_mutedPlayers != null)
                            this.round_mutedPlayers.Clear();
                        if (this.playerDictionary != null)
                            this.playerDictionary.Clear();
                        if (this.playerAccessCache != null)
                            this.playerAccessCache.Clear();
                        if (this.FrostbitePlayerInfoList != null)
                            this.FrostbitePlayerInfoList.Clear();
                        if (this.cBanProcessingQueue != null)
                            this.cBanProcessingQueue.Clear();
                        if (this.banEnforcerProcessingQueue != null)
                            this.banEnforcerProcessingQueue.Clear();
                        if (this.adminAssistantCache != null)
                            this.adminAssistantCache.Clear();
                        if (this.actOnSpawnDictionary != null)
                            this.actOnSpawnDictionary.Clear();
                        if (this.actionConfirmDic != null)
                            this.actionConfirmDic.Clear();
                        if (this.teamswapRoundWhitelist != null)
                            this.teamswapRoundWhitelist.Clear();
                        //Now that plugin is disabled, update the settings page to reflect
                        this.updateSettingPage();
                        ConsoleWrite("^b^1AdKats " + this.GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e)
                    {
                        this.HandleException(new AdKat_Exception("Error occured while disabling Adkats.", e));
                    }
                }));

                //Start the finalizer thread
                this.finalizer.Start();
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error occured while initializing AdKats disable thread.", e));
            }
        }

        public override void OnPlayerTeamChange(string soldierName, int teamId, int squadId)
        {
            this.DebugWrite("Entering OnPlayerTeamChange", 7);
            try
            {
                //When a player changes team, tell teamswap to recheck queues
                this.teamswapHandle.Set();
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while handling player team change.", e));
            }
            this.DebugWrite("Exiting OnPlayerTeamChange", 7);
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset cpsSubset)
        {
            try
            {
                //Only handle the list if it is an "All players" list
                if (cpsSubset.Subset == CPlayerSubset.PlayerSubsetType.All)
                {
                    //Return if small duration (5 seconds) since last player list
                    if ((DateTime.Now - this.lastSuccessfulPlayerList) < TimeSpan.FromSeconds(5))
                    {
                        //Avoid "over-handling" player listing
                        return;
                    }
                    else
                    {
                        //Only perform the following if all threads are ready
                        if (this.threadsReady)
                        {
                            this.DebugWrite("Listing Players", 5);
                            //Player list and ban list need to be locked for this operation
                            lock (this.playersMutex)
                            {
                                List<String> playerNames = new List<String>();
                                //Reset the player counts of both sides and recount everything
                                this.USPlayerCount = 0;
                                this.RUPlayerCount = 0;
                                //Loop over all players in the list
                                foreach (CPlayerInfo player in players)
                                {
                                    playerNames.Add(player.SoldierName);
                                    AdKat_Player aPlayer = null;
                                    //Check if the player is already in the player dictionary
                                    if (this.playerDictionary.TryGetValue(player.SoldierName, out aPlayer))
                                    {
                                        //If they are just update the internal frostbite player info
                                        this.playerDictionary[player.SoldierName].frostbitePlayerInfo = player;
                                    }
                                    else
                                    {
                                        //If they aren't in the list, fetch their profile from the database
                                        aPlayer = this.fetchPlayer(-1, player.SoldierName, player.GUID, null);
                                        //Add the frostbite player info
                                        aPlayer.frostbitePlayerInfo = player;
                                        //Set their last death/spawn times
                                        aPlayer.lastDeath = DateTime.Now;
                                        aPlayer.lastSpawn = DateTime.Now;
                                        //Add them to the dictionary
                                        this.playerDictionary.Add(player.SoldierName, aPlayer);
                                        //If using ban enforcer, check the player's ban status
                                        if (this.useBanEnforcer)
                                        {
                                            this.queuePlayerForBanCheck(aPlayer);
                                        }
                                        else if (this.useExperimentalTools && this.useHackerChecker)
                                        {
                                            //Queue the player for a hacker check
                                            this.queuePlayerForHackerCheck(aPlayer);
                                        }
                                        //Load admin assistat status
                                        lock (this.adminAssistantCache)
                                        {
                                            if (this.isAdminAssistant(aPlayer))
                                            {
                                                this.DebugWrite(player.SoldierName + " IS an Admin Assistant.", 3);
                                                if (!this.adminAssistantCache.ContainsKey(player.SoldierName))
                                                {
                                                    this.adminAssistantCache.Add(player.SoldierName, false);
                                                    this.DebugWrite(player.SoldierName + " added to the Admin Assistant Cache.", 4);
                                                }
                                                else
                                                {
                                                    this.DebugWrite("Player is already in the admin assitant cache, this is abnormal.", 3);
                                                }
                                            }
                                            else
                                            {
                                                this.DebugWrite(player.SoldierName + " is NOT an Admin Assistant.", 4);
                                            }
                                        }
                                    }

                                    if (player.TeamID == USTeamID)
                                    {
                                        this.USPlayerCount++;
                                    }
                                    else if (player.TeamID == RUTeamID)
                                    {
                                        this.RUPlayerCount++;
                                    }
                                }
                                //Make sure the player dictionary is clean of any straglers
                                List<String> dicPlayerNames = new List<string>();
                                foreach (String player_name in this.playerDictionary.Keys)
                                {
                                    dicPlayerNames.Add(player_name);
                                }
                                int straglerCount = 0;
                                int dicCount = this.playerDictionary.Count;
                                foreach (String player_name in dicPlayerNames)
                                {
                                    if (!playerNames.Contains(player_name))
                                    {
                                        straglerCount++;
                                        this.DebugWrite("Removing " + player_name + " from current player list (VIA CLEANUP).", 4);
                                        this.playerDictionary.Remove(player_name);
                                    }
                                }
                                //Inform the admins of disconnect
                                if (straglerCount > (dicCount / 2))
                                {
                                    //Create the report record
                                    AdKat_Record record = new AdKat_Record();
                                    record.server_id = this.server_id;
                                    record.command_type = AdKat_CommandType.CallAdmin;
                                    record.command_numeric = 0;
                                    record.target_name = "Server";
                                    record.target_player = null;
                                    record.source_name = "AdKats";
                                    record.record_message = "Server Crashed / Blaze Disconnected";
                                    //Process the record
                                    this.queueRecordForProcessing(record);
                                    this.ConsoleError(record.record_message);
                                }
                            }

                            //Update last successful player list time
                            this.lastSuccessfulPlayerList = DateTime.Now;
                            //Set the handle for TeamSwap 
                            this.listPlayersHandle.Set();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error occured while listing players.", e));
            }
        }

        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo cpbiPlayer)
        {
            try
            {
                this.DebugWrite("OPPI: OnPunkbusterPlayerInfo fired!", 7);
                AdKat_Player targetPlayer = null;
                Boolean updatePlayer = false;
                if (this.playerDictionary.TryGetValue(cpbiPlayer.SoldierName, out targetPlayer))
                {
                    this.DebugWrite("OPPI: PB player already in the player list.", 7);
                    updatePlayer = (targetPlayer.player_ip == null);
                    //Update the player with pb info
                    targetPlayer.PBPlayerInfo = cpbiPlayer;
                    targetPlayer.player_pbguid = cpbiPlayer.GUID;
                    targetPlayer.player_slot = cpbiPlayer.SlotID;
                    targetPlayer.player_ip = cpbiPlayer.Ip.Split(':')[0];
                    if (updatePlayer)
                    {
                        this.DebugWrite("OPPI: Queueing existing player " + targetPlayer.player_name + " for update.", 4);
                        this.updatePlayer(targetPlayer);
                        //If using ban enforcer, queue player for update
                        if (this.useBanEnforcer)
                        {
                            this.queuePlayerForBanCheck(targetPlayer);
                        }
                    }
                }
                this.DebugWrite("OPPI: Player slot: " + cpbiPlayer.SlotID, 7);
                this.DebugWrite("OPPI: OnPunkbusterPlayerInfo finished!", 7);
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error occured while processing punkbuster info.", e));
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            this.DebugWrite("Entering OnServerInfo", 7);
            try
            {
                if (this.isEnabled)
                {
                    int USTeamScore = -1;
                    int RUTeamScore = -1;
                    if (serverInfo != null)
                    {
                        //Get the team scores
                        this.setServerInfo(serverInfo);
                        if (serverInfo.TeamScores != null)
                        {
                            List<TeamScore> listCurrTeamScore = serverInfo.TeamScores;
                            //During round change, teams don't exist
                            if (listCurrTeamScore.Count > 0)
                            {
                                foreach (TeamScore score in listCurrTeamScore)
                                {
                                    if (score.TeamID == AdKats.USTeamID)
                                    {
                                        USTeamScore = score.Score;
                                    }
                                    else if (score.TeamID == AdKats.RUTeamID)
                                    {
                                        RUTeamScore = score.Score;
                                    }
                                    else
                                    {
                                        this.DebugWrite("Score for team " + score.TeamID + " not parsable.", 2);
                                    }
                                }
                            }
                            else
                            {
                                this.DebugWrite("Server info fired while changing rounds, no teams to parse.", 5);
                            }

                            if (USTeamScore >= 0 && RUTeamScore >= 0)
                            {
                                this.lowestTicketCount = (USTeamScore < RUTeamScore) ? (USTeamScore) : (RUTeamScore);
                                this.highestTicketCount = (USTeamScore > RUTeamScore) ? (USTeamScore) : (RUTeamScore);
                            }
                        }

                        //Only activate the following on ADK servers.
                        Boolean wasADK = this.isADK;
                        this.isADK = serverInfo.ServerName.Contains("=ADK=");
                        if (!wasADK && this.isADK)
                        {
                            this.ConsoleWrite("Server belongs to ADK");
                            this.updateSettingPage();
                        }
                    }
                    else
                    {
                        this.HandleException(new AdKat_Exception("Server info was null"));
                    }
                    this.serverInfoHandle.Set();
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while processing server info.", e));
            }
            this.DebugWrite("Exiting OnServerInfo", 7);
        }

        public override void OnLevelLoaded(string strMapFileName, string strMapMode, int roundsPlayed, int roundsTotal)
        {
            this.DebugWrite("Entering OnLevelLoaded", 7);
            try
            {
                if (this.isEnabled)
                {
                    //Completely clear all round-centric data
                    this.round_reports = new Dictionary<string, AdKat_Record>();
                    this.round_mutedPlayers = new Dictionary<string, int>();
                    this.teamswapRoundWhitelist = new Dictionary<string, Boolean>();
                    this.actionConfirmDic = new Dictionary<string, AdKat_Record>();
                    this.actOnSpawnDictionary = new Dictionary<string, AdKat_Record>();
                    this.teamswapOnDeathMoveDic = new Dictionary<string, CPlayerInfo>();
                    this.autoWhitelistPlayers();
                    this.USMoveQueue.Clear();
                    this.RUMoveQueue.Clear();
                    this.roundCookers = new Dictionary<string, AdKat_Player>();

                    //Reset whether they have been informed
                    lock (this.adminAssistantCache)
                    {
                        foreach (string assistantName in this.adminAssistantCache.Keys)
                        {
                            this.adminAssistantCache[assistantName] = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while handling level load.", e));
            }
            this.DebugWrite("Exiting OnLevelLoaded", 7);
        }

        //Move delayed players when they are killed
        public override void OnPlayerKilled(Kill kKillerVictimDetails)
        {
            this.DebugWrite("Entering OnPlayerKilled", 7);
            try
            {
                //Used for delayed player moving
                if (isEnabled && this.teamswapOnDeathMoveDic.Count > 0)
                {
                    lock (this.teamswapMutex)
                    {
                        this.teamswapOnDeathCheckingQueue.Enqueue(kKillerVictimDetails.Victim);
                        this.teamswapHandle.Set();
                    }
                }

                Boolean gKillHandled = false;
                //Update player death information
                if (this.playerDictionary.ContainsKey(kKillerVictimDetails.Victim.SoldierName))
                {
                    lock (this.playersMutex)
                    {
                        AdKat_Player victim = this.playerDictionary[kKillerVictimDetails.Victim.SoldierName];
                        this.DebugWrite("Setting " + victim.player_name + " time of death to " + kKillerVictimDetails.TimeOfDeath, 7);
                        victim.lastDeath = kKillerVictimDetails.TimeOfDeath;
                        //Only add the last death if it's not a death by admin
                        if (!String.IsNullOrEmpty(kKillerVictimDetails.Killer.SoldierName))
                        {
                            try
                            {
                                //ADK grenade cooking catcher
                                if (this.useExperimentalTools && this.useGrenadeCookCatcher)
                                {
                                    if (this.roundCookers == null)
                                    {
                                        this.roundCookers = new Dictionary<string, AdKat_Player>();
                                    }
                                    double fuseTime = 3735.00;
                                    double possibleRange = 750.00;
                                    //Update killer information
                                    AdKat_Player killer = null;
                                    if (this.playerDictionary.TryGetValue(kKillerVictimDetails.Killer.SoldierName, out killer))
                                    {
                                        //Initialize / clean up the recent kills queue
                                        if (killer.recentKills == null)
                                        {
                                            killer.recentKills = new Queue<KeyValuePair<AdKat_Player, DateTime>>();
                                        }
                                        //Only keep the last 6 kills in memory
                                        while (killer.recentKills.Count >= 6)
                                        {
                                            killer.recentKills.Dequeue();
                                        }
                                        //Add the player
                                        killer.recentKills.Enqueue(new KeyValuePair<AdKat_Player, DateTime>(victim, kKillerVictimDetails.TimeOfDeath));
                                        //Check for cooked grenade and non-suicide
                                        if (kKillerVictimDetails.DamageType == "M67")
                                        {
                                            if (kKillerVictimDetails.Killer.SoldierName != kKillerVictimDetails.Victim.SoldierName)
                                            {
                                                double milli = 0.00;
                                                double probability = 0.00;
                                                Boolean told = false;
                                                List<KeyValuePair<AdKat_Player, string>> possible = new List<KeyValuePair<AdKat_Player, string>>();
                                                List<KeyValuePair<AdKat_Player, string>> sure = new List<KeyValuePair<AdKat_Player, string>>();
                                                foreach (KeyValuePair<AdKat_Player, DateTime> cooker in killer.recentKills)
                                                {

                                                    //Get the actual time since cooker value
                                                    milli = kKillerVictimDetails.TimeOfDeath.Subtract(cooker.Value).TotalMilliseconds;
                                                    //Calculate the percentage probability
                                                    if (Math.Abs(milli - fuseTime) < possibleRange)
                                                    {
                                                        probability = (1 - Math.Abs((milli - fuseTime) / possibleRange)) * 100;
                                                        this.DebugWrite(cooker.Key.player_name + " cooking probability: " + probability + "%", 2);
                                                    }
                                                    else
                                                    {
                                                        probability = 0.00;
                                                    }

                                                    //If probability > 60% report the player and add them to the round cookers list
                                                    if (probability > 60.00)
                                                    {
                                                        this.DebugWrite(cooker.Key.player_name + " in " + killer.player_name + " recent kills has a " + probability + "% cooking probability.", 2);
                                                        gKillHandled = true;
                                                        //Code to avoid spam
                                                        if (cooker.Key.lastAction.AddSeconds(2) < DateTime.Now)
                                                        {
                                                            cooker.Key.lastAction = DateTime.Now;
                                                        }
                                                        else
                                                        {
                                                            this.DebugWrite("Skipping additional auto-actions for multi-kill event.", 2);
                                                            continue;
                                                        }

                                                        if (!told)
                                                        {
                                                            //Inform the victim player that they will not be punished
                                                            this.playerSayMessage(kKillerVictimDetails.Killer.SoldierName, "You appear to be a victim of grenade cooking and will NOT be punished.");
                                                            told = true;
                                                        }

                                                        //Create the probability string
                                                        string probString = ((int)probability) + "-" + ((int)milli);

                                                        //If the player is already on the round cooker list, ban them
                                                        if (this.roundCookers.ContainsKey(cooker.Key.player_name))
                                                        {
                                                            //Create the ban record
                                                            AdKat_Record record = new AdKat_Record();
                                                            record.server_id = this.server_id;
                                                            record.command_type = AdKat_CommandType.PunishPlayer;
                                                            record.command_numeric = 0;
                                                            record.target_name = cooker.Key.player_name;
                                                            record.target_player = cooker.Key;
                                                            record.source_name = "AutoAdmin";
                                                            record.record_message = "Rules: Cooking Grenades [" + probString + "-X] [Victim " + killer.player_name + " Protected]";
                                                            //Process the record
                                                            this.queueRecordForProcessing(record);
                                                            //this.adminSay("Punishing " + killer.player_name + " for " + record.record_message);
                                                            this.DebugWrite(record.target_player.player_name + " punished for " + record.record_message, 2);
                                                            return;
                                                        }
                                                        //else if probability > 92.5% add them to the SURE list, and round cooker list
                                                        else if (probability > 92.5)
                                                        {
                                                            this.roundCookers.Add(cooker.Key.player_name, cooker.Key);
                                                            this.DebugWrite(cooker.Key.player_name + " added to round cooker list.", 2);
                                                            //Add to SURE
                                                            sure.Add(new KeyValuePair<AdKat_Player, string>(cooker.Key, probString));
                                                        }
                                                        //Otherwise add them to the round cooker list, and add to POSSIBLE list
                                                        else
                                                        {
                                                            this.roundCookers.Add(cooker.Key.player_name, cooker.Key);
                                                            this.DebugWrite(cooker.Key.player_name + " added to round cooker list.", 2);
                                                            //Add to POSSIBLE
                                                            possible.Add(new KeyValuePair<AdKat_Player, string>(cooker.Key, probString));
                                                        }
                                                    }
                                                }
                                                //This method used for dealing with multiple kills at the same instant i.e twin/triple headshots
                                                if (sure.Count == 1 && possible.Count == 0)
                                                {
                                                    AdKat_Player player = sure[0].Key;
                                                    string probString = sure[0].Value;
                                                    //Create the ban record
                                                    AdKat_Record record = new AdKat_Record();
                                                    record.server_id = this.server_id;
                                                    record.command_type = AdKat_CommandType.PunishPlayer;
                                                    record.command_numeric = 0;
                                                    record.target_name = player.player_name;
                                                    record.target_player = player;
                                                    record.source_name = "AutoAdmin";
                                                    record.record_message = "Rules: Cooking Grenades [" + probString + "] [Victim " + killer.player_name + " Protected]";
                                                    //Process the record
                                                    this.queueRecordForProcessing(record);
                                                    //this.adminSay("Punishing " + killer.player_name + " for " + record.record_message);
                                                    this.DebugWrite(record.target_player.player_name + " punished for " + record.record_message, 2);
                                                }
                                                else
                                                {
                                                    AdKat_Player player = null;
                                                    string probString = null;
                                                    foreach (KeyValuePair<AdKat_Player, string> playerPair in sure)
                                                    {
                                                        player = playerPair.Key;
                                                        probString = playerPair.Value;
                                                        //Create the report record
                                                        AdKat_Record record = new AdKat_Record();
                                                        record.server_id = this.server_id;
                                                        record.command_type = AdKat_CommandType.ReportPlayer;
                                                        record.command_numeric = 0;
                                                        record.target_name = player.player_name;
                                                        record.target_player = player;
                                                        record.source_name = "AutoAdmin";
                                                        record.record_message = "Possible Grenade Cooker [" + probString + "] [Victim " + killer.player_name + " Protected]";
                                                        //Process the record
                                                        this.queueRecordForProcessing(record);
                                                        this.DebugWrite(record.target_player.player_name + " reported for " + record.record_message, 2);
                                                    }
                                                    foreach (KeyValuePair<AdKat_Player, string> playerPair in possible)
                                                    {
                                                        player = playerPair.Key;
                                                        probString = playerPair.Value;
                                                        //Create the report record
                                                        AdKat_Record record = new AdKat_Record();
                                                        record.server_id = this.server_id;
                                                        record.command_type = AdKat_CommandType.ReportPlayer;
                                                        record.command_numeric = 0;
                                                        record.target_name = player.player_name;
                                                        record.target_player = player;
                                                        record.source_name = "AutoAdmin";
                                                        record.record_message = "Possible Grenade Cooker [" + probString + "] [Victim " + killer.player_name + " Protected]";
                                                        //Process the record
                                                        this.queueRecordForProcessing(record);
                                                        this.DebugWrite(record.target_player.player_name + " reported for " + record.record_message, 2);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                this.HandleException(new AdKat_Exception("Error in grenade cook catcher.", e));
                            }
                        }
                    }
                }

                try
                {
                    //ADK No EXPLOSIVES special enforcement
                    if (this.useExperimentalTools && this.useNoExplosivesLimit && !gKillHandled)
                    {
                        //Check if restricted weapon
                        if (Regex.Match(kKillerVictimDetails.DamageType, @"(?:M320|RPG|SMAW|C4|M67|Claymore|MAV|FGM-148|FIM92|ROADKILL|Death)", RegexOptions.IgnoreCase).Success)
                        {
                            //Check if suicide
                            if (kKillerVictimDetails.Killer.SoldierName != kKillerVictimDetails.Victim.SoldierName)
                            {
                                //Get player from the dictionary
                                AdKat_Player killer = null;
                                if (this.playerDictionary.TryGetValue(kKillerVictimDetails.Killer.SoldierName, out killer))
                                {
                                    //Code to avoid spam
                                    if (killer.lastAction.AddSeconds(2) < DateTime.Now)
                                    {
                                        killer.lastAction = DateTime.Now;

                                        //Create the punish record
                                        AdKat_Record record = new AdKat_Record();
                                        record.server_id = this.server_id;
                                        record.command_type = AdKat_CommandType.PunishPlayer;
                                        record.command_numeric = 0;
                                        record.target_name = killer.player_name;
                                        record.target_player = killer;
                                        record.source_name = "AutoAdmin";
                                        String removeWeapon = "Weapons/";
                                        String removeGadgets = "Gadgets/";
                                        String weapon = kKillerVictimDetails.DamageType;
                                        int index = weapon.IndexOf(removeWeapon);
                                        weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removeWeapon.Length));
                                        index = weapon.IndexOf(removeGadgets);
                                        weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removeGadgets.Length));
                                        if (Regex.Match(weapon, "RoadKill", RegexOptions.IgnoreCase).Success)
                                        {
                                            record.record_message = "Roadkilling with EOD or MAV";
                                        }
                                        else
                                        {
                                            if (Regex.Match(weapon, "Death", RegexOptions.IgnoreCase).Success)
                                            {
                                                weapon = "Mortar";
                                            }
                                            record.record_message = "Rules: Using Explosives [" + weapon + "]";
                                        }
                                        //Process the record
                                        this.queueRecordForProcessing(record);
                                    }
                                    else
                                    {
                                        this.DebugWrite("Skipping additional auto-actions for multi-kill event.", 2);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.HandleException(new AdKat_Exception("Error in no explosives auto-admin.", e));
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while processing player kill.", e));
            }
            this.DebugWrite("Exiting OnPlayerKilled", 7);
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            this.DebugWrite("Entering OnPlayerSpawned", 7);
            try
            {
                if (this.isEnabled)
                {
                    //Handle TeamSwap notifications
                    Boolean informed = true;
                    string command = this.teamSwapPlayerCommandText;
                    lock (this.adminAssistantCache)
                    {
                        if (this.enableAdminAssistants && this.adminAssistantCache.TryGetValue(soldierName, out informed))
                        {
                            if (informed == false)
                            {
                                this.ExecuteCommand("procon.protected.send", "admin.yell", "For your consistent player reporting you can now use @" + command + ". Type @" + command + " to move yourself between teams.", "10", "player", soldierName);
                                this.adminAssistantCache[soldierName] = true;
                            }
                        }
                        else if (this.teamswapRoundWhitelist.Count > 0 && this.teamswapRoundWhitelist.TryGetValue(soldierName, out informed))
                        {
                            if (informed == false)
                            {
                                this.ExecuteCommand("procon.protected.send", "admin.yell", "By random selection you can use @" + command + " for this round. Type @" + command + " to move yourself between teams.", "10", "player", soldierName);
                                this.teamswapRoundWhitelist[soldierName] = true;
                            }
                        }
                    }

                    //Handle Dev Notifications
                    if (soldierName == "ColColonCleaner" && !toldCol)
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.yell", "CONGRATS! This server has version " + this.plugin_version + " of AdKats installed!", "20", "player", "ColColonCleaner");
                        this.toldCol = true;
                    }

                    //Update player spawn information
                    if (this.playerDictionary.ContainsKey(soldierName))
                    {
                        lock (this.playersMutex)
                        {
                            this.playerDictionary[soldierName].lastSpawn = DateTime.Now;
                        }
                    }

                    if (this.actOnSpawnDictionary.Count > 0)
                    {
                        lock (this.actOnSpawnDictionary)
                        {
                            AdKat_Record record = null;
                            if (this.actOnSpawnDictionary.TryGetValue(soldierName, out record))
                            {
                                //Remove it from the dic
                                this.actOnSpawnDictionary.Remove(soldierName);
                                //Wait 1.5 seconds to kill them again
                                Thread.Sleep(1500);
                                //Queue the action
                                this.queueRecordForActionHandling(record);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while handling player spawn.", e));
            }
            this.DebugWrite("Exiting OnPlayerSpawned", 7);
        }

        //DONE
        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            this.DebugWrite("Entering OnPlayerLeft", 7);
            try
            {
                this.removePlayerFromDictionary(playerInfo.SoldierName);
                this.teamswapHandle.Set();
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while handling player exit.", e));
            }
            this.DebugWrite("Exiting OnPlayerLeft", 7);
        }

        #endregion

        #region Ban Enforcer

        private void queuePlayerForBanCheck(AdKat_Player player)
        {
            this.DebugWrite("Entering queuePlayerForBanCheck", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to queue player for ban check", 6);
                    lock (banEnforcerMutex)
                    {
                        this.banEnforcerCheckingQueue.Enqueue(player);
                        this.DebugWrite("Player queued for checking", 6);
                        this.banEnforcerHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing player for ban check.", e));
            }
            this.DebugWrite("Exiting queuePlayerForBanCheck", 7);
        }

        private void queueSettingImport(int serverID)
        {
            this.DebugWrite("Entering queueSettingImport", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to queue server ID for setting import", 6);
                    this.settingImportID = serverID;
                    this.dbCommHandle.Set();
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while preparing to import settings.", e));
            }
            this.DebugWrite("Exiting queueSettingImport", 7);
        }

        private void queueSettingForUpload(CPluginVariable setting)
        {
            this.DebugWrite("Entering queueSettingForUpload", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to queue setting " + setting.Name + " for upload", 6);
                    lock (this.settingUploadQueue)
                    {
                        this.settingUploadQueue.Enqueue(setting);
                        this.dbCommHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing setting for upload.", e));
            }
            this.DebugWrite("Exiting queueSettingForUpload", 7);
        }

        private void queueBanForProcessing(AdKat_Ban aBan)
        {
            this.DebugWrite("Entering queueBanForProcessing", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to queue ban for processing", 6);
                    lock (banEnforcerMutex)
                    {
                        this.banEnforcerProcessingQueue.Enqueue(aBan);
                        this.DebugWrite("Ban queued for processing", 6);
                        this.dbCommHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing ban for processing.", e));
            }
            this.DebugWrite("Exiting queueBanForProcessing", 7);
        }

        private void banEnforcerThreadLoop()
        {
            try
            {
                this.DebugWrite("BANENF: Starting Ban Enforcer Thread", 2);
                Thread.CurrentThread.Name = "BanEnforcer";

                Queue<AdKat_Player> playerCheckingQueue;
                while (true)
                {
                    this.DebugWrite("BANENF: Entering Ban Enforcer Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("BANENF: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Get all unchecked players
                    playerCheckingQueue = new Queue<AdKat_Player>();
                    if (this.banEnforcerCheckingQueue.Count > 0 && this.useBanEnforcer)
                    {
                        this.DebugWrite("BANENF: Preparing to lock banEnforcerMutex to retrive new players", 6);
                        lock (banEnforcerMutex)
                        {
                            this.DebugWrite("BANENF: Inbound players found. Grabbing.", 5);
                            //Grab all players in the queue
                            playerCheckingQueue = new Queue<AdKat_Player>(this.banEnforcerCheckingQueue.ToArray());
                            //Clear the queue for next run
                            this.banEnforcerCheckingQueue.Clear();
                        }
                    }
                    else
                    {
                        this.DebugWrite("BANENF: No inbound ban checks. Waiting for Input.", 4);
                        //Wait for input
                        this.banEnforcerHandle.Reset();
                        this.banEnforcerHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }

                    //Get all checks in order that they came in
                    AdKat_Player aPlayer = null;
                    while (playerCheckingQueue != null && playerCheckingQueue.Count > 0)
                    {
                        //Grab first/next player
                        aPlayer = playerCheckingQueue.Dequeue();
                        this.DebugWrite("BANENF: begin reading player", 5);

                        //this.DebugWrite("Checking " + aPlayer.player_name + " Against " + this.AdKat_BanList_Name.Count + " Name Bans. " + this.AdKat_BanList_GUID.Count + " GUID Bans. And " + this.AdKat_BanList_IP.Count + " IP Bans.", 5);

                        if (this.playerDictionary.ContainsKey(aPlayer.player_name))
                        {
                            AdKat_Ban aBan = this.fetchPlayerBan(aPlayer);

                            if (aBan != null)
                            {
                                this.DebugWrite("BANENF: BAN ENFORCED", 3);
                                //Create the new record
                                AdKat_Record record = new AdKat_Record();
                                record.source_name = "BanEnforcer";
                                record.isIRO = false;
                                record.server_id = this.server_id;
                                record.target_name = aPlayer.player_name;
                                record.target_player = aPlayer;
                                record.command_source = AdKat_CommandSource.InGame;
                                record.command_type = AdKat_CommandType.EnforceBan;
                                record.command_numeric = (int)aBan.ban_id;
                                record.record_message = aBan.ban_record.record_message;
                                //Queue record for upload
                                this.queueRecordForProcessing(record);
                                //Enforce the ban
                                this.enforceBan(aBan, true);
                            }
                            else
                            {
                                this.DebugWrite("BANENF: No ban found for player", 5);
                                //Only call a ban check if the player does not already have a ban
                                if (this.useExperimentalTools && this.useHackerChecker)
                                {
                                    this.queuePlayerForHackerCheck(aPlayer);
                                }
                            }
                        }
                    }
                }
                this.DebugWrite("BANENF: Ending Ban Enforcer Thread", 2);
            }
            catch (Exception e)
            {
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.HandleException(new AdKat_Exception("Ban enforcer thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                    return;
                }
                else
                {
                    this.HandleException(new AdKat_Exception("Error occured in ban enforcer thread.", e));
                }
            }
        }

        public override void OnBanAdded(CBanInfo ban)
        {
            if (!this.isEnabled || !this.useBanEnforcer) return;
            //this.DebugWrite("OnBanAdded fired", 6);
            //this.ExecuteCommand("procon.protected.send", "banList.list");
        }

        public override void OnBanList(List<CBanInfo> banList)
        {
            try
            {
                //Return if small duration (10 seconds) since last ban list
                if ((DateTime.Now - this.lastSuccessfulBanList) < TimeSpan.FromSeconds(10))
                {
                    this.DebugWrite("Banlist being called quickly.", 4);
                    return;
                }
                else
                {
                    DateTime startTime = DateTime.Now;
                    this.lastSuccessfulBanList = startTime;
                    if (!this.isEnabled) return;
                    this.DebugWrite("OnBanList fired", 3);
                    if (this.useBanEnforcer)
                    {
                        if (banList.Count > 0)
                        {
                            this.DebugWrite("Bans found", 3);
                            lock (this.cBanProcessingQueue)
                            {
                                //Only allow one banlist to happen during initial startup
                                if (this.useBanEnforcerPreviousState || !this.bansFirstListed)
                                {
                                    foreach (CBanInfo cBan in banList)
                                    {
                                        this.DebugWrite("Queuing Ban.", 7);
                                        this.cBanProcessingQueue.Enqueue(cBan);
                                        if (DateTime.Now - startTime > TimeSpan.FromSeconds(50))
                                        {
                                            this.HandleException(new AdKat_Exception("OnBanList took longer than 50 seconds, exiting so procon doesn't panic."));
                                            return;
                                        }
                                    }
                                    this.bansFirstListed = true;
                                }
                            }
                        }
                    }
                }
                this.dbCommHandle.Set();
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error occured while listing procon bans.", e));
            }
        }

        public override void OnBanListClear()
        {
            this.DebugWrite("Ban list cleared", 5);
        }
        public override void OnBanListSave()
        {
            this.DebugWrite("Ban list saved", 5);
        }
        public override void OnBanListLoad()
        {
            this.DebugWrite("Ban list loaded", 5);
        }

        #endregion

        #region Hacker Checker

        private void queuePlayerForHackerCheck(AdKat_Player player)
        {
            this.DebugWrite("Entering queuePlayerForHackerCheck", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to queue player for hacker check", 6);
                    lock (this.hackerCheckerMutex)
                    {
                        this.hackerCheckerQueue.Enqueue(player);
                        this.DebugWrite("Player queued for checking", 6);
                        this.hackerCheckerHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing player for hacker check.", e));
            }
            this.DebugWrite("Exiting queuePlayerForHackerCheck", 7);
        }

        public void hackerCheckerThreadLoop()
        {
            //Hacker Checker thread for ADK servers
            try
            {
                this.DebugWrite("HCKCHK: Starting Hacker Checker Thread", 2);
                Thread.CurrentThread.Name = "HackerChecker";

                Queue<AdKat_Player> playerCheckingQueue = new Queue<AdKat_Player>();
                Queue<AdKat_Player> repeatCheckingQueue = new Queue<AdKat_Player>();
                DateTime lastRepeatCheck = DateTime.Now;
                double checkedPlayers = 0;
                double playersWithStats = 0;
                while (true)
                {
                    this.DebugWrite("HCKCHK: Entering Hacker Checker Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        playerCheckingQueue.Clear();
                        repeatCheckingQueue.Clear();
                        this.DebugWrite("HCKCHK: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Get all unchecked players
                    if (this.hackerCheckerQueue.Count > 0)
                    {
                        this.DebugWrite("HCKCHK: Preparing to lock hackerCheckerMutex to retrive new players", 6);
                        lock (this.hackerCheckerMutex)
                        {
                            this.DebugWrite("HCKCHK: Inbound players found. Grabbing.", 5);
                            //Grab all players in the queue
                            playerCheckingQueue = new Queue<AdKat_Player>(this.hackerCheckerQueue.ToArray());
                            //Clear the queue for next run
                            this.hackerCheckerQueue.Clear();
                        }
                    }
                    else
                    {
                        this.DebugWrite("HCKCHK: No inbound hacker checks. Waiting 10 seconds or for input.", 4);
                        //Wait for input
                        this.hackerCheckerHandle.Reset();
                        //Either loop when handle is set, or after 3 minutes
                        this.hackerCheckerHandle.WaitOne(180000 / ((repeatCheckingQueue.Count > 0)?(repeatCheckingQueue.Count):(1)));
                    }

                    //Current player being checked
                    AdKat_Player aPlayer = null;

                    //Check one player from the repeat checking queue
                    if (repeatCheckingQueue.Count > 0)
                    {
                        //Only keep players still in the server in the repeat checking list
                        Boolean stillInServer = true;
                        do
                        {
                            aPlayer = repeatCheckingQueue.Dequeue();
                            if (!this.playerDictionary.ContainsKey(aPlayer.player_name))
                            {
                                stillInServer = false;
                            }
                        } while (!stillInServer && repeatCheckingQueue.Count > 0);
                        if (aPlayer != null)
                        {
                            //Fetch their stats from BF3Stats
                            this.fetchPlayerStats(aPlayer);
                            //check for dmg mod if stats available
                            if (aPlayer.stats != null)
                            {
                                playersWithStats++;
                                this.ConsoleSuccess(aPlayer.player_name + " now has stats. Checking.");

                                if (this.useHackerChecker)
                                {
                                    Boolean acted = false;
                                    if (this.useDPSChecker)
                                    {
                                        acted = this.damageHackCheck(aPlayer, false);
                                    }
                                    if (this.useHSKChecker && !acted)
                                    {
                                        acted = this.aimbotHackCheck(aPlayer, false);
                                    }
                                    if (acted)
                                    {
                                        this.DebugWrite(aPlayer.player_name + " banned for hacking.", 1);
                                    }
                                    else
                                    {
                                        this.DebugWrite(aPlayer.player_name + " is clean.", 5);
                                    }
                                }
                                else
                                {
                                    this.DebugWrite("Player removed from check list after disabling hacker checker.", 2);
                                }
                            }
                            else
                            {
                                //If they still dont have stats, add them back to the queue
                                repeatCheckingQueue.Enqueue(aPlayer);
                            }
                        }
                    }

                    //Get all checks in order that they came in
                    while (playerCheckingQueue != null && playerCheckingQueue.Count > 0)
                    {
                        //Grab first/next player
                        aPlayer = playerCheckingQueue.Dequeue();
                        this.DebugWrite("HCKCHK: begin reading player", 5);

                        //Fetch their stats from BF3Stats
                        this.fetchPlayerStats(aPlayer);
                        checkedPlayers++;
                        //check for dmg mod if stats available
                        if (aPlayer.stats != null)
                        {
                            playersWithStats++;

                            if (this.useHackerChecker)
                            {
                                Boolean acted = false;
                                if (this.useDPSChecker)
                                {
                                    acted = this.damageHackCheck(aPlayer, false);
                                }
                                if (this.useHSKChecker && !acted)
                                {
                                    acted = this.aimbotHackCheck(aPlayer, false);
                                }
                                if (acted)
                                {
                                    this.DebugWrite(aPlayer.player_name + " banned for hacking.", 1);
                                }
                                else
                                {
                                    this.DebugWrite(aPlayer.player_name + " is clean.", 5);
                                }
                            }
                            else
                            {
                                this.DebugWrite("Player skipped after disabling hacker checker.", 2);
                            }
                        }
                        else
                        {
                            //this.ConsoleError(aPlayer.player_name + " doesn't have stats.");
                            repeatCheckingQueue.Enqueue(aPlayer);
                        }
                        //this.ConsoleSuccess("Players with stats: " + (int)(playersWithStats / checkedPlayers * 100) + "%");
                    }
                }
                this.DebugWrite("HCKCHK: Ending Hacker Checker Thread", 2);
            }
            catch (Exception e)
            {
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.HandleException(new AdKat_Exception("Hacker Checker thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                    return;
                }
                else
                {
                    this.HandleException(new AdKat_Exception("Error occured in Hacker Checker thread.", e));
                }
            }
        }

        private Boolean damageHackCheck(AdKat_Player player, Boolean debugMode)
        {
            //Get the player's top 20 weapons
            Boolean acted = false;
            List<AdKat_WeaponStats> topWeapons = new List<AdKat_WeaponStats>();
            foreach (AdKat_WeaponStats weaponStat in player.stats.weaponStats.Values)
            {
                topWeapons.Add(weaponStat);
            }
            topWeapons.Sort(
                delegate(AdKat_WeaponStats a1, AdKat_WeaponStats a2)
                {
                    if (a1.kills == a2.kills)
                    {
                        return 0;
                    }
                    return (a1.kills < a2.kills) ? (1) : (-1);
                }
            );

            AdKat_WeaponStats actedWeapon = null;
            double actedPerc = -1;
            StatLibrary_Weapon weapon = null;
            int index = 0;
            foreach (AdKat_WeaponStats weaponStat in topWeapons)
            {
                //Break after 15th top weapon
                if (index++ > 15) { break; }
                //Only count certain weapon categories
                if (weaponStat.category == "Sub machine guns" ||
                    weaponStat.category == "Assault rifles" ||
                    weaponStat.category == "Carbines" ||
                    weaponStat.category == "Machine guns" ||
                    weaponStat.category == "Handheld weapons")
                {
                    if (this.statLibrary.weapons.TryGetValue(weaponStat.name, out weapon))
                    {
                        //Only handle weapons that do < 50 max dps
                        if (weapon.damage_max < 50)
                        {
                            //Only take weapons with more than 50 kills
                            if (weaponStat.kills > 50)
                            {
                                //Check for damage hack
                                if (weaponStat.dps > weapon.damage_max)
                                {
                                    //Get the percentage over normal
                                    double percDiff = (weaponStat.dps - weapon.damage_max) / weaponStat.dps;

                                    if (percDiff > (this.DPSTriggerLevel/100))
                                    {
                                        if (percDiff > actedPerc)
                                        {
                                            actedPerc = percDiff;
                                            actedWeapon = weaponStat;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        this.ConsoleError("Could not find " + weaponStat.name + " in stat library.");
                    }
                }
            }
            if (actedWeapon != null)
            {
                acted = true;
                this.ConsoleError(player.player_name + " auto-banned for damage mod. [" + actedWeapon.name + "-" + (int)actedWeapon.dps + "-" + (int)actedWeapon.kills + "-" + (int)actedWeapon.headshots + "]");
                if(!debugMode)
                {
                    //Create the ban record
                    AdKat_Record record = new AdKat_Record();
                    record.server_id = this.server_id;
                    record.command_type = AdKat_CommandType.PermabanPlayer;
                    record.command_numeric = 0;
                    record.target_name = player.player_name;
                    record.target_player = player;
                    record.source_name = "AutoAdmin";
                    record.record_message = "Hacking/Cheating DPS Automatic Ban [" + actedWeapon.name.Replace("-", "").Replace(" ", "") + "-" + (int)actedWeapon.dps + "-" + (int)actedWeapon.kills + "-" + (int)actedWeapon.headshots + "]";
                    //Process the record
                    this.queueRecordForProcessing(record);
                    this.adminSay(player.player_name + " auto-banned for damage mod. [" + actedWeapon.name + "-" + (int)actedWeapon.dps + "-" + (int)actedWeapon.kills + "-" + (int)actedWeapon.headshots + "]");
                }
            }
            return acted;
        }

        private Boolean aimbotHackCheck(AdKat_Player player, Boolean debugMode)
        {
            //Get the player's top 20 weapons
            Boolean acted = false;
            List<AdKat_WeaponStats> topWeapons = new List<AdKat_WeaponStats>();
            foreach (AdKat_WeaponStats weaponStat in player.stats.weaponStats.Values)
            {
                topWeapons.Add(weaponStat);
            }
            topWeapons.Sort(
                delegate(AdKat_WeaponStats a1, AdKat_WeaponStats a2)
                {
                    if (a1.kills == a2.kills)
                    {
                        return 0;
                    }
                    return (a1.kills < a2.kills) ? (1) : (-1);
                }
            );

            AdKat_WeaponStats actedWeapon = null;
            double actedHskr = -1;
            StatLibrary_Weapon weapon = null;
            int index = 0;
            foreach (AdKat_WeaponStats weaponStat in topWeapons)
            {
                //Break after 15 th top weapon
                if (index++ > 15) { break; }
                //Only count certain weapon categories
                if (weaponStat.category == "Sub machine guns" ||
                    weaponStat.category == "Assault rifles" ||
                    weaponStat.category == "Carbines" ||
                    weaponStat.category == "Machine guns")
                {
                    if (this.statLibrary.weapons.TryGetValue(weaponStat.name, out weapon))
                    {
                        //Only handle weapons that do < 50 max dps
                        if (weapon.damage_max < 50)
                        {
                            //Only take weapons with more than 100 kills
                            if (weaponStat.kills > 100)
                            {
                                //Check for aimbot hack
                                if (weaponStat.hskr > (this.HSKTriggerLevel/100))
                                {
                                    if (weaponStat.hskr > actedHskr)
                                    {
                                        actedHskr = weaponStat.hskr;
                                        actedWeapon = weaponStat;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        this.ConsoleError("Could not find " + weaponStat.name + " in stat library.");
                    }
                }
            }
            if (actedWeapon != null)
            {
                acted = true;
                this.ConsoleError(player.player_name + " auto-banned for aimbot. [" + actedWeapon.name + "-" + (int)(actedWeapon.hskr * 100) + "-" + (int)actedWeapon.kills + "-" + (int)actedWeapon.headshots + "]");
                if (!debugMode)
                {
                    //Create the ban record
                    AdKat_Record record = new AdKat_Record();
                    record.server_id = this.server_id;
                    record.command_type = AdKat_CommandType.PermabanPlayer;
                    record.command_numeric = 0;
                    record.target_name = player.player_name;
                    record.target_player = player;
                    record.source_name = "AutoAdmin";
                    record.record_message = "Hacking/Cheating HSK Automatic Ban [" + actedWeapon.name.Replace("-", "").Replace(" ", "") + "-" + (int)(actedWeapon.hskr * 100) + "-" + (int)actedWeapon.kills + "-" + (int)actedWeapon.headshots + "]";
                    //Process the record
                    this.queueRecordForProcessing(record);
                    this.adminSay(player.player_name + " auto-banned for aimbot. [" + actedWeapon.name + "-" + (int)actedWeapon.hskr + "-" + (int)actedWeapon.kills + "-" + (int)actedWeapon.headshots + "]");
                }
            }
            return acted;
        }

        #endregion

        #region Messaging
        //all messaging is redirected to global chat for analysis
        public override void OnGlobalChat(string speaker, string message)
        {
            this.DebugWrite("Entering ", 7);
            try
            {
                if (isEnabled)
                {
                    //Performance testing area
                    if (speaker == this.debugSoldierName)
                    {
                        this.commandStartTime = DateTime.Now;
                    }
                    //Only queue the message for parsing if it's from a player
                    if (!speaker.Equals("Server"))
                    {
                        this.queueMessageForParsing(speaker, message);
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while processing inbound chat messages.", e));
            }
            this.DebugWrite("Exiting ", 7);
        }
        public override void OnTeamChat(string speaker, string message, int teamId) { this.OnGlobalChat(speaker, message); }
        public override void OnSquadChat(string speaker, string message, int teamId, int squadId) { this.OnGlobalChat(speaker, message); }

        public string sendMessageToSource(AdKat_Record record, string message)
        {
            this.DebugWrite("Entering sendMessageToSource", 7);
            string returnMessage = null;
            try
            {
                switch (record.command_source)
                {
                    case AdKat_CommandSource.InGame:
                        if (!String.IsNullOrEmpty(message))
                        {
                            this.playerSayMessage(record.source_name, message);
                        }
                        else
                        {
                            this.ConsoleError("message null in sendMessageToSource");
                        }
                        break;
                    case AdKat_CommandSource.Console:
                        this.ConsoleWrite(message);
                        break;
                    case AdKat_CommandSource.Settings:
                        this.ConsoleWrite(message);
                        break;
                    case AdKat_CommandSource.Database:
                        //Do nothing, no way to communicate to source when database
                        break;
                    case AdKat_CommandSource.HTTP:
                        returnMessage = message;
                        break;
                    default:
                        //this.ConsoleWarn("Command source not set, or not recognized.");
                        break;
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while sending message to record source.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting sendMessageToSource", 7);
            return returnMessage;
        }

        public void playerSayMessage(string target, string message)
        {
            this.DebugWrite("Entering playerSayMessage", 7);
            try
            {
                if (String.IsNullOrEmpty(target) || String.IsNullOrEmpty(message))
                {
                    this.ConsoleError("target or message null in playerSayMessage");
                    return;
                }
                ExecuteCommand("procon.protected.send", "admin.say", message, "player", target);
                ExecuteCommand("procon.protected.chat.write", string.Format("(PlayerSay {0}) ", target) + message);
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while sending message to player.", e));
            }
            this.DebugWrite("Exiting playerSayMessage", 7);
        }

        public void adminSay(String message)
        {
            this.DebugWrite("Entering adminSay", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    this.ConsoleError("message null in adminSay");
                    return;
                }
                this.ExecuteCommand("procon.protected.send", "admin.say", message, "all");
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while sending admin say.", e));
            }
            this.DebugWrite("Exiting adminSay", 7);
        }

        public void adminYell(String message)
        {
            this.DebugWrite("Entering adminYell", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    this.ConsoleError("message null in adminYell");
                    return;
                }
                this.ExecuteCommand("procon.protected.send", "admin.yell", message, this.yellDuration + "", "all");
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while sending admin yell.", e));
            }
            this.DebugWrite("Exiting adminYell", 7);
        }

        private void queueMessageForParsing(string speaker, string message)
        {
            this.DebugWrite("Entering queueMessageForParsing", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to queue message for parsing", 6);
                    lock (unparsedMessageMutex)
                    {
                        this.unparsedMessageQueue.Enqueue(new KeyValuePair<String, String>(speaker, message));
                        this.DebugWrite("Message queued for parsing.", 6);
                        this.messageParsingHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing chat message for parsing.", e));
            }
            this.DebugWrite("Exiting queueMessageForParsing", 7);
        }

        private void queueCommandForParsing(string speaker, string command)
        {
            this.DebugWrite("Entering queueCommandForParsing", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to queue command for parsing", 6);
                    lock (unparsedCommandMutex)
                    {
                        this.unparsedCommandQueue.Enqueue(new KeyValuePair<String, String>(speaker, command));
                        this.DebugWrite("Command sent to unparsed commands.", 6);
                        this.commandParsingHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing command for parsing.", e));
            }
            this.DebugWrite("Exiting queueCommandForParsing", 7);
        }

        private void messagingThreadLoop()
        {
            try
            {
                this.DebugWrite("MESSAGE: Starting Messaging Thread", 2);
                Thread.CurrentThread.Name = "messaging";
                while (true)
                {
                    this.DebugWrite("MESSAGE: Entering Messaging Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("MESSAGE: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Get all unparsed inbound messages
                    Queue<KeyValuePair<String, String>> inboundMessages;
                    if (this.unparsedMessageQueue.Count > 0)
                    {
                        this.DebugWrite("MESSAGE: Preparing to lock messaging to retrive new messages", 7);
                        lock (unparsedMessageMutex)
                        {
                            this.DebugWrite("MESSAGE: Inbound messages found. Grabbing.", 6);
                            //Grab all messages in the queue
                            inboundMessages = new Queue<KeyValuePair<string, string>>(this.unparsedMessageQueue.ToArray());
                            //Clear the queue for next run
                            this.unparsedMessageQueue.Clear();
                        }
                    }
                    else
                    {
                        this.DebugWrite("MESSAGE: No inbound messages. Waiting for Input.", 4);
                        //Wait for input
                        this.messageParsingHandle.Reset();
                        this.messageParsingHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }

                    //Loop through all messages in order that they came in
                    while (inboundMessages != null && inboundMessages.Count > 0)
                    {
                        this.DebugWrite("MESSAGE: begin reading message", 6);
                        //Dequeue the first/next message
                        KeyValuePair<String, String> messagePair = inboundMessages.Dequeue();
                        string speaker = messagePair.Key;
                        string message = messagePair.Value;

                        //check for player mute case
                        //ignore if it's a server call
                        if (speaker != "Server")
                        {
                            lock (playersMutex)
                            {
                                //Check if the player is muted
                                this.DebugWrite("MESSAGE: Checking for mute case.", 7);
                                if (this.round_mutedPlayers.ContainsKey(speaker))
                                {
                                    this.DebugWrite("MESSAGE: Player is muted. Acting.", 7);
                                    //Increment the muted chat count
                                    this.round_mutedPlayers[speaker] = this.round_mutedPlayers[speaker] + 1;
                                    //Create record
                                    AdKat_Record record = new AdKat_Record();
                                    record.command_source = AdKat_CommandSource.InGame;
                                    record.server_id = this.server_id;
                                    record.source_name = "PlayerMuteSystem";
                                    record.target_player = this.playerDictionary[speaker];
                                    record.target_name = record.target_player.player_name;
                                    if (this.round_mutedPlayers[speaker] > this.mutedPlayerChances)
                                    {
                                        record.record_message = this.mutedPlayerKickMessage;
                                        record.command_type = AdKat_CommandType.KickPlayer;
                                        record.command_action = AdKat_CommandType.KickPlayer;
                                    }
                                    else
                                    {
                                        record.record_message = mutedPlayerKillMessage;
                                        record.command_type = AdKat_CommandType.KillPlayer;
                                        record.command_action = AdKat_CommandType.KillPlayer;
                                    }

                                    this.queueRecordForProcessing(record);
                                    continue;
                                }
                            }
                        }

                        //Check if the message is a command
                        if (message.StartsWith("@") || message.StartsWith("!"))
                        {
                            message = message.Substring(1);
                        }
                        else if (message.StartsWith("/@") || message.StartsWith("/!"))
                        {
                            message = message.Substring(2);
                        }
                        else if (message.StartsWith("/"))
                        {
                            message = message.Substring(1);
                        }
                        else
                        {
                            //If the message does not cause either of the above clauses, then ignore it.
                            this.DebugWrite("MESSAGE: Message is regular chat. Ignoring.", 7);
                            continue;
                        }
                        this.queueCommandForParsing(speaker, message);
                    }
                }
                this.DebugWrite("MESSAGE: Ending Messaging Thread", 2);
            }
            catch (Exception e)
            {
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.HandleException(new AdKat_Exception("Messaging thread aborted. Attempting to restart.", e));
                    this.DebugWrite("Thread Exception", 4);
                    Thread.ResetAbort();
                    return;
                }
                else
                {
                    this.HandleException(new AdKat_Exception("Error occured in messaging thread.", e));
                }
            }
        }

        #endregion

        #region Teamswap Methods

        private void queuePlayerForForceMove(CPlayerInfo player)
        {
            this.DebugWrite("Entering queuePlayerForForceMove", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to queue player for TeamSwap ", 6);
                    lock (teamswapMutex)
                    {
                        this.teamswapForceMoveQueue.Enqueue(player);
                        this.teamswapHandle.Set();
                        this.DebugWrite("Player queued for TeamSwap", 6);
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing player for force-move.", e));
            }
            this.DebugWrite("Exiting queuePlayerForForceMove", 7);
        }

        private void queuePlayerForMove(CPlayerInfo player)
        {
            this.DebugWrite("Entering queuePlayerForMove", 7);
            try
            {
                if (this.isEnabled)
                {
                    this.DebugWrite("Preparing to add player to 'on-death' move dictionary.", 6);
                    lock (teamswapMutex)
                    {
                        if (!this.teamswapOnDeathMoveDic.ContainsKey(player.SoldierName))
                        {
                            this.teamswapOnDeathMoveDic.Add(player.SoldierName, player);
                            this.teamswapHandle.Set();
                            this.DebugWrite("Player added to 'on-death' move dictionary.", 6);
                        }
                        else
                        {
                            this.DebugWrite("Player already in 'on-death' move dictionary.", 6);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing player for move.", e));
            }
            this.DebugWrite("Exiting queuePlayerForMove", 7);
        }

        //runs through both team swap queues and performs the swapping
        public void teamswapThreadLoop()
        {
            //assume the max player count per team is 32 if no server info has been provided
            int maxPlayerCount = 32;
            Queue<CPlayerInfo> checkingQueue;
            Queue<CPlayerInfo> movingQueue;
            try
            {
                this.DebugWrite("TSWAP: Starting TeamSwap Thread", 2);
                Thread.CurrentThread.Name = "teamswap";
                while (true)
                {
                    this.DebugWrite("TSWAP: Entering TeamSwap Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("TSWAP: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Call List Players
                    this.listPlayersHandle.Reset();
                    this.ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
                    //Wait for listPlayers to finish
                    if (!this.listPlayersHandle.WaitOne(10000))
                    {
                        this.DebugWrite("ListPlayers ran out of time for TeamSwap. 10 sec.", 1);
                    }

                    //Refresh Max Player Count, needed for responsive server size
                    CServerInfo info = this.getServerInfo();
                    if (info != null && info.MaxPlayerCount != maxPlayerCount)
                    {
                        maxPlayerCount = info.MaxPlayerCount / 2;
                    }

                    //Get players who died that need moving
                    if ((this.teamswapOnDeathMoveDic.Count > 0 && this.teamswapOnDeathCheckingQueue.Count > 0) || this.teamswapForceMoveQueue.Count > 0)
                    {
                        this.DebugWrite("TSWAP: Preparing to lock TeamSwap queues", 4);
                        lock (teamswapMutex)
                        {
                            this.DebugWrite("TSWAP: Players in ready for TeamSwap. Grabbing.", 6);
                            //Grab all messages in the queue
                            movingQueue = new Queue<CPlayerInfo>(this.teamswapForceMoveQueue.ToArray());
                            checkingQueue = new Queue<CPlayerInfo>(this.teamswapOnDeathCheckingQueue.ToArray());
                            //Clear the queue for next run
                            this.teamswapOnDeathCheckingQueue.Clear();
                            this.teamswapForceMoveQueue.Clear();

                            //Check for "on-death" move players
                            while (this.teamswapOnDeathMoveDic.Count > 0 && checkingQueue != null && checkingQueue.Count > 0)
                            {
                                //Dequeue the first/next player
                                String playerName = checkingQueue.Dequeue().SoldierName;
                                CPlayerInfo player;
                                //If they are 
                                if (this.teamswapOnDeathMoveDic.TryGetValue(playerName, out player))
                                {
                                    //Player has died, remove from the dictionary
                                    this.teamswapOnDeathMoveDic.Remove(playerName);
                                    //Add to move queue
                                    movingQueue.Enqueue(player);
                                }
                            }

                            while (movingQueue != null && movingQueue.Count > 0)
                            {
                                CPlayerInfo player = movingQueue.Dequeue();
                                if (player.TeamID == USTeamID)
                                {
                                    if (!this.containsCPlayerInfo(this.USMoveQueue, player.SoldierName))
                                    {
                                        this.USMoveQueue.Enqueue(player);
                                        this.playerSayMessage(player.SoldierName, "You have been added to the (US -> RU) TeamSwap queue in position " + (this.indexOfCPlayerInfo(this.USMoveQueue, player.SoldierName) + 1) + ".");
                                    }
                                    else
                                    {
                                        this.playerSayMessage(player.SoldierName, "RU Team Full (" + this.RUPlayerCount + "/" + maxPlayerCount + "). You are in queue position " + (this.indexOfCPlayerInfo(this.USMoveQueue, player.SoldierName) + 1));
                                    }
                                }
                                else
                                {
                                    if (!this.containsCPlayerInfo(this.RUMoveQueue, player.SoldierName))
                                    {
                                        this.RUMoveQueue.Enqueue(player);
                                        this.playerSayMessage(player.SoldierName, "You have been added to the (RU -> US) TeamSwap queue in position " + (this.indexOfCPlayerInfo(this.RUMoveQueue, player.SoldierName) + 1) + ".");
                                    }
                                    else
                                    {
                                        this.playerSayMessage(player.SoldierName, "US Team Full (" + this.USPlayerCount + "/" + maxPlayerCount + "). You are in queue position " + (this.indexOfCPlayerInfo(this.RUMoveQueue, player.SoldierName) + 1));
                                    }
                                }
                            }
                        }
                    }
                    this.DebugWrite("Team Info: US:" + this.USPlayerCount + "/" + maxPlayerCount + " RU:" + this.RUPlayerCount + "/" + maxPlayerCount, 5);
                    if (this.RUMoveQueue.Count > 0 || this.USMoveQueue.Count > 0)
                    {
                        //Perform player moving
                        Boolean movedPlayer;
                        do
                        {
                            movedPlayer = false;
                            if (this.RUMoveQueue.Count > 0)
                            {
                                if (this.USPlayerCount < maxPlayerCount)
                                {
                                    CPlayerInfo player = this.RUMoveQueue.Dequeue();
                                    AdKat_Player dicPlayer = null;
                                    if (this.playerDictionary.TryGetValue(player.SoldierName, out dicPlayer))
                                    {
                                        if (dicPlayer.frostbitePlayerInfo.TeamID == USTeamID)
                                        {
                                            //Skip the kill/swap if they are already on the goal team by some other means
                                            continue;
                                        }
                                    }
                                    if (String.IsNullOrEmpty(player.SoldierName) || String.IsNullOrEmpty(USTeamID.ToString()))
                                    {
                                        this.ConsoleError("soldiername null in US teamswap");
                                    }
                                    else
                                    {
                                        ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, USTeamID.ToString(), "1", "true");
                                    }
                                    this.playerSayMessage(player.SoldierName, "Swapping you from team RU to team US");
                                    movedPlayer = true;
                                }
                            }
                            if (this.USMoveQueue.Count > 0)
                            {
                                if (this.RUPlayerCount < maxPlayerCount)
                                {
                                    CPlayerInfo player = this.USMoveQueue.Dequeue();
                                    AdKat_Player dicPlayer = null;
                                    if (this.playerDictionary.TryGetValue(player.SoldierName, out dicPlayer))
                                    {
                                        if (dicPlayer.frostbitePlayerInfo.TeamID == RUTeamID)
                                        {
                                            //Skip the kill/swap if they are already on the goal team by some other means
                                            continue;
                                        }
                                    }
                                    if (String.IsNullOrEmpty(player.SoldierName) || String.IsNullOrEmpty(RUTeamID.ToString()))
                                    {
                                        this.ConsoleError("soldiername null in RU teamswap");
                                    }
                                    else
                                    {
                                        ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, RUTeamID.ToString(), "1", "true");
                                    }
                                    this.playerSayMessage(player.SoldierName, "Swapping you from team US to team RU");
                                    movedPlayer = true;
                                }
                            }
                        } while (movedPlayer);

                        //Sleep for 5 seconds
                        Thread.Sleep(5000);
                    }
                    else
                    {
                        this.DebugWrite("TSWAP: No players to swap. Waiting for input.", 4);
                        //There are no players to swap, wait.
                        this.teamswapHandle.Reset();
                        this.teamswapHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }
                }
                this.DebugWrite("TSWAP: Ending TeamSwap Thread", 2);
            }
            catch (Exception e)
            {
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.HandleException(new AdKat_Exception("Teamswap thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                    return;
                }
                else
                {
                    this.HandleException(new AdKat_Exception("Error occured in teamswap thread.", e));
                }
            }
        }

        //Whether a move queue contains a given player
        private bool containsCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            this.DebugWrite("Entering containsCPlayerInfo", 7);
            try
            {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 index = 0; index < queueList.Count; index++)
                {
                    if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while checking for player in teamswap queue.", e));
            }
            this.DebugWrite("Exiting containsCPlayerInfo", 7);
            return false;
        }

        //Helper method to find a player's information in the move queue
        private CPlayerInfo getCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            this.DebugWrite("Entering getCPlayerInfo", 7);
            CPlayerInfo playerInfo = null;
            try
            {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 index = 0; index < queueList.Count; index++)
                {
                    if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                    {
                        playerInfo = ((CPlayerInfo)playerArray[index]);
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while getting player info from teamswap queue.", e));
            }
            this.DebugWrite("Exiting getCPlayerInfo", 7);
            return playerInfo;
        }

        //The index of a player in the move queue
        private Int32 indexOfCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            this.DebugWrite("Entering getCPlayerInfo", 7);
            int playerIndex = -1;
            try
            {
                CPlayerInfo[] playerArray = queueList.ToArray();
                for (Int32 i = 0; i < queueList.Count; i++)
                {
                    if (((CPlayerInfo)playerArray[i]).SoldierName.Equals(player))
                    {
                        return playerIndex;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while getting index of player in teamswap queue.", e));
            }
            this.DebugWrite("Exiting getCPlayerInfo", 7);
            return playerIndex;
        }

        #endregion

        #region Record Creation and Processing

        private void queueRecordForProcessing(AdKat_Record record)
        {
            this.DebugWrite("Entering queueRecordForProcessing", 7);
            try
            {
                this.DebugWrite("Preparing to queue record for processing", 6);
                lock (unprocessedRecordMutex)
                {
                    //Queue the record for processing
                    this.unprocessedRecordQueue.Enqueue(record);
                    this.DebugWrite("Record queued for processing", 6);
                    this.dbCommHandle.Set();
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while queueing record for processing.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting queueRecordForProcessing", 7);
        }

        private void commandParsingThreadLoop()
        {
            try
            {
                this.DebugWrite("COMMAND: Starting Command Parsing Thread", 2);
                Thread.CurrentThread.Name = "Command";
                while (true)
                {
                    this.DebugWrite("COMMAND: Entering Command Parsing Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("COMMAND: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Get all unparsed inbound messages
                    Queue<KeyValuePair<String, String>> unparsedCommands;
                    if (this.unparsedCommandQueue.Count > 0)
                    {
                        this.DebugWrite("COMMAND: Preparing to lock command queue to retrive new commands", 7);
                        lock (unparsedCommandMutex)
                        {
                            this.DebugWrite("COMMAND: Inbound commands found. Grabbing.", 6);
                            //Grab all messages in the queue
                            unparsedCommands = new Queue<KeyValuePair<string, string>>(this.unparsedCommandQueue.ToArray());
                            //Clear the queue for next run
                            this.unparsedCommandQueue.Clear();
                        }

                        //Loop through all commands in order that they came in
                        while (unparsedCommands != null && unparsedCommands.Count > 0)
                        {
                            this.DebugWrite("COMMAND: begin reading command", 6);
                            //Dequeue the first/next command
                            KeyValuePair<String, String> commandPair = unparsedCommands.Dequeue();
                            string speaker = commandPair.Key;
                            string command = commandPair.Value;

                            AdKat_Record record = new AdKat_Record();
                            record.command_source = AdKat_CommandSource.InGame;
                            record.source_name = speaker;
                            //Complete the record creation
                            this.completeRecord(record, command);
                        }
                    }
                    else
                    {
                        this.DebugWrite("COMMAND: No inbound commands, ready.", 7);
                        //No commands to parse, ready.
                        this.commandParsingHandle.Reset();
                        this.commandParsingHandle.WaitOne(Timeout.Infinite);
                        continue;
                    }
                }
                this.DebugWrite("COMMAND: Ending Command Thread", 2);
            }
            catch (Exception e)
            {
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.HandleException(new AdKat_Exception("Command parsing thread aborted. Attempting to restart.", e));
                    this.DebugWrite("COMMAND: Thread Exception", 4);
                    Thread.ResetAbort();
                    return;
                }
                else
                {
                    this.HandleException(new AdKat_Exception("Error occured in command parsing thread.", e));
                }
            }
        }

        //Before calling this, the record is initialized, and command_source/source_name are filled
        public void completeRecord(AdKat_Record record, String message)
        {
            try
            {
                //Initial split of command by whitespace
                String[] splitMessage = message.Split(' ');
                if (splitMessage.Length < 1)
                {
                    this.DebugWrite("Completely blank command entered", 5);
                    this.sendMessageToSource(record, "You entered a completely blank command.");
                    return;
                }
                String commandString = splitMessage[0].ToLower();
                DebugWrite("Raw Command: " + commandString, 6);
                String remainingMessage = message.TrimStart(splitMessage[0].ToCharArray()).Trim();

                //GATE 1: Add general data
                record.server_id = this.server_id;
                record.record_time = DateTime.Now;

                //GATE 2: Add Command
                AdKat_CommandType commandType = this.getCommand(commandString);
                if (commandType == AdKat_CommandType.Default)
                {
                    //If command not parsable, return without creating
                    DebugWrite("Command not parsable", 6);
                    return;
                }
                record.command_type = commandType;
                record.command_action = commandType;
                DebugWrite("Command type: " + record.command_type, 6);

                //GATE 3: Check Access Rights
                //Check for server command case
                if (record.source_name == "server")
                {
                    record.source_name = "PRoConAdmin";
                    record.command_source = AdKat_CommandSource.Console;
                }
                //Check if player has the right to perform what he's asking, only perform for InGame actions
                else if (record.command_source == AdKat_CommandSource.InGame && !this.hasAccess(record.source_name, record.command_type))
                {
                    DebugWrite("No rights to call command", 6);
                    this.sendMessageToSource(record, "Cannot use class " + this.AdKat_CommandAccessRank[record.command_type] + " command, " + record.command_type + ". You are access class " + this.getAccessLevel(record.source_name) + ".");
                    //Return without creating if player doesn't have rights to do it
                    return;
                }

                //GATE 4: Add specific data based on command type.
                //Items that need filling before record processing:
                //target_name
                //target_guid
                //target_playerInfo
                //record_message
                switch (record.command_type)
                {
                    #region MovePlayer
                    case AdKat_CommandType.MovePlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.record_message = "MovePlayer";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region ForceMovePlayer
                    case AdKat_CommandType.ForceMovePlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.record_message = "ForceMovePlayer";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region Teamswap
                    case AdKat_CommandType.Teamswap:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //May only call this command from in-game
                            if (record.command_source != AdKat_CommandSource.InGame)
                            {
                                this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                break;
                            }
                            record.record_message = "TeamSwap";
                            record.target_name = record.source_name;
                            this.completeTargetInformation(record, false);
                        }
                        break;
                    #endregion
                    #region KillSelf
                    case AdKat_CommandType.KillSelf:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //May only call this command from in-game
                            if (record.command_source != AdKat_CommandSource.InGame)
                            {
                                this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                break;
                            }
                            record.record_message = "Self-Inflicted";
                            record.target_name = record.source_name;
                            this.completeTargetInformation(record, false);
                        }
                        break;
                    #endregion
                    #region KillPlayer
                    case AdKat_CommandType.KillPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        if (record.record_message.Length >= this.requiredReasonLength)
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                        else
                                        {
                                            this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        }
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region KickPlayer
                    case AdKat_CommandType.KickPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        if (record.record_message.Length >= this.requiredReasonLength)
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                        else
                                        {
                                            this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        }
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region TempBanPlayer
                    case AdKat_CommandType.TempBanPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 3);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    break;
                                case 1:
                                    int record_duration = 0;
                                    DebugWrite("Raw Duration: " + parameters[0], 6);
                                    if (!Int32.TryParse(parameters[0], out record_duration))
                                    {
                                        this.sendMessageToSource(record, "Invalid time given, unable to submit.");
                                        return;
                                    }
                                    record.command_numeric = record_duration;
                                    //Target is source
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 2:
                                    DebugWrite("Raw Duration: " + parameters[0], 6);
                                    if (Int32.TryParse(parameters[0], out record_duration))
                                    {
                                        record.command_numeric = record_duration;

                                        record.target_name = parameters[1];
                                        DebugWrite("target: " + record.target_name, 6);

                                        //Handle based on report ID as only option
                                        if (!this.handleRoundReport(record))
                                        {
                                            this.sendMessageToSource(record, "No reason given, unable to submit.");
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Invalid time given, unable to submit.");
                                    }
                                    break;
                                case 3:
                                    DebugWrite("Raw Duration: " + parameters[0], 6);
                                    if (Int32.TryParse(parameters[0], out record_duration))
                                    {
                                        record.command_numeric = record_duration;

                                        record.target_name = parameters[1];
                                        DebugWrite("target: " + record.target_name, 6);

                                        //attempt to handle via pre-message ID
                                        record.record_message = this.getPreMessage(parameters[2], this.requirePreMessageUse);
                                        if (record.record_message == null)
                                        {
                                            this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                            break;
                                        }

                                        DebugWrite("reason: " + record.record_message, 6);

                                        //Handle based on report ID if possible
                                        if (!this.handleRoundReport(record))
                                        {
                                            if (record.record_message.Length >= this.requiredReasonLength)
                                            {
                                                this.completeTargetInformation(record, false);
                                            }
                                            else
                                            {
                                                this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Invalid time given, unable to submit.");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region PermabanPlayer
                    case AdKat_CommandType.PermabanPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        if (record.record_message.Length >= this.requiredReasonLength)
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                        else
                                        {
                                            this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        }
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region PunishPlayer
                    case AdKat_CommandType.PunishPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        if (record.record_message.Length >= this.requiredReasonLength)
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                        else
                                        {
                                            this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        }
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region ForgivePlayer
                    case AdKat_CommandType.ForgivePlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        if (record.record_message.Length >= this.requiredReasonLength)
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                        else
                                        {
                                            this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        }
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region MutePlayer
                    case AdKat_CommandType.MutePlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], this.requirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                        break;
                                    }

                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        if (record.record_message.Length >= this.requiredReasonLength)
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                        else
                                        {
                                            this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        }
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region JoinPlayer
                    case AdKat_CommandType.JoinPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "foreveralone.jpg");
                                    return;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Joining Player";
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region RoundWhitelistPlayer
                    case AdKat_CommandType.RoundWhitelistPlayer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.command_source != AdKat_CommandSource.InGame)
                                    {
                                        this.sendMessageToSource(record, "You can't use a self-inflicting command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    this.completeTargetInformation(record, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!this.handleRoundReport(record))
                                    {
                                        this.sendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], false);

                                    //Handle based on report ID if possible
                                    if (!this.handleRoundReport(record))
                                    {
                                        if (record.record_message.Length >= this.requiredReasonLength)
                                        {
                                            this.completeTargetInformation(record, false);
                                        }
                                        else
                                        {
                                            this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        }
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region ReportPlayer
                    case AdKat_CommandType.ReportPlayer:
                        {
                            string command = this.reportPlayerCommandText;

                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                    break;
                                case 1:
                                    this.sendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                    break;
                                case 2:
                                    record.target_name = parameters[0];
                                    DebugWrite("target: " + record.target_name, 6);

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], false);

                                    DebugWrite("reason: " + record.record_message, 6);

                                    //Only 1 character reasons are required for reports and admin calls
                                    if (record.record_message.Length >= 1)
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    else
                                    {
                                        DebugWrite("reason too short", 6);
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        return;
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region CallAdmin
                    case AdKat_CommandType.CallAdmin:
                        {
                            string command = this.callAdminCommandText;

                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                    break;
                                case 1:
                                    this.sendMessageToSource(record, "Format must be: @" + command + " playername reason");
                                    break;
                                case 2:
                                    record.target_name = parameters[0];
                                    DebugWrite("target: " + record.target_name, 6);

                                    //attempt to handle via pre-message ID
                                    record.record_message = this.getPreMessage(parameters[1], false);

                                    DebugWrite("reason: " + record.record_message, 6);
                                    //Only 1 character reasons are required for reports and admin calls
                                    if (record.record_message.Length >= 1)
                                    {
                                        this.completeTargetInformation(record, false);
                                    }
                                    else
                                    {
                                        DebugWrite("reason too short", 6);
                                        this.sendMessageToSource(record, "Reason too short, unable to submit.");
                                        return;
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region NukeServer
                    case AdKat_CommandType.NukeServer:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    break;
                                case 1:
                                    string targetTeam = parameters[0];
                                    record.record_message = "Nuke Server";
                                    DebugWrite("target: " + targetTeam, 6);
                                    if (targetTeam.ToLower().Contains("us"))
                                    {
                                        record.target_name = "US Team";
                                        record.record_message += " (US Team)";
                                    }
                                    else if (targetTeam.ToLower().Contains("ru"))
                                    {
                                        record.target_name = "RU Team";
                                        record.record_message += " (RU Team)";
                                    }
                                    else if (targetTeam.ToLower().Contains("all"))
                                    {
                                        record.target_name = "Everyone";
                                        record.record_message += " (Everyone)";
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Use 'US', 'RU', or 'ALL' as targets.");
                                    }
                                    //Have the admin confirm the action
                                    this.confirmActionWithSource(record);
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region KickAll
                    case AdKat_CommandType.KickAll:
                        this.cancelSourcePendingAction(record);
                        record.target_name = "Non-Admins";
                        record.record_message = "Kick All Players";
                        this.confirmActionWithSource(record);
                        break;
                    #endregion
                    #region EndLevel
                    case AdKat_CommandType.EndLevel:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    string targetTeam = parameters[0];
                                    DebugWrite("target team: " + targetTeam, 6);
                                    record.record_message = "End Round";
                                    if (targetTeam.ToLower().Contains("us"))
                                    {
                                        record.target_name = "US Team";
                                        record.command_numeric = AdKats.USTeamID;
                                        record.record_message += " (US Win)";
                                    }
                                    else if (targetTeam.ToLower().Contains("ru"))
                                    {
                                        record.target_name = "RU Team";
                                        record.command_numeric = AdKats.RUTeamID;
                                        record.record_message += " (RU Win)";
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, "Use 'US' or 'RU' as team names to end round");
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                            //Have the admin confirm the action
                            this.confirmActionWithSource(record);
                        }
                        break;
                    #endregion
                    #region RestartLevel
                    case AdKat_CommandType.RestartLevel:
                        this.cancelSourcePendingAction(record);
                        record.target_name = "Server";
                        record.record_message = "Restart Round";
                        this.confirmActionWithSource(record);
                        break;
                    #endregion
                    #region NextLevel
                    case AdKat_CommandType.NextLevel:
                        this.cancelSourcePendingAction(record);
                        record.target_name = "Server";
                        record.record_message = "Run Next Map";
                        this.confirmActionWithSource(record);
                        break;
                    #endregion
                    #region WhatIs
                    case AdKat_CommandType.WhatIs:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    record.record_message = this.getPreMessage(parameters[0], true);
                                    if (record.record_message == null)
                                    {
                                        this.sendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + this.preMessageList.Count);
                                    }
                                    else
                                    {
                                        this.sendMessageToSource(record, record.record_message);
                                    }
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                            //This type is not processed
                        }
                        break;
                    #endregion
                    #region Voip
                    case AdKat_CommandType.Voip:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);
                            
                            //Send them voip information
                            this.sendMessageToSource(record, this.serverVoipAddress);
                        }
                        break;
                    #endregion
                    #region AdminSay
                    case AdKat_CommandType.AdminSay:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    record.record_message = this.getPreMessage(parameters[0], false);
                                    DebugWrite("message: " + record.record_message, 6);
                                    record.target_name = "Server";
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                            this.queueRecordForProcessing(record);
                        }
                        break;
                    #endregion
                    #region AdminYell
                    case AdKat_CommandType.AdminYell:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    record.record_message = this.getPreMessage(parameters[0], false).ToUpper();
                                    DebugWrite("message: " + record.record_message, 6);
                                    record.target_name = "Server";
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                            this.queueRecordForProcessing(record);
                        }
                        break;
                    #endregion
                    #region PlayerSay
                    case AdKat_CommandType.PlayerSay:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    this.sendMessageToSource(record, "No message given, unable to submit.");
                                    return;
                                case 2:
                                    record.target_name = parameters[0];
                                    DebugWrite("target: " + record.target_name, 6);

                                    record.record_message = this.getPreMessage(parameters[1], false);
                                    DebugWrite("message: " + record.record_message, 6);

                                    this.completeTargetInformation(record, false);
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region PlayerYell
                    case AdKat_CommandType.PlayerYell:
                        {
                            //Remove previous commands awaiting confirmation
                            this.cancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = this.parseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    this.sendMessageToSource(record, "No parameters given, unable to submit.");
                                    return;
                                case 1:
                                    this.sendMessageToSource(record, "No message given, unable to submit.");
                                    return;
                                case 2:
                                    record.target_name = parameters[0];
                                    DebugWrite("target: " + record.target_name, 6);

                                    record.record_message = this.getPreMessage(parameters[1], false).ToUpper();
                                    DebugWrite("message: " + record.record_message, 6);

                                    this.completeTargetInformation(record, false);
                                    break;
                                default:
                                    this.sendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    return;
                            }
                        }
                        break;
                    #endregion
                    #region ConfirmCommand
                    case AdKat_CommandType.ConfirmCommand:
                        this.DebugWrite("attempting to confirm command", 6);
                        AdKat_Record recordAttempt = null;
                        this.actionConfirmDic.TryGetValue(record.source_name, out recordAttempt);
                        if (recordAttempt != null)
                        {
                            this.DebugWrite("command found, calling processing", 6);
                            this.actionConfirmDic.Remove(record.source_name);
                            this.queueRecordForProcessing(recordAttempt);
                        }
                        else
                        {
                            this.DebugWrite("no command to confirm", 6);
                            this.sendMessageToSource(record, "No command to confirm.");
                        }
                        //This type is not processed
                        break;
                    #endregion
                    #region CancelCommand
                    case AdKat_CommandType.CancelCommand:
                        this.DebugWrite("attempting to cancel command", 6);
                        if (!this.actionConfirmDic.Remove(record.source_name))
                        {
                            this.DebugWrite("no command to cancel", 6);
                            this.sendMessageToSource(record, "No command to cancel.");
                        }
                        //This type is not processed
                        break;
                    #endregion
                    default:
                        break;
                }
                return;
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error occured while completing record information.", e));
            }
        }

        public string completeTargetInformation(AdKat_Record record, Boolean requireConfirm)
        {
            try
            {
                lock (playersMutex)
                {
                    this.ConsoleError("Completing target info");
                    //Check for an exact match
                    if (playerDictionary.ContainsKey(record.target_name))
                    {
                        //Exact player match, call processing without confirmation
                        record.target_player = this.playerDictionary[record.target_name];
                        record.target_name = record.target_player.player_name;
                        if (!requireConfirm)
                        {
                            //Process record right away
                            this.queueRecordForProcessing(record);
                        }
                        else
                        {
                            this.confirmActionWithSource(record);
                        }
                    }
                    else
                    {
                        //Get all substring matches
                        Converter<String, List<AdKat_Player>> ExactNameMatches = delegate(String sub)
                        {
                            List<AdKat_Player> matches = new List<AdKat_Player>();
                            if (String.IsNullOrEmpty(sub)) return matches;
                            foreach (AdKat_Player player in this.playerDictionary.Values)
                            {
                                if (Regex.Match(player.player_name, sub, RegexOptions.IgnoreCase).Success)
                                {
                                    matches.Add(player);
                                }
                            }
                            return matches;
                        };
                        List<AdKat_Player> substringMatches = ExactNameMatches(record.target_name);
                        if (substringMatches.Count == 1)
                        {
                            //Only one substring match, call processing without confirmation if able
                            record.target_player = substringMatches[0];
                            record.target_name = record.target_player.player_name;
                            if (!requireConfirm)
                            {
                                //Process record right away
                                this.queueRecordForProcessing(record);
                            }
                            else
                            {
                                this.confirmActionWithSource(record);
                            }
                        }
                        else if (substringMatches.Count > 1)
                        {
                            //Multiple players matched the query, choose correct one
                            string msg = "'" + record.target_name + "' matches multiple players: ";
                            bool first = true;
                            AdKat_Player suggestion = null;
                            foreach (AdKat_Player player in substringMatches)
                            {
                                if (first)
                                {
                                    msg = msg + player.player_name;
                                    first = false;
                                }
                                else
                                {
                                    msg = msg + ", " + player.player_name;
                                }
                                //Suggest player names that start with the text admins entered over others
                                if (player.player_name.ToLower().StartsWith(record.target_name.ToLower()))
                                {
                                    suggestion = player;
                                }
                            }
                            if (suggestion == null)
                            {
                                //If no player name starts with what admins typed, suggest substring name with lowest Levenshtein distance
                                int bestDistance = Int32.MaxValue;
                                foreach (AdKat_Player player in substringMatches)
                                {
                                    int distance = LevenshteinDistance(record.target_name, player.player_name);
                                    if (distance < bestDistance)
                                    {
                                        bestDistance = distance;
                                        suggestion = player;
                                    }
                                }
                            }
                            //If the suggestion is still null, something has failed
                            if (suggestion == null) { this.DebugWrite("name suggestion system failed substring match", 5); };

                            //Inform admin of multiple players found
                            this.sendMessageToSource(record, msg);

                            //Use suggestion for target
                            record.target_player = suggestion;
                            record.target_name = suggestion.player_name;
                            //Send record to attempt list for confirmation
                            return this.confirmActionWithSource(record);
                        }
                        else
                        {
                            //There were no players found, run a fuzzy search using Levenshtein Distance on all players in server
                            AdKat_Player fuzzyMatch = null;
                            int bestDistance = Int32.MaxValue;
                            foreach (AdKat_Player player in this.playerDictionary.Values)
                            {
                                int distance = LevenshteinDistance(record.target_name, player.player_name);
                                if (distance < bestDistance)
                                {
                                    bestDistance = distance;
                                    fuzzyMatch = player;
                                }
                            }
                            //If the suggestion is still null, something has failed
                            if (fuzzyMatch == null) { this.DebugWrite("name suggestion system failed fuzzy match", 5); return "ERROR"; };

                            //Use suggestion for target
                            record.target_player = fuzzyMatch;
                            record.target_name = fuzzyMatch.player_name;
                            //Send record to attempt list for confirmation
                            return this.confirmActionWithSource(record);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while completing target information.", e));
            }
            return "END OF FUNCTION";
        }

        public string confirmActionWithSource(AdKat_Record record)
        {
            this.DebugWrite("Entering confirmActionWithSource", 7);
            string returnMessage = null;
            try
            {
                lock (actionConfirmMutex)
                {
                    //Cancel any source pending action
                    this.cancelSourcePendingAction(record);
                    //Send record to attempt list
                    this.actionConfirmDic.Add(record.source_name, record);
                    return this.sendMessageToSource(record, record.command_type + "->" + record.target_name + " for " + record.record_message + "?");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while confirming action with record source.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting confirmActionWithSource", 7);
            return returnMessage;
        }

        public void cancelSourcePendingAction(AdKat_Record record)
        {
            this.DebugWrite("Entering cancelSourcePendingAction", 7);
            try
            {
                this.DebugWrite("attempting to cancel command", 6);
                lock (actionConfirmMutex)
                {
                    if (!this.actionConfirmDic.Remove(record.source_name))
                    {
                        //this.sendMessageToSource(record, "No command to cancel.");
                    }
                    else
                    {
                        this.sendMessageToSource(record, "Command Canceled.");
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while canceling source pending action.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting cancelSourcePendingAction", 7);
        }

        public void autoWhitelistPlayers()
        {
            try
            {
                lock (playersMutex)
                {
                    if (this.playersToAutoWhitelist > 0)
                    {
                        Random random = new Random();
                        List<string> playerListCopy = new List<string>();
                        foreach (AdKat_Player player in this.playerDictionary.Values)
                        {
                            this.DebugWrite("Checking for TeamSwap access on " + player.player_name, 6);
                            if (!this.hasAccess(player.player_name, AdKat_CommandType.Teamswap))
                            {
                                this.DebugWrite("player doesnt have access, adding them to chance list", 6);
                                if (!playerListCopy.Contains(player.player_name))
                                {
                                    playerListCopy.Add(player.player_name);
                                }
                            }
                        }
                        if (playerListCopy.Count > 0)
                        {
                            int maxIndex = (playerListCopy.Count < this.playersToAutoWhitelist) ? (playerListCopy.Count) : (this.playersToAutoWhitelist);
                            this.DebugWrite("MaxIndex: " + maxIndex, 6);
                            for (int index = 0; index < maxIndex; index++)
                            {
                                string playerName = null;
                                int iterations = 0;
                                do
                                {
                                    playerName = playerListCopy[random.Next(0, playerListCopy.Count - 1)];
                                } while (this.teamswapRoundWhitelist.ContainsKey(playerName) && (iterations++ < 100));
                                if (!this.teamswapRoundWhitelist.ContainsKey(playerName))
                                {
                                    AdKat_Player aPlayer = null;
                                    if (this.playerDictionary.TryGetValue(playerName, out aPlayer))
                                    {
                                        //Create the Exception record
                                        AdKat_Record record = new AdKat_Record();
                                        record.server_id = this.server_id;
                                        record.command_type = AdKat_CommandType.RoundWhitelistPlayer;
                                        record.command_numeric = 0;
                                        record.target_name = aPlayer.player_name;
                                        record.target_player = aPlayer;
                                        record.source_name = "AdKats";
                                        record.record_message = "Round-Whitelisting Player";
                                        //Process the record
                                        this.queueRecordForProcessing(record);
                                    }
                                    else
                                    {
                                        this.ConsoleError("Player was not in player dictionary when calling auto-whitelist.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while auto-whitelisting players.", e));
            }
        }

        public Boolean handleRoundReport(AdKat_Record record)
        {
            Boolean acted = false;
            try
            {
                lock (reportsMutex)
                {
                    //report ID will be housed in target_name
                    this.DebugWrite("Attempting to handle based on round report.", 6);
                    if (this.round_reports.ContainsKey(record.target_name))
                    {
                        this.DebugWrite("Handling round report.", 5);
                        //Get the reported record
                        AdKat_Record reportedRecord = this.round_reports[record.target_name];
                        //Remove it from the reports for this round
                        this.round_reports.Remove(record.target_name);
                        //Update it in the database
                        reportedRecord.command_action = AdKat_CommandType.ConfirmReport;
                        this.updateRecord(reportedRecord);
                        //Get target information
                        record.target_name = reportedRecord.target_name;
                        record.target_player = reportedRecord.target_player;
                        //Update record message if needed
                        //attempt to handle via pre-message ID
                        //record.record_message = this.getPreMessage(record.record_message, this.requirePreMessageUse);
                        this.DebugWrite("MESS: " + record.record_message, 5);
                        if (record.record_message == null || record.record_message.Length < this.requiredReasonLength)
                        {
                            record.record_message = reportedRecord.record_message;
                        }
                        //Inform the reporter that they helped the admins
                        this.sendMessageToSource(reportedRecord, "Your report has been acted on. Thank you.");
                        //Queue for processing right away
                        this.queueRecordForProcessing(record);
                        acted = true;
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while handling round report.", e);
                this.HandleException(record.record_exception);
            }
            return acted;
        }

        //Attempts to parse the command from a in-game string
        private AdKat_CommandType getCommand(string commandString)
        {
            this.DebugWrite("Entering getCommand", 7);
            AdKat_CommandType command = AdKat_CommandType.Default;
            try
            {
                this.AdKat_CommandStrings.TryGetValue(commandString.ToLower(), out command);
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while parsing command.", e));
            }
            this.DebugWrite("Exiting getCommand", 7);
            return command;
        }

        //Attempts to parse the command from a database string
        private AdKat_CommandType getDBCommand(string commandString)
        {
            this.DebugWrite("Entering getDBCommand", 7);
            AdKat_CommandType command = AdKat_CommandType.Default;
            try
            {
                this.AdKat_RecordTypesInv.TryGetValue(commandString, out command);
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while parsing DB command.", e));
            }
            this.DebugWrite("Exiting getDBCommand", 7);
            return command;
        }

        //replaces the message with a pre-message
        public string getPreMessage(string message, Boolean required)
        {
            this.DebugWrite("Entering getPreMessage", 7);
            try
            {
                if (message != null && message.Length > 0)
                {
                    //Attempt to fill the message via pre-message ID
                    int preMessageID = 0;
                    DebugWrite("Raw preMessageID: " + message, 6);
                    Boolean valid = Int32.TryParse(message, out preMessageID);
                    if (valid && (preMessageID > 0) && (preMessageID <= this.preMessageList.Count))
                    {
                        message = this.preMessageList[preMessageID - 1];
                    }
                    else if (required)
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while getting pre-message.", e));
            }
            this.DebugWrite("Exiting getPreMessage", 7);
            return message;
        }

        #endregion

        #region Action Methods

        private void queueRecordForActionHandling(AdKat_Record record)
        {
            this.DebugWrite("Entering queueRecordForActionHandling", 6);
            try
            {
                this.DebugWrite("Preparing to queue record for action handling", 6);
                lock (unprocessedActionMutex)
                {
                    this.unprocessedActionQueue.Enqueue(record);
                    this.DebugWrite("Record queued for action handling", 6);
                    this.actionHandlingHandle.Set();
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while queuing record for action handling.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting queueRecordForActionHandling", 6);
        }

        private void actionHandlingThreadLoop()
        {
            try
            {
                this.DebugWrite("ACTION: Starting Action Thread", 2);
                Thread.CurrentThread.Name = "action";
                Queue<AdKat_Record> unprocessedActions;
                while (true)
                {
                    this.DebugWrite("ACTION: Entering Action Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("ACTION: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }
                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Handle Inbound Actions
                    if (this.unprocessedActionQueue.Count > 0)
                    {
                        lock (unprocessedActionMutex)
                        {
                            this.DebugWrite("ACTION: Inbound actions found. Grabbing.", 6);
                            //Grab all messages in the queue
                            unprocessedActions = new Queue<AdKat_Record>(this.unprocessedActionQueue.ToArray());
                            //Clear the queue for next run
                            this.unprocessedActionQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (unprocessedActions != null && unprocessedActions.Count > 0)
                        {
                            this.DebugWrite("ACTION: Preparing to Run Actions for record", 6);
                            //Dequeue the record
                            AdKat_Record record = unprocessedActions.Dequeue();

                            //Run the appropriate action
                            this.runAction(record);
                            //If more processing is needed, then perform it
                            //If any errors exist in the record, do not re-queue
                            if (record.record_exception == null)
                            {
                                this.queueRecordForProcessing(record);
                            }
                            else
                            {
                                this.DebugWrite("ACTION: Record has errors, not re-queueing after action.", 3);
                            }
                        }
                    }
                    else
                    {
                        this.DebugWrite("ACTION: No inbound actions. Waiting.", 6);
                        //Wait for new actions
                        this.actionHandlingHandle.Reset();
                        this.actionHandlingHandle.WaitOne(Timeout.Infinite);
                    }
                }
                this.DebugWrite("ACTION: Ending Action Handling Thread", 2);
            }
            catch (Exception e)
            {
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.HandleException(new AdKat_Exception("Action handling thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                    return;
                }
                else
                {
                    this.HandleException(new AdKat_Exception("Fatal occured in action handling thread. Unable to restart.", e));
                }
            }
        }

        private string runAction(AdKat_Record record)
        {
            this.DebugWrite("Entering runAction", 6);
            string response = "ERROR";
            try
            {
                //Perform Actions
                switch (record.command_action)
                {
                    case AdKat_CommandType.MovePlayer:
                        response = this.moveTarget(record);
                        break;
                    case AdKat_CommandType.ForceMovePlayer:
                        response = this.forceMoveTarget(record);
                        break;
                    case AdKat_CommandType.Teamswap:
                        response = this.forceMoveTarget(record);
                        break;
                    case AdKat_CommandType.KillSelf:
                        response = this.killTarget(record, null);
                        break;
                    case AdKat_CommandType.KillPlayer:
                        response = this.killTarget(record, null);
                        break;
                    case AdKat_CommandType.LowPopKillPlayer:
                        response = this.killTarget(record, null);
                        break;
                    case AdKat_CommandType.RepeatKillPlayer:
                        response = this.killTarget(record, null);
                        break;
                    case AdKat_CommandType.KickPlayer:
                        response = this.kickTarget(record, null);
                        break;
                    case AdKat_CommandType.TempBanPlayer:
                        response = this.tempBanTarget(record, null);
                        break;
                    case AdKat_CommandType.PermabanPlayer:
                        response = this.permaBanTarget(record, null);
                        break;
                    case AdKat_CommandType.PunishPlayer:
                        response = this.punishTarget(record);
                        break;
                    case AdKat_CommandType.ForgivePlayer:
                        response = this.forgiveTarget(record);
                        break;
                    case AdKat_CommandType.MutePlayer:
                        response = this.muteTarget(record);
                        break;
                    case AdKat_CommandType.JoinPlayer:
                        response = this.joinTarget(record);
                        break;
                    case AdKat_CommandType.RoundWhitelistPlayer:
                        response = this.roundWhitelistTarget(record);
                        break;
                    case AdKat_CommandType.ReportPlayer:
                        response = this.reportTarget(record);
                        break;
                    case AdKat_CommandType.CallAdmin:
                        response = this.callAdminOnTarget(record);
                        break;
                    case AdKat_CommandType.RestartLevel:
                        response = this.restartLevel(record);
                        break;
                    case AdKat_CommandType.NextLevel:
                        response = this.nextLevel(record);
                        break;
                    case AdKat_CommandType.EndLevel:
                        response = this.endLevel(record);
                        break;
                    case AdKat_CommandType.NukeServer:
                        response = this.nukeTarget(record);
                        break;
                    case AdKat_CommandType.KickAll:
                        response = this.kickAllPlayers(record);
                        break;
                    case AdKat_CommandType.AdminSay:
                        response = this.adminSay(record);
                        break;
                    case AdKat_CommandType.PlayerSay:
                        response = this.playerSay(record);
                        break;
                    case AdKat_CommandType.AdminYell:
                        response = this.adminYell(record);
                        break;
                    case AdKat_CommandType.PlayerYell:
                        response = this.playerYell(record);
                        break;
                    case AdKat_CommandType.EnforceBan:
                        //Don't do anything here, ban enforcer handles this
                        break;
                    default:
                        response = "Command not recognized when running action.";
                        record.record_exception = this.HandleException(new AdKat_Exception("Command not found in runAction"));
                        break;
                }
            }
            catch (Exception e)
            {
                record.record_exception = this.HandleException(new AdKat_Exception("Error while choosing action for record.", e));
            }
            this.DebugWrite("Exiting runAction", 6);
            return response;
        }

        public string moveTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering moveTarget", 6);
            string message = null;
            try
            {
                this.queuePlayerForMove(record.target_player.frostbitePlayerInfo);
                this.playerSayMessage(record.target_name, "On your next death you will be moved to the opposing team.");
                message = this.sendMessageToSource(record, record.target_name + " will be sent to TeamSwap on their next death.");
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for move record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting moveTarget", 6);
            return message;
        }

        public string forceMoveTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering forceMoveTarget", 6);
            string message = null;
            try
            {
                if (record.command_type == AdKat_CommandType.Teamswap)
                {
                    if (this.hasAccess(record.source_name, AdKat_CommandType.Teamswap) || ((this.teamSwapTicketWindowHigh >= this.highestTicketCount) && (this.teamSwapTicketWindowLow <= this.lowestTicketCount)))
                    {
                        message = "Calling Teamswap on self";
                        this.DebugWrite(message, 6);
                        this.queuePlayerForForceMove(record.target_player.frostbitePlayerInfo);
                    }
                    else
                    {
                        message = "Player unable to TeamSwap";
                        this.DebugWrite(message, 6);
                        this.sendMessageToSource(record, "You cannot TeamSwap at this time. Game outside ticket window [" + this.teamSwapTicketWindowLow + ", " + this.teamSwapTicketWindowHigh + "].");
                    }
                }
                else
                {
                    message = "TeamSwap called on " + record.target_name;
                    this.DebugWrite("Calling Teamswap on target", 6);
                    this.sendMessageToSource(record, "" + record.target_name + " sent to TeamSwap.");
                    this.queuePlayerForForceMove(record.target_player.frostbitePlayerInfo);
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for force-move/teamswap record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forceMoveTarget", 6);
            return message;
        }

        public string killTarget(AdKat_Record record, string additionalMessage)
        {
            this.DebugWrite("Entering killTarget", 6);
            string message = null;
            try
            {
                additionalMessage = (additionalMessage != null && additionalMessage.Length > 0) ? (" " + additionalMessage) : ("");
                this.playerSayMessage(record.target_name, "Killed by admin for " + record.record_message + " " + additionalMessage);
                int seconds = (int)DateTime.Now.Subtract(record.target_player.lastDeath).TotalSeconds;
                this.DebugWrite("Killing player. Player last died " + seconds + " seconds ago.", 3);
                if (seconds < 5 && record.command_action != AdKat_CommandType.RepeatKillPlayer)
                {
                    this.DebugWrite("Queueing player for kill on spawn. (" + seconds + ")&(" + record.command_action + ")", 3);
                    if (!this.actOnSpawnDictionary.ContainsKey(record.target_player.player_name))
                    {
                        lock (this.actOnSpawnDictionary)
                        {
                            record.command_action = AdKat_CommandType.RepeatKillPlayer;
                            this.actOnSpawnDictionary.Add(record.target_player.player_name, record);
                        }
                    }
                }
                //Perform actions
                if (String.IsNullOrEmpty(record.target_player.player_name))
                {
                    this.ConsoleError("playername null in 5437");
                }
                else
                {
                    ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_player.player_name);
                    this.sendMessageToSource(record, "You KILLED " + record.target_name + " for " + record.record_message + additionalMessage);
                }
                if (record.source_name == "AutoAdmin" && record.command_type == AdKat_CommandType.PunishPlayer)
                {
                    this.adminSay("Punishing " + record.target_name + " for " + record.record_message);
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for kill record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting killTarget", 6);
            return message;
        }

        public string kickTarget(AdKat_Record record, string additionalMessage)
        {
            this.DebugWrite("Entering kickTarget", 6);
            string message = null;
            try
            {
                additionalMessage = (additionalMessage != null && additionalMessage.Length > 0) ? (" " + additionalMessage) : ("");
                string kickReason = record.source_name + " - " + record.record_message + additionalMessage;
                int cutLength = kickReason.Length - 80;
                if (cutLength > 0)
                {
                    string cutReason = record.record_message.Substring(0, record.record_message.Length - cutLength);
                    kickReason = record.source_name + " - " + cutReason + additionalMessage;
                }
                //Perform Actions
                this.DebugWrite("Kick Message: '" + kickReason + "'", 3);
                if (String.IsNullOrEmpty(record.target_player.player_name) || String.IsNullOrEmpty(kickReason))
                {
                    this.ConsoleError("Item null in 5464");
                }
                else
                {
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", record.target_player.player_name, kickReason); 
                    this.removePlayerFromDictionary(record.target_player.player_name);
                    if (record.target_name != record.source_name)
                    {
                        this.adminSay("Player " + record.target_name + " was KICKED by admin for " + record.record_message + " " + additionalMessage);
                    }
                    message = this.sendMessageToSource(record, "You KICKED " + record.target_name + " for " + record.record_message + ". " + additionalMessage);
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for kick record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting kickTarget", 6);
            return message;
        }

        public string tempBanTarget(AdKat_Record record, string additionalMessage)
        {
            this.DebugWrite("Entering tempBanTarget", 6);
            string message = null;
            try
            {
                //Subtract 1 second for visual effect
                Int32 seconds = (record.command_numeric * 60) - 1;

                //Calculate the remaining ban time
                TimeSpan remainingTime = TimeSpan.FromSeconds(seconds);

                //Perform Actions
                //Only post to ban enforcer if there are no exceptions
                if (this.useBanEnforcer && record.record_exception == null)
                {
                    //Create the ban
                    AdKat_Ban aBan = new AdKat_Ban();
                    aBan.ban_record = record;

                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean GUIDAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean IPAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);
                    aBan.ban_enforceName = nameAvailable && (this.defaultEnforceName || (!GUIDAvailable && !IPAvailable));
                    aBan.ban_enforceGUID = GUIDAvailable && (this.defaultEnforceGUID || (!nameAvailable && !IPAvailable));
                    aBan.ban_enforceIP = IPAvailable && (this.defaultEnforceIP || (!nameAvailable && !GUIDAvailable));

                    //Queue the ban for upload
                    this.queueBanForProcessing(aBan);
                }
                else
                {
                    if (record.record_exception != null)
                    {
                        this.HandleException(new AdKat_Exception("Defaulting to procon banlist usage since exceptions existed in record"));
                    }
                    //Trim the ban message if necessary
                    string banMessage = record.record_message;
                    int cutLength = banMessage.Length - 80;
                    if (cutLength > 0)
                    {
                        banMessage = banMessage.Substring(0, banMessage.Length - cutLength);
                    }
                    this.DebugWrite("Ban Message: '" + banMessage + "'", 3);
                    if (!String.IsNullOrEmpty(record.target_player.player_guid))
                    {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_player.player_guid, "seconds", seconds + "", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_ip))
                    {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "ip", record.target_player.player_ip, "seconds", seconds + "", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_name))
                    {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "name", record.target_player.player_name, "seconds", seconds + "", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else
                    {
                        this.ConsoleError("Player has no information to ban with.");
                        return "ERROR";
                    }
                    this.removePlayerFromDictionary(record.target_player.player_name);
                }
                if (record.target_name != record.source_name)
                {
                    this.adminSay("Player " + record.target_name + " was BANNED by admin for " + record.record_message + " " + additionalMessage);
                }
                message = this.sendMessageToSource(record, "You TEMP BANNED " + record.target_name + " for " + this.formatTimeString(TimeSpan.FromMinutes(record.command_numeric)) + "." + additionalMessage);
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for TempBan record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting tempBanTarget", 6);
            return message;
        }

        public string permaBanTarget(AdKat_Record record, string additionalMessage)
        {
            this.DebugWrite("Entering permaBanTarget", 6);
            string message = null;
            try
            {
                //Subtract 1 second for visual effect
                Int32 seconds = (record.command_numeric * 60) - 1;

                //Calculate the remaining ban time
                TimeSpan remainingTime = TimeSpan.FromSeconds(seconds);

                //Perform Actions
                //Only post to ban enforcer if there are no exceptions
                if (this.useBanEnforcer && record.record_exception == null)
                {
                    //Create the ban
                    AdKat_Ban aBan = new AdKat_Ban();
                    aBan.ban_record = record;

                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean GUIDAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean IPAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);
                    aBan.ban_enforceName = nameAvailable && (this.defaultEnforceName || (!GUIDAvailable && !IPAvailable));
                    aBan.ban_enforceGUID = GUIDAvailable && (this.defaultEnforceGUID || (!nameAvailable && !IPAvailable));
                    aBan.ban_enforceIP = IPAvailable && (this.defaultEnforceIP || (!nameAvailable && !GUIDAvailable));

                    //Queue the ban for upload
                    this.queueBanForProcessing(aBan);
                }
                else
                {
                    if (record.record_exception != null)
                    {
                        this.HandleException(new AdKat_Exception("Defaulting to procon banlist usage since exceptions existed in record"));
                    }
                    //Trim the ban message if necessary
                    string banMessage = record.record_message;
                    int cutLength = banMessage.Length - 80;
                    if (cutLength > 0)
                    {
                        banMessage = banMessage.Substring(0, banMessage.Length - cutLength);
                    }
                    this.DebugWrite("Ban Message: '" + banMessage + "'", 3);
                    if (!String.IsNullOrEmpty(record.target_player.player_guid))
                    {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_player.player_guid, "perm", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_ip))
                    {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "ip", record.target_player.player_ip, "perm", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_name))
                    {
                        this.ExecuteCommand("procon.protected.send", "banList.add", "name", record.target_player.player_name, "perm", banMessage);
                        this.ExecuteCommand("procon.protected.send", "banList.save");
                        this.ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else
                    {
                        this.ConsoleError("Player has no information to ban with.");
                        return "ERROR";
                    }
                    this.removePlayerFromDictionary(record.target_player.player_name);
                }
                if (record.target_name != record.source_name)
                {
                    this.adminSay("Player " + record.target_name + " was BANNED by admin for " + record.record_message + additionalMessage);
                }
                message = this.sendMessageToSource(record, "You PERMA BANNED " + record.target_name + "! Get a vet admin NOW!" + additionalMessage);
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for PermaBan record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting permaBanTarget", 6);
            return message;
        }

        public string enforceBan(AdKat_Ban aBan, Boolean verbose)
        {
            this.DebugWrite("Entering enforceBan", 6);
            string message = null;
            try
            {
                //Create the prefix and suffix for the ban
                string kickMessagePrefix = "" + aBan.ban_record.source_name;
                //If ban time > 1000 days just say perm ban
                TimeSpan remainingTime = this.getRemainingBanTime(aBan);

                if (remainingTime.TotalDays > 1000)
                {
                    kickMessagePrefix += " [perm] ";
                }
                else
                {
                    kickMessagePrefix += " [" + this.formatTimeString(remainingTime) + "] ";
                }
                string kickMessageSuffix = ((this.useBanAppend) ? (" - " + this.banAppend) : (""));
                //Create the total kick message
                string kickMessage = kickMessagePrefix + aBan.ban_record.record_message + kickMessageSuffix;

                //Trim the kick message if necessary
                int cutLength = kickMessage.Length - 80;
                if (cutLength > 0)
                {
                    string cutReason = aBan.ban_record.record_message.Substring(0, aBan.ban_record.record_message.Length - cutLength);
                    kickMessage = kickMessagePrefix + cutReason + kickMessageSuffix;
                }
                this.DebugWrite("Ban Enforce Message: '" + kickMessage + "'", 3);

                //Perform Actions
                if (this.playerDictionary.ContainsKey(aBan.ban_record.target_player.player_name))
                {
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", aBan.ban_record.target_player.player_name, kickMessage); 
                    this.removePlayerFromDictionary(aBan.ban_record.target_player.player_name);
                    //Inform the server of the enforced ban
                    if (verbose)
                    {
                        this.adminSay("Enforcing ban on " + aBan.ban_record.target_player.player_name + " for " + aBan.ban_record.record_message);
                    }
                    message = "Ban Enforced";
                }
                else
                {
                    aBan.ban_exception = new AdKat_Exception("Attempted to kick a player who wasn't in the server.");
                    this.HandleException(aBan.ban_exception);
                }
            }
            catch (Exception e)
            {
                aBan.ban_exception = new AdKat_Exception("Error while enforcing ban.", e);
                this.HandleException(aBan.ban_exception);
            }
            this.DebugWrite("Exiting enforceBan", 6);
            return message;
        }

        public string punishTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering punishTarget", 6);
            string message = null;
            try
            {
                //If the record has any exceptions, skip everything else and just kill the player
                if (record.record_exception == null)
                {
                    //Get number of points the player from server
                    int points = this.fetchPoints(record.target_player);
                    this.DebugWrite(record.target_player.player_name + " has " + points + " points.", 5);
                    //Get the proper action to take for player punishment
                    string action = "noaction";
                    string skippedAction = null;
                    if (points > (this.punishmentHierarchy.Length - 1))
                    {
                        action = this.punishmentHierarchy[this.punishmentHierarchy.Length - 1];
                    }
                    else if (points > 0)
                    {
                        action = this.punishmentHierarchy[points - 1];
                        if (record.isIRO)
                        {
                            skippedAction = this.punishmentHierarchy[points - 2];
                        }
                    }
                    else
                    {
                        action = this.punishmentHierarchy[0];
                    }

                    //Handle the case where and IRO punish skips higher level punishment for a lower one, use the higher one
                    if (skippedAction != null && this.punishmentSeverityIndex.IndexOf(skippedAction) > this.punishmentSeverityIndex.IndexOf(action))
                    {
                        action = skippedAction;
                    }

                    //Set additional message
                    string pointMessage = " [" + ((record.isIRO) ? ("IRO ") : ("")) + points + "pts]";
                    if (!record.record_message.Contains(pointMessage))
                    {
                        record.record_message += pointMessage;
                    }
                    string additionalMessage = null;

                    Boolean isLowPop = this.onlyKillOnLowPop && (this.playerDictionary.Count < this.lowPopPlayerCount);
                    Boolean IROOverride = record.isIRO && this.IROOverridesLowPop;

                    this.DebugWrite("Server low population: " + isLowPop + " (" + this.playerDictionary.Count + " <? " + this.lowPopPlayerCount + ") | Override: " + IROOverride, 5);

                    //Call correct action
                    if ((action.Equals("kill") || (isLowPop && !IROOverride)) && !action.Equals("ban"))
                    {
                        record.command_action = (isLowPop) ? (AdKat_CommandType.LowPopKillPlayer) : (AdKat_CommandType.KillPlayer);
                        message = this.killTarget(record, additionalMessage);
                    }
                    else if (action.Equals("kick"))
                    {
                        record.command_action = AdKat_CommandType.KickPlayer;
                        message = this.kickTarget(record, additionalMessage);
                    }
                    else if (action.Equals("tban60"))
                    {
                        record.command_numeric = 60;
                        record.command_action = AdKat_CommandType.TempBanPlayer;
                        message = this.tempBanTarget(record, additionalMessage);
                    }
                    else if (action.Equals("tbanday"))
                    {
                        record.command_numeric = 1440;
                        record.command_action = AdKat_CommandType.TempBanPlayer;
                        message = this.tempBanTarget(record, additionalMessage);
                    }
                    else if (action.Equals("tbanweek"))
                    {
                        record.command_numeric = 10080;
                        record.command_action = AdKat_CommandType.TempBanPlayer;
                        message = this.tempBanTarget(record, additionalMessage);
                    }
                    else if (action.Equals("tban2weeks"))
                    {
                        record.command_numeric = 20160;
                        record.command_action = AdKat_CommandType.TempBanPlayer;
                        message = this.tempBanTarget(record, additionalMessage);
                    }
                    else if (action.Equals("tbanmonth"))
                    {
                        record.command_numeric = 43200;
                        record.command_action = AdKat_CommandType.TempBanPlayer;
                        message = this.tempBanTarget(record, additionalMessage);
                    }
                    else if (action.Equals("ban"))
                    {
                        record.command_action = AdKat_CommandType.PermabanPlayer;
                        message = this.permaBanTarget(record, additionalMessage);
                    }
                    else
                    {
                        record.command_action = AdKat_CommandType.KillPlayer;
                        this.killTarget(record, additionalMessage);
                        record.record_exception = new AdKat_Exception("Punish options are set incorrectly. '" + action + "' not found. Inform plugin setting manager.");
                        this.HandleException(record.record_exception);
                    }
                }
                else
                {
                    //Exception found, just kill the player
                    record.command_action = AdKat_CommandType.KillPlayer;
                    this.killTarget(record, null);
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for Punish record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting punishTarget", 6);
            return message;
        }

        public string forgiveTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering forgiveTarget", 6);
            string message = null;
            try
            {
                //If the record has any exceptions, skip everything
                if (record.record_exception == null)
                {
                    int points = this.fetchPoints(record.target_player);
                    this.playerSayMessage(record.target_name, "Forgiven 1 infraction point. You now have " + points + " point(s) against you.");
                    return this.sendMessageToSource(record, "Forgive Logged for " + record.target_name + ". They now have " + points + " infraction points.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for Forgive record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forgiveTarget", 6);
            return message;
        }

        public string muteTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering muteTarget", 6);
            string message = null;
            try
            {
                if (!this.hasAccess(record.target_name, AdKat_CommandType.MutePlayer))
                {
                    if (!this.round_mutedPlayers.ContainsKey(record.target_name))
                    {
                        this.round_mutedPlayers.Add(record.target_name, 0);
                        this.playerSayMessage(record.target_name, this.mutedPlayerMuteMessage);
                        message = this.sendMessageToSource(record, record.target_name + " has been muted for this round.");
                    }
                    else
                    {
                        message = this.sendMessageToSource(record, record.target_name + " already muted for this round.");
                    }
                }
                else
                {
                    message = this.sendMessageToSource(record, "You can't mute an admin, dimwit.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for Mute record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting muteTarget", 6);
            return message;
        }

        public string joinTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering joinTarget", 6);
            string message = null;
            try
            {
                //Get source player
                AdKat_Player source_player = null;
                if (this.playerDictionary.TryGetValue(record.source_name, out source_player))
                {
                    //If the source has basic admin access, then the squad will be unlocked for their entry
                    if (this.hasAccess(record.source_name, AdKat_CommandType.MovePlayer))
                    {
                        //Unlock target squad
                        this.sendMessageToSource(record, "Unlocking target squad if needed, please wait.");
                        this.ExecuteCommand("procon.protected.send", "squad.private", record.target_player.frostbitePlayerInfo.TeamID + "", record.target_player.frostbitePlayerInfo.SquadID + "", "false");
                        //If anything longer is needed...tisk tisk
                        Thread.Sleep(500);
                    }
                    //Check for player access to change teams
                    if (record.target_player.frostbitePlayerInfo.TeamID != source_player.frostbitePlayerInfo.TeamID &&
                        !this.hasAccess(record.source_name, AdKat_CommandType.Teamswap))
                    {
                        message = this.sendMessageToSource(record, "Target player is not on your team, you need @moveme/TeamSwap access to join them.");
                    }
                    else
                    {
                        //Move to specific squad
                        ExecuteCommand("procon.protected.send", "admin.movePlayer", record.source_name, record.target_player.frostbitePlayerInfo.TeamID + "", record.target_player.frostbitePlayerInfo.SquadID + "", "true");
                        message = this.sendMessageToSource(record, "Attempting to join " + record.target_name);
                    }
                }
                else
                {
                    message = this.sendMessageToSource(record, "Unable to find you in the player list, please try again.");
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for Join record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting joinTarget", 6);
            return message;
        }

        public string roundWhitelistTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering roundWhitelistTarget", 6);
            string message = null;
            try
            {
                if (!this.teamswapRoundWhitelist.ContainsKey(record.target_name))
                {
                    if (this.teamswapRoundWhitelist.Count < this.playersToAutoWhitelist + 2)
                    {
                        this.teamswapRoundWhitelist.Add(record.target_name, false);
                        string command = this.teamSwapPlayerCommandText;
                        message = record.target_name + " can now use @" + command + " for this round.";
                    }
                    else
                    {
                        message = "Cannot whitelist more than two extra people per round.";
                    }
                }
                else
                {
                    message = record.target_name + " is already in this round's TeamSwap whitelist.";
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for RoundWhitelist record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting roundWhitelistTarget", 6);
            return message;
        }

        public string reportTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering reportTarget", 6);
            string message = null;
            try
            {
                Random random = new Random();
                int reportID;
                do
                {
                    reportID = random.Next(100, 999);
                } while (round_reports.ContainsKey(reportID + ""));
                this.round_reports.Add(reportID + "", record);
                string adminAssistantIdentifier = (this.adminAssistantCache.ContainsKey(record.source_name)) ? ("[AA]") : ("");
                foreach (String admin_name in this.playerAccessCache.Keys)
                {
                    if (this.playerAccessCache[admin_name].access_level <= 4 && this.playerDictionary.ContainsKey(admin_name))
                    {
                        this.playerSayMessage(admin_name, "REPORT " + adminAssistantIdentifier + "[" + reportID + "]: " + record.source_name + " reported " + record.target_name + " for " + record.record_message);
                    }
                }
                message = this.sendMessageToSource(record, "REPORT [" + reportID + "] sent. " + record.target_name + " for " + record.record_message);
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for Report record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting reportTarget", 6);
            return message;
        }

        public string callAdminOnTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering callAdminOnTarget", 6);
            string message = null;
            try
            {
                Random random = new Random();
                int reportID;
                do
                {
                    reportID = random.Next(100, 999);
                } while (round_reports.ContainsKey(reportID + ""));
                this.round_reports.Add(reportID + "", record);
                string adminAssistantIdentifier = (this.adminAssistantCache.ContainsKey(record.source_name)) ? ("[AA]") : ("");
                foreach (String admin_name in this.playerAccessCache.Keys)
                {
                    if (this.playerAccessCache[admin_name].access_level <= 4 && this.playerDictionary.ContainsKey(admin_name))
                    {
                        this.playerSayMessage(admin_name, "ADMIN CALL " + adminAssistantIdentifier + "[" + reportID + "]: " + record.source_name + " called admin on " + record.target_name + " for " + record.record_message);
                    }
                }
                message = this.sendMessageToSource(record, "ADMIN CALL [" + reportID + "] sent. " + record.target_name + " for " + record.record_message);
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for CallAdmin record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting callAdminOnTarget", 6);
            return message;
        }

        public string restartLevel(AdKat_Record record)
        {
            this.DebugWrite("Entering restartLevel", 6);
            string message = null;
            try
            {
                this.ExecuteCommand("procon.protected.send", "mapList.restartRound");
                message = this.sendMessageToSource(record, "Round Restarted.");
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for RestartLevel record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting restartLevel", 6);
            return message;
        }

        public string nextLevel(AdKat_Record record)
        {
            this.DebugWrite("Entering nextLevel", 6);
            string message = null;
            try
            {
                this.ExecuteCommand("procon.protected.send", "mapList.runNextRound");
                message = this.sendMessageToSource(record, "Next round has been run.");
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for NextLevel record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting nextLevel", 6);
            return message;
        }

        public string endLevel(AdKat_Record record)
        {
            this.DebugWrite("Entering forgiveTarget", 6);
            string message = null;
            try
            {
                this.ExecuteCommand("procon.protected.send", "mapList.endRound", record.command_numeric + "");
                message = this.sendMessageToSource(record, "Ended round with " + record.target_name + " as winner.");
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for EndLevel record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forgiveTarget", 6);
            return message;
        }

        public string nukeTarget(AdKat_Record record)
        {
            this.DebugWrite("Entering nukeTarget", 6);
            string message = null;
            try
            {
                foreach (AdKat_Player player in this.playerDictionary.Values)
                {
                    if ((record.target_name == "US Team" && player.frostbitePlayerInfo.TeamID == AdKats.USTeamID) ||
                        (record.target_name == "RU Team" && player.frostbitePlayerInfo.TeamID == AdKats.RUTeamID) ||
                        (record.target_name == "Server"))
                    {
                        Thread.Sleep(30);
                        ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                        this.playerSayMessage(record.target_name, "Killed by admin for: " + record.record_message);
                    }
                }
                message = this.sendMessageToSource(record, "You NUKED " + record.target_name + " for " + record.record_message + ".");
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for NukeServer record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting nukeTarget", 6);
            return message;
        }

        public string kickAllPlayers(AdKat_Record record)
        {
            this.DebugWrite("Entering kickAllPlayers", 6);
            string message = null;
            try
            {
                foreach (AdKat_Player player in this.playerDictionary.Values)
                {
                    if (!(this.playerAccessCache.ContainsKey(player.player_name) && this.playerAccessCache[player.player_name].access_level < 5))
                    {
                        Thread.Sleep(30);
                        ExecuteCommand("procon.protected.send", "admin.kickPlayer", player.player_name, "(" + record.source_name + ") " + record.record_message);
                        this.playerSayMessage(record.target_name, "Killed by admin for: " + record.record_message);
                    }
                }
                this.ExecuteCommand("procon.protected.send", "admin.say", "All players with access class 5 or lower have been kicked.", "all");
                message = "You KICKED EVERYONE for " + record.record_message + ". ";
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for KickAll record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting kickAllPlayers", 6);
            return message;
        }

        public string adminSay(AdKat_Record record)
        {
            this.DebugWrite("Entering forgiveTarget", 6);
            string message = null;
            try
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", record.record_message, "all");
                message = "Server has been told '" + record.record_message + "'";
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for AdminSay record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forgiveTarget", 6);
            return message;
        }

        public string adminYell(AdKat_Record record)
        {
            this.DebugWrite("Entering forgiveTarget", 6);
            string message = null;
            try
            {
                this.ExecuteCommand("procon.protected.send", "admin.yell", record.record_message, this.yellDuration + "", "all");
                message = "Server has been told '" + record.record_message + "'";
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for AdminYell record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forgiveTarget", 6);
            return message;
        }

        public string playerSay(AdKat_Record record)
        {
            this.DebugWrite("Entering forgiveTarget", 6);
            string message = null;
            try
            {
                this.playerSayMessage(record.target_name, record.record_message);
                message = record.target_name + " has been told '" + record.record_message + "' by SAY.";
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for Forgive record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forgiveTarget", 6);
            return message;
        }

        public string playerYell(AdKat_Record record)
        {
            this.DebugWrite("Entering forgiveTarget", 6);
            string message = null;
            try
            {
                this.ExecuteCommand("procon.protected.send", "admin.yell", record.record_message, this.yellDuration + "", "player", record.target_name);
                message = record.target_name + " has been told '" + record.record_message + "' by YELL.";
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Error while taking action for Forgive record.", e);
                this.HandleException(record.record_exception);
            }
            this.DebugWrite("Exiting forgiveTarget", 6);
            return message;
        }

        #endregion

        #region Player Access

        private void queueAccessUpdate(AdKat_Access access)
        {
            try
            {
                this.DebugWrite("Preparing to queue player for access update", 6);
                lock (playerAccessMutex)
                {
                    this.playerAccessUpdateQueue.Enqueue(access);
                    this.DebugWrite("Player queued for access update", 6);
                    this.dbCommHandle.Set();
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queuing access update.", e));
            }
        }

        private void queueAccessRemoval(String player_name)
        {
            try
            {
                this.DebugWrite("Preparing to queue player for access removal", 6);
                lock (playerAccessMutex)
                {
                    this.playerAccessRemovalQueue.Enqueue(player_name);
                    this.DebugWrite("Player queued for access removal", 6);
                    this.dbCommHandle.Set();
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queuing access removal.", e));
            }
        }

        private Boolean hasAccess(String player_name, AdKat_CommandType command)
        {
            try
            {

            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queuing record for action handling.", e));
            }
            Boolean access = false;
            //Check if the player can access the desired command
            if (this.getAccessLevel(player_name) <= this.AdKat_CommandAccessRank[command])
            {
                access = true;
            }
            return access;
        }

        private int getAccessLevel(String player_name)
        {
            try
            {

            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queuing record for action handling.", e));
            }
            int access_level = 6;
            //Get access level of player
            if (this.playerAccessCache.ContainsKey(player_name))
            {
                access_level = this.playerAccessCache[player_name].access_level;
            }
            else if
                (!this.requireTeamswapWhitelist ||
                this.teamswapRoundWhitelist.ContainsKey(player_name) ||
                (this.enableAdminAssistants && this.adminAssistantCache.ContainsKey(player_name)))
            {
                access_level = this.AdKat_CommandAccessRank[AdKat_CommandType.Teamswap];
            }
            return access_level;
        }

        #endregion

        #region Database Methods

        private void databaseCommThreadLoop()
        {
            try
            {
                this.DebugWrite("DBCOMM: Starting Database Comm Thread", 2);
                Thread.CurrentThread.Name = "databasecomm";
                Boolean firstRun = true;
                Queue<AdKat_Record> inboundRecords;
                Queue<AdKat_Ban> inboundBans;
                Queue<AdKat_Access> inboundAccessUpdates;
                Queue<String> inboundAccessRemoval;
                Queue<CPluginVariable> inboundSettingUpload;
                Queue<CBanInfo> inboundCBans;
                while (true)
                {
                    this.DebugWrite("DBCOMM: Entering Database Comm Thread Loop", 7);
                    if (!this.isEnabled)
                    {
                        this.DebugWrite("DBCOMM: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                        break;
                    }

                    //Sleep for 10ms
                    Thread.Sleep(10);

                    //Check if database connection settings have changed
                    if (this.dbSettingsChanged)
                    {
                        this.DebugWrite("DBCOMM: DB Settings have changed, calling test.", 6);
                        if (this.testDatabaseConnection())
                        {
                            this.DebugWrite("DBCOMM: Database Connection Good. Continuing Thread.", 6);
                        }
                        else
                        {
                            this.dbSettingsChanged = true;
                            continue;
                        }
                    }

                    //Every 60 seconds make sure stat logger is running and fully operational
                    if (this.lastStatLoggerStatusUpdateTime.AddSeconds(60) < DateTime.Now)
                    {
                        this.confirmStatLoggerSetup();
                    }

                    //Update server ID
                    if (this.server_id < 0)
                    {
                        //Checking for database server info
                        if (this.fetchServerID() >= 0)
                        {
                            this.ConsoleSuccess("Database Server Info Fetched. Server ID is " + this.server_id + "!");

                            //Now that we have the current server ID from stat logger, import all records from previous versions of AdKats
                            this.updateDB_0251_0300();

                            //Push all settings for this instance to the database
                            this.uploadAllSettings();
                        }
                        else
                        {
                            //Inform the user
                            this.ConsoleError("Database Server info could not be fetched! Make sure XpKiller's Stat Logger is running on this server!");
                            //Disable the plugin
                            this.disable();
                            break;
                        }
                    }
                    else
                    {
                        this.DebugWrite("Skipping server ID fetch. Server ID: " + this.server_id, 7);
                    }

                    //Check if settings need sync
                    if (this.settingImportID != this.server_id || this.lastDBSettingFetch.AddSeconds(this.dbSettingFetchFrequency) < DateTime.Now)
                    {
                        this.DebugWrite("Preparing to fetch settings from server " + server_id, 6);
                        //Fetch new settings from the database
                        this.fetchSettings(this.settingImportID);
                        //Update the database with setting logic employed here
                        this.uploadAllSettings();
                    }

                    //Handle Inbound Setting Uploads
                    if (this.settingUploadQueue.Count > 0)
                    {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound setting queue to retrive new settings", 7);
                        lock (this.settingUploadQueue)
                        {
                            this.DebugWrite("DBCOMM: Inbound settings found. Grabbing.", 6);
                            //Grab all settings in the queue
                            inboundSettingUpload = new Queue<CPluginVariable>(this.settingUploadQueue.ToArray());
                            //Clear the queue for next run
                            this.settingUploadQueue.Clear();
                        }
                        //Loop through all settings in order that they came in
                        while (inboundSettingUpload != null && inboundSettingUpload.Count > 0)
                        {
                            CPluginVariable setting = inboundSettingUpload.Dequeue();

                            this.uploadSetting(setting);
                        }
                    }

                    //Check for new actions from the database at given interval
                    if (this.fetchActionsFromDB && (DateTime.Now > this.lastDBActionFetch.AddSeconds(this.dbActionFrequency)))
                    {
                        this.runActionsFromDB();
                    }
                    else
                    {
                        this.DebugWrite("DBCOMM: Skipping DB action fetch", 7);
                    }

                    //Call banlist at set interval (20 seconds)
                    if (this.useBanEnforcerPreviousState && (DateTime.Now > this.lastBanListCall.AddSeconds(20)))
                    {
                        this.lastBanListCall = DateTime.Now;
                        this.DebugWrite("banlist.list called at interval.", 6);
                        this.ExecuteCommand("procon.protected.send", "banList.list");

                    }

                    //Handle access updates
                    if (this.playerAccessUpdateQueue.Count > 0 || this.playerAccessRemovalQueue.Count > 0)
                    {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound access queues to retrive access changes", 7);
                        lock (playerAccessMutex)
                        {
                            this.DebugWrite("DBCOMM: Inbound access changes found. Grabbing.", 6);
                            //Grab all in the queue
                            inboundAccessUpdates = new Queue<AdKat_Access>(this.playerAccessUpdateQueue.ToArray());
                            inboundAccessRemoval = new Queue<String>(this.playerAccessRemovalQueue.ToArray());
                            //Clear the queue for next run
                            this.playerAccessUpdateQueue.Clear();
                            this.playerAccessRemovalQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (inboundAccessUpdates != null && inboundAccessUpdates.Count > 0)
                        {
                            AdKat_Access playerAccess = inboundAccessUpdates.Dequeue();
                            this.uploadPlayerAccess(playerAccess);
                        }
                        //Loop through all records in order that they came in
                        while (inboundAccessRemoval != null && inboundAccessRemoval.Count > 0)
                        {
                            String playerName = inboundAccessRemoval.Dequeue();
                            this.removePlayerAccess(playerName);
                        }
                        this.fetchAccessList();
                        //Update the setting page with new information
                        this.updateSettingPage();
                    }
                    else if (DateTime.Now > this.lastDBAccessFetch.AddSeconds(this.dbAccessFetchFrequency))
                    {
                        //Handle access updates directly from the database
                        this.fetchAccessList();
                        //Update the setting page with new information
                        this.updateSettingPage();
                    }
                    else if (this.playerAccessCache.Count == 0)
                    {
                        //Handle access updates directly from the database
                        this.fetchAccessList();
                        //If there are now people in the access list, update the UI
                        if (this.playerAccessCache.Count > 0)
                        {
                            //Update the setting page with new information
                            this.updateSettingPage();
                        }
                    }
                    else
                    {
                        this.DebugWrite("DBCOMM: No inbound access changes.", 7);
                    }

                    //Start the other threads
                    if (firstRun)
                    {
                        //Start other threads
                        this.MessagingThread.Start();
                        this.CommandParsingThread.Start();
                        this.ActionHandlingThread.Start();
                        this.TeamSwapThread.Start();
                        this.BanEnforcerThread.Start();
                        this.HackerCheckerThread.Start();
                        firstRun = false;

                        this.threadsReady = true;
                        this.updateSettingPage();

                        //Register a command to indicate availibility to other plugins
                        this.RegisterCommand(AdKatsAvailableIndicator);

                        this.ConsoleWrite("^b^2Enabled!^n^0 Version: " + this.GetPluginVersion());
                    }

                    //Ban Enforcer
                    if (this.useBanEnforcer)
                    {
                        if (!this.useBanEnforcerPreviousState || (DateTime.Now > this.lastDBBanFetch.AddSeconds(this.dbBanFetchFrequency)))
                        {
                            //Load all bans on startup
                            if (!this.useBanEnforcerPreviousState)
                            {
                                //Get all bans from procon
                                this.ConsoleWarn("Preparing to queue procon bans for import. Please wait.");
                                this.dbCommHandle.Reset();
                                this.ExecuteCommand("procon.protected.send", "banList.list");
                                this.dbCommHandle.WaitOne(Timeout.Infinite);
                                this.ConsoleWrite(this.cBanProcessingQueue.Count + " procon bans queued for import. Import might take several minutes if you have many bans!");
                            }
                        }
                        else
                        {
                            this.DebugWrite("DBCOMM: Skipping DB ban fetch", 7);
                        }

                        //Handle Inbound Ban Comms
                        if (this.banEnforcerProcessingQueue.Count > 0)
                        {
                            this.DebugWrite("DBCOMM: Preparing to lock inbound ban enforcer queue to retrive new bans", 7);
                            lock (this.banEnforcerMutex)
                            {
                                this.DebugWrite("DBCOMM: Inbound bans found. Grabbing.", 6);
                                //Grab all messages in the queue
                                inboundBans = new Queue<AdKat_Ban>(this.banEnforcerProcessingQueue.ToArray());
                                //Clear the queue for next run
                                this.banEnforcerProcessingQueue.Clear();
                            }
                            Int32 index = 1;
                            //Loop through all bans in order that they came in
                            while (inboundBans != null && inboundBans.Count > 0)
                            {
                                if (!this.isEnabled || !this.useBanEnforcer)
                                {
                                    this.ConsoleWarn("Canceling ban import mid-operation.");
                                    break;
                                }
                                //Grab the ban
                                AdKat_Ban aBan = inboundBans.Dequeue();

                                this.DebugWrite("DBCOMM: Processing Frostbite Ban: " + index++, 6);

                                //Upload the ban
                                this.uploadBan(aBan);

                                //Only perform special action when ban is direct
                                //Indirect bans are through the procon banlist, so the player has already been kicked
                                if (!aBan.ban_record.source_name.Equals("BanEnforcer"))
                                {
                                    //Enforce the ban
                                    this.enforceBan(aBan, false);
                                }
                                else
                                {
                                    this.removePlayerFromDictionary(aBan.ban_record.target_player.player_name);
                                }
                            }
                        }

                        //Handle BF3 Ban Manager imports
                        if (!this.useBanEnforcerPreviousState)
                        {
                            //Import all bans from BF3 Ban Manager
                            this.importBansFromBBM5108();
                        }

                        //Handle Inbound CBan Uploads
                        if (this.cBanProcessingQueue.Count > 0)
                        {
                            if (!this.useBanEnforcerPreviousState)
                            {
                                this.ConsoleWarn("Do not disable AdKats or change any settings until upload is complete!");
                            }
                            this.DebugWrite("DBCOMM: Preparing to lock inbound cBan queue to retrive new cBans", 7);
                            double totalCBans = 0;
                            double bansImported = 0;
                            Boolean earlyExit = false;
                            DateTime startTime = DateTime.Now;
                            lock (this.cBanProcessingQueue)
                            {
                                this.DebugWrite("DBCOMM: Inbound cBans found. Grabbing.", 6);
                                //Grab all cBans in the queue
                                inboundCBans = new Queue<CBanInfo>(this.cBanProcessingQueue.ToArray());
                                totalCBans = inboundCBans.Count;
                                //Clear the queue for next run
                                this.cBanProcessingQueue.Clear();
                            }
                            //Loop through all cBans in order that they came in
                            AdKat_Ban aBan;
                            AdKat_Record record;
                            Boolean bansFound = false;
                            while (inboundCBans != null && inboundCBans.Count > 0)
                            {
                                //Break from the loop if the plugin is disabled or the setting is reverted.
                                if (!this.isEnabled || !this.useBanEnforcer)
                                {
                                    this.ConsoleWarn("You exited the ban upload process early, the process was not completed.");
                                    earlyExit = true;
                                    break;
                                }

                                bansFound = true;

                                CBanInfo cBan = inboundCBans.Dequeue();

                                //Create the record
                                record = new AdKat_Record();
                                record.command_source = AdKat_CommandSource.InGame;
                                //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                if (cBan.BanLength.Seconds > 0 && cBan.BanLength.Seconds < 31536000)
                                {
                                    record.command_type = AdKat_CommandType.TempBanPlayer;
                                    record.command_action = AdKat_CommandType.TempBanPlayer;
                                    record.command_numeric = cBan.BanLength.Seconds / 60;
                                }
                                else
                                {
                                    record.command_type = AdKat_CommandType.PermabanPlayer;
                                    record.command_action = AdKat_CommandType.PermabanPlayer;
                                    record.command_numeric = 0;
                                }
                                record.source_name = "BanEnforcer";
                                record.server_id = this.server_id;
                                record.target_player = this.fetchPlayer(-1, cBan.SoldierName, cBan.Guid, cBan.IpAddress);
                                if (!String.IsNullOrEmpty(record.target_player.player_name))
                                {
                                    record.target_name = record.target_player.player_name;
                                }
                                record.isIRO = false;
                                record.record_message = cBan.Reason;

                                //Create the ban
                                aBan = new AdKat_Ban();
                                aBan.ban_record = record;

                                //Update the ban enforcement depending on available information
                                Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                                Boolean GUIDAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                                Boolean IPAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);
                                aBan.ban_enforceName = nameAvailable && (this.defaultEnforceName || (!GUIDAvailable && !IPAvailable) || !String.IsNullOrEmpty(cBan.SoldierName));
                                aBan.ban_enforceGUID = GUIDAvailable && (this.defaultEnforceGUID || (!nameAvailable && !IPAvailable) || !String.IsNullOrEmpty(cBan.Guid));
                                aBan.ban_enforceIP = IPAvailable && (this.defaultEnforceIP || (!nameAvailable && !GUIDAvailable) || !String.IsNullOrEmpty(cBan.IpAddress));
                                if (!aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP)
                                {
                                    this.ConsoleError("Unable to create ban, no proper player information");
                                    continue;
                                }

                                //Upload the ban
                                this.DebugWrite("Uploading ban from procon.", 5);
                                this.uploadBan(aBan);

                                if (!this.useBanEnforcerPreviousState && (++bansImported % 25 == 0))
                                {
                                    this.ConsoleWrite(Math.Round(100 * bansImported / totalCBans, 2) + "% of bans uploaded. AVG " + Math.Round(bansImported / ((DateTime.Now - startTime).TotalSeconds), 2) + " uploads/sec.");
                                }
                            }
                            if (bansFound && !earlyExit)
                            {
                                //If all bans have been queued for processing, clear the ban list
                                this.ExecuteCommand("procon.protected.send", "banList.clear");
                                this.ExecuteCommand("procon.protected.send", "banList.save");
                                this.ExecuteCommand("procon.protected.send", "banList.list");
                                if (!this.useBanEnforcerPreviousState)
                                {
                                    this.ConsoleSuccess("All bans uploaded into AdKats database.");
                                }
                            }
                        }

                        this.useBanEnforcerPreviousState = true;
                    }
                    else
                    {
                        //If the ban enforcer was previously enabled, and the user disabled it, repopulate procon's ban list
                        if (this.useBanEnforcerPreviousState)
                        {
                            this.repopulateProconBanList();
                            this.useBanEnforcerPreviousState = false;
                            this.bansFirstListed = false;
                        }
                        //If not, completely ignore all ban enforcer code
                    }

                    //Handle Inbound Records
                    if (this.unprocessedRecordQueue.Count > 0)
                    {
                        this.DebugWrite("DBCOMM: Preparing to lock inbound record queue to retrive new records", 7);
                        lock (this.unprocessedRecordMutex)
                        {
                            this.DebugWrite("DBCOMM: Inbound records found. Grabbing.", 6);
                            //Grab all messages in the queue
                            inboundRecords = new Queue<AdKat_Record>(this.unprocessedRecordQueue.ToArray());
                            //Clear the queue for next run
                            this.unprocessedRecordQueue.Clear();
                        }
                        //Loop through all records in order that they came in
                        while (inboundRecords != null && inboundRecords.Count > 0)
                        {
                            //Pull the next record
                            AdKat_Record record = inboundRecords.Dequeue();
                            //Upload the record
                            this.handleRecordUpload(record);
                            //Check for action handling needs
                            if (!record.record_action_executed)
                            {
                                //Action is only called after initial upload, not after update
                                this.DebugWrite("DBCOMM: Upload success. Attempting to add to action queue.", 6);

                                //Only queue the record for action handling if it's not an enforced ban
                                if (record.command_type != AdKat_CommandType.EnforceBan)
                                {
                                    this.queueRecordForActionHandling(record);
                                }
                            }
                            else
                            {
                                //Performance testing area
                                if (record.source_name == this.debugSoldierName)
                                {
                                    this.sendMessageToSource(record, "Duration: " + ((int)DateTime.Now.Subtract(this.commandStartTime).TotalMilliseconds) + "ms");
                                }
                                this.DebugWrite("DBCOMM: Update success. Record does not need action handling.", 6);
                            }
                        }
                    }
                    else
                    {
                        this.DebugWrite("DBCOMM: No unprocessed records. Waiting for input", 7);
                        this.dbCommHandle.Reset();

                        if (this.fetchActionsFromDB || this.useBanEnforcer || this.usingAWA)
                        {
                            //If waiting on DB input, the maximum time we can wait is "db action frequency"
                            this.dbCommHandle.WaitOne(this.dbActionFrequency * 1000);
                        }
                        else
                        {
                            //Maximum wait time is DB access fetch time
                            this.dbCommHandle.WaitOne(this.dbAccessFetchFrequency * 1000);
                        }
                    }
                }
                this.DebugWrite("DBCOMM: Ending Database Comm Thread", 2);
            }
            catch (Exception e)
            {
                if (typeof(ThreadAbortException).Equals(e.GetType()))
                {
                    this.HandleException(new AdKat_Exception("Database comm thread aborted. Attempting to restart.", e));
                    Thread.ResetAbort();
                    return;
                }
                else
                {
                    this.HandleException(new AdKat_Exception("Error occured in database comm thread.", e));
                }
            }
        }

        #region Connection and Setup

        private Boolean connectionCapable()
        {
            if ((this.mySqlDatabaseName != null && this.mySqlDatabaseName.Length > 0) &&
                (this.mySqlHostname != null && this.mySqlHostname.Length > 0) &&
                (this.mySqlPassword != null && this.mySqlPassword.Length > 0) &&
                (this.mySqlPort != null && this.mySqlPort.Length > 0) &&
                (this.mySqlUsername != null && this.mySqlUsername.Length > 0))
            {
                this.DebugWrite("MySql Connection capable. All variables in place.", 7);
                return true;
            }
            return false;
        }

        private MySqlConnection getDatabaseConnection()
        {
            if (this.connectionCapable())
            {
                MySqlConnection conn = new MySqlConnection(this.PrepareMySqlConnectionString());
                conn.Open();
                return conn;
            }
            else
            {
                this.ConsoleError("Attempted to connect to database without all variables in place.");
                return null;
            }
        }

        private Boolean testDatabaseConnection()
        {
            Boolean databaseValid = false;
            DebugWrite("testDatabaseConnection starting!", 6);
            if (this.connectionCapable())
            {
                Boolean success = false;
                int attempt = 0;
                do
                {
                    if (!this.isEnabled) { return false; };
                    attempt++;
                    try
                    {
                        //Prepare the connection string and create the connection object
                        using (MySqlConnection connection = this.getDatabaseConnection())
                        {
                            this.ConsoleWrite("Attempting database connection. Attempt " + attempt + " of 5.");
                            //Attempt a ping through the connection
                            if (connection.Ping())
                            {
                                //Connection good
                                this.ConsoleSuccess("Database connection open.");
                                success = true;
                                this.dbConnectionActive = true;
                            }
                            else
                            {
                                //Connection poor
                                this.ConsoleError("Database connection FAILED ping test.");
                            }
                        } //databaseConnection gets closed here
                        if (success)
                        {
                            //Make sure database structure is good
                            if (this.confirmDatabaseSetup())
                            {
                                //Fetch all access lists
                                this.fetchAccessList();
                                //Fetch the database time conversion
                                this.fetchDBTimeConversion();
                                //Confirm the database is valid
                                databaseValid = true;
                                //clear setting change monitor
                                this.dbSettingsChanged = false;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        //Only perform retries if the error was a timeout
                        if (e.ToString().Contains("Unable to connect"))
                        {
                            this.ConsoleError("Connection failed on attempt " + attempt + ". " + ((attempt <= 5) ? ("Retrying in 5 seconds. ") : ("")));
                            Thread.Sleep(5000);
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
                    this.ConsoleError("Database connection FAILED with EXCEPTION. Bad credentials, invalid hostname, or invalid port.");
                    this.disable();
                }
            }
            else
            {
                this.ConsoleError("Not DB connection capable yet, complete SQL connection variables.");
            }
            DebugWrite("testDatabaseConnection finished!", 6);

            return databaseValid;
        }

        private Boolean confirmDatabaseSetup()
        {
            this.DebugWrite("Confirming Database Structure.", 3);
            try
            {
                Boolean confirmed = true;
                if (!this.confirmTable("tbl_playerdata") || !this.confirmTable("tbl_server"))
                {
                    this.ConsoleError("Tables from XPKiller's Stat Logger not found in the database. Enable that plugin then re-run AdKats!");
                    confirmed = false;
                }
                else
                {
                    if (!this.confirmTable("adkats_records"))
                    {
                        this.ConsoleError("Main Record table not present in the database.");
                        //Temporary code until delimiters are fixed
                        this.ConsoleError("For this release the adkats database setup script must be run manually. Run the script then restart AdKats.");
                        return false;
                        this.runDBSetupScript();
                        if (!this.confirmTable("adkats_records"))
                        {
                            this.ConsoleError("After running setup script main record table still not present.");
                            confirmed = false;
                        }
                    }
                    if (!this.confirmTable("adkats_accesslist"))
                    {
                        this.ConsoleError("Access Table not present in the database.");
                        //Temporary code until delimiters are fixed
                        this.ConsoleError("For this release the adkats database setup script must be run manually. Run the script then restart AdKats.");
                        return false;
                        this.runDBSetupScript();
                        if (!this.confirmTable("adkats_accesslist"))
                        {
                            this.ConsoleError("After running setup script access table still not present.");
                            confirmed = false;
                        }
                    }
                    if (!this.confirmTable("adkats_serverPlayerPoints"))
                    {
                        this.ConsoleError("Server Points Table not present in the database.");
                        //Temporary code until delimiters are fixed
                        this.ConsoleError("For this release the adkats database setup script must be run manually. Run the script then restart AdKats.");
                        return false;
                        this.runDBSetupScript();
                        if (!this.confirmTable("adkats_serverPlayerPoints"))
                        {
                            this.ConsoleError("After running setup script Server Points table still not present.");
                            confirmed = false;
                        }
                    }
                    if (!this.confirmTable("adkats_globalPlayerPoints"))
                    {
                        this.ConsoleError("Global Points Table not present in the database.");
                        //Temporary code until delimiters are fixed
                        this.ConsoleError("For this release the adkats database setup script must be run manually. Run the script then restart AdKats.");
                        return false;
                        this.runDBSetupScript();
                        if (!this.confirmTable("adkats_globalPlayerPoints"))
                        {
                            this.ConsoleError("After running setup script Global Points table still not present.");
                            confirmed = false;
                        }
                    }
                    if (!this.confirmTable("adkats_banlist"))
                    {
                        this.ConsoleError("Ban List not present in the database.");
                        //Temporary code until delimiters are fixed
                        this.ConsoleError("For this release the adkats database setup script must be run manually. Run the script then restart AdKats.");
                        return false;
                        this.runDBSetupScript();
                        if (!this.confirmTable("adkats_accesslist"))
                        {
                            this.ConsoleError("After running setup script banlist still not present.");
                            confirmed = false;
                        }
                    }
                    if (!this.confirmTable("adkats_settings"))
                    {
                        this.ConsoleError("Settings Table not present in the database.");
                        //Temporary code until delimiters are fixed
                        this.ConsoleError("For this release the adkats database setup script must be run manually. Run the script then restart AdKats.");
                        return false;
                        this.runDBSetupScript();
                        if (!this.confirmTable("adkats_settings"))
                        {
                            this.ConsoleError("After running setup script Settings Table still not present.");
                        }
                    }
                }
                if (confirmed)
                {
                    this.ConsoleSuccess("Database confirmed functional for AdKats use.");
                }
                else
                {
                    this.ConsoleError("Database structure errors detected, not set up for AdKats use.");
                }
                return confirmed;
            }
            catch (Exception e)
            {
                this.ConsoleError("ERROR in helper_confirmDatabaseSetup: " + e.ToString());
                return false;
            }
        }

        private void runDBSetupScript()
        {
            try
            {
                ConsoleWrite("Running database setup script. You will not lose any data.");
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        WebClient downloader = new WebClient();
                        //Set the insert command structure
                        command.CommandText = downloader.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/adkats.sql");
                        try
                        {
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() >= 0)
                            {
                                ConsoleWrite("Setup script successful, your database is now prepared for use by AdKats " + this.GetPluginVersion());
                            }
                        }
                        catch (Exception e)
                        {
                            this.ConsoleError("Your database did not accept the script. Does your account have access to table creation? AdKats will not function properly. Exception: " + e.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.ConsoleError("ERROR when setting up DB, you might not have connection to github: " + e.ToString());
            }
        }

        private Boolean confirmTable(string tablename)
        {
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='" + tablename + "'";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.ConsoleError("ERROR in helper_confirmTable: " + e.ToString());
                return false;
            }
        }

        private string PrepareMySqlConnectionString()
        {
            //Create new string for connection
            string conString = String.Empty;
            lock (this.dbCommStringBuilder)
            {
                //Default to port 3306 and attempt to parse correct port
                uint uintport = 3306;
                uint.TryParse(this.mySqlPort, out uintport);
                //Add connection variables
                this.dbCommStringBuilder.Port = uintport;
                this.dbCommStringBuilder.Server = this.mySqlHostname;
                this.dbCommStringBuilder.UserID = this.mySqlUsername;
                this.dbCommStringBuilder.Password = this.mySqlPassword;
                this.dbCommStringBuilder.Database = this.mySqlDatabaseName;
                //Set up connection pooling
                if (this.useConnectionPooling)
                {
                    this.dbCommStringBuilder.Pooling = true;
                    this.dbCommStringBuilder.MinimumPoolSize = Convert.ToUInt32(this.minConnectionPoolSize);
                    this.dbCommStringBuilder.MaximumPoolSize = Convert.ToUInt32(this.maxConnectionPoolSize);
                    this.dbCommStringBuilder.ConnectionLifeTime = 600;
                }
                else
                {
                    this.dbCommStringBuilder.Pooling = false;
                }
                //Set Compression
                this.dbCommStringBuilder.UseCompression = this.useCompressedConnection;
                //Allow User Settings
                this.dbCommStringBuilder.AllowUserVariables = true;
                //Set Timeout Settings
                this.dbCommStringBuilder.DefaultCommandTimeout = 3600;
                this.dbCommStringBuilder.ConnectionTimeout = 50;

                //Build the final connection string
                conString = this.dbCommStringBuilder.ConnectionString;
            }
            return conString;
            //Old Code
            //return "Server=" + mySqlHostname + ";Port=" + mySqlPort + ";Database=" + this.mySqlDatabaseName + ";Uid=" + mySqlUsername + ";Pwd=" + mySqlPassword + ";";
        }

        #endregion

        #region Queries

        private void uploadAllSettings()
        {
            if (!this.isEnabled) { return; }
            try
            {
                DebugWrite("uploadAllSettings starting!", 6);
                this.queueSettingForUpload(new CPluginVariable(@"Plugin Version", typeof(string), this.GetPluginVersion()));
                this.queueSettingForUpload(new CPluginVariable(@"Debug level", typeof(Int32), this.debugLevel));
                this.queueSettingForUpload(new CPluginVariable(@"Debug Soldier Name", typeof(string), this.debugSoldierName));
                this.queueSettingForUpload(new CPluginVariable(@"Server VOIP Address", typeof(string), this.serverVoipAddress));
                this.queueSettingForUpload(new CPluginVariable(@"External Access Key", typeof(string), this.externalCommandAccessKey));
                this.queueSettingForUpload(new CPluginVariable(@"Fetch Actions from Database", typeof(Boolean), this.fetchActionsFromDB));
                this.queueSettingForUpload(new CPluginVariable(@"Use Additional Ban Message", typeof(Boolean), this.useBanAppend));
                this.queueSettingForUpload(new CPluginVariable(@"Additional Ban Message", typeof(string), this.banAppend));
                this.queueSettingForUpload(new CPluginVariable(@"Use Ban Enforcer", typeof(Boolean), this.useBanEnforcer));
                this.queueSettingForUpload(new CPluginVariable(@"Enforce New Bans by NAME", typeof(Boolean), this.defaultEnforceName));
                this.queueSettingForUpload(new CPluginVariable(@"Enforce New Bans by GUID", typeof(Boolean), this.defaultEnforceGUID));
                this.queueSettingForUpload(new CPluginVariable(@"Enforce New Bans by IP", typeof(Boolean), this.defaultEnforceIP));
                this.queueSettingForUpload(new CPluginVariable(@"Minimum Required Reason Length", typeof(Int32), this.requiredReasonLength));
                this.queueSettingForUpload(new CPluginVariable(@"Confirm Command", typeof(string), this.confirmCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Cancel Command", typeof(string), this.cancelCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Kill Player", typeof(string), this.killPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Kick Player", typeof(string), this.kickPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Temp-Ban Player", typeof(string), this.tempBanPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Permaban Player", typeof(string), this.permaBanPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Punish Player", typeof(string), this.punishPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Forgive Player", typeof(string), this.forgivePlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Mute Player", typeof(string), this.mutePlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Join Player", typeof(string), this.joinPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Round Whitelist Player", typeof(string), this.roundWhitelistPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"OnDeath Move Player", typeof(string), this.movePlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Force Move Player", typeof(string), this.forceMovePlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Teamswap Self", typeof(string), this.teamSwapPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Report Player", typeof(string), this.reportPlayerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Call Admin on Player", typeof(string), this.callAdminCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Admin Say", typeof(string), this.adminSayCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Player Say", typeof(string), this.playerSayCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Admin Yell", typeof(string), this.adminYellCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Player Yell", typeof(string), this.playerYellCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"What Is", typeof(string), this.whatIsCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Voip", typeof(string), this.voipCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Restart Level", typeof(string), this.restartLevelCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Next Level", typeof(string), this.nextLevelCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"End Level", typeof(string), this.endLevelCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Nuke Server", typeof(string), this.nukeServerCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Kick All NonAdmins", typeof(string), this.kickAllCommandText));
                this.queueSettingForUpload(new CPluginVariable(@"Punishment Hierarchy", typeof(string), CPluginVariable.EncodeStringArray(this.punishmentHierarchy)));
                this.queueSettingForUpload(new CPluginVariable(@"Combine Server Punishments", typeof(Boolean), this.combineServerPunishments));
                this.queueSettingForUpload(new CPluginVariable(@"Only Kill Players when Server in low population", typeof(Boolean), this.onlyKillOnLowPop));
                this.queueSettingForUpload(new CPluginVariable(@"Low Population Value", typeof(Int32), this.lowPopPlayerCount));
                this.queueSettingForUpload(new CPluginVariable(@"IRO Punishment Overrides Low Pop", typeof(Boolean), this.IROOverridesLowPop));
                this.queueSettingForUpload(new CPluginVariable(@"Send Emails", typeof(Boolean), this.useEmail));
                this.queueSettingForUpload(new CPluginVariable(@"On-Player-Muted Message", typeof(string), this.mutedPlayerMuteMessage));
                this.queueSettingForUpload(new CPluginVariable(@"On-Player-Killed Message", typeof(string), this.mutedPlayerKillMessage));
                this.queueSettingForUpload(new CPluginVariable(@"On-Player-Kicked Message", typeof(string), this.mutedPlayerKickMessage));
                this.queueSettingForUpload(new CPluginVariable(@"# Chances to give player before kicking", typeof(Int32), this.mutedPlayerChances));
                this.queueSettingForUpload(new CPluginVariable(@"Require Whitelist for Access", typeof(Boolean), this.requireTeamswapWhitelist));
                this.queueSettingForUpload(new CPluginVariable(@"Auto-Whitelist Count", typeof(Int32), this.playersToAutoWhitelist));
                this.queueSettingForUpload(new CPluginVariable(@"Ticket Window High", typeof(Int32), this.teamSwapTicketWindowHigh));
                this.queueSettingForUpload(new CPluginVariable(@"Ticket Window Low", typeof(Int32), this.teamSwapTicketWindowLow));
                this.queueSettingForUpload(new CPluginVariable(@"Enable Admin Assistant Perk", typeof(Boolean), this.enableAdminAssistants));
                this.queueSettingForUpload(new CPluginVariable(@"Minimum Confirmed Reports Per Week", typeof(Int32), this.minimumRequiredWeeklyReports));
                this.queueSettingForUpload(new CPluginVariable(@"Yell display time seconds", typeof(Int32), this.yellDuration));
                this.queueSettingForUpload(new CPluginVariable(@"Pre-Message List", typeof(string), CPluginVariable.EncodeStringArray(this.preMessageList.ToArray())));
                this.queueSettingForUpload(new CPluginVariable(@"Require Use of Pre-Messages", typeof(Boolean), this.requirePreMessageUse));
                DebugWrite("uploadAllSettings finished!", 6);
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing all settings for upload.", e));
            }
        }

        private void uploadSetting(CPluginVariable var)
        {
            DebugWrite("uploadSetting starting!", 7);

            //List<CPluginVariable> vars = this.GetPluginVariables();

            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        string value = var.Value.Replace("'", "*").Replace('"', '*');
                        //Check for length too great
                        if (value.Length > 1499)
                        {
                            this.ConsoleError("Unable to upload setting. Length of setting too great. Really dude? It's 1500 chars. This is battlefield, not a book club.");
                            return;
                        }
                        this.DebugWrite(value, 7);
                        //Set the insert command structure
                        command.CommandText = @"
                        INSERT INTO `" + this.mySqlDatabaseName + @"`.`adkats_settings` 
                        (
                            `server_id`, 
                            `setting_name`, 
                            `setting_type`, 
                            `setting_value`
                        ) 
                        VALUES 
                        ( 
                            " + this.server_id + @", 
                            '" + var.Name + @"', 
                            '" + var.Type + @"', 
                            '" + value + @"'
                        ) 
                        ON DUPLICATE KEY 
                        UPDATE 
                            `setting_value` = '" + value + @"'";
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0)
                        {
                            this.DebugWrite("Setting " + var.Name + " pushed to database", 7);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while uploading setting to database.", e));
            }

            DebugWrite("uploadSetting finished!", 7);
        }

        private Boolean fetchSettings(Int64 server_id)
        {
            DebugWrite("fetchSettings starting!", 6);
            Boolean success = false;
            try
            {
                //Success fetching settings
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        String sql = @"
                        SELECT  
                            `setting_name`, 
                            `setting_type`, 
                            `setting_value`
                        FROM 
                            `" + this.mySqlDatabaseName + @"`.`adkats_settings` 
                        WHERE 
                            `server_id` = " + server_id;
                        command.CommandText = sql;
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            //Grab the settings
                            CPluginVariable var = null;
                            while (reader.Read())
                            {
                                success = true;
                                //Create as variable in case needed later
                                var = new CPluginVariable(reader.GetString("setting_name"), reader.GetString("setting_type"), reader.GetString("setting_value"));
                                this.SetPluginVariable(var.Name, var.Value);
                            }
                            if (success)
                            {
                                this.lastDBSettingFetch = DateTime.Now;
                                this.updateSettingPage();
                            }
                            else
                            {
                                this.ConsoleError("Settings could not be loaded. Server " + server_id + " invalid.");
                            }
                            this.settingImportID = this.server_id;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching settings from database.", e));
            }
            this.DebugWrite("fetchSettings finished!", 6);
            return success;
        }

        private AdKat_Record handleRecordUpload(AdKat_Record record)
        {
            try
            {
                this.DebugWrite("DBCOMM: Entering handle record upload", 5);
                if (record.record_id != -1 || record.record_action_executed)
                {
                    //Record already has a record ID, or action has already been taken, it can only be updated
                    if (this.AdKat_LoggingSettings[record.command_type])
                    {
                        if (record.record_exception == null)
                        {
                            //Only call update if the record contained no errors
                            this.DebugWrite("DBCOMM: UPDATING record for " + record.command_type, 5);
                            //Update Record
                            this.updateRecord(record);
                        }
                        else
                        {
                            this.DebugWrite("DBCOMM: " + record.command_type + " record contained errors, skipping UPDATE", 4);
                        }
                    }
                    else
                    {
                        this.DebugWrite("DBCOMM: Skipping record UPDATE for " + record.command_type, 5);
                    }
                }
                else
                {
                    this.DebugWrite("DBCOMM: Record needs full upload, checking.", 5);
                    //No record ID. Perform full upload
                    switch (record.command_type)
                    {
                        case AdKat_CommandType.PunishPlayer:
                            //Upload for punish is required
                            //Check if the punish will be double counted
                            if (this.isDoubleCounted(record))
                            {
                                this.DebugWrite("DBCOMM: Punish is double counted.", 5);
                                //Check if player is on timeout
                                if (this.canPunish(record))
                                {
                                    //IRO - Immediate Repeat Offence
                                    record.isIRO = true;
                                    //Upload record twice
                                    this.DebugWrite("DBCOMM: UPLOADING IRO Punish", 5);
                                    this.uploadRecord(record);
                                    this.uploadRecord(record);
                                }
                            }
                            else
                            {
                                //Upload record once
                                this.DebugWrite("DBCOMM: UPLOADING Punish", 5);
                                this.uploadRecord(record);
                            }
                            break;
                        case AdKat_CommandType.ForgivePlayer:
                            //Upload for forgive is required
                            //No restriction on forgives/minute
                            this.DebugWrite("DBCOMM: UPLOADING Forgive", 5);
                            this.uploadRecord(record);
                            break;
                        default:
                            //Case for any other command
                            //Check logging setting for record command type
                            if (this.AdKat_LoggingSettings[record.command_type])
                            {
                                this.DebugWrite("UPLOADING record for " + record.command_type, 5);
                                //Upload Record
                                this.uploadRecord(record);
                            }
                            else
                            {
                                this.DebugWrite("Skipping record UPLOAD for " + record.command_type, 6);
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = this.HandleException(new AdKat_Exception("Error while handling record upload.", e));
            }
            return record;
        }

        private Boolean uploadRecord(AdKat_Record record)
        {
            DebugWrite("uploadRecord starting!", 6);

            Boolean success = false;
            MySqlCommand commandAttempt = null;
            try
            {
                //Make sure record has an action
                if (record.command_action == AdKat_CommandType.Default)
                {
                    record.command_action = record.command_type;
                }
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "INSERT INTO `" + this.mySqlDatabaseName + @"`.`adkats_records` 
                        (
                            `server_id`, 
                            `command_type`, 
                            `command_action`, 
                            `command_numeric`, 
                            `target_name`, 
                            `target_id`, 
                            `source_name`, 
                            `record_message`, 
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
                            @record_message, 
                            'Y'
                        )";

                        //Fill the command
                        command.Parameters.AddWithValue("@server_id", record.server_id);

                        //Convert enum to DB string
                        string type = String.Empty;
                        string action = String.Empty;
                        if (this.AdKat_RecordTypes.TryGetValue(record.command_type, out type))
                        {
                            if (!this.AdKat_RecordTypes.TryGetValue(record.command_action, out action))
                            {
                                record.record_exception = new AdKat_Exception(record.command_type + " not found.");
                                this.HandleException(record.record_exception);
                                return false;
                            }
                        }
                        else
                        {
                            record.record_exception = new AdKat_Exception(record.command_type + " not found.");
                            this.HandleException(record.record_exception);
                            return false;
                        }
                        command.Parameters.AddWithValue("@command_type", type);
                        command.Parameters.AddWithValue("@command_action", action);
                        command.Parameters.AddWithValue("@command_numeric", record.command_numeric);
                        string tName = "NoNameTarget";
                        if (!String.IsNullOrEmpty(record.target_name))
                        {
                            tName = record.target_name;
                        }
                        if (record.target_player != null)
                        {
                            if (!String.IsNullOrEmpty(record.target_player.player_name))
                            {
                                tName = record.target_player.player_name;
                            }
                            command.Parameters.AddWithValue("@target_id", record.target_player.player_id);
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@target_id", null);
                        }
                        command.Parameters.AddWithValue("@target_name", tName);
                        command.Parameters.AddWithValue("@source_name", record.source_name);
                        string messageIRO = record.record_message + ((record.isIRO) ? (" [IRO]") : (""));
                        //Trim to 500 characters (Should only hit this limit when processing error messages)
                        messageIRO = messageIRO.Length <= 500 ? messageIRO : messageIRO.Substring(0, 500);
                        command.Parameters.AddWithValue("@record_message", messageIRO);

                        //Get reference to the command in case of error
                        commandAttempt = command;
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0)
                        {
                            success = true;
                            record.record_id = command.LastInsertedId;
                        }
                    }
                }

                string temp = this.AdKat_RecordTypes[record.command_action];

                if (success)
                {
                    DebugWrite(temp + " upload for player " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
                }
                else
                {
                    record.record_exception = new AdKat_Exception("Unknown error uploading record.");
                    this.HandleException(record.record_exception);
                    return false;
                }

                DebugWrite("uploadRecord finished!", 6);
                return success;
            }
            catch (MySqlException e)
            {
                //Handle the specific case of 'data too long'
                try
                {
                    if (e.Message.Contains("Data too long for column"))
                    {
                        if (this.fixRecordMessageLength())
                        {
                            this.queueRecordForProcessing(record);
                        }
                    }
                }
                catch (Exception innerE)
                {
                    record.record_exception = new AdKat_Exception("Unable to upload record after data length error.", innerE);
                    this.HandleException(record.record_exception);
                    return false;
                }
                return success;
            }
            catch (Exception e)
            {
                record.record_exception = new AdKat_Exception("Unexpected error uploading Record.", e);
                this.HandleException(record.record_exception);
                return false;
            }
        }

        private Boolean updateRecord(AdKat_Record record)
        {
            DebugWrite("updateRecord starting!", 6);

            Boolean success = false;
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        Boolean hasRecordID = (record.record_id > 0);
                        //Set the insert command structure
                        command.CommandText = "UPDATE `" + this.mySqlDatabaseName + @"`.`adkats_records` 
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
                        //Trim to 100 characters
                        record.record_message = record.record_message.Length <= 500 ? record.record_message : record.record_message.Substring(0, 500);
                        command.Parameters.AddWithValue("@record_message", record.record_message);
                        command.Parameters.AddWithValue("@command_action", this.AdKat_RecordTypes[record.command_action]);

                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0)
                        {
                            success = true;
                        }
                    }
                }

                string temp = this.AdKat_RecordTypes[record.command_action];

                if (success)
                {
                    DebugWrite(temp + " update for player " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
                }
                else
                {
                    ConsoleError(temp + " update for player '" + record.target_name + " by " + record.source_name + " FAILED!");
                }

                DebugWrite("updateRecord finished!", 6);
                return success;
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while updating record"));
                return success;             
            }
        }

        //TEST
        private Boolean fixRecordMessageLength()
        {
            //Handle data too long case
            //Length for message used to be 100. Change to 500.
            Boolean success = false;
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "ALTER TABLE `" + this.mySqlDatabaseName + @"`.`adkats_records` MODIFY COLUMN `record_message` VARCHAR(500)";
                        //Attempt to execute the query
                        if (command.ExecuteNonQuery() > 0)
                        {
                            success = true;
                        }
                    }
                }
            }
            catch (Exception innerE)
            {
                this.HandleException(new AdKat_Exception("Unable to change record_message column length.", innerE));
            }
            return success;
        }

        //DONE
        private AdKat_Record fetchRecordByID(Int64 record_id)
        {
            DebugWrite("fetchRecordByID starting!", 6);
            AdKat_Record record = null;
            try
            {
                //Success fetching record
                Boolean success = false;
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
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
                            `" + this.mySqlDatabaseName + @"`.`adkats_records` 
                        WHERE 
                            `record_id` = " + record_id;
                        command.CommandText = sql;
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            //Grab the record
                            if (reader.Read())
                            {
                                success = true;

                                record = new AdKat_Record();

                                record.record_id = reader.GetInt64("record_id");
                                record.server_id = reader.GetInt64("server_id");
                                record.command_type = this.getDBCommand(reader.GetString("command_type"));
                                record.command_action = this.getDBCommand(reader.GetString("command_action"));
                                record.command_numeric = reader.GetInt32("command_numeric");
                                record.target_name = reader.GetString("target_name");
                                if (!reader.IsDBNull(6))
                                {
                                    record.target_player = new AdKat_Player();
                                    record.target_player.player_id = reader.GetInt64(6);
                                }
                                record.source_name = reader.GetString("source_name");
                                record.record_message = reader.GetString("record_message");
                                record.record_time = reader.GetDateTime("record_time");
                            }
                            if (success)
                            {
                                this.DebugWrite("Record found for ID " + record_id, 5);
                            }
                            else
                            {
                                this.DebugWrite("No record found for ID " + record_id, 5);
                            }
                        }
                        if (success && record.target_player != null)
                        {
                            record.target_player = this.fetchPlayer(record.target_player.player_id, null, null, null);
                            record.target_name = record.target_player.player_name;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching record by ID", e));
            }

            DebugWrite("fetchRecordByID finished!", 6);
            return record;
        }

        //DONE
        private List<AdKat_Record> fetchUnreadRecords()
        {
            DebugWrite("fetchUnreadRecords starting!", 6);
            //Create return list

            List<AdKat_Record> records = new List<AdKat_Record>();
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
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
                            `" + this.mySqlDatabaseName + @"`.`adkats_records` 
                        WHERE 
                            `adkats_read` = 'N' 
                        AND 
                            `server_id` = " + this.server_id;
                        command.CommandText = sql;
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            //Grab the record
                            while (reader.Read())
                            {
                                AdKat_Record record = new AdKat_Record();

                                record.record_id = reader.GetInt64("record_id");
                                record.server_id = reader.GetInt64("server_id");
                                record.command_type = this.getDBCommand(reader.GetString("command_type"));
                                record.command_action = this.getDBCommand(reader.GetString("command_action"));
                                record.command_numeric = reader.GetInt32("command_numeric");
                                record.target_name = reader.GetString("target_name");
                                object value = reader.GetValue(6);
                                Int64 target_id_parse = -1;
                                DebugWrite("id fetched!", 6);
                                if (Int64.TryParse(value.ToString(), out target_id_parse))
                                {
                                    DebugWrite("id parsed! " + target_id_parse, 6);
                                    record.target_player = this.fetchPlayer(target_id_parse, null, null, null);
                                    record.target_name = record.target_player.player_name;
                                }
                                else
                                {
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
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching unread records from database.", e));
            }

            DebugWrite("fetchUnreadRecords finished!", 6);
            return records;
        }

        //DONE
        private long fetchNameBanCount()
        {
            DebugWrite("fetchNameBanCount starting!", 6);
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            COUNT(ban_id) AS `ban_count`
                        FROM 
	                        `adkats_banlist` 
                        WHERE 
                            `adkats_banlist`.`ban_enforceName` = 'Y' 
                        AND 
                            `ban_status` = 'Active'";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return reader.GetInt64("ban_count");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching number of name bans.", e));
            }

            DebugWrite("fetchNameBanCount finished!", 6);
            return -1;
        }

        //DONE
        private long fetchGUIDBanCount()
        {
            DebugWrite("fetchGUIDBanCount starting!", 6);
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            COUNT(ban_id) AS `ban_count`
                        FROM 
	                        `adkats_banlist` 
                        WHERE 
                            `adkats_banlist`.`ban_enforceGUID` = 'Y' 
                        AND 
                            `ban_status` = 'Active'";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return reader.GetInt64("ban_count");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching number of GUID bans.", e));
            }

            DebugWrite("fetchGUIDBanCount finished!", 6);
            return -1;
        }

        //DONE
        private long fetchIPBanCount()
        {
            DebugWrite("fetchIPBanCount starting!", 6);
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            COUNT(ban_id) AS `ban_count` 
                        FROM 
	                        `adkats_banlist` 
                        WHERE 
                            `adkats_banlist`.`ban_enforceIP` = 'Y' 
                        AND 
                            `ban_status` = 'Active'";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return reader.GetInt64("ban_count");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching number of IP bans.", e));
            }

            DebugWrite("fetchIPBanCount finished!", 6);
            return -1;
        }

        //DONE
        private void removePlayerAccess(string player_name)
        {
            DebugWrite("removePlayerAccess starting!", 6);
            try
            {
                if (!this.playerAccessCache.ContainsKey(player_name))
                {
                    this.ConsoleError("Player doesn't have any access to remove.");
                    return;
                }
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "DELETE FROM `" + this.mySqlDatabaseName + "`.`adkats_accesslist` WHERE `player_name` = @player_name";
                        //Set values
                        command.Parameters.AddWithValue("@player_name", player_name);
                        //Attempt to execute the query
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while removing player access.", e));
            }
            DebugWrite("removePlayerAccess finished!", 6);
        }

        //DONE
        private void uploadPlayerAccess(AdKat_Access access)
        {
            DebugWrite("uploadPlayerAccess(Email) starting!", 6);
            try
            {
                this.DebugWrite("NEW Access: " + access.player_name + "|" + access.access_level + "|" + access.player_email, 5);

                if (access.access_level < 0 || access.access_level > 6)
                {
                    this.ConsoleError("Desired Access Level for " + access.player_name + " was invalid.");
                    return;
                }
                if (!Regex.IsMatch(access.player_email, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$"))
                {
                    this.ConsoleError(access.player_email + " is an invalid email address.");
                    return;
                }

                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    AdKat_Access oldAccess = null;
                    this.playerAccessCache.TryGetValue(access.player_name, out oldAccess);

                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "INSERT INTO `" + this.mySqlDatabaseName + "`.`adkats_accesslist` (`player_name`, `player_email`, `access_level`) VALUES (@player_name, @player_email, @access_level) ON DUPLICATE KEY UPDATE `player_email` = @player_email, `access_level` = @access_level";
                        //Set values
                        command.Parameters.AddWithValue("@player_name", access.player_name);

                        int access_level = -1;
                        if (access.access_level == -1)
                        {
                            if (oldAccess != null && oldAccess.access_level != -1)
                            {
                                access_level = oldAccess.access_level;
                            }
                            else
                            {
                                access_level = 6;
                            }
                        }
                        else
                        {
                            access_level = access.access_level;
                        }
                        command.Parameters.AddWithValue("@access_level", access_level);

                        string player_email = "InvalidEmail@gmail.com";
                        if (String.IsNullOrEmpty(access.player_email))
                        {
                            if (oldAccess != null)
                            {
                                player_email = oldAccess.player_email;
                            }
                            else
                            {
                                player_email = "test@gmail.com";
                            }
                        }
                        else
                        {
                            player_email = access.player_email;
                        }
                        command.Parameters.AddWithValue("@player_email", player_email);

                        this.DebugWrite("Uploading Access: " + access.player_name + "|" + access.access_level + "|" + access.player_email, 5);
                        //Attempt to execute the query
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while updating player access.", e));
            }

            DebugWrite("uploadPlayerAccess(Email) finished!", 6);
        }

        //DONE
        private Boolean uploadBan(AdKat_Ban aBan)
        {
            DebugWrite("uploadBan starting!", 6);

            Boolean success = false;
            if (aBan == null)
            {
                this.ConsoleError("Ban invalid in uploadBan.");
            }
            else
            {
                try
                {
                    //Upload the inner record if needed
                    if (aBan.ban_record.record_id < 0)
                    {
                        if (!this.uploadRecord(aBan.ban_record))
                        {
                            return false;
                        }
                    }

                    using (MySqlConnection connection = this.getDatabaseConnection())
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                            INSERT INTO 
                            `" + this.mySqlDatabaseName + @"`.`adkats_banlist` 
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
	                            NOW(), 
	                            DATE_ADD(NOW(), INTERVAL @ban_durationMinutes MINUTE), 
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
	                            `ban_startTime` = NOW(), 
	                            `ban_endTime` = DATE_ADD(NOW(), INTERVAL @ban_durationMinutes MINUTE), 
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
                            command.Parameters.AddWithValue("@ban_sync", "*" + this.server_id + "*");
                            //Handle permaban case
                            if (aBan.ban_record.command_action == AdKat_CommandType.PermabanPlayer)
                            {
                                aBan.ban_record.command_numeric = (int)this.permaBanEndTime.Subtract(DateTime.Now).TotalMinutes;
                            }
                            command.Parameters.AddWithValue("@ban_durationMinutes", aBan.ban_record.command_numeric);
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() >= 0)
                            {
                                this.DebugWrite("Success Uploading Ban on player " + aBan.ban_record.target_player.player_id, 5);
                                success = true;
                            }
                        }
                        if (success)
                        {
                            using (MySqlCommand command = connection.CreateCommand())
                            {
                                command.CommandText = @"
                                SELECT 
                                    `ban_id`,
                                    `ban_startTime`, 
                                    `ban_endTime` 
                                FROM 
                                    `adkats_banlist` 
                                WHERE 
                                    `player_id` = @player_id";

                                command.Parameters.AddWithValue("@player_id", aBan.ban_record.target_player.player_id);
                                //Attempt to execute the query
                                using (MySqlDataReader reader = command.ExecuteReader())
                                {
                                    //Grab the ban ID
                                    if (reader.Read())
                                    {
                                        aBan.ban_id = reader.GetInt64("ban_id");
                                        aBan.ban_startTime = reader.GetDateTime("ban_startTime");
                                        aBan.ban_endTime = reader.GetDateTime("ban_endTime");

                                        //Update setting page to reflect the ban count
                                        this.updateSettingPage();
                                        this.DebugWrite("Ban ID: " + aBan.ban_id, 5);
                                    }
                                    else
                                    {
                                        this.ConsoleError("Could not fetch ban information after upload");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.HandleException(new AdKat_Exception("Error while uploading new ban.", e));
                }
            }
            DebugWrite("uploadBan finished!", 6);
            return success;
        }

        //DONE
        private AdKat_Player fetchPlayer(Int64 player_id, String player_name, String player_guid, String player_ip)
        {
            DebugWrite("fetchPlayer starting!", 6);
            //Create return list
            AdKat_Player player = null;
            if (player_id < 0 && String.IsNullOrEmpty(player_name) && String.IsNullOrEmpty(player_guid) && String.IsNullOrEmpty(player_ip))
            {
                this.ConsoleError("Attempted to fetch player with no information.");
            }
            else
            {
                try
                {
                    using (MySqlConnection connection = this.getDatabaseConnection())
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            String sql = @"
                            SELECT 
                                `PlayerID` as `player_id`, 
                                `SoldierName` as `player_name`, 
                                `EAGUID` as `player_guid`, 
                                `PBGUID` as `player_pbguid`, 
                                `IP_Address` as `player_ip` 
                            FROM `" + this.mySqlDatabaseName + @"`.`tbl_playerdata` ";
                            bool sqlender = true;
                            if (player_id >= 0)
                            {
                                if (sqlender)
                                {
                                    sql += " WHERE (";
                                    sqlender = false;
                                }
                                sql += " `PlayerID` LIKE '" + player_id + "'";
                            }
                            if (!String.IsNullOrEmpty(player_guid))
                            {
                                if (sqlender)
                                {
                                    sql += " WHERE (";
                                    sqlender = false;
                                }
                                else
                                {
                                    sql += " OR ";
                                }
                                sql += " `EAGUID` LIKE '" + player_guid + "'";
                            }
                            if (String.IsNullOrEmpty(player_guid) && !String.IsNullOrEmpty(player_name))
                            {
                                if (sqlender)
                                {
                                    sql += " WHERE (";
                                    sqlender = false;
                                }
                                else
                                {
                                    sql += " OR ";
                                }
                                sql += " `SoldierName` LIKE '" + player_name + "'";
                            }
                            if (String.IsNullOrEmpty(player_guid) && !String.IsNullOrEmpty(player_ip))
                            {
                                if (sqlender)
                                {
                                    sql += " WHERE (";
                                    sqlender = false;
                                }
                                else
                                {
                                    sql += " OR ";
                                }
                                sql += " `IP_Address` LIKE '" + player_ip + "'";
                            }
                            if (!sqlender)
                            {
                                sql += ")";
                            }
                            command.CommandText = sql;
                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    player = new AdKat_Player();
                                    //Player ID will never be null
                                    player.player_id = reader.GetInt64("player_id");
                                    if (!reader.IsDBNull(1))
                                        player.player_name = reader.GetString("player_name");
                                    if (!reader.IsDBNull(2))
                                        player.player_guid = reader.GetString("player_guid");
                                    if (!reader.IsDBNull(3))
                                        player.player_pbguid = reader.GetString("player_pbguid");
                                    if (!reader.IsDBNull(4))
                                        player.player_ip = reader.GetString("player_ip");
                                }
                                else
                                {
                                    this.DebugWrite("No player matching search information.", 5);
                                }
                            }
                        }
                        if (player == null)
                        {
                            using (MySqlCommand command = connection.CreateCommand())
                            {
                                //Set the insert command structure
                                command.CommandText = @"
                                INSERT INTO `" + this.mySqlDatabaseName + @"`.`tbl_playerdata` 
                                (
                                    `SoldierName`, 
                                    `EAGUID`, 
                                    `IP_Address`
                                ) 
                                VALUES 
                                (
                                    '" + player_name + @"', 
                                    '" + player_guid + @"',
                                    '" + player_ip + @"'
                                )
                                ON DUPLICATE KEY 
                                UPDATE 
                                    `PlayerID` = LAST_INSERT_ID(`PlayerID`),
                                    `SoldierName` = '" + player_name + @"',
                                    `EAGUID` = '" + player_guid + @"',
                                    `IP_Address` = '" + player_ip + "'";
                                //Attempt to execute the query
                                if (command.ExecuteNonQuery() > 0)
                                {
                                    player = new AdKat_Player();
                                    player.player_id = command.LastInsertedId;
                                    player.player_name = player_name;
                                    player.player_guid = player_guid;
                                    player.player_ip = player_ip;
                                }
                            }
                        }
                        if (!String.IsNullOrEmpty(player_name) && !String.IsNullOrEmpty(player.player_guid) && !player_name.Equals(player.player_name))
                        {
                            this.DebugWrite(player.player_name + " changed their name to " + player_name + ". Updating the database.", 2);
                            using (MySqlCommand command = connection.CreateCommand())
                            {
                                //Set the insert command structure
                                command.CommandText = @"UPDATE `" + this.mySqlDatabaseName + @"`.`tbl_playerdata` SET `SoldierName` = '" + player_name + "' WHERE `EAGUID` = '" + player.player_guid + "'";
                                //Attempt to execute the query
                                if (command.ExecuteNonQuery() <= 0)
                                {
                                    this.ConsoleError("Could not update " + player.player_name + "'s name-change to " + player_name + " in the database.");
                                }
                                //Update the player name in the player object
                                player.player_name = player_name;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.HandleException(new AdKat_Exception("Error while fetching player.", e));
                }

            }
            DebugWrite("fetchPlayer finished!", 6);
            return player;
        }

        private AdKat_Player updatePlayer(AdKat_Player player)
        {
            DebugWrite("updatePlayer starting!", 6);
            if (player == null || player.player_id < 0 || String.IsNullOrEmpty(player.player_name) || String.IsNullOrEmpty(player.player_guid) || String.IsNullOrEmpty(player.player_ip))
            {
                this.ConsoleError("Attempted to updated player without required information.");
            }
            else
            {
                try
                {
                    using (MySqlConnection connection = this.getDatabaseConnection())
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            //Set the insert command structure
                            command.CommandText = @"
                            UPDATE `" + this.mySqlDatabaseName + @"`.`tbl_playerdata` SET 
                                `SoldierName` = '" + player.player_name + @"',
                                `EAGUID` = '" + player.player_guid + @"',
                                `IP_Address` = '" + player.player_ip + @"'
                            WHERE `tbl_playerdata`.`PlayerID` = " + player.player_id;
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() > 0)
                            {
                                this.DebugWrite("Update player info success.", 5);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.HandleException(new AdKat_Exception("Error while updating player.", e));
                }

            }
            DebugWrite("updatePlayer finished!", 6);
            return player;
        }

        //DONE
        private AdKat_Ban fetchPlayerBan(AdKat_Player player)
        {
            DebugWrite("fetchPlayerBan starting!", 6);

            AdKat_Ban aBan = null;
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        //Build the query
                        string query = @"
                        SELECT 
                            `adkats_banlist`.`ban_id`,
                            `adkats_banlist`.`player_id`, 
                            `adkats_banlist`.`latest_record_id`, 
                            `adkats_banlist`.`ban_status`, 
                            `adkats_banlist`.`ban_notes`, 
                            `adkats_banlist`.`ban_startTime`, 
                            `adkats_banlist`.`ban_endTime`, 
                            `adkats_banlist`.`ban_enforceName`, 
                            `adkats_banlist`.`ban_enforceGUID`, 
                            `adkats_banlist`.`ban_enforceIP`, 
                            `adkats_banlist`.`ban_sync`
                        FROM 
                            `adkats_banlist` 
                        INNER JOIN 
                            `tbl_playerdata` 
                        ON 
                            `tbl_playerdata`.`PlayerID` = `adkats_banlist`.`player_id` 
                        WHERE 
                            `adkats_banlist`.`ban_status` = 'Active' 
                        AND 
                        (";
                        Boolean started = false;
                        if (!String.IsNullOrEmpty(player.player_name))
                        {
                            started = true;
                            query += "(`tbl_playerdata`.`SoldierName` = '" + player.player_name + @"' AND `adkats_banlist`.`ban_enforceName` = 'Y')";
                        }
                        if (!String.IsNullOrEmpty(player.player_guid))
                        {
                            if (started)
                            {
                                query += " OR ";
                            }
                            started = true;
                            query += "(`tbl_playerdata`.`EAGUID` = '" + player.player_guid + "' AND `adkats_banlist`.`ban_enforceGUID` = 'Y')";
                        }
                        if (!String.IsNullOrEmpty(player.player_ip))
                        {
                            if (started)
                            {
                                query += " OR ";
                            }
                            started = true;
                            query += "(`tbl_playerdata`.`IP_Address` = '" + player.player_ip + "' AND `adkats_banlist`.`ban_enforceIP` = 'Y')";
                        }
                        if (!started)
                        {
                            this.HandleException(new AdKat_Exception("No data to fetch ban with. This should never happen."));
                            return null;
                        }
                        query += ")";

                        //Assign the query
                        command.CommandText = query;

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            Boolean fetchedFirstBan = false;
                            if (reader.Read())
                            {
                                fetchedFirstBan = true;
                                //Create the ban element
                                aBan = new AdKat_Ban();
                                aBan.ban_id = reader.GetInt64("ban_id");
                                aBan.ban_status = reader.GetString("ban_status");
                                aBan.ban_notes = reader.GetString("ban_notes");
                                aBan.ban_sync = reader.GetString("ban_sync");
                                aBan.ban_startTime = reader.GetDateTime("ban_startTime");
                                aBan.ban_endTime = reader.GetDateTime("ban_endTime");

                                if (reader.GetString("ban_enforceName").Equals("Y"))
                                    aBan.ban_enforceName = true;
                                else
                                    aBan.ban_enforceName = false;

                                if (reader.GetString("ban_enforceGUID").Equals("Y"))
                                    aBan.ban_enforceGUID = true;
                                else
                                    aBan.ban_enforceGUID = false;

                                if (reader.GetString("ban_enforceIP").Equals("Y"))
                                    aBan.ban_enforceIP = true;
                                else
                                    aBan.ban_enforceIP = false;

                                //Get the record information
                                aBan.ban_record = this.fetchRecordByID(reader.GetInt64("latest_record_id"));
                            }
                            if (reader.Read() && fetchedFirstBan)
                            {
                                this.ConsoleWarn("Multiple banned players matched ban information, possible duplicate account");
                            }
                        }
                    }
                    //If bans were fetched successfully, update the ban lists and sync back
                    if (aBan != null)
                    {
                        long totalSeconds = (long)this.convertToProconTime(aBan.ban_endTime).Subtract(DateTime.Now).TotalSeconds;
                        if (totalSeconds < 0)
                        {
                            aBan.ban_status = "Expired";
                            //Update the sync for this ban
                            this.updateBanStatus(aBan);
                            return null;
                        }
                        else
                        {
                            //Update the sync for this ban
                            this.updateBanStatus(aBan);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching player ban.", e));
            }
            return aBan;
        }

        //DONE
        private void repopulateProconBanList()
        {
            DebugWrite("repopulateProconBanList starting!", 6);
            this.ConsoleWarn("Downloading bans from database, please wait. This might take several minutes depending on your ban count!");

            double totalBans = 0;
            double bansRepopulated = 0;
            Boolean earlyExit = false;
            DateTime startTime = DateTime.Now;

            List<AdKat_Ban> updatedBans = new List<AdKat_Ban>();
            try
            {
                //Success fetching bans
                Boolean success = false;
                List<AdKat_Ban> tempBanList = new List<AdKat_Ban>();

                using (MySqlConnection connection = this.getDatabaseConnection())
                {

                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            COUNT(*) AS `ban_count`
                        FROM 
	                        `adkats_banlist`";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                totalBans = reader.GetInt64("ban_count");
                            }
                        }
                    }
                    if (totalBans < 1)
                    {
                        return;
                    }
                    using (MySqlCommand command = connection.CreateCommand())
                    {
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
	                        `adkats_banlist`";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            //Loop through all incoming bans
                            long totalBanSeconds;
                            while (reader.Read())
                            {
                                //Break from the loop if the plugin is disabled or the setting is reverted.
                                if (!this.isEnabled || this.useBanEnforcer)
                                {
                                    this.ConsoleWarn("You exited the ban download process early, the process was not completed.");
                                    earlyExit = true;
                                    break;
                                }

                                //Bans have been found
                                success = true;

                                //Create the ban element
                                AdKat_Ban aBan = new AdKat_Ban();
                                aBan.ban_id = reader.GetInt64("ban_id");
                                aBan.ban_status = reader.GetString("ban_status");
                                aBan.ban_notes = reader.GetString("ban_notes");
                                aBan.ban_sync = reader.GetString("ban_sync");
                                aBan.ban_startTime = reader.GetDateTime("ban_startTime");
                                aBan.ban_endTime = reader.GetDateTime("ban_endTime");

                                //Get the record information
                                aBan.ban_record = this.fetchRecordByID(reader.GetInt64("latest_record_id"));

                                totalBanSeconds = (long)this.convertToProconTime(aBan.ban_endTime).Subtract(DateTime.Now).TotalSeconds;
                                if (totalBanSeconds > 0)
                                {
                                    this.DebugWrite("Re-ProconBanning: " + aBan.ban_record.target_player.player_name + " for " + totalBanSeconds + "sec for " + aBan.ban_record.record_message, 4);

                                    //Push the name ban
                                    if (reader.GetString("ban_enforceName").Equals("Y"))
                                    {
                                        Thread.Sleep(75);
                                        //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                        if (totalBanSeconds > 0 && totalBanSeconds < 31536000)
                                        {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "name", aBan.ban_record.target_player.player_name, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                        }
                                        else
                                        {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "name", aBan.ban_record.target_player.player_name, "perm", aBan.ban_record.record_message);
                                        }
                                    }

                                    //Push the guid ban
                                    if (reader.GetString("ban_enforceGUID").Equals("Y"))
                                    {
                                        Thread.Sleep(75);
                                        //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                        if (totalBanSeconds > 0 && totalBanSeconds < 31536000)
                                        {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "guid", aBan.ban_record.target_player.player_guid, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                        }
                                        else
                                        {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "guid", aBan.ban_record.target_player.player_guid, "perm", aBan.ban_record.record_message);
                                        }
                                    }

                                    //Push the IP ban
                                    if (reader.GetString("ban_enforceIP").Equals("Y"))
                                    {
                                        Thread.Sleep(75);
                                        //Permabans and Temp bans longer than 1 year will be defaulted to permaban
                                        if (totalBanSeconds > 0 && totalBanSeconds < 31536000)
                                        {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "ip", aBan.ban_record.target_player.player_ip, "seconds", totalBanSeconds + "", aBan.ban_record.record_message);
                                        }
                                        else
                                        {
                                            this.ExecuteCommand("procon.protected.send", "banList.add", "ip", aBan.ban_record.target_player.player_ip, "perm", aBan.ban_record.record_message);
                                        }
                                    }
                                }

                                if (++bansRepopulated % 15 == 0)
                                {
                                    this.ConsoleWrite(Math.Round(100 * bansRepopulated / totalBans, 2) + "% of bans repopulated. AVG " + Math.Round(bansRepopulated / ((DateTime.Now - startTime).TotalSeconds), 2) + " downloads/sec.");
                                }
                            }
                            this.ExecuteCommand("procon.protected.send", "banList.save");
                            this.ExecuteCommand("procon.protected.send", "banList.list");
                            if (!earlyExit)
                            {
                                this.ConsoleSuccess("All AdKats Enforced bans repopulated to procon's ban list.");
                            }

                            //Update the last db ban fetch time
                            this.lastDBBanFetch = DateTime.Now;

                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while repopulating procon banlist.", e));
            }
            return;
        }

        //DONE
        private Boolean updateBanStatus(AdKat_Ban aBan)
        {
            DebugWrite("updateBanStatus starting!", 6);

            Boolean success = false;
            if (aBan == null)
            {
                this.ConsoleError("Ban invalid in updateBanStatus.");
            }
            else
            {
                try
                {
                    //Conditionally modify the ban_sync for this server
                    if (!aBan.ban_sync.Contains("*" + this.server_id + "*"))
                    {
                        aBan.ban_sync += ("*" + this.server_id + "*");
                    }

                    using (MySqlConnection connection = this.getDatabaseConnection())
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            string query = @"
                            UPDATE 
                            `" + this.mySqlDatabaseName + @"`.`adkats_banlist` 
                            SET 
                            `ban_sync` = '" + aBan.ban_sync + @"', 
                            `ban_status` = '" + aBan.ban_status + @"'
                            WHERE 
                            `ban_id` = " + aBan.ban_id;
                            command.CommandText = query;
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() > 0)
                            {
                                success = true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.HandleException(new AdKat_Exception("Error while updating status of ban.", e));
                }
            }

            DebugWrite("updateBanStatus finished!", 6);
            return success;
        }

        //DONE
        private void updateDB_0251_0300()
        {
            if (!this.confirmTable("adkat_records"))
            {
                this.DebugWrite("No tables from previous versions. No need for database update.", 3);
                return;
            }

            try
            {
                //Get record count from current version table
                int currentRecordCount = 0;
                //initial record ID. Fetching 250 records at a time.
                Int64 initial_record_id = 0;
                Int64 importCount = 0;
                Int64 uploadCount = 0;

                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            SELECT 
                                COUNT(*) AS `record_count` 
                            FROM 
	                            `adkats_records`";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                currentRecordCount = reader.GetInt32("record_count");
                            }
                            else
                            {
                                this.ConsoleError("Unable to fetch current record count.");
                            }
                        }
                    }
                }

                if (currentRecordCount == 0)
                {
                    this.ConsoleWarn("Updating records from previous versions to 0.3.0.0! Do not turn off AdKats until it's finished!");
                    List<AdKat_Record> newRecords = new List<AdKat_Record>();
                    do
                    {
                        newRecords = new List<AdKat_Record>();

                        using (MySqlConnection connection = this.getDatabaseConnection())
                        {
                            this.ConsoleWrite("creating connection to update records");

                            using (MySqlCommand command = connection.CreateCommand())
                            {
                                this.ConsoleWrite("creating fetch command");
                                command.CommandText = @"
                                SELECT 
                                    `record_id`,
                                    `tbl_server`.`ServerID` AS `server_id`,
                                    `command_type`, 
                                    `command_action`, 
                                    `record_durationMinutes`, 
                                    `target_guid`, 
                                    `target_name`, 
                                    `source_name`, 
                                    `record_message`, 
                                    `record_time`
                                FROM 
                                    `adkat_records` 
                                INNER JOIN 
                                    `tbl_server` 
                                ON
                                    `adkat_records`.`server_ip` = `tbl_server`.`IP_Address`
                                WHERE 
                                    `record_id` > @initial_record_id 
                                ORDER BY 
                                    `record_id` ASC 
                                LIMIT 
                                    50";

                                command.Parameters.AddWithValue("@initial_record_id", initial_record_id);

                                this.ConsoleWrite("reading input");
                                using (MySqlDataReader reader = command.ExecuteReader())
                                {
                                    //Loop through all incoming bans
                                    while (reader.Read())
                                    {
                                        //Pull record ID to post as new greatest ID
                                        initial_record_id = reader.GetInt64("record_id");

                                        AdKat_Record record = new AdKat_Record();
                                        //Get server information
                                        record.server_id = reader.GetInt64("server_id");
                                        //Get command information
                                        record.command_type = this.getDBCommand(reader.GetString("command_type"));
                                        record.command_action = this.getDBCommand(reader.GetString("command_action"));
                                        record.command_numeric = reader.GetInt32("record_durationMinutes");
                                        //Get source information
                                        record.source_name = reader.GetString("source_name");
                                        //Get target information
                                        record.target_player = this.fetchPlayer(-1, reader.GetString("target_name"), reader.GetString("target_guid"), null);
                                        //Get general record information
                                        record.record_message = reader.GetString("record_message");
                                        record.record_time = reader.GetDateTime("record_time");

                                        //Push to lists
                                        newRecords.Add(record);

                                        //Increase the import count
                                        importCount++;
                                    }
                                    this.ConsoleWrite(importCount + " records downloaded...");
                                }
                            }
                        }

                        foreach (AdKat_Record record in newRecords)
                        {
                            if (!this.uploadRecord(record))
                            {
                                return;
                            }

                            uploadCount++;
                        }
                        this.ConsoleWrite(uploadCount + " records uploaded...");
                    } while (newRecords.Count > 0);
                }

                this.ConsoleSuccess(uploadCount + " records imported from previous versions of AdKats!");

                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    //Get player access count from current version table
                    int currentAccessCount = 0;
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            COUNT(*) AS `access_count` 
                        FROM 
	                        `adkats_accesslist`";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                currentAccessCount = reader.GetInt32("access_count");
                            }
                            else
                            {
                                this.ConsoleError("Unable to fetch current access count.");
                            }
                        }
                    }
                    if (currentAccessCount == 0)
                    {
                        this.ConsoleWarn("Updating player access from previous versions to 0.3.0.0! Do not turn off AdKats until it's finished!");

                        List<AdKat_Access> newAccess = new List<AdKat_Access>();

                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                            SELECT 
                                `player_name`,
                                `access_level`
                            FROM 
                                `adkat_accesslist`";

                            using (MySqlDataReader reader = command.ExecuteReader())
                            {
                                importCount = 0;

                                //Loop through all incoming bans
                                while (reader.Read())
                                {
                                    AdKat_Access access = new AdKat_Access();
                                    access.player_name = reader.GetString("player_name");
                                    access.access_level = reader.GetInt32("access_level");
                                    access.member_id = 0;
                                    access.player_email = "test@gmail.com";

                                    //Push to lists
                                    newAccess.Add(access);

                                    if ((++importCount % 500) == 0)
                                    {
                                        this.ConsoleWrite(importCount + " access entries downloaded...");
                                    }
                                }
                                this.ConsoleWrite(importCount + " access entries downloaded...");
                            }
                        }

                        uploadCount = 0;
                        foreach (AdKat_Access access in newAccess)
                        {
                            this.uploadPlayerAccess(access);

                            if ((++uploadCount % 500) == 0)
                            {
                                this.ConsoleWrite(uploadCount + " access entries uploaded...");
                            }
                        }
                        this.ConsoleSuccess(uploadCount + " access entries imported from previous versions of AdKats!");

                        //Fetch the updated access list
                        this.fetchAccessList();
                    }
                }
                this.updateSettingPage();
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while updating database from 2.5.1 to 3.0+.", e));
            }
        }

        //DONE
        private void importBansFromBBM5108()
        {
            //Check if tables exist from BF3 Ban Manager
            if (!this.confirmTable("bm_banlist"))
            {
                return;
            }
            this.ConsoleWarn("BF3 Ban Manager tables detected. Checking validity....");

            //Check if any BBM5108 bans exist in the AdKats Banlist
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            * 
                        FROM 
                            `" + this.mySqlDatabaseName + @"`.`adkats_banlist` 
                        WHERE 
                            `adkats_banlist`.`ban_notes` = 'BBM5108' 
                        LIMIT 1";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                this.ConsoleWarn("BF3 Ban Manager bans already imported, canceling import.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while checking for BBM Bans.", e));
                return;
            }

            this.ConsoleWarn("Validity confirmed. Preparing to fetch all BF3 Ban Manager Bans...");
            double totalBans = 0;
            double bansImported = 0;
            Queue<BBM5108_Ban> inboundBBMBans = new Queue<BBM5108_Ban>();
            DateTime startTime = DateTime.Now;
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
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

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            BBM5108_Ban BBMBan;
                            Boolean told = false;
                            while (reader.Read())
                            {
                                if (!told)
                                {
                                    this.DebugWrite("BBM5108 bans found, grabbing.", 3);
                                }
                                BBMBan = new BBM5108_Ban();
                                BBMBan.soldiername = reader.IsDBNull(reader.GetOrdinal("soldiername")) ? null : reader.GetString("soldiername");
                                BBMBan.eaguid = reader.IsDBNull(reader.GetOrdinal("eaguid")) ? null : reader.GetString("eaguid");
                                BBMBan.ban_length = reader.GetString("ban_length");
                                BBMBan.ban_duration = reader.GetDateTime("ban_duration");
                                BBMBan.ban_reason = reader.IsDBNull(reader.GetOrdinal("ban_reason")) ? null : reader.GetString("ban_reason");
                                inboundBBMBans.Enqueue(BBMBan);
                                totalBans++;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching BBM Bans.", e));
                return;
            }
            this.ConsoleWarn(totalBans + " Ban Manager bans fetched, starting import to AdKats Ban Enforcer...");

            try
            {
                //Loop through all BBMBans in order that they came in
                AdKat_Ban aBan;
                AdKat_Record record;
                while (inboundBBMBans.Count > 0)
                {
                    //Break from the loop if the plugin is disabled or the setting is reverted.
                    if (!this.isEnabled || !this.useBanEnforcer)
                    {
                        this.ConsoleError("You exited the ban import process process early, the process was not completed and cannot recover without manual override. Talk to ColColonCleaner.");
                        break;
                    }

                    BBM5108_Ban BBMBan = inboundBBMBans.Dequeue();

                    //Create the record
                    record = new AdKat_Record();
                    //Fetch the player
                    record.target_player = this.fetchPlayer(-1, BBMBan.soldiername, BBMBan.eaguid, null);

                    record.command_source = AdKat_CommandSource.InGame;
                    if (BBMBan.ban_length == "permanent")
                    {
                        this.DebugWrite("Ban is permanent", 4);
                        record.command_type = AdKat_CommandType.PermabanPlayer;
                        record.command_action = AdKat_CommandType.PermabanPlayer;
                        record.command_numeric = 0;
                    }
                    else if (BBMBan.ban_length == "seconds")
                    {
                        this.DebugWrite("Ban is temporary", 4);
                        record.command_type = AdKat_CommandType.TempBanPlayer;
                        record.command_action = AdKat_CommandType.TempBanPlayer;
                        record.command_numeric = (int)(BBMBan.ban_duration - DateTime.UtcNow).TotalMinutes;
                    }
                    else
                    {
                        //Ignore all other cases e.g. round bans
                        this.DebugWrite("Ban type '" + BBMBan.ban_length + "' not usable", 3);
                        continue;
                    }

                    record.source_name = "BanEnforcer";
                    record.server_id = this.server_id;
                    if (!String.IsNullOrEmpty(record.target_player.player_name))
                    {
                        record.target_name = record.target_player.player_name;
                    }
                    record.isIRO = false;
                    record.record_message = BBMBan.ban_reason;
                    //Create the ban
                    aBan = new AdKat_Ban();
                    aBan.ban_record = record;
                    aBan.ban_notes = "BBM5108";

                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean GUIDAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean IPAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);
                    aBan.ban_enforceName = nameAvailable && (this.defaultEnforceName || (!GUIDAvailable && !IPAvailable) || !String.IsNullOrEmpty(BBMBan.soldiername));
                    aBan.ban_enforceGUID = GUIDAvailable && (this.defaultEnforceGUID || (!nameAvailable && !IPAvailable) || !String.IsNullOrEmpty(BBMBan.eaguid));
                    aBan.ban_enforceIP = IPAvailable && this.defaultEnforceIP;
                    if (!aBan.ban_enforceName && !aBan.ban_enforceGUID && !aBan.ban_enforceIP)
                    {
                        this.ConsoleError("Unable to create ban, no proper player information");
                        continue;
                    }

                    //Upload the ban
                    this.DebugWrite("Uploading Ban Manager ban.", 5);
                    this.uploadBan(aBan);

                    if (++bansImported % 25 == 0)
                    {
                        this.ConsoleWrite(Math.Round(100 * bansImported / totalBans, 2) + "% of Ban Manager bans uploaded. AVG " + Math.Round(bansImported / ((DateTime.Now - startTime).TotalSeconds), 2) + " uploads/sec.");
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while processing imported BBM Bans to AdKats banlist.", e));
                return;
            }
            if (inboundBBMBans.Count == 0)
            {
                this.ConsoleSuccess("All Ban Manager bans imported into AdKats Ban Enforcer!");
            }
        }

        //TODO
        private Boolean canPunish(AdKat_Record record)
        {
            DebugWrite("canPunish starting!", 6);
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            `record_time` AS `latest_time` 
                        FROM 
                            `" + this.mySqlDatabaseName + @"`.`adkats_records` 
                        WHERE 
                            `adkats_records`.`command_type` = 'Punish' 
                        AND 
                            `adkats_records`.`target_id` = " + record.target_player.player_id + @" 
                        AND 
                            DATE_ADD(`record_time`, INTERVAL 20 SECOND) > NOW() 
                        ORDER BY 
                            `record_time` 
                        DESC LIMIT 1";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                this.DebugWrite("can't upload punish", 6);
                                this.sendMessageToSource(record, record.target_name + " already punished in the last 20 seconds.");
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while checking if player can be punished.", e));
                //Assume false if any errors
                return false;
            }
        }

        //DONE
        private Boolean isDoubleCounted(AdKat_Record record)
        {
            DebugWrite("isDoubleCounted starting!", 6);

            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        SELECT 
                            `record_time` AS `latest_time` 
                        FROM 
                            `" + this.mySqlDatabaseName + @"`.`adkats_records` 
                        WHERE 
                            `adkats_records`.`command_type` = 'Punish' 
                        AND 
                            `adkats_records`.`target_id` = " + record.target_player.player_id + @" 
                        AND 
                            DATE_ADD(`record_time`, INTERVAL 5 MINUTE) > NOW() 
                        ORDER BY 
                            `record_time` 
                        DESC LIMIT 1";

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                this.DebugWrite("Punish is double counted", 6);
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while checking if punish will be IRO.", e));
                //Assume false if any errors
                return false;
            }
        }

        //DONE
        private void runActionsFromDB()
        {
            DebugWrite("runActionsFromDB starting!", 7);
            try
            {
                foreach (AdKat_Record record in this.fetchUnreadRecords())
                {
                    this.queueRecordForActionHandling(record);
                }
                //Update the last time this was fetched
                this.lastDBActionFetch = DateTime.Now;
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while queueing unread records for action handling.", e));
            }
        }

        //DONE
        private int fetchPoints(AdKat_Player player)
        {
            DebugWrite("fetchPoints starting!", 6);

            int returnVal = -1;

            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        if (this.combineServerPunishments)
                        {
                            command.CommandText = @"SELECT `total_points` FROM `" + this.mySqlDatabaseName + @"`.`adkats_globalPlayerPoints` WHERE `player_id` = @player_id";
                            command.Parameters.AddWithValue("@player_id", player.player_id);
                        }
                        else
                        {
                            command.CommandText = @"SELECT `total_points` FROM `" + this.mySqlDatabaseName + @"`.`adkats_serverPlayerPoints` WHERE `player_id` = @player_id and `server_id` = @server_id";
                            command.Parameters.AddWithValue("@player_id", player.player_id);
                            command.Parameters.AddWithValue("@server_id", this.server_id);
                        }
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                returnVal = reader.GetInt32("total_points");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while getting infraction points for player.", e));
            }
            DebugWrite("fetchPoints finished!", 6);
            return returnVal;
        }

        //IN Progress
        private void fetchAccessList()
        {
            DebugWrite("fetchAccessList starting!", 6);

            Boolean success = false;
            Dictionary<String, AdKat_Access> tempAccessCache = new Dictionary<String, AdKat_Access>();
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    List<string> namesToGUIDUpdate = new List<string>();
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT `player_name`, `member_id`, `player_email`, `access_level` FROM `" + this.mySqlDatabaseName + "`.`adkats_accesslist` ORDER BY `access_level` ASC";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                success = true;
                                AdKat_Access access = new AdKat_Access();
                                access.player_name = reader.GetString("player_name");
                                access.member_id = reader.GetInt32("member_id");
                                access.player_email = reader.GetString("player_email");
                                access.access_level = reader.GetInt32("access_level");
                                if (!String.IsNullOrEmpty(access.player_name))
                                {
                                    if (this.soldierNameValid(access.player_name))
                                    {
                                        //Add to the access cache
                                        tempAccessCache.Add(access.player_name, access);
                                        DebugWrite("Admin found: " + access.player_name, 6);
                                    }
                                }
                                else
                                {
                                    this.ConsoleError("Blank admin name found in database, ignoring that entry.");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching access list.", e));
            }

            //Update the access cache
            this.playerAccessCache = tempAccessCache;
            //Update the last update time
            this.lastDBAccessFetch = DateTime.Now;
            if (success)
            {
                this.DebugWrite("Access List Fetched from Database. Access Count: " + this.playerAccessCache.Count, 1);
                //Update MULTIBalancer Whitelists
                this.updateMULTIBalancerWhitelist();
                //Update Server Reserved Slots
                this.updateReservedSlots();
            }
            else
            {
                this.ConsoleWarn("No admins in the admin table.");
            }

            DebugWrite("fetchAccessList finished!", 6);
        }

        //DONE
        private Boolean isAdminAssistant(AdKat_Player player)
        {
            DebugWrite("fetchAdminAssistants starting!", 6);
            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                    SELECT
                        'isAdminAssistant' 
                    FROM 
                        `" + this.mySqlDatabaseName + @"`.`adkats_records`
                    WHERE (
	                    SELECT count(`command_action`) 
	                    FROM `adkats_records` 
	                    WHERE `command_action` = 'ConfirmReport' 
	                    AND `source_name` = '" + player.player_name + @"' 
	                    AND (`adkats_records`.`record_time` BETWEEN date_sub(now(),INTERVAL 30 DAY) AND now())
                    ) >= " + this.minimumRequiredWeeklyReports + " LIMIT 1";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            return reader.Read();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while checking if player is an admin assistant.", e));
            }
            DebugWrite("fetchAdminAssistants finished!", 6);
            return false;
        }

        //DONE
        private Int64 fetchServerID()
        {
            DebugWrite("fetchServerID starting!", 6);

            Int64 returnVal = -1;

            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"SELECT `ServerID` as `server_id` FROM `tbl_server` WHERE IP_Address = @IP_Address";
                        command.Parameters.AddWithValue("@IP_Address", this.server_ip);
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                returnVal = reader.GetInt64("server_id");
                                if (this.server_id != -1)
                                {
                                    this.DebugWrite("Attempted server ID update after ID already chosen.", 5);
                                }
                                else
                                {
                                    this.server_id = returnVal;
                                    this.settingImportID = this.server_id;
                                    this.DebugWrite("Server ID fetched: " + this.server_id, 1);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching server ID from database.", e));
            }

            DebugWrite("fetchServerID finished!", 6);

            return returnVal;
        }

        //DONE
        private TimeSpan fetchDBTimeConversion()
        {
            DebugWrite("fetchDBTimeConversion starting!", 6);

            TimeSpan returnVal = new TimeSpan(0);

            try
            {
                using (MySqlConnection connection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"SELECT NOW() AS `current_time`";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                //Get the db time
                                DateTime dbTime = reader.GetDateTime("current_time");
                                DateTime proconTime = DateTime.Now;
                                //Calculate the difference between database time and procon time
                                this.dbTimeConversion = proconTime.Subtract(dbTime);
                                returnVal = this.dbTimeConversion;

                                this.DebugWrite("Time conversion fetched: PT(" + proconTime + ") - DT(" + dbTime + ") = (" + this.dbTimeConversion + ")", 2);
                            }
                            else
                            {
                                this.ConsoleError("Could not fetch database time conversion");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while fetching database time conversion.", e));
            }

            DebugWrite("fetchDBTimeConversion finished!", 6);

            return returnVal;
        }

        #endregion

        #region MULTIBalancer Whitelisting

        private void updateMULTIBalancerWhitelist()
        {
            try
            {
                if (this.feedMULTIBalancerWhitelist)
                {
                    List<string> access05 = new List<string>();
                    foreach (AdKat_Access access in this.playerAccessCache.Values)
                    {
                        if (access.access_level <= 5)
                        {
                            access05.Add(access.player_name);
                        }
                    }
                    this.setExternalPluginSetting("MULTIbalancer", "1 - Settings|Whitelist", CPluginVariable.EncodeStringArray(access05.ToArray()));
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while updating MULTIBalancer whitelist.", e));
            }
        }

        #endregion

        #region Server Reserved Slots

        private void updateReservedSlots()
        {
            try
            {
                if (this.feedServerReservedSlots && this.playerAccessCache.Count > 0 && this.currentReservedSlotPlayers != null)
                {
                    this.DebugWrite("Checking validity of reserve slotted players.", 6);
                    //Check for differences between the two lists
                    foreach (string player_name in this.playerAccessCache.Keys)
                    {
                        if (!this.currentReservedSlotPlayers.Contains(player_name))
                        {
                            this.DebugWrite(player_name + " in AdKats access, but not in reserved slots. Adding.", 3);
                            this.ExecuteCommand("procon.protected.send", "reservedSlotsList.add", player_name);
                            Thread.Sleep(50);
                        }
                    }
                    foreach (string player_name in this.currentReservedSlotPlayers)
                    {
                        if (!this.playerAccessCache.ContainsKey(player_name))
                        {
                            this.DebugWrite(player_name + " in reserved slots, but not AdKats Access List. Removing.", 3);
                            this.ExecuteCommand("procon.protected.send", "reservedSlotsList.remove", player_name);
                            Thread.Sleep(50);
                        }
                    }
                    this.ExecuteCommand("procon.protected.send", "reservedSlotsList.save");
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while updating server reserved slots.", e));
            }
        }

        public override void OnReservedSlotsList(List<string> soldierNames) 
        {
            try
            {
                this.DebugWrite("Reserved slots listed.", 6);
                this.currentReservedSlotPlayers = soldierNames;
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while handling reserved slot list.", e));
            }
        }

        #endregion

        #endregion

        #region HTTP Server Handling

        public override HttpWebServerResponseData OnHttpRequest(HttpWebServerRequestData data)
        {
            string responseString = "AdKats Remote: ";
            try
            {
                /*foreach (String key in data.POSTData.AllKeys)
                {
                    this.DebugWrite("POST Key: " + key + " val: " + data.Headers[key], 6);
                }*/
                foreach (String key in data.Query.AllKeys)
                {
                    this.DebugWrite("Query Key: " + key + " val: " + data.Query[key], 6);
                }
                this.DebugWrite("method: " + data.Method, 6);
                //this.DebugWrite("doc: " + data.Document, 6);
                AdKat_Record record = new AdKat_Record();
                record.command_source = AdKat_CommandSource.HTTP;

                NameValueCollection dataCollection = null;
                if (String.Compare(data.Method, "GET", true) == 0)
                {
                    dataCollection = data.Query;
                }
                else if (String.Compare(data.Method, "POST", true) == 0)
                {
                    return null;//dataCollection = data.POSTData;
                }
                string commandString = dataCollection["command_type"];
                record.command_type = this.getDBCommand(commandString);

                if (dataCollection["access_key"] != null && dataCollection["access_key"].Equals(this.externalCommandAccessKey))
                {
                    //If command not parsable, return without creating
                    if (record.command_type != AdKat_CommandType.Default)
                    {
                        //Set the command action
                        record.command_action = record.command_type;

                        //Set the source
                        string sourceName = dataCollection["source_name"];
                        if (!String.IsNullOrEmpty(sourceName))
                            record.source_name = sourceName;
                        else
                            record.source_name = "HTTPAdmin";

                        string duration = dataCollection["record_durationMinutes"];
                        if (duration != null && duration.Length > 0)
                        {
                            record.command_numeric = Int32.Parse(duration);
                        }
                        else
                        {
                            record.command_numeric = 0;
                        }

                        string message = dataCollection["record_message"];
                        if (!String.IsNullOrEmpty(message))
                        {
                            if (message.Length >= this.requiredReasonLength)
                            {
                                record.record_message = message;

                                //Check the target
                                string targetName = dataCollection["target_name"];
                                //Check for an exact match
                                if (!String.IsNullOrEmpty(targetName))
                                {
                                    record.target_name = targetName;
                                    responseString += this.completeTargetInformation(record, false);
                                }
                                else
                                {
                                    responseString += "target_name cannot be null";
                                }
                            }
                            else
                            {
                                responseString += "Reason too short. Needs to be at least " + this.requiredReasonLength + " chars.";
                            }
                        }
                        else
                        {
                            responseString += "record_message cannot be null.";
                        }
                    }
                    else
                    {
                        responseString += "Command '" + commandString + "' Not Parsable. Check AdKats doc for valid DB commands.";
                    }
                }
                else
                {
                    responseString += "access_key either not given or incorrect.";
                }
            }
            catch (Exception e)
            {
                responseString += e.ToString();
            }
            return new HttpWebServerResponseData(responseString);
        }

        #endregion

        #region Mailing Functions

        public class EmailHandler
        {
            public AdKats plugin;
            public string strSMTPServer = "smtp.gmail.com";
            public Boolean blUseSSL = true;
            public int iSMTPPort = 993;
            public string strSenderMail = "adkatsbattlefield@gmail.com";
            public string strSMTPUser = "adkatsbattlefield@gmail.com";
            public string strSMTPPassword = "paqwjboqkbfywapu";

            public EmailHandler(AdKats plugin)
            {
                this.plugin = plugin;

                this.blUseSSL = false;
                this.strSMTPServer = String.Empty;
                this.iSMTPPort = 25;
                this.strSenderMail = String.Empty;
                this.strSMTPUser = String.Empty;
                this.strSMTPPassword = String.Empty;
            }

            private void sendHighProblemStateEmail()
            {
                CServerInfo info = plugin.getServerInfo();
                string subject = String.Empty;
                string body = String.Empty;

                subject = "AdKats: Server in High Problem State";

                StringBuilder sb = new StringBuilder();
                sb.Append("<h1>AdKats</h1>");
                sb.Append("<h2 style='color:#FF0000;'>Warning, high problem state detected.</h2>");
                sb.Append("<h2>SERVERNAME</h2>");
                sb.Append("<h3>CURRENTTIME</h3>");
                sb.Append("<h4>X reports have been made in the past 5 minutes.</h4>");
                sb.Append("<h4>Report List:</h4>");
                sb.Append("<table>");
                sb.Append("<tbody>");
                sb.Append("<tr>");
                sb.Append("<td><b>Player Name</b></td>");
                sb.Append("<td><b>Report Reason</b></td>");
                sb.Append("</tr>");
                //<tr>
                //  <td><b>PLAYERNAME</b></td>
                //  <td>REPORTREASON</td>
                //</tr>
                sb.Append("</tbody>");
                sb.Append("</table>");
                sb.Append("<p>");
                sb.Append("Map: MAPNAME<br>");
                sb.Append("Player Count: PLAYERCOUNT<br>");
                sb.Append("</p>");

                body = sb.ToString();

                this.EmailWrite(subject, body);
            }

            private void EmailWrite(string subject, string body)
            {
                try
                {
                    MailMessage email = new MailMessage();

                    email.From = new MailAddress(this.strSenderMail);

                    String mailto = null;
                    foreach (AdKat_Access access in plugin.playerAccessCache.Values)
                    {
                        mailto = access.player_email;
                        //Check for not null and default values
                        if (!String.IsNullOrEmpty(mailto) && mailto != "test@gmail.com")
                        {
                            if (Regex.IsMatch(mailto, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$"))
                            {
                                email.To.Add(new MailAddress(mailto));
                            }
                            else
                            {
                                plugin.ConsoleError("Error in receiver email address: " + mailto);
                            }
                        }
                        else
                        {
                            plugin.DebugWrite("Skipping email to " + access.player_name + ", no email given.", 6);
                        }
                    }

                    email.Subject = subject;
                    email.Body = body;
                    email.IsBodyHtml = true;
                    email.BodyEncoding = UTF8Encoding.UTF8;

                    SmtpClient smtp = new SmtpClient(this.strSMTPServer, this.iSMTPPort);

                    smtp.EnableSsl = this.blUseSSL;

                    smtp.Timeout = 10000;
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(this.strSMTPUser, this.strSMTPPassword);
                    smtp.Send(email);

                    plugin.DebugWrite("A notification email has been sent.", 1);
                }
                catch (Exception e)
                {
                    plugin.ConsoleError("Error while sending mails: " + e.ToString());
                }
            }
        }

        #endregion

        #region Twitter Functions

        /////////////////////////CODE CREDIT////////////////////////
        //All below twitter related functions are credited to Micovery's Insane Limits and PapaCharlie9
        ////////////////////////////////////////////////////////////
        public class TwitterHandler
        {
            public AdKats plugin = null;

            private string oauth_token = String.Empty;
            private string oauth_token_secret = String.Empty;

            public String twitter_PIN = "2916484";
            public String twitter_consumer_key = "3rkSNbotknUEMstELBNnQg";
            public String twitter_consumer_secret = "vRijlzIyJO8uXcoRM6ikis298sJJcxFkP3sf4hrL7A";
            public String twitter_access_token = "1468907792-UcOkpQhqFXdJM1rsYFq4XHYz9RPIjIW0PYDRfsB";
            public String twitter_access_token_secret = "VzqhUNthdTadAthiX7CqXU62VP7eRXAaw3Jfc1j0";
            public String twitter_user_id = "1468907792";
            public String twitter_screen_name = "AdKats Tool";

            public TwitterHandler(AdKats plugin)
            {
                this.plugin = plugin;
                this.SetupTwitter();
            }

            public bool sendTweet(String status)
            {
                return sendCustomTweet
                (
                    status,
                    twitter_access_token,
                    twitter_access_token_secret,
                    twitter_consumer_key,
                    twitter_consumer_secret,
                    true
                );
            }

            public bool sendCustomTweet
                (
                String status,
                String access_token,
                String access_token_secret,
                String consumer_key,
                String consumer_secret,
                bool quiet
                )
            {
                try
                {
                    if (String.IsNullOrEmpty(status))
                        throw new TwitterException("Cannot update Twitter status, invalid ^bstatus^n value");


                    if (String.IsNullOrEmpty(access_token) || String.IsNullOrEmpty(access_token_secret) ||
                        String.IsNullOrEmpty(consumer_key) || String.IsNullOrEmpty(consumer_secret))
                        throw new TwitterException("Cannot update Twitter status, looks like you have not run Twitter setup");

                    /* Create the Status Update Request */
                    OAuthRequest orequest = TwitterStatusUpdateRequest(status, access_token, access_token_secret, consumer_key, consumer_secret);

                    HttpWebResponse oresponse = (HttpWebResponse)orequest.request.GetResponse();

                    String protcol = "HTTP/" + oresponse.ProtocolVersion + " " + (int)oresponse.StatusCode;

                    if (!oresponse.StatusCode.Equals(HttpStatusCode.OK))
                        throw new TwitterException("Twitter UpdateStatus Request failed, " + protcol);

                    if (oresponse.ContentLength == 0)
                        throw new TwitterException("Twitter UpdateStatus Request failed, ContentLength=0");

                    StreamReader sin = new StreamReader(oresponse.GetResponseStream());
                    String response = sin.ReadToEnd();
                    sin.Close();

                    Hashtable data = (Hashtable)JSON.JsonDecode(response);

                    if (data == null || !data.ContainsKey("id_str"))
                        throw new TwitterException("Twitter UpdateStatus Request failed, response missing ^bid^n field");

                    String id = (String)(data["id_str"].ToString());

                    plugin.DebugWrite("Tweet Successful, id=^b" + id + "^n, Status: " + status, 4);

                    return true;
                }
                catch (TwitterException e)
                {
                    if (!quiet)
                        plugin.ConsoleError(e.Message);
                }
                catch (WebException e)
                {
                    if (!quiet)
                        HandleTwitterWebException(e, "UpdateStatus");
                }
                catch (Exception e)
                {
                    plugin.ConsoleError(e.ToString());
                }

                return false;

            }

            public void VerifyTwitterPin(String PIN)
            {
                try
                {
                    if (String.IsNullOrEmpty(PIN))
                    {
                        plugin.ConsoleError("Cannot verify Twitter PIN, value(^b" + PIN + "^n) is invalid");
                        return;
                    }

                    plugin.DebugWrite("VERIFIER_PIN: " + PIN, 5);

                    if (String.IsNullOrEmpty(oauth_token) || String.IsNullOrEmpty(oauth_token_secret))
                        throw new TwitterException("Cannot verify Twitter PIN, There is no ^boauth_token^n or ^boauth_token_secret^n in memory");

                    OAuthRequest orequest = TwitterAccessTokenRequest(PIN, oauth_token, oauth_token_secret);

                    HttpWebResponse oresponse = (HttpWebResponse)orequest.request.GetResponse();

                    String protcol = "HTTP/" + oresponse.ProtocolVersion + " " + (int)oresponse.StatusCode;

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
                catch (TwitterException e)
                {
                    plugin.ConsoleError(e.Message);
                    return;
                }
                catch (WebException e)
                {
                    HandleTwitterWebException(e, "AccessToken");
                }
                catch (Exception e)
                {
                    plugin.ConsoleError(e.ToString());
                }
            }

            public void SetupTwitter()
            {
                try
                {
                    oauth_token = String.Empty;
                    oauth_token_secret = String.Empty;

                    OAuthRequest orequest = TwitterRequestTokenRequest();

                    HttpWebResponse oresponse = (HttpWebResponse)orequest.request.GetResponse();
                    String protcol = "HTTP/" + oresponse.ProtocolVersion + " " + (int)oresponse.StatusCode;

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
                catch (TwitterException e)
                {
                    plugin.ConsoleError(e.Message);
                    return;
                }
                catch (WebException e)
                {
                    HandleTwitterWebException(e, "RequestToken");
                }
                catch (Exception e)
                {
                    plugin.ConsoleError(e.ToString());
                }

            }

            public void HandleTwitterWebException(WebException e, String prefix)
            {
                HttpWebResponse response = (HttpWebResponse)e.Response;
                String protcol = (response == null) ? "" : "HTTP/" + response.ProtocolVersion;

                String error = String.Empty;
                //try reading JSON response
                if (response != null && response.ContentType != null && response.ContentType.ToLower().Contains("json"))
                {
                    try
                    {
                        StreamReader sin = new StreamReader(response.GetResponseStream());
                        String data = sin.ReadToEnd();
                        sin.Close();

                        Hashtable jdata = (Hashtable)JSON.JsonDecode(data);
                        if (jdata == null || !jdata.ContainsKey("error") ||
                            jdata["error"] == null || !jdata["error"].GetType().Equals(typeof(String)))
                            throw new Exception();

                        error = "Twitter Error: " + (String)jdata["error"] + ", ";
                    }
                    catch (Exception ex)
                    {
                    }
                }

                /* Handle Time-Out Gracefully */
                if (e.Status.Equals(WebExceptionStatus.Timeout))
                {
                    plugin.ConsoleError("Twitter " + prefix + " Request(" + protcol + ") timed-out");
                    return;
                }
                else if (e.Status.Equals(WebExceptionStatus.ProtocolError))
                {
                    plugin.ConsoleError("Twitter " + prefix + " Request(" + protcol + ") failed, " + error + " " + e.GetType() + ": " + e.Message);
                    return;
                }
                else
                    throw e;
            }

            public Dictionary<String, String> ParseQueryString(String text)
            {
                MatchCollection matches = Regex.Matches(text, @"([^=]+)=([^&]+)&?", RegexOptions.IgnoreCase);

                Dictionary<String, String> pairs = new Dictionary<string, string>();

                foreach (Match match in matches)
                    if (match.Success && !pairs.ContainsKey(match.Groups[1].Value))
                        pairs.Add(match.Groups[1].Value, match.Groups[2].Value);

                return pairs;
            }

            public static int MAX_STATUS_LENGTH = 140;
            public OAuthRequest TwitterStatusUpdateRequest(
                String status,
                String access_token,
                String access_token_secret,
                String consumer_key,
                String consumer_secret)
            {
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
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_consumer_key", consumer_key));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_nonce", Guid.NewGuid().ToString("N")));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_signature_method", "HMAC-SHA1"));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_token", access_token));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_timestamp", ((long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString()));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_version", "1.0"));
                orequest.parameters.Add(new KeyValuePair<string, string>("status", OAuthRequest.UrlEncode(Encoding.UTF8.GetBytes(status))));

                // Compute and add the signature
                String signature = orequest.Signature(consumer_secret, access_token_secret);
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_signature", OAuthRequest.UrlEncode(signature)));

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

            public OAuthRequest TwitterAccessTokenRequest(String verifier, String token, String secret)
            {
                OAuthRequest orequest = new OAuthRequest(plugin, "http://api.twitter.com/oauth/access_token");
                orequest.Method = HTTPMethod.POST;
                orequest.request.ContentLength = 0;

                // Parameters required by the Twitter OAuth Protocol
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_consumer_key", twitter_consumer_key));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_nonce", Guid.NewGuid().ToString("N")));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_timestamp", ((long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString()));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_signature_method", "HMAC-SHA1"));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_version", "1.0"));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_token", token));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_verifier", verifier));

                // Compute and add the signature
                String signature = orequest.Signature(twitter_consumer_secret, secret);
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_signature", OAuthRequest.UrlEncode(signature)));

                // Add the OAuth authentication header
                String OAuthHeader = orequest.Header();
                orequest.request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequired;
                orequest.request.Headers["Authorization"] = OAuthHeader;
                return orequest;
            }

            public OAuthRequest TwitterRequestTokenRequest()
            {
                OAuthRequest orequest = new OAuthRequest(plugin, "http://api.twitter.com/oauth/request_token");
                orequest.Method = HTTPMethod.POST;
                orequest.request.ContentLength = 0;

                // Parameters required by the Twitter OAuth Protocol
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_callback", OAuthRequest.UrlEncode("oob")));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_consumer_key", twitter_consumer_key));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_nonce", Guid.NewGuid().ToString("N")));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_timestamp", ((long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString()));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_signature_method", "HMAC-SHA1"));
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_version", "1.0"));

                // Compute and add the signature
                String signature = orequest.Signature(twitter_consumer_secret, null);
                orequest.parameters.Add(new KeyValuePair<string, string>("oauth_signature", OAuthRequest.UrlEncode(signature)));

                // Add the OAuth authentication header
                String OAuthHeader = orequest.Header();
                orequest.request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequired;
                orequest.request.Headers["Authorization"] = OAuthHeader;

                return orequest;
            }

            public enum HTTPMethod
            {
                POST = 0x01,
                GET = 0x02,
                PUT = 0x04
            };

            public class TwitterException : Exception
            {
                public int code = 0;
                public TwitterException(String message) : base(message) { }
                public TwitterException(String message, int code) : base(message) { this.code = code; }
            }

            public class OAuthRequest
            {
                private AdKats plugin = null;
                public HttpWebRequest request = null;
                HMACSHA1 SHA1 = null;
                public List<KeyValuePair<String, String>> parameters = new List<KeyValuePair<string, string>>();
                public HTTPMethod Method { set { request.Method = value.ToString(); } get { return (HTTPMethod)Enum.Parse(typeof(HTTPMethod), request.Method); } }

                public OAuthRequest(AdKats plugin, String URL)
                {
                    this.plugin = plugin;
                    this.request = (HttpWebRequest)HttpWebRequest.Create(URL);
                    this.request.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US; rv:1.9.1.3) Gecko/20090824 Firefox/3.5.3 (.NET CLR 4.0.20506)";
                }

                public void Sort()
                {
                    // sort the query parameters
                    parameters.Sort(delegate(KeyValuePair<String, String> left, KeyValuePair<String, String> right)
                    {
                        if (left.Key.Equals(right.Key))
                            return left.Value.CompareTo(right.Value);
                        else
                            return left.Key.CompareTo(right.Key);
                    });
                }

                public String Header()
                {
                    String header = "OAuth ";
                    List<String> pairs = new List<string>();

                    Sort();

                    for (int i = 0; i < parameters.Count; i++)
                    {

                        KeyValuePair<String, String> pair = parameters[i];
                        if (pair.Key.Equals("status"))
                            continue;

                        pairs.Add(pair.Key + "=\"" + pair.Value + "\"");
                    }

                    header += String.Join(", ", pairs.ToArray());

                    plugin.DebugWrite("OAUTH_HEADER: " + header, 7);

                    return header;
                }

                public String Signature(String ConsumerSecret, String AccessTokenSecret)
                {
                    String base_url = request.Address.Scheme + "://" + request.Address.Host + request.Address.AbsolutePath;
                    String encoded_base_url = UrlEncode(base_url);

                    String http_method = request.Method;

                    Sort();

                    List<String> encoded_parameters = new List<string>();
                    List<String> raw_parameters = new List<string>();

                    // encode and concatenate the query parameters
                    for (int i = 0; i < parameters.Count; i++)
                    {
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

                public String HMACSHA1_HASH(String text, String ConsumerSecret, String AccessTokenSecret)
                {
                    if (SHA1 == null)
                    {
                        /* Initialize the SHA1 */
                        String HMACSHA1_KEY = String.IsNullOrEmpty(ConsumerSecret) ? "" : UrlEncode(ConsumerSecret) + "&" + (String.IsNullOrEmpty(AccessTokenSecret) ? "" : UrlEncode(AccessTokenSecret));
                        plugin.DebugWrite("HMACSHA1_KEY: " + HMACSHA1_KEY, 7);
                        SHA1 = new HMACSHA1(Encoding.ASCII.GetBytes(HMACSHA1_KEY));
                    }

                    return Convert.ToBase64String(SHA1.ComputeHash(System.Text.Encoding.ASCII.GetBytes(text)));
                }

                public static String UnreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

                public static String UrlEncode(string Input)
                {
                    StringBuilder Result = new StringBuilder();

                    for (int x = 0; x < Input.Length; ++x)
                    {
                        if (UnreservedChars.IndexOf(Input[x]) != -1)
                            Result.Append(Input[x]);
                        else
                            Result.Append("%").Append(String.Format("{0:X2}", (int)Input[x]));
                    }

                    return Result.ToString();
                }

                public static String UrlEncode(byte[] Input)
                {
                    StringBuilder Result = new StringBuilder();

                    for (int x = 0; x < Input.Length; ++x)
                    {
                        if (UnreservedChars.IndexOf((char)Input[x]) != -1)
                            Result.Append((char)Input[x]);
                        else
                            Result.Append("%").Append(String.Format("{0:X2}", (int)Input[x]));
                    }

                    return Result.ToString();
                }
            }
        }

        #endregion

        #region BF3Stats Fetch

        public AdKat_PlayerStats fetchPlayerStats(AdKat_Player player)
        {
            this.DebugWrite("entering getPlayerStats", 7);
            //Create return value
            AdKat_PlayerStats stats = new AdKat_PlayerStats();
            try
            {
                //Fetch from BF3Stats
                Hashtable playerData = this.fetchBF3StatsPlayer(player.player_name);
                if(playerData != null)
                {
                    string dataStatus = (String)playerData["status"];
                    if (dataStatus == "error")
                    {
                        stats.stats_exception = new AdKat_Exception("BF3 Stats reported error.");
                    }
                    else if (dataStatus == "notfound")
                    {
                        stats.stats_exception = new AdKat_Exception(player.player_name + " not found");
                    }
                    else
                    {
                        //Pull the global stats
                        stats.platform = (String)playerData["plat"];
                        stats.clanTag = (String)playerData["tag"];
                        stats.language = (String)playerData["language"];
                        stats.country = (String)playerData["country"];
                        stats.country_name = (String)playerData["country_name"];
                        DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        stats.firstSeen = dtDateTime.AddSeconds((Double)playerData["date_insert"]);
                        stats.lastPlayerUpdate = dtDateTime.AddSeconds((Double)playerData["date_update"]);

                        //Get internal stats
                        if (dataStatus == "data")
                        {
                            Hashtable statsList = (Hashtable)playerData["stats"];
                            stats.lastStatUpdate = dtDateTime.AddSeconds((Double)statsList["date_update"]);
                            //Get rank
                            Hashtable rankTable = (Hashtable)statsList["rank"];
                            stats.rank = (Double)rankTable["nr"];
                            //Get overall
                            Hashtable global = (Hashtable)statsList["global"];
                            stats.kills = (Double)global["kills"];
                            stats.deaths = (Double)global["deaths"];
                            stats.wins = (Double)global["wins"];
                            stats.losses = (Double)global["losses"];
                            stats.shots = (Double)global["shots"];
                            stats.hits = (Double)global["hits"];
                            stats.headshots = (Double)global["headshots"];
                            stats.time = TimeSpan.FromSeconds((Double)global["time"]);
                            //Get weapons
                            stats.weaponStats = new Dictionary<string, AdKat_WeaponStats>();
                            Hashtable weaponStats = (Hashtable)statsList["weapons"];
                            Hashtable currentWeapon = null;
                            AdKat_WeaponStats weapon = null;
                            foreach (String weaponKey in weaponStats.Keys)
                            {
                                //Create new construct
                                weapon = new AdKat_WeaponStats();
                                //Grab data
                                currentWeapon = (Hashtable)weaponStats[weaponKey];
                                //Parse into construct
                                weapon.name = (String)currentWeapon["name"];
                                weapon.shots = (Double)currentWeapon["shots"];
                                weapon.hits = (Double)currentWeapon["hits"];
                                weapon.kills = (Double)currentWeapon["kills"];
                                weapon.headshots = (Double)currentWeapon["headshots"];
                                weapon.category = (String)currentWeapon["category"];
                                weapon.kit = (String)currentWeapon["kit"];
                                weapon.range = (String)currentWeapon["range"];
                                weapon.time = TimeSpan.FromSeconds((Double)currentWeapon["time"]);
                                //Calculate values
                                weapon.hskr = weapon.headshots / weapon.kills;
                                weapon.kpm = weapon.kills / weapon.time.TotalMinutes;
                                weapon.dps = weapon.kills / weapon.hits * 100;
                                //Assign the construct
                                stats.weaponStats.Add(weapon.name, weapon);
                            }
                        }
                        else
                        {
                            stats.stats_exception = new AdKat_Exception(player.player_name + " did not have stats");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                stats.stats_exception = new AdKat_Exception("Server error fetching stats.", e);
            }
            //Assign the stats if no errors
            if (stats.stats_exception == null)
            {
                player.stats = stats;
            }
            this.DebugWrite("exiting getPlayerStats", 7);
            return stats;
        }

        private Hashtable fetchBF3StatsPlayer(String player_name)
        {
            Hashtable playerData = null;
            using (WebClient client = new WebClient())
            {
                NameValueCollection data = new NameValueCollection();
                data.Add("player", player_name);
                data.Add("opt", "all");
                byte[] response = client.UploadValues("http://api.bf3stats.com/pc/player/", data);
                if (response != null)
                {
                    string textResponse = System.Text.Encoding.Default.GetString(response);
                    playerData = (Hashtable)JSON.JsonDecode(textResponse);
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
                    hashData.Add("time", this.ConvertToTimestamp(DateTime.Now) + "");
                    hashData.Add("ident", "pUNykTul3R");
                    hashData.Add("player", player_name);
                    hashData.Add("type", "cronjob");
                    string dataString = JSON.JsonEncode(hashData);
                    this.ConsoleError("DATA:" + dataString);
                    //url encode the data
                    dataString = System.Web.HttpUtility.UrlEncode(dataString);
                    this.ConsoleError("E DATA: " + dataString);
                    //Compute the sig
                    string sig = System.Web.HttpUtility.UrlEncode(this.GetString(hmac256.ComputeHash(this.GetBytes(dataString))));
                    this.ConsoleError("SIG: " + sig);
                    NameValueCollection data = new NameValueCollection();
                    data.Add("data", dataString);
                    data.Add("sig", sig);
                    byte[] response = client.UploadValues("http://api.bf3stats.com/pc/playerupdate/", data);
                    if (response != null)
                    {
                        string textResponse = System.Text.Encoding.Default.GetString(response);
                        this.ConsoleSuccess(textResponse);
                        playerData = (Hashtable)JSON.JsonDecode(textResponse);
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error updating BF3Stats player.", e));
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
                    string textResponse = System.Text.Encoding.Default.GetString(response);
                    playerData = (Hashtable)JSON.JsonDecode(textResponse);
                }
            }
            return playerData;
        }*/

        #endregion

        #region Helper Methods and Classes

        public string extractString(string s, string tag)
        {
            if (String.IsNullOrEmpty(s) || String.IsNullOrEmpty(tag))
            {
                this.ConsoleError("Unable to extract string. Invalid inputs.");
                return null;
            }
            string startTag = "<" + tag + ">";
            int startIndex = s.IndexOf(startTag) + startTag.Length;
            if (startIndex == -1)
            {
                this.ConsoleError("Unable to extract string. Tag not found.");
            }
            int endIndex = s.IndexOf("</" + tag + ">", startIndex);
            return s.Substring(startIndex, endIndex - startIndex);
        }

        public Boolean soldierNameValid(string input)
        {
            try
            {
                this.DebugWrite("Checking player '" + input + "' for validity.", 7);
                if (String.IsNullOrEmpty(input))
                {
                    this.ConsoleError("Soldier Name empty or null.");
                    return false;
                }
                else if (input.Length > 16)
                {
                    this.ConsoleError("Soldier Name '" + input + "' too long, maximum length is 16 characters.");
                    return false;
                }
                else if (new Regex("[^a-zA-Z0-9_-]").Replace(input, "").Length != input.Length)
                {
                    this.ConsoleError("Soldier Name '" + input + "' contained invalid characters.");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                //Soldier name caused exception in the regex, definitely not valid
                this.ConsoleError("Soldier Name '" + input + "' contained invalid characters.");
                return false;
            }
        }

        public string formatTimeString(TimeSpan timeSpan)
        {
            this.DebugWrite("Entering formatTimeString", 7);
            string returnMessage = null;
            try
            {
                string formattedTime = (timeSpan.TotalMilliseconds >= 0) ? ("") : ("-");
                int days = Math.Abs(timeSpan.Days);
                int hours = Math.Abs(timeSpan.Hours);
                int minutes = Math.Abs(timeSpan.Minutes);
                int seconds = Math.Abs(timeSpan.Seconds);
                //Only show day if greater than 1 day
                if (days > 0)
                {
                    //Show day count
                    formattedTime += days + "d";
                }
                //Only show more information if less than 35 days
                if (days < 35)
                {
                    //Only show hours if days exist, or hours > 0
                    if (hours > 0 || days > 0)
                    {
                        //Show hour count
                        formattedTime += hours + "h";
                    }
                    //Only show more infomation if less than 1 day
                    if (days < 1)
                    {
                        //Only show minutes if Hours exist, or minutes > 0
                        if (minutes > 0 || hours > 0)
                        {
                            //Show hour count
                            formattedTime += minutes + "m";
                        }
                        //Only show more infomation if less than 1 hour
                        if (hours < 1)
                        {
                            //Only show seconds if minutes exist, or seconds > 0
                            if (seconds > 0 || minutes > 0)
                            {
                                //Show hour count
                                formattedTime += seconds + "s";
                            }
                        }
                    }
                }
                returnMessage = formattedTime;
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while formatting time string.", e));
            }
            this.DebugWrite("Exiting formatTimeString", 7);
            return returnMessage;
        }

        private double ConvertToTimestamp(DateTime value)
        {
            //create Timespan by subtracting the value provided from
            //the Unix Epoch
            TimeSpan span = (value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());

            //return the total seconds (which is a UNIX timestamp)
            return (double)span.TotalSeconds;
        }

        private void removePlayerFromDictionary(String player_name)
        {
            this.DebugWrite("Entering removePlayerFromDictionary", 7);
            try
            {
                //If the player is currently in the player list, remove them
                if (!String.IsNullOrEmpty(player_name))
                {
                    if (this.playerDictionary.ContainsKey(player_name))
                    {
                        lock (this.playersMutex)
                        {
                            this.DebugWrite("Removing " + player_name + " from current player list.", 4);
                            this.playerDictionary.Remove(player_name);
                        }
                    }
                    lock (this.adminAssistantCache)
                    {
                        if (this.adminAssistantCache.ContainsKey(player_name))
                        {
                            this.DebugWrite("Removing " + player_name + " from Admin Assistant Cache.", 4);
                            this.adminAssistantCache.Remove(player_name);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while removing player from player dictionary.", e));
            }
            this.DebugWrite("Exiting removePlayerFromDictionary", 7);
        }

        public CPlayerInfo buildCPlayerInfo(String player_name, String player_guid)
        {
            this.DebugWrite("Entering ", 7);
            CPlayerInfo playerInfo = null;
            try
            {
                IList<string> lstParameters = new List<string>();
                IList<string> lstVariables = new List<string>();
                lstParameters.Add("name");
                lstVariables.Add(player_name);
                lstParameters.Add("guid");
                lstVariables.Add(player_guid);
                playerInfo = new CPlayerInfo(lstParameters, lstVariables);
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while creating CPlayerInfo object.", e));
            }
            this.DebugWrite("Exiting ", 7);
            return playerInfo;
        }

        private DateTime convertToDBTime(DateTime proconTime)
        {
            return proconTime.Subtract(this.dbTimeConversion);
        }

        private DateTime convertToProconTime(DateTime DBTime)
        {
            return DBTime.Add(this.dbTimeConversion);
        }

        private TimeSpan getRemainingBanTime(AdKat_Ban aBan)
        {
            return aBan.ban_endTime.Subtract(this.convertToDBTime(DateTime.Now));
        }

        public Boolean confirmStatLoggerSetup()
        {
            try
            {
                List<MatchCommand> registered = this.GetRegisteredCommands();
                MatchCommand loggerStatusCommand = null;
                foreach (MatchCommand command in registered)
                {
                    if (command.RegisteredClassname.CompareTo("CChatGUIDStatsLoggerBF3") == 0 && command.RegisteredMethodName.CompareTo("GetStatus") == 0)
                    {
                        loggerStatusCommand = command;
                        this.DebugWrite("Found command for stat logger.", 5);
                        break;
                    }
                }
                if (loggerStatusCommand != null)
                {
                    //Stat logger is installed, fetch its status
                    Hashtable statLoggerStatus = this.getStatLoggerStatus();

                    //Only continue if response value
                    if (statLoggerStatus == null)
                    {
                        return false;
                    }
                    foreach (String key in statLoggerStatus.Keys)
                    {
                        this.DebugWrite("Logger response: (" + key + "): " + statLoggerStatus[key], 5);
                    }
                    if (!((String)statLoggerStatus["pluginVersion"]).Equals("1.1.0.2"))
                    {
                        this.ConsoleError("Invalid version of CChatGUIDStatsLoggerBF3 installed. Version 1.1.0.2 is required. If there is a new version, inform ColColonCleaner.");
                        return false;
                    }

                    if (!Regex.Match((String)statLoggerStatus["DBHost"], this.mySqlHostname, RegexOptions.IgnoreCase).Success ||
                        !Regex.Match((String)statLoggerStatus["DBPort"], this.mySqlPort, RegexOptions.IgnoreCase).Success ||
                        !Regex.Match((String)statLoggerStatus["DBName"], this.mySqlDatabaseName, RegexOptions.IgnoreCase).Success)
                    {
                        //Are db settings set for AdKats? If not, import them from stat logger.
                        if (String.IsNullOrEmpty(this.mySqlHostname) || String.IsNullOrEmpty(this.mySqlPort) || String.IsNullOrEmpty(this.mySqlDatabaseName))
                        {
                            this.mySqlHostname = (String)statLoggerStatus["DBHost"];
                            this.mySqlPort = (String)statLoggerStatus["DBPort"];
                            this.mySqlDatabaseName = (String)statLoggerStatus["DBName"];
                            this.updateSettingPage();
                        }
                        //Are DB Settings set for stat logger? If not, set them
                        if (String.IsNullOrEmpty((String)statLoggerStatus["DBHost"]) || String.IsNullOrEmpty((String)statLoggerStatus["DBPort"]) || String.IsNullOrEmpty((String)statLoggerStatus["DBName"]))
                        {
                            this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Host", this.mySqlHostname);
                            this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Port", this.mySqlPort);
                            this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Database Name", this.mySqlDatabaseName);
                            this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "UserName", this.mySqlUsername);
                            this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Password", this.mySqlPassword);

                            this.ConsoleError("CChatGUIDStatsLoggerBF3 database connection was not configured. It has been set up to use the same database and credentials as AdKats."); 
                            //Update the logger status
                            statLoggerStatus = this.getStatLoggerStatus();
                        }
                        else
                        {
                            this.ConsoleError("CChatGUIDStatsLoggerBF3 is not set up to use the same database as AdKats. Modify settings so they both use the same database.");
                            return false;
                        }
                    }
                    if (!((String)statLoggerStatus["DBConnectionActive"]).Equals("True"))
                    {
                        this.ConsoleError("CChatGUIDStatsLoggerBF3's connection to the database is not active. Backup mode Enabled...");
                    }
                    //Whether to feed stat logger settings per the 
                    if (this.feedStatLoggerSettings)
                    {
                        this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Enable Chatlogging?", "Yes");
                        this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Instant Logging of Chat Messages?", "Yes");
                        //Due to narwhals, Stat logger time offset is in the opposite direction of Adkats time offset
                        double SLOffset = this.dbTimeConversion.TotalHours * (-1);
                        this.setExternalPluginSetting("CChatGUIDStatsLoggerBF3", "Servertime Offset", SLOffset + "");
                    }
                    return true;
                }
                else
                {
                    this.ConsoleError("^1^bCChatGUIDStatsLoggerBF3^n plugin not found. Installing special release version 1.1.0.2 of that plugin is required for AdKats!");
                    return false;
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while confirming stat logger setup.", e));
                return false;
            }
        }

        public Hashtable getStatLoggerStatus()
        {
            try
            {
                //Check if enabled
                if (!this.isEnabled && false)
                {
                    this.DebugWrite("Attempted to fetch stat logger status while plugin disabled.", 4);
                    return null;
                }
                //Build request
                Hashtable request = new Hashtable();
                request["pluginName"] = "AdKats";
                request["pluginMethod"] = "HandleStatLoggerStatusResponse";
                // Send request
                this.statLoggerStatusHandle.Reset();
                this.ExecuteCommand("procon.protected.plugins.call", "CChatGUIDStatsLoggerBF3", "GetStatus", JSON.JsonEncode(request));
                //Wait a maximum of 5 seconds for stat logger response
                if (!this.statLoggerStatusHandle.WaitOne(5000))
                {
                    this.ConsoleWarn("^bCChatGUIDStatsLoggerBF3^n is not enabled or is lagging! Attempting to enable, please wait...");
                    //Issue the command to enable stat logger
                    this.ExecuteCommand("procon.protected.plugins.enable", "CChatGUIDStatsLoggerBF3", "True");
                    //Wait 5 seconds for enable and initial connect
                    Thread.Sleep(5000);
                    //Refetch the status
                    this.statLoggerStatusHandle.Reset();
                    this.ExecuteCommand("procon.protected.plugins.call", "CChatGUIDStatsLoggerBF3", "GetStatus", JSON.JsonEncode(request));
                    if (!this.statLoggerStatusHandle.WaitOne(5000))
                    {
                        this.ConsoleError("CChatGUIDStatsLoggerBF3 could not be enabled automatically. Please enable manually.");
                    }
                }
                if (this.lastStatLoggerStatusUpdate != null &&
                    this.lastStatLoggerStatusUpdateTime != DateTime.MinValue &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("pluginVersion") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("pluginEnabled") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("DBHost") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("DBPort") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("DBName") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("DBTimeOffset") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("DBConnectionActive") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("ChatloggingEnabled") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("InstantChatLoggingEnabled") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("StatsLoggingEnabled") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("DBliveScoreboardEnabled") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("DebugMode") &&
                    this.lastStatLoggerStatusUpdate.ContainsKey("Error"))
                {
                    //Response appears to be valid, return it
                    return this.lastStatLoggerStatusUpdate;
                }
                else
                {
                    //Response is invalid, throw error and return null
                    this.ConsoleError("Status response from CChatGUIDStatsLoggerBF3 was not valid.");
                    return null;
                }
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while getting stat logger status."));
                return null;
            }
        }

        public void HandleStatLoggerStatusResponse(params String[] commands)
        {
            this.DebugWrite("Entering HandleStatLoggerStatusResponse", 7);
            try
            {
                if (commands.Length < 1)
                {
                    this.ConsoleError("Status fetch response handle canceled, no parameters provided.");
                    return;
                }
                this.lastStatLoggerStatusUpdate = (Hashtable)JSON.JsonDecode(commands[0]);
                this.lastStatLoggerStatusUpdateTime = DateTime.Now;
                this.statLoggerStatusHandle.Set();
            }
            catch (Exception e)
            {
                this.HandleException(new AdKat_Exception("Error while handling stat logger status response.", e));
            }
            this.DebugWrite("Exiting HandleStatLoggerStatusResponse", 7);
        }

        public static string GetRandom32BitHashCode()
        {
            string randomString = "";
            Random random = new Random();

            for (int i = 0; i < 32; i++)
            {
                randomString += Convert.ToChar(Convert.ToInt32(Math.Floor(91 * random.NextDouble()))).ToString(); ;
            }

            return Encode(randomString);
        }

        public static string Encode(string str)
        {
            byte[] encbuff = System.Text.Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(encbuff);
        }

        public static string Decode(string str)
        {
            byte[] decbuff = Convert.FromBase64String(str.Replace(" ", "+"));
            return System.Text.Encoding.UTF8.GetString(decbuff);
        }

        public static string EncodeStringArray(string[] a_strValue)
        {

            StringBuilder encodedString = new StringBuilder();

            for (int i = 0; i < a_strValue.Length; i++)
            {
                if (i > 0)
                {
                    encodedString.Append("|");
                    //strReturn += "|";
                }
                encodedString.Append(Encode(a_strValue[i]));
                //strReturn += Encode(a_strValue[i]);
            }

            return encodedString.ToString();
        }

        public byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }

        public void setServerInfo(CServerInfo info)
        {
            lock (this.serverInfoMutex)
            {
                this.serverInfo = info;
            }
        }

        public CServerInfo getServerInfo()
        {
            lock (this.serverInfoMutex)
            {
                return this.serverInfo;
            }
        }

        //Calling this method will make the settings window refresh with new data
        public void updateSettingPage()
        {
            this.setExternalPluginSetting("AdKats", "UpdateSettings", "Update");
        }

        //Calls setVariable with the given parameters
        public void setExternalPluginSetting(string plugin_name, string setting_name, string setting_value)
        {
            if (String.IsNullOrEmpty(plugin_name) || String.IsNullOrEmpty(setting_name) || String.IsNullOrEmpty(setting_value))
            {
                this.ConsoleError("Required inputs null in setExternalPluginSetting");
                return;
            }
            this.ExecuteCommand("procon.protected.plugins.setVariable", plugin_name, setting_name, setting_value);
        }

        //Credit to Micovery and PapaCharlie9 for modified Levenshtein Distance algorithm 
        public static int LevenshteinDistance(string s, string t)
        {
            s = s.ToLower();
            t = t.ToLower();
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];
            if (n == 0)
                return m;
            if (m == 0)
                return n;
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 0), d[i - 1, j - 1] + ((t[j - 1] == s[i - 1]) ? 0 : 1));
            return d[n, m];
        }

        //parses single word or number parameters out of a string until param count is reached
        private String[] parseParameters(string message, int maxParamCount)
        {
            //create list for parameters
            List<String> parameters = new List<String>();
            if (message.Length > 0)
            {
                //Add all single word/number parameters
                String[] paramSplit = message.Split(' ');
                int maxLoop = (paramSplit.Length < maxParamCount) ? (paramSplit.Length) : (maxParamCount);
                for (int i = 0; i < maxLoop - 1; i++)
                {
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

        public void JoinWith(Thread thread)
        {
            if (thread == null || !thread.IsAlive)
            {
                DebugWrite("^b" + thread.Name + "^n already finished.", 3);
                return;
            }
            DebugWrite("Waiting for ^b" + thread.Name + "^n to finish", 3);
            thread.Join();
        }

        public class AdKat_Access
        {
            //No reference to player table made here, plain string name access
            public String player_name = "player_name";
            public Int32 member_id = 0;
            public String player_email = "test@gmail.com";
            public Int32 access_level = -1;
        }

        public class AdKat_Player
        {
            public Int64 player_id = -1;
            public string player_name = null;
            public string player_guid = null;
            public string player_pbguid = null;
            public string player_ip = null;
            public string player_slot = null;

            public CPlayerInfo frostbitePlayerInfo = null;
            public CPunkbusterInfo PBPlayerInfo = null;

            public DateTime lastSpawn = DateTime.Now;
            public DateTime lastDeath = DateTime.Now;
            public DateTime lastAction = DateTime.Now;

            public Queue<KeyValuePair<AdKat_Player, DateTime>> recentKills = null;

            public AdKat_PlayerStats stats = null;

            public AdKat_Player()
            {

            }
        }

        public class AdKat_PlayerStats
        {
            public String platform = null;
            public String clanTag = null;
            public String language = null;
            public String country = null;
            public String country_name = null;
            public DateTime firstSeen = DateTime.MaxValue;
            public DateTime lastPlayerUpdate = DateTime.MaxValue;
            public DateTime lastStatUpdate = DateTime.MaxValue;

            public double rank = -1;
            public TimeSpan time = TimeSpan.FromSeconds(0);
            public int score = -1;
            public double kills = -1;
            public double assists = -1;
            public double deaths = -1;
            public double shots = -1;
            public double hits = -1;
            public double headshots = -1;

            public double wins = -1;
            public double losses = -1;

            public Dictionary<String, AdKat_WeaponStats> weaponStats = null;

            public AdKat_Exception stats_exception = null;

            public AdKat_PlayerStats()
            {

            }
        }

        public class AdKat_WeaponStats
        {
            public String name = null;
            public String category = null;
            public String kit = null;
            public String range = null;

            public TimeSpan time = TimeSpan.FromSeconds(0);
            public double kills = -1;
            public double headshots = -1;
            public double hits = -1;
            public double shots = -1;

            //Calculated values
            public double hskr = -1;
            public double dps = -1;
            public double kpm = -1;

            public AdKat_WeaponStats()
            {

            }
        }

        public class StatLibrary
        {
            public Dictionary<String, StatLibrary_Weapon> weapons = null;

            public StatLibrary()
            {
                //Create the weapon library
                this.weapons = new Dictionary<string, StatLibrary_Weapon>();
                //Add the weapons
                StatLibrary_Weapon weapon = new StatLibrary_Weapon();
                weapon.name = "G17C";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon.name = "G17C SUPP.";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon.name = "G17C TACT.";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = ".44 MAGNUM";
                weapon.damage_max = 60;
                weapon.damage_min = 30;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = ".44 SCOPED";
                weapon.damage_max = 60;
                weapon.damage_min = 30;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "93R";
                weapon.damage_max = 20;
                weapon.damage_min = 12.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "G18";
                weapon.damage_max = 20;
                weapon.damage_min = 12.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "G18 SUPP.";
                weapon.damage_max = 20;
                weapon.damage_min = 12.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "G18 TACT.";
                weapon.damage_max = 20;
                weapon.damage_min = 12.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M9";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M9 TACT.";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M9 SUPP.";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M1911";
                weapon.damage_max = 34;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M1911 TACT.";
                weapon.damage_max = 34;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M1911 SUPP.";
                weapon.damage_max = 34;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M1911 S-TAC";
                weapon.damage_max = 34;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "MP412 REX";
                weapon.damage_max = 50;
                weapon.damage_min = 28;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "MP443";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "MP443 TACT.";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "MP443 SUPP.";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "AS VAL";
                weapon.damage_max = 20;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M5K";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "MP7";
                weapon.damage_max = 20;
                weapon.damage_min = 11.2;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "P90";
                weapon.damage_max = 20;
                weapon.damage_min = 11.2;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "PDW-R";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "PP-19";
                weapon.damage_max = 16.7;
                weapon.damage_min = 12.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "PP-2000";
                weapon.damage_max = 25;
                weapon.damage_min = 13.75;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "UMP-45";
                weapon.damage_max = 34;
                weapon.damage_min = 12.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "JNG-90";
                weapon.damage_max = 80;
                weapon.damage_min = 59;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "L96";
                weapon.damage_max = 80;
                weapon.damage_min = 59;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M39 EMR";
                weapon.damage_max = 50;
                weapon.damage_min = 37.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M40A5";
                weapon.damage_max = 80;
                weapon.damage_min = 59;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M98B";
                weapon.damage_max = 95;
                weapon.damage_min = 59;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M417";
                weapon.damage_max = 50;
                weapon.damage_min = 37.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "MK11";
                weapon.damage_max = 50;
                weapon.damage_min = 37.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "QBU-88";
                weapon.damage_max = 50;
                weapon.damage_min = 37.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "SKS";
                weapon.damage_max = 43;
                weapon.damage_min = 27;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "SV98";
                weapon.damage_max = 80;
                weapon.damage_min = 50;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "SVD";
                weapon.damage_max = 50;
                weapon.damage_min = 37.5;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M27 IAR";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "RPK-74M";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "L86A2";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "LSAT";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M60E4";
                weapon.damage_max = 34;
                weapon.damage_min = 22;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M240B";
                weapon.damage_max = 34;
                weapon.damage_min = 22;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M249";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "MG36";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "PKP PECHENEG";
                weapon.damage_max = 34;
                weapon.damage_min = 22;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "QBB-95";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "TYPE 88 LMG";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "A-91";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "ACW-R";
                weapon.damage_max = 20;
                weapon.damage_min = 16.7;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "AKS-74u";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "G36C";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "G53";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M4A1";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "MTAR-21";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "QBZ-95B";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "SCAR-H";
                weapon.damage_max = 30;
                weapon.damage_min = 20;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "SG553";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M4";
                weapon.damage_max = 25;
                weapon.damage_min = 14.3;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "AEK-971";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "AK-74M";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "AN-94";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "AUG A3";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "F2000";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "FAMAS";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "G3A3";
                weapon.damage_max = 34;
                weapon.damage_min = 22;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "KH2002";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "L85A2";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M16A3";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M416";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "SCAR-L";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M16A4";
                weapon.damage_max = 25;
                weapon.damage_min = 18.4;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "XBOW";
                weapon.damage_max = 100;
                weapon.damage_min = 10;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "XBOW SCOPED";
                weapon.damage_max = 100;
                weapon.damage_min = 10;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M320";
                weapon.damage_max = 100;
                weapon.damage_min = 1;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M26";
                weapon.damage_max = 100;
                weapon.damage_min = 1;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M26 MASS";
                weapon.damage_max = 100;
                weapon.damage_min = 1;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M26 SLUG";
                weapon.damage_max = 100;
                weapon.damage_min = 1;
                this.weapons.Add(weapon.name, weapon);
                weapon = new StatLibrary_Weapon();
                weapon.name = "M26 FRAG";
                weapon.damage_max = 100;
                weapon.damage_min = 1;
                this.weapons.Add(weapon.name, weapon);
            }
        }

        public class StatLibrary_Weapon
        {
            public String name = null;
            public double damage_max = -1;
            public double damage_min = -1;

            public StatLibrary_Weapon()
            {

            }
        }

        public class AdKat_Exception
        {
            public String method = String.Empty;
            public String message = String.Empty;
            public System.Exception internalException = null;
            //Param Constructors
            public AdKat_Exception(String message, System.Exception internalException)
            {
                this.method = new StackFrame(1).GetMethod().Name;
                this.message = message;
                this.internalException = internalException;
            }
            public AdKat_Exception(String message)
            {
                this.method = new StackFrame(1).GetMethod().Name;
                this.message = message;
            }
            //Override toString
            public override string ToString()
            {
                return "[" + this.method + "]["  + this.message + "]" + ((this.internalException != null)?(": " + internalException.ToString()):(""));
            }
        }

        public class AdKat_Record
        {
            //Attributes for the record
            public Int64 record_id = -1;
            public Int64 server_id = -1;
            public AdKat_CommandType command_type = AdKat_CommandType.Default;
            public AdKat_CommandType command_action = AdKat_CommandType.Default;
            public int command_numeric = 0;
            public string target_name = null;
            public AdKat_Player target_player = null;
            public string source_name = null;
            public string record_message = null;
            public DateTime record_time = DateTime.MinValue;

            //Not stored separately in the database
            public AdKat_CommandSource command_source = AdKat_CommandSource.Default;
            public Boolean isIRO = false;

            //Current exception state of the record
            public AdKat_Exception record_exception = null;

            //If record action was take
            public Boolean record_action_executed = false;

            //Default Constructor
            public AdKat_Record()
            {
            }
        }

        public class AdKat_Ban
        {
            //Current exception state of the ban
            public AdKat_Exception ban_exception = null;

            public Int64 ban_id = -1;
            public AdKat_Record ban_record = null;
            public string ban_status = "Enabled";
            public string ban_notes = null;
            public string ban_sync = null;
            //startTime and endTime are not set by AdKats, they are set in the database.
            //startTime and endTime will be valid only when bans are fetched from the database
            public DateTime ban_startTime;
            public DateTime ban_endTime;
            public Boolean ban_enforceName = false;
            public Boolean ban_enforceGUID = true;
            public Boolean ban_enforceIP = false;

            public AdKat_Ban()
            {
            }
        }

        public class BBM5108_Ban
        {
            public string soldiername = null;
            public string eaguid = null;
            public string ban_length = null;
            public DateTime ban_duration;
            public string ban_reason = null;

            public BBM5108_Ban()
            {
            }
        }

        #endregion

        #region Logging

        public string FormatMessage(string msg, ConsoleMessageType type)
        {
            string prefix = "[^bAdKats^n] ";

            if (type.Equals(ConsoleMessageType.Warning))
            {
                prefix += "^1^bWARNING^0^n: ";
            }
            else if (type.Equals(ConsoleMessageType.Error))
            {
                prefix += "^1^bERROR^0^n: ";
            }
            else if (type.Equals(ConsoleMessageType.Success))
            {
                prefix += "^b^2SUCCESS^n^0: ";
            }
            else if (type.Equals(ConsoleMessageType.Exception))
            {
                prefix += "^1^bEXCEPTION^0^n: ";
            }

            return prefix + msg;
        }

        public void LogWrite(string msg)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
            if (this.slowmo)
            {
                Thread.Sleep(1000);
            }
        }

        public void ConsoleWrite(string msg, ConsoleMessageType type)
        {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(string msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Normal);
        }

        public void ConsoleWarn(string msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Warning);
        }

        public void ConsoleError(string msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Error);
        }

        public void ConsoleSuccess(string msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Success);
        }
        
        public AdKat_Exception HandleException(AdKat_Exception aException)
        {
            //Always write the exception to console
            this.ConsoleWrite(aException.ToString(), ConsoleMessageType.Exception);
            //Check if the exception attributes to the database
            if (aException.internalException.GetType() == typeof(System.TimeoutException) || 
                aException.internalException.ToString().Contains("Unable to connect to any of the specified MySQL hosts"))
            {
                this.dbConnectionActive = false;
            }
            else if (false)
            {
                //Create the Exception record
                AdKat_Record record = new AdKat_Record();
                record.server_id = this.server_id;
                record.command_type = AdKat_CommandType.Exception;
                record.command_numeric = 0;
                record.target_name = "AdKats";
                record.target_player = null;
                record.source_name = "AdKats";
                record.record_message = aException.ToString();
                //Process the record
                this.queueRecordForProcessing(record);
            }
            return aException;
        }

        public void DebugWrite(string msg, int level)
        {
            if (debugLevel >= level)
            {
                ConsoleWrite(msg, ConsoleMessageType.Normal);
            }
        }

        #endregion
    } // end AdKats
} // end namespace PRoConEvents
