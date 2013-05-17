/* 
 * AdKats is a MySQL reflected admin tool for Procon Frostbite.
 * 
 * This plugin should be used by groups with high traffic servers, with set rules, and many admins. It is a
 * MySQL database reflected admin tool that includes editable in-game commands, database reflected punishment and
 * forgiveness, proper player report and admin call handling, player name completion, and internal implementation of TeamSwap.
 * 
 * Requires a MySQL Database connection for proper use. Will set up needed tables in the database if they are not there already.
 * 
 * Current version: 0.1.7.3
 * 
 * AdKats.cs
 */

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Net.WebClient;
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


namespace PRoConEvents
{
    //Aliases
    using EventType = PRoCon.Core.Events.EventType;
    using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

    public class ADKATs : PRoConPluginAPI, IPRoConPluginInterface
    {
        #region Variables

        // Enumerations
        //Messaging
        public enum MessageTypeEnum { Warning, Error, Exception, Normal };
        //Possible Admin Commands
        public enum ADKAT_CommandType
        {
            //Case for use while parsing and handling errors
            Default,
            //Confirm or cancel a command
            ConfirmCommand,
            CancelCommand,
            //Moving players
            MovePlayer,
            ForceMovePlayer,
            Teamswap,
            //Punishing players
            KillPlayer,
            KickPlayer,
            TempBanPlayer,
            PermabanPlayer,
            PunishPlayer,
            ForgivePlayer,
            //Reporting players
            ReportPlayer,
            CallAdmin,
            //Round Commands
            RestartLevel,
            NextLevel,
            EndLevel,
            //Messaging
            AdminSay,
            PlayerSay,
            AdminYell,
            PlayerYell,
            PreYell,
            PreSay
        };
        //enum for player ban types
        public enum ADKAT_BanType
        {
            FrostbiteName,
            FrostbiteEaGuid,
            PunkbusterGuid
        };

        // General settings
        //Whether to get the release version of plugin description, or the dev version.
        //This setting is unchangeable by users, and will always be TRUE for released versions of the plugin.
        private bool isRelease = false;
        //Whether the plugin is enabled
        private bool isEnabled;
        //Current debug level
        private int debugLevel;
        //IDs of the two teams as the server understands it
        private int USTeamId = 1;
        private int RUTeamId = 2;
        //Boolean used for archaic thread sync
        private Boolean updating = false;
        //All server info
        private CServerInfo serverInfo = null;

        // Player Lists
        Dictionary<string, CPlayerInfo> currentPlayers = new Dictionary<string, CPlayerInfo>();
        List<CPlayerInfo> playerList = new List<CPlayerInfo>();

        //Teamswap
        //Delayed move list
        private List<CPlayerInfo> onDeathMoveList = new List<CPlayerInfo>();
        //The list of players on RU wishing to move to US (This list takes first priority)
        private Queue<CPlayerInfo> USMoveQueue = new Queue<CPlayerInfo>();
        //the list of players on US wishing to move to RU (This list takes secondary)
        private Queue<CPlayerInfo> RUMoveQueue = new Queue<CPlayerInfo>();
        //player counts per team
        private int USPlayerCount = 0;
        private int RUPlayerCount = 0;

        // Admin Settings
        private Boolean useDatabaseAdminList = false;
        private List<string> databaseAdminCache = new List<string>();
        private List<string> staticAdminCache = new List<string>();

        // MySQL Settings
        private string mySqlHostname;
        private string mySqlPort;
        private string mySqlDatabaseName;
        private string mySqlUsername;
        private string mySqlPassword;
        private string tablename_adminlist = "tbl_adminlist";
        private string columnname_adminname = "name";

        // Battlefield Server ID
        private int serverID = -1;

        //current ban type
        private string m_strBanTypeOption = "Frostbite - Name";
        private ADKAT_BanType m_banMethod;

        //Command Strings for Input
        //Player punishment
        private string m_strKillCommand = "kill|log";
        private string m_strKickCommand = "kick|log";
        private string m_strTemporaryBanCommand = "tban|log";
        private string m_strPermanentBanCommand = "ban|log";
        private string m_strPunishCommand = "punish|log";
        private string m_strForgiveCommand = "forgive|log";
        private string m_strMoveCommand = "move|log";
        private string m_strForceMoveCommand = "fmove|log";
        private string m_strTeamswapCommand = "moveme|log";
        private string m_strReportCommand = "report|log";
        private string m_strCallAdminCommand = "admin|log";
        //Admin messaging
        private string m_strSayCommand = "say|log";
        private string m_strPlayerSayCommand = "psay|log";
        private string m_strYellCommand = "yell|log";
        private string m_strPlayerYellCommand = "pyell|log";
        private string m_strPreYellCommand = "preyell|log";
        private string m_strPreSayCommand = "presay|log";
        private List<string> preMessageList = new List<string>();
        private int m_iShowMessageLength = 5;
        private string m_strShowMessageLength = "5";
        //Map control
        private string m_strRestartLevelCommand = "restart|log";
        private string m_strNextLevelCommand = "nextlevel|log";
        private string m_strEndLevelCommand = "endround|log";
        //Confirm and cancel
        private string m_strConfirmCommand = "yes";
        private string m_strCancelCommand = "no";
        //Used to parse incoming commands quickly
        public Dictionary<string, ADKAT_CommandType> ADKAT_CommandStrings;
        //Database record types
        public Dictionary<ADKAT_CommandType, string> ADKAT_RecordTypes;
        //Logging settings
        public Dictionary<ADKAT_CommandType, Boolean> ADKAT_LoggingSettings;

        //When a player partially completes a name, this dictionary holds those actions until player confirms action
        private Dictionary<string, ADKAT_Record> actionAttemptList = new Dictionary<string, ADKAT_Record>();
        //Whether to act on punishments within the plugin
        private Boolean actOnPunishments = true;
        //Default hierarchy of punishments
        private string[] punishmentHierarchy = 
        {
            "kill",
            "kill",
            "kick",
            "kick",
            "tban60",
            "tban60",
            "tbanweek",
            "tbanweek",
            "ban"
        };
        //When punishing, only kill players when server is in low population
        private Boolean onlyKillOnLowPop = true;
        //Default for low populations
        private int lowPopPlayerCount = 20;
        //Default required reason length
        private int requiredReasonLength = 5;
        //Default punishment timeout in minutes
        private Double punishmentTimeout = 0.5;

        //TeamSwap Settings
        //Last time list players was called
        private DateTime lastListPlayersRequest = DateTime.Now;
        //whether to allow all players, or just players in the whitelist
        private Boolean requireTeamswapWhitelist = false;
        //Static whitelist for plugin only use
        private List<string> staticTeamswapWhitelistCache = new List<string>();
        //Whether to use the database whitelist for teamswap
        private Boolean useDatabaseTeamswapWhitelist = false;
        //Database whitelist cache
        private List<string> databaseTeamswapWhitelistCache = new List<string>();
        //the lowest ticket count of either team
        private int lowestTicketCount = 500000;
        //the highest ticket count of either team
        private int highestTicketCount = 0;
        //the highest ticket count of either team to allow self move
        private int teamSwapTicketWindowHigh = 500000;
        //the lowest ticket count of either team to allow self move
        private int teamSwapTicketWindowLow = 0;

        #endregion

        public ADKATs()
        {
            isEnabled = false;
            debugLevel = 0;

            preMessageList.Add("US TEAM: DO NOT ENTER THE STREETS BEYOND 'A', YOU WILL BE PUNISHED.");
            preMessageList.Add("US TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.");
            preMessageList.Add("RU TEAM: DO NOT GO BEYOND 'C' FLAG, YOU WILL BE PUNISHED.");
            preMessageList.Add("RU TEAM: DO NOT BASERAPE, YOU WILL BE PUNISHED.");

            //Create command and logging dictionaries
            this.ADKAT_CommandStrings = new Dictionary<string, ADKAT_CommandType>();
            //this.ADKAT_CommandStrings.Add(this.m_strKillCommand, ADKAT_CommandType.KillPlayer);
            this.ADKAT_LoggingSettings = new Dictionary<ADKAT_CommandType, Boolean>();

            //Fill command and logging dictionaries by calling rebind
            this.rebindAllCommands();

            //Create database dictionaries
            this.ADKAT_RecordTypes = new Dictionary<ADKAT_CommandType, string>();

            //Fill DB record types
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.MovePlayer, "Move");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.ForceMovePlayer, "ForceMove");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.Teamswap, "Teamswap");

            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.KillPlayer, "Kill");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.KickPlayer, "Kick");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.TempBanPlayer, "TempBan");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.PermabanPlayer, "PermaBan");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.PunishPlayer, "Punish");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.ForgivePlayer, "Forgive");

            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.ReportPlayer, "Report");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.CallAdmin, "CallAdmin");

            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.AdminSay, "AdminSay");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.PlayerSay, "PlayerSay");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.AdminYell, "AdminYell");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.PlayerYell, "PlayerYell");

            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.RestartLevel, "RestartLevel");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.NextLevel, "NextLevel");
            this.ADKAT_RecordTypes.Add(ADKAT_CommandType.EndLevel, "EndLevel");
        }

        #region Plugin details

        public string GetPluginName()
        {
            return "AdKats";
        }

        public string GetPluginVersion()
        {
            return "0.1.7.3";
        }

        public string GetPluginAuthor()
        {
            return "ColColonCleaner";
        }

        public string GetPluginWebsite()
        {
            return "http://www.adkgamers.com/";
        }

        public string GetPluginDescription()
        {
            if (this.isRelease)
            {
                string pluginDescription = WebClient.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/README.md");
                string pluginChangelog = WebClient.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/master/CHANGELOG.md");
            }
            else
            {
                string pluginDescription = WebClient.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/dev/README.md");
                string pluginChangelog = WebClient.DownloadString("https://raw.github.com/ColColonCleaner/AdKats/dev/CHANGELOG.md");
            }
            return pluginDescription + pluginChangelog;
        }

        #endregion

        #region Plugin settings

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            //Debug settings
            lstReturn.Add(new CPluginVariable("Debugging|Debug level", typeof(int), this.debugLevel));

            //Server Settings
            lstReturn.Add(new CPluginVariable("Server Settings|Server ID", typeof(int), this.serverID));

            //SQL Settings
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Hostname", typeof(string), mySqlHostname));
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Port", typeof(string), mySqlPort));
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Database", typeof(string), mySqlDatabaseName));
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Username", typeof(string), mySqlUsername));
            lstReturn.Add(new CPluginVariable("MySQL Settings|MySQL Password", typeof(string), mySqlPassword));
            //TODO implement advanced sql settings

            //Ban Settings
            lstReturn.Add(new CPluginVariable("Banning|Ban Type", "enum.ADKATs_BanType(Frostbite - Name|Frostbite - EA GUID|Punkbuster - GUID)", this.m_strBanTypeOption));

            //Command Settings
            lstReturn.Add(new CPluginVariable("Command Settings|Minimum Required Reason Length", typeof(int), this.requiredReasonLength));
            lstReturn.Add(new CPluginVariable("Command Settings|Yell display time seconds", typeof(int), this.m_iShowMessageLength));
            lstReturn.Add(new CPluginVariable("Command Settings|Confirm Command", typeof(string), m_strConfirmCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Cancel Command", typeof(string), m_strCancelCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Kill Player", typeof(string), m_strKillCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Kick Player", typeof(string), m_strKickCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Temp-Ban Player", typeof(string), m_strTemporaryBanCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Permaban Player", typeof(string), m_strPermanentBanCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Punish Player", typeof(string), m_strPunishCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Forgive Player", typeof(string), m_strForgiveCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|OnDeath Move Player", typeof(string), m_strMoveCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Force Move Player", typeof(string), m_strForceMoveCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Teamswap Self", typeof(string), m_strTeamswapCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Report Player", typeof(string), m_strReportCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Call Admin on Player", typeof(string), m_strCallAdminCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Admin Say", typeof(string), m_strSayCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Admin Pre-Say", typeof(string), m_strPreSayCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Player Say", typeof(string), m_strPlayerSayCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Admin Yell", typeof(string), m_strYellCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Admin Pre-Yell", typeof(string), m_strPreYellCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Player Yell", typeof(string), m_strPlayerYellCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Restart Level", typeof(string), m_strRestartLevelCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|Next Level", typeof(string), m_strNextLevelCommand));
            lstReturn.Add(new CPluginVariable("Command Settings|End Level", typeof(string), m_strEndLevelCommand));

            //Punishment Settings
            lstReturn.Add(new CPluginVariable("Punishment Settings|Act on Punishments", typeof(Boolean), this.actOnPunishments));
            lstReturn.Add(new CPluginVariable("Punishment Settings|Punishment Hierarchy", typeof(string[]), this.punishmentHierarchy));
            lstReturn.Add(new CPluginVariable("Punishment Settings|Only Kill Players when Server in low population", typeof(Boolean), this.onlyKillOnLowPop));
            if (this.onlyKillOnLowPop)
            {
                lstReturn.Add(new CPluginVariable("Punishment Settings|Low Population Value", typeof(int), this.lowPopPlayerCount));
            }
            lstReturn.Add(new CPluginVariable("Punishment Settings|Punishment Timeout", typeof(Double), this.punishmentTimeout));

            //Admin Settings
            lstReturn.Add(new CPluginVariable("Admin Settings|Use Database Admin List", typeof(Boolean), this.useDatabaseAdminList));
            if (!this.useDatabaseAdminList)
            {
                lstReturn.Add(new CPluginVariable("Admin Settings|Static Admin List", typeof(string[]), this.staticAdminCache.ToArray()));
            }
            else
            {
                lstReturn.Add(new CPluginVariable("Admin Settings|Admin Table Name", typeof(string), this.tablename_adminlist));
                lstReturn.Add(new CPluginVariable("Admin Settings|Column That Contains Admin Name", typeof(string), this.columnname_adminname));
            }
            //TeamSwap Settings
            lstReturn.Add(new CPluginVariable("TeamSwap Settings|Require Whitelist for Access", typeof(Boolean), this.requireTeamswapWhitelist));
            if (this.requireTeamswapWhitelist)
            {
                lstReturn.Add(new CPluginVariable("TeamSwap Settings|Use Database Whitelist", typeof(Boolean), this.useDatabaseTeamswapWhitelist));
                if (!this.useDatabaseTeamswapWhitelist)
                {
                    lstReturn.Add(new CPluginVariable("TeamSwap Settings|Static Player Whitelist", typeof(string[]), this.staticTeamswapWhitelistCache.ToArray()));
                }
            }
            lstReturn.Add(new CPluginVariable("TeamSwap Settings|Ticket Window High", typeof(int), this.teamSwapTicketWindowHigh));
            lstReturn.Add(new CPluginVariable("TeamSwap Settings|Ticket Window Low", typeof(int), this.teamSwapTicketWindowLow));

            lstReturn.Add(new CPluginVariable("Messaging Settings|Pre-Message List", typeof(string[]), this.preMessageList));
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            #region debugging
            if (Regex.Match(strVariable, @"Debug level").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                debugLevel = tmp;
            }
            #endregion
            #region server settings
            else if (Regex.Match(strVariable, @"Server ID").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.serverID = tmp;
            }
            #endregion
            #region ban settings
            else if (Regex.Match(strVariable, @"Ban Type").Success)
            {
                this.m_strBanTypeOption = strValue;

                if (String.Compare("Frostbite - Name", this.m_strBanTypeOption, true) == 0)
                {
                    this.m_banMethod = ADKAT_BanType.FrostbiteName;
                }
                else if (String.Compare("Frostbite - EA GUID", this.m_strBanTypeOption, true) == 0)
                {
                    this.m_banMethod = ADKAT_BanType.FrostbiteEaGuid;
                }
                else if (String.Compare("Punkbuster - GUID", this.m_strBanTypeOption, true) == 0)
                {
                    this.m_banMethod = ADKAT_BanType.PunkbusterGuid;
                }
            }
            #endregion
            #region command settings
            else if (Regex.Match(strVariable, @"Minimum Required Reason Length").Success)
            {
                this.requiredReasonLength = Int32.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Yell display time seconds").Success)
            {
                this.m_iShowMessageLength = Int32.Parse(strValue);
                this.m_strShowMessageLength = m_iShowMessageLength + "";
            }
            else if (Regex.Match(strVariable, @"Confirm Command").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strConfirmCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strConfirmCommand = ADKAT_CommandType.ConfirmCommand + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Cancel Command").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strCancelCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strCancelCommand = ADKAT_CommandType.CancelCommand + " COMMAND BLANK";
                }
            }
            else if (strVariable.EndsWith(@"Kill Player"))
            {
                if (strValue.Length > 0)
                {
                    this.m_strKillCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strKillCommand = ADKAT_CommandType.KillPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Kick Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strKickCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strKickCommand = ADKAT_CommandType.KickPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Temp-Ban Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strTemporaryBanCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strTemporaryBanCommand = ADKAT_CommandType.TempBanPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Permaban Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strPermanentBanCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strPermanentBanCommand = ADKAT_CommandType.PermabanPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Punish Player").Success)
            {
                if (strValue.Length > 0)
                {
                    //Punish logging is required for functionality
                    if (!strValue.ToLower().EndsWith("|log"))
                    {
                        strValue += "|log";
                    }
                    this.m_strPunishCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strPunishCommand = ADKAT_CommandType.PunishPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Forgive Player").Success)
            {
                if (strValue.Length > 0)
                {
                    //Forgive logging is required for functionality
                    if (!strValue.ToLower().EndsWith("|log"))
                    {
                        strValue += "|log";
                    }
                    this.m_strForgiveCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strForgiveCommand = ADKAT_CommandType.ForgivePlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"OnDeath Move Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strMoveCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strMoveCommand = ADKAT_CommandType.MovePlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Force Move Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strForceMoveCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strForceMoveCommand = ADKAT_CommandType.ForceMovePlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Teamswap Self").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strTeamswapCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strTeamswapCommand = ADKAT_CommandType.Teamswap + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Report Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strReportCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strReportCommand = ADKAT_CommandType.ReportPlayer + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Call Admin on Player").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strCallAdminCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strCallAdminCommand = ADKAT_CommandType.CallAdmin + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Admin Say").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strSayCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strSayCommand = ADKAT_CommandType.AdminSay + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Player Say").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strPlayerSayCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strPlayerSayCommand = ADKAT_CommandType.PlayerSay + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Admin Yell").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strYellCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strYellCommand = ADKAT_CommandType.AdminYell + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Player Yell").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strPlayerYellCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strPlayerYellCommand = ADKAT_CommandType.PlayerYell + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Restart Level").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strRestartLevelCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strRestartLevelCommand = ADKAT_CommandType.RestartLevel + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"Next Level").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strNextLevelCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strNextLevelCommand = ADKAT_CommandType.NextLevel + " COMMAND BLANK";
                }
            }
            else if (Regex.Match(strVariable, @"End Level").Success)
            {
                if (strValue.Length > 0)
                {
                    this.m_strEndLevelCommand = strValue;
                    rebindAllCommands();
                }
                else
                {
                    this.m_strEndLevelCommand = ADKAT_CommandType.EndLevel + " COMMAND BLANK";
                }
            }
            #endregion
            #region punishment settings
            else if (Regex.Match(strVariable, @"Act on Punishments").Success)
            {
                this.actOnPunishments = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Punishment Hierarchy").Success)
            {
                this.punishmentHierarchy = CPluginVariable.DecodeStringArray(strValue);
            }
            else if (Regex.Match(strVariable, @"Only Kill Players when Server in low population").Success)
            {
                this.onlyKillOnLowPop = Boolean.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Low Population Value").Success)
            {
                this.lowPopPlayerCount = Int32.Parse(strValue);
            }
            else if (Regex.Match(strVariable, @"Punishment Timeout").Success)
            {
                this.punishmentTimeout = Double.Parse(strValue);
            }
            #endregion
            #region admin settings
            else if (Regex.Match(strVariable, @"Use Database Admin List").Success)
            {
                if (this.useDatabaseAdminList = Boolean.Parse(strValue))
                {
                    if (this.isEnabled)
                    {
                        //Test the database connection
                        testDatabaseConnection();
                    }
                }
            }
            else if (Regex.Match(strVariable, @"Static Admin List").Success)
            {
                this.staticAdminCache = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (Regex.Match(strVariable, @"Admin Table Name").Success)
            {
                this.tablename_adminlist = strValue;
                if (this.useDatabaseAdminList)
                    this.fetchAdminList();
            }
            else if (Regex.Match(strVariable, @"Column That Contains Admin Name").Success)
            {
                this.columnname_adminname = strValue;
                if (this.useDatabaseAdminList)
                    this.fetchAdminList();
            }
            #endregion
            #region sql settings
            else if (Regex.Match(strVariable, @"MySQL Hostname").Success)
            {
                mySqlHostname = strValue;
                if (this.isEnabled)
                {
                    //Test the database connection
                    testDatabaseConnection();
                }
            }
            else if (Regex.Match(strVariable, @"MySQL Port").Success)
            {
                int tmp = 3306;
                int.TryParse(strValue, out tmp);
                if (tmp > 0 && tmp < 65536)
                {
                    mySqlPort = strValue;
                }
                else
                {
                    ConsoleException("Invalid value for MySQL Port: '" + strValue + "'. Must be number between 1 and 65535!");
                }
                if (this.isEnabled)
                {
                    //Test the database connection
                    testDatabaseConnection();
                }
            }
            else if (Regex.Match(strVariable, @"MySQL Database").Success)
            {
                this.mySqlDatabaseName = strValue;
                if (this.isEnabled)
                {
                    //Test the database connection
                    testDatabaseConnection();
                }
            }
            else if (Regex.Match(strVariable, @"MySQL Username").Success)
            {
                mySqlUsername = strValue;
                if (this.isEnabled)
                {
                    //Test the database connection
                    testDatabaseConnection();
                }
            }
            else if (Regex.Match(strVariable, @"MySQL Password").Success)
            {
                mySqlPassword = strValue;
                if (this.isEnabled)
                {
                    //Test the database connection
                    testDatabaseConnection();
                }
            }
            #endregion
            #region teamswap settings
            else if (Regex.Match(strVariable, @"Require Whitelist for Access").Success)
            {
                if (!(this.requireTeamswapWhitelist = Boolean.Parse(strValue)))
                {
                    //If no whitelist is necessary change use db whitelist to false
                    this.useDatabaseTeamswapWhitelist = false;
                }
            }
            else if (Regex.Match(strVariable, @"Static Player Whitelist").Success)
            {
                this.staticTeamswapWhitelistCache = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (Regex.Match(strVariable, @"Use Database Whitelist").Success)
            {
                if (this.useDatabaseTeamswapWhitelist = Boolean.Parse(strValue))
                {
                    if (this.isEnabled)
                    {
                        //Test the database connection
                        testDatabaseConnection();
                    }
                }
            }
            else if (Regex.Match(strVariable, @"Ticket Window High").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.teamSwapTicketWindowHigh = tmp;
            }
            else if (Regex.Match(strVariable, @"Ticket Window Low").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                this.teamSwapTicketWindowLow = tmp;
            }
            #endregion
            #region Messaging Settings
            else if (Regex.Match(strVariable, @"Pre-Message List").Success)
            {
                this.preMessageList = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            #endregion
        }

        private void rebindAllCommands()
        {
            this.DebugWrite("Entering rebindAllCommands", 6);

            Dictionary<String, ADKAT_CommandType> tempDictionary = new Dictionary<String, ADKAT_CommandType>();

            //Update confirm and cancel 
            this.m_strConfirmCommand = this.parseAddCommand(tempDictionary, this.m_strConfirmCommand, ADKAT_CommandType.ConfirmCommand);
            this.m_strCancelCommand = this.parseAddCommand(tempDictionary, this.m_strCancelCommand, ADKAT_CommandType.CancelCommand);
            //Update player punishment
            this.m_strKillCommand = this.parseAddCommand(tempDictionary, this.m_strKillCommand, ADKAT_CommandType.KillPlayer);
            this.m_strKickCommand = this.parseAddCommand(tempDictionary, this.m_strKickCommand, ADKAT_CommandType.KickPlayer);
            this.m_strTemporaryBanCommand = this.parseAddCommand(tempDictionary, this.m_strTemporaryBanCommand, ADKAT_CommandType.TempBanPlayer);
            this.m_strPermanentBanCommand = this.parseAddCommand(tempDictionary, this.m_strPermanentBanCommand, ADKAT_CommandType.PermabanPlayer);
            this.m_strPunishCommand = this.parseAddCommand(tempDictionary, this.m_strPunishCommand, ADKAT_CommandType.PunishPlayer);
            this.m_strForgiveCommand = this.parseAddCommand(tempDictionary, this.m_strForgiveCommand, ADKAT_CommandType.ForgivePlayer);
            this.m_strMoveCommand = this.parseAddCommand(tempDictionary, this.m_strMoveCommand, ADKAT_CommandType.MovePlayer);
            this.m_strForceMoveCommand = this.parseAddCommand(tempDictionary, this.m_strForceMoveCommand, ADKAT_CommandType.ForceMovePlayer);
            this.m_strTeamswapCommand = this.parseAddCommand(tempDictionary, this.m_strTeamswapCommand, ADKAT_CommandType.Teamswap);
            this.m_strReportCommand = this.parseAddCommand(tempDictionary, this.m_strReportCommand, ADKAT_CommandType.ReportPlayer);
            this.m_strCallAdminCommand = this.parseAddCommand(tempDictionary, this.m_strCallAdminCommand, ADKAT_CommandType.CallAdmin);
            //Update Messaging
            this.m_strSayCommand = this.parseAddCommand(tempDictionary, this.m_strSayCommand, ADKAT_CommandType.AdminSay);
            this.m_strPlayerSayCommand = this.parseAddCommand(tempDictionary, this.m_strPlayerSayCommand, ADKAT_CommandType.PlayerSay);
            this.m_strYellCommand = this.parseAddCommand(tempDictionary, this.m_strYellCommand, ADKAT_CommandType.AdminYell);
            this.m_strPlayerYellCommand = this.parseAddCommand(tempDictionary, this.m_strPlayerYellCommand, ADKAT_CommandType.PlayerYell);
            this.m_strPreYellCommand = this.parseAddCommand(tempDictionary, this.m_strPreYellCommand, ADKAT_CommandType.PreYell);
            this.m_strPreSayCommand = this.parseAddCommand(tempDictionary, this.m_strPreSayCommand, ADKAT_CommandType.PreSay);


            //Update level controls
            this.m_strRestartLevelCommand = this.parseAddCommand(tempDictionary, this.m_strRestartLevelCommand, ADKAT_CommandType.RestartLevel);
            this.m_strNextLevelCommand = this.parseAddCommand(tempDictionary, this.m_strNextLevelCommand, ADKAT_CommandType.NextLevel);
            this.m_strEndLevelCommand = this.parseAddCommand(tempDictionary, this.m_strEndLevelCommand, ADKAT_CommandType.EndLevel);

            //Overwrite command string dictionary with the new one
            this.ADKAT_CommandStrings = tempDictionary;

            this.DebugWrite("rebindAllCommands finished!", 6);
        }

        private String parseAddCommand(Dictionary<String, ADKAT_CommandType> tempDictionary, String strCommand, ADKAT_CommandType enumCommand)
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

                //Check for additional input
                if (split.Length > 1)
                {
                    //There is additional input, check if it's valid
                    //Right now only accepting 'log' as an additional input
                    if (split[1] == "log")
                    {
                        this.setLoggingForCommand(enumCommand, true);
                    }
                    else
                    {
                        this.ConsoleError("Invalid command format for: " + enumCommand);
                        return enumCommand + " INVALID FORMAT";
                    }
                }
                //Set logging to false for this command
                else
                {
                    this.setLoggingForCommand(enumCommand, false);
                }
                this.DebugWrite("parseAddCommand Finished!", 7);
                return strCommand;
            }
            catch (ArgumentException e)
            {
                //The command attempting to add was the same name as another command currently in the dictionary, inform the user.
                this.ConsoleError("Duplicate Command detected for " + enumCommand + ". That command will not work.");
                return enumCommand + " DUPLICATE COMMAND";
            }
            catch (Exception e)
            {
                this.ConsoleError("Unknown error for  " + enumCommand + ". Message: " + e.Message + ". Contact ColColonCleaner.");
                return enumCommand + " UNKNOWN ERROR";
            }
        }

        private void setLoggingForCommand(ADKAT_CommandType enumCommand, Boolean newLoggingEnabled)
        {
            try
            {
                //Get current value
                bool currentLoggingEnabled = this.ADKAT_LoggingSettings[enumCommand];
                this.DebugWrite("set logging for " + enumCommand + " to " + newLoggingEnabled + " from " + currentLoggingEnabled, 7);
                //Only perform replacement if the current value is different than what we want
                if (currentLoggingEnabled != newLoggingEnabled)
                {
                    this.DebugWrite("Changing logging option for " + enumCommand + " to " + newLoggingEnabled, 2);
                    this.ADKAT_LoggingSettings[enumCommand] = newLoggingEnabled;
                }
                else
                {
                    this.DebugWrite("Logging option for " + enumCommand + " still " + currentLoggingEnabled + ".", 3);
                }
            }
            catch (Exception e)
            {
                this.DebugWrite("Current value null?: " + e.Message, 6);
                this.DebugWrite("Setting initial logging option for " + enumCommand + " to " + newLoggingEnabled, 6);
                ADKAT_LoggingSettings.Add(enumCommand, newLoggingEnabled);
                this.DebugWrite("Logging option set successfuly", 6);
            }
        }

        #endregion

        #region Procon Events : General

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name, "OnVersion", "OnServerInfo", "OnResponseError", "OnListPlayers", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerKilled", "OnPlayerSpawned", "OnPlayerTeamChange", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOverPlayers", "OnRoundOver", "OnRoundOverTeamScores", "OnLoadingLevel", "OnLevelStarted", "OnLevelLoaded");
        }

        public void OnPluginEnable()
        {
            isEnabled = true;
            ConsoleWrite("^b^2Enabled!^n^0 Version: " + GetPluginVersion());
            //Test the database connection
            testDatabaseConnection();
        }

        public void OnPluginDisable()
        {
            ConsoleWrite("Disabling command functionality");
            isEnabled = false;
            ConsoleWrite("^b^1Disabled! =(^n^0");
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            if (isEnabled)
            {
                //Update last call time
                this.lastListPlayersRequest = DateTime.Now;
                //this.updating used as a primitive thread sync
                if (this.updating)
                {
                    return;
                }
                else
                {
                    this.updating = true;
                }
                Dictionary<String, CPlayerInfo> currentPlayers = new Dictionary<String, CPlayerInfo>();
                //Reset the player counts of both sides and recount everything
                this.USPlayerCount = 0;
                this.RUPlayerCount = 0;
                foreach (CPlayerInfo player in players)
                {
                    if (player.TeamID == this.USTeamId)
                    {
                        this.USPlayerCount++;
                    }
                    else
                    {
                        this.RUPlayerCount++;
                    }
                    currentPlayers.Add(player.SoldierName, player);
                }
                this.currentPlayers = currentPlayers;
                this.playerList = players;
                //perform player switching
                this.runTeamSwap();
                this.updating = false;
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            if (isEnabled)
            {
                //Get the team scores
                this.serverInfo = serverInfo;
                List<TeamScore> listCurrTeamScore = serverInfo.TeamScores;
                int iTeam0Score = listCurrTeamScore[0].Score;
                int iTeam1Score = listCurrTeamScore[1].Score;
                this.lowestTicketCount = (iTeam0Score < iTeam1Score) ? (iTeam0Score) : (iTeam1Score);
                this.highestTicketCount = (iTeam0Score > iTeam1Score) ? (iTeam0Score) : (iTeam1Score);
            }
        }

        //execute the swap code on player leaving
        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            //Only call when a player is waiting to be switched
            if (isEnabled && (this.USMoveQueue.Count > 0 || this.RUMoveQueue.Count > 0))
            {
                //When any player leaves, the list of players needs to be updated.
                this.callListPlayers(false);
            }
        }

        //execute the swap code on player teamchange
        public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId)
        {
            //Only call when a player is waiting to be switched
            if (isEnabled && (this.USMoveQueue.Count > 0 || this.RUMoveQueue.Count > 0))
            {
                //When any player changes team, the list of players needs to be updated.
                this.callListPlayers(false);
            }
        }

        public void callListPlayers(Boolean bypass)
        {
            if (DateTime.Now > this.lastListPlayersRequest.AddSeconds(5) || bypass)
            {
                this.ExecuteCommand("procon.protected.send", "admin.listPlayers", "all");
            }
        }

        //Move delayed players when they are killed
        public override void OnPlayerKilled(Kill kKillerVictimDetails)
        {
            //Used for delayed player moving
            if (isEnabled)
            {
                this.DebugWrite("Player Killed", 6);
                //Only do a search if the list contains players
                if (this.onDeathMoveList.Count > 0)
                {
                    CPlayerInfo playerToMove = null;
                    this.DebugWrite("Checking for this player in list of " + this.onDeathMoveList.Count + " deathmove players.", 6);
                    foreach (CPlayerInfo player in this.onDeathMoveList)
                    {
                        if (player.SoldierName.Equals(kKillerVictimDetails.Victim.SoldierName))
                        {
                            playerToMove = player;
                            break;
                        }
                    }
                    if (playerToMove != null)
                    {
                        //if the player is found, remove their ondeath info and send them to teamswap
                        this.DebugWrite("deathmove player found. swapping.", 6);
                        this.onDeathMoveList.Remove(playerToMove);
                        this.teamSwapPlayer(playerToMove);
                    }
                }
                else
                {
                    this.DebugWrite("No deathmove players", 6);
                }
            }
        }

        #endregion

        #region Procon Events : Messaging

        //all messaging is redirected to global chat for analysis
        public override void OnGlobalChat(string speaker, string message)
        {
            if (isEnabled)
            {
                if (message.StartsWith("@") || message.StartsWith("!"))
                {
                    message = message.Substring(1);
                }
                else if (message.StartsWith("/@") || message.StartsWith("/!"))
                {
                    message = message.Substring(2);
                }
                else
                {
                    //If the message does not cause either of the above clauses, then ignore it.
                    return;
                }
                //Create the record
                this.createRecord(speaker, message);
            }
        }
        public override void OnTeamChat(string speaker, string message, int teamId) { this.OnGlobalChat(speaker, message); }
        public override void OnSquadChat(string speaker, string message, int teamId, int squadId) { this.OnGlobalChat(speaker, message); }

        public void playerSayMessage(string target, string message)
        {
            ExecuteCommand("procon.protected.send", "admin.say", message, "player", target);
            ExecuteCommand("procon.protected.chat.write", string.Format("(PlayerSay {0}) ", target) + message);
        }

        #endregion

        #region Teamswap Methods

        //runs through both team swap queues and performs the swapping
        public void runTeamSwap()
        {
            //assume the max player count per team is 32 if no server info has been provided
            int maxPlayerCount = (this.serverInfo != null) ? (this.serverInfo.MaxPlayerCount / 2) : (32);
            Boolean movedPlayer;
            do
            {
                movedPlayer = false;
                if (this.RUMoveQueue.Count > 0)
                {
                    if (this.USPlayerCount < maxPlayerCount)
                    {
                        CPlayerInfo player = this.RUMoveQueue.Dequeue();
                        ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, this.USTeamId.ToString(), "1", "true");
                        this.playerSayMessage(player.SoldierName, "Swapping you from team RU to team US");
                        movedPlayer = true;
                        USPlayerCount++;
                    }
                }
                if (this.USMoveQueue.Count > 0)
                {
                    if (this.RUPlayerCount < maxPlayerCount)
                    {
                        CPlayerInfo player = this.USMoveQueue.Dequeue();
                        ExecuteCommand("procon.protected.send", "admin.movePlayer", player.SoldierName, this.RUTeamId.ToString(), "1", "true");
                        this.playerSayMessage(player.SoldierName, "Swapping you from team US to team RU");
                        movedPlayer = true;
                        RUPlayerCount++;
                    }
                }
            } while (movedPlayer);
        }

        //Adds a player to the proper move queue
        public void teamSwapPlayer(CPlayerInfo player)
        {
            if (player.TeamID == this.USTeamId)
            {
                if (!this.containsCPlayerInfo(this.USMoveQueue, player.SoldierName))
                {
                    this.USMoveQueue.Enqueue(player);
                    this.playerSayMessage(player.SoldierName, "You have been added to the (US -> RU) TeamSwap queue in position " + (this.indexOfCPlayerInfo(this.USMoveQueue, player.SoldierName) + 1) + ".");
                }
                else
                {
                    this.playerSayMessage(player.SoldierName, "(US -> RU) queue: Position " + (this.indexOfCPlayerInfo(this.USMoveQueue, player.SoldierName) + 1));
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
                    this.playerSayMessage(player.SoldierName, "(RU -> US) queue: Position " + (this.indexOfCPlayerInfo(this.RUMoveQueue, player.SoldierName) + 1));
                }
            }
            //call an update of the player list, this will move players when possible
            this.callListPlayers(true);
        }

        //Whether a move queue contains a given player
        private bool containsCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
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

        //Helper method to find a player's information in the move queue
        private CPlayerInfo getCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            CPlayerInfo[] playerArray = queueList.ToArray();
            for (Int32 index = 0; index < queueList.Count; index++)
            {
                if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                {
                    return ((CPlayerInfo)playerArray[index]);
                }
            }
            return null;
        }

        //The index of a player in the move queue
        //TODO make this accessible via in-game command
        private Int32 indexOfCPlayerInfo(Queue<CPlayerInfo> queueList, String player)
        {
            CPlayerInfo[] playerArray = queueList.ToArray();
            for (Int32 index = 0; index < queueList.Count; index++)
            {
                if (((CPlayerInfo)playerArray[index]).SoldierName.Equals(player))
                {
                    return index;
                }
            }
            return -1;
        }

        #endregion

        #region Record Creation and Processing

        public void createRecord(String speaker, String message)
        {
            //Initial split of command by whitespace
            String[] splitCommand = message.Split(' ');

            //Create record for processing
            ADKAT_Record record = new ADKAT_Record();

            //GATE 1: Add general data
            record.source_name = speaker;
            record.server_id = this.serverID;
            record.record_time = DateTime.Now;

            //GATE 2: Add Command
            string commandString = splitCommand[0].ToLower();
            DebugWrite("Raw Command: " + commandString, 6);
            ADKAT_CommandType commandType = this.getCommand(commandString);
            //If command not parsable, return without creating
            if (commandType == ADKAT_CommandType.Default)
            {
                DebugWrite("Command not parsable", 6);
                this.playerSayMessage(speaker, "Invalid command format.");
                return;
            }
            message = message.TrimStart(commandString.ToCharArray()).Trim();
            record.command_type = commandType;

            //GATE 3: Add source
            //Check if player has the right to perform what he's asking
            if (!this.hasAccess(speaker, record.command_type))
            {
                DebugWrite("No rights to call command", 6);
                this.playerSayMessage(speaker, "No rights to use " + commandString + " command. Inquire about access on ADKGamers.com");
                //Return without creating if player doesn't have rights to do it
                return;
            }
            record.source_name = speaker;

            //GATE 4: Add specific data based on command type
            //Make sure the specific data entered is valid and worth parsing
            //Process the completed record
            switch (record.command_type)
            {
                //No command actions should be acted on here. This section prepares the record for upload. Only actions to be taken here are messaging to inform of invalid commands.
                //Items that need filling before calling processRecord:
                //target_name
                //target_guid
                //record_message

                #region MovePlayer
                case ADKAT_CommandType.MovePlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    record.record_message = "";
                    //Sets target_guid and completes target_name, then calls processRecord
                    confirmPlayerName(record);
                    break;
                #endregion
                #region ForceMovePlayer
                case ADKAT_CommandType.ForceMovePlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = "ForceMove";
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format, unable to submit.");
                        return;
                    }
                    //Sets target_guid and completes target_name, then calls processRecord
                    confirmPlayerName(record);
                    break;
                #endregion
                #region Teamswap
                case ADKAT_CommandType.Teamswap:
                    record.target_name = speaker;
                    record.record_message = "TeamSwap";
                    //Sets target_guid and completes target_name, then calls processRecord
                    confirmPlayerName(record);
                    break;
                #endregion
                #region KillPlayer
                case ADKAT_CommandType.KillPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        message = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = message;
                        DebugWrite("reason: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_message.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "Reason too short, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region KickPlayer
                case ADKAT_CommandType.KickPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        message = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = message;
                        DebugWrite("reason: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_message.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "Reason too short, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region TempBanPlayer
                case ADKAT_CommandType.TempBanPlayer:
                    try
                    {
                        int record_duration = 0;
                        DebugWrite("Raw Duration: " + splitCommand[1], 6);
                        Boolean valid = Int32.TryParse(splitCommand[1], out record_duration);
                        record.record_durationMinutes = record_duration;
                        message = message.TrimStart(splitCommand[1].ToCharArray()).Trim();
                        record.target_name = splitCommand[2];
                        DebugWrite("target: " + record.target_name, 6);
                        message = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        record.record_message = message;
                        DebugWrite("reason: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_message.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "Reason too short, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region PermabanPlayer
                case ADKAT_CommandType.PermabanPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        message = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = message;
                        DebugWrite("reason: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_message.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "Reason too short, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region PunishPlayer
                case ADKAT_CommandType.PunishPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        message = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = message;
                        DebugWrite("reason: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_message.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "Reason too short, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region ForgivePlayer
                case ADKAT_CommandType.ForgivePlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        message = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = message;
                        DebugWrite("reason: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_message.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "Reason too short, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region ReportPlayer
                case ADKAT_CommandType.ReportPlayer:
                    try
                    {
                        record.target_name = splitCommand[1];
                        message = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = message;
                        DebugWrite("reason: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_message.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "Reason too short, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region CallAdmin
                case ADKAT_CommandType.CallAdmin:
                    try
                    {
                        record.target_name = splitCommand[1];
                        message = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = message;
                        DebugWrite("reason: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no reason given, unable to submit.");
                        return;
                    }
                    if (record.record_message.Length < this.requiredReasonLength)
                    {
                        DebugWrite("reason too short", 6);
                        this.playerSayMessage(speaker, "Reason too short, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region EndLevel
                case ADKAT_CommandType.EndLevel:
                    try
                    {
                        record.record_message = "End Round";
                        String targetTeam = splitCommand[1];
                        DebugWrite("target team: " + targetTeam, 6);
                        if (targetTeam.ToLower().Contains("us"))
                        {
                            record.target_name = "US Team";
                            record.target_guid = "US Team";
                            record.record_message += " (US Win)";
                        }
                        else if (targetTeam.ToLower().Contains("ru"))
                        {
                            record.target_name = "RU Team";
                            record.target_guid = "RU Team";
                            record.record_message += " (RU Win)";
                        }
                        else
                        {
                            this.playerSayMessage(record.source_name, "Use 'US' or 'RU' as team names to end round");
                        }
                        confirmAction(record);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format, use 'RU' or 'US' to end round.");
                        return;
                    }
                    break;
                #endregion
                #region RestartLevel
                case ADKAT_CommandType.RestartLevel:
                    record.target_name = "Server";
                    record.target_guid = "Server";
                    record.record_message = "Restart Round";
                    confirmAction(record);
                    break;
                #endregion
                #region NextLevel
                case ADKAT_CommandType.NextLevel:
                    record.target_name = "Server";
                    record.target_guid = "Server";
                    record.record_message = "Run Next Map";
                    confirmAction(record);
                    break;
                #endregion
                #region AdminSay
                case ADKAT_CommandType.AdminSay:
                    record.target_name = "Server";
                    record.target_guid = "Server";
                    try
                    {
                        String adminMessage = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("message: " + adminMessage, 6);
                        record.record_message = adminMessage;
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format, no message given.");
                        return;
                    }
                    this.processRecord(record);
                    break;
                #endregion
                #region PreSay
                case ADKAT_CommandType.PreSay:
                    record.target_name = "Server";
                    record.target_guid = "Server";
                    try
                    {
                        if (this.preMessageList.Count > 0)
                        {
                            int preSayID = 0;
                            DebugWrite("Raw preSayID: " + splitCommand[1], 6);
                            Boolean valid = Int32.TryParse(splitCommand[1], out preSayID);
                            if (valid && (preSayID >= 0) && (preSayID < this.preMessageList.Count))
                            {
                                record.record_message = this.preMessageList.Get(preSayID);
                                record.command_type = ADKAT_CommandType.AdminSay;
                            }
                            else
                            {
                                DebugWrite("invalid pre message id", 6);
                                this.playerSayMessage(speaker, "Invalid Pre-Message ID. Valid IDs 0-" + this.preMessageList.Count - 1);
                                return;
                            }
                        }
                        else
                        {
                            DebugWrite("no premessages stored", 6);
                            this.playerSayMessage(speaker, "No Pre-Messages stored");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format, no pre-message ID given.");
                        return;
                    }
                    confirmAction(record);
                    break;
                #endregion
                #region AdminYell
                case ADKAT_CommandType.AdminYell:
                    record.target_name = "Server";
                    record.target_guid = "Server";
                    try
                    {
                        String adminMessage = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("message: " + adminMessage, 6);
                        record.record_message = adminMessage.ToUpper();
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format, no message given.");
                        return;
                    }
                    this.processRecord(record);
                    break;
                #endregion
                #region PreYell
                case ADKAT_CommandType.PreYell:
                    record.target_name = "Server";
                    record.target_guid = "Server";
                    try
                    {
                        if (this.preMessageList.Count > 0)
                        {
                            int preYellID = 0;
                            DebugWrite("Raw preYellID: " + splitCommand[1], 6);
                            Boolean valid = Int32.TryParse(splitCommand[1], out preYellID);
                            if (valid && (preYellID >= 0) && (preYellID < this.preMessageList.Count))
                            {
                                record.record_message = this.preMessageList.Get(preYellID);
                                record.command_type = ADKAT_CommandType.AdminYell;
                            }
                            else
                            {
                                DebugWrite("invalid pre message id", 6);
                                this.playerSayMessage(speaker, "Invalid Pre-Message ID. Valid IDs 0-" + this.preMessageList.Count - 1);
                                return;
                            }
                        }
                        else
                        {
                            DebugWrite("no premessages stored", 6);
                            this.playerSayMessage(speaker, "No Pre-Messages stored");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format, no Pre-Message ID given.");
                        return;
                    }
                    confirmAction(record);
                    break;
                #endregion
                #region PlayerSay
                case ADKAT_CommandType.PlayerSay:
                    try
                    {
                        record.target_name = splitCommand[1];
                        String adminMessage = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = adminMessage;
                        DebugWrite("message: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no message given, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region PlayerYell
                case ADKAT_CommandType.PlayerYell:
                    try
                    {
                        record.target_name = splitCommand[1];
                        String adminMessage = message.TrimStart(record.target_name.ToCharArray()).Trim();
                        DebugWrite("target: " + record.target_name, 6);
                        record.record_message = adminMessage.ToUpper();
                        DebugWrite("message: " + record.record_message, 6);
                    }
                    catch (Exception e)
                    {
                        DebugWrite("invalid format", 6);
                        this.playerSayMessage(speaker, "Invalid command format or no message given, unable to submit.");
                        return;
                    }
                    confirmPlayerName(record);
                    break;
                #endregion
                #region ConfirmCommand
                case ADKAT_CommandType.ConfirmCommand:
                    ADKAT_Record recordAttempt = null;
                    this.actionAttemptList.TryGetValue(speaker, out recordAttempt);
                    if (recordAttempt != null)
                    {
                        this.actionAttemptList.Remove(speaker);
                        this.processRecord(recordAttempt);
                    }
                    return;
                #endregion
                #region CancelCommand
                case ADKAT_CommandType.CancelCommand:
                    this.actionAttemptList.Remove(speaker);
                    return;
                #endregion
                default:
                    return;
            }
            return;
        }

        //Attempts to parse the command from a string
        private ADKAT_CommandType getCommand(string commandString)
        {
            ADKAT_CommandType command = ADKAT_CommandType.Default;
            this.ADKAT_CommandStrings.TryGetValue(commandString, out command);
            return command;
        }

        public void confirmAction(ADKAT_Record record)
        {
            //Send record to attempt list
            this.playerSayMessage(record.source_name, "Confirm " + record.command_type + ": " + record.record_message);
            this.actionAttemptList.Remove(record.source_name);
            this.actionAttemptList.Add(record.source_name, record);
        }

        //Used for player name suggestion
        public void confirmPlayerName(ADKAT_Record record)
        {
            //Check if player exists in the game, or suggest a player
            foreach (CPlayerInfo playerInfo in this.playerList)
            {
                //If they entered the full player name, dont ask for completion just process it
                if (playerInfo.SoldierName.ToLower().Equals(record.target_name.ToLower()))
                {
                    //Player found, grab guid and name
                    record.target_guid = playerInfo.GUID;
                    record.target_name = playerInfo.SoldierName;
                    record.targetPlayerInfo = playerInfo;
                    //Process record right away
                    this.processRecord(record);
                    return;
                }
                //If they entered a partial name then suggest the first player that contains the partial they entered
                else if (playerInfo.SoldierName.ToLower().Contains(record.target_name.ToLower()))
                {
                    //Possible player found, grab guid
                    record.target_guid = playerInfo.GUID;
                    record.target_name = playerInfo.SoldierName;
                    record.targetPlayerInfo = playerInfo;
                    //Send record to attempt list
                    this.playerSayMessage(record.source_name, "Did you mean: " + playerInfo.SoldierName + "?");
                    this.actionAttemptList.Remove(record.source_name);
                    this.actionAttemptList.Add(record.source_name, record);
                    return;
                }
            }
            //No player found
            DebugWrite("player not found", 6);
            this.playerSayMessage(record.source_name, "Player not found.");
            return;
        }

        /*
         * This method handles uploading of records and calling their action methods
         * Will only upload a record if upload setting for that command is true, or if uploading is required
         */
        private void processRecord(ADKAT_Record record)
        {
            //Perform Actions
            switch (record.command_type)
            {
                case ADKAT_CommandType.MovePlayer:
                    this.conditionalUploadRecord(record);
                    this.moveTarget(record);
                    break;
                case ADKAT_CommandType.ForceMovePlayer:
                    this.conditionalUploadRecord(record);
                    this.forceMoveTarget(record);
                    break;
                case ADKAT_CommandType.Teamswap:
                    this.conditionalUploadRecord(record);
                    this.forceMoveTarget(record);
                    break;
                case ADKAT_CommandType.KillPlayer:
                    this.conditionalUploadRecord(record);
                    this.killTarget(record, "");
                    break;
                case ADKAT_CommandType.KickPlayer:
                    this.conditionalUploadRecord(record);
                    this.kickTarget(record, "");
                    break;
                case ADKAT_CommandType.TempBanPlayer:
                    this.conditionalUploadRecord(record);
                    this.tempBanTarget(record, "");
                    break;
                case ADKAT_CommandType.PermabanPlayer:
                    this.conditionalUploadRecord(record);
                    this.permaBanTarget(record, "");
                    break;
                case ADKAT_CommandType.PunishPlayer:
                    //If the record is a punish, check if it can be uploaded
                    if (this.canPunish(record))
                    {
                        //Upload for punish is required
                        this.uploadRecord(record);
                        this.punishTarget(record);
                    }
                    else
                    {
                        this.playerSayMessage(record.source_name, record.target_name + " already punished in the last " + this.punishmentTimeout + " minute(s).");
                    }
                    break;
                case ADKAT_CommandType.ForgivePlayer:
                    //Upload for forgive is required
                    //No restriction on forgives/minute
                    this.uploadRecord(record);
                    this.forgiveTarget(record);
                    break;
                case ADKAT_CommandType.ReportPlayer:
                    this.conditionalUploadRecord(record);
                    this.reportTarget(record);
                    break;
                case ADKAT_CommandType.CallAdmin:
                    this.conditionalUploadRecord(record);
                    this.callAdminOnTarget(record);
                    break;
                case ADKAT_CommandType.RestartLevel:
                    this.conditionalUploadRecord(record);
                    this.restartLevel(record);
                    break;
                case ADKAT_CommandType.NextLevel:
                    this.conditionalUploadRecord(record);
                    this.nextLevel(record);
                    break;
                case ADKAT_CommandType.EndLevel:
                    this.conditionalUploadRecord(record);
                    this.endLevel(record);
                    break;
                case ADKAT_CommandType.AdminSay:
                    this.conditionalUploadRecord(record);
                    this.adminSay(record);
                    break;
                case ADKAT_CommandType.PlayerSay:
                    this.conditionalUploadRecord(record);
                    this.playerSay(record);
                    break;
                case ADKAT_CommandType.AdminYell:
                    this.conditionalUploadRecord(record);
                    this.adminYell(record);
                    break;
                case ADKAT_CommandType.PlayerYell:
                    this.conditionalUploadRecord(record);
                    this.playerYell(record);
                    break;
                default:
                    break;
            }
        }

        //Checks the logging setting for a record type to see if it should be sent to database
        //If yes then it's sent, if not then it's ignored
        private void conditionalUploadRecord(ADKAT_Record record)
        {
            if (this.ADKAT_LoggingSettings[record.command_type])
            {
                this.DebugWrite("Uploading record for " + record.command_type, 6);
                //Upload Record
                this.uploadRecord(record);
            }
            else
            {
                this.DebugWrite("Skipping record upload for " + record.command_type, 6);
            }
        }

        #endregion

        #region Action Methods

        public void moveTarget(ADKAT_Record record)
        {
            this.onDeathMoveList.Add(record.targetPlayerInfo);
            this.DebugWrite("Player set to move on next death", 6);
            this.playerSayMessage(record.source_name, record.target_name + " will be sent to teamswap on their next death.");
        }

        public void forceMoveTarget(ADKAT_Record record)
        {
            this.DebugWrite("Entering forceMoveTarget", 6);

            if (record.command_type == ADKAT_CommandType.Teamswap)
            {
                if (this.isAdmin(record.source_name) || ((this.teamSwapTicketWindowHigh >= this.highestTicketCount) && (this.teamSwapTicketWindowLow <= this.lowestTicketCount)))
                {
                    this.DebugWrite("Calling Teamswap on self", 6);
                    teamSwapPlayer(record.targetPlayerInfo);
                }
                else
                {
                    this.DebugWrite("Player unable to teamswap", 6);
                    this.playerSayMessage(record.source_name, "You cannot TeamSwap at this time. Game outside ticket window [" + this.teamSwapTicketWindowLow + ", " + this.teamSwapTicketWindowHigh + "].");
                }
            }
            else
            {
                this.DebugWrite("Calling Teamswap on target", 6);
                this.playerSayMessage(record.source_name, "" + record.target_name + " sent to teamswap.");
                teamSwapPlayer(record.targetPlayerInfo);
            }

            this.DebugWrite("Exiting forceMoveTarget", 6);
        }

        public void killTarget(ADKAT_Record record, string additionalMessage)
        {
            //Perform actions
            ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_name);
            this.playerSayMessage(record.target_name, "Killed by admin for: " + record.record_message + ". " + additionalMessage);
            this.playerSayMessage(record.source_name, "You KILLED " + record.target_name + " for " + record.record_message + ". " + additionalMessage);
        }

        public void kickTarget(ADKAT_Record record, string additionalMessage)
        {
            //Perform Actions
            ExecuteCommand("procon.protected.send", "admin.kickPlayer", record.target_name, record.record_message + ". " + additionalMessage);
            this.playerSayMessage(record.target_name, "Killed by admin for: " + record.record_message + "." + additionalMessage);
            this.playerSayMessage(record.source_name, "You KICKED " + record.target_name + " for " + record.record_message + ". ");
        }

        public void tempBanTarget(ADKAT_Record record, string additionalMessage)
        {
            Int32 seconds = record.record_durationMinutes * 60;
            //Perform Actions
            switch (this.m_banMethod)
            {
                case ADKAT_BanType.FrostbiteName:
                    ExecuteCommand("procon.protected.send", "banList.add", "name", record.target_name, "seconds", seconds + "", record.record_message + ". " + additionalMessage);
                    ExecuteCommand("procon.protected.send", "banList.save");
                    ExecuteCommand("procon.protected.send", "banList.list");
                    break;
                case ADKAT_BanType.FrostbiteEaGuid:
                    ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_guid, "seconds", seconds + "", record.record_message + ". " + additionalMessage);
                    ExecuteCommand("procon.protected.send", "banList.save");
                    ExecuteCommand("procon.protected.send", "banList.list");
                    break;
                case ADKAT_BanType.PunkbusterGuid:
                    this.ExecuteCommand("procon.protected.send", "punkBuster.pb_sv_command", String.Format("pb_sv_kick \"{0}\" {1} \"{2}\"", record.target_name, record.record_durationMinutes.ToString(), "BC2! " + record.record_message + ". " + additionalMessage));
                    break;
                default:
                    break;
            }
            this.playerSayMessage(record.source_name, "You TEMP BANNED " + record.target_name + " for " + record.record_durationMinutes + " minutes. " + additionalMessage);
        }

        public void permaBanTarget(ADKAT_Record record, string additionalMessage)
        {
            //Perform Actions
            switch (this.m_banMethod)
            {
                case ADKAT_BanType.FrostbiteName:
                    ExecuteCommand("procon.protected.send", "banList.add", "name", record.target_name, "perm", record.record_message + ". " + additionalMessage);
                    ExecuteCommand("procon.protected.send", "banList.save");
                    ExecuteCommand("procon.protected.send", "banList.list");
                    break;
                case ADKAT_BanType.FrostbiteEaGuid:
                    ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_guid, "perm", record.record_message + ". " + additionalMessage);
                    ExecuteCommand("procon.protected.send", "banList.save");
                    ExecuteCommand("procon.protected.send", "banList.list");
                    break;
                case ADKAT_BanType.PunkbusterGuid:
                    this.ExecuteCommand("procon.protected.send", "punkBuster.pb_sv_command", String.Format("pb_sv_ban \"{0}\" \"{1}\"", record.target_name, "BC2! " + record.record_message + ". " + additionalMessage));
                    break;
                default:
                    break;
            }
            this.playerSayMessage(record.source_name, "You PERMA BANNED " + record.target_name + "! Get a vet admin NOW!" + additionalMessage);
        }

        public void punishTarget(ADKAT_Record record)
        {
            if (this.actOnPunishments)
            {
                //Get number of points the player from server
                int points = this.fetchPoints(record.target_guid, this.serverID);
                //Get the proper action to take for player punishment
                string action = "noaction";
                if (points > (this.punishmentHierarchy.Length - 1))
                {
                    action = this.punishmentHierarchy[this.punishmentHierarchy.Length - 1];
                }
                else if (points > 0)
                {
                    action = this.punishmentHierarchy[points - 1];
                }
                //Set additional message
                string additionalMessage = "(" + points + " infraction points)";

                //Call correct action
                if (action.Equals("kill") || (this.onlyKillOnLowPop && this.playerList.Count < this.lowPopPlayerCount))
                {
                    this.killTarget(record, additionalMessage);
                }
                else if (action.Equals("kick"))
                {
                    this.kickTarget(record, additionalMessage);
                }
                else if (action.Equals("tban60"))
                {
                    record.record_durationMinutes = 60;
                    this.tempBanTarget(record, additionalMessage);
                }
                else if (action.Equals("tbanweek"))
                {
                    record.record_durationMinutes = 10080;
                    this.tempBanTarget(record, additionalMessage);
                }
                else if (action.Equals("ban"))
                {
                    this.permaBanTarget(record, additionalMessage);
                }
            }
            else
            {
                this.playerSayMessage(record.source_name, "Punish Logged for " + record.target_name);
                this.uploadAction(record);
            }
        }

        public void forgiveTarget(ADKAT_Record record)
        {
            this.playerSayMessage(record.source_name, "Forgive Logged for " + record.target_name);
            this.playerSayMessage(record.target_name, "Forgiven 1 infraction point. You now have " + this.fetchPoints(record.target_guid, record.server_id) + " point(s) against you.");
        }

        public void reportTarget(ADKAT_Record record)
        {
            foreach (String admin_name in this.databaseAdminCache)
            {
                this.playerSayMessage(admin_name, "REPORT: " + record.source_name + " reported " + record.target_name + " for " + record.record_message);
            }
            foreach (String admin_name in this.staticAdminCache)
            {
                this.playerSayMessage(admin_name, "REPORT: " + record.source_name + " reported " + record.target_name + " for " + record.record_message);
            }
            this.playerSayMessage(record.source_name, "Report sent to admins on " + record.target_name + " for " + record.record_message);
        }

        public void callAdminOnTarget(ADKAT_Record record)
        {
            foreach (String admin_name in this.databaseAdminCache)
            {
                this.playerSayMessage(admin_name, "ADMIN CALL: " + record.source_name + " called admin on " + record.target_name + " for " + record.record_message);
            }
            foreach (String admin_name in this.staticAdminCache)
            {
                this.playerSayMessage(admin_name, "ADMIN CALL: " + record.source_name + " called admin on " + record.target_name + " for " + record.record_message);
            }
            this.playerSayMessage(record.source_name, "Admin call sent on " + record.target_name + " for " + record.record_message);
        }

        public void restartLevel(ADKAT_Record record)
        {
            this.ExecuteCommand("procon.protected.send", "mapList.restartRound");
        }

        public void nextLevel(ADKAT_Record record)
        {
            this.ExecuteCommand("procon.protected.send", "mapList.runNextRound");
        }

        public void endLevel(ADKAT_Record record)
        {
            this.ExecuteCommand("procon.protected.send", "mapList.endRound", record.target_guid);
        }

        public void playerSay(ADKAT_Record record)
        {
            this.playerSayMessage(record.target_name, record.record_message);
        }

        public void adminSay(ADKAT_Record record)
        {
            this.ExecuteCommand("procon.protected.send", "admin.say", record.record_message, "all");
        }

        public void adminYell(ADKAT_Record record)
        {
            this.ExecuteCommand("procon.protected.send", "admin.yell", record.record_message, this.m_strShowMessageLength, "all");
        }

        public void playerYell(ADKAT_Record record)
        {
            this.ExecuteCommand("procon.protected.send", "admin.yell", record.record_message, this.m_strShowMessageLength, "player", record.target_name);
        }

        #endregion

        #region MySQL Methods

        private Boolean connectionCapable()
        {
            if ((this.mySqlDatabaseName != null && this.mySqlDatabaseName.Length > 0) &&
                (this.mySqlHostname != null && this.mySqlHostname.Length > 0) &&
                (this.mySqlPassword != null && this.mySqlPassword.Length > 0) &&
                (this.mySqlPort != null && this.mySqlPort.Length > 0) &&
                (this.mySqlUsername != null && this.mySqlUsername.Length > 0))
            {
                this.DebugWrite("MySql Connection capable. All variables in place.", 6);
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
                this.DebugWrite("Attempted to connect to database without all variables in place", 2);
                return null;
            }
        }

        private void testDatabaseConnection()
        {
            DebugWrite("testDatabaseConnection starting!", 6);
            if (this.connectionCapable())
            {
                try
                {
                    Boolean success = false;
                    //Prepare the connection string and create the connection object
                    using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                    {
                        this.ConsoleWrite("Attempting database connection.");
                        //Attempt a ping through the connection
                        if (databaseConnection.Ping())
                        {
                            //Connection good
                            this.ConsoleWrite("Database connection SUCCESS.");
                            success = true;
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
                        if (confirmDatabaseSetup())
                        {
                            //If the structure is good, fetch all access lists
                            this.fetchAllAccessLists();
                        }
                    }
                }
                catch (Exception e)
                {
                    //Invalid credentials or no connection to database
                    this.ConsoleError("Database connection FAILED with EXCEPTION. Bad credentials, invalid hostname, or invalid port.");
                }
            }
            else
            {
                this.ConsoleWrite("Not DB connection capable yet, complete sql connection variables.");
            }
            DebugWrite("testDatabaseConnection finished!", 6);
        }

        private Boolean confirmDatabaseSetup()
        {
            if (!this.connectionCapable())
            {
                this.DebugWrite("Attempted to confirm database setup without being connection capable.", 3);
            }
            this.DebugWrite("Confirming Database Structure.", 3);
            try
            {
                Boolean confirmed = true;
                if (!this.confirmTable("adkat_records"))
                {
                    this.ConsoleError("Main Record table not present in the database.");
                    this.runDBSetupScript();
                    if (!this.confirmTable("adkat_records"))
                    {
                        this.ConsoleError("After running setup script main record table still not present.");
                        confirmed = false;
                    }
                }
                if (this.useDatabaseTeamswapWhitelist && !this.confirmTable("adkat_teamswapwhitelist"))
                {
                    ConsoleError("adkat_teamswapwhitelist not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }
                if (this.useDatabaseAdminList && !this.confirmTable(this.tablename_adminlist))
                {
                    ConsoleError(this.tablename_adminlist + " not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }
                if (!this.confirmTable("adkat_actionlist"))
                {
                    ConsoleError("adkat_actionlist not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }
                if (!this.confirmTable("adkat_playerlist"))
                {
                    ConsoleError("adkat_playerlist not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }
                if (!this.confirmTable("adkat_playerpoints"))
                {
                    ConsoleError("adkat_playerpoints not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }
                if (!this.confirmTable("adkat_reports"))
                {
                    ConsoleError("adkat_reports not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }
                if (!this.confirmTable("adkat_naughtylist"))
                {
                    ConsoleError("adkat_naughtylist not present in the database. AdKats will not function properly.");
                    confirmed = false;
                }
                if (confirmed)
                {
                    this.DebugWrite("Database confirmed functional for AdKats use.", 3);
                }
                else
                {
                    this.ConsoleError("Database structure errors detected, not set up for AdKats use.");
                }
                return confirmed;
            }
            catch (Exception e)
            {
                ConsoleException("ERROR in helper_confirmDatabaseSetup: " + e.ToString());
                return false;
            }
        }

        private void runDBSetupScript()
        {
            try
            {
                ConsoleWrite("Running database setup script.");
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText =
                            @"
                            DROP TABLE IF EXISTS `adkat_records`;
                            DROP TABLE IF EXISTS `adkat_actionlist`;
                            DROP TABLE IF EXISTS `adkat_teamswapwhitelist`;
                            CREATE TABLE `adkat_records` (
                            `record_id` int(11) NOT NULL AUTO_INCREMENT,
                            `server_id` int(11) NOT NULL,
                            `command_type` enum('Move','ForceMove','Teamswap','Kill','Kick','TempBan','PermaBan','Punish','Forgive','Report','CallAdmin', 'AdminSay', 'PlayerSay', 'AdminYell', 'PlayerYell', 'RestartLevel', 'NextLevel', 'EndLevel') NOT NULL,
                            `record_durationMinutes` int(11) NOT NULL,
                            `target_guid` varchar(100) NOT NULL,
                            `target_name` varchar(45) NOT NULL,
                            `source_name` varchar(45) NOT NULL,
                            `record_message` varchar(100) NOT NULL,
                            `record_time` datetime NOT NULL,
                            PRIMARY KEY (`record_id`)
                            );
                            CREATE TABLE `adkat_actionlist` (
                            `action_id` int(11) NOT NULL AUTO_INCREMENT,
                            `server_id` int(11) NOT NULL,
                            `player_guid` varchar(100) NOT NULL,
                            `player_name` varchar(45) NOT NULL,
                            PRIMARY KEY (`action_id`)    
                            );
                            CREATE TABLE `adkat_teamswapwhitelist` (
                            `player_name` varchar(45) NOT NULL DEFAULT 'NOTSET',
                            PRIMARY KEY (`player_name`),
                            UNIQUE KEY `player_name_UNIQUE` (`player_name`)
                            );
                            CREATE OR REPLACE VIEW `adkat_playerlist` AS select `adkat_records`.`target_name` AS `player_name`,`adkat_records`.`target_guid` AS `player_guid`,`adkat_records`.`server_id` AS `server_id` from `adkat_records` group by `adkat_records`.`target_guid`,`adkat_records`.`server_id` order by `adkat_records`.`target_name`;
                            CREATE OR REPLACE VIEW `adkat_playerpoints` AS select `adkat_playerlist`.`player_name` AS `playername`,`adkat_playerlist`.`player_guid` AS `playerguid`,`adkat_playerlist`.`server_id` AS `serverid`,(select count(`adkat_records`.`target_guid`) from `adkat_records` where ((`adkat_records`.`command_type` = 'Punish') and (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`) and (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) AS `punishpoints`,(select count(`adkat_records`.`target_guid`) from `adkat_records` where ((`adkat_records`.`command_type` = 'Forgive') and (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`) and (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) AS `forgivepoints`,((select count(`adkat_records`.`target_guid`) from `adkat_records` where ((`adkat_records`.`command_type` = 'Punish') and (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`) and (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`))) - (select count(`adkat_records`.`target_guid`) from `adkat_records` where ((`adkat_records`.`command_type` = 'Forgive') and (`adkat_records`.`target_guid` = `adkat_playerlist`.`player_guid`) and (`adkat_records`.`server_id` = `adkat_playerlist`.`server_id`)))) AS `totalpoints` from `adkat_playerlist`;
                            CREATE OR REPLACE VIEW `adkat_reports` AS select `adkat_records`.`record_id` AS `record_id`,`adkat_records`.`server_id` AS `server_id`,`adkat_records`.`command_type` AS `command_type`,`adkat_records`.`record_durationMinutes` AS `record_durationMinutes`,`adkat_records`.`target_guid` AS `target_guid`,`adkat_records`.`target_name` AS `target_name`,`adkat_records`.`source_name` AS `source_name`,`adkat_records`.`record_message` AS `record_message`,`adkat_records`.`record_time` AS `record_time` from `adkat_records` where ((`adkat_records`.`command_type` = 'Report') or (`adkat_records`.`command_type` = 'CallAdmin'));
                            CREATE OR REPLACE VIEW `adkat_naughtylist` AS select `adkat_playerpoints`.`serverid` AS `server_id`,`adkat_playerpoints`.`playername` AS `player_name`,`adkat_playerpoints`.`totalpoints` AS `total_points` from `adkat_playerpoints` where (`adkat_playerpoints`.`totalpoints` > 0) order by `adkat_playerpoints`.`serverid`,`adkat_playerpoints`.`playername`;";
                        try
                        {
                            //Attempt to execute the query
                            if (command.ExecuteNonQuery() >= 0)
                            {
                                ConsoleWrite("Creation script successful, your database is now setup for AdKats use.");
                            }
                        }
                        catch (Exception e)
                        {
                            ConsoleException("Your database did not accept the script. Does your account have access to table creation? AdKats will not function properly. Exception: " + e.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.ConsoleException("ERROR in runDBSetupScript: " + e.ToString());
            }
        }

        private Boolean confirmTable(string tablename)
        {
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SHOW TABLES LIKE '" + tablename + "'";
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
                this.ConsoleException("ERROR in helper_confirmTable: " + e.ToString());
                return false;
            }
        }

        private void fetchAllAccessLists()
        {
            if (this.useDatabaseAdminList)
                fetchAdminList();
            if (this.useDatabaseTeamswapWhitelist)
                fetchTeamswapWhitelist();
        }

        private string PrepareMySqlConnectionString()
        {
            return "Server=" + mySqlHostname + ";Port=" + mySqlPort + ";Database=" + this.mySqlDatabaseName + ";Uid=" + mySqlUsername + ";Pwd=" + mySqlPassword + ";";
        }

        private void uploadRecord(ADKAT_Record record)
        {
            DebugWrite("postRecord starting!", 6);

            Boolean success = false;
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "INSERT INTO `" + this.mySqlDatabaseName + "`.`adkat_records` (`server_id`, `command_type`, `record_durationMinutes`,`target_guid`, `target_name`, `source_name`, `record_message`, `record_time`) VALUES (@server_id, @command_type, @record_durationMinutes, @target_guid, @target_name, @source_name, @record_message, @record_time)";
                        //Fill the command
                        DebugWrite(record.target_guid, 6);
                        command.Parameters.AddWithValue("@server_id", record.server_id);
                        string type = "";
                        //Convert enum to DB string
                        this.ADKAT_RecordTypes.TryGetValue(record.command_type, out type);
                        command.Parameters.AddWithValue("@command_type", type);
                        command.Parameters.AddWithValue("@record_durationMinutes", record.record_durationMinutes);
                        command.Parameters.AddWithValue("@target_guid", record.target_guid);
                        command.Parameters.AddWithValue("@target_name", record.target_name);
                        command.Parameters.AddWithValue("@source_name", record.source_name);
                        command.Parameters.AddWithValue("@record_message", record.record_message);
                        command.Parameters.AddWithValue("@record_time", record.record_time);
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
                DebugWrite(FormatMessage(e.ToString(), MessageTypeEnum.Exception), 3);
            }

            string temp = "";
            this.ADKAT_RecordTypes.TryGetValue(record.command_type, out temp);

            if (success)
            {
                DebugWrite(temp + " log for player " + record.target_name + " by " + record.source_name + " SUCCESSFUL!", 3);
            }
            else
            {
                ConsoleError(temp + " log for player '" + record.target_name + " by " + record.source_name + " FAILED!");
            }

            DebugWrite("postRecord finished!", 6);
        }

        private Boolean canPunish(ADKAT_Record record)
        {
            DebugWrite("canPunish starting!", 6);

            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        //command.CommandText = "select latest_time from (select record_time as `latest_time` from `" + this.mySqlDatabase + "`.`adkat_records` where `adkat_records`.`server_id` = " + record.server_id + " and `adkat_records`.`command_type` = 'Punish' and `adkat_records`.`target_guid` = '" + record.target_guid + "' order by latest_time desc limit 1) as temp where latest_time < (NOW() - INTERVAL '1' MINUTE)";
                        command.CommandText = "select record_time as `latest_time` from `" + this.mySqlDatabaseName + "`.`adkat_records` where `adkat_records`.`server_id` = " + record.server_id + " and `adkat_records`.`command_type` = 'Punish' and `adkat_records`.`target_guid` = '" + record.target_guid + "' order by latest_time desc limit 1";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            DateTime latestUpload;
                            if (reader.Read())
                            {
                                latestUpload = reader.GetDateTime("latest_time");
                                this.DebugWrite("player has at least one punish", 6);
                            }
                            else
                            {
                                return true;
                            }
                            if (record.record_time.CompareTo(latestUpload.AddMinutes(this.punishmentTimeout)) > 0)
                            {
                                this.DebugWrite("new punish > " + this.punishmentTimeout + " minutes over last logged punish", 6);
                                return true;
                            }
                            else
                            {
                                this.DebugWrite("can't upload punish", 6);
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }
            DebugWrite("ERROR in canPunish!", 6);
            return false;
        }

        private void uploadAction(ADKAT_Record record)
        {

            Boolean success = false;
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        //Set the insert command structure
                        command.CommandText = "INSERT INTO `" + this.mySqlDatabaseName + "`.`adkat_actionlist` (`server_id`, `player_guid`, `player_name`) VALUES (@server_id, @player_guid, @player_name)";
                        //Fill the command
                        command.Parameters.AddWithValue("@server_id", record.server_id);
                        command.Parameters.AddWithValue("@player_guid", record.target_guid);
                        command.Parameters.AddWithValue("@player_name", record.target_name);
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
                DebugWrite(FormatMessage(e.ToString(), MessageTypeEnum.Exception), 3);
            }


            if (success)
            {
                DebugWrite("Action log for player '" + record.target_name + " by admin " + record.source_name + " SUCCESSFUL!", 5);
            }
            else
            {
                DebugWrite("Action log for player '" + record.target_name + " by admin " + record.source_name + " FAILED!", 0);
            }
        }

        private int fetchPoints(string player_guid, int server_id)
        {
            DebugWrite("fetchPoints starting!", 6);

            int returnVal = -1;

            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT playername, playerguid, serverid, totalpoints FROM `" + this.mySqlDatabaseName + "`.`adkat_playerpoints` WHERE `playerguid` = '" + player_guid + "' AND `serverid` = " + server_id;
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                DebugWrite("getPoints found records for player " + reader.GetString("playername") + "!", 5);
                                returnVal = reader.GetInt32("totalpoints");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }

            DebugWrite("fetchPoints finished!", 6);

            return returnVal;
        }

        private void fetchAdminList()
        {
            DebugWrite("fetchAdminList starting!", 6);

            List<string> tempAdminList = new List<string>();

            Boolean success = false;
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT `" + this.columnname_adminname + "` AS `admin_name` FROM `" + this.mySqlDatabaseName + "`.`" + this.tablename_adminlist + "`";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                success = true;
                                string admin_name = reader.GetString("admin_name");
                                DebugWrite("Admin found: " + admin_name, 6);
                                //only use admin names not guids for now
                                tempAdminList.Add(admin_name);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }

            if (success)
            {
                this.databaseAdminCache = tempAdminList;
                ConsoleWrite("Admin List Fetched from Database. Admin Count: " + this.databaseAdminCache.Count);
            }
            else
            {
                ConsoleError("Either no admins in the admin table, or admin table/column names set incorrectly.");
            }

            DebugWrite("fetchAdminList finished!", 6);
        }

        private void fetchTeamswapWhitelist()
        {
            DebugWrite("fetchTeamswapWhitelist starting!", 6);

            List<string> tempTeamswapWhitelist = new List<string>();

            Boolean success = false;
            try
            {
                using (MySqlConnection databaseConnection = this.getDatabaseConnection())
                {
                    using (MySqlCommand command = databaseConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT player_name AS player_name FROM `" + this.mySqlDatabaseName + "`.`adkat_teamswapwhitelist`";
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                success = true;
                                string player_name = reader.GetString("player_name");
                                DebugWrite("Player found: " + player_name, 6);
                                //only use admin names not guids for now
                                tempTeamswapWhitelist.Add(player_name);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                DebugWrite(e.ToString(), 3);
            }

            if (success)
            {
                this.databaseTeamswapWhitelistCache = tempTeamswapWhitelist;
                DebugWrite("Teamswap Whitelist Fetched from Database. Whitelist Player Count: " + this.databaseTeamswapWhitelistCache.Count, 0);
            }
            else
            {
                DebugWrite("No whitelisted players found in the teamswap whitelist table.", 0);
            }

            DebugWrite("fetchTeamswapWhitelist finished!", 6);
        }

        #endregion

        #region Access Checking

        private Boolean hasAccess(String player_name, ADKAT_CommandType command)
        {
            //if (player_name == "ColColonCleaner")return true;
            switch (command)
            {
                case ADKAT_CommandType.MovePlayer:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.ForceMovePlayer:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.Teamswap:
                    if (this.requireTeamswapWhitelist)
                    {
                        if (this.isAdmin(player_name))
                        {
                            return true;
                        }
                        return this.isTeamswapWhitelisted(player_name);
                    }
                    else
                    {
                        return true;
                    }
                case ADKAT_CommandType.KillPlayer:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.KickPlayer:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.TempBanPlayer:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.PermabanPlayer:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.PunishPlayer:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.ForgivePlayer:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.ReportPlayer:
                    return true;
                case ADKAT_CommandType.CallAdmin:
                    return true;
                case ADKAT_CommandType.RestartLevel:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.NextLevel:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.EndLevel:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.AdminSay:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.PlayerSay:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.AdminYell:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.PlayerYell:
                    return this.isAdmin(player_name);
                case ADKAT_CommandType.ConfirmCommand:
                    return true;
                case ADKAT_CommandType.CancelCommand:
                    return true;
                default:
                    this.DebugWrite("Command not recognized in hasAccess", 6);
                    return false;
            }
        }

        private Boolean isAdmin(string player_name)
        {
            if (this.useDatabaseAdminList)
            {
                //If the cache of admins plugin-side doesnt contain the person, just make sure it doesnt need updating
                if (!this.databaseAdminCache.Contains(player_name))
                {
                    this.fetchAdminList();
                }
                return this.databaseAdminCache.Contains(player_name);
            }
            else
            {
                return this.staticAdminCache.Contains(player_name);
            }
        }

        private Boolean isTeamswapWhitelisted(string player_name)
        {
            if (this.useDatabaseTeamswapWhitelist)
            {
                //if the cache of teamswap whitelisted players plugin-side doesnt contain the person, just make sure it doesnt need updating
                if (!this.databaseTeamswapWhitelistCache.Contains(player_name))
                {
                    this.fetchTeamswapWhitelist();
                }
                return this.databaseTeamswapWhitelistCache.Contains(player_name);
            }
            else
            {
                return this.staticTeamswapWhitelistCache.Contains(player_name);
            }
        }

        #endregion

        #region Helper Classes

        public class ADKAT_Record
        {
            public int server_id = -1;
            public string target_guid = null;
            public string target_name = null;
            public string source_name = null;
            public ADKAT_CommandType command_type = ADKAT_CommandType.Default;
            public string record_message = null;
            public DateTime record_time = DateTime.Now;
            public Int32 record_durationMinutes = 0;

            //Sup Attributes
            public CPlayerInfo targetPlayerInfo;

            public ADKAT_Record(int server_id, ADKAT_CommandType command_type, Int32 record_durationMinutes, string target_guid, string target_name, string source_name, string record_message, DateTime record_time)
            {
                this.server_id = server_id;
                this.command_type = command_type;
                this.target_guid = target_guid;
                this.target_name = target_name;
                this.source_name = source_name;
                this.record_message = record_message;
                this.record_time = record_time;
                this.record_durationMinutes = record_durationMinutes;
            }

            public ADKAT_Record()
            {
            }
        }

        #endregion

        #region Logging

        public string FormatMessage(string msg, MessageTypeEnum type)
        {
            string prefix = "[^bAdKats^n] ";

            if (type.Equals(MessageTypeEnum.Warning))
            {
                prefix += "^1^bWARNING^0^n: ";
            }
            else if (type.Equals(MessageTypeEnum.Error))
            {
                prefix += "^1^bERROR^0^n: ";
            }
            else if (type.Equals(MessageTypeEnum.Exception))
            {
                prefix += "^1^bEXCEPTION^0^n: ";
            }

            return prefix + msg;
        }

        public void LogWrite(string msg)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
        }

        public void ConsoleWrite(string msg, MessageTypeEnum type)
        {
            LogWrite(FormatMessage(msg, type));
        }

        public void ConsoleWrite(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Normal);
        }

        public void ConsoleWarn(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Warning);
        }

        public void ConsoleError(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Error);
        }

        public void ConsoleException(string msg)
        {
            ConsoleWrite(msg, MessageTypeEnum.Exception);
        }

        public void DebugWrite(string msg, int level)
        {
            if (debugLevel >= level)
            {
                ConsoleWrite(msg, MessageTypeEnum.Normal);
            }
        }

        #endregion

    } // end AdKats
} // end namespace PRoConEvents
