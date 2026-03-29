using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public void AntiCheatThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting AntiCheat Thread", 1);
                Thread.CurrentThread.Name = "AntiCheat";

                //Current player being checked
                APlayer aPlayer = null;

                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering AntiCheat Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        try
                        {
                            if (_BattlelogFetchQueue.Count >= 5)
                            {
                                Log.Debug(() => "AntiCheat waiting on battlelog fetches to complete. In Queue [" + _BattlelogFetchQueue.Count + "].", 4);
                                Threading.Wait(TimeSpan.FromSeconds(10));
                                continue;
                            }

                            //Get all unchecked players
                            if (_AntiCheatQueue.Count > 0)
                            {
                                lock (_AntiCheatQueue)
                                {
                                    aPlayer = _AntiCheatQueue.Dequeue();
                                }
                            }
                            else
                            {
                                Log.Debug(() => "No inbound AntiCheat checks. Waiting 10 seconds or for input.", 4);
                                //Wait for input
                                if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                                {
                                    Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                                }
                                _AntiCheatWaitHandle.Reset();
                                //Either loop when handle is set, or after 3 minutes
                                _AntiCheatWaitHandle.WaitOne(TimeSpan.FromMinutes(3));
                                loopStart = UtcNow();
                                continue;
                            }
                        }
                        catch (Exception e)
                        {
                            Log.HandleException(new AException("Error while fetching new players to check.", e));
                        }

                        if (aPlayer != null)
                        {
                            if (!PlayerProtected(aPlayer))
                            {
                                Log.Debug(() => "Reading " + aPlayer.GetVerboseName() + " for AntiCheat", 5);
                                _AntiCheatCheckedPlayers.Add(aPlayer.player_guid);
                                if (!String.IsNullOrEmpty(aPlayer.player_name) &&
                                    !String.IsNullOrEmpty(aPlayer.player_battlelog_personaID) &&
                                    FetchPlayerStatInformation(aPlayer))
                                {
                                    RunStatSiteHackCheck(aPlayer, false);
                                    _AntiCheatCheckedPlayersStats.Add(aPlayer.player_guid);
                                    Log.Debug(() => aPlayer.GetVerboseName() + " stat checked. (" + String.Format("{0:0.00}", (_AntiCheatCheckedPlayersStats.Count / (Double)_AntiCheatCheckedPlayers.Count) * 100) + "% of " + _AntiCheatCheckedPlayers.Count + " players checked)", 4);
                                }
                                else if (aPlayer.player_online && _PlayerDictionary.ContainsKey(aPlayer.player_name))
                                {
                                    //No stats found, requeue them for checking
                                    Thread.Sleep(TimeSpan.FromSeconds(1.0));
                                    QueuePlayerForAntiCheatCheck(aPlayer);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("AntiCheat thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in AntiCheat thread. Skipping current loop.", e));
                    }
                }
                Log.Debug(() => "Ending AntiCheat Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in AntiCheat thread.", e));
            }
        }

        private void RunStatSiteHackCheck(APlayer aPlayer, Boolean verbose)
        {
            try
            {
                Log.Debug(() => "AntiCheat running on " + aPlayer.GetVerboseName(), 5);
                Boolean acted = false;
                if (_UseHskChecker)
                {
                    Log.Debug(() => "Preparing to HSK check " + aPlayer.GetVerboseName(), 5);
                    acted = AimbotHackCheck(aPlayer, verbose);
                }
                if (!acted)
                {
                    Log.Debug(() => "Preparing to DPS check " + aPlayer.GetVerboseName(), 5);
                    acted = DamageHackCheck(aPlayer, verbose);
                }
                if (_UseKpmChecker && !acted)
                {
                    Log.Debug(() => "Preparing to KPM check " + aPlayer.GetVerboseName(), 5);
                    acted = KPMHackCheck(aPlayer, verbose);
                }
                if (_useAntiCheatLIVESystem &&
                    //Only on BF4
                    GameVersion == GameVersionEnum.BF4 &&
                    //Stats are available
                    aPlayer.RoundStats.ContainsKey(_roundID - 1) &&
                    aPlayer.RoundStats.ContainsKey(_roundID) &&
                    //AdKats has been running long enough to collect kill codes
                    _previousRoundDuration.TotalSeconds > 0 &&
                    (UtcNow() - _AdKatsRunningTime).TotalSeconds > _previousRoundDuration.TotalSeconds * 1.5)
                {
                    APlayerStats previousStats;
                    APlayerStats currentStats;
                    if (aPlayer.RoundStats.TryGetValue(_roundID, out currentStats) &&
                        aPlayer.RoundStats.TryGetValue(_roundID - 1, out previousStats))
                    {
                        if (previousStats.WeaponStats != null &&
                            previousStats.VehicleStats != null &&
                            previousStats.LiveStats != null &&
                            currentStats.WeaponStats != null &&
                            currentStats.VehicleStats != null)
                        {
                            //Weapon specific info
                            Int32 previousWeaponKillCount =
                                (Int32)previousStats.WeaponStats.Values.Sum(aWeapon => aWeapon.Kills) +
                                (Int32)previousStats.VehicleStats.Values.Sum(aVehicle => aVehicle.Kills);
                            Int32 currentWeaponKillCount =
                                (Int32)currentStats.WeaponStats.Values.Sum(aWeapon => aWeapon.Kills) +
                                (Int32)currentStats.VehicleStats.Values.Sum(aVehicle => aVehicle.Kills);
                            Int32 previousWeaponHitCount = (Int32)previousStats.WeaponStats.Values.Sum(aWeapon => aWeapon.Hits);
                            Int32 currentWeaponHitCount = (Int32)currentStats.WeaponStats.Values.Sum(aWeapon => aWeapon.Hits);
                            //Calcs
                            Int32 weaponKillDiff = currentWeaponKillCount - previousWeaponKillCount;
                            Int32 weaponHitDiff = currentWeaponHitCount - previousWeaponHitCount;
                            Int64 overallKillDiff = currentStats.Kills - previousStats.Kills;
                            Int64 overallHitDiff = currentStats.Hits - previousStats.Hits;
                            Int64 killDiscrepancy = overallKillDiff - weaponKillDiff;
                            Int64 hitDiscrepancy = overallHitDiff - weaponHitDiff;
                            Int32 rconKillDiff = aPlayer.LiveKills.Count(aKill => aKill.RoundID == _roundID - 1);
                            Int32 serverKillDiff = previousStats.LiveStats.Kills;
                            Int32 nonBLWeaponKills = aPlayer.LiveKills.Count(aKill => aKill.RoundID == _roundID - 1 && aKill.weaponCode == "DamageArea");

                            killDiscrepancy = killDiscrepancy - nonBLWeaponKills;

                            //Confirm kill codes are loaded and valid
                            if (rconKillDiff > 0 && Math.Abs(serverKillDiff - overallKillDiff) <= 5)
                            {
                                if (killDiscrepancy >= 10 &&
                                    hitDiscrepancy * 2 <= killDiscrepancy &&
                                    !PlayerProtected(aPlayer))
                                {
                                    Log.Warn("KILLDIFF - " + aPlayer.GetVerboseName() + " - (" + killDiscrepancy + " Unaccounted Kills)(" + hitDiscrepancy + " Unaccounted Hits)");
                                    Log.Warn(String.Join(", ", aPlayer.LiveKills.Select(aKill => aKill.weaponCode).ToArray()));
                                    QueueRecordForProcessing(new ARecord
                                    {
                                        record_source = ARecord.Sources.Automated,
                                        server_id = _serverInfo.ServerID,
                                        command_type = GetCommandByKey("player_ban_perm"),
                                        command_numeric = 0,
                                        target_name = aPlayer.player_name,
                                        target_player = aPlayer,
                                        source_name = "AutoAdmin",
                                        record_message = "Magic Bullet [LIVE][7-" + killDiscrepancy + "-" + hitDiscrepancy + "]",
                                        record_time = UtcNow()
                                    });
                                    acted = true;
                                }
                            }
                        }
                    }
                }
                if (!acted && verbose)
                {
                    Log.Success(aPlayer.GetVerboseName() + " is clean.");
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running stat site hack check.", e));
            }
        }

        private Boolean DamageHackCheck(APlayer aPlayer, Boolean debugMode)
        {
            Boolean acted = false;
            try
            {
                APlayerStats currentStats;
                if (aPlayer == null || !aPlayer.RoundStats.TryGetValue(_roundID, out currentStats) || currentStats.WeaponStats == null)
                {
                    return false;
                }
                APlayerStats previousStats;
                aPlayer.RoundStats.TryGetValue(_roundID - 1, out previousStats);

                //Confirm stat changes from battlelog are valid for the previous round
                var killStatsValid = false;
                Int32 serverKillDiff = 0;
                Int32 statKillDiff = 0;
                if (_useAntiCheatLIVESystem &&
                    previousStats != null &&
                    previousStats.LiveStats != null &&
                    previousStats.WeaponStats != null &&
                    previousStats.VehicleStats != null &&
                    currentStats.WeaponStats != null &&
                    currentStats.VehicleStats != null)
                {
                    serverKillDiff = previousStats.LiveStats.Kills;
                    Int32 previousWeaponKillCount =
                        (Int32)previousStats.WeaponStats.Values.Sum(aWeapon => aWeapon.Kills) +
                        (Int32)previousStats.VehicleStats.Values.Sum(aVehicle => aVehicle.Kills);
                    Int32 currentWeaponKillCount =
                        (Int32)currentStats.WeaponStats.Values.Sum(aWeapon => aWeapon.Kills) +
                        (Int32)currentStats.VehicleStats.Values.Sum(aVehicle => aVehicle.Kills);
                    statKillDiff = currentWeaponKillCount - previousWeaponKillCount;
                    killStatsValid = serverKillDiff >= statKillDiff - 1;
                }

                List<String> allowedCategories;
                switch (GameVersion)
                {
                    case GameVersionEnum.BF3:
                        allowedCategories = new List<string> {
                            "sub_machine_guns",
                            "assault_rifles",
                            "carbines",
                            "machine_guns",
                            "handheld_weapons"
                        };
                        break;
                    case GameVersionEnum.BF4:
                        allowedCategories = new List<string> {
                            "pdws",
                            "assault_rifles",
                            "carbines",
                            "lmgs",
                            "handguns"
                        };
                        break;
                    case GameVersionEnum.BFHL:
                        allowedCategories = new List<string> {
                            "assault_rifles",
                            "ar_standard",
                            "handguns",
                            "pistols",
                            "machine_pistols",
                            "revolvers",
                            "smg_mechanic",
                            "smg"
                        };
                        break;
                    default:
                        return false;
                }
                List<AWeaponStat> topWeapons = currentStats.WeaponStats.Values.OrderByDescending(aStat => aStat.Kills).ToList();

                AWeaponStat actedWeapon = null;
                Double actedPerc = -1;
                foreach (AWeaponStat weaponStat in topWeapons)
                {
                    //Only count certain weapon categories
                    if (allowedCategories.Contains(weaponStat.Category))
                    {
                        Boolean isSidearm =
                            weaponStat.Category == "handheld_weapons" ||
                            weaponStat.Category == "handguns" ||
                            weaponStat.Category == "pistols" ||
                            weaponStat.Category == "machine_pistols" ||
                            weaponStat.Category == "revolvers";
                        StatLibraryWeapon weapon;
                        if (_StatLibrary.Weapons.TryGetValue(weaponStat.ID, out weapon))
                        {
                            //Only handle weapons that do < 50 max dps
                            if (weapon.DamageMax < 50)
                            {
                                //For live stat check, look for previous round stat difference and valid stat difference
                                if (_useAntiCheatLIVESystem &&
                                    previousStats != null &&
                                    previousStats.WeaponStats != null)
                                {
                                    AWeaponStat previousWeaponStat;
                                    if (previousStats.WeaponStats.TryGetValue(weaponStat.ID, out previousWeaponStat))
                                    {
                                        if (weaponStat.Kills > previousWeaponStat.Kills && killStatsValid)
                                        {
                                            //Handle servers with different health amounts
                                            Double weaponHitsToKill = (_soldierHealth / weapon.DamageMax);
                                            Double killDiff = weaponStat.Kills - previousWeaponStat.Kills;
                                            Double hitDiff = weaponStat.Hits - previousWeaponStat.Hits;
                                            Double HSDiff = weaponStat.Headshots - previousWeaponStat.Headshots;
                                            //Reject processing of invalid data returned from battlelog
                                            if (killDiff <= 0 || hitDiff <= 0 || HSDiff < 0)
                                            {
                                                continue;
                                            }
                                            Double liveDPS = (killDiff / hitDiff) * _soldierHealth;
                                            //Coerce the live damage
                                            if (liveDPS < 0)
                                            {
                                                liveDPS = 0;
                                            }
                                            Double expectedHits = (HSDiff * weaponHitsToKill / 2) + ((killDiff - HSDiff) * weaponHitsToKill);
                                            Double expectedDPS = (killDiff / expectedHits) * _soldierHealth;
                                            //Coerce the expected damage
                                            if (expectedDPS < 0)
                                            {
                                                expectedDPS = 0;
                                            }
                                            Double percDiff = (liveDPS - expectedDPS) / expectedDPS;
                                            String formattedName = weaponStat.ID.Replace("-", "").Replace(" ", "").ToUpper();
                                            if (Math.Round(percDiff) > 0 && killDiff > 2)
                                            {
                                                Log.Info("STATDIFF - " + aPlayer.GetVerboseName() + " - " + formattedName + " [" + killDiff + "/" + hitDiff + "][" + Math.Round(liveDPS) + " DPS][" + ((Math.Round(percDiff * 100) > 0) ? ("+") : ("")) + Math.Round(percDiff * 100) + "%]");
                                            }
                                            //Check for damage mod
                                            //Require at least 12 kills difference, +75% normal weapon damage for non-sidearm weapons, and 85 DPS weapon damage for sidearms.
                                            if (killDiff >= 12 &&
                                                liveDPS > weapon.DamageMax &&
                                                (liveDPS >= 85 || (!isSidearm && percDiff > 0.75)))
                                            {
                                                Log.Info(aPlayer.GetVerboseName() + " auto-banned for damage mod. [LIVE][" + formattedName + "-" + (int)liveDPS + "-" + (int)killDiff + "-" + (int)HSDiff + "-" + (int)hitDiff + "]");
                                                if (!debugMode)
                                                {
                                                    //Create the ban record
                                                    QueueRecordForProcessing(new ARecord
                                                    {
                                                        record_source = ARecord.Sources.Automated,
                                                        server_id = _serverInfo.ServerID,
                                                        command_type = GetCommandByKey("player_ban_perm"),
                                                        command_numeric = 0,
                                                        target_name = aPlayer.player_name,
                                                        target_player = aPlayer,
                                                        source_name = "AutoAdmin",
                                                        record_message = _AntiCheatDPSBanMessage + " [LIVE]" + (killStatsValid ? "" : "[CAUTION]") + "[4-" + formattedName + "-" + (int)liveDPS + "-" + (int)killDiff + "-" + (int)HSDiff + "-" + (int)hitDiff + "]",
                                                        record_time = UtcNow()
                                                    });
                                                }
                                                return true;
                                            }
                                        }
                                    }
                                }
                                //For full stat check only take weapons with more than 50 kills
                                if (weaponStat.Kills > 50)
                                {
                                    //Check for damage hack
                                    if (weaponStat.DPS > weapon.DamageMax && (!_UseHskChecker || weaponStat.HSKR < (_HskTriggerLevel / 100)))
                                    {
                                        //Account for hsk ratio with the weapon
                                        Double expectedDmg = weapon.DamageMax * (1 + weaponStat.HSKR);
                                        //Get the percentage over normal
                                        Double percDiff = (weaponStat.DPS - expectedDmg) / expectedDmg;
                                        Double triggerLevel = ((_soldierHealth > 65) ? (0.50) : (0.60));
                                        //Increase trigger level for kill counts under 100
                                        if (weaponStat.Kills < 100)
                                        {
                                            triggerLevel = triggerLevel * 1.8;
                                        }
                                        //Increase trigger level for sidearms
                                        if (isSidearm)
                                        {
                                            triggerLevel = triggerLevel * 1.5;
                                        }
                                        if (percDiff > triggerLevel && percDiff > actedPerc)
                                        {
                                            //Act on the weapon
                                            actedPerc = percDiff;
                                            actedWeapon = weaponStat;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.Warn("Could not find damage stats for " + weaponStat.Category + ":" + weaponStat.ID + " in " + GameVersion + " library of " + _StatLibrary.Weapons.Count + " weapons.");
                        }
                    }
                }
                if (actedWeapon != null)
                {
                    acted = true;
                    String formattedName = actedWeapon.ID.Replace("-", "").Replace(" ", "").ToUpper();
                    if (_roundState == RoundState.Playing)
                    {
                        if (!aPlayer.IsLocked())
                        {
                            APlayer banPlayer = aPlayer;
                            banPlayer.Lock("AutoAdmin", TimeSpan.FromMinutes(10));
                            //Special case. Let server live with the hacker for 1 minute then watch them be banned
                            Thread banDelayThread = new Thread(new ThreadStart(delegate
                            {
                                Log.Debug(() => "Starting a ban delay thread.", 5);
                                try
                                {
                                    Thread.CurrentThread.Name = "BanDelay";
                                    DateTime start = UtcNow();
                                    Log.Info(banPlayer.GetVerboseName() + " will be DPS banned. Waiting for starting case.");
                                    OnlineAdminTellMessage(banPlayer.GetVerboseName() + " will be DPS banned. Waiting for starting case.");
                                    while (banPlayer.player_online && !banPlayer.player_spawnedOnce && (UtcNow() - start).TotalSeconds < 300)
                                    {
                                        if (!_pluginEnabled)
                                        {
                                            break;
                                        }
                                        //Wait for trigger case to start timer
                                        Threading.Wait(1000);
                                    }
                                    //Onced triggered, ban after 90 seconds.
                                    OnlineAdminTellMessage(banPlayer.GetVerboseName() + " triggered DPS timer. [" + formattedName + "-" + (int)actedWeapon.DPS + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "] They will be banned in 90 seconds.");
                                    Threading.Wait(TimeSpan.FromSeconds(83));
                                    PlayerTellMessage(banPlayer.player_name, "Thank you for making our system look good. Goodbye.", true, 6);
                                    Threading.Wait(TimeSpan.FromSeconds(7));

                                    Log.Info(aPlayer.GetVerboseName() + " auto-banned for damage mod. [" + formattedName + "-" + (int)actedWeapon.DPS + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]");
                                    if (!debugMode)
                                    {
                                        //Unlock the player
                                        banPlayer.Unlock();
                                        //Create the ban record
                                        ARecord record = new ARecord
                                        {
                                            record_source = ARecord.Sources.Automated,
                                            server_id = _serverInfo.ServerID,
                                            command_type = GetCommandByKey("player_ban_perm"),
                                            command_numeric = 0,
                                            target_name = aPlayer.player_name,
                                            target_player = aPlayer,
                                            source_name = "AutoAdmin",
                                            record_message = _AntiCheatDPSBanMessage + " [4-" + formattedName + "-" + (int)actedWeapon.DPS + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]",
                                            record_time = UtcNow()
                                        };
                                        //Process the record
                                        QueueRecordForProcessing(record);
                                    }
                                }
                                catch (Exception)
                                {
                                    Log.HandleException(new AException("Error while runnin ban delay."));
                                }
                                Log.Debug(() => "Exiting a ban delay thread.", 5);
                                Threading.StopWatchdog();
                            }));

                            //Start the thread
                            Threading.StartWatchdog(banDelayThread);
                        }
                    }
                    else
                    {
                        Log.Info(aPlayer.GetVerboseName() + " auto-banned for damage mod. [" + formattedName + "-" + (int)actedWeapon.DPS + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]");
                        if (!debugMode)
                        {
                            //Create the ban record
                            ARecord record = new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_ban_perm"),
                                command_numeric = 0,
                                target_name = aPlayer.player_name,
                                target_player = aPlayer,
                                source_name = "AutoAdmin",
                                record_message = _AntiCheatDPSBanMessage + " [4-" + formattedName + "-" + (int)actedWeapon.DPS + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]",
                                record_time = UtcNow()
                            };
                            //Process the record
                            QueueRecordForProcessing(record);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running DPS hack check", e));
            }
            return acted;
        }

        private Boolean AimbotHackCheck(APlayer aPlayer, Boolean debugMode)
        {
            Boolean acted = false;
            try
            {
                APlayerStats stats;
                if (aPlayer == null || !aPlayer.RoundStats.TryGetValue(_roundID, out stats) || stats.WeaponStats == null)
                {
                    return false;
                }
                APlayerStats previousStats;
                aPlayer.RoundStats.TryGetValue(_roundID - 1, out previousStats);
                List<String> allowedCategories;
                switch (GameVersion)
                {
                    case GameVersionEnum.BF3:
                        allowedCategories = new List<string> {
                            "sub_machine_guns",
                            "assault_rifles",
                            "carbines",
                            "machine_guns"
                        };
                        break;
                    case GameVersionEnum.BF4:
                        allowedCategories = new List<string> {
                            "pdws",
                            "assault_rifles",
                            "carbines",
                            "lmgs"
                        };
                        break;
                    case GameVersionEnum.BFHL:
                        allowedCategories = new List<string> {
                            "assault_rifles",
                            "ar_standard",
                            "machine_pistols",
                            "smg_mechanic",
                            "smg"
                        };
                        break;
                    default:
                        return false;
                }
                List<AWeaponStat> topWeapons = stats.WeaponStats.Values.ToList();
                topWeapons.Sort(delegate (AWeaponStat a1, AWeaponStat a2)
                {
                    if (Math.Abs(a1.Kills - a2.Kills) < 0.001)
                    {
                        return 0;
                    }
                    return (a1.Kills < a2.Kills) ? (1) : (-1);
                });

                AWeaponStat actedWeapon = null;
                Double actedHskr = -1;
                foreach (AWeaponStat weaponStat in topWeapons)
                {
                    //Only count certain weapon categories
                    if (allowedCategories.Contains(weaponStat.Category))
                    {
                        StatLibraryWeapon weapon;
                        if (_StatLibrary.Weapons.TryGetValue(weaponStat.ID, out weapon))
                        {
                            //Only take weapons with more than 100 kills, and less than 50% damage
                            if (weaponStat.Kills > 100 && weapon.DamageMax < 50)
                            {
                                //Check for aimbot hack
                                Log.Debug(() => "Checking " + weaponStat.ID + " HSKR (" + weaponStat.HSKR + " >? " + (_HskTriggerLevel / 100) + ")", 6);
                                if (weaponStat.HSKR > (_HskTriggerLevel / 100))
                                {
                                    if (weaponStat.HSKR > actedHskr)
                                    {
                                        actedHskr = weaponStat.HSKR;
                                        actedWeapon = weaponStat;
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.Warn("Could not find damage stats for " + weaponStat.Category + ":" + weaponStat.ID + " in " + GameVersion + " library of " + _StatLibrary.Weapons.Count + " weapons.");
                        }
                    }
                }
                if (actedWeapon != null)
                {
                    acted = true;
                    String formattedName = actedWeapon.ID.Replace("-", "").Replace(" ", "").ToUpper();
                    if (_roundState == RoundState.Playing)
                    {
                        if (!aPlayer.IsLocked())
                        {
                            APlayer banPlayer = aPlayer;
                            banPlayer.Lock("AutoAdmin", TimeSpan.FromMinutes(10));
                            //Special case. Let server live with the hacker for 1 minute then watch them be banned
                            Thread banDelayThread = new Thread(new ThreadStart(delegate
                            {
                                Log.Debug(() => "Starting a ban delay thread.", 5);
                                try
                                {
                                    Thread.CurrentThread.Name = "BanDelay";
                                    DateTime start = UtcNow();
                                    Log.Info(banPlayer.GetVerboseName() + " will be HSK banned. Waiting for starting case.");
                                    OnlineAdminTellMessage(banPlayer.GetVerboseName() + " will be HSK banned. Waiting for starting case.");
                                    while (_roundState == RoundState.Playing && banPlayer.player_online && !banPlayer.player_spawnedOnce && (UtcNow() - start).TotalSeconds < 300)
                                    {
                                        if (!_pluginEnabled)
                                        {
                                            break;
                                        }
                                        //Wait for trigger case to start timer
                                        Threading.Wait(1000);
                                    }
                                    //Onced triggered, ban after 90 seconds.
                                    OnlineAdminTellMessage(banPlayer.GetVerboseName() + " triggered HSK timer. [" + formattedName + "-" + (int)(actedWeapon.HSKR * 100) + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "] They will be banned in 90 seconds.");
                                    Threading.Wait(TimeSpan.FromSeconds(83));
                                    if (actedWeapon.HSKR >= .75)
                                    {
                                        PlayerTellMessage(banPlayer.player_name, "Thank you for making our system look good. Goodbye.", true, 6);
                                    }
                                    Threading.Wait(TimeSpan.FromSeconds(7));

                                    Log.Info(banPlayer.GetVerboseName() + " auto-banned for aimbot. [6-" + formattedName + "-" + (int)(actedWeapon.HSKR * 100) + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]");
                                    if (!debugMode)
                                    {
                                        //Unlock player
                                        banPlayer.Unlock();
                                        //Create the ban record
                                        ARecord record = new ARecord
                                        {
                                            record_source = ARecord.Sources.Automated,
                                            server_id = _serverInfo.ServerID,
                                            command_type = GetCommandByKey("player_ban_perm"),
                                            command_numeric = 0,
                                            target_name = banPlayer.player_name,
                                            target_player = banPlayer,
                                            source_name = "AutoAdmin",
                                            record_message = _AntiCheatHSKBanMessage + " [6-" + formattedName + "-" + (int)(actedWeapon.HSKR * 100) + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]",
                                            record_time = UtcNow()
                                        };
                                        //Process the record
                                        QueueRecordForProcessing(record);
                                    }
                                }
                                catch (Exception)
                                {
                                    Log.HandleException(new AException("Error while runnin ban delay."));
                                }
                                Log.Debug(() => "Exiting a ban delay thread.", 5);
                                Threading.StopWatchdog();
                            }));

                            //Start the thread
                            Threading.StartWatchdog(banDelayThread);
                        }
                    }
                    else
                    {
                        Log.Info(aPlayer.GetVerboseName() + " auto-banned for aimbot. [6-" + formattedName + "-" + (int)(actedWeapon.HSKR * 100) + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]");
                        if (!debugMode)
                        {
                            //Create the ban record
                            ARecord record = new ARecord
                            {
                                record_source = ARecord.Sources.Automated,
                                server_id = _serverInfo.ServerID,
                                command_type = GetCommandByKey("player_ban_perm"),
                                command_numeric = 0,
                                target_name = aPlayer.player_name,
                                target_player = aPlayer,
                                source_name = "AutoAdmin",
                                record_message = _AntiCheatHSKBanMessage + " [6-" + formattedName + "-" + (int)(actedWeapon.HSKR * 100) + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]",
                                record_time = UtcNow()
                            };
                            //Process the record
                            QueueRecordForProcessing(record);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running HSK hack check.", e));
            }
            return acted;
        }

        private Boolean KPMHackCheck(APlayer aPlayer, Boolean debugMode)
        {
            Boolean acted = false;
            try
            {
                APlayerStats stats;
                if (aPlayer == null || !aPlayer.RoundStats.TryGetValue(_roundID, out stats) || stats.WeaponStats == null)
                {
                    return false;
                }
                APlayerStats previousStats;
                aPlayer.RoundStats.TryGetValue(_roundID - 1, out previousStats);
                List<String> allowedCategories;
                switch (GameVersion)
                {
                    case GameVersionEnum.BF3:
                        allowedCategories = new List<string> {
                            "assault_rifles",
                            "carbines",
                            "sub_machine_guns",
                            "machine_guns"
                        };
                        break;
                    case GameVersionEnum.BF4:
                        allowedCategories = new List<string> {
                            "assault_rifles",
                            "carbines",
                            "dmrs",
                            "lmgs",
                            "sniper_rifles",
                            "pdws",
                            "shotguns"
                        };
                        break;
                    case GameVersionEnum.BFHL:
                        allowedCategories = new List<string> {
                            "assault_rifles",
                            "ar_standard",
                            "sr_standard",
                            "br_standard",
                            "shotguns",
                            "smg_mechanic",
                            "sg_enforcer",
                            "smg"
                        };
                        break;
                    default:
                        return false;
                }
                //Wow, i wrote this before knowing linq, this looks terrible
                List<AWeaponStat> topWeapons = stats.WeaponStats.Values.ToList();
                topWeapons.Sort(delegate (AWeaponStat a1, AWeaponStat a2)
                {
                    if (a1.Kills == a2.Kills)
                    {
                        return 0;
                    }
                    return (a1.Kills < a2.Kills) ? (1) : (-1);
                });

                AWeaponStat actedWeapon = null;
                Double actedKpm = -1;
                foreach (AWeaponStat weaponStat in topWeapons)
                {
                    //Only count certain weapon categories, and ignore gadgets/sidearms (shotgun issue with BF4)
                    if (allowedCategories.Contains(weaponStat.Category) &&
                        weaponStat.CategorySID != "WARSAW_ID_P_CAT_GADGET" &&
                        weaponStat.CategorySID != "WARSAW_ID_P_CAT_SIDEARM")
                    {
                        //Only take weapons with more than 200 kills
                        if (weaponStat.Kills > 200)
                        {
                            //Check for KPM limit
                            Log.Debug(() => "Checking " + weaponStat.ID + " KPM (" + String.Format("{0:0.00}", weaponStat.KPM) + " >? " + (_KpmTriggerLevel) + ")", 6);
                            if (weaponStat.KPM > (_KpmTriggerLevel))
                            {
                                if (weaponStat.KPM > actedKpm)
                                {
                                    actedKpm = weaponStat.KPM;
                                    actedWeapon = weaponStat;
                                }
                            }
                        }
                    }
                }
                if (actedWeapon != null)
                {
                    acted = true;
                    String formattedName = actedWeapon.ID.Replace("-", "").Replace(" ", "").ToUpper();
                    Log.Info(aPlayer.GetVerboseName() + ((debugMode) ? (" debug") : (" auto")) + "-banned for KPM. [" + formattedName + "-" + String.Format("{0:0.00}", actedWeapon.KPM) + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]");
                    if (!debugMode)
                    {
                        //Create the ban record
                        ARecord record = new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_ban_perm"),
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "AutoAdmin",
                            record_message = _AntiCheatKPMBanMessage + " [5-" + formattedName + "-" + String.Format("{0:0.00}", actedWeapon.KPM) + "-" + (int)actedWeapon.Kills + "-" + (int)actedWeapon.Headshots + "-" + (int)actedWeapon.Hits + "]",
                            record_time = UtcNow()
                        };
                        //Process the record
                        QueueRecordForProcessing(record);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running KPM hack check.", e));
            }
            return acted;
        }

        //all messaging is redirected to global chat for analysis
        public override void OnGlobalChat(String speaker, String message)
        {
            try
            {
                message = message.Trim();
                AChatMessage chatMessage = new AChatMessage()
                {
                    Speaker = speaker,
                    Message = message,
                    OriginalMessage = message,
                    Subset = AChatMessage.ChatSubset.Global,
                    Hidden = message.Trim().StartsWith("/"),
                    SubsetTeamID = -1,
                    SubsetSquadID = -1,
                    Timestamp = UtcNow()
                };
                APlayer aPlayer;
                if (_PlayerDictionary.TryGetValue(speaker, out aPlayer))
                {
                    if (aPlayer.fbpInfo != null)
                    {
                        chatMessage.SubsetTeamID = aPlayer.fbpInfo.TeamID;
                        chatMessage.SubsetSquadID = aPlayer.fbpInfo.SquadID;
                    }
                    aPlayer.player_chatOnce = true;
                }
                HandleChat(chatMessage);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error when handling OnGlobalChat", e));
            }
        }

        public override void OnTeamChat(String speaker, String message, Int32 teamId)
        {
            try
            {
                message = message.Trim();
                AChatMessage chatMessage = new AChatMessage()
                {
                    Speaker = speaker,
                    Message = message,
                    OriginalMessage = message,
                    Subset = AChatMessage.ChatSubset.Team,
                    Hidden = message.Trim().StartsWith("/"),
                    SubsetTeamID = teamId,
                    SubsetSquadID = -1,
                    Timestamp = UtcNow()
                };
                APlayer aPlayer;
                if (_PlayerDictionary.TryGetValue(speaker, out aPlayer))
                {
                    if (aPlayer.fbpInfo != null)
                    {
                        chatMessage.SubsetSquadID = aPlayer.fbpInfo.SquadID;
                    }
                }
                HandleChat(chatMessage);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error when handling OnTeamChat", e));
            }
        }

        public override void OnSquadChat(String speaker, String message, Int32 teamId, Int32 squadId)
        {
            try
            {
                message = message.Trim();
                AChatMessage chatMessage = new AChatMessage()
                {
                    Speaker = speaker,
                    Message = message,
                    OriginalMessage = message,
                    Subset = AChatMessage.ChatSubset.Squad,
                    Hidden = message.Trim().StartsWith("/"),
                    SubsetTeamID = teamId,
                    SubsetSquadID = squadId,
                    Timestamp = UtcNow()
                };
                HandleChat(chatMessage);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error when handling OnSquadChat", e));
            }
        }

        private void HandleChat(AChatMessage messageObject)
        {
            Log.Debug(() => "Entering handleChat", 7);
            try
            {
                if (_pluginEnabled)
                {
                    //If message contains comorose just return and ignore
                    if (messageObject.OriginalMessage.Contains("ID_CHAT"))
                    {
                        return;
                    }
                    QueueMessageForParsing(messageObject);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while processing inbound chat messages.", e));
            }
            Log.Debug(() => "Exiting handleChat", 7);
        }

        public void SendMessageToSource(ARecord record, String message)
        {
            Log.Debug(() => "Entering sendMessageToSource", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    return;
                }
                switch (record.record_source)
                {
                    case ARecord.Sources.InGame:
                        PlayerSayMessage(record.source_name, message);
                        break;
                    case ARecord.Sources.ServerCommand:
                        ProconChatWrite(Log.FBold(message));
                        break;
                    case ARecord.Sources.Settings:
                        Log.Write(message);
                        break;
                    case ARecord.Sources.Database:
                        //Do nothing, no way to communicate to source when database
                        break;
                    case ARecord.Sources.Automated:
                        //Do nothing, no source to communicate with
                        break;
                    case ARecord.Sources.ExternalPlugin:
                        record.debugMessages.Add(message);
                        break;
                    case ARecord.Sources.HTTP:
                        record.debugMessages.Add(message);
                        break;
                    default:
                        Log.Warn("Command source not set, or not recognized.");
                        break;
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while sending message to record source.", e);
                Log.HandleException(record.record_exception);
            }
            Log.Debug(() => "Exiting sendMessageToSource", 7);
        }

        public Boolean OnlineNonWhitelistSayMessage(String message)
        {
            return OnlineNonWhitelistSayMessage(message, true);
        }

        public Boolean OnlineNonWhitelistSayMessage(String message, Boolean displayProconChat)
        {
            Boolean nonWhitelistedTold = false;
            try
            {
                Dictionary<String, APlayer> whitelistedPlayers = GetOnlinePlayerDictionaryOfGroup("whitelist_spambot");
                foreach (APlayer aPlayer in _PlayerDictionary.Values.ToList())
                {
                    if (!whitelistedPlayers.ContainsKey(aPlayer.player_name))
                    {
                        // Hedius: Remove !PlayerIsAdmin(aPlayer)? This blocks this exclusion for admins hmmm
                        if ((_spamBotExcludeHighReputation && aPlayer.player_reputation >= _reputationThresholdGood && !PlayerIsAdmin(aPlayer)) ||
                            (message.ToLower().Contains("donat") && aPlayer.player_serverplaytime.TotalHours <= 5.0) ||
                            (message.ToLower().Contains("reserve") && _populationStatus != PopulationState.High) ||
                            (_spamBotExcludeTeamspeakDiscord && (_TeamspeakPlayers.ContainsKey(aPlayer.player_name) || _DiscordPlayers.ContainsKey(aPlayer.player_name))) ||
                             aPlayer.player_serverplaytime.TotalHours < (double)_spamBotMinPlaytimeHours)
                        {
                            whitelistedPlayers[aPlayer.player_name] = aPlayer;
                        }
                    }
                }
                const string bypassPrefix = "[whitelistbypass]";
                var bypass = false;
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                    bypass = true;
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (bypass)
                {
                    whitelistedPlayers.Clear();
                }
                if (whitelistedPlayers.Any())
                {
                    Thread nonAdminSayThread = new Thread(new ThreadStart(delegate
                    {
                        Log.Debug(() => "Starting an online non-whitelist say thread.", 8);
                        try
                        {
                            Thread.CurrentThread.Name = "OnlineNonAdminSay";
                            var spambotMessage = false;
                            if (message.Contains("[SpamBotMessage]"))
                            {
                                message = message.Replace("[SpamBotMessage]", "");
                                spambotMessage = true;
                            }
                            if (displayProconChat)
                            {
                                ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Say (" + (!_spamBotExcludeAdmins ? "Admins & " : "") + whitelistedPlayers.Count + " Whitelisted Players) > " + message);
                            }
                            //Process will take ~2 seconds for a full server
                            foreach (APlayer aPlayer in _PlayerDictionary.Values.ToList())
                            {
                                if (whitelistedPlayers.ContainsKey(aPlayer.player_name))
                                {
                                    continue;
                                }
                                nonWhitelistedTold = true;
                                aPlayer.Say(message, false, 1);
                                Thread.Sleep(30);
                            }
                        }
                        catch (Exception)
                        {
                            Log.HandleException(new AException("Error while running online non-whitelist say."));
                        }
                        Log.Debug(() => "Exiting an online non-whitelist say thread.", 8);
                        Threading.StopWatchdog();
                    }));
                    Threading.StartWatchdog(nonAdminSayThread);
                }
                else
                {
                    AdminSayMessage(message, displayProconChat);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running non-whitelist admin say.", e));
            }
            return nonWhitelistedTold;
        }

        public Boolean OnlineNonWhitelistYellMessage(String message)
        {
            return OnlineNonWhitelistYellMessage(message, true);
        }

        public Boolean OnlineNonWhitelistYellMessage(String message, Boolean displayProconChat)
        {
            Boolean nonWhitelistedTold = false;
            try
            {
                Dictionary<String, APlayer> whitelistedPlayers = GetOnlinePlayerDictionaryOfGroup("whitelist_spambot");
                foreach (APlayer aPlayer in _PlayerDictionary.Values.ToList())
                {
                    if (!whitelistedPlayers.ContainsKey(aPlayer.player_name))
                    {
                        if ((_spamBotExcludeHighReputation && aPlayer.player_reputation >= _reputationThresholdGood && !PlayerIsAdmin(aPlayer)) ||
                            (message.ToLower().Contains("donat") && aPlayer.player_serverplaytime.TotalHours <= 50.0) ||
                            (message.ToLower().Contains("reserve") && _populationStatus != PopulationState.High) ||
                            (_spamBotExcludeTeamspeakDiscord && (_TeamspeakPlayers.ContainsKey(aPlayer.player_name) || _DiscordPlayers.ContainsKey(aPlayer.player_name))) ||
                            aPlayer.player_serverplaytime.TotalHours < (double)_spamBotMinPlaytimeHours)
                        {
                            whitelistedPlayers[aPlayer.player_name] = aPlayer;
                        }
                    }
                }
                const string bypassPrefix = "[whitelistbypass]";
                var bypass = false;
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                    bypass = true;
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (bypass)
                {
                    whitelistedPlayers.Clear();
                }
                if (whitelistedPlayers.Any())
                {
                    Thread nonAdminYellThread = new Thread(new ThreadStart(delegate
                    {
                        Log.Debug(() => "Starting an online non-whitelist yell thread.", 8);
                        try
                        {
                            Thread.CurrentThread.Name = "OnlineNonAdminYell";
                            var spambotMessage = false;
                            if (message.Contains("[SpamBotMessage]"))
                            {
                                message = message.Replace("[SpamBotMessage]", "");
                                spambotMessage = true;
                            }
                            if (displayProconChat)
                            {
                                ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Yell[" + _YellDuration + "s] (" + (!_spamBotExcludeAdmins ? "Admins & " : "") + whitelistedPlayers.Count + " Whitelisted Players) > " + message);
                            }
                            //Process will take ~2 seconds for a full server
                            foreach (APlayer aPlayer in _PlayerDictionary.Values.ToList())
                            {
                                if (whitelistedPlayers.ContainsKey(aPlayer.player_name))
                                {
                                    continue;
                                }
                                nonWhitelistedTold = true;
                                PlayerYellMessage(aPlayer.player_name, message, false, 1);
                                Thread.Sleep(30);
                            }
                        }
                        catch (Exception)
                        {
                            Log.HandleException(new AException("Error while running online non-whitelist yell."));
                        }
                        Log.Debug(() => "Exiting an online non-whitelist yell thread.", 8);
                        Threading.StopWatchdog();
                    }));
                    Threading.StartWatchdog(nonAdminYellThread);
                }
                else
                {
                    AdminYellMessage(message, displayProconChat, 0);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running non-whitelist admin yell.", e));
            }
            return nonWhitelistedTold;
        }

        public Boolean OnlineNonWhitelistTellMessage(String message)
        {
            return OnlineNonWhitelistTellMessage(message, true);
        }

        public Boolean OnlineNonWhitelistTellMessage(String message, Boolean displayProconChat)
        {
            Boolean nonWhitelistedTold = false;
            try
            {
                Dictionary<String, APlayer> whitelistedPlayers = GetOnlinePlayerDictionaryOfGroup("whitelist_spambot");
                foreach (APlayer aPlayer in _PlayerDictionary.Values.ToList())
                {
                    if (!whitelistedPlayers.ContainsKey(aPlayer.player_name))
                    {
                        if ((_spamBotExcludeHighReputation && aPlayer.player_reputation >= _reputationThresholdGood && !PlayerIsAdmin(aPlayer)) ||
                            (message.ToLower().Contains("donat") && aPlayer.player_serverplaytime.TotalHours <= 50.0) ||
                            (message.ToLower().Contains("reserve") && _populationStatus != PopulationState.High) ||
                            (_spamBotExcludeTeamspeakDiscord && (_TeamspeakPlayers.ContainsKey(aPlayer.player_name) || _DiscordPlayers.ContainsKey(aPlayer.player_name))) ||
                             aPlayer.player_serverplaytime.TotalHours < (double)_spamBotMinPlaytimeHours)
                        {
                            whitelistedPlayers[aPlayer.player_name] = aPlayer;
                        }
                    }
                }
                const string bypassPrefix = "[whitelistbypass]";
                var bypass = false;
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                    bypass = true;
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (bypass)
                {
                    whitelistedPlayers.Clear();
                }
                if (FetchOnlineAdminSoldiers().Any() || whitelistedPlayers.Any())
                {
                    Thread nonAdminTellThread = new Thread(new ThreadStart(delegate
                    {
                        Log.Debug(() => "Starting an online non-whitelist tell thread.", 8);
                        try
                        {
                            Thread.CurrentThread.Name = "OnlineNonAdminTell";
                            var spambotMessage = false;
                            if (message.Contains("[SpamBotMessage]"))
                            {
                                message = message.Replace("[SpamBotMessage]", "");
                                spambotMessage = true;
                            }
                            if (displayProconChat)
                            {
                                ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Tell[" + _YellDuration + "s] (" + (!_spamBotExcludeAdmins ? "Admins & " : "") + whitelistedPlayers.Count + " Whitelisted Players) > " + message);
                            }
                            //Process will take ~2 seconds for a full server
                            foreach (APlayer aPlayer in _PlayerDictionary.Values.ToList())
                            {
                                if (whitelistedPlayers.ContainsKey(aPlayer.player_name))
                                {
                                    continue;
                                }
                                nonWhitelistedTold = true;
                                PlayerTellMessage(aPlayer.player_name, message, false, 1);
                                Thread.Sleep(30);
                            }
                        }
                        catch (Exception)
                        {
                            Log.HandleException(new AException("Error while running online non-whitelist tell."));
                        }
                        Log.Debug(() => "Exiting an online non-whitelist tell thread.", 8);
                        Threading.StopWatchdog();
                    }));
                    Threading.StartWatchdog(nonAdminTellThread);
                }
                else
                {
                    AdminTellMessage(message, displayProconChat);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running non-whitelist admin tell.", e));
            }
            return nonWhitelistedTold;
        }

        public Boolean OnlineAdminSayMessage(String message)
        {
            return OnlineAdminSayMessage(message, null);
        }

        public Boolean OnlineAdminSayMessage(String message, String exclude)
        {
            ProconChatWrite(Log.FBold(Log.CMaroon(message)));
            Boolean adminsTold = false;
            foreach (APlayer player in FetchOnlineAdminSoldiers().Where(aPlayer => aPlayer.player_name != exclude))
            {
                adminsTold = true;
                player.Say(message, true, 1);
            }
            return adminsTold;
        }

        public Boolean OnlineAdminYellMessage(String message)
        {
            ProconChatWrite(Log.FBold(Log.CMaroon(message)));
            Boolean adminsTold = false;
            foreach (APlayer player in FetchOnlineAdminSoldiers())
            {
                adminsTold = true;
                PlayerYellMessage(player.player_name, message, true, 1);
            }
            return adminsTold;
        }

        public Boolean OnlineAdminTellMessage(String message)
        {
            ProconChatWrite(Log.FBold(Log.CMaroon(message)));
            Boolean adminsTold = false;
            foreach (APlayer player in FetchOnlineAdminSoldiers())
            {
                adminsTold = true;
                PlayerTellMessage(player.player_name, message, true, 1);
            }
            return adminsTold;
        }

        public void AdminSayMessage(String message)
        {
            AdminSayMessage(message, true);
        }

        public void AdminSayMessage(String message, Boolean displayProconChat)
        {
            Log.Debug(() => "Entering adminSay", 7);
            try
            {
                message = message.Trim();
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("Attempted to say an empty message.");
                    return;
                }
                var spambotMessage = false;
                if (message.Contains("[SpamBotMessage]"))
                {
                    message = message.Replace("[SpamBotMessage]", "");
                    spambotMessage = true;
                }
                const string bypassPrefix = "[whitelistbypass]";
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (displayProconChat)
                {
                    ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Say > " + message);
                }
                string[] messageSplit = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                int maxLineLength = 127;
                foreach (String subMessage in messageSplit)
                {
                    int charCount = 0;
                    IEnumerable<string> lines = subMessage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).GroupBy(w => (charCount += w.Length + 1) / maxLineLength).Select(g => string.Join(" ", g.ToArray()));
                    foreach (string line in lines)
                    {
                        ExecuteCommand("procon.protected.send", "admin.say", Log.FClear(line), "all");
                        Threading.Wait(25);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while sending admin say.", e));
            }
            Log.Debug(() => "Exiting adminSay", 7);
        }

        public void PlayerSayMessage(String target, String message)
        {
            PlayerSayMessage(target, message, true, 1);
        }

        public void PlayerSayMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            Log.Debug(() => "Entering playerSayMessage", 7);
            try
            {
                message = message.Trim();
                if (String.IsNullOrEmpty(target) || String.IsNullOrEmpty(message))
                {
                    Log.Error("target or message null in playerSayMessage");
                    return;
                }
                var spambotMessage = false;
                if (message.Contains("[SpamBotMessage]"))
                {
                    message = message.Replace("[SpamBotMessage]", "");
                    spambotMessage = true;
                }
                const string bypassPrefix = "[whitelistbypass]";
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (displayProconChat)
                {
                    ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Say > " + Log.CBlue(target) + " > " + message);
                }
                string[] messageSplit = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                int maxLineLength = 127;
                foreach (String subMessage in messageSplit)
                {
                    int charCount = 0;
                    IEnumerable<string> lines = subMessage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).GroupBy(w => (charCount += w.Length + 1) / maxLineLength).Select(g => string.Join(" ", g.ToArray()));
                    foreach (string line in lines)
                    {
                        ExecuteCommand("procon.protected.send", "admin.say", Log.FClear(line), "player", target);
                        Threading.Wait(25);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while sending message to player.", e));
            }
            Log.Debug(() => "Exiting playerSayMessage", 7);
        }

        public void AdminYellMessage(String message)
        {
            AdminYellMessage(message, true, 0);
        }

        public void AdminYellMessage(String message, Boolean displayProconChat, Int32 overrideYellDuration)
        {
            Log.Debug(() => "Entering adminYell", 7);
            try
            {
                message = message.Trim();
                var duration = _YellDuration;
                if (overrideYellDuration > 0)
                {
                    duration = overrideYellDuration;
                }
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("message null in adminYell");
                    return;
                }
                var spambotMessage = false;
                if (message.Contains("[SpamBotMessage]"))
                {
                    message = message.Replace("[SpamBotMessage]", "");
                    spambotMessage = true;
                }
                const string bypassPrefix = "[whitelistbypass]";
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (displayProconChat)
                {
                    ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Yell[" + duration + "s] > " + message);
                }
                ExecuteCommand("procon.protected.send", "admin.yell", ((GameVersion == GameVersionEnum.BF4) ? (Environment.NewLine) : ("")) + message.ToUpper(), duration.ToString(), "all");
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while sending admin yell.", e));
            }
            Log.Debug(() => "Exiting adminYell", 7);
        }

        public void PlayerYellMessage(String target, String message)
        {
            PlayerYellMessage(target, message, true, 1);
        }

        public void PlayerYellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            Log.Debug(() => "Entering adminYell", 7);
            try
            {
                message = message.Trim();
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("message null in adminYell");
                    return;
                }
                var spambotMessage = false;
                if (message.Contains("[SpamBotMessage]"))
                {
                    message = message.Replace("[SpamBotMessage]", "");
                    spambotMessage = true;
                }
                const string bypassPrefix = "[whitelistbypass]";
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (displayProconChat)
                {
                    ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Yell[" + _YellDuration + "s] > " + Log.CBlue(target) + " > " + message);
                }
                for (int count = 0; count < spamCount; count++)
                {
                    ExecuteCommand("procon.protected.send", "admin.yell", ((GameVersion != GameVersionEnum.BF3) ? (Environment.NewLine) : ("")) + message.ToUpper(), _YellDuration.ToString(), "player", target);
                    Threading.Wait(50);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while sending admin yell.", e));
            }
            Log.Debug(() => "Exiting adminYell", 7);
        }

        public void AdminTellMessage(String message)
        {
            AdminTellMessage(message, true);
        }

        public void AdminTellMessage(String message, Boolean displayProconChat)
        {
            try
            {
                message = message.Trim();
                var spambotMessage = false;
                if (message.Contains("[SpamBotMessage]"))
                {
                    message = message.Replace("[SpamBotMessage]", "");
                    spambotMessage = true;
                }
                const string bypassPrefix = "[whitelistbypass]";
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (displayProconChat)
                {
                    ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Tell[" + _YellDuration + "s] > " + message);
                }
                AdminSayMessage(message, false);
                AdminYellMessage(message, false, 0);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running admin tell message.", e));
            }
        }

        public void PlayerTellMessage(String target, String message)
        {
            PlayerTellMessage(target, message, true, 1);
        }

        public void PlayerTellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            try
            {
                message = message.Trim();
                var spambotMessage = false;
                if (message.Contains("[SpamBotMessage]"))
                {
                    message = message.Replace("[SpamBotMessage]", "");
                    spambotMessage = true;
                }
                const string bypassPrefix = "[whitelistbypass]";
                while (message.Contains(bypassPrefix))
                {
                    message = message.Replace(bypassPrefix, "");
                }
                const string newlinePrefix = "[newline]";
                while (message.Contains(newlinePrefix))
                {
                    message = message.Replace(newlinePrefix, Environment.NewLine);
                }
                if (displayProconChat)
                {
                    ProconChatWrite(((spambotMessage) ? (Log.FBold(Log.CPink("SpamBot")) + " ") : ("")) + "Tell[" + _YellDuration + "s] > " + Log.CBlue(target) + " > " + message);
                }
                PlayerSayMessage(target, message, false, spamCount);
                PlayerYellMessage(target, message, false, spamCount);
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error running player tell message.", e));
            }
        }

        private void QueueMessageForParsing(AChatMessage messageObject)
        {
            Log.Debug(() => "Entering queueMessageForParsing", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue message for parsing", 6);
                    lock (_UnparsedMessageQueue)
                    {
                        _UnparsedMessageQueue.Enqueue(messageObject);
                        Log.Debug(() => "Message queued for parsing.", 6);
                        _MessageParsingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing chat message for parsing.", e));
            }
            Log.Debug(() => "Exiting queueMessageForParsing", 7);
        }

        private void QueueCommandForParsing(AChatMessage chatMessage)
        {
            Log.Debug(() => "Entering queueCommandForParsing", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue command for parsing", 6);
                    lock (_UnparsedCommandQueue)
                    {
                        _UnparsedCommandQueue.Enqueue(chatMessage);
                        Log.Debug(() => "Command sent to unparsed commands.", 6);
                        _CommandParsingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing command for parsing.", e));
            }
            Log.Debug(() => "Exiting queueCommandForParsing", 7);
        }
    }
}
