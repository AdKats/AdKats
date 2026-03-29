using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

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
        // ===========================================================================================
        // Kill Processing (lines 16207-17974 from original AdKats.cs)
        // ===========================================================================================

        public void KillProcessingThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Kill Processing Thread", 1);
                Thread.CurrentThread.Name = "KillProcessing";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    loopStart = UtcNow();
                    try
                    {
                        Log.Debug(() => "Entering Kill Processing Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Get all unprocessed inbound kills
                        Queue<Kill> inboundPlayerKills;
                        if (_KillProcessingQueue.Count > 0)
                        {
                            Log.Debug(() => "Preparing to lock inbound kill queue to retrive new player kills", 7);
                            lock (_KillProcessingQueue)
                            {
                                Log.Debug(() => "Inbound kills found. Grabbing.", 6);
                                //Grab all kills in the queue
                                inboundPlayerKills = new Queue<Kill>(_KillProcessingQueue.ToArray());
                                //Clear the queue for next run
                                _KillProcessingQueue.Clear();
                            }
                        }
                        else
                        {
                            Log.Debug(() => "No inbound player kills. Waiting for Input.", 6);
                            //Wait for input
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _KillProcessingWaitHandle.Reset();
                            _KillProcessingWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                            loopStart = UtcNow();
                            continue;
                        }

                        //Loop through all kils in order that they came in
                        while (inboundPlayerKills.Count > 0)
                        {
                            if (!_pluginEnabled)
                            {
                                break;
                            }
                            Log.Debug(() => "begin reading player kills", 6);
                            //Dequeue the first/next kill
                            Kill playerKill = inboundPlayerKills.Dequeue();

                            DamageTypes category = DamageTypes.None;
                            if (playerKill != null && !String.IsNullOrEmpty(playerKill.DamageType))
                            {
                                category = WeaponDictionary.GetDamageTypeByWeaponCode(playerKill.DamageType);
                            }
                            if (!_DetectedWeaponCodes.Contains(playerKill.DamageType))
                            {
                                _DetectedWeaponCodes.Add(playerKill.DamageType);
                            }
                            if (!_firstPlayerListComplete)
                            {
                                continue;
                            }
                            APlayer victim = null;
                            _PlayerDictionary.TryGetValue(playerKill.Victim.SoldierName, out victim);
                            APlayer killer = null;
                            _PlayerDictionary.TryGetValue(playerKill.Killer.SoldierName, out killer);
                            if (killer == null || victim == null)
                            {
                                continue;
                            }

                            //Call processing on the player kill
                            ProcessPlayerKill(new AKill(this)
                            {
                                killer = killer,
                                killerCPI = playerKill.Killer,
                                victim = victim,
                                victimCPI = playerKill.Victim,
                                weaponCode = String.IsNullOrEmpty(playerKill.DamageType) ? "NoDamageType" : playerKill.DamageType,
                                weaponDamage = category,
                                TimeStamp = playerKill.TimeOfDeath,
                                UTCTimeStamp = playerKill.TimeOfDeath.ToUniversalTime(),
                                IsSuicide = playerKill.IsSuicide,
                                IsHeadshot = playerKill.Headshot,
                                IsTeamkill = (playerKill.Killer.TeamID == playerKill.Victim.TeamID),
                                RoundID = _roundID
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("kill processing thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in kill processing thread.", e));
                    }
                }
                Log.Debug(() => "Ending Kill Processing Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in kill processing thread.", e));
            }
        }

        private String GetEventMessage(Boolean nextRound)
        {
            Log.Debug(() => "Entering GetEventMessage", 7);
            try
            {
                switch (GetEventRoundRuleCode(GetActiveEventRoundNumber(nextRound)))
                {
                    case AEventOption.RuleCode.KO:
                        return "KNIFE ONLY!";
                    case AEventOption.RuleCode.BSO:
                        return "BOLT SNIPER ONLY!";
                    case AEventOption.RuleCode.MLO:
                        return "Mares LEG ONLY!";
                    case AEventOption.RuleCode.DO:
                        return "DEFIBS ONLY!";
                    case AEventOption.RuleCode.BKO:
                        return "BOW/KNIVES ONLY!";
                    case AEventOption.RuleCode.RTO:
                        return "REPAIR TOOL ONLY!";
                    case AEventOption.RuleCode.PO:
                        return "PISTOLS ONLY!";
                    case AEventOption.RuleCode.SO:
                        return "SHOTGUNS ONLY!";
                    case AEventOption.RuleCode.NE:
                        return "NO EXPLOSIVES!";
                    case AEventOption.RuleCode.EO:
                        return "EXPLOSIVES ONLY!";
                    case AEventOption.RuleCode.AO:
                        return "AUTO-PRIMARIES ONLY!";
                    case AEventOption.RuleCode.ARO:
                        return "ASSAULT RIFLES ONLY!";
                    case AEventOption.RuleCode.LMGO:
                        return "LMGS ONLY!";
                    case AEventOption.RuleCode.GO:
                        return "GRENADES ONLY!";
                    case AEventOption.RuleCode.HO:
                        return "HEADSHOTS ONLY!";
                    case AEventOption.RuleCode.NH:
                        return "NO HEADSHOTS!";
                    case AEventOption.RuleCode.AW:
                        return "ALL WEAPONS!";
                    case AEventOption.RuleCode.CAI:
                        return "COWBOYS AND INDIANS!";
                    case AEventOption.RuleCode.TR:
                        return "TROLL RULES!";
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting event message.", e));
            }
            Log.Debug(() => "Exiting GetEventMessage", 7);
            return "UNKNOWN";
        }

        private String GetEventDescription(Boolean nextRound)
        {
            Log.Debug(() => "Entering GetEventDescription", 7);
            try
            {
                switch (GetEventRoundRuleCode(GetActiveEventRoundNumber(nextRound)))
                {
                    case AEventOption.RuleCode.KO:
                        return "KNIFE ONLY! Only kills with knives are allowed.";
                    case AEventOption.RuleCode.BSO:
                        return "BOLT SNIPER ONLY! Only bolt action sniper rifles are allowed. NO Knives.";
                    case AEventOption.RuleCode.MLO:
                        return "Mares LEG ONLY! Only kills with the mares leg are allowed. NO Knives.";
                    case AEventOption.RuleCode.DO:
                        return "DEFIBS ONLY! Only kills with the medic's defibrilators are allowed.";
                    case AEventOption.RuleCode.BKO:
                        return "BOW/KNIVES ONLY! Only Phantom Bow/Knives are allowed. NO poison/explosive arrows.";
                    case AEventOption.RuleCode.RTO:
                        return "REPAIR TOOL ONLY! Only kills with repair tools and EOD bots are allowed.";
                    case AEventOption.RuleCode.PO:
                        return "PISTOLS ONLY! Only kills with pistols are allowed. NO G18/93R. NO Shorty 12G. Knives allowed.";
                    case AEventOption.RuleCode.SO:
                        return "SHOTGUNS ONLY! Only kills with shotguns are allowed. Any ammo type. Knives allowed.";
                    case AEventOption.RuleCode.NE:
                        return "NO EXPLOSIVES! Kills with explosive weapons are NOT allowed, all others are allowed.";
                    case AEventOption.RuleCode.EO:
                        return "EXPLOSIVES ONLY! Only explosive weapons are allowed. NO shotgun frag rounds. NO Knives.";
                    case AEventOption.RuleCode.AO:
                        return "AUTO-PRIMARIES ONLY! Only automatic primary weapons. Assault rifles, LMGs, Burst, etc. Knives allowed.";
                    case AEventOption.RuleCode.ARO:
                        return "ASSAULT RIFLES ONLY! Only kills with assault rifles are allowed. Knives allowed.";
                    case AEventOption.RuleCode.LMGO:
                        return "LMGS ONLY! Only kills with light machine guns are allowed. Knives allowed.";
                    case AEventOption.RuleCode.GO:
                        return "GRENADES ONLY! Only kills with grenades are allowed. M67, V40, etc. NO Knives.";
                    case AEventOption.RuleCode.HO:
                        return "HEADSHOTS ONLY! All weapons, but only headshots. If you kill without a headshot you are slain.";
                    case AEventOption.RuleCode.NH:
                        return "NO HEADSHOTS! All weapons, but NO headshots. If you kill with a headshot you are slain";
                    case AEventOption.RuleCode.AW:
                        return "ALL WEAPONS! No weapon restrictions. Go nuts.";
                    case AEventOption.RuleCode.CAI:
                        return "COWBOYS AND INDIANS! Phantom Bow, Mares Leg, Revolvers, and Knives only. NO poison/explosive arrows.";
                    case AEventOption.RuleCode.TR:
                        return "TROLL RULES! Knives, Defibs, RepairTools, Shields, EODBots, SUAVs, and Smoke Launchers.";
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting event description.", e));
            }
            Log.Debug(() => "Exiting GetEventDescription", 7);
            return "UNKNOWN";
        }

        private Int32 GetActiveEventRoundNumber(Boolean nextRound)
        {
            Log.Debug(() => "Entering GetEventRoundProgress", 7);
            try
            {
                var roundID = nextRound ? _roundID + 1 : _roundID;
                if (_CurrentEventRoundNumber == 999999 ||
                    _CurrentEventRoundNumber > roundID)
                {
                    Log.Error("Can't get active event round number, event not active for round " + roundID + ".");
                    return 999999;
                }
                return roundID - _CurrentEventRoundNumber;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting event round progress.", e));
            }
            Log.Debug(() => "Exiting GetEventRoundProgress", 7);
            return 999999;
        }

        private AEventOption.MapCode GetEventRoundMapCode(Int32 eventRoundNumber)
        {
            Log.Debug(() => "Entering GetEventRoundMapCode", 7);
            try
            {
                if (!_EventRoundOptions.Any() ||
                    eventRoundNumber < 0 ||
                    eventRoundNumber >= _EventRoundOptions.Count())
                {
                    Log.Error("Event round number " + eventRoundNumber + " was invalid when fetching map code.");
                    return AEventOption.MapCode.UNKNOWN;
                }
                return _EventRoundOptions[eventRoundNumber].Map;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting event round map code.", e));
            }
            Log.Debug(() => "Exiting GetEventRoundMapCode", 7);
            return AEventOption.MapCode.UNKNOWN;
        }

        private AEventOption.ModeCode GetEventRoundModeCode(Int32 eventRoundNumber)
        {
            Log.Debug(() => "Entering GetEventRoundMapModeCode", 7);
            try
            {
                if (!_EventRoundOptions.Any() ||
                    eventRoundNumber < 0 ||
                    eventRoundNumber >= _EventRoundOptions.Count())
                {
                    Log.Error("Event round number " + eventRoundNumber + " was invalid when fetching mode code.");
                    return AEventOption.ModeCode.UNKNOWN;
                }
                return _EventRoundOptions[eventRoundNumber].Mode;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting event round mode code.", e));
            }
            Log.Debug(() => "Exiting GetEventRoundMapModeCode", 7);
            return AEventOption.ModeCode.UNKNOWN;
        }

        private AEventOption.RuleCode GetEventRoundRuleCode(Int32 eventRoundNumber)
        {
            Log.Debug(() => "Entering GetEventRoundRestrictionCode", 7);
            try
            {
                if (!_EventRoundOptions.Any() || eventRoundNumber < 0 || eventRoundNumber >= _EventRoundOptions.Count())
                {
                    Log.Error("Event round number " + eventRoundNumber + " was invalid when fetching restriction code.");
                    return AEventOption.RuleCode.UNKNOWN;
                }
                return _EventRoundOptions[eventRoundNumber].Rule;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting event round restriction code.", e));
            }
            Log.Debug(() => "Exiting GetEventRoundRestrictionCode", 7);
            return AEventOption.RuleCode.UNKNOWN;
        }

        private Boolean ProcessEventKill(AKill aKill, out String message)
        {
            Log.Debug(() => "Entering ProcessEventKill", 7);
            try
            {
                message = GetEventMessage(false) + " EVENT";
                switch (GetEventRoundRuleCode(GetActiveEventRoundNumber(false)))
                {
                    case AEventOption.RuleCode.KO:
                        // KNIFE ONLY!
                        // Only 5 knife codes known, fuzzy match for unknown knife types
                        if (!aKill.weaponCode.ToLower().Contains("knife") &&
                            !aKill.weaponCode.ToLower().Contains("melee") &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.BSO:
                        // BOLT ACTIONS ONLY!
                        if ((aKill.weaponDamage != DamageTypes.SniperRifle) &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.MLO:
                        // Mares LEG ONLY!
                        if (aKill.weaponCode != "U_SaddlegunSnp" &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.DO:
                        // DEFIBS ONLY!
                        if (aKill.weaponCode != "U_Defib" &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.BKO:
                        // PHANTOM BOW AND KNIVES ONLY!
                        if (!aKill.weaponCode.ToLower().Contains("knife") &&
                            !aKill.weaponCode.ToLower().Contains("melee") &&
                            aKill.weaponCode != "dlSHTR" &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.RTO:
                        // REPAIR TOOL ONLY!
                        if (aKill.weaponCode != "U_Repairtool" &&
                            aKill.weaponCode != "EODBot" &&
                            aKill.weaponCode != "Death" &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.PO:
                        // PISTOLS ONLY!
                        if ((aKill.weaponDamage != DamageTypes.Handgun || aKill.weaponCode == "U_M93R" || aKill.weaponCode == "U_Glock18") &&
                            !aKill.weaponCode.ToLower().Contains("knife") &&
                            !aKill.weaponCode.ToLower().Contains("melee") &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.SO:
                        // SHOTGUNS ONLY!
                        if (aKill.weaponDamage != DamageTypes.Shotgun &&
                            !aKill.weaponCode.ToLower().Contains("knife") &&
                            !aKill.weaponCode.ToLower().Contains("melee") &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.NE:
                        // NO EXPLOSIVES!
                        if (aKill.weaponDamage == DamageTypes.Explosive || aKill.weaponDamage == DamageTypes.ProjectileExplosive)
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.EO:
                        // EXPLOSIVES ONLY!
                        if (aKill.weaponDamage != DamageTypes.Explosive &&
                            aKill.weaponDamage != DamageTypes.ProjectileExplosive &&
                            aKill.weaponCode != "DamageArea" &&
                            aKill.weaponCode != "Gameplay/Gadgets/MAV/MAV" &&
                            aKill.weaponCode != "Death")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.AO:
                        // AUTOMATIC PRIMARIES ONLY!
                        if ((!aKill.weaponCode.ToLower().Contains("knife") &&
                             !aKill.weaponCode.ToLower().Contains("melee") &&
                             aKill.weaponDamage != DamageTypes.AssaultRifle &&
                             aKill.weaponDamage != DamageTypes.Carbine &&
                             aKill.weaponDamage != DamageTypes.LMG &&
                             aKill.weaponDamage != DamageTypes.SMG &&
                             aKill.weaponCode != "U_Groza-4" &&
                             aKill.weaponCode != "DamageArea") ||
                            aKill.weaponCode == "dlSHTR")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.ARO:
                        // ASSAULT RIFLES ONLY!
                        if (!aKill.weaponCode.ToLower().Contains("knife") &&
                            !aKill.weaponCode.ToLower().Contains("melee") &&
                            aKill.weaponDamage != DamageTypes.AssaultRifle &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.LMGO:
                        // LMGS ONLY!
                        if (!aKill.weaponCode.ToLower().Contains("knife") &&
                            !aKill.weaponCode.ToLower().Contains("melee") &&
                            aKill.weaponDamage != DamageTypes.LMG &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.GO:
                        // GRENADES ONLY!
                        if (aKill.weaponCode != "U_M67" &&
                            aKill.weaponCode != "U_M34" &&
                            aKill.weaponCode != "U_V40" &&
                            aKill.weaponCode != "U_Grenade_RGO" &&
                            aKill.weaponCode != "DamageArea")
                        {
                            return true;
                        }
                        break;
                    case AEventOption.RuleCode.HO:
                        // HEADSHOTS ONLY!
                        if (!aKill.IsHeadshot &&
                            aKill.weaponCode != "DamageArea")
                        {
                            QueueRecordForProcessing(new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_kill"),
                                command_numeric = _roundID,
                                target_name = aKill.killer.player_name,
                                target_player = aKill.killer,
                                source_name = "AutoAdmin",
                                record_time = UtcNow(),
                                record_message = "HEADSHOTS ONLY THIS ROUND!"
                            });
                        }
                        return false;
                    case AEventOption.RuleCode.NH:
                        // NO HEADSHOTS!
                        if (aKill.IsHeadshot &&
                            aKill.weaponCode != "DamageArea")
                        {
                            QueueRecordForProcessing(new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_kill"),
                                command_numeric = _roundID,
                                target_name = aKill.killer.player_name,
                                target_player = aKill.killer,
                                source_name = "AutoAdmin",
                                record_time = UtcNow(),
                                record_message = "NO HEADSHOTS THIS ROUND!"
                            });
                        }
                        return false;
                    case AEventOption.RuleCode.AW:
                        // Everything is allowed, always return false
                        return false;
                    case AEventOption.RuleCode.CAI:
                        // COWBOYS AND INDIANS!
                        // Phantom Bow, Mares Leg, Revolvers, and Knives.
                        if (!aKill.weaponCode.ToLower().Contains("knife") &&
                            !aKill.weaponCode.ToLower().Contains("melee") &&
                            aKill.weaponCode != "U_SaddlegunSnp" &&
                            aKill.weaponCode != "U_SW40" &&
                            aKill.weaponCode != "U_Taurus44" &&
                            aKill.weaponCode != "U_Unica6" &&
                            aKill.weaponCode != "U_MP412Rex" &&
                            aKill.weaponCode != "dlSHTR" &&
                            aKill.weaponCode != "DamageArea")
                        {
                            message = GetEventMessage(false) + " Use !rules for details.";
                            return true;
                        }
                        return false;
                    case AEventOption.RuleCode.TR:
                        // TROLL RULES!
                        if (!aKill.weaponCode.ToLower().Contains("knife") &&
                            !aKill.weaponCode.ToLower().Contains("melee") &&
                            aKill.weaponCode != "U_Defib" &&
                            aKill.weaponCode != "U_Repairtool" &&
                            aKill.weaponCode != "U_BallisticShield" &&
                            aKill.weaponCode != "EODBot" &&
                            aKill.weaponCode != "Death" &&
                            aKill.weaponCode != "U_SUAV" &&
                            aKill.weaponCode.ToLower() != "roadkill" &&
                            aKill.weaponCode != "Gameplay/Gadgets/MAV/MAV" &&
                            aKill.weaponCode != "XP4/Gameplay/Gadgets/MKV/MKV" &&
                            aKill.weaponCode != "U_XM25_Smoke" &&
                            !aKill.weaponCode.ToLower().Contains("m320_smk") &&
                            aKill.weaponCode != "DamageArea")
                        {
                            message = GetEventMessage(false) + " Use !rules for details.";
                            return true;
                        }
                        break;
                    default:
                        Log.Error("Unknown restriction type when processing event kill");
                        break;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while processing event kill.", e));
            }
            finally
            {
                Log.Debug(() => "Exiting ProcessEventKill", 7);
            }
            message = null;
            return false;
        }

        private Boolean EventActive()
        {
            return EventActive(_roundID);
        }

        private Boolean EventActive(Int32 roundID)
        {
            return _EventRoundOptions.Any() &&
                   _CurrentEventRoundNumber != 999999 &&
                   roundID >= _CurrentEventRoundNumber &&
                   roundID < _CurrentEventRoundNumber + _EventRoundOptions.Count();
        }

        private void ProcessPlayerKill(AKill aKill)
        {
            try
            {
                aKill.victim.lastAction = UtcNow();
                aKill.killer.lastAction = UtcNow();

                if (_DebugKills)
                {
                    Log.Info(aKill.ToString());
                }

                //Add the unmatched unique round death
                if (!_unmatchedRoundDeaths.Contains(aKill.victim.player_name))
                {
                    _unmatchedRoundDeaths.Add(aKill.victim.player_name);
                }
                //Add the unmatched round death count
                if (_unmatchedRoundDeathCounts.ContainsKey(aKill.victim.player_name))
                {
                    _unmatchedRoundDeathCounts[aKill.victim.player_name] = _unmatchedRoundDeathCounts[aKill.victim.player_name] + 1;
                }
                else
                {
                    _unmatchedRoundDeathCounts[aKill.victim.player_name] = 1;
                }

                Boolean gKillHandled = false;
                //Update player death information
                Log.Debug(() => "Setting " + aKill.victim.GetVerboseName() + " time of death to " + aKill.TimeStamp, 7);
                aKill.victim.lastDeath = UtcNow();

                //Add the kill
                aKill.killer.LiveKills.Add(aKill);

                if (_useAntiCheatLIVESystem &&
                    _AntiCheatLIVESystemActiveStats &&
                    _serverInfo.ServerType != "OFFICIAL" &&
                    !PlayerProtected(aKill.killer) &&
                    !EventActive())
                {
                    //KPM check
                    Int32 lowCountRecent = aKill.killer.LiveKills.Count(dKill => 0 <= (DateTime.Now - dKill.TimeStamp).TotalSeconds
                                                                                 && (DateTime.Now - dKill.TimeStamp).TotalSeconds < 60
                                                                                 && dKill.weaponDamage != DamageTypes.VehicleAir);
                    int lowCountBan =
                        ((GameVersion == GameVersionEnum.BF3) ? (25) : (20)) -
                        ((aKill.killer.fbpInfo.Rank <= 15) ? (6) : (0));
                    if (lowCountRecent >= lowCountBan)
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_ban_perm"),
                            command_numeric = 0,
                            target_name = aKill.killer.player_name,
                            target_player = aKill.killer,
                            source_name = "AutoAdmin",
                            record_message = _AntiCheatKPMBanMessage + " [LIVE][5-L" + lowCountBan + "-" + lowCountRecent + "]",
                            record_time = UtcNow()
                        });
                        return;
                    }
                    Int32 highCountRecent = aKill.killer.LiveKills.Count(dKill => 0 <= (DateTime.Now - dKill.TimeStamp).TotalSeconds
                                                                                  && (DateTime.Now - dKill.TimeStamp).TotalSeconds < 120
                                                                                  && dKill.weaponDamage != DamageTypes.VehicleAir);
                    int highCountBan =
                        ((GameVersion == GameVersionEnum.BF3) ? (40) : (32)) -
                        ((aKill.killer.fbpInfo.Rank <= 15) ? (8) : (0));
                    if (highCountRecent >= highCountBan)
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_ban_perm"),
                            command_numeric = 0,
                            target_name = aKill.killer.player_name,
                            target_player = aKill.killer,
                            source_name = "AutoAdmin",
                            record_message = _AntiCheatKPMBanMessage + " [LIVE][5-H" + highCountBan + "-" + highCountRecent + "]",
                            record_time = UtcNow()
                        });
                        return;
                    }

                    //HSK Check
                    Int32 lowKillCount = 22;
                    Double lowKillTriggerHSKP = 90;
                    Int32 highKillCount = 47;
                    Double highKillTriggerHSKP = 80;
                    if (_serverInfo.InfoObject.Map == "XP0_Metro" ||
                        _serverInfo.InfoObject.Map == "MP_Prison")
                    {
                        lowKillCount = 30;
                        highKillCount = 60;
                    }
                    var nonSniperKills = aKill.killer.LiveKills
                        .Where(dKill =>
                            dKill.weaponDamage != DamageTypes.SniperRifle &&
                            dKill.weaponDamage != DamageTypes.DMR)
                        .OrderByDescending(dKill => dKill.TimeStamp);
                    var countAll = nonSniperKills.Count();
                    if (countAll >= lowKillCount)
                    {
                        var lowKillHSKP = nonSniperKills.Take(lowKillCount).Count(dKill => dKill.IsHeadshot) / ((Double)lowKillCount) * 100.0;
                        var highKillHSKP = nonSniperKills.Take(highKillCount).Count(dKill => dKill.IsHeadshot) / ((Double)highKillCount) * 100.0;
                        String actionMessage = null;
                        if (countAll >= lowKillCount && lowKillHSKP >= lowKillTriggerHSKP)
                        {
                            actionMessage = _AntiCheatHSKBanMessage + " [LIVE][6-L" + lowKillCount + "-" + countAll + "-" + Math.Round(lowKillHSKP) + "]";
                        }
                        else if (countAll >= highKillCount && highKillHSKP >= highKillTriggerHSKP)
                        {
                            actionMessage = _AntiCheatHSKBanMessage + " [LIVE][6-H" + highKillCount + "-" + countAll + "-" + Math.Round(highKillHSKP) + "]";
                        }
                        if (!String.IsNullOrEmpty(actionMessage))
                        {
                            //Create ban record
                            QueueRecordForProcessing(new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_ban_perm"),
                                command_numeric = 0,
                                target_name = aKill.killer.player_name,
                                target_player = aKill.killer,
                                source_name = "AutoAdmin",
                                record_message = actionMessage,
                                record_time = UtcNow()
                            });
                            return;
                        }
                        if (highKillHSKP >= 75 &&
                            !aKill.killer.TargetedRecords.Any(aRecord => aRecord.record_message.Contains("non-sniper HSKP") &&
                                                                         (UtcNow() - aRecord.record_time).TotalMinutes <= 30))
                        {
                            //Create report record
                            QueueRecordForProcessing(new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_report"),
                                command_numeric = 0,
                                target_name = aKill.killer.player_name,
                                target_player = aKill.killer,
                                source_name = "AutoAdmin",
                                record_message = Math.Round(highKillHSKP) + "% non-sniper HSKP",
                                record_time = UtcNow()
                            });
                        }
                    }
                }

                // Catch BF4 gadget kills
                if (GameVersion == GameVersionEnum.BF4)
                {
                    //Special weapons
                    String actedCode = null;
                    Int32 triggerCount = 3;
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "U_PortableAmmopack") >= triggerCount)
                    {
                        actedCode = "1";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "U_RadioBeacon") >= triggerCount)
                    {
                        actedCode = "2";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "Gameplay/Gadgets/SOFLAM/SOFLAM_Projectile") >= triggerCount)
                    {
                        actedCode = "3";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "U_Motionsensor") >= triggerCount)
                    {
                        actedCode = "4";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "U_PortableMedicpack" && !dKill.IsTeamkill) >= triggerCount)
                    {
                        actedCode = "5";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "U_Medkit" && !dKill.IsTeamkill) >= triggerCount)
                    {
                        actedCode = "6";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "U_Ammobag") >= triggerCount)
                    {
                        actedCode = "7";
                    }
                    if (!String.IsNullOrEmpty(actedCode) && !PlayerProtected(aKill.killer))
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_ban_perm"),
                            command_numeric = 0,
                            target_name = aKill.killer.player_name,
                            target_player = aKill.killer,
                            source_name = "AutoAdmin",
                            record_message = "[LIVE][Code 8-" + actedCode + "]: Dispute Requested",
                            record_time = UtcNow()
                        });
                        return;
                    }
                }
                // Catch BF3 gadget kills
                if (GameVersion == GameVersionEnum.BF3)
                {
                    //Special weapons
                    String actedCode = null;
                    Int32 triggerCount = 3;
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "AmmoBag") >= triggerCount)
                    {
                        actedCode = "1";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "Weapons/Gadgets/RadioBeacon/Radio_Beacon") >= triggerCount)
                    {
                        actedCode = "2";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "Weapons/Gadgets/SOFLAM/SOFLAM_PDA") >= triggerCount)
                    {
                        actedCode = "3";
                    }
                    if (aKill.killer.LiveKills.Count(dKill => dKill.RoundID == _roundID && dKill.weaponCode == "Medkit" && !dKill.IsTeamkill) >= triggerCount)
                    {
                        actedCode = "4";
                    }
                    if (!String.IsNullOrEmpty(actedCode) && !PlayerProtected(aKill.killer))
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_ban_perm"),
                            command_numeric = 0,
                            target_name = aKill.killer.player_name,
                            target_player = aKill.killer,
                            source_name = "AutoAdmin",
                            record_message = "Code [8-" + actedCode + "]: Dispute Requested",
                            record_time = UtcNow()
                        });
                        return;
                    }
                }

                //Grenade cooking catcher
                //Only add the last death if it's not a death by admin
                if (!String.IsNullOrEmpty(aKill.killer.player_name))
                {
                    try
                    {
                        if (_UseGrenadeCookCatcher)
                        {
                            if (_RoundCookers == null)
                            {
                                _RoundCookers = new Dictionary<String, APlayer>();
                            }
                            const double possibleRange = 1100.00;
                            //Check for cooked grenade and non-suicide
                            if (aKill.weaponCode.Contains("M67") || aKill.weaponCode.Contains("V40"))
                            {
                                if (true)
                                {
                                    Double fuseTime = 0;
                                    if (aKill.weaponCode.Contains("M67"))
                                    {
                                        if (GameVersion == GameVersionEnum.BF3)
                                        {
                                            fuseTime = 3735.00;
                                        }
                                        else if (GameVersion == GameVersionEnum.BF4)
                                        {
                                            fuseTime = 3132.00;
                                        }
                                    }
                                    else if (aKill.weaponCode.Contains("V40"))
                                    {
                                        fuseTime = 2865.00;
                                    }
                                    Boolean told = false;
                                    List<KeyValuePair<APlayer, string>> possible = new List<KeyValuePair<APlayer, String>>();
                                    List<KeyValuePair<APlayer, string>> sure = new List<KeyValuePair<APlayer, String>>();
                                    foreach (AKill cookerKill in aKill.killer.LiveKills
                                        .Where(dKill => (aKill.TimeStamp - dKill.TimeStamp).TotalSeconds < 10.0)
                                        .OrderBy(dKill => Math.Abs(aKill.TimeStamp.Subtract(dKill.TimeStamp).TotalMilliseconds - fuseTime)))
                                    {
                                        //Get the actual time since cooker value
                                        Double milli = aKill.TimeStamp.Subtract(cookerKill.TimeStamp).TotalMilliseconds;

                                        //Calculate the percentage probability
                                        Double probability;
                                        if (Math.Abs(milli - fuseTime) < possibleRange)
                                        {
                                            probability = (1 - Math.Abs((milli - fuseTime) / possibleRange)) * 100;
                                            Log.Debug(() => cookerKill.victim.GetVerboseName() + " cooking probability: " + probability + "%", 2);
                                        }
                                        else
                                        {
                                            probability = 0.00;
                                        }

                                        //If probability > 60% report the player and add them to the round cookers list
                                        if (probability > 60.00)
                                        {
                                            Log.Debug(() => cookerKill.victim.GetVerboseName() + " in " + aKill.killer.GetVerboseName() + "'s recent kills has a " + probability + "% cooking probability.", 2);
                                            gKillHandled = true;

                                            //Inform every player killed by the nade that it was a cooked nade
                                            PlayerTellMessage(aKill.victim.player_name, aKill.killer.GetVerboseName() + " was a victim of grenade cooking, they did not use explosives.");

                                            //Code to avoid spam
                                            if (aKill.killer.lastKill.AddSeconds(2) < UtcNow())
                                            {
                                                aKill.killer.lastKill = UtcNow();
                                            }
                                            else
                                            {
                                                Log.Debug(() => "Skipping additional auto-actions for multi-kill event.", 3);
                                                continue;
                                            }

                                            if (!told)
                                            {
                                                //Inform the victim player that they will not be punished
                                                PlayerTellMessage(aKill.killer.player_name, "You appear to be a victim of grenade cooking and will NOT be punished.");
                                                told = true;
                                            }

                                            //Create the probability String
                                            String probString = ((int)probability) + "-" + ((int)milli);

                                            //If the player is already on the round cooker list, punish them
                                            if (_RoundCookers.ContainsKey(cookerKill.victim.player_name))
                                            {
                                                //Create the punish record
                                                ARecord record = new ARecord
                                                {
                                                    record_source = ARecord.Sources.Automated,
                                                    server_id = _serverInfo.ServerID,
                                                    command_type = GetCommandByKey("player_punish"),
                                                    command_numeric = 0,
                                                    target_name = cookerKill.victim.player_name,
                                                    target_player = cookerKill.victim,
                                                    source_name = "AutoAdmin",
                                                    record_message = "Rules: Cooking Grenades [" + probString + "-X] [Victim " + aKill.killer.GetVerboseName() + " Protected]",
                                                    record_time = UtcNow()
                                                };
                                                //Process the record
                                                QueueRecordForProcessing(record);
                                                //adminSay("Punishing " + killer.player_name + " for " + record.record_message);
                                                Log.Debug(() => record.GetTargetNames() + " punished for " + record.record_message, 2);
                                                return;
                                            }
                                            //else if probability > 92.5% add them to the SURE list, and round cooker list
                                            if (probability > 92.5)
                                            {
                                                _RoundCookers.Add(cookerKill.victim.player_name, cookerKill.victim);
                                                Log.Debug(() => cookerKill.victim.GetVerboseName() + " added to round cooker list.", 2);
                                                //Add to SURE
                                                sure.Add(new KeyValuePair<APlayer, String>(cookerKill.victim, probString));
                                            }
                                            //Otherwise add them to the round cooker list, and add to POSSIBLE list
                                            else
                                            {
                                                _RoundCookers.Add(cookerKill.victim.player_name, cookerKill.victim);
                                                Log.Debug(() => cookerKill.victim.GetVerboseName() + " added to round cooker list.", 2);
                                                //Add to POSSIBLE
                                                possible.Add(new KeyValuePair<APlayer, String>(cookerKill.victim, probString));
                                            }
                                        }
                                    }

                                    if (sure.Count == 1 && possible.Count == 0 && GameVersion == GameVersionEnum.BF3)
                                    {
                                        APlayer player = sure[0].Key;
                                        String probString = sure[0].Value;
                                        //Create the ban record
                                        ARecord record = new ARecord
                                        {
                                            record_source = ARecord.Sources.Automated,
                                            server_id = _serverInfo.ServerID,
                                            command_type = GetCommandByKey("player_punish"),
                                            command_numeric = 0,
                                            target_name = player.player_name,
                                            target_player = player,
                                            source_name = "AutoAdmin",
                                            record_message = "Rules: Cooking Grenades [" + probString + "] [Victim " + aKill.killer.GetVerboseName() + " Protected]",
                                            record_time = UtcNow()
                                        };
                                        //Process the record
                                        QueueRecordForProcessing(record);
                                        //adminSay("Punishing " + killer.player_name + " for " + record.record_message);
                                        Log.Debug(() => record.GetTargetNames() + " punished for " + record.record_message, 2);
                                    }
                                    else
                                    {
                                        APlayer player;
                                        String probString;
                                        foreach (KeyValuePair<APlayer, string> playerPair in sure)
                                        {
                                            player = playerPair.Key;
                                            probString = playerPair.Value;
                                            //Create the report record
                                            ARecord record = new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                server_id = _serverInfo.ServerID,
                                                command_type = GetCommandByKey("player_report"),
                                                command_numeric = 0,
                                                target_name = player.player_name,
                                                target_player = player,
                                                source_name = "AutoAdmin",
                                                record_message = "Possible Grenade Cooker [" + probString + "] [Victim " + aKill.killer.GetVerboseName() + " Protected]",
                                                record_time = UtcNow()
                                            };
                                            //Process the record
                                            QueueRecordForProcessing(record);
                                            Log.Debug(() => record.GetTargetNames() + " reported for " + record.record_message, 2);
                                        }
                                        foreach (KeyValuePair<APlayer, string> playerPair in possible)
                                        {
                                            player = playerPair.Key;
                                            probString = playerPair.Value;
                                            //Create the report record
                                            ARecord record = new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                server_id = _serverInfo.ServerID,
                                                command_type = GetCommandByKey("player_report"),
                                                command_numeric = 0,
                                                target_name = player.player_name,
                                                target_player = player,
                                                source_name = "AutoAdmin",
                                                record_message = "Possible Grenade Cooker [" + probString + "] [Victim " + aKill.killer.GetVerboseName() + " Protected]",
                                                record_time = UtcNow()
                                            };
                                            //Process the record
                                            QueueRecordForProcessing(record);
                                            Log.Debug(() => record.GetTargetNames() + " reported for " + record.record_message, 2);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error in grenade cook catcher.", e));
                    }
                }

                var acted = false;
                try
                {
                    if (EventActive())
                    {
                        if (aKill.killerCPI.TeamID != aKill.victimCPI.TeamID && _roundState == RoundState.Playing)
                        {
                            var killSpam = (aKill.killer.lastKill.AddSeconds(2) > UtcNow());
                            aKill.killer.lastKill = UtcNow();
                            String recordMessage;
                            if (ProcessEventKill(aKill, out recordMessage))
                            {
                                ACommand aCommand = GetCommandByKey("player_kill");
                                if (_populationStatus == PopulationState.High &&
                                    aKill.killer.TargetedRecords.Any(targetedRecord =>
                                    (targetedRecord.command_numeric == _roundID) &&
                                    (targetedRecord.command_action.command_key == "player_kill" || targetedRecord.command_action.command_key == "player_kick") &&
                                    (UtcNow() - targetedRecord.record_time).TotalMinutes < 7.5))
                                {
                                    aCommand = GetCommandByKey("player_kick");
                                }
                                QueueRecordForProcessing(new ARecord
                                {
                                    record_source = ARecord.Sources.Automated,
                                    server_id = _serverInfo.ServerID,
                                    command_type = aCommand,
                                    command_numeric = _roundID,
                                    target_name = aKill.killer.player_name,
                                    target_player = aKill.killer,
                                    source_name = "AutoAdmin",
                                    record_time = UtcNow(),
                                    record_message = recordMessage
                                });
                                acted = true;
                            }
                        }
                    }
                    else if (_UseWeaponLimiter && !gKillHandled)
                    {
                        //Check for restricted weapon
                        if (Regex.Match(aKill.weaponCode, @"(?:" + _WeaponLimiterString + ")", RegexOptions.IgnoreCase).Success)
                        {
                            //Check for exception type
                            if (!Regex.Match(aKill.weaponCode, @"(?:" + _WeaponLimiterExceptionString + ")", RegexOptions.IgnoreCase).Success)
                            {
                                //Check if suicide
                                if (!aKill.IsSuicide)
                                {
                                    //Get player from the dictionary
                                    if (aKill.killer != null)
                                    {
                                        var killSpam = (aKill.killer.lastKill.AddSeconds(2) > UtcNow());
                                        aKill.killer.lastKill = UtcNow();

                                        const string removeWeapon = "Weapons/";
                                        const string removeGadgets = "Gadgets/";
                                        const string removePrefix = "U_";
                                        String weapon = WeaponDictionary.GetShortWeaponNameByCode(aKill.weaponCode);
                                        Int32 index = weapon.IndexOf(removeWeapon, StringComparison.Ordinal);
                                        weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removeWeapon.Length));
                                        index = weapon.IndexOf(removeGadgets, StringComparison.Ordinal);
                                        weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removeGadgets.Length));
                                        index = weapon.IndexOf(removePrefix, StringComparison.Ordinal);
                                        weapon = (index < 0) ? (weapon) : (weapon.Remove(index, removePrefix.Length));

                                        //Record to boost rep for victim
                                        if (aKill.killer.IsLocked())
                                        {
                                            PlayerYellMessage(aKill.victim.player_name, aKill.killer.GetVerboseName() + " is currently locked from autoadmin actions for " + FormatTimeString(aKill.killer.GetLockRemaining(), 2) + ".");
                                        }
                                        else
                                        {
                                            PlayerYellMessage(aKill.victim.player_name, aKill.killer.GetVerboseName() + " was punished for killing you with " + weapon);
                                        }
                                        ARecord repRecord = new ARecord
                                        {
                                            record_source = ARecord.Sources.Automated,
                                            server_id = _serverInfo.ServerID,
                                            command_type = GetCommandByKey("player_repboost"),
                                            command_numeric = 0,
                                            target_name = aKill.victim.player_name,
                                            target_player = aKill.victim,
                                            source_name = "RepManager",
                                            record_message = "Player killed by restricted weapon " + weapon,
                                            record_time = UtcNow()
                                        };
                                        QueueRecordForProcessing(repRecord);

                                        if (!killSpam && _serverInfo.ServerType != "OFFICIAL")
                                        {
                                            //Create the punish record
                                            ARecord record = new ARecord
                                            {
                                                record_source = ARecord.Sources.Automated,
                                                server_id = _serverInfo.ServerID,
                                                command_type = GetCommandByKey("player_punish"),
                                                command_numeric = 0,
                                                target_name = aKill.killer.player_name,
                                                target_player = aKill.killer,
                                                source_name = "AutoAdmin",
                                                record_time = UtcNow()
                                            };
                                            if (weapon.ToLower() == "roadkill")
                                            {
                                                record.record_message = "Rules: Roadkilling with EOD or MAV";
                                            }
                                            else if (weapon == "Death")
                                            {
                                                if (GameVersion == GameVersionEnum.BF3)
                                                {
                                                    record.record_message = "Rules: Using Mortar";
                                                }
                                                else if (GameVersion == GameVersionEnum.BF4)
                                                {
                                                    record.record_message = "Rules: Using EOD Bot";
                                                }
                                            }
                                            else
                                            {
                                                record.record_message = "Rules: Using Explosives [" + weapon + "]";
                                            }

                                            //Process the record
                                            QueueRecordForProcessing(record);
                                            acted = true;
                                        }
                                        else
                                        {
                                            Log.Debug(() => "Skipping additional auto-actions for multi-kill event.", 3);
                                        }
                                    }
                                    else
                                    {
                                        Log.Error("Killer was null when processing kill");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error in no explosives auto-admin.", e));
                }

                try
                {
                    if (!acted &&
                        ChallengeManager != null &&
                        ChallengeManager.Loaded &&
                        !EventActive() &&
                        GetPlayerCount() >= ChallengeManager.MinimumPlayers)
                    {
                        if (aKill.killer.ActiveChallenge == null)
                        {
                            ChallengeManager.AssignRoundChallengeIfKillValid(aKill);
                        }
                        if (aKill.killer.ActiveChallenge != null)
                        {
                            aKill.killer.ActiveChallenge.AddKill(aKill);
                        }
                        if (aKill.victim.ActiveChallenge != null)
                        {
                            aKill.victim.ActiveChallenge.AddDeath(aKill);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.HandleException(new AException("Error while running challenge kill processing.", e));
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while processing player kill.", e));
            }
            Log.Debug(() => "Exiting OnPlayerKilled", 7);
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            Log.Debug(() => "Entering OnPlayerSpawned", 7);
            try
            {
                APlayer aPlayer = null;
                if (_pluginEnabled && _threadsReady && _firstPlayerListComplete)
                {
                    //Fetch the player
                    if (!_PlayerDictionary.TryGetValue(soldierName, out aPlayer))
                    {
                        Log.Warn(soldierName + " spawned without being in player list.");
                        if (!_MissingPlayers.Contains(soldierName))
                        {
                            _MissingPlayers.Add(soldierName);
                        }
                        return;
                    }
                    aPlayer.player_spawnedRound = true;

                    //Ensure frostbite player info
                    if (aPlayer.fbpInfo == null)
                    {
                        return;
                    }

                    //Fetch teams
                    ATeam team1, team2;
                    if (!GetTeamByID(1, out team1))
                    {
                        if (_roundState == RoundState.Playing)
                        {
                            Log.Error("Teams not loaded when they should be.");
                        }
                        return;
                    }
                    if (!GetTeamByID(2, out team2))
                    {
                        if (_roundState == RoundState.Playing)
                        {
                            Log.Error("Teams not loaded when they should be.");
                        }
                        return;
                    }
                    ATeam friendlyTeam, enemyTeam;
                    if (aPlayer.fbpInfo.TeamID == team1.TeamID)
                    {
                        friendlyTeam = team1;
                        enemyTeam = team2;
                    }
                    else
                    {
                        friendlyTeam = team2;
                        enemyTeam = team1;
                    }

                    if (_roundState == RoundState.Loaded)
                    {
                        _playingStartTime = UtcNow();
                        _roundState = RoundState.Playing;

                        //Take minimum ticket count between teams (accounts for rush), but not less than 0
                        _startingTicketCount = Math.Max(0, Math.Min(team1.TeamTicketCount, team2.TeamTicketCount));

                        if (EventActive())
                        {
                            Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                            {
                                Thread.CurrentThread.Name = "RoundWelcome";
                                Thread.Sleep(TimeSpan.FromSeconds(10));
                                AdminTellMessage("WELCOME TO ROUND EVENT " + String.Format("{0:n0}", _roundID) + "! " + GetEventMessage(false));
                                Int32 messages = 0;
                                while (messages++ < 10)
                                {
                                    Threading.Wait(TimeSpan.FromSeconds(3));
                                    AdminSayMessage(GetEventMessage(false) + " Use !rules for details.");
                                }
                                Threading.StopWatchdog();
                            })));
                        }
                        else if (_UseExperimentalTools && GameVersion == GameVersionEnum.BF4 && _serverInfo != null && _serverInfo.GetRoundElapsedTime().TotalSeconds < 30)
                        {
                            if (_serverInfo.ServerName.ToLower().Contains("metro") && _serverInfo.ServerName.ToLower().Contains("no explosives"))
                            {
                                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "RoundWelcome";
                                    Thread.Sleep(TimeSpan.FromSeconds(17));
                                    AdminTellMessage("Welcome to round " + String.Format("{0:n0}", _roundID) + " of No Explosives Metro!");
                                    Threading.StopWatchdog();
                                })));
                            }
                            else if (_serverInfo.ServerName.ToLower().Contains("locker") && _serverInfo.ServerName.ToLower().Contains("pistol"))
                            {
                                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "RoundWelcome";
                                    Thread.Sleep(TimeSpan.FromSeconds(17));
                                    AdminTellMessage("Welcome to round " + String.Format("{0:n0}", _roundID) + " of Pistols Only Locker!");
                                    Threading.StopWatchdog();
                                })));
                            }
                        }

                        if (_useRoundTimer)
                        {
                            StartRoundTimer();
                        }

                        if (ChallengeManager != null)
                        {
                            ChallengeManager.OnRoundPlaying(_roundID);
                        }
                    }

                    if (_CommandNameDictionary.Count > 0)
                    {
                        //Handle TeamSwap notifications
                        String command = GetChatCommandByKey("self_teamswap");
                        aPlayer.lastSpawn = UtcNow();
                        aPlayer.lastAction = UtcNow();

                        //Add matched spawn count
                        if (_unmatchedRoundDeaths.Contains(aPlayer.player_name))
                        {
                            friendlyTeam.IncrementTeamTicketAdjustment();
                        }
                        //Removed unmatched death if applicable
                        _unmatchedRoundDeaths.Remove(aPlayer.player_name);
                        //Decrement unmatched death count if applicable
                        if (_unmatchedRoundDeathCounts.ContainsKey(aPlayer.player_name))
                        {
                            _unmatchedRoundDeathCounts[aPlayer.player_name] = _unmatchedRoundDeathCounts[aPlayer.player_name] - 1;
                        }

                        if (aPlayer.player_aa && !aPlayer.player_aa_told)
                        {
                            String adminAssistantMessage = "You are an Admin Assistant. ";
                            if (!_UseAAReportAutoHandler && !_EnableAdminAssistantPerk)
                            {
                                adminAssistantMessage += "Thank you for your consistent reporting.";
                            }
                            else
                            {
                                adminAssistantMessage += "Perks: ";
                                if (_UseAAReportAutoHandler)
                                {
                                    adminAssistantMessage += "AutoAdmin can handle some of your reports. ";
                                }
                                if (_EnableAdminAssistantPerk)
                                {
                                    adminAssistantMessage += "You can use the " + command + " command.";
                                }
                            }
                            PlayerSayMessage(soldierName, adminAssistantMessage);
                            aPlayer.player_aa_told = true;
                        }
                    }

                    //Handle Dev Notifications
                    if (soldierName == "H3dius" && !_toldCol)
                    {
                        PlayerTellMessage("H3dius", "AdKats " + PluginVersion + " running!");
                        _toldCol = true;
                    }

                    var startDuration = NowDuration(_AdKatsStartTime).TotalSeconds;
                    var startupDuration = TimeSpan.FromSeconds(_startupDurations.Average(span => span.TotalSeconds)).TotalSeconds;
                    if (!aPlayer.player_spawnedOnce &&
                        ChallengeManager != null)
                    {
                        // Make sure that they have their challenge entry assigned if applicable
                        ChallengeManager.AssignActiveEntryForPlayer(aPlayer);
                    }
                    if (!aPlayer.player_spawnedOnce && startDuration - startupDuration > 120)
                    {
                        if (_ShowNewPlayerAnnouncement && aPlayer.player_new)
                        {
                            OnlineAdminSayMessage(aPlayer.GetVerboseName() + " just joined for the first time!");
                        }

                        if (_UseFirstSpawnMessage ||
                            (_battlecryVolume != BattlecryVolume.Disabled && !String.IsNullOrEmpty(aPlayer.player_battlecry)) ||
                            _UsePerkExpirationNotify ||
                            _UseFirstSpawnMutedMessage)
                        {
                            Thread spawnPrinter = new Thread(new ThreadStart(delegate
                            {
                                Log.Debug(() => "Starting a spawn printer thread.", 5);
                                try
                                {
                                    Thread.CurrentThread.Name = "SpawnPrinter";

                                    // Wait 2 seconds
                                    Threading.Wait(2000);

                                    // Send warning to player if the player is muted.
                                    if (_UseFirstSpawnMutedMessage && (GetMatchingVerboseASPlayersOfGroup("persistent_mute", aPlayer).Any() || GetMatchingVerboseASPlayersOfGroup("persistent_mute_force", aPlayer).Any()))
                                    {
                                        PlayerTellMessage(aPlayer.player_name, _FirstSpawnMutedMessage);
                                        Threading.Wait(TimeSpan.FromSeconds(_YellDuration));
                                    }

                                    // Send perk expiration notification
                                    if (_UsePerkExpirationNotify)
                                    {
                                        var groups = GetMatchingVerboseASPlayers(aPlayer);
                                        var expiringGroups = groups.Where(group => NowDuration(group.player_expiration).TotalDays < _PerkExpirationNotifyDays);
                                        if (expiringGroups.Any())
                                        {
                                            PlayerTellMessage(aPlayer.player_name, "You have perks expiring soon. Use " + GetChatCommandByKey("player_perks") + " to see your perks.");
                                            Threading.Wait(TimeSpan.FromSeconds(_YellDuration));
                                        }
                                    }

                                    // Battlecry
                                    if (_battlecryVolume != BattlecryVolume.Disabled &&
                                        !String.IsNullOrEmpty(aPlayer.player_battlecry))
                                    {
                                        switch (_battlecryVolume)
                                        {
                                            case BattlecryVolume.Say:
                                                AdminSayMessage(aPlayer.player_battlecry);
                                                break;
                                            case BattlecryVolume.Yell:
                                                AdminYellMessage(aPlayer.player_battlecry);
                                                break;
                                            case BattlecryVolume.Tell:
                                                AdminTellMessage(aPlayer.player_battlecry);
                                                break;
                                        }
                                        Threading.Wait(TimeSpan.FromSeconds(_YellDuration));
                                    }
                                    else if (_UseFirstSpawnMessage)
                                    {
                                        if (_EnableLowPlaytimeSpawnMessage && aPlayer.player_serverplaytime.TotalHours < (double)_LowPlaytimeSpawnMessageHours)
                                        {
                                            PlayerTellMessage(aPlayer.player_name, _LowPlaytimeSpawnMessage);
                                        }
                                        else
                                        {
                                            PlayerTellMessage(aPlayer.player_name, _FirstSpawnMessage);
                                        }
                                        Threading.Wait(TimeSpan.FromSeconds(_YellDuration));
                                    }

                                    int points = FetchPoints(aPlayer, false, true);
                                    if (_useFirstSpawnRepMessage)
                                    {
                                        Boolean isAdmin = PlayerIsAdmin(aPlayer);
                                        String repMessage = "Your reputation is " + Math.Round(aPlayer.player_reputation, 2) + ", with ";
                                        if (points > 0)
                                        {
                                            repMessage += points + " infraction point(s). ";
                                        }
                                        else
                                        {
                                            repMessage += "a clean infraction record. ";
                                        }
                                        PlayerTellMessage(aPlayer.player_name, repMessage);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.HandleException(new AException("Error while printing spawn messages", e));
                                }
                                Log.Debug(() => "Exiting a spawn printer.", 5);
                                Threading.StopWatchdog();
                            }));

                            //Start the thread
                            Threading.StartWatchdog(spawnPrinter);
                        }
                    }
                    aPlayer.player_spawnedOnce = true;

                    if (_ActOnSpawnDictionary.Count > 0)
                    {
                        lock (_ActOnSpawnDictionary)
                        {
                            ARecord record;
                            if (_ActOnSpawnDictionary.TryGetValue(soldierName, out record))
                            {
                                //Remove it from the dic
                                _ActOnSpawnDictionary.Remove(soldierName);
                                //Wait 1.5 seconds to take action (no "killed by admin" message in BF3 without this wait)
                                Threading.Wait(1500);
                                //Queue the action
                                QueueRecordForActionHandling(record);
                            }
                        }
                    }

                    if (_AutomaticForgives &&
                        aPlayer.player_infractionPoints > 0 &&
                        aPlayer.LastPunishment != null &&
                        (UtcNow() - aPlayer.LastPunishment.record_time).TotalDays > _AutomaticForgiveLastPunishDays &&
                        (aPlayer.LastForgive == null || (UtcNow() - aPlayer.LastForgive.record_time).TotalDays > _AutomaticForgiveLastForgiveDays))
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_forgive"),
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "InfractionManager",
                            record_message = "Auto-Forgiven for Clean Play",
                            record_time = UtcNow()
                        });
                    }

                    //Auto-Nuke Slay Duration
                    var duration = NowDuration(_lastNukeTime);
                    if (duration.TotalSeconds < _nukeAutoSlayActiveDuration &&
                        _lastNukeTeam != null &&
                        aPlayer.fbpInfo.TeamID == _lastNukeTeam.TeamID)
                    {
                        var endDuration = NowDuration(_lastNukeTime.AddSeconds(_nukeAutoSlayActiveDuration));
                        var durationRounded = Math.Round(endDuration.TotalSeconds, 1);
                        if (durationRounded > 0)
                        {
                            PlayerTellMessage(aPlayer.player_name, _lastNukeTeam.TeamKey + " nuke active for " + Math.Round(endDuration.TotalSeconds, 1) + " seconds!");
                            ExecuteCommand("procon.protected.send", "admin.killPlayer", aPlayer.player_name);
                        }
                    }

                    if (aPlayer.ActiveChallenge != null)
                    {
                        aPlayer.ActiveChallenge.AddSpawn(aPlayer);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player spawn.", e));
            }
            Log.Debug(() => "Exiting OnPlayerSpawned", 7);
        }

        public override void OnPlayerJoin(string soldierName)
        {
            Log.Debug(() => "Entering OnPlayerJoin", 7);
            try
            {
                if (_pluginEnabled &&
                    _firstPlayerListComplete &&
                    GameVersion == GameVersionEnum.BF4 &&
                    !String.IsNullOrEmpty(_vipKickedPlayerName))
                {
                    var matchingPlayer = GetFetchedPlayers().FirstOrDefault(aPlayer => aPlayer.player_name == soldierName);
                    if (matchingPlayer != null)
                    {
                        OnlineAdminSayMessage(_vipKickedPlayerName + " kicked for VIP " + matchingPlayer.GetVerboseName() + " to join.");
                    }
                    else
                    {
                        OnlineAdminSayMessage(_vipKickedPlayerName + " kicked for VIP " + soldierName + " to join.");
                    }
                    _vipKickedPlayerName = null;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player join.", e));
            }
            Log.Debug(() => "Exiting OnPlayerJoin", 7);
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            Log.Debug(() => "Entering OnPlayerLeft", 7);
            try
            {
                QueuePlayerForRemoval(playerInfo);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player left.", e));
            }
            Log.Debug(() => "Exiting OnPlayerLeft", 7);
        }

        public override void OnPlayerDisconnected(string soldierName, string reason)
        {
            Log.Debug(() => "Entering OnPlayerDisconnected", 7);
            try
            {
                if (_pluginEnabled &&
                    _firstPlayerListComplete &&
                    GameVersion == GameVersionEnum.BF4 &&
                    reason == "PLAYER_KICKED")
                {
                    var matchingPlayer = GetFetchedPlayers().FirstOrDefault(aPlayer => aPlayer.player_name == soldierName);
                    if (matchingPlayer != null)
                    {
                        _vipKickedPlayerName = matchingPlayer.GetVerboseName();
                    }
                    else
                    {
                        _vipKickedPlayerName = soldierName;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling player disconnected.", e));
            }
            Log.Debug(() => "Exiting OnPlayerDisconnected", 7);
        }

        private void QueueSettingImport(Int32 serverID)
        {
            Log.Debug(() => "Entering queueSettingImport", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue server ID for setting import", 6);
                    _settingImportID = serverID;
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while preparing to import settings.", e));
            }
            Log.Debug(() => "Exiting queueSettingImport", 7);
        }

        private void QueueSettingForUpload(CPluginVariable setting)
        {
            Log.Debug(() => "Entering queueSettingForUpload", 7);
            if (!_settingsFetched)
            {
                return;
            }
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue setting " + setting.Name + " for upload", 6);
                    lock (_SettingUploadQueue)
                    {
                        _SettingUploadQueue.Enqueue(setting);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing setting for upload.", e));
            }
            Log.Debug(() => "Exiting queueSettingForUpload", 7);
        }

        private void QueueCommandForUpload(ACommand command)
        {
            Log.Debug(() => "Entering queueCommandForUpload", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue command " + command.command_key + " for upload", 6);
                    lock (_CommandUploadQueue)
                    {
                        _CommandUploadQueue.Enqueue(command);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing command for upload.", e));
            }
            Log.Debug(() => "Exiting queueCommandForUpload", 7);
        }

        private void QueueRoleForUpload(ARole aRole)
        {
            Log.Debug(() => "Entering queueRoleForUpload", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue role " + aRole.role_key + " for upload", 6);
                    lock (_RoleUploadQueue)
                    {
                        _RoleUploadQueue.Enqueue(aRole);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing role for upload.", e));
            }
            Log.Debug(() => "Exiting queueRoleForUpload", 7);
        }

        private void QueueRoleForRemoval(ARole aRole)
        {
            Log.Debug(() => "Entering queueRoleForRemoval", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue role " + aRole.role_key + " for removal", 6);
                    lock (_RoleRemovalQueue)
                    {
                        _RoleRemovalQueue.Enqueue(aRole);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing role for removal.", e));
            }
            Log.Debug(() => "Exiting queueRoleForRemoval", 7);
        }

        private void QueuePlayerForBanCheck(APlayer player)
        {
            Log.Debug(() => "Entering queuePlayerForBanCheck", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue player for ban check", 6);
                    lock (_BanEnforcerCheckingQueue)
                    {
                        _BanEnforcerCheckingQueue.Enqueue(player);
                        Log.Debug(() => "Player queued for checking", 6);
                        _BanEnforcerWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing player for ban check.", e));
            }
            Log.Debug(() => "Exiting queuePlayerForBanCheck", 7);
        }

        private void QueueBanForProcessing(ABan aBan)
        {
            Log.Debug(() => "Entering queueBanForProcessing", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue ban for processing", 6);
                    lock (_BanEnforcerProcessingQueue)
                    {
                        _BanEnforcerProcessingQueue.Enqueue(aBan);
                        Log.Debug(() => "Ban queued for processing", 6);
                        _DbCommunicationWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing ban for processing.", e));
            }
            Log.Debug(() => "Exiting queueBanForProcessing", 7);
        }


        // ===========================================================================================
        // Action Handling (lines 30796-39896 from original AdKats.cs)
        // ===========================================================================================

        private void ActionHandlingThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Action Thread", 1);
                Thread.CurrentThread.Name = "ActionHandling";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering Action Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        //Sleep for 10ms
                        Threading.Wait(10);

                        //Handle Inbound Actions
                        if (_UnprocessedActionQueue.Count > 0)
                        {
                            Queue<ARecord> unprocessedActions;
                            lock (_UnprocessedActionQueue)
                            {
                                Log.Debug(() => "Inbound actions found. Grabbing.", 6);
                                //Grab all messages in the queue
                                unprocessedActions = new Queue<ARecord>(_UnprocessedActionQueue.ToArray());
                                //Clear the queue for next run
                                _UnprocessedActionQueue.Clear();
                            }
                            //Loop through all records in order that they came in
                            while (unprocessedActions.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                Log.Debug(() => "Preparing to Run Actions for record", 6);
                                //Dequeue the record
                                ARecord record = unprocessedActions.Dequeue();
                                //Run the appropriate action
                                RunAction(record);
                                //If more processing is needed, then perform it
                                //If any errors exist in the record, do not re-queue
                                if (record.record_exception == null)
                                {
                                    QueueRecordForProcessing(record);
                                }
                                else
                                {
                                    Log.Debug(() => "Record has errors, not re-queueing after action.", 3);
                                }
                            }
                        }
                        else
                        {
                            Log.Debug(() => "No inbound actions. Waiting.", 6);
                            //Wait for new actions
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _ActionHandlingWaitHandle.Reset();
                            _ActionHandlingWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                            loopStart = UtcNow();
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("Action Handling thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in Action Handling thread. Skipping current loop.", e));
                    }
                }
                Log.Debug(() => "Ending Action Handling Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in action handling thread.", e));
            }
        }

        private void RunAction(ARecord record)
        {
            Log.Debug(() => "Entering runAction", 6);
            try
            {
                //Make sure record has an action
                if (record.command_action == null)
                {
                    record.command_action = record.command_type;
                }
                //Automatic player locking
                if (!record.record_action_executed &&
                    record.target_player != null &&
                    record.source_name != record.target_name &&
                    (record.source_player == null || PlayerIsAdmin(record.source_player)) &&
                    _playerLockingAutomaticLock &&
                    !record.target_player.IsLocked())
                {
                    record.target_player.Lock(record.source_name, TimeSpan.FromMinutes(_playerLockingAutomaticDuration));
                }
                //Perform Actions
                switch (record.command_action.command_key)
                {
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
                    case "self_assist_unconfirmed":
                        AssistWeakTeam(record);
                        break;
                    case "player_debugassist":
                        DubugAssistWeakTeam(record);
                        break;
                    case "self_kill":
                        ForceKillTarget(record);
                        break;
                    case "player_kill":
                        KillTarget(record);
                        break;
                    case "player_kill_force":
                        ForceKillTarget(record);
                        break;
                    case "player_warn":
                        WarnTarget(record);
                        break;
                    case "player_kill_lowpop":
                        KillTarget(record);
                        break;
                    case "player_kill_repeat":
                        KillTarget(record);
                        break;
                    case "player_kick":
                        KickTarget(record);
                        break;
                    case "player_ban_temp":
                        TempBanTarget(record);
                        break;
                    case "player_ban_perm":
                        PermaBanTarget(record);
                        break;
                    case "player_ban_perm_future":
                        FuturePermaBanTarget(record);
                        break;
                    case "player_unban":
                        UnBanTarget(record);
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
                    case "player_persistentmute":
                        PersistentMuteTarget(record, false);
                        break;
                    case "player_persistentmute_force":
                        PersistentMuteTarget(record, true);
                        break;
                    case "player_unmute":
                    case "player_persistentmute_remove":
                        UnMuteTarget(record);
                        break;
                    case "player_join":
                        JoinTarget(record);
                        break;
                    case "player_pull":
                        PullTarget(record);
                        break;
                    case "player_report":
                        ReportTarget(record);
                        break;
                    case "player_calladmin":
                        CallAdminOnTarget(record);
                        break;
                    case "player_info":
                        SendTargetInfo(record);
                        break;
                    case "player_perks":
                        SendTargetPerks(record);
                        break;
                    case "poll_trigger":
                        TriggerTargetPoll(record);
                        break;
                    case "player_chat":
                        SendTargetChat(record);
                        break;
                    case "player_find":
                        FindTarget(record);
                        break;
                    case "player_lock":
                        LockTarget(record);
                        break;
                    case "player_unlock":
                        UnlockTarget(record);
                        break;
                    case "player_mark":
                        MarkTarget(record);
                        break;
                    case "player_loadout":
                        LoadoutFetchTarget(record);
                        break;
                    case "player_loadout_force":
                        LoadoutForceTarget(record);
                        break;
                    case "player_loadout_ignore":
                        LoadoutIgnoreTarget(record);
                        break;
                    case "player_ping":
                        PingFetchTarget(record);
                        break;
                    case "player_forceping":
                        ForcePingTarget(record);
                        break;
                    case "server_afk":
                        ManageAFKPlayers(record);
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
                    case "server_countdown":
                        CountdownTarget(record);
                        break;
                    case "server_kickall":
                        KickAllPlayers(record);
                        break;
                    case "server_swapnuke":
                        SwapNukeServer(record);
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
                    case "player_pm_send":
                        PMSendTarget(record);
                        break;
                    case "player_pm_reply":
                        PMReplyTarget(record);
                        break;
                    case "player_pm_start":
                        PMStartTarget(record);
                        break;
                    case "player_pm_cancel":
                        PMCancelTarget(record);
                        break;
                    case "player_pm_transmit":
                        PMTransmitTarget(record);
                        break;
                    case "admin_pm_send":
                        PMOnlineAdmins(record);
                        break;
                    case "player_dequeue":
                        DequeueTarget(record);
                        break;
                    case "player_blacklistdisperse":
                        BalanceDisperseTarget(record);
                        break;
                    case "player_whitelistbalance":
                        BalanceWhitelistTarget(record);
                        break;
                    case "player_whitelistbf4db":
                        BF4DBWhitelistTarget(record);
                        break;
                    case "player_whitelistba":
                        BAWhitelistTarget(record);
                        break;
                    case "player_slotreserved":
                        ReservedSlotTarget(record);
                        break;
                    case "player_slotspectator":
                        SpectatorSlotTarget(record);
                        break;
                    case "player_whitelistanticheat":
                        AntiCheatWhitelistTarget(record);
                        break;
                    case "player_whitelistping":
                        PingWhitelistTarget(record);
                        break;
                    case "player_whitelistaa":
                        AAWhitelistTarget(record);
                        break;
                    case "player_whitelistreport":
                        ReportWhitelistTarget(record);
                        break;
                    case "player_whitelistspambot":
                        SpamBotWhitelistTarget(record);
                        break;
                    case "player_whitelistspambot_remove":
                        SpamBotWhitelistRemoveTarget(record);
                        break;
                    case "player_blacklistspectator":
                        SpectatorBlacklistTarget(record);
                        break;
                    case "player_blacklistspectator_remove":
                        SpectatorBlacklistRemoveTarget(record);
                        break;
                    case "player_blacklistreport":
                        ReportSourceBlacklistTarget(record);
                        break;
                    case "player_blacklistreport_remove":
                        ReportSourceBlacklistRemoveTarget(record);
                        break;
                    case "player_challenge_play":
                        ChallengePlayTarget(record);
                        break;
                    case "player_challenge_play_remove":
                        ChallengePlayRemoveTarget(record);
                        break;
                    case "player_challenge_ignore":
                        ChallengeIgnoreTarget(record);
                        break;
                    case "player_challenge_ignore_remove":
                        ChallengeIgnoreRemoveTarget(record);
                        break;
                    case "player_challenge_autokill":
                        ChallengeAutoKillTarget(record);
                        break;
                    case "player_challenge_autokill_remove":
                        ChallengeAutoKillRemoveTarget(record);
                        break;
                    case "player_whitelistcommand":
                        CommandTargetWhitelistTarget(record);
                        break;
                    case "player_whitelistcommand_remove":
                        CommandTargetWhitelistRemoveTarget(record);
                        break;
                    case "player_blacklistautoassist":
                        AutoAssistBlacklistTarget(record);
                        break;
                    case "player_blacklistautoassist_remove":
                        AutoAssistBlacklistRemoveTarget(record);
                        break;
                    case "player_whitelistreport_remove":
                        ReportWhitelistRemoveTarget(record);
                        break;
                    case "player_whitelistaa_remove":
                        AAWhitelistRemoveTarget(record);
                        break;
                    case "player_whitelistping_remove":
                        PingWhitelistRemoveTarget(record);
                        break;
                    case "player_whitelistanticheat_remove":
                        AntiCheatWhitelistRemoveTarget(record);
                        break;
                    case "player_slotspectator_remove":
                        SpectatorSlotRemoveTarget(record);
                        break;
                    case "player_slotreserved_remove":
                        ReservedSlotRemoveTarget(record);
                        break;
                    case "player_whitelistbalance_remove":
                        BalanceWhitelistRemoveTarget(record);
                        break;
                    case "player_blacklistdisperse_remove":
                        BalanceDisperseRemoveTarget(record);
                        break;
                    case "player_whitelistbf4db_remove":
                        BF4DBWhitelistRemoveTarget(record);
                        break;
                    case "player_whitelistba_remove":
                        BAWhitelistRemoveTarget(record);
                        break;
                    case "player_whitelistpopulator":
                        PopulatorWhitelistTarget(record);
                        break;
                    case "player_whitelistpopulator_remove":
                        PopulatorWhitelistRemoveTarget(record);
                        break;
                    case "player_whitelistteamkill":
                        TeamKillTrackerWhitelistTarget(record);
                        break;
                    case "player_whitelistteamkill_remove":
                        TeamKillTrackerWhitelistRemoveTarget(record);
                        break;
                    case "player_watchlist":
                        WatchlistTarget(record);
                        break;
                    case "player_watchlist_remove":
                        WatchlistRemoveTarget(record);
                        break;
                    case "player_whitelistmoveprotection":
                        MoveProtectionWhitelistTarget(record);
                        break;
                    case "player_whitelistmoveprotection_remove":
                        MoveProtectionWhitelistRemoveTarget(record);
                        break;
                    case "player_log":
                        SendMessageToSource(record, "Log saved for " + record.GetTargetNames());
                        break;
                    case "self_feedback":
                        SendMessageToSource(record, "Feedback saved.");
                        break;
                    case "player_population_success":
                        SendPopulationSuccess(record);
                        break;
                    case "self_rules":
                        SendServerRules(record);
                        break;
                    case "self_surrender":
                        SourceVoteSurrender(record);
                        break;
                    case "self_nosurrender":
                        SourceVoteNoSurrender(record);
                        break;
                    case "self_votenext":
                        SourceVoteSurrender(record);
                        break;
                    case "self_help":
                        SendServerCommands(record);
                        break;
                    case "self_rep":
                        SendTargetRep(record);
                        break;
                    case "player_isadmin":
                        SendTargetIsAdmin(record);
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
                    case "self_reportlist":
                        SendPlayerReports(record);
                        break;
                    case "plugin_restart":
                        RebootPlugin(record);
                        break;
                    case "plugin_update":
                        UpdatePlugin(record);
                        break;
                    case "server_shutdown":
                        ShutdownServer(record);
                        break;
                    case "adkats_exception":
                        record.record_action_executed = true;
                        break;
                    case "self_battlecry":
                    case "player_battlecry":
                        UpdatePlayerBattlecry(record);
                        break;
                    case "player_discordlink":
                        UpdatePlayerDiscordLink(record);
                        break;
                    case "player_blacklistallcaps":
                        AllCapsBlacklistTarget(record);
                        break;
                    case "player_blacklistallcaps_remove":
                        AllCapsBlacklistRemoveTarget(record);
                        break;
                    case "self_challenge":
                        SendChallengeInfo(record);
                        break;
                    case "player_language_punish":
                        LanguagePunishTarget(record);
                        break;
                    case "player_language_reset":
                        LanguageResetTarget(record);
                        break;
                    case "player_changename":
                    case "player_changetag":
                    case "player_changeip":
                    case "player_challenge_complete":
                    case "admin_accept":
                    case "admin_deny":
                    case "admin_ignore":
                    case "self_contest":
                    case "banenforcer_enforce":
                    case "player_repboost":
                    case "server_map_detriment":
                    case "server_map_benefit":
                    case "poll_vote":
                    case "poll_cancel":
                    case "poll_complete":
                        record.record_action_executed = true;
                        //Don't do anything here
                        break;
                    default:
                        record.record_action_executed = true;
                        SendMessageToSource(record, "Command not recognized when running " + record.command_action.command_key + " action.");
                        record.record_exception = Log.HandleException(new AException("Command " + record.command_action + " not found in runAction. Record ID " + record.record_id));
                        FinalizeRecord(record);
                        break;
                }
                Log.Debug(() => record.command_type.command_key + " last used " + FormatTimeString(UtcNow() - _commandUsageTimes[record.command_type.command_key], 10) + " ago.", 3);
                _commandUsageTimes[record.command_type.command_key] = UtcNow();
            }
            catch (Exception e)
            {
                record.record_exception = Log.HandleException(new AException("Error while choosing action for record.", e));
            }
            Log.Debug(() => "Exiting runAction", 6);
        }

        public void MoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering moveTarget", 6);
            try
            {
                record.record_action_executed = true;
                if (GameVersion != GameVersionEnum.BF3 && !record.isAliveChecked)
                {
                    if (!_ActOnIsAliveDictionary.ContainsKey(record.target_player.player_name))
                    {
                        lock (_ActOnIsAliveDictionary)
                        {
                            _ActOnIsAliveDictionary.Add(record.target_player.player_name, record);
                        }
                    }
                    ExecuteCommand("procon.protected.send", "player.isAlive", record.target_name);
                    return;
                }

                QueuePlayerForMove(record.target_player.fbpInfo);
                record.target_player.Say("On your next death you will be moved to the opposing team.");
                SendMessageToSource(record, Log.CViolet(record.GetTargetNames() + " will be sent to TeamSwap on their next death."));
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for move record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting moveTarget", 6);
        }

        public void ForceMoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering forceMoveTarget", 6);
            String message = null;
            try
            {
                record.record_action_executed = true;
                if (record.command_type == GetCommandByKey("self_teamswap"))
                {
                    if ((record.source_player != null && HasAccess(record.source_player, GetCommandByKey("self_teamswap"))) || ((_TeamSwapTicketWindowHigh >= _highestTicketCount) && (_TeamSwapTicketWindowLow <= _lowestTicketCount)))
                    {
                        message = "Calling Teamswap on self";
                        Log.Debug(() => message, 6);
                        QueuePlayerForForceMove(record.target_player.fbpInfo);
                    }
                    else
                    {
                        message = "Player unable to TeamSwap";
                        Log.Debug(() => message, 6);
                        SendMessageToSource(record, "You cannot TeamSwap at this time. Game outside ticket window [" + _TeamSwapTicketWindowLow + ", " + _TeamSwapTicketWindowHigh + "].");
                    }
                }
                else
                {
                    message = "TeamSwap called on " + record.GetTargetNames();
                    Log.Debug(() => "Calling Teamswap on target", 6);
                    SendMessageToSource(record, Log.CViolet(record.GetTargetNames() + " sent to TeamSwap."));
                    QueuePlayerForForceMove(record.target_player.fbpInfo);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for force-move/teamswap record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting forceMoveTarget", 6);
        }

        public void AssistWeakTeam(ARecord record)
        {
            Log.Debug(() => "Entering AssistLosingTeam", 6);
            try
            {
                record.record_action_executed = true;

                if (record.source_name == record.target_name)
                {
                    _roundAssists[record.target_player.player_name] = record;
                }
                QueuePlayerForForceMove(record.target_player.fbpInfo);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for assist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AssistLosingTeam", 6);
        }

        public void DubugAssistWeakTeam(ARecord record)
        {
            Log.Debug(() => "Entering DubugAssistWeakTeam", 6);
            try
            {
                record.record_action_executed = true;

                RunAssist(record.target_player, null, record, false);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for debug assist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting DubugAssistWeakTeam", 6);
        }

        public void KillTarget(ARecord record)
        {
            Log.Debug(() => "Entering killTarget", 6);
            try
            {
                record.record_action_executed = true;
                if (record.source_name != record.target_name)
                {
                    switch (GameVersion)
                    {
                        case GameVersionEnum.BF3:
                            if (record.command_type.command_key == "player_punish")
                            {
                                if (record.source_name == "AutoAdmin" || record.source_name == "ProconAdmin")
                                {
                                    AdminSayMessage(Log.FBold(Log.CRed("Punishing " + record.GetTargetNames() + " for " + record.record_message)));
                                }
                                else
                                {
                                    AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " PUNISHED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                                }
                            }
                            else if (record.source_name != "PlayerMuteSystem")
                            {
                                AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " KILLED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                            }
                            int seconds = (int)UtcNow().Subtract(record.target_player.lastDeath).TotalSeconds;
                            Log.Debug(() => "Killing player. Player last died " + seconds + " seconds ago.", 3);
                            if (seconds < 6 && record.command_action.command_key != "player_kill_repeat")
                            {
                                Log.Debug(() => "Queueing player for kill on spawn. (" + seconds + ")&(" + record.command_action + ")", 3);
                                if (!_ActOnSpawnDictionary.ContainsKey(record.target_player.player_name))
                                {
                                    lock (_ActOnSpawnDictionary)
                                    {
                                        record.command_action = GetCommandByKey("player_kill_repeat");
                                        _ActOnSpawnDictionary.Add(record.target_player.player_name, record);
                                    }
                                }
                            }
                            break;
                        case GameVersionEnum.BF4:
                        case GameVersionEnum.BFHL:
                            if (!record.isAliveChecked)
                            {
                                if (record.command_type.command_key == "player_punish")
                                {
                                    if (record.source_name == "AutoAdmin" || record.source_name == "ProconAdmin")
                                    {
                                        AdminSayMessage(Log.FBold(Log.CRed("Punishing " + record.GetTargetNames() + " for " + record.record_message)));
                                    }
                                    else
                                    {
                                        AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " PUNISHED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                                    }
                                }
                                else if (record.source_name != "PlayerMuteSystem")
                                {
                                    AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " KILLED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                                }
                                if (!_ActOnIsAliveDictionary.ContainsKey(record.target_player.player_name))
                                {
                                    lock (_ActOnIsAliveDictionary)
                                    {
                                        _ActOnIsAliveDictionary.Add(record.target_player.player_name, record);
                                    }
                                }
                                ExecuteCommand("procon.protected.send", "player.isAlive", record.target_name);
                                return;
                            }
                            break;
                        default:
                            Log.Error("Invalid game version in killtarget");
                            return;
                    }
                }

                //Perform actions
                if (String.IsNullOrEmpty(record.target_player.player_name))
                {
                    Log.Error("playername null in 5437");
                }
                else
                {
                    ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_player.player_name);
                    if (record.source_name != record.target_name || record.command_type.command_key == "player_punish")
                    {
                        PlayerTellMessage(record.target_name, "KILLED by " + (record.source_name == "AutoAdmin" ? "AutoAdmin" : "admin") + " for " + record.record_message);
                        SendMessageToSource(record, "You KILLED " + record.GetTargetNames() + " for " + record.record_message);
                    }
                    else
                    {
                        PlayerTellMessage(record.target_name, "You killed yourself");
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for kill record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting killTarget", 6);
        }

        public void ForceKillTarget(ARecord record)
        {
            Log.Debug(() => "Entering ForceKillTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Perform actions
                if (String.IsNullOrEmpty(record.target_player.player_name))
                {
                    Log.Error("playername null in 14491");
                }
                else
                {
                    ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_player.player_name);
                    if (record.source_name != record.target_name)
                    {
                        PlayerTellMessage(record.target_name, "KILLED by " + (record.source_name == "AutoAdmin" ? "AutoAdmin" : "admin") + " for " + record.record_message);
                        SendMessageToSource(record, "You KILLED " + record.GetTargetNames() + " for " + record.record_message);
                    }
                    else
                    {
                        PlayerTellMessage(record.target_name, "You killed yourself");
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for kill record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ForceKillTarget", 6);
        }

        public void WarnTarget(ARecord record)
        {
            Log.Debug(() => "Entering WarnTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Perform actions
                if (String.IsNullOrEmpty(record.target_player.player_name))
                {
                    Log.Error("playername null in 14526");
                }
                else
                {
                    if (record.record_source != ARecord.Sources.InGame &&
                        record.record_source != ARecord.Sources.Automated &&
                        record.record_source != ARecord.Sources.ServerCommand)
                    {
                        SendMessageToSource(record, "You WARNED " + record.GetTargetNames() + " for " + record.record_message);
                    }
                    AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " WARNED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                    PlayerTellMessage(record.target_name, "Warned for " + record.record_message, true, 3);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for warn record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting WarnTarget", 6);
        }

        public void DequeueTarget(ARecord record)
        {
            Log.Debug(() => "Entering DequeueTarget", 6);
            try
            {
                record.record_action_executed = true;
                if (record.target_player != null)
                {
                    DequeuePlayer(record.target_player);
                    record.target_player.Say("All queued actions canceled.");
                    SendMessageToSource(record, "All queued actions for " + record.GetTargetNames() + " canceled.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for dequeue record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting DequeueTarget", 6);
        }

        public void DequeuePlayer(APlayer aPlayer)
        {
            Log.Debug(() => "Entering DequeuePlayer", 6);
            try
            {
                //Handle spawn action
                if (_ActOnSpawnDictionary.ContainsKey(aPlayer.player_name))
                {
                    _ActOnSpawnDictionary.Remove(aPlayer.player_name);
                }
                //Handle teamswap action
                lock (_Team1MoveQueue)
                {
                    CPlayerInfo info = _Team1MoveQueue.FirstOrDefault(playerInfo => playerInfo.SoldierName == aPlayer.player_name);
                    if (info != null)
                    {
                        _Team1MoveQueue = new Queue<CPlayerInfo>(_Team1MoveQueue.Where(p => p != info));
                    }
                }
                lock (_Team2MoveQueue)
                {
                    CPlayerInfo info = _Team2MoveQueue.FirstOrDefault(playerInfo => playerInfo.SoldierName == aPlayer.player_name);
                    if (info != null)
                    {
                        _Team2MoveQueue = new Queue<CPlayerInfo>(_Team2MoveQueue.Where(p => p != info));
                    }
                }
                lock (_TeamswapForceMoveQueue)
                {
                    CPlayerInfo info = _TeamswapForceMoveQueue.FirstOrDefault(playerInfo => playerInfo.SoldierName == aPlayer.player_name);
                    if (info != null)
                    {
                        _TeamswapForceMoveQueue = new Queue<CPlayerInfo>(_TeamswapForceMoveQueue.Where(p => p != info));
                    }
                }
                lock (_TeamswapOnDeathCheckingQueue)
                {
                    CPlayerInfo info = _TeamswapOnDeathCheckingQueue.FirstOrDefault(playerInfo => playerInfo.SoldierName == aPlayer.player_name);
                    if (info != null)
                    {
                        _TeamswapOnDeathCheckingQueue = new Queue<CPlayerInfo>(_TeamswapOnDeathCheckingQueue.Where(p => p != info));
                    }
                }
                lock (_AssistAttemptQueue)
                {
                    var record = _AssistAttemptQueue.FirstOrDefault(dRecord => dRecord.target_name == aPlayer.player_name);
                    if (record != null)
                    {
                        _AssistAttemptQueue = new Queue<ARecord>(_AssistAttemptQueue.Where(p => p != record));
                    }
                }
                if (_TeamswapOnDeathMoveDic.ContainsKey(aPlayer.player_name))
                {
                    _TeamswapOnDeathMoveDic.Remove(aPlayer.player_name);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while dequeuing player.", e));
            }
            Log.Debug(() => "Exiting DequeuePlayer", 6);
        }

        public void KickTarget(ARecord record)
        {
            Log.Debug(() => "Entering kickTarget", 6);
            try
            {
                record.record_action_executed = true;
                String kickReason = GenerateKickReason(record);
                //Perform Actions
                Log.Debug(() => "Kick '" + kickReason + "'", 3);
                if (String.IsNullOrEmpty(record.target_player.player_name) || String.IsNullOrEmpty(kickReason))
                {
                    Log.Error("Item null in 5464");
                }
                else
                {
                    if (record.target_name != record.source_name)
                    {
                        if (record.source_name == "PingEnforcer")
                        {
                            AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " KICKED for " + ((record.target_player.player_ping_avg > 0) ? (Math.Round(record.target_player.player_ping) + "ms ping. Avg:" + Math.Round(record.target_player.player_ping_avg) + "ms") : ("missing ping.")))));
                        }
                        else if (record.source_name != "AFKManager" && record.source_name != "SpectatorManager")
                        {
                            AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " KICKED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                        }
                    }
                    if (record.target_player.fbpInfo != null)
                    {
                        SendMessageToSource(record, "You KICKED " + record.GetTargetNames() + " from " + GetPlayerTeamName(record.target_player) + " for " + record.record_message);
                    }
                    else
                    {
                        SendMessageToSource(record, "You KICKED " + record.GetTargetNames() + " for " + record.record_message);
                    }
                    if (record.target_name != record.source_name)
                    {
                        KickPlayerMessage(record.target_player, kickReason);
                    }
                    else
                    {
                        // Don't have any delay if the kick is self targeted
                        KickPlayerMessage(record.target_player.player_name, kickReason, 0);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for kick record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting kickTarget", 6);
        }

        public void TempBanTarget(ARecord record)
        {
            Log.Debug(() => "Entering tempBanTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Subtract 1 second for visual effect
                Int32 seconds = (record.command_numeric * 60) - 1;

                //Perform Actions
                //Only post to ban enforcer if there are no exceptions
                if (_UseBanEnforcer && record.record_exception == null)
                {
                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                    //Create the ban
                    ABan aBan = new ABan
                    {
                        ban_record = record,
                        ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable)),
                        ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable)),
                        ban_enforceIP = ipAvailable && (_DefaultEnforceIP || (!nameAvailable && !guidAvailable))
                    };

                    //Queue the ban for upload
                    QueueBanForProcessing(aBan);
                }
                else
                {
                    if (record.record_exception != null)
                    {
                        Log.HandleException(new AException("Defaulting to procon banlist usage since exceptions existed in record"));
                    }
                    //Trim the ban message if necessary
                    String banMessage = record.record_message + " [" + record.source_name + "]";
                    Int32 cutLength = banMessage.Length - 80;
                    if (cutLength > 0)
                    {
                        banMessage = banMessage.Substring(0, banMessage.Length - cutLength);
                    }
                    Log.Debug(() => "Ban '" + banMessage + "'", 3);
                    if (!String.IsNullOrEmpty(record.target_player.player_guid))
                    {
                        ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_player.player_guid, "seconds", seconds.ToString(), banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_ip))
                    {
                        ExecuteCommand("procon.protected.send", "banList.add", "ip", record.target_player.player_ip, "seconds", seconds.ToString(), banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_name))
                    {
                        ExecuteCommand("procon.protected.send", "banList.add", "id", record.target_player.player_name, "seconds", seconds.ToString(), banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else
                    {
                        Log.Error("Player has no information to ban with.");
                        SendMessageToSource(record, "ERROR");
                    }
                }
                if (record.target_name != record.source_name)
                {
                    AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " BANNED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                }
                SendMessageToSource(record, "You TEMP BANNED " + record.GetTargetNames() + " for " + FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 3));
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for TempBan record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting tempBanTarget", 6);
        }

        public void PermaBanTarget(ARecord record)
        {
            Log.Debug(() => "Entering permaBanTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Perform Actions
                //Only post to ban enforcer if there are no exceptions
                if (_UseBanEnforcer && record.record_exception == null)
                {
                    //Update the ban enforcement depending on available information
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);

                    //Create the ban
                    ABan aBan = new ABan
                    {
                        ban_record = record,
                        ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable)),
                        ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable)),
                        ban_enforceIP = ipAvailable && (_DefaultEnforceIP || (!nameAvailable && !guidAvailable))
                    };

                    //Queue the ban for upload
                    QueueBanForProcessing(aBan);
                }
                else
                {
                    if (record.record_exception != null)
                    {
                        Log.HandleException(new AException("Defaulting to procon banlist usage since exceptions existed in record"));
                    }
                    //Trim the ban message if necessary
                    String banMessage = record.record_message + " [" + record.source_name + "]";
                    Int32 cutLength = banMessage.Length - 80;
                    if (cutLength > 0)
                    {
                        banMessage = banMessage.Substring(0, banMessage.Length - cutLength);
                    }
                    Log.Debug(() => "Ban '" + banMessage + "'", 3);
                    if (!String.IsNullOrEmpty(record.target_player.player_guid))
                    {
                        ExecuteCommand("procon.protected.send", "banList.add", "guid", record.target_player.player_guid, "perm", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_ip))
                    {
                        ExecuteCommand("procon.protected.send", "banList.add", "ip", record.target_player.player_ip, "perm", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else if (!String.IsNullOrEmpty(record.target_player.player_name))
                    {
                        ExecuteCommand("procon.protected.send", "banList.add", "id", record.target_player.player_name, "perm", banMessage);
                        ExecuteCommand("procon.protected.send", "banList.save");
                        ExecuteCommand("procon.protected.send", "banList.list");
                    }
                    else
                    {
                        Log.Error("Player has no information to ban with.");
                        SendMessageToSource(record, "ERROR");
                    }
                }
                if (record.target_name != record.source_name)
                {
                    AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " BANNED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                }
                SendMessageToSource(record, "You PERMA BANNED " + record.GetTargetNames());
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for PermaBan record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting permaBanTarget", 6);
        }

        public void FuturePermaBanTarget(ARecord record)
        {
            Log.Debug(() => "Entering permaBanTarget", 6);
            try
            {
                record.record_action_executed = true;
                if (_UseBanEnforcer && record.record_exception == null)
                {
                    Boolean nameAvailable = !String.IsNullOrEmpty(record.target_player.player_name);
                    Boolean guidAvailable = !String.IsNullOrEmpty(record.target_player.player_guid);
                    Boolean ipAvailable = !String.IsNullOrEmpty(record.target_player.player_ip);
                    ABan aBan = new ABan
                    {
                        ban_record = record,
                        ban_enforceName = nameAvailable && (_DefaultEnforceName || (!guidAvailable && !ipAvailable)),
                        ban_enforceGUID = guidAvailable && (_DefaultEnforceGUID || (!nameAvailable && !ipAvailable)),
                        ban_enforceIP = ipAvailable && (_DefaultEnforceIP || (!nameAvailable && !guidAvailable))
                    };
                    QueueBanForProcessing(aBan);
                    DateTime endTime = record.record_time + TimeSpan.FromMinutes(record.command_numeric);
                    SendMessageToSource(record, "You FUTURE BANNED " + record.GetTargetNames() + ". Their ban will activate at " + endTime + " UTC.");
                }
                else
                {
                    SendMessageToSource(record, "Future ban cannot be posted.");
                    FinalizeRecord(record);
                    return;
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for PermaBan record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting permaBanTarget", 6);
        }

        public void UnBanTarget(ARecord record)
        {
            Log.Debug(() => "Entering UnBanTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Cancel call if not using ban enforcer
                if (!_UseBanEnforcer || !_UseBanEnforcerPreviousState)
                {
                    Log.Error("Attempted to issue unban when ban enforcer is disabled.");
                    return;
                }
                if (record.target_player == null)
                {
                    Log.Error("Player was null when attempting to unban.");
                    FinalizeRecord(record);
                    return;
                }
                List<ABan> banList = FetchPlayerBans(record.target_player);
                if (banList.Count == 0)
                {
                    FinalizeRecord(record);
                    return;
                }
                foreach (ABan aBan in banList)
                {
                    aBan.ban_status = "Disabled";
                    UpdateBanStatus(aBan);
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
                SendMessageToSource(record, record.GetTargetNames() + " is now unbanned.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for UnBan record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting UnBanTarget", 6);
        }

        public void EnforceBan(ABan aBan, Boolean verbose)
        {
            Log.Debug(() => "Entering enforceBan", 6);
            try
            {
                //Create the total kick message
                String generatedBanReason = GenerateBanReason(aBan);
                Log.Debug(() => "Ban Enforce '" + generatedBanReason + "'", 3);

                //Perform Actions
                aBan.ban_record.target_player.BanEnforceCount++;
                if (aBan.ban_record.target_name != aBan.ban_record.source_name)
                {
                    BanKickPlayerMessage(aBan.ban_record.target_player, generatedBanReason);
                }
                else
                {
                    // Don't have any delay if the ban is self targeted
                    BanKickPlayerMessage(aBan.ban_record.target_player, generatedBanReason, 0);
                }
                if (_PlayerDictionary.ContainsKey(aBan.ban_record.target_player.player_name) && aBan.ban_startTime < UtcNow())
                {
                    //Inform the server of the enforced ban
                    if (verbose)
                    {
                        String banDurationString;
                        //If ban time > 1000 days just say perm
                        TimeSpan remainingTime = GetRemainingBanTime(aBan);
                        TimeSpan totalTime = aBan.ban_endTime.Subtract(aBan.ban_startTime);
                        if (remainingTime.TotalDays > 500.0)
                        {
                            banDurationString = "permanent";
                        }
                        else
                        {
                            banDurationString = FormatTimeString(totalTime, 2) + " (" + FormatTimeString(remainingTime, 2) + ")";
                        }
                        AdminSayMessage(Log.FBold(Log.CRed("Enforcing " + banDurationString + " ban on " + aBan.ban_record.GetTargetNames() + " for " + aBan.ban_record.record_message)));
                    }
                }
            }
            catch (Exception e)
            {
                aBan.ban_exception = new AException("Error while enforcing ban.", e);
                Log.HandleException(aBan.ban_exception);
            }
            Log.Debug(() => "Exiting enforceBan", 6);
        }

        public void PunishTarget(ARecord record)
        {
            Log.Debug(() => "Entering PunishTarget", 6);
            try
            {
                record.record_action_executed = true;
                //If the record has any exceptions, skip everything else and just kill the player
                if (record.record_exception == null)
                {
                    //Get number of points the player from server
                    Int32 points = FetchPoints(record.target_player, false, true);
                    Log.Debug(() => record.GetTargetNames() + " has " + points + " points.", 5);
                    //Get the proper action to take for player punishment
                    String action = "noaction";
                    String skippedAction = null;
                    if (points > (_PunishmentHierarchy.Length - 1))
                    {
                        action = _PunishmentHierarchy[_PunishmentHierarchy.Length - 1];
                    }
                    else if (points > 1)
                    {
                        action = _PunishmentHierarchy[points - 1];
                        if (record.isIRO)
                        {
                            skippedAction = _PunishmentHierarchy[points - 2];
                        }
                    }
                    else
                    {
                        action = _PunishmentHierarchy[0];
                    }

                    //Handle the case where and IRO punish skips higher level punishment for a lower one, use the higher one
                    if (skippedAction != null && _PunishmentSeverityIndex.IndexOf(skippedAction) > _PunishmentSeverityIndex.IndexOf(action))
                    {
                        action = skippedAction;
                    }

                    //Set additional message
                    String pointMessage = " [" + ((record.isIRO) ? ("IRO ") : ("")) + points + "pts]";
                    if (!record.record_message.Contains(pointMessage))
                    {
                        record.record_message += pointMessage;
                    }

                    Boolean isLowPop = _OnlyKillOnLowPop && (GetPlayerCount() < _highPopulationPlayerCount);
                    Boolean iroOverride = record.isIRO && _IROOverridesLowPop && points >= _IROOverridesLowPopInfractions;

                    Log.Debug(() => "Server low population: " + isLowPop + " (" + GetPlayerCount() + " <? " + _highPopulationPlayerCount + ") | Override: " + iroOverride, 5);

                    //Call correct action
                    if (action == "repwarn")
                    {
                        record.command_action = GetCommandByKey("player_warn");
                        WarnTarget(record);
                        Threading.Wait(TimeSpan.FromSeconds(1));
                        PlayerTellMessage(record.target_name, "Your reputation protected you from a punish, but has been reduced. Inform an admin!", true, 3);
                    }
                    else if (action == "warn")
                    {
                        record.command_action = GetCommandByKey("player_warn");
                        WarnTarget(record);
                    }
                    else if ((action == "kill" || (isLowPop && !iroOverride)) && !action.Equals("ban"))
                    {
                        record.command_action = (isLowPop) ? (GetCommandByKey("player_kill_lowpop")) : (GetCommandByKey("player_kill"));
                        if (_subscribedClients.Any(client => client.ClientName == "AdKatsLRT" && client.SubscriptionEnabled))
                        {
                            ExecuteCommand("procon.protected.plugins.call", "AdKatsLRT", "CallLoadoutCheckOnPlayer", "AdKats", JSON.JsonEncode(new Hashtable {
                                {"caller_identity", "AdKats"},
                                {"response_requested", false},
                                {"player_name", record.target_player.player_name},
                                {"loadoutCheck_reason", "punished"}
                            }));
                        }
                        KillTarget(record);
                    }
                    else if (action == "kick")
                    {
                        record.command_action = GetCommandByKey("player_kick");
                        KickTarget(record);
                    }
                    else if (action == "tban60")
                    {
                        record.command_numeric = 60;
                        record.command_action = GetCommandByKey("player_ban_temp");
                        TempBanTarget(record);
                    }
                    else if (action == "tban120")
                    {
                        record.command_numeric = 120;
                        record.command_action = GetCommandByKey("player_ban_temp");
                        TempBanTarget(record);
                    }
                    else if (action == "tbanday")
                    {
                        record.command_numeric = 1440;
                        record.command_action = GetCommandByKey("player_ban_temp");
                        TempBanTarget(record);
                    }
                    else if (action == "tban2days")
                    {
                        record.command_numeric = 2880;
                        record.command_action = GetCommandByKey("player_ban_temp");
                        TempBanTarget(record);
                    }
                    else if (action == "tban3days")
                    {
                        record.command_numeric = 4320;
                        record.command_action = GetCommandByKey("player_ban_temp");
                        TempBanTarget(record);
                    }
                    else if (action == "tbanweek")
                    {
                        record.command_numeric = 10080;
                        record.command_action = GetCommandByKey("player_ban_temp");
                        TempBanTarget(record);
                    }
                    else if (action == "tban2weeks")
                    {
                        record.command_numeric = 20160;
                        record.command_action = GetCommandByKey("player_ban_temp");
                        TempBanTarget(record);
                    }
                    else if (action == "tbanmonth")
                    {
                        record.command_numeric = 43200;
                        record.command_action = GetCommandByKey("player_ban_temp");
                        TempBanTarget(record);
                    }
                    else if (action == "ban")
                    {
                        record.command_action = GetCommandByKey("player_ban_perm");
                        PermaBanTarget(record);
                    }
                    else
                    {
                        record.command_action = GetCommandByKey("player_kill");
                        if (_subscribedClients.Any(client => client.ClientName == "AdKatsLRT" && client.SubscriptionEnabled) &&
                            record.target_player != null &&
                            record.target_player.player_reputation <= 0 &&
                            record.target_player.player_online)
                        {
                            ExecuteCommand("procon.protected.plugins.call", "AdKatsLRT", "CallLoadoutCheckOnPlayer", "AdKats", JSON.JsonEncode(new Hashtable {
                                {"caller_identity", "AdKats"},
                                {"response_requested", false},
                                {"player_name", record.target_player.player_name},
                                {"loadoutCheck_reason", "punished"}
                            }));
                        }
                        KillTarget(record);
                        record.record_exception = new AException("Punish options are set incorrectly. '" + action + "' not found. Inform plugin setting manager.");
                        Log.HandleException(record.record_exception);
                    }
                    record.target_player.LastPunishment = record;
                }
                else
                {
                    //Exception found, just kill the player
                    record.command_action = GetCommandByKey("player_kill");
                    KillTarget(record);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Punish record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PunishTarget", 6);
        }

        public void ForgiveTarget(ARecord record)
        {
            Log.Debug(() => "Entering forgiveTarget", 6);
            try
            {
                record.record_action_executed = true;
                //If the record has any exceptions, skip everything
                if (record.record_exception == null)
                {
                    Int32 points = FetchPoints(record.target_player, false, true);
                    PlayerSayMessage(record.target_player.player_name, Log.CGreen("Forgiven 1 infraction point. You now have " + points + " point(s) against you."));
                    SendMessageToSource(record, "Forgive Logged for " + record.GetTargetNames() + ". They now have " + points + " infraction points.");
                    record.target_player.LastForgive = record;
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Forgive record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting forgiveTarget", 6);
        }

        public void BalanceDisperseTarget(ARecord record)
        {
            Log.Debug(() => "Entering DisperseTarget", 6);
            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_server`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_server,
                            @player_identifier,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "blacklist_dispersion");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_server", _serverInfo.ServerID);
                        command.Parameters.AddWithValue("@player_identifier", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " autobalance dispersion.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to even dispersion. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Disperse record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting DisperseTarget", 6);
        }

        public void AllCapsBlacklistTarget(ARecord record)
        {
            Log.Debug(() => "Entering AllCapsBlacklistTarget", 6);
            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_server`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_server,
                            @player_identifier,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "blacklist_allcaps");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_server", _serverInfo.ServerID);
                        command.Parameters.AddWithValue("@player_identifier", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " all-caps chat blacklist.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to all-caps chat blacklist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for all-caps chat blacklist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AllCapsBlacklistTarget", 6);
        }

        public void BalanceWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering BalanceWhitelistTarget", 6);
            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_server`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_server,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_multibalancer");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_server", _serverInfo.ServerID);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " autobalance whitelist.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to autobalance whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Balance Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting BalanceWhitelistTarget", 6);
        }

        public void BF4DBWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering BF4DBWhitelistTarget", 6);
            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_bf4db");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " BF4DB whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to BF4DB whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for BF4DB Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting BF4DBWhitelistTarget", 6);
        }

        // Welcome to redundant code part 90k.
        public void BAWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering BAWhitelistTarget", 6);
            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_ba");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " BattlefiedAgency whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to BattlefieldAgency whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for BattlefieldAgency Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting BattlefieldAgencyWhitelistTarget", 6);
        }

        public void ReservedSlotTarget(ARecord record)
        {
            Log.Debug(() => "Entering ReservedSlotTarget", 6);
            try
            {
                record.record_action_executed = true;

                // Thanks XTheLoneShadowX for this idea
                if (!_FeedServerReservedSlots && _FeedServerReservedSlots_VSM)
                {
                    var commandString = "/vsm-addvip " + record.target_player.player_name + " +" + Math.Round(TimeSpan.FromMinutes(record.command_numeric).TotalDays);
                    AdminSayMessage(commandString);
                    FetchAllAccess(true);
                }
                else
                {
                    using (MySqlConnection connection = GetDatabaseConnection())
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_server`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_server,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                            if (record.target_player.player_id <= 0)
                            {
                                Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                                SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                                FinalizeRecord(record);
                                return;
                            }
                            if (record.command_numeric > 10518984)
                            {
                                record.command_numeric = 10518984;
                            }
                            command.Parameters.AddWithValue("@player_group", "slot_reserved");
                            command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                            command.Parameters.AddWithValue("@player_server", _serverInfo.ServerID);
                            command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                            command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " reserved slot.";
                                SendMessageToSource(record, message);
                                Log.Debug(() => message, 3);
                                FetchAllAccess(true);
                            }
                            else
                            {
                                Log.Error("Unable to add player to reserved slot. Error uploading.");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Reserved Slot record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ReservedSlotTarget", 6);
        }

        public void SpectatorSlotTarget(ARecord record)
        {
            Log.Debug(() => "Entering SpectatorSlotTarget", 6);
            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_server`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_server,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "slot_spectator");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_server", _serverInfo.ServerID);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " spectator slot.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to spectator slot. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Spectator Slot record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SpectatorSlotTarget", 6);
        }

        public void AntiCheatWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering AntiCheatWhitelistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "AntiCheatWhitelistTarget not available for multiple targets.");
                    Log.Error("AntiCheatWhitelistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_anticheat");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " AntiCheat whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to AntiCheat whitelist. Error uploading.");
                        }
                    }
                }
                //Unban the player
                UnBanTarget(record);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for AntiCheat Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AntiCheatWhitelistTarget", 6);
        }

        public void PingWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering PingWhitelistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "PingWhitelistTarget not available for multiple targets.");
                    Log.Error("PingWhitelistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_ping");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " ping whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to ping whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Ping Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PingWhitelistTarget", 6);
        }

        public void AAWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering AAWhitelistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "AAWhitelistTarget not available for multiple targets.");
                    Log.Error("AAWhitelistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_adminassistant");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " admin assistant whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to Admin Assistant whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Admin Assistant Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AAWhitelistTarget", 6);
        }

        public void ReportWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering ReportWhitelistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ReportWhitelistTarget not available for multiple targets.");
                    Log.Error("ReportWhitelistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_report");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " report whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to Report whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Report Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ReportWhitelistTarget", 6);
        }

        public void SpamBotWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering SpamBotWhitelistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "SpamBotWhitelistTarget not available for multiple targets.");
                    Log.Error("SpamBotWhitelistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_spambot");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " spambot whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to SpamBot whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for SpamBot Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SpamBotWhitelistTarget", 6);
        }

        public void SpamBotWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering SpamBotWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "SpamBotWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("SpamBotWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_spambot", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the SpamBot whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from SpamBot whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from SpamBot whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from SpamBot whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SpamBotWhitelistRemoveTarget", 6);
        }

        public void SpectatorBlacklistTarget(ARecord record)
        {
            Log.Debug(() => "Entering SpectatorBlacklistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "SpectatorBlacklistTarget not available for multiple targets.");
                    Log.Error("SpectatorBlacklistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "blacklist_spectator");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " spectator blacklist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to spectator blacklist. Error uploading.");
                        }
                    }
                }
                //Kick target if they are currently spectating
                if (record.target_player.player_online && record.target_player.player_type == PlayerType.Spectator)
                {
                    QueueRecordForProcessing(new ARecord
                    {
                        record_source = ARecord.Sources.Automated,
                        server_id = _serverInfo.ServerID,
                        command_type = GetCommandByKey("player_kick"),
                        command_numeric = 0,
                        target_name = record.target_player.player_name,
                        target_player = record.target_player,
                        source_name = "SpectatorManager",
                        record_message = "You may not spectate at this time.",
                        record_time = UtcNow()
                    });
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for spectator blacklist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SpectatorBlacklistTarget", 6);
        }

        public void SpectatorBlacklistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering SpectatorBlacklistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "SpectatorBlacklistRemoveTarget not available for multiple targets.");
                    Log.Error("SpectatorBlacklistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("blacklist_spectator", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the spectator blacklist.");
                    FinalizeRecord(record);
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from spectator blacklist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from spectator blacklist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from spectator blacklist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SpectatorBlacklistRemoveTarget", 6);
        }

        public void ReportSourceBlacklistTarget(ARecord record)
        {
            Log.Debug(() => "Entering ReportSourceBlacklistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ReportSourceBlacklistTarget not available for multiple targets.");
                    Log.Error("ReportSourceBlacklistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "blacklist_report");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " report source blacklist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to report source blacklist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for report source blacklist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ReportSourceBlacklistTarget", 6);
        }

        public void ReportSourceBlacklistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering ReportSourceBlacklistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ReportSourceBlacklistRemoveTarget not available for multiple targets.");
                    Log.Error("ReportSourceBlacklistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("blacklist_report", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the report source blacklist.");
                    FinalizeRecord(record);
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from report source blacklist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from report source blacklist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from report source blacklist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ReportSourceBlacklistRemoveTarget", 6);
        }

        public void CommandTargetWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering CommandTargetWhitelistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "CommandTargetWhitelistTarget not available for multiple targets.");
                    Log.Error("CommandTargetWhitelistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_commandtarget");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " command target whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to command target whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for command target whitelist.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting CommandTargetWhitelistTarget", 6);
        }

        public void CommandTargetWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering CommandTargetWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "CommandTargetWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("CommandTargetWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_commandtarget", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the command target whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from command target whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from command target whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from command target whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting CommandTargetWhitelistRemoveTarget", 6);
        }

        public void AutoAssistBlacklistTarget(ARecord record)
        {
            Log.Debug(() => "Entering AutoAssistBlacklistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "AutoAssistBlacklistTarget not available for multiple targets.");
                    Log.Error("AutoAssistBlacklistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "blacklist_autoassist");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " auto-assist blacklist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to auto-assist blacklist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for command target whitelist.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AutoAssistBlacklistTarget", 6);
        }

        public void AutoAssistBlacklistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering AutoAssistBlacklistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "AutoAssistBlacklistRemoveTarget not available for multiple targets.");
                    Log.Error("AutoAssistBlacklistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("blacklist_autoassist", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the auto-assist blacklist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from auto-assist blacklist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from auto-assist blacklist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from auto-assist blacklist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AutoAssistBlacklistRemoveTarget", 6);
        }

        public void ReportWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering ReportWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ReportWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("ReportWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_report", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the Report whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from Report whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from Report whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from Report whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ReportWhitelistRemoveTarget", 6);
        }

        public void AAWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering AAWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "AAWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("AAWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_adminassistant", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the Admin Assistant whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from Admin Assistant whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from Admin Assistant whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from Admin Assistant whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AAWhitelistRemoveTarget", 6);
        }

        public void PingWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering PingWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "PingWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("PingWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_ping", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the Ping whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from Ping whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from Ping whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from Ping whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PingWhitelistRemoveTarget", 6);
        }

        public void AntiCheatWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering AntiCheatWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "AntiCheatWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("AntiCheatWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_anticheat", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the AntiCheat whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from AntiCheat whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from AntiCheat whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from AntiCheat whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AntiCheatWhitelistRemoveTarget", 6);
        }

        public void SpectatorSlotRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering SpectatorSlotRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "SpectatorSlotRemoveTarget not available for multiple targets.");
                    Log.Error("SpectatorSlotRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("slot_spectator", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the spectator slot list.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from spectator slot list.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from spectator slot list. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from spectator slot list.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SpectatorSlotRemoveTarget", 6);
        }

        public void ReservedSlotRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering ReservedSlotRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ReservedSlotRemoveTarget not available for multiple targets.");
                    Log.Error("ReservedSlotRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("slot_reserved", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the reserved slot list.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from reserved slot list.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from reserved slot list. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from reserved slot list.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ReservedSlotRemoveTarget", 6);
        }

        public void BalanceWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering BalanceWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "BalanceWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("BalanceWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_multibalancer", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the autobalance whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from autobalance whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from autobalance whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from autobalance whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting BalanceWhitelistRemoveTarget", 6);
        }

        public void BalanceDisperseRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering BalanceDisperseRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "BalanceDisperseRemoveTarget not available for multiple targets.");
                    Log.Error("BalanceDisperseRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("blacklist_dispersion", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not under autobalance dispersion.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from autobalance dispersion.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from autobalance dispersion. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from autobalance dispersion.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting BalanceDisperseRemoveTarget", 6);
        }

        public void BF4DBWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering BF4DBWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "BF4DBWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("BF4DBWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_bf4db", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the BF4DB whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from BF4DB whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from BF4DB whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from BF4DB whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting BF4DBWhitelistRemoveTarget", 6);
        }

        // Redundant :P
        public void BAWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering BAWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "BAWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("BAWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_ba", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the BattlefieldAgency whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from BattlefieldAgency whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from BattlefieldAgency whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from BattlefieldAgency whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting BAWhitelistRemoveTarget", 6);
        }

        public void AllCapsBlacklistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering AllCapsBlacklistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "AllCapsBlacklistRemoveTarget not available for multiple targets.");
                    Log.Error("AllCapsBlacklistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("blacklist_allcaps", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not under all-caps chat blacklist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from all-caps chat blacklist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from all-caps chat blacklist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from all-caps chat blacklist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting AllCapsBlacklistRemoveTarget", 6);
        }

        public void PopulatorWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering PopulatorWhitelistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "PopulatorWhitelistTarget not available for multiple targets.");
                    Log.Error("PopulatorWhitelistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_populator");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);
                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " populator whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                        }
                        else
                        {
                            Log.Error("Unable to add player to Populator whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Populator Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PopulatorWhitelistTarget", 6);
        }

        public void PopulatorWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering PopulatorWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "PopulatorWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("PopulatorWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_populator", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the populator whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from populator whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from populator whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from populator whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PopulatorWhitelistRemoveTarget", 6);
        }

        public void TeamKillTrackerWhitelistTarget(ARecord record)
        {
            Log.Debug(() => "Entering TeamKillTrackerWhitelistTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "TeamKillTrackerWhitelistTarget not available for multiple targets.");
                    Log.Error("TeamKillTrackerWhitelistTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_teamkill");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " TeamKillTracker whitelist for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                        }
                        else
                        {
                            Log.Error("Unable to add player to TeamKillTracker whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for TeamKillTracker Whitelist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting TeamKillTrackerWhitelistTarget", 6);
        }

        public void TeamKillTrackerWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering TeamKillTrackerWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "TeamKillTrackerWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("TeamKillTrackerWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_teamkill", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the TeamKillTracker whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from TeamKillTracker whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from TeamKillTracker whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from TeamKillTracker whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting TeamKillTrackerWhitelistRemoveTarget", 6);
        }

        public void WatchlistTarget(ARecord record)
        {
            Log.Debug(() => "Entering WatchlistTarget", 6);
            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "watchlist");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " watchlist entry for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to watchlist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for watchlist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting WatchlistTarget", 6);
        }

        public void WatchlistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering WatchlistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "WatchlistRemoveTarget not available for multiple targets.");
                    Log.Error("WatchlistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }

                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("watchlist", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the watchlist.");
                    FinalizeRecord(record);
                    return;
                }

                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from watchlist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from watchlist. Error uploading.");
                            }
                        }
                    }

                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from watchlist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }

            Log.Debug(() => "Exiting WatchlistRemoveTarget", 6);
        }

        public void MoveProtectionWhitelistTarget(ARecord record)
        {
            // insert hedius redundancy rant. This is not my SW. i am just extending it :)
            // Do not judge my coding skills by this terrible project :)
            // btw the method name is not matching the other ones.... eh... whatever...
            Log.Debug(() => "Entering MoveProtectionWhitelistTarget", 6);
            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", "whitelist_move_protection");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " move protection whitelist entry for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to move protection whitelist. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for watchlist record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting MoveProtectionWhitelistTarget", 6);
        }

        public void MoveProtectionWhitelistRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering MoveProtectionWhitelistRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "MoveProtectionWhitelistRemoveTarget not available for multiple targets.");
                    Log.Error("MoveProtectionWhitelistRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("whitelist_move_protection", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the move protection whitelist.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from move protection whitelist.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from move protection whitelist. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from move protection whitelist.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting MoveProtectionWhitelistRemoveTarget", 6);
        }

        public void UpdatePlayerBattlecry(ARecord record)
        {
            Log.Debug(() => "Entering UpdatePlayerBattlecry", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "UpdatePlayerBattlecry not available for multiple targets.");
                    Log.Error("UpdatePlayerBattlecry not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                if (record.target_player.player_id <= 0)
                {
                    Log.Error("Player ID invalid when assigning player battlecry. Unable to complete.");
                    SendMessageToSource(record, "Player ID invalid when assigning player battlecry. Unable to complete.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;

                //Update the player's battlecry on the object
                record.target_player.player_battlecry = record.record_message;

                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        if (String.IsNullOrEmpty(record.target_player.player_battlecry))
                        {
                            command.CommandText = @"DELETE FROM `adkats_battlecries` WHERE `player_id` = @player_id";
                            command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                if (record.source_name == record.target_name)
                                {
                                    SendMessageToSource(record, "Your battlecry has been removed.");
                                }
                                else
                                {
                                    SendMessageToSource(record, record.GetTargetNames() + "'s battlecry has been removed.");
                                }
                            }
                            else
                            {
                                Log.Error("Unable to remove player battlecry.");
                                SendMessageToSource(record, "Unable to remove your battlecry.");
                            }
                        }
                        else
                        {
                            command.CommandText = @"
                            REPLACE INTO
                                `adkats_battlecries`
                            (
                                `player_id`,
                                `player_battlecry`
                            )
                            VALUES
                            (
                                @player_id,
                                @player_battlecry
                            )";
                            command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                            command.Parameters.AddWithValue("@player_battlecry", record.target_player.player_battlecry);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "'" + record.target_player.player_battlecry + "'.";
                                if (record.source_name == record.target_name)
                                {
                                    message = "Your new battlecry: " + message;
                                }
                                else
                                {
                                    message = record.GetTargetNames() + "'s new battlecry: " + message;
                                }
                                SendMessageToSource(record, message);
                            }
                            else
                            {
                                Log.Error("Unable to update player battlecry. Error uploading.");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Player Battlecry record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting UpdatePlayerBattlecry", 6);
        }

        public void UpdatePlayerDiscordLink(ARecord record)
        {
            Log.Debug(() => "Entering UpdatePlayerDiscordLink", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "UpdatePlayerDiscordLink not available for multiple targets.");
                    Log.Error("UpdatePlayerDiscordLink not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                if (record.target_player.player_id <= 0)
                {
                    Log.Error("Player ID invalid when linking player to discord member. Unable to complete.");
                    SendMessageToSource(record, "Player ID invalid when linking player to discord member. Unable to complete.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;

                // Pull the discord member
                var matchingMember = _DiscordManager.GetMembers(false, true, true)
                    .FirstOrDefault(aMember => aMember.ID == record.record_message);
                if (matchingMember == null)
                {
                    SendMessageToSource(record, "No matching discord member for ID " + record.record_message + ".");
                    FinalizeRecord(record);
                    return;
                }

                //Update the player's discord ID on the object
                record.target_player.player_discord_id = matchingMember.ID;

                //Update the member object with the player
                matchingMember.PlayerObject = record.target_player;
                matchingMember.PlayerTested = true;

                //Save info to the database
                UpdatePlayer(record.target_player);

                SendMessageToSource(record, record.target_player.GetVerboseName() + " linked with discord member " + matchingMember.Name + ".");

                // Update the setting page since the list there needs to be updated
                UpdateSettingPage();
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Link Player to Discord Member record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting UpdatePlayerDiscordLink", 6);
        }

        public void MuteTarget(ARecord record)
        {
            Log.Debug(() => "Entering muteTarget", 6);
            try
            {
                record.record_action_executed = true;
                if (!HasAccess(record.target_player, GetCommandByKey("player_mute")))
                {
                    if (!_RoundMutedPlayers.ContainsKey(record.target_player.player_name))
                    {
                        _RoundMutedPlayers.Add(record.target_player.player_name, 0);
                        AdminSayMessage(record.GetTargetNames() + " has been muted for this round.");
                        if (record.record_source != ARecord.Sources.InGame &&
                            record.record_source != ARecord.Sources.Automated &&
                            record.record_source != ARecord.Sources.ServerCommand)
                        {
                            SendMessageToSource(record, record.GetTargetNames() + " has been muted for this round.");
                        }
                        PlayerSayMessage(record.target_player.player_name, _MutedPlayerMuteMessage);
                    }
                    else
                    {
                        SendMessageToSource(record, record.GetTargetNames() + " already muted for this round.");
                    }
                }
                else
                {
                    SendMessageToSource(record, "You can't mute an admin.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Mute record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting muteTarget", 6);
        }

        public void PersistentMuteTarget(ARecord record, bool force)
        {
            Log.Debug(() => "Entering PersistentMuteTarget", 6);
            if (HasAccess(record.target_player, GetCommandByKey("player_persistentmute"))
                || HasAccess(record.target_player, GetCommandByKey("player_persistentmute_force")))
            {
                SendMessageToSource(record, "You can't mute an admin.");
                FinalizeRecord(record);
                return;
            }

            try
            {
                record.record_action_executed = true;
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        DELETE FROM
                            `adkats_specialplayers`
                        WHERE `player_group` = @player_group
                          AND (`player_id` = @player_id OR `player_identifier` = @player_name);
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            @player_group,
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_group", force ? "persistent_mute_force" : "persistent_mute");
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " persistent " + (force ? "force " : "") + "mute on all servers.";
                            AdminSayMessage(message);
                            if (record.record_source != ARecord.Sources.InGame &&
                                record.record_source != ARecord.Sources.Automated &&
                                record.record_source != ARecord.Sources.ServerCommand)
                            {
                                SendMessageToSource(record, message);
                            }
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to persistent mute list. Error uploading. Force: " + force);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for persistent mute record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PersistentMuteTarget", 6);
        }

        public void UnMuteTarget(ARecord record)
        {
            Log.Debug(() => "Entering UnMuteTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "UnMuteTarget not available for multiple targets.");
                    Log.Error("UnMuteTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                var persistentMute = GetMatchingVerboseASPlayersOfGroup("persistent_mute", record.target_player).Any();
                var persistentForceMute = GetMatchingVerboseASPlayersOfGroup("persistent_mute_force", record.target_player).Any();
                if (persistentMute || persistentForceMute)
                {
                    List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("persistent_mute", record.target_player);
                    List<ASpecialPlayer> matchingPlayersForce = GetMatchingASPlayersOfGroup("persistent_mute_force", record.target_player);
                    if (!matchingPlayers.Any() && !matchingPlayersForce.Any())
                    {
                        SendMessageToSource(record, "Matching player not in the persistent mute list.");
                        FinalizeRecord(record);
                        return;
                    }
                    using (MySqlConnection connection = GetDatabaseConnection())
                    {
                        Boolean updated = false;
                        foreach (ASpecialPlayer asPlayer in matchingPlayers.Concat(matchingPlayersForce).ToList())
                        {
                            using (MySqlCommand command = connection.CreateCommand())
                            {
                                command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                                command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                                Int32 rowsAffected = SafeExecuteNonQuery(command);
                                if (rowsAffected > 0)
                                {
                                    String message = "Player " + record.GetTargetNames() + " removed from persistent mutes.";
                                    Log.Debug(() => message, 3);
                                    updated = true;
                                }
                                else
                                {
                                    Log.Error("Unable to remove player from persistent mutes. Error uploading.");
                                }
                            }
                        }
                        if (updated)
                        {
                            String message = "Player " + record.GetTargetNames() + " has been unmuted. (removed perma/temp mute)";
                            SendMessageToSource(record, message);
                            FetchAllAccess(true);
                        }
                    }
                }
                if (_RoundMutedPlayers.ContainsKey(record.target_player.player_name))
                {
                    _RoundMutedPlayers.Remove(record.target_player.player_name);
                    if (!persistentMute)
                        SendMessageToSource(record, record.GetTargetNames() + " has been unmuted.");
                    PlayerSayMessage(record.target_player.player_name, _UnMutePlayerMessage);
                }
                else
                {
                    if (!persistentMute && !persistentForceMute)
                        SendMessageToSource(record, record.GetTargetNames() + " is not muted.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for UnMute record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting UnMuteTarget", 6);
        }

        public void JoinTarget(ARecord record)
        {
            Log.Debug(() => "Entering joinTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Get source player
                APlayer sourcePlayer = null;
                if (_PlayerDictionary.TryGetValue(record.source_name, out sourcePlayer))
                {
                    sourcePlayer.LastUsage = UtcNow();
                    //If the source has access to move players, then the squad will be unlocked for their entry
                    if (HasAccess(record.source_player, GetCommandByKey("player_move")))
                    {
                        //Unlock target squad
                        SendMessageToSource(record, "Unlocking target squad if needed, please wait.");
                        ExecuteCommand("procon.protected.send", "squad.private", record.target_player.fbpInfo.TeamID.ToString(), record.target_player.fbpInfo.SquadID.ToString(), "false");
                        //If anything longer is needed...tisk tisk
                        Threading.Wait(500);
                    }
                    //Check for player access to change teams
                    if (record.target_player.fbpInfo.TeamID != sourcePlayer.fbpInfo.TeamID && !HasAccess(record.source_player, GetCommandByKey("self_teamswap")))
                    {
                        SendMessageToSource(record, "Target player is not on your team, you need " + GetChatCommandByKey("self_teamswap") + " access to join them.");
                    }
                    else
                    {
                        //Move to specific squad
                        Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                        ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                        _MULTIBalancerUnswitcherDisabled = true;
                        ATeam targetTeam;
                        if (GetTeamByID(record.target_player.fbpInfo.TeamID, out targetTeam))
                        {
                            record.source_player.RequiredTeam = targetTeam;
                            _LastPlayerMoveIssued = UtcNow();
                            SendMessageToSource(record, "Attempting to join " + record.GetTargetNames());
                            ExecuteCommand("procon.protected.send", "admin.movePlayer", record.source_player.player_name, record.target_player.fbpInfo.TeamID.ToString(), record.target_player.fbpInfo.SquadID.ToString(), "true");
                        }
                    }
                }
                else
                {
                    SendMessageToSource(record, "Unable to find you in the player list, please try again.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Join record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting joinTarget", 6);
        }

        public void PullTarget(ARecord record)
        {
            Log.Debug(() => "Entering PullTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Unlock squad
                SendMessageToSource(record, "Unlocking your squad for entry.");
                ExecuteCommand("procon.protected.send", "squad.private", record.source_player.fbpInfo.TeamID.ToString(), record.source_player.fbpInfo.SquadID.ToString(), "false");
                Threading.Wait(500);
                //Move to specific squad
                Log.Debug(() => "MULTIBalancer Unswitcher Disabled", 3);
                ExecuteCommand("procon.protected.plugins.call", "MULTIbalancer", "UpdatePluginData", "AdKats", "bool", "DisableUnswitcher", "True");
                _MULTIBalancerUnswitcherDisabled = true;
                ATeam sourceTeam;
                if (GetTeamByID(record.source_player.fbpInfo.TeamID, out sourceTeam))
                {
                    record.target_player.RequiredTeam = sourceTeam;
                    _LastPlayerMoveIssued = UtcNow();
                    SendMessageToSource(record, "Pulling " + record.GetTargetNames() + " to your squad.");
                    ExecuteCommand("procon.protected.send", "admin.movePlayer", record.target_player.player_name, sourceTeam.TeamID.ToString(), record.source_player.fbpInfo.SquadID.ToString(), "true");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Join record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PullTarget", 6);
        }

        public void ReportTarget(ARecord record)
        {
            Log.Debug(() => "Entering reportTarget", 6);
            try
            {
                Int32 reportID = AddRecordToReports(record);
                record.record_action_executed = true;
                if (_subscribedClients.Any(client => client.ClientName == "AdKatsLRT" && client.SubscriptionEnabled) &&
                    record.target_player != null &&
                    record.target_player.player_reputation <= 0 &&
                    record.target_player.player_online)
                {
                    Log.Info("Running loadout case for report record " + reportID);
                    if (!record.isLoadoutChecked)
                    {
                        lock (_LoadoutConfirmDictionary)
                        {
                            _LoadoutConfirmDictionary[record.target_player.player_name] = record;
                        }
                        Log.Info("Report " + reportID + " waiting for loadout confirmation.");
                        ExecuteCommand("procon.protected.plugins.call", "AdKatsLRT", "CallLoadoutCheckOnPlayer", "AdKats", JSON.JsonEncode(new Hashtable {
                            {"caller_identity", "AdKats"},
                            {"response_requested", false},
                            {"player_name", record.target_player.player_name},
                            {"loadoutCheck_reason", "reported"}
                        }));
                        return;
                    }
                    if (record.targetLoadoutActed)
                    {
                        SendMessageToSource(record, "Your report [" + reportID + "] has been acted on. Thank you.");
                        OnlineAdminSayMessage("Report " + reportID + " is being acted on by Loadout Enforcer.");
                        ConfirmActiveReport(record);
                        return;
                    }
                }
                AttemptReportAutoAction(record, reportID.ToString());
                String sourceAAIdentifier = (record.source_player != null && record.source_player.player_aa) ? ("(AA)") : ("");
                String targetAAIdentifier = (record.target_player != null && record.target_player.player_aa) ? ("(AA)") : ("");
                String slotID = (record.target_player != null) ? (record.target_player.player_slot) : (null);
                if (!String.IsNullOrEmpty(slotID))
                {
                    ExecuteCommand("procon.protected.send", "punkBuster.pb_sv_command", "pb_sv_getss " + slotID);
                }
                String sourcePlayerInfo = "";
                if (record.source_player != null && record.source_player.fbpInfo != null)
                {
                    if (record.source_player.player_online)
                    {
                        sourcePlayerInfo = " (" + Math.Round(record.source_player.player_reputation, 1) + ")(" + GetPlayerTeamKey(record.source_player) + "/" + (_PlayerDictionary.Values.Where(aPlayer => aPlayer.fbpInfo.TeamID == record.source_player.fbpInfo.TeamID).OrderBy(aPlayer => aPlayer.fbpInfo.Score).Reverse().ToList().IndexOf(record.target_player) + 1) + ")";
                    }
                    else
                    {
                        sourcePlayerInfo = " (OFFLINE)";
                    }
                }
                var sourceString = sourceAAIdentifier + record.GetSourceName() + sourcePlayerInfo;
                String targetPlayerInfo = "";
                if (record.target_player != null && record.target_player.fbpInfo != null)
                {
                    if (record.target_player.player_online)
                    {
                        targetPlayerInfo = " (" + Math.Round(record.target_player.player_reputation, 1) + ")(" + GetPlayerTeamKey(record.target_player) + "/" + (_PlayerDictionary.Values.Where(aPlayer => aPlayer.fbpInfo.TeamID == record.target_player.fbpInfo.TeamID).OrderBy(aPlayer => aPlayer.fbpInfo.Score).Reverse().ToList().IndexOf(record.target_player) + 1) + ")";
                    }
                    else
                    {
                        targetPlayerInfo = " (OFFLINE)";
                    }
                }
                var targetString = targetAAIdentifier + record.GetTargetNames() + targetPlayerInfo;
                OnlineAdminSayMessage("R[" + reportID + "] Source: " + sourceString);
                OnlineAdminSayMessage("R[" + reportID + "] Target: " + targetString);
                OnlineAdminSayMessage("R[" + reportID + "] Reason: " + record.record_message);
                if (record.isLoadoutChecked)
                {
                    if (record.target_player.loadout_valid)
                    {
                        OnlineAdminSayMessage("R[" + reportID + "] Loadout(VALID): " + record.target_player.loadout_items);
                    }
                    else
                    {
                        OnlineAdminSayMessage("R[" + reportID + "] Loadout(INVALID): " + record.target_player.loadout_deniedItems);
                    }
                }
                if (record.target_player != null && (record.target_player.player_reputation > _reputationThresholdGood || PlayerIsAdmin(record.target_player)))
                {
                    //Set Contested
                    record.isContested = true;
                    //Inform All Parties
                    SendMessageToSource(record, record.GetTargetNames() + "'s reputation has automatically contested your report against them.");
                    PlayerTellMessage(record.target_player.player_name, "Your reputation has automatically contested " + record.GetSourceName() + "'s report against you.");
                    OnlineAdminSayMessage(record.GetTargetNames() + "'s reputation has automatically contested report [" + record.command_numeric + "]");
                }
                else if (_InformReportedPlayers)
                {
                    String mesLow = record.record_message.ToLower();
                    if (!_PlayerInformExclusionStrings.Any(exc => mesLow.Contains(exc.ToLower())) && record.source_name != "AutoAdmin")
                    {
                        PlayerTellMessage(record.target_name, record.GetSourceName() + " reported you for " + record.record_message, true, 6);
                    }
                }
                if (_UseEmail)
                {
                    if (_EmailReportsOnlyWhenAdminless && FetchOnlineAdminSoldiers().Any())
                    {
                        Log.Debug(() => "Email cancelled, admins online.", 3);
                    }
                    else
                    {
                        Log.Debug(() => "Preparing to send report email.", 3);
                        _EmailHandler.SendReport(record);
                    }
                }
                if (_UsePushBullet)
                {
                    if (_PushBulletReportsOnlyWhenAdminless && FetchOnlineAdminSoldiers().Any())
                    {
                        Log.Debug(() => "PushBullet report cancelled, admins online.", 3);
                    }
                    else
                    {
                        Log.Debug(() => "Preparing to send PushBullet report.", 3);
                        _PushBulletHandler.PushReport(record);
                    }
                }
                if (_UseDiscordForReports)
                {
                    if (_DiscordReportsOnlyWhenAdminless && FetchOnlineAdminSoldiers().Any())
                    {
                        Log.Debug(() => "Discord report cancelled, admins online.", 3);
                    }
                    else
                    {
                        Log.Debug(() => "Preparing to send Discord report.", 3);
                        _DiscordManager.PostReport(record, "REPORT", sourceString, targetString);
                    }
                }
                if (record.source_player != null && record.source_name != record.target_name && record.source_player.player_type == PlayerType.Spectator)
                {
                    //Custom record to boost rep for reporting from spectator mode
                    ARecord repRecord = new ARecord
                    {
                        record_source = ARecord.Sources.Automated,
                        server_id = _serverInfo.ServerID,
                        command_type = GetCommandByKey("player_repboost"),
                        command_numeric = 0,
                        target_name = record.source_player.player_name,
                        target_player = record.source_player,
                        source_name = "RepManager",
                        record_message = "Player reported from Spectator Mode",
                        record_time = UtcNow()
                    };
                    UploadRecord(repRecord);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Report record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting reportTarget", 6);
        }

        public void CallAdminOnTarget(ARecord record)
        {
            Log.Debug(() => "Entering callAdminOnTarget", 6);
            try
            {
                Int32 reportID = AddRecordToReports(record);
                record.record_action_executed = true;
                if (_subscribedClients.Any(client => client.ClientName == "AdKatsLRT" && client.SubscriptionEnabled) &&
                    record.target_player != null &&
                    record.target_player.player_reputation <= 0 &&
                    record.target_player.player_online)
                {
                    Log.Info("Running loadout case for report record " + reportID);
                    if (!record.isLoadoutChecked)
                    {
                        if (!_LoadoutConfirmDictionary.ContainsKey(record.target_player.player_name))
                        {
                            lock (_LoadoutConfirmDictionary)
                            {
                                _LoadoutConfirmDictionary[record.target_player.player_name] = record;
                            }
                            Log.Info("Admin call " + reportID + " waiting for loadout confirmation.");
                            ExecuteCommand("procon.protected.plugins.call", "AdKatsLRT", "CallLoadoutCheckOnPlayer", "AdKats", JSON.JsonEncode(new Hashtable {
                                {"caller_identity", "AdKats"},
                                {"response_requested", false},
                                {"player_name", record.target_player.player_name},
                                {"loadoutCheck_reason", "reported"}
                            }));
                        }
                        return;
                    }
                    if (record.targetLoadoutActed)
                    {
                        SendMessageToSource(record, "Your report [" + reportID + "] has been acted on. Thank you.");
                        OnlineAdminSayMessage("Report " + reportID + " is being acted on by Loadout Enforcer.");
                        ConfirmActiveReport(record);
                        return;
                    }
                }
                AttemptReportAutoAction(record, reportID.ToString());
                String sourceAAIdentifier = (record.source_player != null && record.source_player.player_aa) ? ("(AA)") : ("");
                String targetAAIdentifier = (record.target_player != null && record.target_player.player_aa) ? ("(AA)") : ("");
                String slotID = (record.target_player != null) ? (record.target_player.player_slot) : (null);
                if (!String.IsNullOrEmpty(slotID))
                {
                    ExecuteCommand("procon.protected.send", "punkBuster.pb_sv_command", "pb_sv_getss " + slotID);
                }
                String sourcePlayerInfo = "";
                if (record.source_player != null && record.source_player.fbpInfo != null)
                {
                    if (record.source_player.player_online)
                    {
                        sourcePlayerInfo = " (" + Math.Round(record.source_player.player_reputation, 1) + ")(" + GetPlayerTeamKey(record.source_player) + "/" + (_PlayerDictionary.Values.Where(aPlayer => aPlayer.fbpInfo.TeamID == record.source_player.fbpInfo.TeamID).OrderBy(aPlayer => aPlayer.fbpInfo.Score).Reverse().ToList().IndexOf(record.target_player) + 1) + ")";
                    }
                    else
                    {
                        sourcePlayerInfo = " (OFFLINE)";
                    }
                }
                var sourceString = sourceAAIdentifier + record.GetSourceName() + sourcePlayerInfo;
                String targetPlayerInfo = "";
                if (record.target_player != null && record.target_player.fbpInfo != null)
                {
                    if (record.target_player.player_online)
                    {
                        targetPlayerInfo = " (" + Math.Round(record.target_player.player_reputation, 1) + ")(" + GetPlayerTeamKey(record.target_player) + "/" + (_PlayerDictionary.Values.Where(aPlayer => aPlayer.fbpInfo.TeamID == record.target_player.fbpInfo.TeamID).OrderBy(aPlayer => aPlayer.fbpInfo.Score).Reverse().ToList().IndexOf(record.target_player) + 1) + ")";
                    }
                    else
                    {
                        targetPlayerInfo = " (OFFLINE)";
                    }
                }
                var targetString = targetAAIdentifier + record.GetTargetNames() + targetPlayerInfo;
                OnlineAdminSayMessage("A[" + reportID + "] Source: " + sourceString);
                OnlineAdminSayMessage("A[" + reportID + "] Target: " + targetString);
                OnlineAdminSayMessage("A[" + reportID + "] Reason: " + record.record_message);
                if (record.isLoadoutChecked)
                {
                    if (record.target_player.loadout_valid)
                    {
                        OnlineAdminSayMessage("A[" + reportID + "] Loadout(VALID): " + record.target_player.loadout_items);
                    }
                    else
                    {
                        OnlineAdminSayMessage("A[" + reportID + "] Loadout(INVALID): " + record.target_player.loadout_deniedItems);
                    }
                }
                if (_InformReportedPlayers)
                {
                    String mesLow = record.record_message.ToLower();
                    if (!_PlayerInformExclusionStrings.Any(exc => mesLow.Contains(exc.ToLower())))
                    {
                        PlayerTellMessage(record.target_name, record.GetSourceName() + " reported you for " + record.record_message, true, 6);
                    }
                }
                if (_UseEmail)
                {
                    if (_EmailReportsOnlyWhenAdminless && FetchOnlineAdminSoldiers().Any())
                    {
                        Log.Debug(() => "Email cancelled, admins online.", 3);
                    }
                    else
                    {
                        Log.Debug(() => "Preparing to send admin call email.", 3);
                        _EmailHandler.SendReport(record);
                    }
                }
                if (_UsePushBullet)
                {
                    if (_PushBulletReportsOnlyWhenAdminless && FetchOnlineAdminSoldiers().Any())
                    {
                        Log.Debug(() => "PushBullet admin call cancelled, admins online.", 3);
                    }
                    else
                    {
                        Log.Debug(() => "Preparing to send PushBullet admin call.", 3);
                        _PushBulletHandler.PushReport(record);
                    }
                }
                if (_UseDiscordForReports)
                {
                    if (_DiscordReportsOnlyWhenAdminless && FetchOnlineAdminSoldiers().Any())
                    {
                        Log.Debug(() => "Discord admin call cancelled, admins online.", 3);
                    }
                    else
                    {
                        Log.Debug(() => "Preparing to send Discord admin call.", 3);
                        _DiscordManager.PostReport(record, "ADMIN CALL", sourceString, targetString);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for CallAdmin record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting callAdminOnTarget", 6);
        }

        public void AttemptReportAutoAction(ARecord record, String reportID)
        {
            Boolean sourceAA = record.source_player != null && record.source_player.player_aa;
            Int32 onlineAdminCount = FetchOnlineAdminSoldiers().Count;
            String messageLower = record.record_message.ToLower();
            Boolean canAutoHandle = _UseAAReportAutoHandler && sourceAA && _AutoReportHandleStrings.Any() && !String.IsNullOrEmpty(_AutoReportHandleStrings[0]) && _AutoReportHandleStrings.Any(messageLower.Contains) && !record.target_player.player_aa && !PlayerIsAdmin(record.target_player);
            Boolean adminsOnline = onlineAdminCount > 0;
            String reportMessage = "";
            if (!sourceAA || !adminsOnline || !canAutoHandle)
            {
                reportMessage = "REPORT [" + reportID + "] sent on " + record.GetTargetNames() + " for " + record.record_message;
            }
            else
            {
                reportMessage = "REPORT [" + reportID + "] on " + record.GetTargetNames() + " sent to " + onlineAdminCount + " in-game admin" + ((onlineAdminCount > 1) ? ("s") : ("")) + ". " + ((canAutoHandle) ? ("Admins have 45 seconds before auto-handling.") : (""));
            }
            SendMessageToSource(record, reportMessage);
            if (!canAutoHandle)
            {
                //Log.Warn("Cancelling auto-handler.");
                return;
            }
            Thread reportAutoHandler = new Thread(new ThreadStart(delegate
            {
                //Log.Warn("Starting report auto-handler thread.");
                try
                {
                    Thread.CurrentThread.Name = "ReportAutoHandler";
                    //If admins are online, act after 45 seconds. If they are not, act after 5 seconds.
                    Threading.Wait(TimeSpan.FromSeconds((adminsOnline) ? (45.0) : (5.0)));
                    //Get the report record
                    ARecord reportRecord = FetchPlayerReportByID(reportID);
                    if (reportRecord != null)
                    {
                        if (CanPunish(reportRecord, 90) || !adminsOnline)
                        {
                            //Update it in the database
                            ConfirmActiveReport(reportRecord);
                            //Get target information
                            ARecord aRecord = new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_punish"),
                                command_numeric = 0,
                                target_name = reportRecord.target_player.player_name,
                                target_player = reportRecord.target_player,
                                source_name = "ProconAdmin",
                                record_message = reportRecord.record_message,
                                record_time = UtcNow()
                            };
                            //Inform the reporter that they helped the admins
                            SendMessageToSource(reportRecord, "Your report [" + reportRecord.command_numeric + "] has been acted on. Thank you.");
                            //Queue for processing
                            QueueRecordForProcessing(aRecord);
                        }
                        else
                        {
                            SendMessageToSource(reportRecord, "Reported player has already been acted on.");
                        }
                    }
                }
                catch (Exception)
                {
                    Log.HandleException(new AException("Error while auto-handling report."));
                }
                Log.Debug(() => "Exiting a report auto-handler.", 5);
            }));

            //Start the thread
            Threading.StartWatchdog(reportAutoHandler);
        }

        public void ChallengePlayTarget(ARecord record)
        {
            Log.Debug(() => "Entering ChallengePlayTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ChallengePlayTarget not available for multiple targets.");
                    Log.Error("ChallengePlayTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("challenge_play", record.target_player);
                if (matchingPlayers.Count > 0)
                {
                    SendMessageToSource(record, matchingPlayers.Count + " matching player(s) already in the challenge playing status.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            'challenge_play',
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " challenge playing status for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to challenge playing status. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for challenge playing status.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ChallengePlayTarget", 6);
        }

        public void ChallengePlayRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering ChallengePlayRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ChallengePlayRemoveTarget not available for multiple targets.");
                    Log.Error("ChallengePlayRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("challenge_play", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the challenge playing status.");
                    FinalizeRecord(record);
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from challenge playing status.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from challenge playing status. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from challenge playing status.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ChallengePlayRemoveTarget", 6);
        }

        public void ChallengeIgnoreTarget(ARecord record)
        {
            Log.Debug(() => "Entering ChallengeIgnoreTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ChallengeIgnoreTarget not available for multiple targets.");
                    Log.Error("ChallengeIgnoreTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("challenge_ignore", record.target_player);
                if (matchingPlayers.Count > 0)
                {
                    SendMessageToSource(record, matchingPlayers.Count + " matching player(s) already in the challenge ignoring status.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            'challenge_ignore',
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " challenge ignoring status for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to challenge ignoring status. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for challenge ignoring status.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ChallengeIgnoreTarget", 6);
        }

        public void ChallengeIgnoreRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering ChallengeIgnoreRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ChallengeIgnoreRemoveTarget not available for multiple targets.");
                    Log.Error("ChallengeIgnoreRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("challenge_ignore", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the challenge ignoring status.");
                    FinalizeRecord(record);
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from challenge ignoring status.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from challenge ignoring status. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from challenge ignoring status.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ChallengeIgnoreRemoveTarget", 6);
        }

        public void ChallengeAutoKillTarget(ARecord record)
        {
            Log.Debug(() => "Entering ChallengeAutoKillTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ChallengeAutoKillTarget not available for multiple targets.");
                    Log.Error("ChallengeAutoKillTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("challenge_autokill", record.target_player);
                if (matchingPlayers.Count > 0)
                {
                    SendMessageToSource(record, matchingPlayers.Count + " matching player(s) already in the challenge playing status.");
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        INSERT INTO
                            `adkats_specialplayers`
                        (
                            `player_group`,
                            `player_id`,
                            `player_identifier`,
                            `player_effective`,
                            `player_expiration`
                        )
                        VALUES
                        (
                            'challenge_autokill',
                            @player_id,
                            @player_name,
                            UTC_TIMESTAMP(),
                            DATE_ADD(UTC_TIMESTAMP(), INTERVAL @duration_minutes MINUTE)
                        )";
                        if (record.target_player.player_id <= 0)
                        {
                            Log.Error("Player ID invalid when assigning special player entry. Unable to complete.");
                            SendMessageToSource(record, "Player ID invalid when assigning special player entry. Unable to complete.");
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.command_numeric > 10518984)
                        {
                            record.command_numeric = 10518984;
                        }
                        command.Parameters.AddWithValue("@player_id", record.target_player.player_id);
                        command.Parameters.AddWithValue("@player_name", record.target_player.player_name);
                        command.Parameters.AddWithValue("@duration_minutes", record.command_numeric);

                        Int32 rowsAffected = SafeExecuteNonQuery(command);
                        if (rowsAffected > 0)
                        {
                            String message = "Player " + record.GetTargetNames() + " given " + ((record.command_numeric == 10518984) ? ("permanent") : (FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2))) + " challenge autokill status for all servers.";
                            SendMessageToSource(record, message);
                            Log.Debug(() => message, 3);
                            FetchAllAccess(true);
                        }
                        else
                        {
                            Log.Error("Unable to add player to challenge playing status. Error uploading.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for challenge playing status.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ChallengeAutoKillTarget", 6);
        }

        public void ChallengeAutoKillRemoveTarget(ARecord record)
        {
            Log.Debug(() => "Entering ChallengeAutoKillRemoveTarget", 6);
            try
            {
                //Case for multiple targets
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "ChallengeAutoKillRemoveTarget not available for multiple targets.");
                    Log.Error("ChallengeAutoKillRemoveTarget not available for multiple targets.");
                    FinalizeRecord(record);
                    return;
                }
                record.record_action_executed = true;
                List<ASpecialPlayer> matchingPlayers = GetMatchingASPlayersOfGroup("challenge_autokill", record.target_player);
                if (!matchingPlayers.Any())
                {
                    SendMessageToSource(record, "Matching player not in the challenge autokill status.");
                    FinalizeRecord(record);
                    return;
                }
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    Boolean updated = false;
                    foreach (ASpecialPlayer asPlayer in matchingPlayers)
                    {
                        using (MySqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = @"DELETE FROM `adkats_specialplayers` WHERE `specialplayer_id` = @sp_id";
                            command.Parameters.AddWithValue("@sp_id", asPlayer.specialplayer_id);
                            Int32 rowsAffected = SafeExecuteNonQuery(command);
                            if (rowsAffected > 0)
                            {
                                String message = "Player " + record.GetTargetNames() + " removed from challenge autokill status.";
                                Log.Debug(() => message, 3);
                                updated = true;
                            }
                            else
                            {
                                Log.Error("Unable to remove player from challenge autokill status. Error uploading.");
                            }
                        }
                    }
                    if (updated)
                    {
                        String message = "Player " + record.GetTargetNames() + " removed from challenge autokill status.";
                        SendMessageToSource(record, message);
                        FetchAllAccess(true);
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for " + record.command_action.command_name + " record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ChallengeAutoKillRemoveTarget", 6);
        }

        public void LanguagePunishTarget(ARecord record)
        {
            Log.Debug(() => "Entering LanguagePunishTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Perform actions
                if (String.IsNullOrEmpty(record.target_player.player_name))
                {
                    Log.Error("Tried to issue an language punish on a null target.");
                }
                else
                {
                    if (record.record_source != ARecord.Sources.InGame && record.record_source != ARecord.Sources.Automated && record.record_source != ARecord.Sources.ServerCommand)
                    {
                        SendMessageToSource(record, "You issued a LANGUAGE PUNISH on " + record.GetTargetNames() + " for " + record.record_message);
                    }

                    AdminSayMessage(Log.FBold(Log.CRed(record.GetTargetNames() + " LANGUAGE PUNISHED" + (_ShowAdminNameInAnnouncement ? (" by " + record.GetSourceName()) : ("")) + " for " + record.record_message)));
                    ExecuteCommand("procon.protected.plugins.call", "LanguageEnforcer", "RemoteManuallyPunishPlayer", GetType().Name, record.target_player.player_name, record.target_player.player_guid);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for language punish record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }

            Log.Debug(() => "Exiting LanguagePunishTarget", 6);
        }

        public void LanguageResetTarget(ARecord record)
        {
            Log.Debug(() => "Entering LanguageResetTarget", 6);
            try
            {
                record.record_action_executed = true;
                //Perform actions
                if (String.IsNullOrEmpty(record.target_player.player_name))
                {
                    Log.Error("Tried to issue an language reset on a null target.");
                }
                else
                {
                    if (record.record_source != ARecord.Sources.InGame &&
                        record.record_source != ARecord.Sources.Automated &&
                        record.record_source != ARecord.Sources.ServerCommand)
                    {
                        SendMessageToSource(record, "You issued a LANGUAGE RESET on " + record.GetTargetNames() + " for " + record.record_message);
                    }
                    ExecuteCommand("procon.protected.plugins.call", "LanguageEnforcer", "RemoteManuallyResetPlayer", GetType().Name, record.target_player.player_name, record.target_player.player_guid);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for language reset record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting LanguageResetTarget", 6);
        }

        public void PurgeExtendedRoundStats()
        {
            Log.Debug(() => "Entering PurgeExtendedRoundStats", 6);
            try
            {
                //Purge all extended round stats older than two years
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"delete from tbl_extendedroundstats where tbl_extendedroundstats.roundstat_time < date_sub(sysdate(), interval 2 year)";
                        Int32 affectedRows = SafeExecuteNonQuery(command);
                        if (affectedRows > 0)
                        {
                            Log.Debug(() => "Purged " + affectedRows + " extended round stats older than 2 years.", 5);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while purging extended round statistics.", e));
            }
            Log.Debug(() => "Exiting PurgeExtendedRoundStats", 6);
        }

        public void PurgeOutdatedStatistics()
        {
            Log.Debug(() => "Entering PurgeOutdatedStatistics", 6);
            try
            {
                //Purge all Adkats statistics older than 180 days
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"delete from adkats_statistics where adkats_statistics.stat_time < date_sub(sysdate(), interval 180 day)";
                        Int32 affectedRows = SafeExecuteNonQuery(command);
                        if (affectedRows > 0)
                        {
                            Log.Debug(() => "Purged " + affectedRows + " AdKats statistics older than 180 days.", 5);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while purging Adkats statistics.", e));
            }
            Log.Debug(() => "Exiting PurgeOutdatedStatistics", 6);
        }

        public void PurgeOutdatedExceptions()
        {
            Log.Debug(() => "Entering PurgeOutdatedExceptions", 6);
            try
            {
                //Purge all extended round stats older than 60 days
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"delete from adkats_records_debug where record_time < '2017-10-12'";
                        Int32 affectedRows = SafeExecuteNonQuery(command);
                        if (affectedRows > 0)
                        {
                            Log.Debug(() => "Purged " + affectedRows + " debug records older than current stable version.", 5);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while purging outdated exceptions.", e));
            }
            Log.Debug(() => "Exiting PurgeOutdatedExceptions", 6);
        }

        public void FixInvalidCommandIds()
        {
            Log.Debug(() => "Entering FixInvalidCommandIds", 6);
            try
            {
                //The BFACP has an old bug that people keep running into.
                //The incorrect command action values are used when taking some actions.
                //This monitor will fix those with the correct ones.
                using (MySqlConnection connection = GetDatabaseConnection())
                {
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"update adkats_records_main set command_action = 62 where command_action = 61 and command_type in (18, 20);
                                                update adkats_records_main set command_action = 42 where command_action = 41 and command_type in (18, 20);
                                                update adkats_records_main set command_action = 19 where command_action = 40 and command_type in (18, 20);";
                        Int32 affectedRows = SafeExecuteNonQuery(command);
                        if (affectedRows > 0)
                        {
                            Log.Debug(() => "Fixed " + affectedRows + " invalid command action values.", 5);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fixing invalid command action values.", e));
            }
            Log.Debug(() => "Exiting FixInvalidCommandIds", 6);
        }

        public void RestartLevel(ARecord record)
        {
            Log.Debug(() => "Entering restartLevel", 6);
            try
            {
                record.record_action_executed = true;
                ExecuteCommand("procon.protected.send", "mapList.restartRound");
                SendMessageToSource(record, "Round Restarted.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for RestartLevel record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting restartLevel", 6);
        }

        public void NextLevel(ARecord record)
        {
            Log.Debug(() => "Entering nextLevel", 6);
            try
            {
                record.record_action_executed = true;
                ExecuteCommand("procon.protected.send", "mapList.runNextRound");
                SendMessageToSource(record, "Next round has been run.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for NextLevel record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting nextLevel", 6);
        }

        public void EndLevel(ARecord record)
        {
            Log.Debug(() => "Entering EndLevel", 6);
            try
            {
                record.record_action_executed = true;
                ExecuteCommand("procon.protected.send", "mapList.endRound", record.command_numeric.ToString());
                SendMessageToSource(record, "Ended round with " + record.GetTargetNames() + " winning.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for EndLevel record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting EndLevel", 6);
        }

        public void NukeTarget(ARecord record)
        {
            Log.Debug(() => "Entering NukeTarget", 6);
            try
            {
                record.record_action_executed = true;

                if (_NukeCountdownDurationSeconds > 0)
                {
                    //Start the thread
                    Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                    {
                        Log.Debug(() => "Starting a nuke countdown printer.", 5);
                        try
                        {
                            Thread.CurrentThread.Name = "NukeCountdownPrinter";
                            for (Int32 countdown = _NukeCountdownDurationSeconds; countdown > 0; countdown--)
                            {
                                if (!_pluginEnabled)
                                {
                                    Threading.StopWatchdog();
                                    return;
                                }
                                AdminTellMessage("Nuking " + record.GetTargetNames() + " team in " + countdown + "...");
                                Threading.Wait(TimeSpan.FromSeconds(1));
                            }
                            _lastNukeTime = UtcNow();
                            ATeam team1 = null;
                            ATeam team2 = null;
                            ATeam nukedTeam = null;
                            ATeam advancingTeam = null;
                            if (!GetTeamByID(1, out team1))
                            {
                                if (_roundState == RoundState.Playing)
                                {
                                    Log.Error("Teams not loaded when they should be.");
                                }
                                FinalizeRecord(record);
                                return;
                            }
                            if (!GetTeamByID(2, out team2))
                            {
                                if (_roundState == RoundState.Playing)
                                {
                                    Log.Error("Teams not loaded when they should be.");
                                }
                                FinalizeRecord(record);
                                return;
                            }
                            if (record.target_name != "Everyone")
                            {
                                if (record.command_numeric == team1.TeamID)
                                {
                                    incNukeCount(team1.TeamID);
                                    _lastNukeTeam = team1;
                                    nukedTeam = team1;
                                    advancingTeam = team2;
                                }
                                else if (record.command_numeric == team2.TeamID)
                                {
                                    incNukeCount(team2.TeamID);
                                    _lastNukeTeam = team2;
                                    nukedTeam = team2;
                                    advancingTeam = team1;
                                }
                            }
                            AdminTellMessage(record.source_name == "RoundManager" ? record.record_message : "Nuking " + record.GetTargetNames() + "!");
                            var nukeTargets = _PlayerDictionary.Values.ToList().Where(player => (nukedTeam != null && player.fbpInfo.TeamID == nukedTeam.TeamID) || (record.target_name == "Everyone"));
                            foreach (APlayer player in nukeTargets)
                            {
                                // Initial kills for nuke
                                ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                            }
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                            foreach (APlayer player in nukeTargets)
                            {
                                // Secondary kills for nuke
                                ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                            }
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                            foreach (APlayer player in nukeTargets)
                            {
                                // Tertiary kills for nuke
                                ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                            }
                        }
                        catch (Exception)
                        {
                            Log.HandleException(new AException("Error while printing nuke countdown"));
                        }
                        Log.Debug(() => "Exiting a nuke countdown printer.", 5);
                        Threading.StopWatchdog();
                    })));
                }
                else
                {
                    _lastNukeTime = UtcNow();
                    AdminTellMessage(record.source_name == "RoundManager" ? record.record_message : "Nuking " + record.GetTargetNames() + " team!");
                    var nukeTargets = _PlayerDictionary.Values.ToList().Where(player => (player.fbpInfo.TeamID == record.command_numeric) || (record.target_name == "Everyone"));
                    foreach (APlayer player in nukeTargets)
                    {
                        // Initial kills for nuke
                        ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                    }
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    foreach (APlayer player in nukeTargets)
                    {
                        // Secondary kills for nuke
                        ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                    }
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    foreach (APlayer player in nukeTargets)
                    {
                        // Tertiary kills for nuke
                        ExecuteCommand("procon.protected.send", "admin.killPlayer", player.player_name);
                    }
                }
                SendMessageToSource(record, "You NUKED " + record.GetTargetNames() + ".");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for NukeServer record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting NukeTarget", 6);
        }

        public void CountdownTarget(ARecord record)
        {
            Log.Debug(() => "Entering CountdownTarget", 6);
            try
            {
                record.record_action_executed = true;
                if (record.command_numeric < 1 || record.command_numeric > 30)
                {
                    SendMessageToSource(record, "Invalid duration, must be 1-30. Unable to act.");
                    FinalizeRecord(record);
                    return;
                }
                if (String.IsNullOrEmpty(record.record_message))
                {
                    SendMessageToSource(record, "Invalid countdown message, unable to act.");
                    FinalizeRecord(record);
                    return;
                }
                List<APlayer> targetedPlayers = new List<APlayer>();
                switch (record.target_name)
                {
                    case "Squad":
                        if (record.source_player == null || !record.source_player.player_online || !_PlayerDictionary.ContainsKey(record.source_player.player_name) || record.source_player.player_type == PlayerType.Spectator)
                        {
                            SendMessageToSource(record, "Source must be an online player to use squad option. Unable to act.");
                            FinalizeRecord(record);
                            return;
                        }
                        targetedPlayers.AddRange(_PlayerDictionary.Values.ToList().Where(aPlayer => aPlayer.fbpInfo.TeamID == record.source_player.fbpInfo.TeamID && aPlayer.fbpInfo.SquadID == record.source_player.fbpInfo.SquadID).ToList());
                        break;
                    case "Team":
                        if (record.source_player == null || !record.source_player.player_online || !_PlayerDictionary.ContainsKey(record.source_player.player_name) || record.source_player.player_type == PlayerType.Spectator)
                        {
                            SendMessageToSource(record, "Source must be an online player to use team option. Unable to act.");
                            FinalizeRecord(record);
                            return;
                        }
                        targetedPlayers.AddRange(_PlayerDictionary.Values.ToList().Where(aPlayer => aPlayer.fbpInfo.TeamID == record.source_player.fbpInfo.TeamID).ToList());
                        break;
                    case "All":
                        //All players, so include spectators and commanders
                        break;
                    default:
                        //Check for specific team targeting
                        var teamTarget = GetTeamByKey(record.target_name);
                        if (teamTarget != null)
                        {
                            //Send to target and neutral team
                            targetedPlayers.AddRange(_PlayerDictionary.Values.ToList().Where(aPlayer => aPlayer.fbpInfo.TeamID == teamTarget.TeamID || aPlayer.fbpInfo.TeamID == 0).ToList());
                        }
                        else
                        {
                            SendMessageToSource(record, "Invalid target, must be Squad, Team, or All. Unable to Act.");
                            FinalizeRecord(record);
                            return;
                        }
                        break;
                }
                //Start the thread
                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                {
                    Log.Debug(() => "Starting a countdown printer thread.", 5);
                    try
                    {
                        Thread.CurrentThread.Name = "CountdownPrinter";
                        for (Int32 countdown = record.command_numeric; countdown > 0; countdown--)
                        {
                            if (!_pluginEnabled)
                            {
                                Threading.StopWatchdog();
                                return;
                            }
                            if (record.target_name == "All")
                            {
                                AdminTellMessage(record.record_message + " in " + countdown + "...");
                            }
                            else
                            {
                                //Threads spawned from threads...oh god
                                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                                {
                                    try
                                    {
                                        Thread.CurrentThread.Name = "CountdownPrinter_Private";
                                        var inCount = countdown;
                                        foreach (APlayer aPlayer in targetedPlayers)
                                        {
                                            PlayerTellMessage(aPlayer.player_name, record.record_message + " in " + inCount + "...", false, 1);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        Log.HandleException(new AException("Error while printing private countdown"));
                                    }
                                    Threading.StopWatchdog();
                                })));
                            }
                            Threading.Wait(TimeSpan.FromSeconds(1));
                        }
                        if (record.target_name == "All")
                        {
                            AdminTellMessage(record.record_message + " NOW!");
                        }
                        else
                        {
                            foreach (APlayer aPlayer in targetedPlayers)
                            {
                                PlayerTellMessage(aPlayer.player_name, record.record_message + " NOW!", false, 1);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.HandleException(new AException("Error while printing server countdown", e));
                    }
                    Log.Debug(() => "Exiting a countdown printer.", 5);
                    Threading.StopWatchdog();
                })));
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for ServerCountdown record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting CountdownTarget", 6);
        }

        public void SwapNukeServer(ARecord record)
        {
            Log.Debug(() => "Entering SwapNukeServer", 6);
            try
            {
                record.record_action_executed = true;
                foreach (APlayer player in _PlayerDictionary.Values.ToList().Where(aPlayer => aPlayer.player_type == PlayerType.Player))
                {
                    QueuePlayerForForceMove(player.fbpInfo);
                }
                SendMessageToSource(record, "You SwapNuked the server.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for SwapNuke record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SwapNukeServer", 6);
        }

        public void KickAllPlayers(ARecord record)
        {
            Log.Debug(() => "Entering kickAllPlayers", 6);
            try
            {
                record.record_action_executed = true;
                foreach (APlayer player in _PlayerDictionary.Values.ToList().Where(player => player.player_role.role_key == "guest_default"))
                {
                    Threading.Wait(50);
                    ExecuteCommand("procon.protected.send", "admin.kickPlayer", player.player_name, "(" + record.source_name + ") " + record.record_message);
                }
                AdminSayMessage("All guest players have been kicked.");
                SendMessageToSource(record, "You KICKED EVERYONE for '" + record.record_message + "'");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for KickAll record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting kickAllPlayers", 6);
        }

        public void AdminSay(ARecord record)
        {
            Log.Debug(() => "Entering adminSay", 6);
            try
            {
                record.record_action_executed = true;
                AdminSayMessage(record.record_message);
                if (record.record_source != ARecord.Sources.InGame && record.record_source != ARecord.Sources.ServerCommand)
                {
                    SendMessageToSource(record, "Server has been told '" + record.record_message + "' by SAY");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for AdminSay record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting adminSay", 6);
        }

        public void PlayerSay(ARecord record)
        {
            Log.Debug(() => "Entering playerSay", 6);
            try
            {
                record.record_action_executed = true;
                PlayerSayMessage(record.target_player.player_name, record.record_message);
                if (record.record_source != ARecord.Sources.ServerCommand)
                {
                    SendMessageToSource(record, record.GetTargetNames() + " has been told '" + record.record_message + "' by SAY");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for playerSay record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting playerSay", 6);
        }

        public void PMSendTarget(ARecord record)
        {
            Log.Debug(() => "Entering PMSendTarget", 6);
            try
            {
                record.record_action_executed = true;

                APlayer sender = record.source_player;
                APlayer partner = record.target_player;
                Boolean adminInformedChange = false;

                //Check sender conditions
                if (sender.conversationPartner == null)
                {
                    //No conversation partner exists. Inform of the new one.
                    if (PlayerIsAdmin(sender) && !PlayerIsAdmin(partner) && !adminInformedChange)
                    {
                        OnlineAdminSayMessage("Admin " + sender.GetVerboseName() + " is now in a private conversation with " + partner.GetVerboseName(), sender.player_name);
                        adminInformedChange = true;
                    }
                    else
                    {
                        PlayerSayMessage(sender.player_name, "You are now in a private conversation with " + partner.GetVerboseName() + ". Use /" + GetCommandByKey("player_pm_reply").command_text + " msg to reply.");
                    }
                }
                else
                {
                    //Conversation partner exists. Cancel that conversation.
                    APlayer oldPartner = sender.conversationPartner;

                    if (oldPartner.player_guid != partner.player_guid)
                    {
                        if (PlayerIsAdmin(sender) && !PlayerIsAdmin(partner) && !adminInformedChange)
                        {
                            OnlineAdminSayMessage("Admin " + sender.GetVerboseName() + " is now in a private conversation with " + partner.GetVerboseName(), sender.player_name);
                            adminInformedChange = true;
                        }
                        else
                        {
                            PlayerSayMessage(sender.player_name, "Private conversation partner changed from " + oldPartner.GetVerboseName() + " to " + partner.GetVerboseName());
                        }
                    }
                    else
                    {
                        PlayerSayMessage(sender.player_name, "You are already in a conversation with " + oldPartner.GetVerboseName() + ". Use /" + GetCommandByKey("player_pm_reply").command_text + " msg to reply.");
                        return;
                    }

                    if (PlayerIsExternal(sender.conversationPartner))
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = oldPartner.player_server.ServerID,
                            record_orchestrate = true,
                            command_type = GetCommandByKey("player_pm_cancel"),
                            command_numeric = 0,
                            target_name = oldPartner.player_name,
                            target_player = oldPartner,
                            source_name = sender.player_name,
                            source_player = sender,
                            record_message = sender.GetVerboseName() + " has left the private conversation.",
                            record_time = UtcNow()
                        });
                    }
                    else
                    {
                        PlayerSayMessage(oldPartner.player_name, sender.GetVerboseName() + " has left the private conversation.");
                        oldPartner.conversationPartner = null;
                    }
                }
                //Assign local conversation partner
                sender.conversationPartner = partner;

                //Check for external case on new conversation partner
                if (PlayerIsExternal(partner))
                {
                    //Player is external, have that instance handle the needed actions
                    QueueRecordForProcessing(new ARecord
                    {
                        record_source = ARecord.Sources.Automated,
                        server_id = partner.player_server.ServerID,
                        record_orchestrate = true,
                        command_type = GetCommandByKey("player_pm_start"),
                        command_numeric = 0,
                        target_name = partner.player_name,
                        target_player = partner,
                        source_name = sender.player_name,
                        source_player = sender,
                        record_message = record.record_message,
                        record_time = UtcNow()
                    });
                }
                else
                {
                    //Player is local, inform them of the conversation start/change.
                    if (partner.conversationPartner == null)
                    {
                        //No conversation partner exists. Inform of the new one.
                        if (PlayerIsAdmin(partner) && !PlayerIsAdmin(sender) && !adminInformedChange)
                        {
                            OnlineAdminSayMessage("Admin " + sender.GetVerboseName() + " is now in a private conversation with " + partner.GetVerboseName(), sender.player_name);
                            adminInformedChange = true;
                        }
                        else
                        {
                            partner.Say("You are now in a private conversation with " + sender.GetVerboseName() + ". Use /" + GetCommandByKey("player_pm_reply").command_text + " msg to reply.");
                        }
                        partner.conversationPartner = sender;
                    }
                    else
                    {
                        //Conversation partner exists. Cancel that conversation. Inform all parties.
                        APlayer oldPartner = partner.conversationPartner;

                        if (oldPartner.player_guid != sender.player_guid)
                        {
                            //Inform partner of change
                            partner.Say("Private conversation partner changed from " + oldPartner.GetVerboseName() + " to " + sender.GetVerboseName());

                            //Cancel oldPartner conversation
                            if (PlayerIsExternal(oldPartner))
                            {
                                QueueRecordForProcessing(new ARecord
                                {
                                    record_source = ARecord.Sources.Automated,
                                    server_id = oldPartner.player_server.ServerID,
                                    record_orchestrate = true,
                                    command_type = GetCommandByKey("player_pm_cancel"),
                                    command_numeric = 0,
                                    target_name = oldPartner.player_name,
                                    target_player = oldPartner,
                                    source_name = sender.player_name,
                                    source_player = sender,
                                    record_message = partner.GetVerboseName() + " has left the private conversation.",
                                    record_time = UtcNow()
                                });
                            }
                            else
                            {
                                PlayerSayMessage(oldPartner.player_name, partner.GetVerboseName() + " has left the private conversation.");
                                oldPartner.conversationPartner = null;
                            }
                        }
                        else
                        {
                            Log.Error("Code 14211: Inform ColColonCleaner");
                            return;
                        }
                    }
                    partner.conversationPartner = sender;
                }

                //Post the first message to the sender
                PlayerSayMessage(sender.player_name, "(MSG)(" + sender.player_name + "): " + record.record_message);
                //Post the first message to the partner
                if (!PlayerIsExternal(partner))
                {
                    partner.Say("(MSG)(" + sender.player_name + "): " + record.record_message);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Private Message record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PMSendTarget", 6);
        }

        public void PMReplyTarget(ARecord record)
        {
            Log.Debug(() => "Entering PMReplyTarget", 6);
            try
            {
                record.record_action_executed = true;
                APlayer sender = record.source_player;
                APlayer partner = record.target_player;
                if (PlayerIsExternal(partner))
                {
                    QueueRecordForProcessing(new ARecord
                    {
                        record_source = ARecord.Sources.Automated,
                        server_id = partner.player_server.ServerID,
                        record_orchestrate = true,
                        command_type = GetCommandByKey("player_pm_transmit"),
                        command_numeric = 0,
                        target_name = partner.player_name,
                        target_player = partner,
                        source_name = sender.player_name,
                        source_player = sender,
                        record_message = record.record_message,
                        record_time = UtcNow()
                    });
                }
                else
                {
                    partner.Say("(MSG)(" + sender.player_name + "): " + record.record_message);
                }
                PlayerSayMessage(sender.player_name, "(MSG)(" + sender.player_name + "): " + record.record_message);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Private Message Reply record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PMReplyTarget", 6);
        }

        public void PMStartTarget(ARecord record)
        {
            Log.Debug(() => "Entering PMStartTarget", 6);
            try
            {
                record.record_action_executed = true;

                APlayer sender = record.source_player;
                APlayer partner = record.target_player;
                Boolean adminInformedChange = false;

                //Sender may not be in this server
                sender = FetchMatchingExternalOnlinePlayer(sender.player_name);

                if (sender == null)
                {
                    return;
                }

                //Inform partner of the conversation start/change.
                if (partner.conversationPartner == null)
                {
                    //No conversation partner exists. Inform of the new one.
                    if (PlayerIsAdmin(partner) && !PlayerIsAdmin(sender) && !adminInformedChange)
                    {
                        OnlineAdminSayMessage("Admin " + sender.GetVerboseName() + " is now in a private conversation with " + partner.GetVerboseName(), sender.player_name);
                        adminInformedChange = true;
                    }
                    else
                    {
                        partner.Say("You are now in a private conversation with " + sender.GetVerboseName() + ". Use /" + GetCommandByKey("player_pm_reply").command_text + " msg to reply.");
                    }
                    partner.conversationPartner = sender;
                }
                else
                {
                    //Conversation partner exists. Cancel that conversation. Inform all parties.
                    APlayer oldPartner = partner.conversationPartner;

                    if (oldPartner.player_guid != sender.player_guid)
                    {
                        //Inform partner of change
                        partner.Say("Private conversation partner changed from " + oldPartner.GetVerboseName() + " to " + sender.GetVerboseName());

                        //Cancel oldPartner conversation
                        if (PlayerIsExternal(oldPartner))
                        {
                            QueueRecordForProcessing(new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = oldPartner.player_server.ServerID,
                                record_orchestrate = true,
                                command_type = GetCommandByKey("player_pm_cancel"),
                                command_numeric = 0,
                                target_name = oldPartner.player_name,
                                target_player = oldPartner,
                                source_name = sender.player_name,
                                source_player = sender,
                                record_message = partner.GetVerboseName() + " has left the private conversation.",
                                record_time = UtcNow()
                            });
                        }
                        else
                        {
                            PlayerSayMessage(oldPartner.player_name, partner.GetVerboseName() + " has left the private conversation.");
                            oldPartner.conversationPartner = null;
                        }
                    }
                    else
                    {
                        PlayerSayMessage(sender.player_name, "You are already in a private conversation with " + partner.GetVerboseName() + ". Use /" + GetCommandByKey("player_pm_reply").command_text + " msg to reply.");
                        return;
                    }
                }
                partner.conversationPartner = sender;

                partner.Say("(MSG)(" + sender.player_name + "): " + record.record_message);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Private Message Start record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PMStartTarget", 6);
        }

        public void PMCancelTarget(ARecord record)
        {
            Log.Debug(() => "Entering PMCancelTarget", 6);
            try
            {
                record.record_action_executed = true;

                APlayer sender = record.source_player;
                APlayer partner = record.target_player;

                if (partner.conversationPartner != null)
                {
                    partner.Say(record.record_message);
                    partner.conversationPartner = null;
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Private Message Cancel record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PMCancelTarget", 6);
        }

        public void PMTransmitTarget(ARecord record)
        {
            Log.Debug(() => "Entering PMTransmitTarget", 6);
            try
            {
                record.record_action_executed = true;

                APlayer sender = record.source_player;
                APlayer partner = record.target_player;

                //Sender may not be in this server
                sender = FetchMatchingExternalOnlinePlayer(sender.player_name);

                if (sender == null)
                {
                    return;
                }

                if (partner.conversationPartner == null || partner.conversationPartner.player_guid != sender.player_guid)
                {
                    //Cancel oldPartner conversation
                    if (PlayerIsExternal(sender))
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = sender.player_server.ServerID,
                            record_orchestrate = true,
                            command_type = GetCommandByKey("player_pm_cancel"),
                            command_numeric = 0,
                            target_name = sender.player_name,
                            target_player = sender,
                            source_name = partner.player_name,
                            source_player = partner,
                            record_message = partner.GetVerboseName() + " is not in a private conversation with you.",
                            record_time = UtcNow()
                        });
                    }
                    else
                    {
                        partner.Say(partner.GetVerboseName() + " is not in a private conversation with you.");
                        sender.conversationPartner = null;
                    }
                }
                else
                {
                    partner.Say("(MSG)(" + sender.player_name + "): " + record.record_message);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Private Message Transmit record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PMTransmitTarget", 6);
        }

        public void PMOnlineAdmins(ARecord record)
        {
            Log.Debug(() => "Entering PMAdmin", 6);
            try
            {
                record.record_action_executed = true;
                if (record.source_player != null && !PlayerIsAdmin(record.source_player))
                {
                    SendMessageToSource(record, "(MSG)(" + record.source_name + "): " + record.record_message);
                }
                OnlineAdminSayMessage("(MSG)(" + record.source_name + "): " + record.record_message);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for Private Message Admin record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PMAdmin", 6);
        }

        public void AdminYell(ARecord record)
        {
            Log.Debug(() => "Entering adminYell", 6);
            try
            {
                record.record_action_executed = true;
                AdminYellMessage(record.record_message);
                if (record.record_source != ARecord.Sources.InGame && record.record_source != ARecord.Sources.ServerCommand)
                {
                    SendMessageToSource(record, "Server has been told '" + record.record_message + "' by YELL");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for AdminYell record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting adminYell", 6);
        }

        public void PlayerYell(ARecord record)
        {
            Log.Debug(() => "Entering playerYell", 6);
            try
            {
                record.record_action_executed = true;
                PlayerYellMessage(record.target_player.player_name, record.record_message);
                if (record.record_source != ARecord.Sources.ServerCommand)
                {
                    SendMessageToSource(record, record.GetTargetNames() + " has been told '" + record.record_message + "' by YELL");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for playerYell record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting playerYell", 6);
        }

        public void AdminTell(ARecord record)
        {
            Log.Debug(() => "Entering adminTell", 6);
            try
            {
                record.record_action_executed = true;
                AdminTellMessage(record.record_message);
                if (record.record_source != ARecord.Sources.InGame && record.record_source != ARecord.Sources.ServerCommand)
                {
                    SendMessageToSource(record, "Server has been told '" + record.record_message + "' by TELL");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for AdminYell record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting adminTell", 6);
        }

        public void PlayerTell(ARecord record)
        {
            Log.Debug(() => "Entering playerTell", 6);
            try
            {
                record.record_action_executed = true;
                PlayerTellMessage(record.target_player.player_name, record.record_message);
                if (record.record_source != ARecord.Sources.ServerCommand)
                {
                    SendMessageToSource(record, record.GetTargetNames() + " has been told '" + record.record_message + "' by TELL");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for playerTell record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting playerTell", 6);
        }

        public void SendPopulationSuccess(ARecord record)
        {
            Log.Debug(() => "Entering SendPopulationSuccess", 6);
            try
            {
                record.record_action_executed = true;
                PlayerTellMessage(record.target_player.player_name, "Thank you for helping populate!");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while taking action for population success record.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendPopulationSuccess", 6);
        }

        public void SendServerRules(ARecord record)
        {
            Log.Debug(() => "Entering sendServerRules", 6);
            try
            {
                record.record_action_executed = true;
                //If server has rules
                if (_ServerRulesList.Length > 0)
                {
                    //If requesting rules on yourself as an admin, rules should be sent to the whole server.
                    Boolean sourceIsAdmin = ((record.source_player != null && PlayerIsAdmin(record.source_player) || record.source_player == null));
                    Boolean allPlayers = (sourceIsAdmin) && (record.target_player == null || record.target_name == record.source_name);
                    if (record.source_name != record.target_name)
                    {
                        if (!sourceIsAdmin)
                        {
                            SendMessageToSource(record, "Telling server rules to " + record.GetTargetNames());
                        }
                        OnlineAdminSayMessage(((sourceIsAdmin) ? ("Admin ") : ("")) + record.GetSourceName() + " told server rules to " + record.GetTargetNames() + ".");
                    }
                    else
                    {
                        OnlineAdminSayMessage(((sourceIsAdmin) ? ("Admin ") : ("")) + record.GetSourceName() + " requested server rules.");
                    }

                    Thread rulePrinter = new Thread(new ThreadStart(delegate
                    {
                        Log.Debug(() => "Starting a rule printer thread.", 5);
                        try
                        {
                            Thread.CurrentThread.Name = "RulePrinter";
                            //Wait the rule delay duration
                            Threading.Wait(TimeSpan.FromSeconds(_ServerRulesDelay));
                            Int32 ruleIndex = 0;
                            List<String> validRules = new List<String>();
                            if (_AvailableMapModes.Any() &&
                                _serverInfo.InfoObject != null &&
                                !String.IsNullOrEmpty(_serverInfo.InfoObject.Map) &&
                                _AvailableMapModes.FirstOrDefault(mapMode => mapMode.FileName == _serverInfo.InfoObject.Map &&
                                                                             mapMode.PlayList == _serverInfo.InfoObject.GameMode) != null)
                            {
                                //Confirm that rule prefixes conform to the map/modes available
                                var allMaps = _AvailableMapModes.Where(mapMode => !String.IsNullOrEmpty(mapMode.PublicLevelName))
                                                                .Select(mapMode => mapMode.PublicLevelName).Distinct().ToArray();
                                var allModes = _AvailableMapModes.Where(mapMode => !String.IsNullOrEmpty(mapMode.GameMode))
                                                                 .Select(mapMode => mapMode.GameMode).Distinct().ToArray();
                                var matchingMapMode = _AvailableMapModes.First(mapMode => mapMode.FileName == _serverInfo.InfoObject.Map &&
                                                                                          mapMode.PlayList == _serverInfo.InfoObject.GameMode);
                                var serverMap = matchingMapMode.PublicLevelName;
                                var serverMode = matchingMapMode.GameMode;
                                foreach (var rule in _ServerRulesList.Where(rule => !String.IsNullOrEmpty(rule)))
                                {
                                    // Need to pull rule into a new var since foreach vars can't be modified
                                    var ruleString = rule;
                                    var useRule = true;
                                    //Check if the rule starts with any map
                                    foreach (var ruleMap in allMaps)
                                    {
                                        if (ruleString.StartsWith(ruleMap + "/"))
                                        {
                                            //Remove the map from the rule text
                                            ruleString = TrimStart(ruleString, ruleMap + "/");
                                            if (ruleMap != serverMap)
                                            {
                                                useRule = false;
                                            }
                                            break;
                                        }
                                    }
                                    //Check if the rule starts with any mode
                                    foreach (var ruleMode in allModes)
                                    {
                                        if (ruleString.StartsWith(ruleMode + "/"))
                                        {
                                            //Remove the mode from the rule text
                                            ruleString = TrimStart(ruleString, ruleMode + "/");
                                            if (ruleMode != serverMode)
                                            {
                                                useRule = false;
                                            }
                                            break;
                                        }
                                    }
                                    //Check again for maps, since they might have put them in a different order
                                    foreach (var ruleMap in allMaps)
                                    {
                                        if (ruleString.StartsWith(ruleMap + "/"))
                                        {
                                            //Remove the map from the rule text
                                            ruleString = TrimStart(ruleString, ruleMap + "/");
                                            if (ruleMap != serverMap)
                                            {
                                                useRule = false;
                                            }
                                            break;
                                        }
                                    }
                                    if (useRule)
                                    {
                                        validRules.Add(ruleString);
                                    }
                                }
                            }
                            else
                            {
                                foreach (var rule in _ServerRulesList.Where(rule => !String.IsNullOrEmpty(rule)))
                                {
                                    validRules.Add(rule);
                                }
                            }
                            foreach (string rule in validRules)
                            {
                                String currentPrefix = (_ServerRulesNumbers) ? ("(" + (++ruleIndex) + "/" + validRules.Count() + ") ") : ("");
                                if (allPlayers)
                                {
                                    AdminSayMessage(currentPrefix + GetPreMessage(rule, false));
                                }
                                else
                                {
                                    if (record.target_player != null)
                                    {
                                        record.target_player.PrintThreadID = Thread.CurrentThread.Name;
                                        if (_ServerRulesYell)
                                        {
                                            PlayerTellMessage(record.target_player.player_name, currentPrefix + GetPreMessage(rule, false));
                                        }
                                        else
                                        {
                                            PlayerSayMessage(record.target_player.player_name, currentPrefix + GetPreMessage(rule, false));
                                        }
                                    }
                                    else
                                    {
                                        SendMessageToSource(record, currentPrefix + GetPreMessage(rule, false));
                                    }
                                }
                                Threading.Wait(TimeSpan.FromSeconds(_ServerRulesInterval));
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error while printing server rules", e));
                        }
                        Log.Debug(() => "Exiting a rule printer.", 5);
                        Threading.StopWatchdog();
                    }));

                    //Start the thread
                    Threading.StartWatchdog(rulePrinter);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending server rules.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting sendServerRules", 6);
        }

        public void SourceVoteSurrender(ARecord record)
        {
            Log.Debug(() => "Entering SourceVoteSurrender", 6);
            try
            {
                record.record_action_executed = true;

                if (EventActive())
                {
                    SendMessageToSource(record, "Surrender Vote is not available during events.");
                    FinalizeRecord(record);
                    return;
                }

                //Case for database added records
                if (!_surrenderVoteEnable)
                {
                    SendMessageToSource(record, "Surrender Vote must be enabled in AdKats settings to use this command.");
                    FinalizeRecord(record);
                    return;
                }
                if (_roundState != RoundState.Playing)
                {
                    SendMessageToSource(record, "Round state must be playing to use surrender. Current: " + _roundState);
                    FinalizeRecord(record);
                    return;
                }
                if (_surrenderVoteSucceeded)
                {
                    SendMessageToSource(record, "Surrender already succeeded.");
                    FinalizeRecord(record);
                    return;
                }
                if (_surrenderVoteList.Contains(record.source_name))
                {
                    SendMessageToSource(record, "You already voted! You can cancel your vote with " + GetChatCommandByKey("command_cancel"));
                    FinalizeRecord(record);
                    return;
                }
                bool voteEnabled = false;
                ATeam team1, team2;
                if (!GetTeamByID(1, out team1))
                {
                    if (_roundState == RoundState.Playing)
                    {
                        Log.Error("Teams not loaded when they should be.");
                    }
                    return;
                }
                if (!GetTeamByID(2, out team2))
                {
                    if (_roundState == RoundState.Playing)
                    {
                        Log.Error("Teams not loaded when they should be.");
                    }
                    return;
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
                if (!_surrenderVoteActive)
                {
                    Int32 playerCount = GetPlayerCount();
                    if (playerCount < _surrenderVoteMinimumPlayerCount)
                    {
                        SendMessageToSource(record, _surrenderVoteMinimumPlayerCount + " players needed to start Surrender Vote. Current: " + playerCount);
                        FinalizeRecord(record);
                        return;
                    }
                    Int32 ticketGap = Math.Abs(team1.TeamTicketCount - team2.TeamTicketCount);
                    if (ticketGap < _surrenderVoteMinimumTicketGap)
                    {
                        SendMessageToSource(record, _surrenderVoteMinimumTicketGap + " ticket gap needed to start Surrender Vote. Current: " + ticketGap);
                        FinalizeRecord(record);
                        return;
                    }
                    Double ticketRateGap = Math.Abs(team1.GetTicketDifferenceRate() - team2.GetTicketDifferenceRate());
                    if (_surrenderVoteTicketRateGapEnable && ticketRateGap < _surrenderVoteMinimumTicketRateGap)
                    {
                        SendMessageToSource(record, _surrenderVoteMinimumTicketRateGap + " ticket rate gap needed to start Surrender Vote. Current: " + Math.Round(ticketRateGap, 2));
                        FinalizeRecord(record);
                        return;
                    }

                    _surrenderVoteActive = true;
                    voteEnabled = true;
                    _surrenderVoteStartTime = UtcNow();
                    if (_surrenderVoteTimeoutEnable)
                    {
                        Thread surrenderTimingThread = new Thread(new ThreadStart(delegate
                        {
                            Log.Debug(() => "Starting a surrender timing thread.", 5);
                            try
                            {
                                while (_pluginEnabled && (UtcNow() - _surrenderVoteStartTime).TotalMinutes < _surrenderVoteTimeoutMinutes && !_surrenderVoteSucceeded && _surrenderVoteActive)
                                {
                                    Threading.Wait(500);
                                }
                                if (!_surrenderVoteSucceeded && _roundState == RoundState.Playing && _pluginEnabled)
                                {
                                    _surrenderVoteActive = false;
                                    _surrenderVoteList.Clear();
                                    AdminTellMessage("Surrender Vote Timed Out. Votes removed.");
                                }
                            }
                            catch (Exception)
                            {
                                Log.HandleException(new AException("Error while running surrender timing."));
                            }
                            Log.Debug(() => "Exiting a surrender timing thread.", 5);
                            Threading.StopWatchdog();
                        }));
                        Threading.StartWatchdog(surrenderTimingThread);
                    }
                }

                //Remove nosurrender vote if any
                _nosurrenderVoteList.Remove(record.source_name);
                //Add the vote
                _surrenderVoteList.Add(record.source_name);
                Int32 requiredVotes = (Int32)((GetPlayerCount() / 2.0) * (_surrenderVoteMinimumPlayerPercentage / 100.0));
                Int32 voteCount = _surrenderVoteList.Count - _nosurrenderVoteList.Count;
                if (voteCount >= requiredVotes)
                {
                    //Vote succeeded, trigger winning team
                    _surrenderVoteSucceeded = true;
                    if (!_endingRound)
                    {
                        _endingRound = true;
                        Thread roundEndDelayThread = new Thread(new ThreadStart(delegate
                        {
                            Log.Debug(() => "Starting a round end delay thread.", 5);
                            try
                            {
                                Thread.CurrentThread.Name = "RoundEndDelay";
                                for (int i = 0; i < 8; i++)
                                {
                                    AdminTellMessage("Surrender Vote Succeeded. " + winningTeam.TeamName + " wins!");
                                    Thread.Sleep(50);
                                }
                                Threading.Wait(7000);
                                ARecord repRecord = new ARecord
                                {
                                    record_source = ARecord.Sources.Automated,
                                    server_id = _serverInfo.ServerID,
                                    command_type = GetCommandByKey("round_end"),
                                    command_numeric = winningTeam.TeamID,
                                    target_name = winningTeam.TeamName,
                                    source_name = "RoundManager",
                                    record_message = "Surrender Vote (" + winningTeam.GetTeamIDKey() + " Win)(" + winningTeam.TeamTicketCount + ":" + losingTeam.TeamTicketCount + ")(" + FormatTimeString(_serverInfo.GetRoundElapsedTime(), 3) + ")",
                                    record_time = UtcNow()
                                };
                                QueueRecordForProcessing(repRecord);
                            }
                            catch (Exception)
                            {
                                Log.HandleException(new AException("Error while running round end delay."));
                            }
                            Log.Debug(() => "Exiting a round end delay thread.", 5);
                            Threading.StopWatchdog();
                        }));
                        Threading.StartWatchdog(roundEndDelayThread);
                    }
                }
                else
                {
                    SendMessageToSource(record, "You voted to end the round!");
                    if (voteEnabled)
                    {
                        AdminTellMessage("Surrender Vote started! Use " + GetChatCommandByKey("self_surrender") + ", " + GetChatCommandByKey("self_votenext") + ", or " + GetChatCommandByKey("self_nosurrender") + " to vote.");
                    }
                    else
                    {
                        AdminSayMessage((requiredVotes - voteCount) + " votes needed for surrender/scramble. Use " + GetChatCommandByKey("self_surrender") + ", " + GetChatCommandByKey("self_votenext") + ", or " + GetChatCommandByKey("self_nosurrender") + " to vote.");
                        AdminYellMessage((requiredVotes - voteCount) + " votes needed for surrender/scramble");
                    }
                    OnlineAdminSayMessage(record.GetSourceName() + " voted for round surrender.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while voting surrender.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SourceVoteSurrender", 6);
        }

        public void SourceVoteNoSurrender(ARecord record)
        {
            Log.Debug(() => "Entering SourceVoteNoSurrender", 6);
            try
            {
                record.record_action_executed = true;

                if (EventActive())
                {
                    SendMessageToSource(record, "Surrender Vote is not available during events.");
                    FinalizeRecord(record);
                    return;
                }

                //Case for database added records
                if (!_surrenderVoteEnable)
                {
                    SendMessageToSource(record, "Surrender Vote must be enabled in AdKats settings to use this command.");
                    FinalizeRecord(record);
                    return;
                }
                if (_roundState != RoundState.Playing)
                {
                    SendMessageToSource(record, "Round state must be playing to vote against surrender. Current: " + _roundState);
                    FinalizeRecord(record);
                    return;
                }
                if (_surrenderVoteSucceeded)
                {
                    SendMessageToSource(record, "Surrender already succeeded.");
                    FinalizeRecord(record);
                    return;
                }
                if (_nosurrenderVoteList.Contains(record.source_name))
                {
                    SendMessageToSource(record, "You already voted against surrender!");
                    FinalizeRecord(record);
                    return;
                }
                if (!_surrenderVoteActive)
                {
                    SendMessageToSource(record, "A surrender vote must be active to vote against it.");
                    FinalizeRecord(record);
                    return;
                }

                //Remove surrender vote if any
                _surrenderVoteList.Remove(record.source_name);
                //Add the vote
                _nosurrenderVoteList.Add(record.source_name);
                Int32 requiredVotes = (Int32)((GetPlayerCount() / 2.0) * (_surrenderVoteMinimumPlayerPercentage / 100.0));
                Int32 voteCount = _surrenderVoteList.Count - _nosurrenderVoteList.Count;
                SendMessageToSource(record, "You voted against ending the round!");
                AdminSayMessage((requiredVotes - voteCount) + " votes needed for surrender/scramble. Use " + GetChatCommandByKey("self_surrender") + ", " + GetChatCommandByKey("self_votenext") + ", or " + GetChatCommandByKey("self_nosurrender") + " to vote.");
                AdminYellMessage((requiredVotes - voteCount) + " votes needed for surrender/scramble");
                OnlineAdminSayMessage(record.GetSourceName() + " voted against round surrender.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while voting against surrender.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SourceVoteNoSurrender", 6);
        }

        public void SendServerCommands(ARecord record)
        {
            Log.Debug(() => "Entering SendServerCommands", 6);
            try
            {
                record.record_action_executed = true;
                Thread commandPrinter = new Thread(new ThreadStart(delegate
                {
                    Log.Debug(() => "Starting a command printer thread.", 5);
                    try
                    {
                        Thread.CurrentThread.Name = "CommandPrinter";

                        List<string> fullCommandList = new List<String>();
                        foreach (ACommand aCommand in _CommandIDDictionary.Values.ToList().Where(dCommand => dCommand.command_active == ACommand.CommandActive.Active))
                        {
                            if (record.target_player == null || HasAccess(record.target_player, aCommand))
                            {
                                fullCommandList.Add("!" + aCommand.command_text);
                            }
                        }
                        if (record.target_player == null || PlayerIsAdmin(record.target_player))
                        {
                            fullCommandList.AddRange(_ExternalAdminCommands);
                        }
                        else
                        {
                            fullCommandList.AddRange(_ExternalPlayerCommands);
                        }
                        List<List<String>> commandSplits = fullCommandList.Select((x, i) => new
                        {
                            Index = i,
                            Value = x
                        }).GroupBy(x => x.Index / 5).Select(x => x.Select(v => v.Value).ToList()).ToList();

                        foreach (List<string> curCommands in commandSplits)
                        {
                            String curCommandsStr = "";
                            foreach (String cur in curCommands)
                            {
                                curCommandsStr += cur + ", ";
                            }
                            SendMessageToSource(record, curCommandsStr);
                            Threading.Wait(TimeSpan.FromSeconds(2));
                        }
                    }
                    catch (Exception)
                    {
                        Log.HandleException(new AException("Error while printing server commands"));
                    }
                    Log.Debug(() => "Exiting a command printer.", 5);
                    Threading.StopWatchdog();
                }));

                //Start the thread
                Threading.StartWatchdog(commandPrinter);

                if (record.source_name != record.target_name)
                {
                    SendMessageToSource(record, "Telling server commands to " + record.GetTargetNames());
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending server commands.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendServerCommands", 6);
        }

        public void SendTargetRep(ARecord record)
        {
            Log.Debug(() => "Entering SendTargetRep", 6);
            try
            {
                record.record_action_executed = true;
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "Reputation fetch player not found, unable to continue.");
                    FinalizeRecord(record);
                    return;
                }
                record.command_numeric = (Int32)record.target_player.player_reputation;
                record.record_message = record.target_player.player_name + "'s reputation is " + Math.Round(record.target_player.player_reputation, 2);
                var isAdmin = PlayerIsAdmin(record.target_player);
                var points = FetchPoints(record.target_player, false, true);
                if (record.source_name == record.target_name)
                {
                    String repMessage = "Your server reputation is " + Math.Round(record.target_player.player_reputation, 2) + ", with ";
                    if (points > 0)
                    {
                        repMessage += points + " infraction point(s). ";
                    }
                    else
                    {
                        repMessage += "a clean infraction record. ";
                    }
                    if (!isAdmin && points < 1 && record.target_player.player_reputation > _reputationThresholdBad)
                    {
                        repMessage += "Thank you for helping the admins!";
                    }
                    if (points > 0 && _AutomaticForgives)
                    {
                        var forgiveTime = UtcNow();
                        if (record.target_player.LastForgive != null)
                        {
                            var forgiveDiff = record.target_player.LastForgive.record_time.AddDays(_AutomaticForgiveLastForgiveDays);
                            if (forgiveDiff > forgiveTime)
                            {
                                forgiveTime = forgiveDiff;
                            }
                        }
                        if (record.target_player.LastPunishment != null)
                        {
                            var punishDiff = record.target_player.LastPunishment.record_time.AddDays(_AutomaticForgiveLastPunishDays);
                            if (punishDiff > forgiveTime)
                            {
                                forgiveTime = punishDiff;
                            }
                        }
                        repMessage += Environment.NewLine + "Next auto-forgive after you spawn " + FormatNowDuration(forgiveTime, 2) + " from now.";
                    }
                    SendMessageToSource(record, repMessage);
                }
                else
                {
                    String repMessage = record.GetTargetNames() + "'s server reputation is " + Math.Round(record.target_player.player_reputation, 2) + ", with ";
                    if (points > 0)
                    {
                        repMessage += points + " infraction point(s). ";
                    }
                    else
                    {
                        repMessage += "a clean infraction record. ";
                    }
                    SendMessageToSource(record, repMessage);
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending server rep.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendTargetRep", 6);
        }

        public void SendTargetIsAdmin(ARecord record)
        {
            Log.Debug(() => "Entering SendTargetIsAdmin", 6);
            try
            {
                record.record_action_executed = true;
                if (record.target_player == null)
                {
                    SendMessageToSource(record, "Player not found, unable to continue.");
                    FinalizeRecord(record);
                    return;
                }
                if (record.source_name == record.target_name)
                {
                    SendMessageToSource(record, "You are " + ((PlayerIsAdmin(record.source_player)) ? ("") : ("not ")) + "an admin. [" + record.source_player.player_role.role_name + "]");
                }
                else
                {
                    SendMessageToSource(record, record.target_player.GetVerboseName() + " is " + ((PlayerIsAdmin(record.target_player)) ? ("") : ("not ")) + "an admin. [" + record.target_player.player_role.role_name + "]");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending admin status.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendTargetIsAdmin", 6);
        }

        public void SendUptime(ARecord record)
        {
            Log.Debug(() => "Entering SendUptime", 6);
            try
            {
                record.record_action_executed = true;
                Thread uptimePrinter = new Thread(new ThreadStart(delegate
                {
                    Log.Debug(() => "Starting a uptime printer thread.", 5);
                    try
                    {
                        Thread.CurrentThread.Name = "UptimePrinter";
                        SendMessageToSource(record, "Server: " + FormatTimeString(TimeSpan.FromSeconds(_serverInfo.InfoObject.ServerUptime), 10));
                        Threading.Wait(3000);
                        SendMessageToSource(record, "Procon: " + FormatNowDuration(_proconStartTime, 10));
                        Threading.Wait(3000);
                        SendMessageToSource(record, "AdKats " + PluginVersion + ": " + FormatNowDuration(_AdKatsRunningTime, 10));
                        Threading.Wait(3000);
                        SendMessageToSource(record, "Last Player List: " + FormatNowDuration(_LastPlayerListProcessed, 10) + " ago");
                        Threading.Wait(3000);
                        SendMessageToSource(record, "Server has been in " + _populationStatus.ToString().ToLower() + " population for " + FormatNowDuration(_populationTransitionTime, 3));
                        Double totalPopulationDuration = _populationDurations[PopulationState.Low].TotalSeconds + _populationDurations[PopulationState.Medium].TotalSeconds + _populationDurations[PopulationState.High].TotalSeconds;
                        if (totalPopulationDuration > 0)
                        {
                            Threading.Wait(5000);
                            Int32 lowPopPercentage = (int)Math.Round(_populationDurations[PopulationState.Low].TotalSeconds / totalPopulationDuration * 100);
                            Int32 medPopPercentage = (int)Math.Round(_populationDurations[PopulationState.Medium].TotalSeconds / totalPopulationDuration * 100);
                            Int32 highPopPercentage = (int)Math.Round(_populationDurations[PopulationState.High].TotalSeconds / totalPopulationDuration * 100);
                            SendMessageToSource(record, "Population since AdKats start: " + lowPopPercentage + "% low. " + medPopPercentage + "% medium. " + highPopPercentage + "% high.");
                        }
                    }
                    catch (Exception)
                    {
                        Log.HandleException(new AException("Error while printing uptime"));
                    }
                    Log.Debug(() => "Exiting a uptime printer.", 5);
                    Threading.StopWatchdog();
                }));

                //Start the thread
                Threading.StartWatchdog(uptimePrinter);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending uptime.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendUptime", 6);
        }

        public void SendPlayerReports(ARecord record)
        {
            Log.Debug(() => "Entering SendPlayerReports", 6);
            try
            {
                record.record_action_executed = true;
                List<ARecord> lastMissedReports = FetchActivePlayerReports().OrderByDescending(aRecord => aRecord.record_time).Take(6).Reverse().ToList();
                Boolean listed = false;
                foreach (ARecord rRecord in lastMissedReports)
                {
                    String location;
                    if (rRecord.target_player.player_online)
                    {
                        location = "/" + GetPlayerTeamKey(rRecord.target_player) + "/" + (_PlayerDictionary.Values.Where(aPlayer => aPlayer.fbpInfo.TeamID == rRecord.target_player.fbpInfo.TeamID).OrderBy(aPlayer => aPlayer.fbpInfo.Score).Reverse().ToList().IndexOf(rRecord.target_player) + 1);
                    }
                    else
                    {
                        location = "";
                    }
                    SendMessageToSource(record, "(" + rRecord.command_numeric + ")(" + FormatTimeString(UtcNow() - rRecord.record_time, 2) + ")(" + rRecord.GetTargetNames() + location + "):" + rRecord.record_message);
                    Thread.Sleep(30);
                    listed = true;
                }
                if (!listed)
                {
                    SendMessageToSource(record, "No missed player reports were found.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending player reports.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendPlayerReports", 6);
        }

        public void RebootPlugin(ARecord record)
        {
            Log.Debug(() => "Entering RebootPlugin", 6);
            try
            {
                record.record_action_executed = true;
                _pluginRebootOnDisable = true;
                if (record.record_source == ARecord.Sources.InGame)
                {
                    _pluginRebootOnDisableSource = record.source_name;
                }
                SendMessageToSource(record, "Rebooting AdKats shortly.");
                //Run the reboot delay thread
                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                {
                    Thread.CurrentThread.Name = "RebootDelay";
                    Thread.Sleep(10000);
                    Disable();
                    Threading.StopWatchdog();
                })));
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while rebooting plugin.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting RebootPlugin", 6);
        }

        public void UpdatePlugin(ARecord record)
        {
            Log.Debug(() => "Entering UpdatePlugin", 6);
            try
            {
                record.record_action_executed = true;
                _pluginUpdateCaller = record;
                CheckForPluginUpdates(true);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while rebooting plugin.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting UpdatePlugin", 6);
        }

        public void ShutdownServer(ARecord record)
        {
            Log.Debug(() => "Entering ShutdownServer", 6);
            try
            {
                record.record_action_executed = true;
                Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                {
                    Thread.CurrentThread.Name = "ShutdownRunner";
                    AdminTellMessage("SERVER RESTART IN 5...");
                    Thread.Sleep(1000);
                    AdminTellMessage("SERVER RESTART IN 4...");
                    Thread.Sleep(1000);
                    AdminTellMessage("SERVER RESTART IN 3...");
                    Thread.Sleep(1000);
                    AdminTellMessage("SERVER RESTART IN 2...");
                    Thread.Sleep(1000);
                    AdminTellMessage("SERVER RESTART IN 1...");
                    Thread.Sleep(1000);
                    AdminTellMessage("REBOOTING SERVER");
                    ExecuteCommand("procon.protected.send", "admin.shutDown");
                    if (record.source_name == "AutoAdmin" &&
                        _automaticServerRestart &&
                        _automaticServerRestartProcon)
                    {
                        // Wait 30 seconds for the server to reboot
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        Environment.Exit(2232);
                    }
                    Threading.StopWatchdog();
                })));
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while shutting down server.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ShutdownServer", 6);
        }

        public void SendTargetInfo(ARecord record)
        {
            Log.Debug(() => "Entering SendTargetInfo", 6);
            try
            {
                record.record_action_executed = true;
                Thread infoPrinter = new Thread(new ThreadStart(delegate
                {
                    Log.Debug(() => "Starting a player info printer thread.", 5);
                    try
                    {
                        Thread.CurrentThread.Name = "PlayerInfoPrinter";
                        if (record.target_player == null)
                        {
                            Log.Error("Player null in player info printer.");
                            return;
                        }
                        Threading.Wait(500);
                        String playerInfo = record.target_player.GetVerboseName() + ": " + record.target_player.player_id + ", " + record.target_player.player_role.role_name;
                        if (record.target_player != null && record.target_player.fbpInfo != null)
                        {
                            if (record.target_player.player_online)
                            {
                                playerInfo += ", " + GetPlayerTeamName(record.target_player) + "/" + (_PlayerDictionary.Values.Where(aPlayer => aPlayer.fbpInfo.TeamID == record.target_player.fbpInfo.TeamID).OrderBy(aPlayer => aPlayer.fbpInfo.Score).Reverse().ToList().IndexOf(record.target_player) + 1) + "/" + record.target_player.fbpInfo.Score;
                            }
                            else
                            {
                                playerInfo += ", OFFLINE";
                            }
                        }
                        SendMessageToSource(record, playerInfo);
                        Threading.Wait(2000);
                        SendMessageToSource(record, "First seen: " + FormatTimeString(UtcNow() - record.target_player.player_firstseen, 3) + " ago.");
                        Threading.Wait(2000);
                        if (_PlayerDictionary.ContainsKey(record.target_player.player_name))
                        {
                            var duration = record.target_player.player_serverplaytime + NowDuration(record.target_player.JoinTime);
                            SendMessageToSource(record, "Time on server: " + Math.Round(duration.TotalHours, 1) + "hrs (" + FormatTimeString(duration, 3) + ").");
                        }
                        else
                        {
                            var duration = record.target_player.player_serverplaytime;
                            SendMessageToSource(record, "Time on server: " + Math.Round(duration.TotalHours, 1) + "hrs (" + FormatTimeString(duration, 3) + ").");
                        }
                        Threading.Wait(2000);
                        String playerLoc = "Unknown";
                        if (!String.IsNullOrEmpty(record.target_player.player_ip))
                        {
                            IPLocation loc = record.target_player.location;
                            if (loc != null && loc.status == "success")
                            {
                                playerLoc = String.Empty;
                                if (!String.IsNullOrEmpty(loc.city))
                                {
                                    playerLoc += loc.city + ", ";
                                }
                                if (!String.IsNullOrEmpty(loc.regionName))
                                {
                                    playerLoc += loc.regionName + ", ";
                                }
                                playerLoc += loc.country;
                                List<ARecord> locRecords = FetchRecentRecords(record.target_player.player_id, GetCommandByKey("player_changeip").command_id, 1000, 50, true, false).Where(aRecord => aRecord.record_message != "No previous IP on record").ToList();
                                if (locRecords.Any())
                                {
                                    playerLoc += " with " + locRecords.GroupBy(locRecord => locRecord.record_message).Select(group => group.First()).Count() + " different IPs.";
                                }
                            }
                        }
                        else
                        {
                            playerLoc = "Player IP not found";
                        }
                        SendMessageToSource(record, "Location: " + playerLoc);
                        Threading.Wait(2000);
                        IEnumerable<ARecord> reportsFrom = _PlayerReports.Where(aRecord => aRecord.source_name == record.target_name);
                        IEnumerable<ARecord> reportsAgainst = _PlayerReports.Where(aRecord => aRecord.target_name == record.target_name);
                        String playerReps = "None from or against.";
                        if (reportsAgainst.Any() || reportsFrom.Any())
                        {
                            playerReps = String.Empty;
                            if (reportsAgainst.Any())
                            {
                                playerReps += "[" + reportsAgainst.Count() + " against:";
                                playerReps = reportsAgainst.Aggregate(playerReps, (current, playerRep) => current + (" (" + ((playerRep.isContested) ? ("-CONTESTED- ") : ("")) + playerRep.record_message + ")"));
                                playerReps += "]";
                            }
                            if (reportsFrom.Any())
                            {
                                playerReps += "[" + reportsFrom.Count() + " from:";
                                playerReps = reportsFrom.Aggregate(playerReps, (current, playerRep) => current + (" (" + ((playerRep.isContested) ? ("-CONTESTED- ") : ("")) + playerRep.record_message + ")"));
                                playerReps += "]";
                            }
                        }
                        SendMessageToSource(record, "Reports: " + playerReps);
                        Threading.Wait(2000);
                        //Infraction Points
                        String playerInf = "Player in good standing.";
                        Int64 infPoints = FetchPoints(record.target_player, false, true);
                        if (infPoints > 0)
                        {
                            playerInf = infPoints + " points.";
                        }
                        SendMessageToSource(record, "Infractions: " + playerInf);
                        Threading.Wait(2000);
                        //Last Punishment
                        String lastPunishText = "No punishments found.";
                        List<ARecord> punishments = FetchRecentRecords(record.target_player.player_id, GetCommandByKey("player_punish").command_id, 1000, 1, true, false);
                        if (punishments.Any())
                        {
                            ARecord lastPunish = punishments[0];
                            lastPunishText = FormatTimeString(UtcNow() - lastPunish.record_time, 2) + " ago by " + lastPunish.GetSourceName() + ": " + lastPunish.record_message;
                        }
                        SendMessageToSource(record, "Last Punishment: " + lastPunishText);
                        Threading.Wait(2000);
                        //Last Forgive
                        String lastForgiveText = "No forgives found.";
                        List<ARecord> forgives = FetchRecentRecords(record.target_player.player_id, GetCommandByKey("player_forgive").command_id, 1000, 1, true, false);
                        if (forgives.Any())
                        {
                            ARecord lastForgive = forgives[0];
                            lastForgiveText = FormatTimeString(UtcNow() - lastForgive.record_time, 2) + " ago by " + lastForgive.GetSourceName() + ": " + lastForgive.record_message;
                        }
                        SendMessageToSource(record, "Last Forgive: " + lastForgiveText);
                        Threading.Wait(2000);
                        //Rules requests
                        String rulesRequestsText = "Player has never requested rules.";
                        List<ARecord> rulesRequests = FetchRecentRecords(record.target_player.player_id, GetCommandByKey("self_rules").command_id, 1000, 50, true, false);
                        if (rulesRequests.Any(innerRecord => innerRecord.source_player != null && innerRecord.source_player.player_id == record.target_player.player_id))
                        {
                            ARecord lastRulesRequest = rulesRequests[0];
                            rulesRequestsText = FormatTimeString(UtcNow() - lastRulesRequest.record_time, 2) + " ago.";
                        }
                        SendMessageToSource(record, "Last Rules Request: " + rulesRequestsText);
                        Threading.Wait(2000);
                        //Ping Kicks
                        String pingKicksText = "Player never kicked for ping.";
                        IEnumerable<ARecord> pingKicks = FetchRecentRecords(record.target_player.player_id, GetCommandByKey("player_kick").command_id, 1000, 50, true, false).Where(innerRecord => innerRecord.source_name == "PingEnforcer");
                        if (pingKicks.Any())
                        {
                            pingKicksText = "Kicked " + pingKicks.Count() + " time(s) for high ping.";
                        }
                        SendMessageToSource(record, "Ping Kicks: " + pingKicksText + " Current Ping [" + ((record.target_player.player_ping_avg > 0) ? (Math.Round(record.target_player.player_ping_avg, 2).ToString()) : ("Missing")) + "].");
                        Threading.Wait(2000);
                        //Reputation
                        SendMessageToSource(record, "Reputation: " + Math.Round(record.target_player.player_reputation, 2));
                        Threading.Wait(2000);
                        //Previous Names
                        String playerNames = record.target_player.player_name;
                        List<ARecord> nameRecords = FetchRecentRecords(record.target_player.player_id, GetCommandByKey("player_changename").command_id, 1000, 50, true, false).GroupBy(nameRecord => nameRecord.record_message).Select(group => group.First()).ToList();
                        if (nameRecords.Any(rec => !String.IsNullOrEmpty(rec.record_message)))
                        {
                            var previousNames = nameRecords.Where(rec => !String.IsNullOrEmpty(rec.record_message) && rec.record_message != record.target_player.player_name).Select(rec => rec.record_message).ToArray();
                            playerNames += ", " + String.Join(", ", previousNames);
                        }
                        SendMessageToSource(record, "Player names: " + playerNames);
                        //Previous Tags
                        String playerTags = "";
                        List<String> playerTagList = new List<String>();
                        if (!String.IsNullOrEmpty(record.target_player.player_clanTag))
                        {
                            playerTagList.Add(record.target_player.player_clanTag);
                        }
                        List<ARecord> tagRecords = FetchRecentRecords(record.target_player.player_id, GetCommandByKey("player_changetag").command_id, 1000, 50, true, false).GroupBy(tagRecord => tagRecord.record_message).Select(group => group.First()).ToList();
                        var previousTags = tagRecords.Where(rec => !String.IsNullOrEmpty(rec.record_message) && rec.record_message != record.target_player.player_clanTag).Select(rec => rec.record_message).ToList();
                        playerTagList.AddRange(previousTags);
                        playerTagList = playerTagList.Distinct().ToList();
                        if (playerTagList.Any())
                        {
                            playerTags = String.Join(", ", playerTagList.ToArray());
                        }
                        if (String.IsNullOrEmpty(playerTags))
                        {
                            playerTags = "No clan tags.";
                        }
                        SendMessageToSource(record, "Player tags: " + playerTags);
                    }
                    catch (Exception)
                    {
                        Log.HandleException(new AException("Error while printing player info"));
                    }
                    Log.Debug(() => "Exiting a player info printer.", 5);
                    Threading.StopWatchdog();
                }));

                //Start the thread
                Threading.StartWatchdog(infoPrinter);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending player info.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendTargetInfo", 6);
        }

        public void SendTargetPerks(ARecord record)
        {
            Log.Debug(() => "Entering SendTargetPerks", 6);
            try
            {
                record.record_action_executed = true;
                Thread perkPrinter = new Thread(new ThreadStart(delegate
                {
                    Log.Debug(() => "Starting a player perk printer thread.", 5);
                    try
                    {
                        Thread.CurrentThread.Name = "PlayerPerkPrinter";
                        if (record.target_player == null)
                        {
                            Log.Error("Player null in player perk printer.");
                            return;
                        }
                        Threading.Wait(500);
                        var asPlayers = GetMatchingVerboseASPlayers(record.target_player);
                        if (!asPlayers.Any())
                        {
                            if (record.source_name == record.target_name)
                            {
                                SendMessageToSource(record, "You do not have any active perks. Contact your admin for more information!");
                            }
                            else
                            {
                                SendMessageToSource(record, record.target_player.GetVerboseName() + " does not have any active perks.");
                            }
                            FinalizeRecord(record);
                            return;
                        }
                        if (record.source_name == record.target_name)
                        {
                            SendMessageToSource(record, "Showing your active perks:");
                        }
                        else
                        {
                            SendMessageToSource(record, "Showing " + record.target_player.GetVerboseName() + "'s active perks:");
                        }
                        foreach (var groupKey in _PerkSpecialPlayerGroups)
                        {
                            var asPlayer = asPlayers.FirstOrDefault(dPlayer => dPlayer.player_group.group_key == groupKey);
                            if (asPlayer != null)
                            {
                                Threading.Wait(1000);
                                var expireDuration = NowDuration(asPlayer.player_expiration);
                                String expiration = (expireDuration.TotalDays > 500.0) ? ("Permanent") : (FormatTimeString(expireDuration, 3));
                                var groupName = !String.IsNullOrEmpty(asPlayer.tempCreationType) ? asPlayer.tempCreationType : asPlayer.player_group.group_name;
                                SendMessageToSource(record, groupName + ": " + expiration);
                                if (groupKey == "slot_reserved" &&
                                    record.target_player != null &&
                                    record.target_player.player_role != null &&
                                    record.target_player.player_role.RoleAllowedCommands != null &&
                                    (_ReservedSelfKill || _ReservedSelfMove || _ReservedSquadLead))
                                {
                                    var allowedCommands = record.target_player.player_role.RoleAllowedCommands;
                                    if (!allowedCommands.ContainsKey("self_lead") && _ReservedSquadLead)
                                    {
                                        Threading.Wait(1000);
                                        SendMessageToSource(record, GetChatCommandByKey("self_lead") + " Command Access: " + expiration);
                                    }
                                    if (!allowedCommands.ContainsKey("self_teamswap") && _ReservedSelfMove)
                                    {
                                        Threading.Wait(1000);
                                        SendMessageToSource(record, GetChatCommandByKey("self_teamswap") + " Command Access: " + expiration);
                                    }
                                    if (!allowedCommands.ContainsKey("self_kill") && _ReservedSelfKill)
                                    {
                                        Threading.Wait(1000);
                                        SendMessageToSource(record, GetChatCommandByKey("self_kill") + " Command Access: " + expiration);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Log.HandleException(new AException("Error while printing player perks"));
                    }
                    Log.Debug(() => "Exiting a player perk printer.", 5);
                    Threading.StopWatchdog();
                }));

                //Start the thread
                Threading.StartWatchdog(perkPrinter);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending player perks.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendTargetPerks", 6);
        }

        public void TriggerTargetPoll(ARecord record)
        {
            Log.Debug(() => "Entering TriggerTargetPoll", 6);
            try
            {
                record.record_action_executed = true;

                if (_roundState != RoundState.Playing)
                {
                    SendMessageToSource(record, record.command_type.command_name + " cannot be used between rounds.");
                    FinalizeRecord(record);
                    return;
                }

                //Take care of the previous poll if one exists
                if (_ActivePoll != null)
                {
                    // Automatically cancel the previous poll
                    SendMessageToSource(record, "Cancelling current " + _ActivePoll.ID + " poll.");
                    _ActivePoll.Canceled = true;
                    var cancelTime = UtcNow();
                    while (_ActivePoll != null)
                    {
                        if (!_pluginEnabled || NowDuration(cancelTime).TotalSeconds > 10)
                        {
                            SendMessageToSource(record, "Unable to cancel previous poll.");
                            FinalizeRecord(record);
                            return;
                        }
                        Threading.Wait(500);
                    };
                    AdminSayMessage("Current poll canceled.");
                }

                //Determine whether this poll can be executed
                if (record.target_name == "event")
                {

                    // Check for options
                    var optionsString = record.record_message.ToLower().Trim();
                    if (optionsString.Contains("reset") &&
                        !optionsString.Contains("start"))
                    {
                        if (!EventActive())
                        {
                            // This option will clear the existing event options from the plugin and start new
                            SendMessageToSource(record, "Removing all existing event rounds.");
                            _EventRoundOptions.Clear();
                            QueueSettingForUpload(new CPluginVariable(@"Event Round Codes", typeof(String[]), _EventRoundOptions.Select(round => round.getCode()).ToArray()));
                            UpdateSettingPage();
                        }
                        else
                        {
                            SendMessageToSource(record, "Cannot remove existing event rounds while an event is active.");
                        }
                    }
                    if (optionsString.Contains("start") &&
                        _EventRoundOptions.Any())
                    {
                        // If the event isn't active, make the event start next round.
                        if (!EventActive())
                        {
                            _CurrentEventRoundNumber = _roundID + 1;
                            QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                            SendMessageToSource(record, "Event will be started next round.");
                            FinalizeRecord(record);
                            return;
                        }
                    }

                    // Run the event poll
                    Thread pollRunner = new Thread(new ThreadStart(delegate
                    {
                        Log.Debug(() => "Starting an event poll runner thread.", 5);
                        try
                        {
                            Thread.CurrentThread.Name = "EventPollRunner";
                            Threading.Wait(500);

                            //Create the poll object
                            _ActivePoll = new APoll(this)
                            {
                                ID = "EVENT"
                            };
                            _EventRoundPolled = true;

                            // This poll has two stages. Choosing the rules and choosing the mode.
                            AEventOption.RuleCode chosenRule = AEventOption.RuleCode.UNKNOWN;
                            AEventOption.ModeCode chosenMode = AEventOption.ModeCode.UNKNOWN;
                            AEventOption.MapCode chosenMap = AEventOption.MapCode.UNKNOWN;


                            ///////////////////////////RULE POLL/////////////////////////////
                            DoEventRulePoll(chosenRule, chosenMode, chosenMap);


                            if (_ActivePoll.Canceled)
                            {
                                AdminSayMessage("Poll canceled.");
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error while processing event poll.", e));
                        }

                        Threading.Wait(500);
                        // Remove the active poll
                        _ActivePoll = null;

                        Log.Debug(() => "Exiting an event poll runner thread.", 5);
                        Threading.StopWatchdog();
                    }));
                    //Start the thread
                    Threading.StartWatchdog(pollRunner);
                }
                else
                {
                    SendMessageToSource(record, "Unable to process event code '" + record.target_name + "'.");
                    FinalizeRecord(record);
                    return;
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while processing general poll.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting TriggerTargetPoll", 6);
        }

        private void DoEventRulePoll(AEventOption.RuleCode chosenRule, AEventOption.ModeCode chosenMode, AEventOption.MapCode chosenMap)
        {
            Log.Debug(() => "Entering DoEventRulePoll", 3);
            try
            {
                ///////////////////////////RULE POLL/////////////////////////////

                _ActivePoll.Title = "Choose event rules with !#";
                // Get the available rule options
                var existingEventRules = _EventRoundOptions
                                            .Select(option => option.Rule)
                                            .Distinct()
                                            .ToList();
                var rng = new Random(Environment.TickCount);
                var availableRuleOptions = _EventRoundPollOptions
                                            .Where(option => !existingEventRules.Contains(option.Rule))
                                            .Select(option => option.Rule)
                                            .Distinct()
                                            .OrderBy(option => rng.Next())
                                            .ToList();
                // Conditionally remove some rule options
                if (_populationStatus != PopulationState.High)
                {
                    // In medium/low population, do not allow melee/explosive rules to show up
                    availableRuleOptions.Remove(AEventOption.RuleCode.DO);
                    availableRuleOptions.Remove(AEventOption.RuleCode.EO);
                    availableRuleOptions.Remove(AEventOption.RuleCode.GO);
                    availableRuleOptions.Remove(AEventOption.RuleCode.KO);
                    availableRuleOptions.Remove(AEventOption.RuleCode.RTO);
                    availableRuleOptions.Remove(AEventOption.RuleCode.TR);
                }
                if (availableRuleOptions.Count() <= 3)
                {
                    availableRuleOptions = _EventRoundPollOptions
                                            .Select(option => option.Rule)
                                            .Distinct()
                                            .OrderBy(option => rng.Next())
                                            .ToList();
                    // Conditionally remove some rule options
                    if (_populationStatus != PopulationState.High)
                    {
                        // In medium/low population, do not allow melee/explosive rules to show up
                        availableRuleOptions.Remove(AEventOption.RuleCode.DO);
                        availableRuleOptions.Remove(AEventOption.RuleCode.EO);
                        availableRuleOptions.Remove(AEventOption.RuleCode.GO);
                        availableRuleOptions.Remove(AEventOption.RuleCode.KO);
                        availableRuleOptions.Remove(AEventOption.RuleCode.RTO);
                        availableRuleOptions.Remove(AEventOption.RuleCode.TR);
                    }
                }
                List<AEventOption.RuleCode> chosenRules = new List<AEventOption.RuleCode>();
                // Add the remaining available rules to the chosen list
                foreach (var rule in availableRuleOptions)
                {
                    if (!chosenRules.Contains(rule))
                    {
                        chosenRules.Add(rule);
                    }
                }
                foreach (var option in chosenRules)
                {
                    if (_ActivePoll.Options.Count() >= _EventPollMaxOptions)
                    {
                        break;
                    }
                    // Add the name of the option to the chosen list
                    _ActivePoll.AddOption(AEventOption.RuleNames[option], false);
                }
                if (_EventRoundOptions.Count() >= _EventRoundAutoPollsMax)
                {
                    _ActivePoll.AddOption(AEventOption.RuleNames[AEventOption.RuleCode.ENDEVENT], false);
                }

                while (_pluginEnabled &&
                       _roundState == RoundState.Playing &&
                       NowDuration(_ActivePoll.StartTime) < _PollMaxDuration &&
                       _ActivePoll.Votes.Count() < _PollMaxVotes &&
                       !_ActivePoll.Completed &&
                       !_ActivePoll.Canceled)
                {
                    if (NowDuration(_ActivePoll.PrintTime) > _PollPrintInterval)
                    {
                        // Print the poll
                        _ActivePoll.PrintPoll(_eventPollYellWinningRule);
                    }

                    Threading.Wait(100);
                }

                if (_ActivePoll.Completed)
                {
                    AdminSayMessage("Event rule poll completed with current winner.");
                }

                // Only continue if the poll has not been canceled
                if (_pluginEnabled &&
                    !_ActivePoll.Canceled)
                {

                    // Get the outcome
                    var ruleString = _ActivePoll.GetWinningOption("won", false);
                    chosenRule = AEventOption.RuleFromDisplay(ruleString);

                    if (chosenRule == AEventOption.RuleCode.ENDEVENT)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            AdminTellMessage("Event ended by vote. Normal rules next round.");
                            Thread.Sleep(500);
                        }
                    }
                    else
                    {
                        ///////////////////////////MODE POLL/////////////////////////////
                        DoEventModePoll(chosenRule, chosenMode, chosenMap);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling poll completion.", e));
            }
            Log.Debug(() => "Exiting DoEventRulePoll", 3);
        }

        private void DoEventModePoll(AEventOption.RuleCode chosenRule, AEventOption.ModeCode chosenMode, AEventOption.MapCode chosenMap)
        {
            Log.Debug(() => "Entering DoEventModePoll", 3);
            try
            {
                ///////////////////////////MODE POLL/////////////////////////////

                // Get the available mode options for the chosen rule
                var rng = new Random(Environment.TickCount);
                var availableModeOptions = _EventRoundPollOptions
                                            .Where(option => option.Rule == chosenRule)
                                            .Select(option => option.Mode)
                                            .Distinct()
                                            .OrderBy(option => rng.Next())
                                            .ToList();

                if (availableModeOptions.Count() == 1)
                {
                    // There is only one option for the poll, so just select it
                    chosenMode = availableModeOptions.First();

                    ///////////////////////////MAP POLL/////////////////////////////
                    DoEventMapPoll(chosenRule, chosenMode, chosenMap);
                }
                else
                {
                    // Reset the poll for the next stage
                    _ActivePoll.Reset();

                    _ActivePoll.Title = "Choose '" + AEventOption.RuleNames[chosenRule] + "' mode with !#";
                    foreach (var option in availableModeOptions)
                    {
                        if (_ActivePoll.Options.Count() >= _EventPollMaxOptions)
                        {
                            break;
                        }
                        // Add the name of the option to the chosen list
                        _ActivePoll.AddOption(AEventOption.ModeNames[option], false);
                    }

                    while (_pluginEnabled &&
                           _roundState == RoundState.Playing &&
                           NowDuration(_ActivePoll.StartTime) < _PollMaxDuration &&
                           _ActivePoll.Votes.Count() < _PollMaxVotes &&
                           !_ActivePoll.Completed &&
                           !_ActivePoll.Canceled)
                    {
                        if (NowDuration(_ActivePoll.PrintTime) > _PollPrintInterval)
                        {
                            // Print the poll
                            _ActivePoll.PrintPoll(_eventPollYellWinningRule);
                        }

                        Threading.Wait(100);
                    }

                    if (_ActivePoll.Completed)
                    {
                        AdminSayMessage("Event mode poll completed with current winner.");
                    }

                    // Only continue if the poll has not been canceled
                    if (_pluginEnabled &&
                        !_ActivePoll.Canceled)
                    {

                        // Get the outcome
                        chosenMode = AEventOption.ModeFromDisplay(_ActivePoll.GetWinningOption("won", false));

                        ///////////////////////////MAP POLL/////////////////////////////
                        DoEventMapPoll(chosenRule, chosenMode, chosenMap);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling poll completion.", e));
            }
            Log.Debug(() => "Exiting DoEventModePoll", 3);
        }

        private void DoEventMapPoll(AEventOption.RuleCode chosenRule, AEventOption.ModeCode chosenMode, AEventOption.MapCode chosenMap)
        {
            Log.Debug(() => "Entering DoEventMapPoll", 3);
            try
            {
                ///////////////////////////MAP POLL/////////////////////////////

                // Get the available map options for the chosen rule
                var rng = new Random(Environment.TickCount);
                var availableMapOptions = _EventRoundPollOptions
                                            .Where(option => option.Rule == chosenRule &&
                                                             option.Mode == chosenMode)
                                            .Select(option => option.Map)
                                            .Distinct()
                                            .OrderBy(option => rng.Next())
                                            .ToList();

                if (availableMapOptions.Count() == 1)
                {
                    // There is only one option for the poll, so just select it and finish
                    chosenMap = availableMapOptions.First();

                    ///////////////////////////COMPLETION//////////////////////
                    DoEventPollCompletion(chosenRule, chosenMode, chosenMap);
                }
                else
                {
                    // Reset the poll for the next stage
                    _ActivePoll.Reset();

                    _ActivePoll.Title = "Choose '" + AEventOption.RuleNames[chosenRule] + "' map with !#";
                    foreach (var option in availableMapOptions)
                    {
                        if (_ActivePoll.Options.Count() >= _EventPollMaxOptions)
                        {
                            break;
                        }
                        // Add the name of the option to the chosen list
                        _ActivePoll.AddOption(AEventOption.MapNames[option], false);
                    }

                    while (_pluginEnabled &&
                           _roundState == RoundState.Playing &&
                           NowDuration(_ActivePoll.StartTime) < _PollMaxDuration &&
                           _ActivePoll.Votes.Count() < _PollMaxVotes &&
                           !_ActivePoll.Completed &&
                           !_ActivePoll.Canceled)
                    {
                        if (NowDuration(_ActivePoll.PrintTime) > _PollPrintInterval)
                        {
                            // Print the poll
                            // Do not announce the current map leader
                            _ActivePoll.PrintPoll(false);
                        }

                        Threading.Wait(100);
                    }

                    if (_ActivePoll.Completed)
                    {
                        AdminSayMessage("Event map poll completed with current winner.");
                    }

                    // Only continue if the poll has not been canceled
                    if (_pluginEnabled &&
                        !_ActivePoll.Canceled)
                    {

                        // Get the outcome
                        chosenMap = AEventOption.MapFromDisplay(_ActivePoll.GetWinningOption("won", false));

                        ///////////////////////////COMPLETION//////////////////////
                        DoEventPollCompletion(chosenRule, chosenMode, chosenMap);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling poll completion.", e));
            }
            Log.Debug(() => "Exiting DoEventMapPoll", 3);
        }

        private void DoEventPollCompletion(AEventOption.RuleCode chosenRule, AEventOption.ModeCode chosenMode, AEventOption.MapCode chosenMap)
        {
            Log.Debug(() => "Entering DoEventPollCompletion", 3);
            try
            {
                ///////////////////////////COMPLETION//////////////////////

                var option = new AEventOption()
                {
                    Map = chosenMap,
                    Mode = chosenMode,
                    Rule = chosenRule
                };
                _EventRoundOptions.Add(option);
                QueueSettingForUpload(new CPluginVariable(@"Event Round Codes", typeof(String[]), _EventRoundOptions.Select(round => round.getCode()).ToArray()));
                AdminTellMessage("EVENT POLL COMPLETE! Next event round is " + option.getDisplay());

                // If the event isn't active or set up with a date, make the event start next round.
                if (!EventActive() &&
                    _CurrentEventRoundNumber == 999999)
                {
                    _CurrentEventRoundNumber = _roundID + 1;
                    QueueSettingForUpload(new CPluginVariable(@"Event Current Round Number", typeof(Int32), _CurrentEventRoundNumber));
                }

                UpdateSettingPage();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while handling poll completion.", e));
            }
            Log.Debug(() => "Exiting DoEventPollCompletion", 3);
        }

        public void SendTargetChat(ARecord record)
        {
            Log.Debug(() => "Entering SendTargetChat", 6);
            try
            {
                record.record_action_executed = true;
                Thread chatPrinter = new Thread(new ThreadStart(delegate
                {
                    Log.Debug(() => "Starting a player chat printer thread.", 5);
                    try
                    {
                        Thread.CurrentThread.Name = "PlayerChatPrinter";
                        if (record.target_player != null)
                        {
                            List<KeyValuePair<DateTime, string>> chatList = FetchChat(record.target_player.player_id, record.command_numeric, 30);
                            if (chatList.Any())
                            {
                                int index = 1;
                                foreach (KeyValuePair<DateTime, string> chatLine in chatList)
                                {
                                    SendMessageToSource(record, "(" + index++ + ") " + chatLine.Value);
                                    Threading.Wait(2000);
                                }
                            }
                            else
                            {
                                SendMessageToSource(record, "Target player(s) have no chat to fetch.");
                            }
                        }
                        else if (record.TargetPlayersLocal.Count == 2)
                        {
                            long firstPlayerID = record.TargetPlayersLocal[0].player_id;
                            long secondPlayerID = record.TargetPlayersLocal[1].player_id;
                            List<KeyValuePair<DateTime, KeyValuePair<string, string>>> chatList = FetchConversation(firstPlayerID, secondPlayerID, record.command_numeric, 30);
                            if (chatList.Any())
                            {
                                int index = 1;
                                foreach (KeyValuePair<DateTime, KeyValuePair<string, string>> chatLine in chatList)
                                {
                                    SendMessageToSource(record, "(" + index++ + "/" + chatLine.Value.Key + ") " + chatLine.Value.Value);
                                    Threading.Wait(2000);
                                }
                            }
                            else
                            {
                                SendMessageToSource(record, "Target player(s) have no chat to fetch.");
                            }
                        }
                        else
                        {
                            Log.Error("Invalid target conditions when printing chat.");
                            SendMessageToSource(record, "Unable to fetch chat for target players.");
                        }
                    }
                    catch (Exception)
                    {
                        Log.HandleException(new AException("Error while printing player chat"));
                    }
                    Log.Debug(() => "Exiting a player chat printer.", 5);
                    Threading.StopWatchdog();
                }));

                //Start the thread
                Threading.StartWatchdog(chatPrinter);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending player chat.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendTargetChat", 6);
        }

        public void FindTarget(ARecord record)
        {
            Log.Debug(() => "Entering FindTarget", 6);
            try
            {
                record.record_action_executed = true;
                if (record.target_player == null)
                {
                    Log.Error("Player null when finding player.");
                    return;
                }
                String playerInfo = record.GetTargetNames() + ": ";
                if (record.target_player.player_online)
                {
                    playerInfo += GetPlayerTeamName(record.target_player) + "/" + (_PlayerDictionary.Values.Where(aPlayer => aPlayer.fbpInfo.TeamID == record.target_player.fbpInfo.TeamID).OrderBy(aPlayer => aPlayer.fbpInfo.Score).Reverse().ToList().IndexOf(record.target_player) + 1) + "/" + record.target_player.fbpInfo.Score;
                }
                else
                {
                    playerInfo += "OFFLINE";
                }
                SendMessageToSource(record, playerInfo);
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending player info.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting FindTarget", 6);
        }

        public void LockTarget(ARecord record)
        {
            Log.Debug(() => "Entering LockTarget", 6);
            try
            {
                if (record.target_player == null)
                {
                    Log.Error("Player null when locking player.");
                    FinalizeRecord(record);
                    return;
                }
                if (String.IsNullOrEmpty(record.source_name) && record.source_player == null)
                {
                    SendMessageToSource(record, "No source provided to lock player. Unable to lock.");
                    FinalizeRecord(record);
                    return;
                }
                //Set the executed bool
                record.record_action_executed = true;
                //Check if already locked
                if (record.target_player.IsLocked())
                {
                    SendMessageToSource(record, record.GetTargetNames() + " is already locked by " + record.target_player.GetLockSource() + " for " + FormatTimeString(record.target_player.GetLockRemaining(), 3) + ".");
                    FinalizeRecord(record);
                    return;
                }
                //Assign the new lock
                Double inputDuration = 1;
                if (record.command_numeric > 0)
                {
                    inputDuration = record.command_numeric;
                }
                else
                {
                    inputDuration = _playerLockingManualDuration;
                }
                TimeSpan duration = TimeSpan.FromMinutes(inputDuration);
                record.target_player.Lock(record.source_name, duration);
                SendMessageToSource(record, record.GetTargetNames() + " is now locked for " + FormatTimeString(duration, 3) + ", or until you unlock them.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while locking player.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting LockTarget", 6);
        }

        public void UnlockTarget(ARecord record)
        {
            Log.Debug(() => "Entering UnlockTarget", 6);
            try
            {
                if (record.target_player == null)
                {
                    Log.Error("Player null when unlocking player.");
                    return;
                }
                //Set the executed bool
                record.record_action_executed = true;
                //Check if already locked
                if (!record.target_player.IsLocked())
                {
                    SendMessageToSource(record, record.GetTargetNames() + " is not locked.");
                    FinalizeRecord(record);
                    return;
                }
                record.target_player.Unlock();
                SendMessageToSource(record, record.GetTargetNames() + " is now unlocked.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while unlocking player.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting UnlockTarget", 6);
        }

        public void MarkTarget(ARecord record)
        {
            Log.Debug(() => "Entering MarkTarget", 6);
            try
            {
                if (record.target_player == null)
                {
                    Log.Error("Player null when marking player.");
                    return;
                }
                record.record_action_executed = true;
                SendMessageToSource(record, record.GetTargetNames() + " marked for leave notification.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while marking player.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting MarkTarget", 6);
        }

        public void LoadoutFetchTarget(ARecord record)
        {
            Log.Debug(() => "Entering LoadoutFetchTarget", 6);
            try
            {
                if (record.target_player == null)
                {
                    Log.Error("Player null when fetching loadout for player.");
                    SendMessageToSource(record, "Error checking loadout for " + record.GetTargetNames() + ".");
                    return;
                }
                record.record_action_executed = true;
                if (_subscribedClients.Any(client => client.ClientName == "AdKatsLRT" && client.SubscriptionEnabled))
                {
                    lock (_LoadoutConfirmDictionary)
                    {
                        _LoadoutConfirmDictionary[record.target_player.player_name] = record;
                    }
                    SendMessageToSource(record, "Fetching loadout for " + record.GetTargetNames() + ".");
                    var startDuration = NowDuration(_AdKatsStartTime).TotalMinutes;
                    var startupDuration = TimeSpan.FromSeconds(_startupDurations.Average(span => span.TotalSeconds)).TotalMinutes;
                    if (startDuration - startupDuration < 10)
                    {
                        SendMessageToSource(record, "WARNING: AdKats/LRT just started, loadouts may not be available for a few minutes.");
                    }
                    ExecuteCommand("procon.protected.plugins.call", "AdKatsLRT", "CallLoadoutCheckOnPlayer", "AdKats", JSON.JsonEncode(new Hashtable {
                        {"caller_identity", "AdKats"},
                        {"response_requested", false},
                        {"player_name", record.target_player.player_name},
                        {"loadoutCheck_reason", "fetch"}
                    }));
                }
                else
                {
                    SendMessageToSource(record, "AdKatsLRT not installed/integrated, loadout for " + record.GetTargetNames() + " cannot be fetched.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while fetching loadout for player.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting LoadoutFetchTarget", 6);
        }

        public void LoadoutForceTarget(ARecord record)
        {
            Log.Debug(() => "Entering LoadoutForceTarget", 6);
            try
            {
                if (record.target_player == null)
                {
                    Log.Error("Player null when forcing loadout on player.");
                    SendMessageToSource(record, "Error forcing loadout on " + record.GetTargetNames() + ".");
                    return;
                }
                if (!record.target_player.player_online)
                {
                    SendMessageToSource(record, record.GetTargetNames() + " is not online, loadout cannot be forced.");
                    return;
                }
                record.record_action_executed = true;
                if (_subscribedClients.Any(client => client.ClientName == "AdKatsLRT" && client.SubscriptionEnabled))
                {
                    ExecuteCommand("procon.protected.plugins.call", "AdKatsLRT", "CallLoadoutCheckOnPlayer", "AdKats", JSON.JsonEncode(new Hashtable {
                        {"caller_identity", "AdKats"},
                        {"response_requested", false},
                        {"player_name", record.target_player.player_name},
                        {"loadoutCheck_reason", "forced"}
                    }));
                    SendMessageToSource(record, record.GetTargetNames() + " forced up to trigger level loadout enforcement.");
                }
                else
                {
                    SendMessageToSource(record, "AdKatsLRT not installed/integrated, " + record.GetTargetNames() + " CANNOT be forced up to trigger level loadout enforcement.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while forcing loadout on player.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting LoadoutForceTarget", 6);
        }

        public void LoadoutIgnoreTarget(ARecord record)
        {
            Log.Debug(() => "Entering LoadoutIgnoreTarget", 6);
            try
            {
                if (record.target_player == null)
                {
                    Log.Error("Player null when ignoring loadout for player.");
                    SendMessageToSource(record, "Error ignoring loadout for " + record.GetTargetNames() + ".");
                    return;
                }
                if (!record.target_player.player_online)
                {
                    SendMessageToSource(record, record.GetTargetNames() + " is not online, loadout cannot be ignored.");
                    return;
                }
                record.record_action_executed = true;
                if (_subscribedClients.Any(client => client.ClientName == "AdKatsLRT" && client.SubscriptionEnabled))
                {
                    ExecuteCommand("procon.protected.plugins.call", "AdKatsLRT", "CallLoadoutCheckOnPlayer", "AdKats", JSON.JsonEncode(new Hashtable {
                        {"caller_identity", "AdKats"},
                        {"response_requested", false},
                        {"player_name", record.target_player.player_name},
                        {"loadoutCheck_reason", "ignored"}
                    }));
                    SendMessageToSource(record, record.GetTargetNames() + " is now temporarily ignored by the loadout enforcer.");
                }
                else
                {
                    SendMessageToSource(record, "AdKatsLRT not installed/integrated.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while ignoring loadout for player.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting LoadoutIgnoreTarget", 6);
        }

        public void PingFetchTarget(ARecord record)
        {
            Log.Debug(() => "Entering PingFetchTarget", 6);
            try
            {
                if (record.target_player == null)
                {
                    Log.Error("Player null when fetching player ping.");
                    SendMessageToSource(record, "Error fetching ping for " + record.GetTargetNames() + ".");
                    return;
                }
                if (!record.target_player.player_online)
                {
                    SendMessageToSource(record, record.GetTargetNames() + " is not online, ping cannot be fetched.");
                    return;
                }
                record.record_action_executed = true;
                record.command_numeric = (int)record.target_player.player_ping;
                String currentString = record.target_player.player_ping > 0 ? Math.Round(record.target_player.player_ping).ToString() : "Missing";
                String averageString = record.target_player.player_ping_avg > 0 ? Math.Round(record.target_player.player_ping_avg).ToString() : "Missing";
                SendMessageToSource(record, record.target_player.GetVerboseName() + "'s Ping: " + (record.target_player.player_ping_manual ? "[M] " : "") + currentString + "ms (Avg: " + averageString + "ms)");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while fetching player ping.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting PingFetchTarget", 6);
        }

        public void ForcePingTarget(ARecord record)
        {
            Log.Debug(() => "Entering ForcePingTarget", 6);
            try
            {
                if (record.target_player == null)
                {
                    Log.Error("Player null when forcing manual ping on player.");
                    SendMessageToSource(record, "Error forcing manual ping on " + record.GetTargetNames() + ".");
                    return;
                }
                if (!record.target_player.player_online)
                {
                    SendMessageToSource(record, record.GetTargetNames() + " is not online, ping cannot be manually fetched.");
                    return;
                }
                if (String.IsNullOrEmpty(record.target_player.player_ip))
                {
                    SendMessageToSource(record, "We don't have an IP for " + record.GetTargetNames() + ", we can't manually fetch their ping.");
                    return;
                }
                record.record_action_executed = true;
                record.target_player.player_ping_manual = true;
                record.target_player.ClearPingEntries();
                SendMessageToSource(record, record.target_player.GetVerboseName() + "'s ping will now be manually fetched. Deleting old pings for them.");
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while forcing manual player ping.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ForcePingTarget", 6);
        }

        public void ManageAFKPlayers(ARecord record)
        {
            Log.Debug(() => "Entering ManageAFKPlayers", 6);
            try
            {
                record.record_action_executed = true;
                if (GetPlayerCount() < _AFKTriggerMinimumPlayers)
                {
                    SendMessageToSource(record, "Server contains less than " + _AFKTriggerMinimumPlayers + ", unable to kick AFK players.");
                    FinalizeRecord(record);
                    return;
                }
                List<APlayer> afkPlayers = _PlayerDictionary.Values.Where(aPlayer => (UtcNow() - aPlayer.lastAction).TotalMinutes > _AFKTriggerDurationMinutes && aPlayer.player_type != PlayerType.Spectator && !PlayerIsAdmin(aPlayer)).Take(_PlayerDictionary.Values.Count(aPlayer => aPlayer.player_type == PlayerType.Player) - _AFKTriggerMinimumPlayers).ToList();
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
                if (afkPlayers.Any())
                {
                    foreach (APlayer aPlayer in afkPlayers)
                    {
                        string afkTime = FormatTimeString(UtcNow() - aPlayer.lastAction, 2);
                        Log.Debug(() => "Kicking " + aPlayer.GetVerboseName() + " for being AFK " + afkTime + ".", 3);
                        ARecord kickRecord = new ARecord
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
                        QueueRecordForProcessing(kickRecord);
                    }
                    SendMessageToSource(record, afkPlayers.Count() + " players kicked for being AFK.");
                }
                else
                {
                    SendMessageToSource(record, "No AFK players found or kickable.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while managing AFK players.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting ManageAFKPlayers", 6);
        }

        public void SendOnlineAdmins(ARecord record)
        {
            Log.Debug(() => "Entering SendOnlineAdmins", 6);
            try
            {
                record.record_action_executed = true;
                List<APlayer> onlineAdminList = FetchOnlineAdminSoldiers();
                String onlineAdmins = "Admins: [" + onlineAdminList.Count + " Online] ";
                onlineAdmins = onlineAdminList.Aggregate(onlineAdmins, (current, aPlayer) => current + (aPlayer.GetVerboseName() + " (" + GetPlayerTeamKey(aPlayer).Replace("Neutral", "Spectator") + "/" + (_PlayerDictionary.Values.Where(innerPlayer => innerPlayer.fbpInfo.TeamID == aPlayer.fbpInfo.TeamID).OrderBy(innerPlayer => innerPlayer.fbpInfo.Score).Reverse().ToList().IndexOf(aPlayer) + 1) + "), "));
                //Send online admins
                SendMessageToSource(record, onlineAdmins.Trim().TrimEnd(','));
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending online admins.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendOnlineAdmins", 6);
        }

        public void LeadCurrentSquad(ARecord record)
        {
            Log.Debug(() => "Entering LeadCurrentSquad", 6);
            try
            {
                record.record_action_executed = true;
                ExecuteCommand("procon.protected.send", "squad.leader", record.target_player.fbpInfo.TeamID.ToString(), record.target_player.fbpInfo.SquadID.ToString(), record.target_player.player_name);
                PlayerSayMessage(record.target_player.player_name, "You are now the leader of your current squad.");
                if (record.source_name != record.target_name)
                {
                    SendMessageToSource(record, record.GetTargetNames() + " is now the leader of their current squad.");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while leading curring squad.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting LeadCurrentSquad", 6);
        }

        public void SendChallengeInfo(ARecord record)
        {
            Log.Debug(() => "Entering SendChallengeInfo", 6);
            try
            {
                record.record_action_executed = true;

                if (record.target_player == null)
                {
                    record.target_player = record.source_player;
                }

                var commandText = GetChatCommandByKey("self_challenge");

                var option = record.record_message.ToLower().Trim();
                if (option.StartsWith("help"))
                {
                    var waitMS = 2000;
                    SendMessageToSource(record, commandText + " info (Current challenge info.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " p (Current challenge progress.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " rewards (List of challenge rewards.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " list (List of available challenges.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " list # (List of available tier # challenges.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " # (Start challenge #.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " random (Start a random challenge of any tier.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " random # (Start a random tier # challenge.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " k (Activates after you complete a challenge weapon. Admin slays you manually.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " autokill (Toggle being automatically slain when completing challenge weapons.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " ignore (Toggle ignoring the challenge system completely.)");
                    Threading.Wait(waitMS);
                    SendMessageToSource(record, commandText + " help (Show this help message.)");
                }
                else if (option == "list" || option.StartsWith("list "))
                {
                    if (record.target_player != null &&
                        GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                    {
                        SendMessageToSource(record, "You are currently ignoring challenges. To stop ignoring challenges type " + commandText + " ignore");
                        FinalizeRecord(record);
                        return;
                    }
                    var split = option.Split(' ');
                    Int32 tier = 0;
                    if (split.Count() >= 2)
                    {
                        Int32.TryParse(split[1], out tier);
                    }
                    if (tier < 0)
                    {
                        tier = 0;
                    }
                    if (tier > 10)
                    {
                        tier = 10;
                    }
                    // Immediately get the rule list, then go async
                    var rules = ChallengeManager.GetRules().Where(rule => rule.Enabled &&
                                                                          rule.Definition.GetDetails().Any())
                                                           .OrderBy(rule => rule.Tier)
                                                           .ThenBy(rule => rule.Name).ToList();
                    if (tier != 0)
                    {
                        rules = rules.Where(rule => rule.Tier == tier).ToList();
                    }
                    var ruleStrings = rules.Select(rule => rule.ToString());
                    if (ruleStrings.Any())
                    {
                        Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                        {
                            try
                            {
                                Thread.CurrentThread.Name = "ChallengeRulePrinter";
                                Threading.Wait(100);
                                foreach (var ruleString in ruleStrings)
                                {
                                    SendMessageToSource(record, "" + commandText + " " + ruleString);
                                    Threading.Wait(1800);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.HandleException(new AException("Error while printing challenge rules.", e));
                            }
                            Threading.StopWatchdog();
                        })));
                    }
                    else
                    {
                        SendMessageToSource(record, "No challenges are available" + (tier != 0 ? " at tier " + tier : "") + ".");
                    }
                }
                else if (option == "random" || option.StartsWith("random "))
                {
                    if (record.target_player != null &&
                        GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                    {
                        SendMessageToSource(record, "You are currently ignoring challenges. To stop ignoring challenges type " + commandText + " ignore");
                        FinalizeRecord(record);
                        return;
                    }
                    var split = option.Split(' ');
                    Int32 tier = 0;
                    if (split.Count() >= 2)
                    {
                        Int32.TryParse(split[1], out tier);
                    }
                    ChallengeManager.CreateAndAssignRandomEntry(record.target_player, tier, true);
                }
                // Be agnostic of plural
                else if (option.StartsWith("reward"))
                {
                    if (record.target_player != null &&
                        GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                    {
                        SendMessageToSource(record, "You are currently ignoring challenges. To stop ignoring challenges type " + commandText + " ignore");
                        FinalizeRecord(record);
                        return;
                    }
                    var activeRules = ChallengeManager.GetRules().Where(dRule => dRule.Enabled);
                    var activeRewards = ChallengeManager.GetRewards().Where(dReward => dReward.Enabled &&
                                                                                       dReward.Reward != AChallengeManager.CReward.RewardType.None &&
                                                                                       activeRules.Any(dRule => dRule.Tier == dReward.Tier))
                                                                     .OrderBy(dReward => dReward.Tier).ThenBy(dReward => dReward.Reward);
                    List<String> rewardMessages = new List<string>();
                    if (activeRewards.Any())
                    {
                        var rewardGroups = activeRewards.GroupBy(dReward => dReward.Tier);
                        foreach (var rewardGroup in rewardGroups)
                        {
                            var groupString = "Tier " + rewardGroup.First().Tier + ": ";
                            var rewardStrings = rewardGroup.OrderBy(dReward => dReward.Reward.ToString())
                                                           .Select(dReward => dReward.getDescriptionString(record.target_player))
                                                           .Distinct();
                            groupString += String.Join(", ", rewardStrings.ToArray());
                            rewardMessages.Add(groupString);
                        }
                    }

                    if (rewardMessages.Any())
                    {
                        Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                        {
                            Log.Debug(() => "Starting a challenge reward printer.", 5);
                            try
                            {
                                Thread.CurrentThread.Name = "ChallengeRewardPrinter";
                                Threading.Wait(100);
                                foreach (var message in rewardMessages)
                                {
                                    if (String.IsNullOrEmpty(message.Replace(Environment.NewLine, "").Trim()))
                                    {
                                        continue;
                                    }
                                    SendMessageToSource(record, message);
                                    Threading.Wait(1500);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.HandleException(new AException("Error while printing challenge rewards.", e));
                            }
                            Log.Debug(() => "Exiting a challenge rewards printer.", 5);
                            Threading.StopWatchdog();
                        })));
                    }
                    else
                    {
                        SendMessageToSource(record, "No challenge rewards are enabled at this time.");
                    }
                }
                else if (option == "info")
                {
                    if (record.target_player != null &&
                        GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                    {
                        SendMessageToSource(record, "You are currently ignoring challenges. To stop ignoring challenges type " + commandText + " ignore");
                        FinalizeRecord(record);
                        return;
                    }
                    // Immediately get the challenge info, then go async
                    var infoMessages = ChallengeManager.GetChallengeInfo(record.target_player, true).Split(
                        new[] { Environment.NewLine },
                        StringSplitOptions.None
                    );

                    Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                    {
                        Log.Debug(() => "Starting a challenge info printer.", 5);
                        try
                        {
                            Thread.CurrentThread.Name = "ChallengeInfoPrinter";
                            Threading.Wait(100);
                            foreach (var message in infoMessages)
                            {
                                if (String.IsNullOrEmpty(message.Replace(Environment.NewLine, "").Trim()))
                                {
                                    continue;
                                }
                                SendMessageToSource(record, message);
                                Threading.Wait(1500);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error while printing challenge info.", e));
                        }
                        Log.Debug(() => "Exiting a challenge info printer.", 5);
                        Threading.StopWatchdog();
                    })));
                }
                else if (option == "p")
                {
                    if (record.target_player != null &&
                        GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                    {
                        SendMessageToSource(record, "You are currently ignoring challenges. To stop ignoring challenges type " + commandText + " ignore");
                        FinalizeRecord(record);
                        return;
                    }
                    // Immediately get the challenge progress, then go async
                    var progressMessages = ChallengeManager.GetChallengeInfo(record.target_player, false).Split(
                        new[] { Environment.NewLine },
                        StringSplitOptions.None
                    );

                    Threading.StartWatchdog(new Thread(new ThreadStart(delegate
                    {
                        Log.Debug(() => "Starting a challenge progress printer.", 5);
                        try
                        {
                            Thread.CurrentThread.Name = "ChallengeProgressPrinter";
                            Threading.Wait(100);
                            foreach (var message in progressMessages)
                            {
                                if (String.IsNullOrEmpty(message.Replace(Environment.NewLine, "").Trim()))
                                {
                                    continue;
                                }
                                SendMessageToSource(record, message);
                                Threading.Wait(1500);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error while printing challenge progress.", e));
                        }
                        Log.Debug(() => "Exiting a challenge progress printer.", 5);
                        Threading.StopWatchdog();
                    })));
                }
                else if (option == "k")
                {
                    if (record.target_player != null &&
                        GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                    {
                        SendMessageToSource(record, "You are currently ignoring challenges. To stop ignoring challenges type " + commandText + " ignore");
                        FinalizeRecord(record);
                        return;
                    }
                    if (record.target_player.ActiveChallenge == null)
                    {
                        SendMessageToSource(record, "You do not have a challenge active.");
                        FinalizeRecord(record);
                        return;
                    }
                    if (GetMatchingVerboseASPlayersOfGroup("challenge_autokill", record.target_player).Any())
                    {
                        SendMessageToSource(record, "You have autokill enabled, you will be slain automatically. No need to manually request it.");
                        FinalizeRecord(record);
                        return;
                    }
                    if (!record.target_player.ActiveChallenge.kAllowed)
                    {
                        SendMessageToSource(record, "You must complete a challenge weapon to use the challenge admin kill.");
                        FinalizeRecord(record);
                        return;
                    }
                    ExecuteCommand("procon.protected.send", "admin.killPlayer", record.target_player.player_name);
                    record.target_player.Say(Log.CPink("Challenge admin kill activated."));
                    record.target_player.ActiveChallenge.kAllowed = false;
                }
                else if (option == "autokill")
                {
                    if (record.target_player == null)
                    {
                        SendMessageToSource(record, "Cannot change autokill status without being a player.");
                        FinalizeRecord(record);
                        return;
                    }
                    if (GetMatchingVerboseASPlayersOfGroup("challenge_autokill", record.target_player).Any())
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_challenge_autokill_remove"),
                            command_numeric = 0,
                            target_name = record.target_player.player_name,
                            target_player = record.target_player,
                            source_name = "ChallengeManager",
                            record_message = "Removing Challenge AutoKill Status",
                            record_time = UtcNow()
                        });
                        SendMessageToSource(record, "You will NOT be slain when completing challenge weapons.");
                    }
                    else
                    {
                        if (record.target_player != null &&
                            GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                        {
                            SendMessageToSource(record, "You are currently ignoring challenges. To stop ignoring challenges type " + commandText + " ignore");
                            FinalizeRecord(record);
                            return;
                        }
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_challenge_autokill"),
                            command_numeric = 10518984,
                            target_name = record.target_player.player_name,
                            target_player = record.target_player,
                            source_name = "ChallengeManager",
                            record_message = "Adding Challenge AutoKill Status",
                            record_time = UtcNow()
                        });
                        SendMessageToSource(record, "You will now be slain when completing challenge weapons.");
                    }
                }
                else if (option == "ignore")
                {
                    if (record.target_player == null)
                    {
                        SendMessageToSource(record, "Cannot change ignoring status without being a player.");
                        FinalizeRecord(record);
                        return;
                    }
                    if (GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_challenge_ignore_remove"),
                            command_numeric = 0,
                            target_name = record.target_player.player_name,
                            target_player = record.target_player,
                            source_name = "ChallengeManager",
                            record_message = "Removing Challenge Ignoring Status",
                            record_time = UtcNow()
                        });
                        SendMessageToSource(record, "You are no longer ignoring challenge related messages.");
                    }
                    else
                    {
                        QueueRecordForProcessing(new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_challenge_ignore"),
                            command_numeric = 10518984,
                            target_name = record.target_player.player_name,
                            target_player = record.target_player,
                            source_name = "ChallengeManager",
                            record_message = "Adding Challenge Ignore Status",
                            record_time = UtcNow()
                        });
                        SendMessageToSource(record, "You are now ignoring challenge related messages.");
                        if (record.target_player.ActiveChallenge != null)
                        {
                            // They are ignoring challenges but have an active challenge. Cancel it.
                            record.target_player.ActiveChallenge.DoCancel();
                        }
                    }
                }
                else
                {
                    if (record.target_player != null &&
                        GetMatchingVerboseASPlayersOfGroup("challenge_ignore", record.target_player).Any())
                    {
                        SendMessageToSource(record, "You are currently ignoring challenges. To stop ignoring challenges type " + commandText + " ignore");
                        FinalizeRecord(record);
                        return;
                    }
                    var split = option.Split(' ');
                    if (split.Any())
                    {
                        Int32 parseID;
                        if (Int32.TryParse(split[0], out parseID))
                        {
                            // They entered a number. See if it's a challenge ID, and if so, assign it to them.
                            var selectRules = ChallengeManager.GetRules().Where(rule => rule.Enabled &&
                                                                                        rule.Definition.GetDetails().Any())
                                                                         .OrderBy(rule => rule.Tier)
                                                                         .ThenBy(rule => rule.Name);
                            var selected = selectRules.FirstOrDefault(rule => rule.ID == parseID);
                            if (selected != null)
                            {
                                // Make sure they aren't overwriting their current challenge
                                if (record.target_player.ActiveChallenge != null &&
                                    record.target_player.ActiveChallenge.Rule.ID == selected.ID)
                                {
                                    record.target_player.Say("You are already playing a " + selected.Name + " challenge. To see your progress type " + commandText + " p");
                                    return;
                                }
                                else
                                {
                                    ChallengeManager.CreateAndAssignEntry(record.target_player, selected, true);
                                    return;
                                }
                            }
                            else
                            {
                                SendMessageToSource(record, "Challenge " + parseID + " does not exist. To see the list type " + commandText + " list");
                                return;
                            }
                        }
                        else if (split[0].Contains("#"))
                        {
                            SendMessageToSource(record, "You need to enter the challenge number from the list. For example " + commandText + " 1");
                            return;
                        }
                    }
                    SendMessageToSource(record, "'" + record.record_message + "' was an invalid option. Type " + commandText + " help");
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending challenge info.", e);
                Log.HandleException(record.record_exception);
                FinalizeRecord(record);
            }
            Log.Debug(() => "Exiting SendCHallengeInfo", 6);
        }

        private void QueueUserForUpload(AUser user)
        {
            try
            {
                Log.Debug(() => "Preparing to queue user for access upload.", 6);
                lock (_UserUploadQueue)
                {
                    _UserUploadQueue.Enqueue(user);
                    Log.Debug(() => "User queued for access upload", 6);
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queuing user upload.", e));
            }
        }

        private void QueueUserForRemoval(AUser user)
        {
            try
            {
                Log.Debug(() => "Preparing to queue user for access removal", 6);
                lock (_UserRemovalQueue)
                {
                    _UserRemovalQueue.Enqueue(user);
                    Log.Debug(() => "User queued for access removal", 6);
                    _DbCommunicationWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queuing access removal.", e));
            }
        }

        private Boolean HasAccess(APlayer aPlayer, ACommand command)
        {
            try
            {
                if (aPlayer == null)
                {
                    Log.Error("player was null in hasAccess.");
                    return false;
                }
                if (aPlayer.player_name == _debugSoldierName)
                {
                    return true;
                }
                if (aPlayer.player_role == null)
                {
                    Log.Error("player role was null in hasAccess.");
                    return false;
                }
                if (command == null)
                {
                    Log.Error("Command was null in hasAccess.");
                    return false;
                }
                if ((_ReservedSelfKill || _ReservedSelfMove || _ReservedSquadLead) &&
                    GetMatchingVerboseASPlayersOfGroup("slot_reserved", aPlayer).Any())
                {
                    // Yes these could be just one if block. readability yo.
                    if (_ReservedSquadLead && command.command_key == "self_lead")
                    {
                        return true;
                    }
                    if (_ReservedSelfMove && command.command_key == "self_teamswap")
                    {
                        return true;
                    }
                    if (_ReservedSelfKill && command.command_key == "self_kill")
                    {
                        return true;
                    }
                }
                lock (aPlayer.player_role)
                {
                    lock (aPlayer.player_role.RoleAllowedCommands)
                    {
                        if (aPlayer.player_role.RoleAllowedCommands.ContainsKey(command.command_key))
                        {
                            return true;
                        }
                        if (aPlayer.player_role.ConditionalAllowedCommands.Values.Any(innerCommand => (innerCommand.Value.command_key == command.command_key) && innerCommand.Key(this, aPlayer)))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while checking command access on player.", e));
            }
            return false;
        }

    }
}
