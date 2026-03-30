using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

using PRoCon.Core;
using PRoCon.Core.Maps;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Players;

namespace PRoConEvents
{
    public partial class AdKats
    {
        // ===========================================================================================
        // Data Model Classes (lines 55475-55566, 61789-63629 from original AdKats.cs)
        // ===========================================================================================

        public class ABan
        {
            //Current exception state of the ban
            public DateTime ban_endTime;
            public Boolean ban_enforceGUID = true;
            public Boolean ban_enforceIP = false;
            public Boolean ban_enforceName = false;
            public AException ban_exception = null;

            public Int64 ban_id = -1;
            public String ban_notes = null;
            public ARecord ban_record = null;
            public DateTime ban_startTime;
            public String ban_status = "Active";
            public String ban_sync = null;
            public Int64 player_id = -1;
            //startTime and endTime are not set by AdKats, they are set in the database.
            //startTime and endTime will be valid only when bans are fetched from the database
        }

        public class ACommand
        {
            //Active option
            public enum CommandActive
            {
                Active,
                Disabled,
                Invisible
            }

            //Logging option
            public enum CommandLogging
            {
                Log,
                Ignore,
                Mandatory,
                Unable
            }

            public enum CommandAccess
            {
                Any,
                AnyHidden,
                AnyVisible,
                GlobalVisible,
                TeamVisible,
                SquadVisible
            }

            public CommandActive command_active = CommandActive.Active;
            public CommandLogging command_logging = CommandLogging.Log;
            public CommandAccess command_access = CommandAccess.Any;
            public Int64 command_id = -1;
            public String command_key = null;
            public String command_name = null;
            public Boolean command_playerInteraction = true;
            public String command_text = null;

            public override string ToString()
            {
                return command_name ?? "Unknown Command";
            }
        }

        public class AException
        {
            public Exception InternalException = null;
            public String Message = String.Empty;
            public String Method = String.Empty;

            // This constructor MUST be executed inside the method where the error was triggered
            // otherwise the most recent item in the stack frame will not be correct
            public AException(String message, Exception internalException)
            {
                Method = new StackFrame(1).GetMethod().Name;
                Message = message;
                InternalException = internalException;
            }

            public AException(String message)
            {
                Method = new StackFrame(1).GetMethod().Name;
                Message = message;
            }

            //Override toString
            public override String ToString()
            {
                return "[" + Method + "][" + Message + "]" + ((InternalException != null) ? (": " + InternalException) : (""));
            }
        }

        public class AEventOption
        {
            public enum MapCode
            {
                UNKNOWN,
                RESET,
                MET,
                LOC
            };
            public static readonly Dictionary<MapCode, String> MapNames = new Dictionary<MapCode, String> {
                {MapCode.MET, "Operation Metro"},
                {MapCode.LOC, "Operation Locker"}
            };
            public enum ModeCode
            {
                UNKNOWN,
                RESET,
                T100,
                T200,
                T300,
                T400,
                R200,
                R300,
                R400,
                C500,
                C1000,
                C2000,
                F9,
                F6,
                F3,
                D500,
                HD500,
                D750,
                D1000
            };
            public static readonly Dictionary<ModeCode, String> ModeNames = new Dictionary<ModeCode, String> {
                {ModeCode.T100, "TDM 100"},
                {ModeCode.T200, "TDM 200"},
                {ModeCode.T300, "TDM 300"},
                {ModeCode.T400, "TDM 400"},
                {ModeCode.R200, "Rush 200"},
                {ModeCode.R300, "Rush 300"},
                {ModeCode.R400, "Rush 400"},
                {ModeCode.C500, "Conquest 500"},
                {ModeCode.C1000, "Conquest 1000"},
                {ModeCode.C2000, "Conquest 2000"},
                {ModeCode.F9, "CTF 9"},
                {ModeCode.F6, "CTF 6"},
                {ModeCode.F3, "CTF 3"},
                {ModeCode.D500, "Domination 500"},
                {ModeCode.HD500, "HC Domination 500"},
                {ModeCode.D750, "Domination 750"},
                {ModeCode.D1000, "Domination 1000"}
            };
            public enum RuleCode
            {
                UNKNOWN,
                ENDEVENT,
                AW,
                KO,
                NE,
                GO,
                AO,
                ARO,
                LMGO,
                PO,
                SO,
                EO,
                MLO,
                DO,
                BKO,
                BSO,
                RTO,
                HO,
                NH,
                CAI,
                TR
            };
            public static readonly Dictionary<RuleCode, String> RuleNames = new Dictionary<RuleCode, String> {
                {RuleCode.AW, "All Weapons"},
                {RuleCode.KO, "Knives Only"},
                {RuleCode.NE, "No Explosives"},
                {RuleCode.GO, "Grenades Only"},
                {RuleCode.AO, "Automatics Only"},
                {RuleCode.ARO, "Assault Rifles Only"},
                {RuleCode.LMGO, "LMGs Only"},
                {RuleCode.PO, "Pistols Only"},
                {RuleCode.SO, "Shotguns Only"},
                {RuleCode.EO, "Explosives Only"},
                {RuleCode.MLO, "Mares Leg Only"},
                {RuleCode.DO, "Defibs Only"},
                {RuleCode.BKO, "Bow And Knives Only"},
                {RuleCode.BSO, "Bolt Sniper Only"},
                {RuleCode.RTO, "Repair Tool Only"},
                {RuleCode.HO, "Headshots Only"},
                {RuleCode.NH, "No Headshots"},
                {RuleCode.CAI, "Cowboys and Indians"},
                {RuleCode.TR, "Troll Rules"},
                {RuleCode.ENDEVENT, "End The Event"}
            };

            public MapCode Map;
            public ModeCode Mode;
            public RuleCode Rule;

            public static AEventOption Default()
            {
                return new AEventOption()
                {
                    Map = MapNames.FirstOrDefault().Key,
                    Mode = ModeNames.FirstOrDefault().Key,
                    Rule = RuleNames.FirstOrDefault().Key
                };
            }

            public String getDisplay()
            {
                return MapNames[Map] + "/" + ModeNames[Mode] + "/" + RuleNames[Rule];
            }

            public String getCode()
            {
                return Map + "-" + Mode + "-" + Rule;
            }

            public static RuleCode RuleFromDisplay(String ruleDisplay)
            {
                if (!RuleNames.Any(rule => rule.Value == ruleDisplay))
                {
                    return RuleNames.FirstOrDefault().Key;
                }
                return RuleNames.FirstOrDefault(rule => rule.Value == ruleDisplay).Key;
            }

            public static ModeCode ModeFromDisplay(String modeDisplay)
            {
                if (!ModeNames.Any(mode => mode.Value == modeDisplay))
                {
                    return ModeNames.FirstOrDefault().Key;
                }
                return ModeNames.FirstOrDefault(mode => mode.Value == modeDisplay).Key;
            }

            public static MapCode MapFromDisplay(String mapDisplay)
            {
                if (!MapNames.Any(map => map.Value == mapDisplay))
                {
                    return MapNames.FirstOrDefault().Key;
                }
                return MapNames.FirstOrDefault(map => map.Value == mapDisplay).Key;
            }

            public static AEventOption FromDisplay(String display)
            {
                if (!display.Contains('/'))
                {
                    return Default();
                }
                var split = display.Split('/');
                if (split.Length != 3 ||
                    !MapNames.Any(map => map.Value == split[0]) ||
                    !ModeNames.Any(mode => mode.Value == split[1]) ||
                    !RuleNames.Any(rule => rule.Value == split[2]))
                {
                    return Default();
                }
                return new AEventOption()
                {
                    Map = MapNames.FirstOrDefault(map => map.Value == split[0]).Key,
                    Mode = ModeNames.FirstOrDefault(mode => mode.Value == split[1]).Key,
                    Rule = RuleNames.FirstOrDefault(rule => rule.Value == split[2]).Key
                };
            }

            public static AEventOption FromCode(String code)
            {
                if (!code.Contains('-'))
                {
                    return Default();
                }
                var split = code.Split('-');
                if (split.Length != 3)
                {
                    return Default();
                }
                var parsedMap = (MapCode)Enum.Parse(typeof(MapCode), split[0]);
                var parsedMode = (ModeCode)Enum.Parse(typeof(ModeCode), split[1]);
                var parsedRule = (RuleCode)Enum.Parse(typeof(RuleCode), split[2]);
                if (!MapNames.ContainsKey(parsedMap) ||
                    !ModeNames.ContainsKey(parsedMode) ||
                    !RuleNames.ContainsKey(parsedRule))
                {
                    return Default();
                }
                return new AEventOption()
                {
                    Map = parsedMap,
                    Mode = parsedMode,
                    Rule = parsedRule
                };
            }
        }

        public class APoll
        {
            private AdKats Plugin;

            public String ID;
            public String Title;
            public Dictionary<Int32, KeyValuePair<String, Boolean>> Options;

            public Boolean Completed;
            public Boolean Canceled;
            public DateTime StartTime;
            private Boolean _FirstPrint;
            public DateTime PrintTime;
            public Dictionary<APlayer, Int32> Votes;

            public APoll(AdKats plugin)
            {
                Plugin = plugin;
                StartTime = Plugin.UtcNow();
                PrintTime = Plugin.UtcNow().AddMinutes(-10);
                Options = new Dictionary<Int32, KeyValuePair<String, Boolean>>();
                Votes = new Dictionary<APlayer, Int32>();
            }

            public void AddOption(String option, Boolean singular)
            {
                Int32 optionNumber = 1;
                while (Options.ContainsKey(optionNumber))
                {
                    optionNumber++;
                }
                Options[optionNumber] = new KeyValuePair<String, Boolean>(option, singular);
            }

            public Boolean AddVote(APlayer voter, Int32 vote)
            {
                if (!Options.ContainsKey(vote))
                {
                    voter.Say("Vote " + vote + " is not valid. Options are " + Options.Keys.Min() + "-" + Options.Keys.Max() + ".");
                    return false;
                }
                Int32 currentVote = -1;
                if (Votes.ContainsKey(voter))
                {
                    currentVote = Votes[voter];
                }
                if (currentVote == vote)
                {
                    voter.Say("You already voted for '" + Options[vote].Key + "'");
                    return false;
                }
                if (currentVote > 0)
                {
                    voter.Say("You changed your vote from '" + Options[currentVote].Key + "' to '" + Options[vote].Key + "'");
                }
                else
                {
                    voter.Say("Vote added for '" + Options[vote].Key + "'.");
                }
                Votes[voter] = vote;
                return true;
            }

            public void PrintPoll(Boolean printWinning)
            {
                List<String> optionStrings = new List<String>();
                foreach (var option in Options)
                {
                    optionStrings.Add("!" + option.Key + " " + option.Value.Key + " [" + Votes.Count(vote => vote.Value == option.Key) + "]");
                }
                List<String> optionLines = new List<String>();
                String currentLine = String.Empty;
                foreach (var option in optionStrings)
                {
                    if (currentLine == String.Empty)
                    {
                        currentLine = option;
                    }
                    else
                    {
                        currentLine += " | " + option;
                        optionLines.Add(currentLine);
                        currentLine = String.Empty;
                    }
                }
                if (currentLine != String.Empty)
                {
                    optionLines.Add(currentLine + " |");
                }
                PrintTime = Plugin.UtcNow();
                if (!_FirstPrint)
                {
                    Plugin.AdminTellMessage(Title);
                    _FirstPrint = true;
                }
                else
                {
                    Plugin.AdminSayMessage(Title);
                    if (printWinning)
                    {
                        GetWinningOption("leading", true);
                    }
                }
                foreach (var line in optionLines)
                {
                    Plugin.AdminSayMessage(line);
                }
            }

            public String GetWinningOption(String printType, Boolean yell)
            {
                if (!Votes.Any())
                {
                    // If nobody has voted yet, use the first option
                    return Options.Values.FirstOrDefault().Key;
                }
                String winnerString = null;
                List<Int32> exclude = new List<Int32>();
                do
                {
                    var votes = Votes.Values.ToList();
                    var results = votes.Where(vote => !exclude.Contains(vote))
                                       .GroupBy(vote => vote)
                                       .Select(group => new
                                       {
                                           Option = group.Key,
                                           Count = group.Count()
                                       })
                                       .OrderByDescending(group => group.Count);
                    var winner = results.First();
                    var winnerOption = Options[winner.Option];
                    winnerString = winnerOption.Key;
                    if (winnerOption.Value)
                    {
                        // This result can only win if it's greater than all other options combined
                        var sumOtherVotes = results.Where(result => result.Option != winner.Option).Sum(result => result.Count);
                        if (winner.Count < sumOtherVotes)
                        {
                            winnerString = null;
                        }
                    }
                    if (printType != null && winnerString != null)
                    {
                        if (printType == "won")
                        {
                            String wonMessage = "'" + winnerString + "' won with " + winner.Count + " votes!";
                            if (yell)
                            {
                                Plugin.AdminYellMessage(wonMessage, true, 0);
                            }
                            else
                            {
                                Plugin.AdminSayMessage(wonMessage);
                            }
                        }
                        else if (printType == "leading")
                        {
                            String leadingMessage = "'" + winnerString + "' is winning! (" + winner.Count + " votes)";
                            if (yell)
                            {
                                Plugin.AdminYellMessage(leadingMessage, true, 2);
                            }
                            else
                            {
                                Plugin.AdminSayMessage(leadingMessage);
                            }
                        }
                    }
                } while (winnerString == null);

                return winnerString;
            }

            public void Reset()
            {
                Options.Clear();
                Votes.Clear();
                Completed = false;
                Canceled = false;
                StartTime = Plugin.UtcNow();
                _FirstPrint = false;
                PrintTime = Plugin.UtcNow().AddMinutes(-10);
            }
        }

        public class AKill
        {
            public AdKats _plugin;

            public String weaponCode;
            public DamageTypes weaponDamage = DamageTypes.None;
            public APlayer killer;
            public APlayer victim;
            public CPlayerInfo killerCPI;
            public CPlayerInfo victimCPI;
            public Boolean IsSuicide;
            public Boolean IsHeadshot;
            public Boolean IsTeamkill;
            public DateTime TimeStamp;
            public DateTime UTCTimeStamp;
            public Int64 RoundID;

            public AKill(AdKats plugin)
            {
                _plugin = plugin;
            }

            public override string ToString()
            {
                // Default values in case any are null;
                String killerString = killer != null ? killer.GetVerboseName() : "UnknownKiller";
                String methodString = "UnknownMethod";
                if (!String.IsNullOrEmpty(weaponCode))
                {
                    methodString = _plugin.WeaponDictionary.GetShortWeaponNameByCode(weaponCode);
                }
                String victimString = victim != null ? victim.GetVerboseName() : "UnknownVictim";
                return killerString + " [" + methodString + "] " + victimString;
            }
        }

        public class AMove
        {
            public APlayer Player;
            public ASquad Squad;
        }

        public class ASquad
        {
            public static readonly Dictionary<Int32, String> Names = new Dictionary<Int32, String>() {
                {0, "None"},
                {1, "Alpha"},
                {2, "Bravo"},
                {3, "Charlie"},
                {4, "Delta"},
                {5, "Echo"},
                {6, "Foxtrot"},
                {7, "Golf"},
                {8, "Hotel"},
                {9, "India"},
                {10, "Juliet"},
                {11, "Kilo"},
                {12, "Lima"},
                {13, "Mike"},
                {14, "November"},
                {15, "Oscar"},
                {16, "Papa"}
            };

            public AdKats Plugin;

            public Int32 TeamID;
            public Int32 SquadID;
            public List<APlayer> Players;

            public ASquad(AdKats plugin)
            {
                Plugin = plugin;
                Players = new List<APlayer>();
            }

            public String GetName()
            {
                if (!Names.ContainsKey(SquadID))
                {
                    Plugin.Log.Error("Invalid squad ID " + SquadID + ", unable to get squad name.");
                    return SquadID.ToString();
                }
                return Names[SquadID];
            }

            public Double GetPower()
            {
                return Players.Sum(aPlayer => aPlayer.GetPower(true));
            }

            public override String ToString()
            {
                return TeamID + ":" + GetName() + ":" + Math.Round(GetPower()) + " / " + String.Join(" | ", Players.OrderBy(member => member.player_name).Select(member => member.player_name).ToArray());
            }
        }

        public class APlayer
        {
            public CPunkbusterInfo PBPlayerInfo = null;
            public List<AKill> LiveKills = null;
            public List<ARecord> TargetedRecords = null;
            public String player_clanTag = null;
            public CPlayerInfo fbpInfo = null;
            public Int32 BanEnforceCount = 0;
            public Int32 backup_kills = 0;
            public Int32 backup_deaths = 0;
            public Int32 backup_score = 0;
            public Int64 game_id = -1;
            public DateTime lastAction = DateTime.UtcNow;
            public DateTime lastKill = DateTime.UtcNow;
            public DateTime lastDeath = DateTime.UtcNow;
            public DateTime lastSpawn = DateTime.UtcNow;
            public DateTime lastSwitchMessage = DateTime.UtcNow;
            public DateTime LastUsage = DateTime.UtcNow;
            public Boolean player_aa = false;
            public Boolean player_aa_fetched = false;
            public Boolean player_aa_told = false;
            public String player_guid = null;
            public Int64 player_id = -1;
            public String player_ip
            {
                get; private set;
            }
            public String player_discord_id;
            public DateTime VoipJoinTime = DateTime.UtcNow - TimeSpan.FromMinutes(15);
            public DiscordManager.DiscordMember DiscordObject;
            public TeamSpeakClientViewer.TeamspeakClient TSClientObject;
            public String player_name = null;
            public String player_name_previous = null;
            public String player_battlecry = null;
            public Boolean player_online = true;
            public String player_pbguid = null;
            public ARole player_role = null;
            public String player_slot = null;
            public Double player_reputation = 0;
            public DateTime player_firstseen = DateTime.UtcNow;
            public DateTime JoinTime = DateTime.UtcNow;
            public AServer player_server = null;
            public TimeSpan player_serverplaytime = TimeSpan.FromSeconds(0);
            public Boolean player_spawnedOnce = false;
            public Boolean player_chatOnce = false;
            public Boolean player_spawnedRound = false;
            public PlayerType player_type = PlayerType.Player;
            public Boolean BLInfoStored = false;
            public String player_battlelog_personaID = null;
            public String player_battlelog_userID = null;
            private Boolean player_locked;
            private DateTime player_locked_start = DateTime.UtcNow;
            private TimeSpan player_locked_duration = TimeSpan.Zero;
            private String player_locked_source;
            public Int32 player_infractionPoints = Int32.MinValue;
            public ARecord LastPunishment = null;
            public ARecord LastForgive = null;
            public ATeam RequiredTeam = null;
            public Int32 RequiredSquad = -1;
            public readonly Queue<KeyValuePair<Double, DateTime>> player_pings;
            public Boolean player_pings_full
            {
                get; private set;
            }
            public Double player_ping_avg
            {
                get; private set;
            }
            public Double player_ping
            {
                get; private set;
            }
            public DateTime player_ping_time
            {
                get; private set;
            }
            public Boolean player_ping_added
            {
                get; private set;
            }
            public Boolean player_ping_manual = false;
            public APlayer conversationPartner = null;
            public Int32 AllCapsMessages = 0;
            public List<DateTime> TeamMoves;
            public Int32 ActiveSession = 1;

            public Dictionary<Int64, APlayerStats> RoundStats;
            public Int64 BL_SPM;
            public Double BL_KDR;
            public Double BL_KPM;
            public Int32 BL_Rank;
            public Double BL_Time;
            public Int32 BL_Kills;
            public Int32 BL_Deaths;
            public Int64 player_personaId;
            public ATopStats TopStats;
            public IPLocation location = null;
            public Double EZScaleAnomalyScore = 0;
            public Boolean EZScaleIsFlagged = false;
            public Double EZScaleRiskScore = 0;
            public Boolean update_playerUpdated = true;
            public Boolean player_new = false;

            public AChallengeManager.CEntry ActiveChallenge = null;

            public Boolean loadout_valid = true;
            public Boolean loadout_spawnValid = true;
            public String loadout_items = "Loadout not fetched yet.";
            public String loadout_items_long = "Loadout not fetched yet.";
            public String loadout_deniedItems = "No denied items.";

            public String PrintThreadID = String.Empty;

            private readonly AdKats Plugin;

            public APlayer(AdKats plugin)
            {
                Plugin = plugin;
                RoundStats = new Dictionary<Int64, APlayerStats>();
                TopStats = new ATopStats();
                LiveKills = new List<AKill>();
                player_pings = new Queue<KeyValuePair<Double, DateTime>>();
                TargetedRecords = new List<ARecord>();
                LastUsage = DateTime.UtcNow;
                TeamMoves = new List<DateTime>();
                PrintThreadID = "";
                ActiveSession = 1;
            }

            public void Say(String message)
            {
                Plugin.PlayerSayMessage(player_name, message);
            }
            public void Say(String message, Boolean displayProconChat, Int32 spamCount)
            {
                Plugin.PlayerSayMessage(player_name, message, displayProconChat, spamCount);
            }

            public void Yell(String message)
            {
                Plugin.PlayerYellMessage(player_name, message);
            }
            public void Yell(String message, Boolean displayProconChat, Int32 spamCount)
            {
                Plugin.PlayerYellMessage(player_name, message, displayProconChat, spamCount);
            }

            public void Tell(String message)
            {
                Plugin.PlayerTellMessage(player_name, message);
            }
            public void Tell(String message, Boolean displayProconChat, Int32 spamCount)
            {
                Plugin.PlayerTellMessage(player_name, message, displayProconChat, spamCount);
            }

            public String GetTeamKey()
            {
                String key = "Unknown";
                if (fbpInfo != null)
                {
                    try
                    {
                        key = Plugin._teamDictionary[fbpInfo.TeamID].TeamKey;
                    }
                    catch (Exception)
                    {
                        key = fbpInfo.TeamID.ToString();
                    }
                }
                return key;
            }

            private Double maxScore = 30000.0;
            private Double maxKills = 200.0;
            private Double maxKd = 4.0;
            public Double GetPower(Boolean includeMods)
            {
                return GetPower(true, includeMods, includeMods);
            }
            public Double GetPower(Boolean includeBase, Boolean includeActive, Boolean includeSaved)
            {
                // Base power is 7-32
                Double basePower = min7(TopStats.RoundCount >= 3 && TopStats.TopCount > 0 ? Math.Pow(TopStats.TopRoundRatio + 1, 5) : 1.0);
                // Cap top power at 30 if the player is new
                if (TopStats.RoundCount < 20)
                {
                    basePower = Math.Min(basePower, 30.0);
                }
                // If their base power is calculated low, use their battlelog stats instead
                var blPower = (BL_KDR * BL_SPM * BL_KPM) / 2500.0 * 20;
                if (basePower < 15 && blPower > 0)
                {
                    // Don't allow the calculation to be less than their base power, or greater than 20
                    basePower = Math.Min(Math.Max(blPower, basePower), 20);
                }
                Double savedPower = TopStats.TempTopPower;
                if (fbpInfo == null)
                {
                    if (!includeSaved)
                    {
                        return basePower;
                    }
                    return Math.Max(basePower, savedPower / 3.0);
                }
                // Active power is 7-ActiveInfluence
                Double killPower = min7(Math.Min(fbpInfo.Kills, maxKills) / maxKills * Plugin._TeamPowerActiveInfluence);
                Double kdPower = min7(Math.Min(fbpInfo.Kills / Math.Max(fbpInfo.Deaths, 1.0), maxKd) / maxKd * Plugin._TeamPowerActiveInfluence);
                Double scorePower = min7(Math.Min(fbpInfo.Score, maxScore) / maxScore * Plugin._TeamPowerActiveInfluence);
                Double activePower = (killPower + kdPower + scorePower) / 3.0;
                // Take whichever power level is greatest
                TopStats.TempTopPower = Math.Max(Math.Max(basePower, savedPower), activePower);
                var returnPower = min7(0);
                if (includeBase)
                {
                    returnPower = Math.Max(returnPower, basePower);
                }
                if (includeActive)
                {
                    returnPower = Math.Max(returnPower, activePower);
                }
                if (includeSaved)
                {
                    returnPower = Math.Max(returnPower, savedPower / 3.0);
                }
                return returnPower;
            }

            private Double min7(Double val)
            {
                return Math.Max(val, 7.0);
            }

            public void SetIP(String ip)
            {
                this.player_ip = (String.IsNullOrEmpty(ip) ? (null) : (ip));
            }

            public String GetVerboseName()
            {
                return ((String.IsNullOrEmpty(player_clanTag)) ? ("") : ("[" + player_clanTag + "]")) + player_name;
            }

            public TimeSpan GetIdleTime()
            {
                return Plugin.UtcNow() - lastAction;
            }

            public void ClearPingEntries()
            {
                player_pings.Clear();
                player_pings_full = false;
                player_ping = 0;
                player_ping_avg = 0;
            }

            public void AddPingEntry(Double newPingValue)
            {
                //Get rounded time (floor)
                DateTime newPingTime = Plugin.UtcNow();
                newPingTime = newPingTime.AddTicks(-(newPingTime.Ticks % TimeSpan.TicksPerSecond));
                if (!player_ping_added)
                {
                    player_ping_avg = newPingValue;
                    player_ping = newPingValue;
                    player_ping_time = newPingTime;
                    player_pings.Enqueue(new KeyValuePair<double, DateTime>(newPingValue, newPingTime));
                    player_ping_added = true;
                    return;
                }

                //Linear Interpolation
                DateTime oldPingTime = player_ping_time;
                Double oldPingValue = player_ping;
                Double interTimeOldSeconds = 0;
                Double interTimeNewSeconds = (newPingTime - oldPingTime).TotalSeconds;
                Double m = (newPingValue - oldPingValue) / (interTimeNewSeconds);
                Double b = oldPingValue;
                for (Int32 sec = (Int32)interTimeOldSeconds; sec < interTimeNewSeconds; sec++)
                {
                    DateTime subPingTime = oldPingTime.AddSeconds(sec);
                    Double subPingValue = (m * sec) + b;
                    player_pings.Enqueue(new KeyValuePair<double, DateTime>(subPingValue, subPingTime));
                }

                //Remove old values
                Boolean removed = false;
                do
                {
                    removed = false;
                    if (player_pings.Any() && (Plugin.UtcNow() - player_pings.Peek().Value).TotalSeconds > Plugin._pingMovingAverageDurationSeconds)
                    {
                        player_pings.Dequeue();
                        player_pings_full = true;
                        removed = true;
                    }
                } while (removed);

                //Set instance vars
                player_ping = newPingValue;
                player_ping_time = newPingTime;
                player_ping_avg = player_pings.Sum(pingEntry => pingEntry.Key) / player_pings.Count;
            }

            public Boolean IsLocked()
            {
                if (player_locked && player_locked_start + player_locked_duration < Plugin.UtcNow())
                {
                    //Unlock the player
                    player_locked = false;
                }
                return player_locked;
            }

            public Boolean Lock(String locker, TimeSpan duration)
            {
                if (IsLocked())
                {
                    return false;
                }
                if (String.IsNullOrEmpty(locker))
                {
                    Plugin.Log.Error("Attempted to lock player with empty locker.");
                    return false;
                }
                if (duration == null || duration == TimeSpan.Zero)
                {
                    Plugin.Log.Error("Attempted to lock player with invalid duration.");
                    return false;
                }
                player_locked = true;
                player_locked_duration = duration;
                player_locked_start = Plugin.UtcNow();
                player_locked_source = locker;
                return true;
            }

            public void Unlock()
            {
                player_locked = false;
            }

            public TimeSpan GetLockRemaining()
            {
                if (IsLocked())
                {
                    return (player_locked_start + player_locked_duration) - Plugin.UtcNow();
                }
                return TimeSpan.Zero;
            }

            public String GetLockSource()
            {
                if (IsLocked())
                {
                    return player_locked_source;
                }
                return null;
            }
        }

        public class AClient
        {
            public String ClientName;
            public String ClientMethod;
            public String SubscriptionGroup;
            public Boolean SubscriptionEnabled;
            public DateTime SubscriptionTime
            {
                get; private set;
            }

            private readonly AdKats Plugin;

            public AClient(AdKats plugin)
            {
                Plugin = plugin;
            }

            public void EnableSubscription()
            {
                SubscriptionEnabled = true;
                SubscriptionTime = Plugin.UtcNow();
            }

            public void DisableSubscription()
            {
                SubscriptionEnabled = false;
                SubscriptionTime = Plugin.UtcNow();
            }
        }

        public class AServer
        {
            public Int64 ServerID;
            public Int64 ServerGroup;
            public String ServerIP;
            public String ServerName;
            public String ServerType = "UNKNOWN";
            public Int64 GameID = -1;
            public Boolean CommanderEnabled;
            public Boolean FairFightEnabled;
            public Boolean ForceReloadWholeMags;
            public Boolean HitIndicatorEnabled;
            public String GamePatchVersion = "UNKNOWN";
            public Int32 MaxSpectators = -1;
            public CServerInfo InfoObject
            {
                get; private set;
            }
            private DateTime infoObjectTime = DateTime.UtcNow;
            private List<MaplistEntry> MapList;
            private Int32 MapIndex;
            private Int32 NextMapIndex;

            private readonly AdKats Plugin;

            public AServer(AdKats plugin)
            {
                Plugin = plugin;
            }

            public void SetInfoObject(CServerInfo infoObject)
            {
                InfoObject = infoObject;
                ServerName = infoObject.ServerName;
                infoObjectTime = Plugin.UtcNow();
            }

            public TimeSpan GetRoundElapsedTime()
            {
                if (InfoObject == null || Plugin._roundState != RoundState.Playing)
                {
                    return TimeSpan.Zero;
                }
                return TimeSpan.FromSeconds(InfoObject.RoundTime) + (Plugin.UtcNow() - infoObjectTime);
            }

            public void SetMapList(List<MaplistEntry> MapList)
            {
                if (this.MapList == null)
                {
                    this.MapList = MapList;
                }
                else
                {
                    lock (this.MapList)
                    {
                        this.MapList = MapList;
                    }
                }
            }

            public List<MaplistEntry> GetMapList()
            {
                List<MaplistEntry> ret;
                if (this.MapList == null)
                {
                    this.MapList = new List<MaplistEntry>();
                }
                lock (this.MapList)
                {
                    ret = MapList;
                }
                return ret;
            }

            public void SetMapListIndicies(Int32 current, Int32 next)
            {
                this.MapIndex = current;
                this.NextMapIndex = next;
            }

            public MaplistEntry GetNextMap()
            {
                if (MapList == null)
                {
                    return null;
                }
                return MapList.FirstOrDefault(entry => entry.Index == this.NextMapIndex);
            }

            public MaplistEntry GetMap()
            {
                if (MapList == null)
                {
                    return null;
                }
                return MapList.FirstOrDefault(entry => entry.Index == this.MapIndex);
            }
        }

        public class ATopStats
        {
            public Boolean Fetched;
            public Int32 RoundCount;
            public Int32 TopCount;
            public Double TopRoundRatio;
            public Double TempTopPower;
        }

        public class APlayerStats
        {
            public Int64 RoundID;
            public Int64 Rank;
            public Int64 Skill;
            public Int64 Kills;
            public Int64 Headshots;
            public Int64 Deaths;
            public Int64 Shots;
            public Int64 Hits;
            public Int64 Score;
            public Double Accuracy;
            public Int64 Revives;
            public Int64 Heals;
            public Dictionary<String, AWeaponStat> WeaponStats = null;
            public Dictionary<String, AVehicleStat> VehicleStats = null;
            public CPlayerInfo LiveStats = null;

            public APlayerStats(Int64 roundID)
            {
                RoundID = roundID;
            }
        }

        public class ARecord
        {
            //Source of this record
            public enum Sources
            {
                Default,
                Automated,
                ExternalPlugin,
                InGame,
                Settings,
                ServerCommand,
                Database,
                HTTP
            }

            //Access method of this record
            public enum AccessMethod
            {
                HiddenInternal,
                HiddenExternal,
                HiddenGlobal,
                HiddenTeam,
                HiddenSquad,
                PublicExternal,
                PublicGlobal,
                PublicTeam,
                PublicSquad
            }

            //Command attributes for the record
            public ACommand command_action = null;
            public Int32 command_numeric = 0;
            public ACommand command_type = null;

            //All messages sent through this record via sendMessageToSource or other means
            public List<String> debugMessages;

            //Settings for External Plugin commands
            public String external_callerIdentity = null;
            public String external_responseClass = null;
            public String external_responseMethod = null;
            public Boolean external_responseRequested;

            //Internal processing
            public Boolean isAliveChecked;
            public Boolean isLoadoutChecked;
            public Boolean targetLoadoutActed = false;
            public Boolean isContested;
            public Boolean isDebug;
            public Boolean isIRO;
            public Boolean record_action_executed;
            public AException record_exception = null;

            //record data
            public Int64 record_id = -1;
            public String record_message = null;
            public Sources record_source = Sources.Default;
            public DateTime record_time = DateTime.UtcNow;
            public DateTime record_time_update = DateTime.UtcNow;
            public Int64 server_id = -1;
            public String source_name = null;
            public APlayer source_player = null;
            public Int32 SourceSession = 0;
            public String target_name = null;
            public APlayer target_player = null;
            public Int32 TargetSession = 0;
            public DateTime record_creationTime;
            public AccessMethod record_access = AccessMethod.HiddenInternal;
            public Boolean record_held;
            public Boolean record_orchestrate;
            public List<String> TargetNamesLocal;
            public List<APlayer> TargetPlayersLocal;
            public List<ARecord> TargetInnerRecords;

            //Default Constructor
            public ARecord()
            {
                debugMessages = new List<String>();
                TargetNamesLocal = new List<String>();
                TargetPlayersLocal = new List<APlayer>();
                TargetInnerRecords = new List<ARecord>();
                record_creationTime = DateTime.UtcNow;
            }

            public String GetSourceName()
            {
                String source = "";
                if (source_player != null)
                {
                    source = ((source_player.player_online) ? ("") : ("(OFFLINE)")) + source_player.GetVerboseName();
                }
                else if (String.IsNullOrEmpty(source_name))
                {
                    source = "NOSOURCE";
                }
                else
                {
                    source = source_name;
                }
                return source;
            }

            public String GetTargetNames()
            {
                String targets = "";
                if (TargetPlayersLocal.Any())
                {
                    foreach (APlayer aPlayer in TargetPlayersLocal)
                    {
                        targets += ((aPlayer.player_online) ? ("") : ("(OFFLINE)")) + aPlayer.GetVerboseName() + ", ";
                    }
                }
                else if (target_player != null)
                {
                    targets = ((target_player.player_online) ? ("") : ("(OFFLINE)")) + target_player.GetVerboseName();
                }
                else
                {
                    targets = ((String.IsNullOrEmpty(target_name)) ? ("NoNameTarget") : (target_name));
                }
                return targets.Trim().TrimEnd(',');
            }
        }

        public class AStatistic
        {
            public enum StatisticType
            {
                map_benefit,
                map_detriment,
                player_win,
                player_loss,
                player_top,
                round_quality,
                battlelog_requestfreq,
                ping_over50,
                ping_over100,
                ping_over150
            }

            public Int64 stat_id;
            public Int64 server_id;
            public Int64 round_id;
            public StatisticType stat_type;
            public String target_name;
            public APlayer target_player;
            public Double stat_value;
            public String stat_comment;
            public DateTime stat_time;
        }

        public class AChatMessage
        {
            public enum ChatSubset
            {
                Global,
                Team,
                Squad
            }

            public String Speaker;
            public String Message;
            public String OriginalMessage;
            public ChatSubset Subset;
            public Boolean Hidden;
            public Int32 SubsetTeamID;
            public Int32 SubsetSquadID;
            public DateTime Timestamp;
        }

        public class ARole
        {
            public Dictionary<String, KeyValuePair<Func<AdKats, APlayer, Boolean>, ACommand>> ConditionalAllowedCommands = null;
            public Dictionary<String, ACommand> RoleAllowedCommands = null;
            public Dictionary<String, ASpecialGroup> RoleSetGroups = null;
            public CPrivileges RoleProconPrivileges = null;
            public Int64 role_id = -1;
            public String role_key = null;
            public String role_name = null;
            public Int64 role_powerLevel = 0;

            public ARole()
            {
                RoleAllowedCommands = new Dictionary<String, ACommand>();
                RoleSetGroups = new Dictionary<String, ASpecialGroup>();
                ConditionalAllowedCommands = new Dictionary<String, KeyValuePair<Func<AdKats, APlayer, Boolean>, ACommand>>();
            }
        }

        public class ASpecialGroup
        {
            public Int64 group_id;
            public String group_key;
            public String group_name;
        }

        public class ASpecialPlayer
        {
            private readonly AdKats Plugin;

            public Int64 specialplayer_id;
            public Int32? player_game = null;
            public ASpecialGroup player_group = null;
            public String player_identifier = null;
            public APlayer player_object = null;
            public Int32? player_server = null;
            public DateTime player_effective;
            public DateTime player_expiration;
            public String tempCreationType;

            public ASpecialPlayer(AdKats plugin)
            {
                Plugin = plugin;
            }

            public Boolean IsMatchingPlayer(APlayer aPlayer)
            {
                try
                {
                    if (player_group != null &&
                        aPlayer != null &&
                        player_object != null &&
                        (player_object.player_id == aPlayer.player_id ||
                         player_identifier == aPlayer.player_name ||
                         player_identifier == aPlayer.player_guid ||
                         player_identifier == aPlayer.player_ip))
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error checking APlayer match with ASPlayer.", e));
                }
                return false;
            }

            public Boolean IsMatchingPlayerOfGroup(APlayer aPlayer, String specialPlayerGroup)
            {
                try
                {
                    if (player_group != null &&
                        player_group.group_key == specialPlayerGroup &&
                        aPlayer != null &&
                        player_object != null &&
                        (player_object.player_id == aPlayer.player_id ||
                         player_identifier == aPlayer.player_name ||
                         player_identifier == aPlayer.player_guid ||
                         player_identifier == aPlayer.player_ip))
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error checking APlayer/Group match with ASPlayer.", e));
                }
                return false;
            }
        }

        public class ATeam
        {
            private readonly AdKats Plugin;

            private readonly Queue<KeyValuePair<Double, DateTime>> TeamTicketCounts;
            public Boolean TeamTicketCountsFull
            {
                get; private set;
            }
            private Double TeamTicketDifferenceRate;
            public Int32 TeamTicketCount
            {
                get; private set;
            }
            public DateTime TeamTicketsTime
            {
                get; private set;
            }
            public Boolean TeamTicketsAdded
            {
                get; private set;
            }

            //Ticket Adjustments
            private Int32 TeamTicketAdjustment;
            private readonly Queue<KeyValuePair<Double, DateTime>> TeamAdjustedTicketCounts;
            public Int32 TeamAdjustedTicketCount
            {
                get; private set;
            }
            public DateTime TeamAdjustedTicketsTime
            {
                get; private set;
            }
            public Boolean TeamAdjustedTicketsAdded
            {
                get; private set;
            }
            private Double TeamAdjustedTicketDifferenceRate;
            private readonly Queue<KeyValuePair<Double, DateTime>> TeamAdjustedTicketDifferenceRates;
            public Double TeamAdjustedTicketAccellerationRate
            {
                get; private set;
            }

            //Score
            private readonly Queue<KeyValuePair<Double, DateTime>> TeamTotalScores;
            public Boolean TeamTotalScoresFull
            {
                get; private set;
            }
            public Double TeamScoreDifferenceRate
            {
                get; private set;
            }
            public Double TeamTotalScore
            {
                get; private set;
            }
            public DateTime TeamTotalScoresTime
            {
                get; private set;
            }
            public Boolean TeamTotalScoresAdded
            {
                get; private set;
            }

            public ATeam(AdKats plugin, Int32 teamID, String teamKey, String teamName, String teamDesc)
            {
                Plugin = plugin;
                TeamID = teamID;
                TeamKey = teamKey;
                TeamName = teamName;
                TeamDesc = teamDesc;
                TeamTotalScores = new Queue<KeyValuePair<Double, DateTime>>();
                TeamTotalScoresFull = false;
                TeamTicketCounts = new Queue<KeyValuePair<Double, DateTime>>();
                TeamTicketCountsFull = false;
                TeamAdjustedTicketCounts = new Queue<KeyValuePair<Double, DateTime>>();
                TeamAdjustedTicketDifferenceRates = new Queue<KeyValuePair<Double, DateTime>>();
            }

            public Int32 TeamID
            {
                get; private set;
            }
            public String TeamKey
            {
                get; private set;
            }
            public String TeamName
            {
                get; private set;
            }
            public String TeamDesc
            {
                get; private set;
            }

            public String GetTeamIDKey()
            {
                return TeamID + "/" + TeamKey;
            }

            //Live Vars
            public Boolean Populated
            {
                get; private set;
            }
            public Int32 TeamPlayerCount
            {
                get; set;
            }

            public Double GetTicketDifferenceRate()
            {
                return TeamTicketDifferenceRate >= 0 ? TeamTicketDifferenceRate : TeamAdjustedTicketDifferenceRate;
            }

            public Double GetRawTicketDifferenceRate()
            {
                return TeamTicketDifferenceRate;
            }

            public void UpdatePlayerCount(Int32 playerCount)
            {
                Populated = true;
                TeamPlayerCount = playerCount;
            }

            public void IncrementTeamTicketAdjustment()
            {
                Interlocked.Increment(ref TeamTicketAdjustment);
            }

            public void UpdateTicketCount(Double newTicketCount)
            {
                try
                {
                    UpdateAdjustedTicketCount(newTicketCount);
                    //Get rounded time (floor)
                    DateTime newTicketTime = Plugin.UtcNow();
                    newTicketTime = newTicketTime.AddTicks(-(newTicketTime.Ticks % TimeSpan.TicksPerSecond));
                    if (!TeamTicketsAdded)
                    {
                        TeamTicketDifferenceRate = 0;
                        TeamTicketCount = (Int32)newTicketCount;
                        TeamTicketsTime = newTicketTime;
                        TeamTicketCounts.Enqueue(new KeyValuePair<double, DateTime>(newTicketCount, newTicketTime));
                        TeamTicketsAdded = true;
                        return;
                    }

                    //Interpolation
                    DateTime oldTicketTime = TeamTicketsTime;
                    Double oldTicketValue = TeamTicketCount;
                    Double interTimeOldSeconds = 0;
                    Double interTimeNewSeconds = (newTicketTime - oldTicketTime).TotalSeconds;
                    Double m = (newTicketCount - oldTicketValue) / (interTimeNewSeconds);
                    Double b = oldTicketValue;
                    for (Int32 sec = (Int32)interTimeOldSeconds; sec < interTimeNewSeconds; sec++)
                    {
                        DateTime subTicketTime = oldTicketTime.AddSeconds(sec);
                        Double subTicketValue = (m * sec) + b;
                        TeamTicketCounts.Enqueue(new KeyValuePair<double, DateTime>(subTicketValue, subTicketTime));
                    }

                    //Remove old values (more than 90 seconds ago)
                    Boolean removed = false;
                    do
                    {
                        removed = false;
                        if (TeamTicketCounts.Any() && (Plugin.UtcNow() - TeamTicketCounts.Peek().Value).TotalSeconds > 90)
                        {
                            TeamTicketCounts.Dequeue();
                            TeamTicketCountsFull = true;
                            removed = true;
                        }
                    } while (removed);

                    //Set instance vars
                    TeamTicketCount = (Int32)newTicketCount;
                    TeamTicketsTime = newTicketTime;

                    List<Double> values = TeamTicketCounts.Select(pair => pair.Key).ToList();
                    List<double> differences = new List<Double>();
                    for (int i = 0; i < values.Count - 1; i++)
                    {
                        differences.Add(values[i + 1] - values[i]);
                    }
                    differences.Sort();
                    //Convert to tickets/min
                    TeamTicketDifferenceRate = (differences.Sum() / differences.Count) * 60;
                    if (Double.IsNaN(TeamTicketDifferenceRate))
                    {
                        TeamTicketDifferenceRate = 0;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error while updating team ticket count.", e));
                }
            }

            private void UpdateAdjustedTicketCount(Double newRealTicketCount)
            {
                try
                {
                    //Calculate adjusted ticket count
                    Double newAdjustedTicketCount = newRealTicketCount + TeamTicketAdjustment;
                    //Get rounded time (floor)
                    DateTime newAdjustedTicketTime = Plugin.UtcNow();
                    newAdjustedTicketTime = newAdjustedTicketTime.AddTicks(-(newAdjustedTicketTime.Ticks % TimeSpan.TicksPerSecond));
                    if (!TeamAdjustedTicketsAdded)
                    {
                        TeamAdjustedTicketDifferenceRate = 0;
                        TeamAdjustedTicketAccellerationRate = 0;
                        TeamAdjustedTicketCount = (Int32)newAdjustedTicketCount;
                        TeamAdjustedTicketsTime = newAdjustedTicketTime;
                        TeamAdjustedTicketCounts.Enqueue(new KeyValuePair<double, DateTime>(newAdjustedTicketCount, newAdjustedTicketTime));
                        TeamAdjustedTicketsAdded = true;
                        return;
                    }

                    //Remove old values (more than 90 seconds ago)
                    Boolean removed = false;
                    do
                    {
                        removed = false;
                        if (TeamAdjustedTicketCounts.Any() && (Plugin.UtcNow() - TeamAdjustedTicketCounts.Peek().Value).TotalSeconds > 90)
                        {
                            TeamAdjustedTicketCounts.Dequeue();
                            removed = true;
                        }
                        if (TeamAdjustedTicketDifferenceRates.Any() && (Plugin.UtcNow() - TeamAdjustedTicketDifferenceRates.Peek().Value).TotalSeconds > 90)
                        {
                            TeamAdjustedTicketDifferenceRates.Dequeue();
                            removed = true;
                        }
                    } while (removed);

                    Double oldAdjustedDifferenceRate = TeamAdjustedTicketDifferenceRate;

                    //Interpolation
                    DateTime oldTicketTime = TeamAdjustedTicketsTime;
                    Double oldTicketValue = TeamAdjustedTicketCount;
                    Double interTimeOldSeconds = 0;
                    Double interTimeNewSeconds = (newAdjustedTicketTime - oldTicketTime).TotalSeconds;
                    Double m = (newAdjustedTicketCount - oldTicketValue) / (interTimeNewSeconds);
                    Double b = oldTicketValue;
                    for (Int32 sec = (Int32)interTimeOldSeconds; sec < interTimeNewSeconds; sec++)
                    {
                        //Calculate time this datapoint occured
                        DateTime subTicketTime = oldTicketTime.AddSeconds(sec);

                        //Caclulate and enqueue the new adjusted ticket count
                        Double subTicketValue = (m * sec) + b;
                        TeamAdjustedTicketCounts.Enqueue(new KeyValuePair<double, DateTime>(subTicketValue, subTicketTime));

                        //Calculate and enqueue the new adjusted ticket difference rate
                        List<Double> ticketValues = TeamAdjustedTicketCounts.Select(pair => pair.Key).ToList();
                        List<double> ticketDifferences = new List<Double>();
                        for (int i = 0; i < ticketValues.Count - 1; i++)
                        {
                            ticketDifferences.Add(ticketValues[i + 1] - ticketValues[i]);
                        }
                        ticketDifferences.Sort();
                        //Convert to tickets/min
                        TeamAdjustedTicketDifferenceRate = (ticketDifferences.Sum() / ticketDifferences.Count) * 60;
                        if (Double.IsNaN(TeamAdjustedTicketDifferenceRate) || TeamAdjustedTicketDifferenceRate > 0)
                        {
                            TeamAdjustedTicketDifferenceRate = 0;
                        }
                        TeamAdjustedTicketDifferenceRates.Enqueue(new KeyValuePair<double, DateTime>(TeamAdjustedTicketDifferenceRate, subTicketTime));

                        //Calculate new ticket acceleration
                        List<Double> accelerationValues = TeamAdjustedTicketDifferenceRates.Select(pair => pair.Key).ToList();
                        List<double> accelerationDifferences = new List<Double>();
                        for (int i = 0; i < accelerationValues.Count - 1; i++)
                        {
                            accelerationDifferences.Add(accelerationValues[i + 1] - accelerationValues[i]);
                        }
                        accelerationDifferences.Sort();
                        //Convert to tickets/min/min
                        TeamAdjustedTicketAccellerationRate = (accelerationDifferences.Sum() / accelerationDifferences.Count) * 60;
                        if (Double.IsNaN(TeamAdjustedTicketAccellerationRate))
                        {
                            TeamAdjustedTicketAccellerationRate = 0;
                        }
                    }

                    //Set instance vars
                    TeamAdjustedTicketCount = (Int32)newAdjustedTicketCount;
                    TeamAdjustedTicketsTime = newAdjustedTicketTime;
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error while updating team adjusted ticket count.", e));
                }
            }

            public void UpdateTotalScore(Double newTotalScore)
            {
                try
                {
                    //Get rounded time (floor)
                    DateTime newScoreTime = Plugin.UtcNow();
                    newScoreTime = newScoreTime.AddTicks(-(newScoreTime.Ticks % TimeSpan.TicksPerSecond));
                    if (!TeamTotalScoresAdded)
                    {
                        TeamScoreDifferenceRate = 0;
                        TeamTotalScore = newTotalScore;
                        TeamTotalScoresTime = newScoreTime;
                        TeamTotalScores.Enqueue(new KeyValuePair<double, DateTime>(newTotalScore, newScoreTime));
                        TeamTotalScoresAdded = true;
                        return;
                    }

                    //Interpolation
                    DateTime oldScoreTime = TeamTotalScoresTime;
                    Double oldScoreValue = TeamTotalScore;
                    Double interTimeOldSeconds = 0;
                    Double interTimeNewSeconds = (newScoreTime - oldScoreTime).TotalSeconds;
                    Double m = (newTotalScore - oldScoreValue) / (interTimeNewSeconds);
                    Double b = oldScoreValue;
                    for (Int32 sec = (Int32)interTimeOldSeconds; sec < interTimeNewSeconds; sec++)
                    {
                        DateTime subScoreTime = oldScoreTime.AddSeconds(sec);
                        Double subScoreValue = (m * sec) + b;
                        TeamTotalScores.Enqueue(new KeyValuePair<double, DateTime>(subScoreValue, subScoreTime));
                    }

                    //Remove old values (more than 60 seconds ago)
                    Boolean removed = false;
                    do
                    {
                        removed = false;
                        if (TeamTotalScores.Any() && (Plugin.UtcNow() - TeamTotalScores.Peek().Value).TotalSeconds > 60)
                        {
                            TeamTotalScores.Dequeue();
                            TeamTotalScoresFull = true;
                            removed = true;
                        }
                    } while (removed);

                    //Set instance vars
                    TeamTotalScore = newTotalScore;
                    TeamTotalScoresTime = newScoreTime;

                    List<Double> values = TeamTotalScores.Select(pair => pair.Key).ToList();
                    List<double> differences = new List<Double>();
                    for (int i = 0; i < values.Count - 1; i++)
                    {
                        differences.Add(values[i + 1] - values[i]);
                    }
                    differences.Sort();
                    //Convert to tickets/min
                    TeamScoreDifferenceRate = (differences.Sum() / differences.Count) * 60;
                    if (Double.IsNaN(TeamScoreDifferenceRate))
                    {
                        TeamScoreDifferenceRate = 0;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error while updating team ticket total score.", e));
                }
            }

            public Double GetTeamPower()
            {
                return GetTeamPower(null, null, true);
            }

            public Double GetTeamPower(Boolean useModifiers)
            {
                return GetTeamPower(null, null, useModifiers);
            }

            public Double GetTeamPower(APlayer ignorePlayer, APlayer includePlayer)
            {
                return GetTeamPower(ignorePlayer, includePlayer, true);
            }

            public Double GetTeamPower(APlayer ignorePlayer, APlayer includePlayer, Boolean useModifiers)
            {
                try
                {
                    if (!Plugin._PlayerDictionary.Any())
                    {
                        return 0;
                    }
                    List<APlayer> teamPlayers = Plugin._PlayerDictionary.Values.ToList()
                        .Where(aPlayer =>
                            // Player is a live soldier in game, not a spectator/commander/etc.
                            aPlayer.player_type == PlayerType.Player
                            &&
                            // Player is not a server seeder
                            Plugin.NowDuration(aPlayer.lastAction).TotalMinutes < 20
                            &&
                            // Player is not the one we decided to ignore, if any
                            (ignorePlayer == null || aPlayer.player_id != ignorePlayer.player_id)
                            &&
                            (
                                // Player is required to be on this team
                                (aPlayer.RequiredTeam != null && aPlayer.RequiredTeam.TeamID == TeamID)
                                ||
                                // Player is actually on this team, and not required to be anywhere specifc
                                (aPlayer.RequiredTeam == null && aPlayer.fbpInfo.TeamID == TeamID)
                                ||
                                // Player is queued to move to this team
                                ((TeamID == 1 ? Plugin._Team2MoveQueue : Plugin._Team1MoveQueue).Any(pObject => pObject.GUID == aPlayer.player_guid))
                            )).ToList();
                    if (includePlayer != null && !teamPlayers.Contains(includePlayer))
                    {
                        teamPlayers.Add(includePlayer);
                    }
                    var roundMinutes = 0.0;
                    if (Plugin._roundState == RoundState.Playing)
                    {
                        roundMinutes = Plugin._serverInfo.GetRoundElapsedTime().TotalMinutes;
                    }
                    var teamTopPlayers = teamPlayers.Where(aPlayer => aPlayer.GetPower(true, roundMinutes >= 5.0, true) > 1);
                    var topPowerSum = teamTopPlayers.Select(aPlayer => aPlayer.GetPower(true, roundMinutes >= 5.0, true)).Sum();
                    var totalPower = Math.Round(topPowerSum);
                    return totalPower;
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error while fetching team power.", e));
                }
                return 0;
            }

            public void Reset()
            {
                try
                {
                    TeamTicketCount = 0;
                    TeamAdjustedTicketCount = 0;
                    TeamTicketCounts.Clear();
                    TeamAdjustedTicketCounts.Clear();
                    TeamAdjustedTicketDifferenceRates.Clear();
                    TeamTicketDifferenceRate = 0;
                    TeamAdjustedTicketDifferenceRate = 0;
                    TeamAdjustedTicketAccellerationRate = 0;
                    TeamTicketsAdded = false;
                    TeamAdjustedTicketsAdded = false;
                    TeamTicketAdjustment = 0;
                    TeamTotalScore = 0;
                    TeamTotalScores.Clear();
                    TeamScoreDifferenceRate = 0;
                    TeamTotalScoresAdded = false;
                    TeamTotalScoresFull = false;
                    TeamTicketCountsFull = false;
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error while resetting team.", e));
                }
            }
        }

        public class AUser
        {
            //No reference to player table made here, plain String id access
            public Dictionary<long, APlayer> soldierDictionary = null;
            public String user_email = null;
            public Int64 user_id = -1;
            public String user_name = null;
            public String user_phone = null;
            public ARole user_role = null;
            public DateTime user_expiration;
            public String user_notes = "";

            public AUser()
            {
                soldierDictionary = new Dictionary<long, APlayer>();
            }
        }

        public class AWeaponStat
        {
            //serviceStars
            public Double ServiceStars = 0;
            //serviceStarsProgress
            public Double ServiceStarsProgress = 0;
            //category
            public String Category;
            //categorySID
            public String CategorySID;
            //slug
            public String ID;
            //name
            public String WarsawID;
            //shotsFired
            public Double Shots = 0;
            //shotsHit
            public Double Hits = 0;
            //accuracy
            public Double Accuracy = 0;
            //headshots
            public Double Headshots = 0;
            //kills
            public Double Kills = 0;
            //timeEquipped
            public TimeSpan Time = TimeSpan.FromSeconds(0);

            //Calculated Values
            public Double HSKR = 0;
            public Double KPM = 0;
            public Double DPS = 0;
        }

        public class AVehicleStat
        {
            //serviceStars
            public Double ServiceStars = 0;
            //serviceStarsProgress
            public Double ServiceStarsProgress = 0;
            //slug
            public String ID;
            //name
            public String WarsawID;
            //kills
            public Double Kills = 0;
            //timeIn
            public TimeSpan TimeIn = TimeSpan.FromSeconds(0);
            //category
            public String Category;
            //destroyXinY
            public Double DestroyXInY;

            //Calculated Values
            public Double KPM = 0;
        }

        public class ASQLUpdate
        {
            public String update_id;
            public String version_minimum;
            public String version_maximum;
            public String message_name;
            public String message_success;
            public String message_failure;
            public Boolean update_checks_hasResults = true;
            public List<String> update_checks;
            public Boolean update_execute_requiresModRows;
            public List<String> update_execute;
            public List<String> update_success;
            public List<String> update_failure;

            public ASQLUpdate()
            {
                update_checks = new List<string>();
                update_execute = new List<string>();
                update_success = new List<string>();
                update_failure = new List<string>();
            }
        }

        internal enum AssessmentTypes
        {
            none,
            black,
            white,
            watch
        }

        public class BBM5108Ban
        {
            public DateTime ban_duration;
            public String ban_length = null;
            public String ban_reason = null;
            public String eaguid = null;
            public String soldiername = null;
        }
    }
}
